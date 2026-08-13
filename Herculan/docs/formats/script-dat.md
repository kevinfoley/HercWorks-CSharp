# `data\script.dat` — the real DBSIM gameplay handoff format

Reverse-engineered from `DBSIM.EXE` and `VSHELL.EXE` disassembly (Ghidra, `E:\ES2Stuff\tools\`),
2026-08-10, as a direct follow-on to [`msn-mission-file.md`](msn-mission-file.md). That session
established two things empirically: the full byte-exact `.msn` mission-file format, and — via
`DBSIM.EXE`'s own decompiled loader — that **DBSIM never reads `.msn` at all**. It only ever opens
`data\script.dat` and `data\mission.str`. `msn-mission-file.md`'s own "Reconciling against
`data\script.dat`" section flagged this file as the real target for anyone wanting DBSIM-side
(actual gameplay simulation) mission data, but — working from disassembly alone at the time —
guessed it was **"a separate, map-rendering-focused structure... not a subset/re-export of the
`.msn` arrays."**

**That guess was wrong, and this session found the real answer by finally decompiling the one
function `msn-mission-file.md` had flagged as "not yet decompiled" (`FUN_0041ac54`).** It turned out
to be exactly what that doc's call-chain notes speculated it might be: `data\script.dat`'s writer.
And it isn't an independent authoring format at all — **it's VSHELL serializing a filtered,
field-subset re-export of the same in-memory `.msn` row arrays `FUN_00417b67` (the `.msn` parser)
already populates**, straight out to a fixed literal path. Every one of `script.dat`'s 13
count-prefixed record blocks maps 1:1 to one of `msn-mission-file.md`'s 17 already-decoded rows.

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
  `ShellMap_Constructor` (`FUN_00423f43`), is a **second, independent reader** of the same file —
  VSHELL's own map-editor/UI view. It reads the identical 13-block structure in the identical order
  (confirmed block-by-block below), which is the strongest possible cross-check: two independently
  compiled consumers agreeing on record shape, the same kind of verification that made the `.msn`
  work trustworthy. Unlike DBSIM, `ShellMap_LoadScriptDat` skips (seeks past without storing) several
  blocks it doesn't need for map rendering, and keeps some blocks **in full** that DBSIM only reads
  one field out of — see the per-block table.
- Also confirmed (already known from `msn-mission-file.md`, restated here for completeness): the
  save-slot↔handoff copy mechanism, `sav\script%d.dat`↔`data\script.dat`.

## The core finding: `script.dat` is a filtered re-export, not an independent format

`WriteScriptDatFile` walks the *same* row-array globals `.msn`'s parser populates (identified by
matching global address, cross-referenced directly against `msn-mission-file.md`'s own table):

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

For every row except #4 and #17, the writer does a two-pass filter: count how many records have a
real GUID (offset `0x00` `!= -1`), write that count, then re-walk writing only those records' fields
— **in the same relative order, just with `GUID == -1` (deleted/inactive) records dropped.** Row #17
(which `msn-mission-file.md` already established has no GUID field at all) is written unconditionally,
all records, no filter. Row #4's export is different in kind — see block 13 below.

**Rows #1, #2, #3, and #5 never appear in `script.dat` at all** — the trigger/flag store, the
one-shot campaign-patch scratch pass, the variant-value table, and the skip-only row respectively.
Consistent with `script.dat` being a *runtime simulation* handoff: DBSIM doesn't need the
authoring-time condition/trigger-evaluation machinery `.msn` uses at load time — VSHELL already
resolved all of that when it filtered `.msn`'s rows down to whichever records survived their
condition checks, and `script.dat` only carries the survivors.

## Fixed-size file — real sample files confirmed this, with an explained anomaly

Real files: `ES2\DATA\script.dat` (the live file) plus 9 distinct save-slot snapshots in `ES2\SAV\`
(`script0.dat`–`script11.dat`; two pairs are byte-identical to each other and to the live file, so 9
genuinely distinct real files total). **Every one of these files is exactly 13,520 bytes**, despite
having wildly different real record counts per block (e.g. row #16's count ranges from 7 to 40
across the corpus) — this alone is a strong signal that `script.dat` is a **fixed-size preallocated
buffer**, not a tightly-packed variable-length file the way `.msn` is.

A byte-walker implementing the header + 13-block structure below (built the same way as the `.msn`
walker: read a count, consume `count × stride` bytes, repeat) was run against all 10 files. **Every
file's block sequence parses cleanly with small, sane counts and zero desync** — but only 1 of 10
(`script0.dat`) lands exactly on EOF; the other 9 stop short, with several thousand bytes unconsumed.

**This is not a walker bug — it's confirmed, byte-for-byte, to be stale leftover buffer content.**
The last 64 bytes of `script1.dat` (which the walker's real data leaves off at byte 7,216, with 6,304
bytes "unconsumed") are **byte-identical** to the last 64 bytes of `script0.dat` (whose real data
genuinely does extend to the full 13,520 bytes). The game evidently opens `script.dat` for writing
without truncating/zeroing the buffer first, so any file whose real content is shorter than a
previous, larger write leaves that earlier write's tail bytes sitting past the new logical end —
`script0.dat` just happens to be (at or near) the largest real write in this sample set, and every
shorter file's tail matches it exactly because they all trace back to the same stale buffer history.
**Read a `script.dat` file using the count-prefixed blocks below and stop — do not trust anything
past the last block's declared end, even though the file itself continues for a while.**

## The 13-block structure — verified against two independent real readers plus 10 real files

All three sources (writer `WriteScriptDatFile`, DBSIM reader `DBSim_LoadScriptDat`, VSHELL reader
`ShellMap_LoadScriptDat`) agree on this exact block order and every byte stride.

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

### How block 13 confirms row #4's "populated prefix, no gaps" assumption

The writer counts real (`!= -1`) entries only in the **first 10 shorts starting at row #4's storage
`+2`** (sub-array A, per `msn-mission-file.md`'s row #4 decode) and then blindly writes that many
*consecutive* shorts from the start — it does not skip gaps. This only produces a correct export if
sub-array A's real entries are always front-packed with no holes, which is exactly what
`msn-mission-file.md`'s real-data analysis of row #4 already found (1-4 of 10 slots populated, no
gap pattern observed). A nice independent confirmation of that earlier finding, found by reading the
writer rather than the original row #4 field decode.

## How this was verified

1. Decompiled `WriteScriptDatFile` (`FUN_0041ac54`) and `DBSim_LoadScriptDat` (`FUN_00424308`, plus
   its helper `FUN_00423104`) via Ghidra headless (`ES2DecompileNamed.java`), then matched every
   block's count/storage global against `msn-mission-file.md`'s own already-published table by exact
   global address — not by guessing from byte size alone (several rows share sizes, e.g. #4/#12 both
   144B, #6/#15 both 22B; address-matching avoids that trap the same way `msn-mission-file.md`
   already flagged as a risk).
2. Cross-checked every block's byte-consumption pattern between the writer and DBSIM's reader — for
   every block, the sequence of individual read/write call sizes matches exactly (e.g. block 5: both
   sides do `[2][2][16][20][20][10][2][2]` = 74 bytes, in the same field order), not just a matching
   total stride.
3. Decompiled `ShellMap_LoadScriptDat` (`FUN_004243d7`) as a **second, independently-compiled**
   reader and confirmed it walks the identical 13-block sequence in the identical order with matching
   strides for every block (differing only in which fields it keeps vs. seeks past) — the same
   "two independent implementations agree" cross-check that made the `.msn` macro-structure trustworthy.
4. Located real sample files (`ES2\DATA\script.dat` + 9 distinct `ES2\SAV\scriptN.dat` snapshots —
   the same save-slot mechanism `msn-mission-file.md` already documented) and ran a byte-walker
   implementing the table above against all 10. Confirmed clean, sane block counts with zero parse
   desync in every file, and root-caused the apparent "doesn't reach EOF" result in 9/10 files down
   to a specific, verified mechanism (stale trailing buffer content — see above), rather than treating
   it as an unexplained anomaly the way `.msn`'s one `DEMO2.MSN` outlier had to be left.

## The C# port — done, and round-trip verified against all 10 real files

`HercWorks.Core.Data.File.Msn.Script.ScriptDat` (model) and
`HercWorks.Core.Io.Transform.Common.ScriptDatTransformer` (reader/writer) implement the block table
above directly, replacing the old stale stub that guessed at an unrelated 20-field/coordinate-array
layout with no basis in the real format. Design choices, matching this project's existing
`MissionFile`/`.msn` port conventions:

- Blocks with a fully-confirmed byte layout (1-6, 10-13) are modeled with named fields per-record,
  reusing the exact field names from the corresponding `.msn` row's own model class wherever the
  writer's byte offsets line up with an already-named `.msn` field (confirmed field-for-field for
  blocks 10-13 — see the block table above for the byte math). Two blocks (5, 11) split what's a
  single interleaved span in the source `.msn` record into two parallel `ArrayA`/`ArrayB` arrays,
  because that's the writer's actual on-disk order (even source offsets in `ArrayA`, odd in
  `ArrayB`) — not the source record's own field order.
- Blocks 7-9 (the ones DBSIM only keeps one field out of) are modeled as `HeadBytes`/named
  field/`TailBytes` — the one confirmed field (`SpawnRecord144.SmallDiscrete`,
  `UnkEntity102Bytes.BinaryField`, `MiscEntityInfo.TypeLikeScalar`) is a real named property; every
  other byte is preserved raw rather than guessed at, the same "decode only what's confirmed,
  round-trip the rest raw" convention used throughout this project (e.g. `Action82.ConstantSpan`).
- The reader stops after block 13 and does not attempt to consume or preserve anything past it — no
  padding logic, no fixed-total-length assumption, matching the "treat as fixed-size, not
  EOF-delimited" guidance below.

**Verified**: read all 10 real files (`ES2\DATA\script.dat` + the 9 distinct `ES2\SAV\scriptN.dat`
snapshots) through `BytesToObject`, wrote each back out through `ObjectToBytes`, and confirmed the
re-encoded bytes match the original file's real-content prefix exactly (i.e. everything through the
end of block 13) for all 10/10 files — including `script0.dat`, whose real content fills the entire
13,520-byte buffer with zero slack.

## How to apply

- **Treat `script.dat` as fixed-size, not EOF-delimited.** Any reader/writer should stop after
  block 13's declared end and ignore trailing bytes — do not treat "bytes remain after the last
  block" as a parse error, and do not assume the file's on-disk length reflects real content length.
  A generator only needs to emit up through block 13; whether to pad to a fixed total size (and if so
  what governs that size — always 13,520 bytes across this sample set, but that could be tied to a
  fixed buffer allocation rather than a format constant) is an open question worth settling before
  writing a real byte-for-byte-compatible generator, though DBSIM's own reader doesn't care either way
  since it never reads past block 13. `ScriptDatTransformer` (see above) deliberately doesn't pad.
- **`msn-mission-file.md`'s "Reconciling against `data\script.dat`" section has been corrected in
  place** (its old "likely a map-rendering-focused derivative... independent format" guess is now
  marked superseded, with the original reasoning kept in a collapsed section for history) — that
  doc no longer needs a follow-up edit, it's done.
- **The two field-semantic findings from this doc have also been folded back into
  `msn-mission-file.md`'s own row #7 and row #16 decode sections** — row #7's payload is now
  documented there as a heading in degrees (not a generic discrete flag), and row #16's cross-ref
  array is documented there as DBSIM's entity-activation mechanism. Read this doc's own per-block
  table above for the full detail; the `.msn` doc's sections now just carry pointers back here.
- **Not yet chased**: the header's 20 bytes are only partially understood — one field (offset 2-3,
  passed to `FUN_00424b48` in DBSIM and to a similarly-placed call in VSHELL's `ShellMap`) is real and
  varies meaningfully across the 10 real files, but its exact meaning (mission/chapter id? a
  checksum? something else?) wasn't chased down, since it's read-only header metadata rather than
  gameplay data and didn't block confirming the record structure or building the C# port.
