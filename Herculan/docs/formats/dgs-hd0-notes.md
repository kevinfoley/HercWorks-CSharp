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
1. 3×`int16` id fields + 6 raw bytes (base header)
2. `int16` child count, then that many nested `ClassItem` records
3. `int16` count + that many 32-byte records (undecoded — BSP-plane-adjacent, per consumer `FUN_00476a1c`)
4. `int16` count + that many `int16` values (undecoded)
5. 5×`int16` scalars + a fixed 1024-byte block (undecoded)
6. if scalar 4 (sub-record count) ≠ 0: that many raw records sized by scalar 3

Every record's on-disk footprint (header+payload) pads to an even total.

**The key finding: every retail record's one child (step 2) is an ordinary TSObjectHeader-family
DTS chunk** — observed tag `0x0014000c` = `TSDetailPart`, byte-identical format to a plain `.DTS`
file's own chunks. No new mesh format was needed; `DTSModelTransformer` gained a public
`ReadOneObject(bytes, ref index)` entry point to parse it in place. Steps 3–6 are read (to keep the
cursor correct) but not modelled — the engine doesn't need them to draw.

**Verified against retail data:** an independent whole-file scan for the `0x02BC0001` tag pattern
finds the same record boundaries the sequential reader does (45/45 `BASES.DGS`, matching
`BaseTypeTable`'s 57 static types many-to-45 via shared `ShapeIndex`es). Every embedded child
parses through `DTSModelTransformer` with zero exceptions and produces real geometry: `BASES.DGS`
45/45 records, 1536 groups, 8978 polys; `BHULKS.DGS` 16/16 records, 113 groups, 786 polys.

Implementation: `HercWorks.Core.Io.Transform.Dbsim.BasesDgsTransformer`,
`HercWorks.Core.Data.File.Dgs.BaseShapeLibrary`. Wired into
`Herculan.Engine.Scene.SceneModelLibrary.Base()`.

## `.HD0`/`.HD1`/`.HD2`/`.HD3` — no loader found

No literal `"hd0"` / `"hd1"` / `".hd"` string in DBSIM.EXE. `.HD0` paired 1:1 with `.HB0`
cockpit-textures (`SAMSON.HD0`/`SAMSON.HB0`, etc.). Likely approach: trace `.HB0`'s confirmed
loader (herc-name + extension, like `.GAU`/`.DMG`) to find `.HD0` sibling.

Previous finding (hex-only, pre-Ghidra): long run of `[UINT16][UINT16]` pairs (one counts up,
one counts down) — suggestive of gradient/remap table; only extant evidence.
