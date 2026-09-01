# .DGS shape library

The `.DGS` container and the structure shapes it holds. Companion:
[`weapons-dat-sim.md`](weapons-dat-sim.md). The `.HD0`-`.HD3` / `.ED0`-`.ED3` clip-region files this
file's name also mentions are documented in [`cockpit-hud.md`](cockpit-hud.md), which owns that
format, its loader and its real-file verification.

## `.DGS` — SOLVED

`BASES.DGS`/`BHULKS.DGS`: a flat sequential list of `ClassItem`-tagged records — **not** the same
container as `.DTS`, despite `BASES_AN.DTS` and `BASES.DGS` both starting with a 4-byte value that
resembles `recordSize<<16|version`.

**Container.** Each record: `[classId:int32 LE][payloadSize:int32 LE]` + payload. `classId` for
this library is `0x02BC0001` (= the record's own leading 4 on-disk bytes). Read via the generic
polymorphic `ClassItem_LoadResource` (`0047a038`) registry dispatch — same mechanism as `.DFN`/
`.DCI` (see `project_es2_exe_recon` memory), different registered class. `BaseType_LoadShape`
(`00405ebc`) → `FUN_00474cd8` walks this list sequentially by index (not random-access) to resolve
`dat\BASES.DAT`'s `ShapeIndex`.

**Record layout** (traced via the class's Watcom base-constructor chain — `FUN_0042762c` →
`FUN_00490d5c` → `FUN_0048fd94` → `FUN_0048f894`):
1. 3×`int16` head fields + 6 raw bytes (base header). The **third is the shape's bounding radius**
   — see [`../simulation/hit-detection.md`](../simulation/hit-detection.md).
2. `int16` child count, then that many nested `ClassItem` records
3. `int16` count + that many 32-byte records (undecoded — BSP-plane-adjacent, per consumer `FUN_00476a1c`)
4. `int16` count + that many `int16` values (undecoded)
5. the shape's **collision volume**: 5×`int16` scalars, a 1024-byte height table, then one row of
   height codes per grid row. Full layout and queries in
   [`../simulation/hit-detection.md`](../simulation/hit-detection.md).

Every record's on-disk footprint (header+payload) pads to an even total.

**The key finding: every retail record's one child (step 2) is an ordinary TSObjectHeader-family
DTS chunk** — observed tag `0x0014000c` = `TSDetailPart`, byte-identical format to a plain `.DTS`
file's own chunks. No new mesh format was needed; `DTSModelTransformer` gained a public
`ReadOneObject(bytes, ref index)` entry point to parse it in place. Steps 3–4 are read to keep the
cursor correct but not modelled; step 5 is modelled and is what makes a building solid.

**Verified against retail data:** an independent whole-file scan for the `0x02BC0001` tag pattern
finds the same record boundaries the sequential reader does (45/45 `BASES.DGS`, matching
`BaseTypeTable`'s 57 static types many-to-45 via shared `ShapeIndex`es). Every embedded child
parses through `DTSModelTransformer` with zero exceptions and produces real geometry: `BASES.DGS`
45/45 records, 1536 groups, 8978 polys; `BHULKS.DGS` 16/16 records, 113 groups, 786 polys.

### Shape origin

**A shape's origin is its ground contact point, not a rig pivot.** Measured across the libraries:
44 of the 45 `BASES.DGS` shapes and all eight `BASES_AN.DTS` roots have their lowest vertex at
exactly y=0. The exception is shape 28 (base type 38, an elevated span), whose geometry starts
10.8 render units up because the structure is meant to stand clear of the terrain.

The HERC roster is the same rule: every root 0 sits at y=0 except COLOSSUS, which dips 2.4 render
units (400 world units) and is also the one HERC with a 400-unit ride height — the same correction
(see [`dts-node-posing.md`](dts-node-posing.md)).

So a placed structure is drawn at terrain height with no vertical correction of any kind. Raising an
object by its mesh's lowest point is a no-op on every shape but 28, which it drags down onto the
ground — visible against retail in `Reference/Building_comparison.png`.

Implementation: `HercWorks.Core.Io.Transform.Dbsim.BasesDgsTransformer`,
`HercWorks.Core.Data.File.Dgs.BaseShapeLibrary`. Wired into
`Herculan.Engine.Scene.SceneModelLibrary.Base()`.
