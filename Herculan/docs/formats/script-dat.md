# `data\script.dat` — the real DBSIM gameplay handoff format

**Format summary:** A GUID-filtered, field-subset re-export of the same in-memory `.msn` row arrays populated by `FUN_00417b67` (the `.msn` parser). Written by `FUN_0041ac54` (`WriteScriptDatFile`, VSHELL) immediately after `.msn` parsing completes. DBSIM reads `script.dat` (not `.msn`) for actual gameplay simulation. Every one of `script.dat`'s 13 count-prefixed record blocks maps 1:1 to one of [`msn-mission-file.md`](msn-mission-file.md)'s 17 already-decoded rows.

## Call chain — confirmed

- `WriteScriptDatFile` (`FUN_0041ac54`, VSHELL, source `msn_gen.cpp`) is called from
  `FUN_0041c73d` **right after** `FUN_00417b67` finishes parsing a `.msn` file (see
  `msn-mission-file.md`'s call-chain section) — i.e. this runs once per mission load, immediately
  after the `.msn` row arrays are populated in memory. It opens the literal path `data\script.dat`
  and writes out 13 count-prefixed record blocks plus a fixed 20-byte header, entirely from the same
  `DAT_0047xxxx` globals `FUN_00417b67` fills. Renamed from `maybe_LoadScriptDatFile` now that it's
  fully traced (see `known_symbols.json`).
- `DBSim_LoadScriptDat` (`FUN_00424308`, DBSIM) is the real simulation-side reader — opens
  `data\player.mec`, `data\mission.str`, and `data\script.dat` (in that order) during world init,
  and reads the exact same 13-block structure `WriteScriptDatFile` writes, in the same order, with
  matching byte strides for every block.
- `ShellMap_LoadScriptDat` (`FUN_004243d7`, VSHELL, source `shellmap.cpp`), called from
  `ShellMap_Constructor` (`FUN_00423f43`), is a **second, independent reader** — VSHELL's map-editor/UI.
  It reads the identical 13-block structure in identical order, but selectively keeps blocks depending
  on what the UI needs (see per-block table). Cross-check: two independently compiled readers agree on
  identical record shape and strides.

## Row mapping

`WriteScriptDatFile` walks the *same* row-array globals `.msn`'s parser populates (cross-referenced by global address against `msn-mission-file.md`'s table):

| `.msn` row | count global | storage global | on-disk stride (`.msn`) |
|---|---|---|---|
| #4 (no stable name) | `DAT_00470668` | `DAT_00470640` | 144B |
| #6 `MapPoint22` | `DAT_0047064e` | `DAT_0047060c` | 22B |
| #7 `Flag10` | `DAT_00470650` | `DAT_00470610` | 10B |
| #8 `WaypointGroup` | `DAT_00470656` | `DAT_0047061c` | variable |
| #9 `LinkOrReward12` | `DAT_0047065e` | `DAT_0047062c` | 12B |
| #10 `Action82` | `DAT_00470660` | `DAT_00470630` | 82B |
| #11 `ActionPair30` | `DAT_00470662` | `DAT_00470634` | 30B |
| #12 (144B type) | `DAT_00470652` | `DAT_00470614` | 144B |
| #13 `UnkEntity102Bytes` | `DAT_00470654` | `DAT_00470618` | 102B |
| #14 `MiscEntityInfo` | `DAT_0047065c` | `DAT_00470628` | 62B |
| #15 `LinkedRef22` | `DAT_00470658` | `DAT_00470620` | 22B |
| #16 `UnkEntity164Bytes` | `DAT_0047065a` | `DAT_00470624` | 164B |
| #17 `LinkedRef58` | `DAT_0047064a` | `DAT_00470608` | 58B |

**Filtering:** All rows except #4 and #17 apply a two-pass GUID-filter (rows with offset `0x00 == -1` are dropped). Row #17 (no GUID field) is written unconditionally. Row #4 is exported differently (see block 13 below).

**Omitted rows:** #1 (trigger/flag store), #2 (campaign-patch scratch), #3 (variant-value table), #5 (skip-only). Not needed at runtime — VSHELL filters conditions during `.msn` load, `script.dat` carries only survivors.

## Fixed-size file structure

Real files: `ES2\DATA\script.dat` (the live file) plus 9 distinct save-slot snapshots in `ES2\SAV\`
(`script0.dat`–`script11.dat`; two pairs are byte-identical to each other and to the live file, so 9
genuinely distinct real files total). **Every one of these files is exactly 13,520 bytes**, despite
having wildly different real record counts per block (e.g. row #16's count ranges from 7 to 40
across the corpus) — this alone is a strong signal that `script.dat` is a **fixed-size preallocated
buffer**, not a tightly-packed variable-length file the way `.msn` is.

All 10 real files parse cleanly with zero desync in the count-prefixed block sequence, but only 1
lands exactly on EOF; the others have several thousand stale trailing bytes (verified by byte-matching
against `script0.dat`'s tail — the file is reused without truncation between writes). **Read only through
block 13's declared end; ignore trailing bytes.**

## The 13-block structure

| # | `.msn` row | on-disk shape | GUID-filtered? | DBSIM keeps | VSHELL `ShellMap` keeps |
|---|---|---|---|---|---|
| header | — | fixed 20 bytes, 10 shorts from unrelated `DAT_004854xx` globals (not part of the 17-row `.msn` table at all) | — | parses into several scalar fields, one passed on to a later call | parses into several scalar fields, one passed on to a later call |
| 1 | #6 `MapPoint22` | count + count×12B (X,Y,Z int32 triple only — GUID/condition/etc. dropped) | yes | full (min/max bbox tracked live as read) | full |
| 2 | #7 `Flag10` | count + count×2B (the `0x08` payload short only) | yes | full, **× 182 (`0xb6`) at load** — the same degrees→BAM conversion already confirmed elsewhere in DBSIM; this reframes row #7's payload as **a heading/orientation in degrees**, not a difficulty tier | full, **not** multiplied (VSHELL just displays/edits it) |
| 3 | #8 `WaypointGroup` | count + per record: nested-count (2B) + nested-count×2B (waypoint refs into block 1) | yes | full, nested refs resolved to block-1 pointers (stride 3 ints) | full, same resolution |
| 4 | #9 `LinkOrReward12` | count + count×6B (`0x06` type flag, `0x08` ref1, `0x0A` ref2/literal) | yes | full, resolved into a 10-byte in-memory record | **skipped** (seek past, discarded) |
| 5 | #10 `Action82` | count + count×74B (`0x06` type, `0x08` verb, `0x0A`-`0x19` ref[0..7] into row 9, `0x1C`/`0x1E`-stride interleaved 20-short span, `0x44`-`0x4D` herc-LUT ref[0..4], `0x4E` secondary, `0x50` target) | yes | reads all 74B but only **keeps** type, verb, the 8 refs (resolved to row-9 pointers), the 40-byte interleaved span, secondary (decremented by 1), and target — **the herc-LUT refs are read then discarded**, DBSIM has no use for the cosmetic/economy LUT | **skipped** (seek past, discarded) |
| 6 | #11 `ActionPair30` | count + count×24B (`0x06` ref into row10, `0x08` type/timer, `0x0A`-`0x1D` 10-slot ref array into row10) | yes | full, resolved via `DBSim_BuildActionPairRecord` (`FUN_00423104`) into target+type+10×ref | **skipped** (seek past, discarded) |
| 7 | #12 (144B type) | count + count×134B (`0x08`-`0x2F` 40B span, `0x30` `SmallDiscrete`, `0x32`-`0x45` 20B span, `0x46`/`0x48` 2 shorts, two 20-short interleaved spans, `0x74`-`0x87` 20B span, 4 trailing shorts; `SmallDiscrete2` at `0x4A` is the one field of row #12 skipped/not exported) | yes | reads all 134B but keeps **only `SmallDiscrete` (`0x30`)** — confirmed via the writer's own assert string on this field ("Invalid mech type") — this is DBSIM's "spawn a unit of this mech type" mechanism | **full 134B kept** — the map editor needs the whole record (name, position refs, etc.) to render/edit a placed unit |
| 8 | #13 `UnkEntity102Bytes` | count + count×92B (`0x08`-`0x33` `FlagsA`+refs, `0x34` `BinaryField`, `0x38`-`0x5F` `FlagsB`, `0x60`-`0x64` refs+`UnkVal_100`; `Unk36` at `0x36` is skipped/not exported) | yes | reads all 92B but keeps only **`BinaryField` (`0x34`)** | **skipped** (seek past, discarded) |
| 9 | #14 `MiscEntityInfo` | count + count×52B (`0x08` `TypeLikeScalar`, `0x0A`-`0x3D` refs+`SparseBlock`+`TrailingField`) | yes | reads all 52B but keeps only **`TypeLikeScalar` (`0x08`)** (matches `msn-mission-file.md`'s row #14 decode: `TypeLikeScalar` correlates ~99% with the trailing `100`/`0` constant) | **full 52B kept** |
| 10 | #15 `LinkedRef22` | count + count×14B (`0x08`-`0x14`, the 7 fields `msn-mission-file.md` decoded as small-int/refs/discriminator) | yes | reads all 14B and **discards it entirely** — DBSIM has no use for the "attach route to entity" authoring metadata | **full 14B kept** — this is exactly the UI-relevant "which route/position/entity is this linked to" data a map editor needs |
| 11 | #16 `UnkEntity164Bytes` | count + count×156B (two 40B/20B spans, a 20-entry nested cross-ref array with a 3-way discriminator, trailing shorts) | yes | full — **this is DBSIM's entity-activation mechanism**: for each populated cross-ref entry, the discriminator (0/1/2) marks the referenced block-1/block-2/block-9 slot as a *live, simulated* object (via `DAT_004aa7ae`/`DAT_004aa8da`/`DAT_004aa93e`+`DAT_004aaa56` flag arrays), turning static declared entries into things DBSIM actually spawns/simulates | full 156B kept, cross-refs resolved to annotate the kept row-#14 records (a UI/display-oriented resolution, not the "activation" one) |
| 12 | #17 `LinkedRef58` | count (unfiltered — all records, matching row #17's "no GUID field" nature) + count×54B | **no** | reads all 54B and **discards it entirely** | **skipped** (seek past, discarded) |
| 13 | #4 (no stable name) | flat tail: **one** count (how many of row #4's 10-slot sub-array A are populated, from its front — assumes no gaps) + that many×2B (the populated LUT-ref prefix itself) | n/a — single mission-level record, not a per-entity array | full — **this is the mission's herc/weapon unlock package** reaching DBSIM, matching `msn-mission-file.md`'s row #4 "working model: per-mission reward/unlock package" | not read (VSHELL's `ShellMap` reader stops after block 12; it has no use for player loadout data) |

## Verification

Two independent real readers (`DBSim_LoadScriptDat` and `ShellMap_LoadScriptDat`) agree on identical block order and strides. Byte-walker tested against 10 real files (`ES2\DATA\script.dat` + 9 distinct save-slot snapshots); all parse cleanly with zero desync.

## Header format

| offset | meaning |
|---|---|
| 0 | theater index, 0-4 — selects `wld\world<index * 2 + variant>.wld` (texture bank, palette) |
| 2 | zone index — passed to `Terrain_LoadZone` |
| 4 | (zeroed by reader before use) |
| 18 | theater variant, 0 or 1 — low bit of world number |
| rest | constant across corpus |

All three are confirmed by `DBSim_LoadScriptDat` → `Terrain_LoadZone` / `maybe_World_LoadTheater`. See [`terrain-texturing.md`](terrain-texturing.md) for theater details.

## Reading script.dat

Stop after block 13's declared end and ignore trailing bytes. Files may have stale buffer content past the real data (see "Fixed-size file" section). DBSIM's reader ignores trailing bytes; padding is not required.

## Implementation

- `HercWorks.Core.Data.File.Msn.Script.ScriptDat` (model) + `HercWorks.Core.Io.Transform.Common.ScriptDatTransformer` (reader/writer) — round-trip verified byte-exact against all 10 real files (through end of block 13). Deliberately does not pad.
- `Herculan.Engine.World.ScriptDatHeader` — the engine-side header port.
- Blocks 7-9 are modeled `HeadBytes`/named field/`TailBytes`: only the one confirmed field is named, the rest round-trips raw. Blocks 5 and 11 split an interleaved source span into parallel `ArrayA`/`ArrayB` (even source offsets in A, odd in B) to match the writer's on-disk order.
