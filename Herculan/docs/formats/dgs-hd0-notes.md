# .DGS and .HD0 formats

Partial investigation of `.DGS` and `.HD0` formats. Companion: [`weapons-dat-sim.md`](weapons-dat-sim.md).

## `.DGS` — SOLVED (2026-08-14)

`BASES.DGS`/`BHULKS.DGS`: a flat sequential list of `ClassItem`-tagged records — **not** the same
container as `.DTS`, despite `BASES_AN.DTS` and `BASES.DGS` both starting with a 4-byte value that
looked like `recordSize<<16|version` (an earlier read of this doc's own guess, now corrected).

**Container.** Each record: `[classId:int32 LE][payloadSize:int32 LE]` + payload. `classId` for
this library is `0x02BC0001` (= the record's own leading 4 on-disk bytes). Read via the generic
polymorphic `ClassItem_LoadResource` (`0047a038`) registry dispatch — same mechanism as `.DFN`/
`.DCI` (see `project_es2_exe_recon` memory), different registered class. `BaseType_LoadShape`
(`00405ebc`) → `FUN_00474cd8` walks this list sequentially by index (not random-access) to resolve
`dat\BASES.DAT`'s `ShapeIndex`.

**Record layout** (traced via the class's Watcom base-constructor chain — `FUN_0042762c` →
`FUN_00490d5c` → `FUN_0048fd94` → `FUN_0048f894`):
1. 3×`int16` head fields + 6 raw bytes (base header). The **third is the shape's bounding radius**
   — see [`../simulation/structure-hit-detection.md`](../simulation/structure-hit-detection.md).
   An earlier read of this doc called all three "id fields".
2. `int16` child count, then that many nested `ClassItem` records
3. `int16` count + that many 32-byte records (undecoded — BSP-plane-adjacent, per consumer `FUN_00476a1c`)
4. `int16` count + that many `int16` values (undecoded)
5. the shape's **collision volume**: 5×`int16` scalars, a 1024-byte height table, then one row of
   height codes per grid row. **Corrects this doc's earlier reading** of steps 5–6 as "5 undecoded
   scalars + an opaque block" followed by "sub-record count × sub-record size raw records" — that
   walk consumed exactly the same bytes, so every retail record parsed correctly while all of it was
   named wrongly. Full layout and queries in
   [`../simulation/structure-hit-detection.md`](../simulation/structure-hit-detection.md).

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

Implementation: `HercWorks.Core.Io.Transform.Dbsim.BasesDgsTransformer`,
`HercWorks.Core.Data.File.Dgs.BaseShapeLibrary`. Wired into
`Herculan.Engine.Scene.SceneModelLibrary.Base()`.

## `.HD0`-`.HD3` / `.ED0`-`.ED3` — SOLVED (2026-08-17)

Fully decoded and paired with `.HB0`/`.HB1`/`.HB2` after all. See
[`cockpit-hud.md`](cockpit-hud.md) for the format, the loader, and real-file verification.

They are the **per-view 3D-viewport clip regions** for the player's cockpit: a rect list plus
per-scanline span blocks defining exactly which columns the 3D scene shows through the canopy. One
file per view, matching one canopy bitmap per view (`hd0`↔`hb0`, and so on). `.ED*` is the 320-wide
set, `.HD*` the 640-wide one.

Two earlier readings in this file were wrong and are corrected there:

- The owning object (`004d2544`) is not a mech-inspection display. It is
  `CockpitViewManagerInstance`, the player's own cockpit view manager, and it owns
  `CockpitViewInstance` rather than being unrelated to it.
- The files are directly paired with the `.HB*` canopy art, not unrelated to it.

The pre-Ghidra hex observation in this file — "long run of `[UINT16][UINT16]` pairs, one counting up,
one counting down" — was the span blocks: `xStart` rising and `xEnd` falling row by row as the canopy
frame narrows inward. Not a gradient or remap table.
