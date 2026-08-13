# .MSN mission file (ZONES.VOL/MSN/*.msn) and its VSHELL load path

Reverse-engineered from `VSHELL.EXE` disassembly (Ghidra, `E:\ES2Stuff\tools\`), 2026-08-10, then
**empirically verified against all 62 real retail `.MSN` files** (extracted from `ES2/VOL/ZONES.VOL`
via a throwaway `HercWorks.Vol` probe — that VOL had never been extracted locally before this
session). Previously `MissionFile.cs`/`MissionFileTransformer.cs` were a literal, unverified port of
the Java project's own guesswork (explicitly hardcoded against a single file, `TRAIN5.MSN`, per its
own source comments, and known to be structurally wrong — see below).

**The macro-structure (revision field + all 17 array/skip rows, in order, with exact byte strides)
is now confirmed, not hypothesized: a walker built from the table below reproduces the exact file
length of 61 of the 62 real `.MSN` files, landing precisely on EOF with zero slack.** The one
outlier, `DEMO2.MSN`, undershoots by 42 bytes — not yet root-caused, see "How to apply." Field-level
*meaning* is now resolved for 14 of the 17 rows (plus row #17's tail), against real data the same
rigorous way each time; the remaining 3 rows (#2, #5) need no further decoding — see "How to apply."

## Call chain — confirmed

- `FUN_0044d5bd` builds the path `msn\<name>.msn` (string `"msn\\%s.msn"` at `0047a2a6`,
  `%s` = a name substituted from `DAT_0048dc18+0x45`, with `^` sanitized to `_`), then calls
  `FUN_0041c73d(path)`.
- `FUN_0041c73d` (asserts trace to `msn_gen.cpp`) calls `FUN_00417b67(param_1)` **first, with the
  `.msn` path** — this is the real raw-file parser, see below. Only afterward does it separately
  open the fixed literal path `data\script.dat` via `FUN_0041ac54` and touch a `DAT_00470640`
  buffer copy — this looks like a **second, later stage** consuming already-loaded state rather
  than reopening the `.msn` file itself; not yet traced further.
- Separately, `ShellMap::ctor` (`FUN_00423f43`, vtable `&PTR_FUN_004721b0`) opens `data\mission.str`
  and `data\maplabel.str` as two independent string tables, then calls `FUN_004243d7` (source
  `shellmap.cpp`) which reads `data\script.dat` directly and builds a *different* set of typed
  arrays (coordinate list, several small records, a big cross-referenced 0x9c/156-byte "part" type
  with 0x86/134-byte and 0x34/52-byte and 0xe/14-byte sibling arrays) — **not yet reconciled with
  the `.msn`-side arrays below**; may be a UI-facing simplification of the same data, or an
  unrelated map-rendering structure. Worth resuming from here next.
- Also confirmed (from `FUN_00412a71`/`FUN_00412bbf`, source `career.cpp`): the literal
  save-slot↔handoff copy mechanism — `sav\script%d.dat`↔`data\script.dat`,
  `sav\missn%d.str`↔`data\mission.str`, `sav\player%d.mec`↔`data\player.mec` — matches the old dev
  note in `herc-works-mdk-main/docs/arch/3space_filetypes_sav.txt` exactly, now with real function
  addresses.

## `FUN_00417b67` — the raw `.MSN` parser

Opens the stream (`FUN_00402ad9`), then **asserts a revision field equals `5`** (2-byte value,
right after open — mirrors the same revision-check pattern already known from `Volume::loadVolume`).
One scratch pass follows (a `DAT_00470648`-counted loop reading fixed 0x52/82-byte chunks into a
*reused* buffer, applying effects via `FUN_00416379` rather than storing an array — looks like a
one-shot campaign-override/patch application, not a persistent entity list), then a long sequence of
`[uint16 count] → array of fixed-size records` reads. Every record array goes through a shared
condition-filter helper (`FUN_00417610`, or a couple of specialized siblings) that **compacts the
array in place**, dropping records whose condition fails — i.e. this function is doing campaign-
state-aware filtering *while* loading, not a flat/passive parse.

### The condition/trigger system (this resolves a long-standing "unknown" from the Java doc comment)

The first record type's type-0 branch is a `switch` on the value `0x119`–`0x11e`:

| code | operator |
|------|----------|
| 0x119 | `==` |
| 0x11a | `!=` |
| 0x11b | `<`  |
| 0x11c | `<=` |
| 0x11d | `>`  (operands swapped) |
| 0x11e | `>=` (operands swapped) |

Each compares a value against `DAT_00482af8[recordField]` — a global flag/counter array, almost
certainly the campaign-progress flag store (the herc-unlock/weapon-unlock flag system the original
`MissionFile.cs` doc comment predicted but never located). Record types 1–3 use different evaluator
functions (`FUN_004659ec`, `FUN_004159d0` — a `-99`-sentinel-or-range-check, `FUN_00417610` again)
— plausibly other trigger-condition flavors (dialogue/event flags, numeric range checks) rather than
pure flag comparisons.

**Byte-level decode, confirmed against all 2,152 real row #1 records across the 62-file corpus:**

| offset | field | real-data findings |
|---|---|---|
| `0x00` | ordinal/index | **never read by any code in this record's own load loop** (unlike every other row, where the analogous field is the GUID used for dedup — row #1 has no dedup step at all, consistent with nothing else in the file referencing a row #1 record by this field). Real values are small and mostly sequential per file (`0,1,2,3,...`), suggesting an authoring-tool bookkeeping index rather than a runtime-consumed key |
| `0x02` | condition input | fed to the type-specific evaluator for **every** type (0/1/2/3). Real usage: 933/2,152 (43%) have a genuine value — by far the highest real-usage rate of the condition mechanism found anywhere in this file, consistent with row #1 being the trigger array other records' conditions ultimately point into (entries here can gate on *each other*) |
| `0x04` | type discriminator | real distribution: `0` (comparison-operator branch, 1,016/47%), `2` (`FUN_00415d90` range-check branch, 847/39%), `3` (condition-only, 257/12%), `1` (`FUN_004659ec` branch, 32/1.5%) — all four declared types are genuinely exercised, unlike most discriminators found elsewhere in this file |
| `0x06` | flag-index (type `0`) / range-lower (type `2`) / evaluator param (type `1`) | real values cluster on round increments of 50 (`0, 8, 9, 10, 50, 100, 150, 250, ...`) — consistent with `DAT_00482af8` being organized in blocks (plausibly per-campaign-chapter flag banks) rather than one flat array |
| `0x08` | operator code / range-upper / result (overwritten in place) | for type `0`, holds the `0x119`-`0x11e` opcode on input, then gets overwritten with the boolean `0`/`1` result — real opcode distribution is dominated by `0x11d`/`285` (`>`, swapped, 707) and `0x119`/`281` (`==`, 259). For type `2`, real values track `0x06` at a consistent `+49` offset (e.g. `0`/`49`, `50`/`99`, `100`/`149`, `150`/`199`) — a clean confirmation these are `[lower, upper]` range-check bucket pairs, most likely per-chapter flag banks |
| `0x0A` | comparison operand (type `0` only) | mostly `0` (1,533/71%); otherwise small integers or round numbers (`1, 2, 3, 4, 100, 200, 400`) |
| `0x0C` | ? | **always `0`** in all 2,152 real records — fully dead, same shape as dead fields elsewhere in this file |

**Working model:** row #1 is the shared flag/condition-store every other row's `0x02`-style
condition field points into. Its own `0x02` field lets trigger entries chain off each other. Type
`0` (plain comparison) and type `2` (range-bucket check) together account for 86% of real entries;
the `+49`-offset pairing on type `2` strongly suggests `DAT_00482af8` is laid out in fixed-size
banks (blocks of 50) rather than being a flat, unstructured flag array — useful context for anyone
who eventually needs to map specific flag indices to specific campaign events.

### The template-inheritance pattern

Most record types carry a "parent index" field: `-1` means "read this record's fields fresh from
the stream," any other value means "`memcpy` the already-loaded record at that index instead,"
sometimes with additional per-field overrides layered on top. This is a real, load-time
prototype/inheritance mechanism — missions can define an entity as "like entity N, but with these
fields changed" — not something the current C# port models at all.

### Record-array table — **empirically confirmed byte-exact against 61/62 real `.MSN` files**

Two corrections versus the first disassembly-only pass (caught by building a strict byte-walker and
testing it against every real file — see "How this was verified" below):

- A **skip-only row** (`DAT_0047066a`) sits between the `UnitInfo` array (#4) and the 22-byte array
  (now #6) — it reads a count, then seeks forward `count * 0x40` (64) bytes **without storing
  anything**. Easy to miss reading the decompiled code linearly since it looks like ordinary array
  setup at a glance.
- The nested-array row (#8) is **not** `count * 18` bytes as the in-memory struct size implied.
  Per record it's 10 fixed bytes (5 shorts, the 5th being a nested-entry count), followed by that
  many nested entries — but **each nested entry only consumes 2 bytes on disk**, not the 6 bytes its
  in-memory slot is allocated as. The other 4 bytes of each in-memory slot are zero-initialized
  locally, never read from the file. Missing this caused total desync a few hundred bytes in.

| # | count global | on-disk shape | storage global | cross-refs into | best current guess |
|---|---|---|---|---|---|
| 1 | `DAT_0047064c` | 14 (`0xe`) bytes/record | `DAT_00470604` | `DAT_00482af8` (flags), self (via `0x02`) | **decoded — see "The condition/trigger system" below, now with byte offsets.** `UnkHeaderEntry` — the campaign trigger/flag-comparison record; real usage is heavy (43% use a real condition, unlike almost every other row) |
| 2 | `DAT_00470648` | 82 (`0x52`) bytes/record | *(scratch, not stored)* | — | one-shot campaign override/patch application, no persistent C# equivalent yet |
| 3 | `DAT_00470666` | 8 bytes/record | `DAT_0047063c` | referenced by #4, #16, #17 | **decoded — see "Row #3 field decode" below.** A small campaign-variant value lookup: GUID + condition + payload, where the same GUID can carry several condition-gated payload variants |
| 4 | `DAT_00470668` | 144 (`0x90`) bytes/record | `DAT_00470640` | 3 sub-arrays (10, 30, 30 shorts) into a shared LUT (`DAT_00470664`); 1 ref into #3 | **decoded — see "Row #4 field decode" below.** No GUID/identity field at all (offset `0x00` is the condition ref instead) — refutes the existing C# `UnitInfo` hypothesis outright, not just its sub-array split; nothing else in the file references this row |
| 5 | `DAT_0047066a` | **skip-only**, `count * 0x40` bytes, nothing stored | — | — | genuinely unread/unmodeled data — the game itself skips it at this load path; may only matter to DBSIM, not VSHELL |
| 6 | `DAT_0047064e` | 22 (`0x16`) bytes/record | `DAT_0047060c` | self (inherit/compose) | **decoded — see "Row #6 field decode" below. A 3D world-position/waypoint record** (`MapPoint22`): GUID + 3 dead fields + an int32 X/Y/Z triple. This is the record every row #9 link/reward ref, and several other rows' refs, ultimately resolve to |
| 7 | `DAT_00470650` | 10 bytes/record | `DAT_00470610` | self | **decoded — see "Row #7 field decode" below.** A minimal record: GUID + 3 fully-dead fields + one small discrete payload (`0`/`1`/`10`) — the simplest record type in the file, unreferenced by anything else |
| 8 | `DAT_00470656` | **variable**: 10 fixed bytes/record + (nested-count × 2) bytes | `DAT_0047061c` | #6 (nested entries) | **decoded — see "Row #8 field decode" below.** A named, orderable list of row #6 world positions (`WaypointGroup`) — a patrol route/waypoint chain, with real evidence of both spatial coherence and closed-loop (patrol circuit) structure |
| 9 | `DAT_0047065e` | 12 (`0xc`) bytes/record | `DAT_0047062c` | #6, self | **decoded — see "Row #9 field decode" below.** A typed dual-purpose record: a GUID-pair "link" (two refs into row #6) when its type flag is 0, or a single row-#6 ref plus a round-number literal (likely a credit/reward value) when the flag is 1 |
| 10 | `DAT_00470660` | 82 (`0x52`) bytes/record | `DAT_00470630` | #9 (8 shorts), LUT `DAT_00470664` (5 shorts) | referenced later by a **4-way type-discriminated remap** (codes 7/8/9/10 → #12/#13/#14/#16) — strong candidate for an "action/objective" record |
| 11 | `DAT_00470662` | 30 (`0x1e`) bytes/record | `DAT_00470634` | #10 (once) + #10 again (10 shorts) | **decoded — see "Row #11 field decode" below.** The nominal 10-slot "sequence" array is a red herring in practice: **96% of real records use at most 1 of its 10 slots**, same "declared capacity, barely used" pattern as row #10's own sub-arrays. Functionally an action-to-action pairing, not a multi-step sequence |
| 12 | `DAT_00470652` | 144 (`0x90`) bytes/record | `DAT_00470614` | #6, #7, #10 (×2) — all declared but nearly dead in retail (≤2.4% used); real payload is an unresolved 10-slot array | **decoded — see "Row #12 field decode" below.** A second, distinct 144-byte type from #4; heaviest template-inheritance usage of any decoded row (48%) |
| 13 | `DAT_00470654` | 102 (`0x66`) bytes/record | `DAT_00470618` | #6, #7 (both declared, both dead in retail), #10 (×2, only the 2nd slot real) | **decoded — see "Row #13 field decode" below.** `UnkEntity102Bytes` — real structure is a 20-flag boolean array + a mostly-inert second 20-slot span + a constant trailing field (always `100`), not the flat `Flags[49]` the old hypothesis assumed; the macro pass's "inherit only" note missed all four real cross-refs |
| 14 | `DAT_0047065c` | 62 (`0x3e`) bytes/record | `DAT_00470628` | #6, #7, #10 (×2) | **decoded — see "Row #14 field decode" below.** `MiscEntityInfo` — 4 real cross-refs, not the 3 the macro pass found (it missed #7); a type-like field at `0x08` correlates ~99% with the trailing constant field being `100` vs `0` |
| 15 | `DAT_00470658` | 22 (`0x16`) bytes/record | `DAT_00470620` | #6 (rare), #8 (dominant — 94% populated), #10 (rare), plus a **4-way** discriminated ref (0/1/2/3 → #16/#12/#13/#14, resolved in two passes since #16 loads after #15) | **decoded — see "Row #15 field decode" below.** A "typed link" record whose primary payload is a near-always-populated ref into row #8 — confirms it's structurally distinct from #6 (which is a flat position record), not just size-coincidentally 22 bytes |
| 16 | `DAT_0047065a` | 164 (`0xa4`) bytes/record | `DAT_00470624` | #6, #7, #8, #10, a **20-entry** discriminated-ref array (0/1/2 → #12/#13/#14), a 10-entry array into #15 | **decoded — see "Row #16 field decode" below.** `UnkEntity164Bytes` — the 20-entry cross-ref array matches `MapEntIds[20]`/`MapEntities[20]` exactly; also has a compound-condition pair (`0x02`/`0x04`, `-99` sentinel), an 18-short always-zero dead zone, and a cleanly discriminated trailing payload (`0x78`: 0/1/2 → 0/2/4 populated fields) |
| 17 | `DAT_0047064a` | 58 (`0x3a`) bytes/record | `DAT_00470608` | #6 (declared, **never used in retail data**), #8, LUT `DAT_00470664` (dominant), a 4-way discriminated ref (0/1/2/3 → #16/#12/#13/#14) | **fully decoded, including the tail — see "Row #17 field decode" below.** Structurally unusual — no leading GUID field at all (this record is never referenced by anything else in the file); the 42-byte tail turned out to be a nested pair-count array, the same idiom as row #8's nested waypoint list |

`DAT_00470664` itself is never the subject of a count+array read in this function — it's used
throughout as a lookup-table size bound, strongly suggesting it's a shared table (plausibly
`HercLUT`) loaded once at VSHELL startup, not per-mission.

### How this was verified

Extracted all 62 real `.MSN` files from `ES2/VOL/ZONES.VOL` (throwaway console probe referencing
`HercWorks.Vol.Io.VolFileReader` directly — same pattern used elsewhere in this project). Built a
strict byte-walker implementing the table above exactly (revision check, then all 17 rows in order,
including the skip-only row and the nested row's true 2-bytes-per-entry width) and ran it against
every file, checking whether the final read position lands exactly on EOF.

**Result: 61 of 62 files land on EOF with zero bytes of slack — `TRAIN5.MSN` (the file the Java port
was originally hand-tuned against) included.** This confirms the row ordering, every fixed stride,
and the skip/nested-row corrections, all at once, across real production data — not just one file.

**One outlier: `DEMO2.MSN` — traced precisely, root cause still unconfirmed but isolated to this one
file.** The walker reads correctly all the way through row #16 (12 records of 164 bytes, ending
exactly at file offset 8166) and row #17's count field (`1`, read at 8166-8167) — but only 16 bytes
remain in the file (8168-8183) where a single 58-byte record is expected. The 58-byte fixed-blob
read for row #17 is the same code path confirmed correct in all 61 other files (including
`TRAIN5.MSN`, which also has exactly 1 record in this row) — so this isn't a formula error. Given
`DEMO2.MSN` sits alongside a separately-named, differently-sized `DEMO_02.MSN` in the same VOL
folder (both extracted, both distinct real files) and every other file including the other three
demos matched exactly, the working theory is that `DEMO2.MSN` is a stale/leftover developer test
file with a genuinely truncated tail, not evidence of a table error — not chased further given the
cost of exhaustively re-deriving row #12/#16's field semantics just to rule out a subtler miscount
for one non-representative file.

### Reconciling against `data\script.dat` (`FUN_004243d7`) — SUPERSEDED, see `script-dat.md`

**This section's original guess was wrong and is kept only for history.** At the time this was
written, `FUN_0041ac54` (the function that actually produces `script.dat`'s content) hadn't been
decompiled, and the record-size mismatch against this doc's own row table led to a "probably a
separate, map-rendering-focused format" conclusion. A follow-up session decompiled `FUN_0041ac54`
and found it's the **writer** of `data\script.dat` — it serializes directly from these same `.msn`
row-array globals (a GUID-filtered field subset of rows #4/#6/#7/#8/#9/#10/#11/#12/#13/#14/#15/#16/#17),
right after `FUN_00417b67` finishes parsing the `.msn` file. The apparent size mismatch was because
`script.dat`'s records are a **field subset** of each row (e.g. row #6's export is just its X/Y/Z
int32 triple, stripped of GUID/condition/dead fields — the "12-byte coordinate triple" guess in the
old text below was correct almost by accident), not because the two formats are unrelated. Full
verified block-by-block mapping, including field-level detail on what each of `script.dat`'s two
independent readers (DBSIM and VSHELL's own `ShellMap`) keeps vs. discards from each row, is in
[`script-dat.md`](script-dat.md) — treat that doc as authoritative over the paragraph below.

<details>
<summary>Original (superseded) reasoning, kept for history</summary>

Compared this session's confirmed `.msn` record sizes against `FUN_004243d7`'s already-decompiled
`data\script.dat` record sizes (see Call chain above): 12-byte coordinate triples, 2-byte scalars,
a 6-byte type with its own nested array, 134-byte records, 52-byte records, 14-byte records, and a
156-byte record with cross-refs into the 52-byte array — **none of these sizes match any row in the
`.msn` table** (14/82/8/144/22/10/12/30/102/62/164/58). Combined with `script.dat`'s home TU being
`shellmap.cpp` (map/UI rendering) rather than `msn_gen.cpp` (mission generation), this looks like
genuinely separate, independently-shaped data — not a re-export or subset of the `.msn` arrays.
Best current read: `script.dat` is a **map-rendering-focused derivative** (coordinates, terrain
bounding data, and whatever the 156-byte cross-referenced type turns out to be) built for VSHELL's
own `ShellMap` UI, while the `.msn` file itself carries the full gameplay/entity/trigger data. Not
proven — `FUN_0041ac54` (what actually produces `script.dat`'s content, and whether it's derived
from the same `.msn` file or authored completely separately) hasn't been decompiled yet.

**Update after row #6 was field-decoded (below): `script.dat`'s "12-byte coordinate triples" are
suspiciously exactly the size of row #6's own position payload** (its 0x0A-0x15 span, stripped of
the 10-byte GUID/dead-field header) — worth a direct comparison of the *values*, not just the sizes,
next time this reconciliation is revisited; it's plausible `script.dat`'s coordinate list is a
positions-only export of (a subset of) row #6, even though the two files are independently parsed.

</details>

## Row #6 field decode — "MapPoint22" (`DAT_0047064e`, 22 bytes/record)

Chosen as a follow-up to row #9's decode: row #9's two ref types (`link` and `reward`) both resolve
into row #6 by GUID, and row #6 is also referenced by rows #8, #10, #15, #16, and #17 — the most
heavily cross-referenced still-undecoded row in the whole file, making it the highest-leverage next
target. Extracted all **2,661 real instances** across all 62 `.MSN` files.

Load code (`FUN_00417b67`, the `DAT_0047064e`/`DAT_0047060c` block): after the usual condition-gate
check, two mechanisms exist that never fire in retail data (see table) — a template-inheritance
field that copies three `int` fields from a referenced sibling record, and a "sum" flag that, if
set, replaces those same three fields with the **sum** of two *other* referenced records' three
fields (i.e. vector addition — compose this record's position from two others'). Both are dead code
paths in every real mission. The record's own key field (offset `0x00`) goes through the same
GUID-based "insert or merge" compaction every other row uses.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | own identity, same convention as every other record type |
| `0x02` | condition ref | **always `-1`** in all 2,661 real records — same dead pattern as rows #9/#10 |
| `0x04` | template/inherit index | **always `-1`** — the "copy 3 fields from record N" mechanism described above is real, working code, but never triggered by any shipped mission |
| `0x06` | ? | **always `-1`** — not read or written anywhere in this record's load loop; same unexplained-but-dead shape as row #9's `0x04` |
| `0x08` | "sum" flag | **always `0`** — the vector-addition-from-two-refs mechanism is real, working code, but never triggered by any shipped mission |
| `0x0A` | X (int32) | real range **77,591 to 3,825,420** — large magnitude, consistent with a ground-plane world coordinate |
| `0x0E` | Y (int32) | real range **17,968 to 3,800,672** — same large-magnitude shape as X |
| `0x12` | Z / altitude (int32) | real range **0 to 35,400** — two orders of magnitude smaller than X/Y, consistent with altitude in a mostly-ground-level mission rather than a third ground-plane axis |

Confirmed these are `int32`, not `float32`: reinterpreting the same bytes as IEEE-754 floats gives
uniformly near-zero denormalized garbage (`~1e-40` to `~5e-39`) across all 2,661 records — not a
plausible coordinate distribution — while the int32 reading gives a coherent, sensibly-scaled result
across every file.

**Working model: row #6 is the file's central spatial-reference table** — a flat, dead-simple
`{GUID, X, Y, Z}` point record (`MapPoint22`) that every other spatially-aware row (#8's nested
entries, #9's link/reward refs, #10's LUT ref, #15, #16, #17) points into by GUID. The three
extra fields (`0x02`/`0x04`/`0x06`/`0x08` — condition, inherit, unknown, sum-flag) are declared and
functionally wired up in the loader but are 100% inert across every real retail mission; a first C#
model should expose this as GUID + X + Y + Z and treat the rest as opaque round-trip padding.

**Sanity-checked against row #9's link theory, with a genuine surprise:** joined row #9's link-type
records (157 real, both endpoints GUID-resolvable into row #6) and compared the 3D distance between
each link's two endpoints against a random-pair baseline from the same file. **Result: link-pair
distances (median ~466,000 units) are not meaningfully closer than random same-file pairs (median
~310,000 units)** — actually slightly larger. This **doesn't support** a "the two endpoints are
physically adjacent/nearby" reading of row #9's link type; a link more likely represents a
deliberately long path leg (e.g. one leg of a patrol route spanning real map distance) or a
non-spatial relationship (grouping/ordering) than a "nearby marker cluster." Recorded here rather
than silently dropped, since it corrects what would otherwise have been an easy but wrong inference
from the `off0A ≈ off08+1` adjacency-in-index observation in row #9's own decode.

### How this was verified

Same walker/method as rows #9 and #10 — extended the (still byte-exact-verified) 17-row walker to
also capture row #6's raw 22-byte records, pulled all 2,661 real instances across the 62-file
corpus, and analyzed per-offset distributions plus a cross-file join against row #9's real link
records (matching both endpoints by GUID, same technique used for the row #9↔row #10 join).

## Row #8 field decode — "WaypointGroup" (`DAT_00470656`, 10 fixed bytes + nested-count×2 bytes/record)

Chosen next because row #15's decode found 94% of its real records point straight into row #8 — the
strongest concrete lead available on what this record actually represents, and it's the only record
type in the file with a nested dynamic array and no C# model at all. Extracted all **470 real
instances** (446 with ≥2 nested entries) across all 62 `.MSN` files.

Load code (`FUN_00417b67`, the `DAT_00470656`/`DAT_0047061c` block, already partly examined during
the original macro-structure pass to confirm the on-disk nested-entry width): the 10-byte fixed
header is 5 individually-read shorts (offsets `0x00`/`0x02`/`0x04`/`0x06`/`0x08`), followed by two
in-memory-only 4-byte fields zeroed at load time (the nested array's runtime pointer, and an always-
zero unused slot — neither is on-disk data). If the nested count (`0x08`) is nonzero, that many
2-byte nested entries are read and each one individually resolved into row #6's index space via
`FUN_00415b44` (the same resolver row #9 uses for its own row-#6 refs) — this is the confirmed
"raw 2-byte entries, not the 6-byte in-memory slot width" correction already recorded in this doc's
main table. Offset `0x04` gates a genuine template-inheritance branch, but a narrower one than row
#15's: only the **nested list itself** (count + resolved contents) is copied from the parent record;
none of the header's other fields are touched.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | own identity — **except 47/470 records have GUID `-1`, and those 47 are *exactly* the same 47 records that have a real (non–`-1`) condition at `0x02`** (verified: the two sets match 1:1). This looks like a deliberate design, not noise: a record whose existence is conditional doesn't get a stable identity, since it might not exist in a given playthrough — nothing else could safely reference it by GUID anyway |
| `0x02` | condition ref | **real usage confirmed** — 47/470 (10%) use a genuine trigger condition (values `13`-`33`). The highest real-usage rate of any condition field decoded in this file so far (row #15's was 5/637 ≈ 0.8%; every other row's was 0/n) |
| `0x04` | parent/inherit index (nested-list only) | **real usage confirmed** — 11/470 (2%) real records use it (parent values `38, 39, 41, 49, 52, 54, 56, 57, 60, 61, 62`). Every one of these 11 has on-disk nested count `0` and an empty nested list — fully consistent with the code: their actual waypoint list only exists at runtime, copied wholesale from the referenced parent. Reads as an intentional "alias" mechanism — giving a second GUID to an already-defined waypoint group without re-authoring its points |
| `0x06` | ? | **always `-1`** in all 470 real records — same dead-field shape as elsewhere in this file |
| `0x08` | nested count | real range `0`-`9`, mean `3.2`. `0`-count records (11/470) still go through the full record (GUID/condition/etc.), they just carry no waypoints |
| nested entries | ref into row #6 (`MapPoint22`), one per 2-byte slot | see spatial analysis below |

**Spatial analysis of the nested lists (this is where the "waypoint chain" reading comes from):**
computed the 3D distance between each pair of *consecutive* nested entries (resolved to real row #6
positions), across all 470 records — **median ~191,000 units**, meaningfully tighter than both row
#6's same-file random-pair baseline (~310,000) and row #9's link-pair median (~466,000) from the
earlier decode. Unlike row #9's link check (which found *no* spatial clustering), this one does —
consecutive entries in a row #8 list are real, measurably closer together than chance, consistent
with an authored path rather than an arbitrary grouping. Separately, **24% of multi-entry records
have their last nested entry equal to their first** — a closed loop, i.e. a patrol circuit that
returns to its own starting point. Both signals point the same direction independently.

**Working model:** row #8 (`WaypointGroup`) is a named, ordered list of row #6 world positions —
functionally a patrol route or waypoint chain, sometimes closed into a loop, occasionally
conditional (in which case it deliberately has no stable GUID) or aliased from an existing group via
inheritance. This directly explains row #15's heavy dependence on it (94% of real row #15 records
point here): a row #15 record is largely "use this waypoint group," optionally paired with a single
extra position, an action, or a polymorphic entity ref.

### How this was verified

Same walker/method as the other decoded rows — extended the confirmed walker to capture row #8's
raw header bytes plus its full nested-entry list per record (re-checked EOF-exactness held at
61/62 before trusting the extraction), then cross-joined the nested entries' raw stream values
against row #6's real GUIDs (same join technique as row #9↔row #6) to get real coordinates for the
spatial-coherence and closed-loop checks, rather than reasoning about the raw index values alone.

## Row #15 field decode — "LinkedRef22" (`DAT_00470658`, 22 bytes/record)

Chosen next since the doc's own "how to apply" notes flagged it as the natural follow-up to row #6:
same 22-byte size, but the summary table already suspected (from disassembly alone) that it was
structurally different — a "typed link," not a flat position record. Extracted all **637 real
instances** across all 62 `.MSN` files.

Load code (`FUN_00417b67`, the `DAT_00470658`/`DAT_00470620` block) is the richest of the four rows
decoded so far. It has a genuine, cleanly-gated template-inheritance branch at offset `0x04` (`-1` =
read fresh sub-fields from the stream; anything else = resolve a parent index via `FUN_00416324` and
copy all 7 remaining short fields — `0x08` through `0x14` — from that parent wholesale, not a
partial/per-field copy like row #6's dead inheritance mechanism). The "fresh" branch resolves three
plain refs (`0x0C`→row #6, `0x0E`→row #8, `0x14`→row #10) and one **4-way discriminated** ref at
`0x12`, selected by a discriminator at `0x10`: `1`→row #12, `2`→row #13, `3`→row #14, and — this
part only became visible by reading past the end of row #15's own loop — `0`→row #16, resolved in a
**second pass** that runs right after row #16 finishes loading (row #16 doesn't exist yet at the
point row #15 itself is parsed, so that discriminator value has to be resolved later). This is the
same "small discriminator selects among #12/#13/#14/#16" motif already seen in row #10's `0x50`
"target" field and row #17's cross-ref, now confirmed a third time.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | own identity, standard convention |
| `0x02` | condition ref | **not always `-1` this time** — 632/637 are `-1`, but **5 real records use a genuine trigger condition** (values `22, 23, 53, 55, 55`). First row decoded so far where this mechanism is confirmed to actually fire in a shipped mission, not just present-but-dead code |
| `0x04` | parent/template index | **always `-1`** in all 637 real records — the inheritance branch is real, working code (and structurally the "proper" wholesale-copy version of the pattern, unlike row #6's partial one) but never triggered by any shipped mission, same dead-in-practice shape as elsewhere in this file |
| `0x06` | ? | **not always `-1`**: 632/637 are `-1`, but exactly the **same 5 records** that have a real `0x02` condition also have a real `0x06` value here (`1, -99, -99, -99, 1`) — a 100% correlation across all 5 exceptions. `-99` matches the sentinel value the doc's condition/trigger section already flagged for the alternate evaluator `FUN_00415d90` ("a `-99`-sentinel-or-range-check"); best guess is `0x02`/`0x06` are a **compound condition pair** (an operator/flag plus a comparison operand), not two independent fields — not confirmed, since neither is read by any code in this specific loop (they're consumed, if at all, somewhere downstream of this parser) |
| `0x08` | small int | real range `0`-`6` (`0`:385, `3`:112, `2`:67, `4`:25, `5`:22, `1`:17, `6`:9). No confirmed meaning; weak correlation with whether `0x0C` (row #6 ref) is populated (`0x08=6` → always has a row #6 ref, `0x08=0`/`1` → never does, others mixed) — not clean enough to call a discriminator |
| `0x0A` | small int | almost always `0` (579/637); otherwise `3` (55) or `1` (3). No confirmed meaning |
| `0x0C` | ref into row #6 (`MapPoint22`) | **sparse** — populated in only 47/637 (7%) real records, real range `0`-`45` when present |
| `0x0E` | ref into row #8 | **dominant field** — populated in 600/637 (94%) real records, the record's primary payload by a wide margin. Real range spans row #8's typical size (18-79+ seen) |
| `0x10` | discriminator | real distribution: `0`→445, `-1`→159, `1`→25, `3`→8. **`2` never occurs** in retail data despite being a valid switch case — the same "declared-but-unexercised switch arm" pattern already seen in row #10's `0x06` (codes `8`/`10` never occur there either) |
| `0x12` | discriminated ref, per `0x10` | `-1` when `0x10=-1` (always, 159/159); real index ranges differ cleanly per discriminator value (`0`→row #16 range `0`-`229`, `1`→row #12 range `61`-`179`, `3`→row #14 range `54`-`197`) — consistent with each discriminator value indexing a genuinely different target array, not overlapping ranges that would suggest a shared domain |
| `0x14` | ref into row #10 (`Action82`) | **sparse** — populated in only 13/637 (2%) real records |

**Working model:** row #15 (`LinkedRef22`) is a "linked reference" record whose real payload is
overwhelmingly a pointer into row #8 (94% of records), optionally annotated with a world position
(row #6, 7%), an action (row #10, 2%), and/or a polymorphic entity ref (rows #12/#13/#14/#16,
~6% combined) chosen by `0x10`. **Update after row #8 was field-decoded (below): row #8 turned out
to be a named waypoint-group/patrol-route list, so row #15 reads most naturally as "attach this
patrol route/waypoint group to (optionally) a position, an action, and/or a specific entity" —
plausibly a patrol-assignment or escort/guard-route record.** `0x08`/`0x0A` (small ints) and the
`0x02`/`0x06` compound-condition guess remain open.

### How this was verified

Same walker/method as rows #6/#9/#10 — extended the confirmed 17-row walker to also capture row
#15's raw 22-byte records (re-checked the walker still lands on EOF exactly for 61/62 files before
trusting the new extraction), pulled all 637 real instances across the 62-file corpus, and analyzed
per-offset distributions including a direct crosstab of the 5 real `0x02`/`0x06` exceptions (rather
than just noting "mostly -1" and moving on, since a 5-record signal is small enough to read in full
rather than only summarize).

## Row #9 field decode — "LinkOrReward12" (`DAT_0047065e`, 12 bytes/record)

Chosen as the second record type to decode field-by-field, specifically because row #10
("Action82") references it (up to 4 slots, offsets `0x0A`-`0x11`) and it's small enough to fully
characterize. Extracted all **335 real instances** across all 62 `.MSN` files (same confirmed
walker, extended by 12 bytes/record at this row's position) and analyzed per-offset value
distributions across the whole set, then cross-referenced those instances against row #10's real
sub-refs by GUID (see "Cross-referencing against row #10" below).

Load code (`FUN_00417b67`, around the `DAT_0047065e`/`DAT_0047062c` block): each record's condition
field is checked via the same `FUN_00417610` gate every row uses, then two `short` fields are
independently resolved into row #6's index space via `FUN_00415b44` — one (`0x08`) unconditionally,
the other (`0x0A`) only if a third field (`0x06`) equals `0`. Finally the record's own `0x00` field
is looked up against already-processed records in this array (`FUN_00415c79`, search-only mode) —
a match causes the current record's data to overwrite that earlier slot instead of appending a new
one, i.e. `0x00` is a GUID-based "insert or merge" key, the same convention as offset `0x00` in
every other record type in this file (row #10/#12/#13/#14/#16 all use offset `0x00` as identity).

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | own identity, same convention as every other record type. Mostly (but not always) ascending per file — 19/61 files have a repeated GUID pair, meaning the load-time merge behavior described above is real and does trigger in retail data (not merely theoretical), unlike row #10's condition field |
| `0x02` | condition ref | the same "is this active" field every record type has — but **always `-1` in all 335 real records**, exactly like row #10's `0x02`. Real/functional per the code, simply unused by every shipped mission |
| `0x04` | ? | **always `-1`** — no read/write anywhere in this record's load loop, no observed use in real data either. Same dead-field pattern as row #10's `0x04` |
| `0x06` | type flag | binary discriminator, real distribution `0`→157, `1`→178 (no other values ever seen). Selects the meaning of `0x0A` (see below) — this is the field that makes the record "dual-purpose" |
| `0x08` | ref into row #6 (`MapPoint22`) | always resolved regardless of `0x06`. Real range `0`-`161`, 59 distinct values — plausible row-#6 array indices, now confirmed to be **world X/Y/Z positions** (see row #6's decode above) |
| `0x0A` | ref into row #6, **or** literal value — depends on `0x06` | when `0x06 == 0`: resolved into row #6's index space exactly like `0x08` (real range `1`-`162`), and in 137/157 (87%) of real records **equals `0x08`'s value plus exactly 1** in index terms — though a real-position distance check (see row #6's decode) found the two referenced *positions* are **not** unusually close together, so this is a link between two points at genuine map distance from each other (a path leg), not a "nearby marker pair." When `0x06 == 1`: **left completely unresolved (raw stream value)** — real values are a small closed set of round numbers (`100, 500, 600, 1000, 1350, 1500, 2000, 3000, 3500, 4500, 5000, 6000, 7000, 7500, 8000, 8500, 10000, 12000, 15000, 19000, 20000`), never overlapping with the index-like range seen when `0x06 == 0`. This looks like a literal quantity (credits/reward value is the best guess given the round-number shape and the game's credit-based Herc/weapon economy — see [[project_es2_game_domain]]), not an index at all |

**Working model:** row #9 is really two record shapes sharing one 12-byte layout, selected by
`0x06`: a **link** (`0x06=0`, two refs to row #6 world positions, forming a path leg spanning real
map distance) and a **reward/value marker** (`0x06=1`, one ref to a row #6 world position plus a
literal round-number quantity — a "reward for reaching/holding this point" shape). `0x00`/`0x02`/
`0x04` behave identically to their counterparts in every other record type (GUID / dead condition
ref / dead field) and aren't worth re-deriving per record type going forward.

### Cross-referencing against row #10 (partially resolves the `0x08` "verb code" open question)

Row #10's sub-refs (`0x0A`-`0x11`, up to 4 slots) are authored as **row #9 GUIDs**, not raw array
indices — matching all 275 non--1 real sub-ref values in the 62-file corpus against row #9's `0x00`
field by exact value gave a **100% match rate (275/275)**, with zero unresolved refs. This confirms
`FUN_00415c79`'s role at that call site (row #10's ref-resolution loop) is a GUID→index lookup into
the row #9 array, the same "author writes a stable GUID, loader resolves it to an array slot"
convention used everywhere else in this file.

Cross-tabbing row #10's `0x08` verb code against the type (`0x06`) of the row #9 record(s) each verb
points to (matched instances only):

| verb (`0x08`) | → row #9 type `0` (link) | → row #9 type `1` (reward) |
|---|---|---|
| `-1` | 0 | 1 |
| `0` | 0 | 9 |
| `1` | 29 | 90 |
| `2` | 13 | 29 |
| `3` | **101** | 3 |

Verb `3` points at a **link**-type row #9 record 97% of the time (101/104) — the strongest signal in
this table. Verbs `1`/`2` lean toward **reward**-type records (about 3:1 and 2:1) but aren't
exclusive. Slot position tells a similar story: slot 0 is fairly mixed (69 link / 126 reward), but
slots 1-3 are overwhelmingly link-type (33+26+15 link vs. 2+2+2 reward) — consistent with slots 1-3
existing mainly to extend a multi-hop link chain (a route/path), while slot 0 alone carries most of
the reward-marker usage. **Best current read: verb `3` ≈ "follow this link chain" (path/waypoint
objective), verbs `1`/`2` more often ≈ "grant this reward" but not purely so — the verb code likely
also encodes something beyond just "which row #9 sub-type to expect" (e.g. an AND/OR/sequence
combinator across the up-to-4 refs, per the original hypothesis in row #10's decode).** Not fully
resolved, but meaningfully narrowed from "unknown" to a specific, testable shape.

### How this was verified

Same walker/method as row #10: rebuilt the 17-row byte-walker (revision check + all rows in exact
order, including the skip-only and nested-array corrections), re-extracted `ZONES.VOL` fresh (the
prior session's extraction wasn't preserved), and re-ran the walker — it reproduced the **exact same
result already on record** (61/62 files land precisely on EOF, `DEMO2.MSN` undershoots by exactly 42
bytes), which is a strong sanity check that the walker implementation is correct before trusting its
row #9 extraction. Row #9 records were pulled from all 62 files (335 total, including `DEMO2.MSN`'s
1 record — that file's truncation only affects row #17, far downstream) and analyzed for per-offset
value distributions, then joined against row #10's real sub-refs by GUID.

## Row #10 field decode — "Action82" (`DAT_00470660`, 82 bytes/record)

Chosen as the first record type to decode field-by-field: it's referenced by four other record
types via a type-discriminated pointer (#12/#13/#14/#16), making it the strongest lead on actual
mission objective/trigger logic. Extracted all **338 real instances** of this record across all 62
`.MSN` files (using the confirmed walker above to locate them precisely) and analyzed value
distributions and per-byte-offset variance across the whole set — not just eyeballing one file.

**Headline finding: this record's real payload is much smaller than 82 bytes suggests.** Two of its
fields are nominal fixed-size arrays (8 slots, 5 slots) that, in every real mission, never use more
than their first few slots — the rest is permanently `-1`. A 42-byte span in the middle is constant
(`0000` followed by twenty `-1` shorts) in 337 of 338 real records.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | own identity, as in every other record type |
| `0x02` | condition ref | the same "is this active" field every record type has (checked via `FUN_00417610`) — but **always `-1` in all 338 real records**. Real/functional per the code, simply unused by every shipped mission. |
| `0x04` | ? | **always `-1`** — no observed use anywhere, load-time code or real data |
| `0x06` | type/category | discriminator also read later by the type-7/8/9/10 remap. Real distribution: `1`→170, `0`→142, `3`→15, `4`→4, `7`→3, `2`→3, `9`→1. Codes `8` and `10` **never occur** in retail data despite being valid switch cases — only `7` and `9` are ever exercised. |
| `0x08` | small int | values `0`(66) `1`(172) `2`(63) `3`(36) `-1`(1). Not simply "count of populated slots below" — cross-tabbing against that count shows no clean correlation. Best guess: an independent verb/operation code (how the sub-references at `0x0A` combine — AND/OR/sequence — or a priority level), not confirmed. |
| `0x0A`–`0x11` | ref[0..3] into row #9 (12-byte type) | the only slots ever populated (up to 4). Authored as row #9 **GUIDs**, resolved at load time to array indices — confirmed by a 100% GUID match rate against row #9's real data, see row #9's decode below |
| `0x12`–`0x19` | ref[4..7] into row #9 | **always `-1`** — declared as an 8-slot array, functions as a ≤4-slot one |
| `0x1A`–`0x43` | (42 bytes) | **constant in 337/338 records**: `0000` then twenty `0xFFFF` shorts. One record somewhere has an alternate pattern at just the first 6 bytes (`01 00 00 00 00 06`); not chased further given how rare it is. Functionally dead space in virtually all real missions. |
| `0x44`–`0x45` | ref[0] into the herc/unit-type LUT (`DAT_00470664`) | rare but real (e.g. one record referencing herc-type `174`) |
| `0x46`–`0x4D` | ref[1..4] into the LUT | **always `-1`** — declared as a 5-slot array, functions as a ≤1-slot one |
| `0x4E` | secondary value | mostly `0` (272/338), otherwise a discrete small number (seen: 1,3,7,12,15,17,19,21,22,25,26...). Not pinned down — candidate guesses: a delay/timer, a sequence index, or a secondary id. |
| `0x50` | "target" | polymorphic entity ref, type chosen by the `0x06` discriminator (`7`→row #12/Type144b, `8`→row #13/never exercised, `9`→row #14/MiscEntity62, `10`→row #16/never exercised). Only 4 of 338 records use it at all (3×type-7, 1×type-9), matching real targets `179`, `139`, `140`. |

**How to apply:** given how sparse this record actually is in practice (most of its 82 bytes are
inert), a first real C# model should probably expose it as GUID + condition + type + verb-code +
`short[4]` sub-refs + optional herc-type ref + secondary value + optional polymorphic target, rather
than faithfully modeling the full 8/5-slot arrays and 42-byte gap as meaningful — those are real
on-disk bytes (round-tripping should still preserve them raw) but not fields worth exposing as
editable. The `0x08` verb-code and `0x4E` secondary value are the two biggest remaining semantic
gaps. `0x08` is now **partially** resolved by cross-tabbing against row #9's own decoded type field
(see row #9's decode below, "Cross-referencing against row #10") — verb `3` correlates strongly
(97%) with "link"-type row #9 sub-refs, verbs `1`/`2` lean toward "reward"-type sub-refs but aren't
exclusive, so `0x08` likely isn't *purely* a row-#9-type selector — it may still encode an
AND/OR/sequence combinator on top. `0x4E` remains fully open.

## Row #3 field decode — "VariantValue8" (`DAT_00470666`, 8 bytes/record)

Small (8 bytes = 4 shorts), cheap to fully characterize, and referenced by row #4's `UnitInfo`
(field `0x8e`, which fetches row #3's own payload field rather than just an index — see the main
table). Extracted all **194 real instances** across all 62 `.MSN` files.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | standard identity/dedup key |
| `0x02` | condition ref | **real usage 86/194 (44%)** — the second-highest real-usage rate of the condition mechanism found in this file (after row #1 itself) |
| `0x04` | ? | **exactly two values**: `-1` (108) or `-99` (86) — and the `-99` set is *identical* to the set of records with a real `0x02` condition. Never read anywhere in this record's own load loop, though, so this looks like an authoring-tool convention marker (consistently written whenever the record has a real condition) rather than a value the runtime itself consumes here |
| `0x06` | payload | real, varied values (54 distinct) — this is the field row #4 fetches by reference. No unit/scale confirmed |

**Working model:** several real records share the same GUID but different conditions and different
`0x06` payload values (e.g. one file has GUID `240` repeated 7 times with conditions `44,45,47,48,
50,51,52` and correspondingly different payloads) — i.e. this is a **condition-gated variant table**:
the same logical value has several campaign-state-dependent versions, and the standard GUID-based
compaction step keeps only whichever variant's condition actually passes at load time (plus any
GUID instance with no condition at all, presumably a default/fallback). Row #4's still-open
sub-array reconciliation question should treat its `0x8e` field as "look up this variant value,"
not "look up this record."

## Row #7 field decode — "Flag10" (`DAT_00470650`, 10 bytes/record)

The simplest record type in the file. Extracted all **105 real instances** across all 62 `.MSN`
files.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | standard identity/dedup key |
| `0x02` | condition ref | **always `-1`** in all 105 real records |
| `0x04` | parent/inherit index | **always `-1`** — unlike row #8's structurally similar inheritance field, never used in retail data here |
| `0x06` | ? | **always `-1`** — same dead-field shape as elsewhere |
| `0x08` | payload | real but narrow: `0` (65/62%), `1` (36/34%), `10` (4/4%) — a small discrete flag/level, not a free-form value |

**Working model, updated 2026-08-10 — this is a heading, not a generic flag/level.** The original
guess below (difficulty-tier/toggle marker) is superseded: `data\script.dat`'s writer exports this
exact payload field verbatim (see [`script-dat.md`](script-dat.md) block 2), and DBSIM's reader
multiplies it by `0xb6` (182) on load — the same degrees-&gt;BAM (16-bit binary angle) conversion
constant already independently confirmed elsewhere in DBSIM (`debris.cpp`'s loader, see
[[project_es2_exe_recon]]). A payload of `0`/`1`/`10` degrees is a small, sensible heading range,
not a coincidence. Row #7 is best read as **a minimal `{GUID, heading}` record** — nothing else in
the `.msn` file references it (its only cross-ref is "self," i.e. its own dead inheritance field),
but `script.dat`/DBSIM is a real downstream consumer of its payload after all.

<details>
<summary>Original (superseded) working model, kept for history</summary>

Row #7 is a minimal `{GUID, payload}` record — nothing else in the file
references it (per the macro-structure table, its only cross-ref is "self," i.e. its own dead
inheritance field), and its own real payload is a 3-valued discrete flag. Best guess given the
narrow domain: a simple per-entity toggle or difficulty-tier marker, not confirmed further —
low-value to chase further without a concrete consumer to check it against.

</details>

## Row #11 field decode — "ActionPair30" (`DAT_00470662`, 30 bytes/record)

Chosen because it directly extends row #10 (`Action82`), already decoded, and the macro-structure
pass had already guessed it was "a grouped sequence of #10 action records." Extracted all **72 real
instances** across all 62 `.MSN` files.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | standard identity/dedup key |
| `0x02` | condition ref | **always `-1`** in all 72 real records |
| `0x04` | ? | **always `-1`** — dead, same shape as elsewhere |
| `0x06` | ref into row #10 (`Action82`) | populated in 59/72 (82%) real records — the dominant single field |
| `0x08` | small int | real values cluster on round numbers (`10` dominant at 49/72=68%, then `30, 60, 120, 45, 8, 2, 1, ...`) — shape consistent with a delay/timer in seconds, not confirmed |
| `0x0A`-`0x1D` | nominal 10-slot ref array into row #10 | **the "sequence" is a red herring in practice: 69/72 (96%) of real records populate at most slot 0, and 3/72 populate none at all. Zero real records use more than 1 slot.** |

**Working model:** despite the nominal 10-slot array suggesting a multi-step scripted sequence, real
missions never use it that way — row #11 is functionally a **pairing of (at most) two row #10
action records** plus a small timer-shaped parameter, the same "declared capacity for future
content, never fully exercised" pattern already seen in row #10's own 8-slot/5-slot sub-arrays and
row #9's 8-slot ref array. A first C# model should expose this as GUID + primary-action-ref +
optional-secondary-action-ref + timer value, not a `short[10]`.

## Row #4 field decode — no stable name; refutes `UnitInfo` (`DAT_00470668`, 144 bytes/record)

Chosen first among the remaining rows because it has an *exact* byte-size match to the existing C#
`UnitInfo` type, making it look like the cheapest of the remaining rows — start from the existing
struct as a hypothesis rather than a blind decode. That hypothesis did not survive contact with the
load code. Extracted all **60 real instances** across the 62-file corpus.

Load code (`FUN_00417b67`, the `DAT_00470668`/`DAT_00470640` block): the condition-gate check reads
directly from offset `0x00` — every other row's condition sits at `0x02`, right after a GUID at
`0x00` (the same "no incoming refs → no need for a GUID" shape already found for row #17, confirmed
here too: nothing else in the file's load function ever touches `DAT_00470640`/`DAT_00470668`). Three
sub-arrays (10, 30, 30 shorts, matching the macro pass's byte-count guess exactly) are each resolved
element-by-element into the shared herc/unit-type LUT (`DAT_00470664`, via `FUN_00415a7c`). A single
trailing field is resolved into row #3 (`VariantValue8`, via `FUN_00415ae3`) and then **overwritten
with row #3's own payload value**, not left as an index — the same "fetch the variant's value, not
just its slot" behavior row #3's own decode already predicted for this row.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | condition ref | **always `-1`** in all 60 real records — dead, same shape as every other row's condition field |
| `0x02`-`0x14` | sub-array A into the LUT (10 slots) | real usage 1-4 of 10 slots populated (mean 2.3); real values range `45`-`243` |
| `0x16`-`0x50` | sub-array B into the LUT (30 slots) | real usage 0-4 of 30 slots populated (mean 1.8); real values range `81`-`246` |
| `0x52`-`0x8c` | sub-array C into the LUT (30 slots) | real usage 0-3 of 30 slots populated (mode exactly 1); real values range `84`-`247` |
| `0x8e` | ref into row #3 (`VariantValue8`) | **dominant field** — populated in 52/60 (87%) real records, real range `85`-`248` |

**No GUID/identity field exists in this record at all** — confirmed two ways: the load code's own
condition-filter call reads offset `0x00` directly (the slot every other row reserves for GUID), and
a source-wide check found `DAT_00470640`/`DAT_00470668` (this row's storage/count globals) are never
referenced anywhere else in `FUN_00417b67` outside this row's own block. This directly contradicts
`UnitInfo.cs`'s assumed layout (`GUID`+`MapCoordId`+`HeaderFlags[22]`+`UnitId`+`Weapons[10]`+
`UnkFlags[36]`+`HealthModAdjust`) — none of those fields exist in the real load code, which touches
every single byte of the record via just five components (condition + 3 LUT-ref sub-arrays + 1 row-#3
ref) that sum to exactly 144 bytes with zero slack, unlike most other rows in this file which have
real unaccounted-for dead spans. The old hypothesis wasn't just wrong about the 10/30/30 vs. 10/36
split — it invented fields (`MapCoordId`, a singular `UnitId`, `HealthModAdjust`) that the real parser
never touches.

**A second, independent surprise: this is a mission-level singleton, not a multi-entity roster.**
Every one of the 62 `.MSN` files has **at most one** row #4 record — 60 files have exactly one,
and only `TRAIN1.MSN`/`TRAIN3.MSN` (the two earliest tutorial missions) have none. Combined with the
sub-arrays resolving into a shared LUT and the row #3 ref fetching a condition-gated variant *value*
(not an index), this reads much more like a per-mission "reward/unlock package" (e.g. hercs/weapons
made available, plus a condition-gated bonus quantity from row #3) than a spawn record — it has no
position ref (into row #6) at all, which a genuine "spawn a herc here" record would need.

**A loose end worth flagging rather than resolving:** within a single record, the real sub-array and
row-#3-ref values are frequently near-consecutive small integers (e.g. one record: sub-array A =
`{85,86,87}`, sub-array B = `{88}`, sub-array C = `{89}`, row-#3 ref = `90`) and **never overlap
across the three sub-arrays in any of the 60 records** (0/60 have any cross-sub-array value repeat).
This is consistent with the authoring tool assigning IDs from one running counter across a batch of
related objects authored together, but it's hard to square with sub-arrays A/B/C resolving into a
*shared, game-wide* LUT (whose entries should have stable, LUT-fixed ids, not per-file-sequential
ones) while the row-#3 ref resolves into a wholly different, per-file-scoped GUID space and still
lands adjacent in value. Not chased further — either the "shared game-wide LUT" reading of
`DAT_00470664` needs revisiting, or this is authoring-tool coincidence, and distinguishing the two
would require tracing `FUN_00415a7c`/`DAT_00470664`'s origin outside this function, which is out of
scope for this pass.

### How this was verified

Extended the confirmed 17-row walker to capture row #4's raw 144-byte records (re-checked EOF-
exactness held at 61/62 first), pulled all 60 real instances, and analyzed per-offset/per-sub-array
population counts and value ranges — plus a per-file record-count check (revealing the "at most one
per mission" pattern) and a within-record cross-sub-array overlap check (revealing the zero-overlap,
near-consecutive-value pattern), neither of which a single-file read would have surfaced.

## Row #13 field decode — "UnkEntity102Bytes" (`DAT_00470654`, 102 bytes/record)

Chosen next for the same reason as row #4: an exact size match to an existing (but unverified) C#
type, `UnkEntity102Bytes`. The macro-structure pass's cross-ref note for this row ("inherit only")
undersold it — reading the fresh-data branch (not just the inherit branch) turned up four real
cross-refs the macro pass missed entirely. Extracted all **124 real instances** across the 62-file
corpus.

Load code (`FUN_00417b67`, the `DAT_00470654`/`DAT_00470618` block): a genuine, cleanly-gated
template-inheritance branch at offset `0x04` (`-1` = read fresh; anything else = resolve a parent via
`FUN_00416276` and wholesale-copy the rest of the record). The "fresh" branch resolves four refs:
`0x30`→row #6 (`FUN_00415b44`), `0x32`→row #7 (`FUN_00415bab`), and **two** separate row #10 refs at
`0x60`/`0x62` (`FUN_00415ce0` each). Two 20-short spans (`0x08`-`0x30` and `0x38`-`0x60`) are
inherited wholesale but never individually resolved by this loop.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | own identity, standard convention — real usage 94/124 (76%) |
| `0x02` | condition ref | **real usage 30/124 (24%)** — unusually high vs. most rows (row #1 and row #3 are the only other rows with comparably high real condition usage) |
| `0x04` | parent/inherit index | **real usage 37/124 (30%)** — the highest real inheritance usage of any row decoded so far (every other row's inheritance mechanism is either fully dead or used in single digits of percent); this is the first row where *both* condition and inheritance are simultaneously well-used in practice |
| `0x06` | ? | **always `-1`** — dead, and (like row #14's/#12's analogous field) excluded from the inherit-copy range entirely |
| `0x08`-`0x30` | flags block A (20 shorts) | **always populated, never `-1`** — a genuine boolean array, values only `0` (96.5%) or `1` (3.5%); this is the real "Flags" array the old C# name gestured at, just half the declared length (20, not 49) |
| `0x30` | ref into row #6 (`MapPoint22`) | declared, resolved via `FUN_00415b44`, but **always `-1`** in all 124 real records — dead |
| `0x32` | ref into row #7 (`Flag10`) | declared, resolved via `FUN_00415bab`, but **always `-1`** — also dead |
| `0x34` | binary field | 68% real, and every real value is exactly `0` (not a range) — a presence flag, not a scaled value |
| `0x36` | ? | not part of the inherit-copy list; essentially always `0` (123/124), one record has `1` |
| `0x38`-`0x60` | flags block B (20 shorts) | **essentially always `-1`** (2/2,480 slots non-`-1`) — functionally inert despite being copied wholesale by inheritance exactly like block A |
| `0x60` | ref into row #10 (`Action82`), slot 1 | declared, resolved via `FUN_00415ce0`, but **always `-1`** — dead |
| `0x62` | ref into row #10 (`Action82`), slot 2 | **the only one of the four declared cross-refs actually used** — 26/124 (21%) real, range `52`-`198` |
| `0x64` | trailing field | **always exactly `100`** in all 124 real records — a hardcoded constant, not a variable value. Matches the old Java port's own field name for this offset, `UnkVal_100` — a prior RE pass evidently noticed the same constant without explaining it |

**Working model:** row #13 is a per-item boolean flag set (block A, 20 real flags) paired with a
second, near-entirely-inert 20-slot span (block B) and four declared-but-mostly-dead cross-refs, of
which only the second row #10 slot (`0x62`) is genuinely exercised. It's also the first row decoded
where both the condition mechanism (24%) and the template-inheritance mechanism (30%) are
simultaneously in real, common use rather than one dominating or both being dead — suggesting this
record type is routinely authored as "variant of an existing one, active under this condition."

### How this was verified

Extended the confirmed walker to capture row #13's raw 102-byte records (re-checked EOF-exactness
held at 61/62 first), pulled all 124 real instances, and analyzed per-offset distributions across the
whole set, reading both the fresh-data branch *and* the inherit-copy branch of the load code (the
macro pass had only looked at the inherit branch, which is why it missed the four cross-refs).

## Row #14 field decode — "MiscEntityInfo" (`DAT_0047065c`, 62 bytes/record)

Chosen next for the same reason as row #13 — exact size match to an existing C# type
(`MiscEntityInfo`), and the macro pass's cross-ref note ("#6 (×1), #10 (×2)") undersold it by one ref,
same as row #13. Extracted all **1,949 real instances** across the 62-file corpus — the largest
sample of any row decoded this session, giving strong statistical power.

Load code (`FUN_00417b67`, the `DAT_0047065c`/`DAT_00470628` block): structurally the same shape as
row #13 — a cleanly-gated inherit branch at `0x04`, four resolved refs in the fresh branch
(`0x0A`→row #6 via `FUN_00415b44`, `0x0C`→row #7 via `FUN_00415bab`, `0x38`/`0x3A`→row #10 ×2 via
`FUN_00415ce0`), and one 20-short span (`0x10`-`0x38`) inherited wholesale but never individually
resolved.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | own identity — real usage 1,937/1,949 (99%) |
| `0x02` | condition ref | real usage 574/1,949 (30%) — same elevated-usage tier as rows #1/#3/#13 |
| `0x04` | parent/inherit index | real usage only 8/1,949 (0.4%) — dead-in-practice here, unlike row #13's version of the same mechanism |
| `0x06` | ? | **always `-1`** — dead, excluded from the inherit-copy range, same shape as row #12's/#13's analogous field |
| `0x08` | type-like scalar | **71% real**, range `0`-`56` (43 distinct values); not resolved via any lookup function in this loop, but strongly correlated with the trailing field (see below) |
| `0x0A` | ref into row #6 (`MapPoint22`) | sparse — 6.4% real, range `1`-`59` |
| `0x0C` | ref into row #7 (`Flag10`) | sparse — 6.7% real, but a narrow domain (only 10 distinct row #7 GUIDs referenced across all 1,949 records), range `40`-`97` |
| `0x0E` | small discrete field | **always populated** (100%, never `-1`), 3 values: `0` (64%), `1` (33%), `2` (3%) |
| `0x10`-`0x38` | block (20 shorts) | sparse — only 3.9% of slots populated overall; when present, real values are heavily concentrated on `2` (half of all populated slots) plus long runs of consecutive values (e.g. `217`-`222`) |
| `0x38` | ref into row #10 (`Action82`), slot 1 | rare — 0.4% real, only values `46`/`47` |
| `0x3A` | ref into row #10 (`Action82`), slot 2 | essentially dead — 0.1% real (1/1,949, value `18`) |
| `0x3C` | trailing field | **always populated**, only two values: `100` (71%) or `0` (29%) — matches `0x08`'s populated/unpopulated split almost exactly (99.1% correlated, 1,931/1,949: real `0x08` ⇔ `0x3C=100`) |

**Working model:** the `0x08`/`0x3C` correlation is the clearest signal in this row — `0x3C` reads as
a `HealthModAdjust`-style percentage (`100` = full/default) that's only meaningfully set when an
entity type (`0x08`) is actually specified, and left at `0` (not `-1`) otherwise. This directly
supports the old C# type's field naming (`MiscEntityId` + `HealthModAdjust`) even though the *cross-
refs* the old model was missing (all four: #6, #7, #10×2) turn out to be real, if sparse.

### How this was verified

Same walker/method as row #13 — extended the confirmed walker to capture row #14's raw 62-byte
records (re-checked EOF-exactness held at 61/62), pulled all 1,949 real instances, analyzed per-
offset distributions, and specifically cross-tabbed `0x08`'s populated/unpopulated state against
`0x3C`'s value (rather than reporting the two marginal distributions independently) once their
percentages turned out to coincide almost exactly.

## Row #16 field decode — "UnkEntity164Bytes" (`DAT_0047065a`, 164 bytes/record)

The macro-structure pass already flagged this as the "strongest correspondence found" that session
(the 20-entry discriminated-ref array matches the existing C# type's `MapEntIds[20]`/`MapEntities[20]`
fields exactly), making a full field-level pass the natural next step to confirm the rest of the
type. Extracted all **1,247 real instances** across the 62-file corpus and, given the record's size,
analyzed all 82 short-offsets individually rather than only the ones the load code explicitly
resolves — several genuinely-used fields turned out to sit in spans the load code never touches with
a lookup call.

Load code (`FUN_00417b67`, the `DAT_0047065a`/`DAT_00470624` block): resolves four plain refs
(`0x32`→row #6, `0x34`→row #7, `0x36`→row #8, `0x76`→row #10), a **20-entry** discriminated-ref array
at `0x38`-`0x5E` (selector = the field at `0x2E`: `0`→row #12, `1`→row #13, `2`→row #14 — the same
3-way discriminator motif already seen elsewhere), and a **10-entry** plain-ref array into row #15 at
`0x60`-`0x72`.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | own identity — 100% real, range `0`-`238` |
| `0x02` | condition ref | sparse — 2.5% real, range `22`-`59` |
| `0x04` | compound-condition partner | **1.4% real, and every real value is exactly `-99`** — the same sentinel row #15's decode already flagged for the alternate evaluator `FUN_00415d90`. All 17 real instances co-occur with a real `0x02` (0 counterexamples) — a third confirmed instance of the "`0x02`/`0x0X` compound condition pair" idiom (after row #15's `0x02`/`0x06` and row #12's `0x02`/`0x06`, see below) |
| `0x06` | binary flag | always populated, `0`/`1` split roughly 39/61 |
| `0x08` | near-constant | always populated, essentially always `0` (1 exception = `25`) |
| `0x0A`-`0x2C` | dead zone (18 shorts) | **always exactly `0`** in all 1,247 records — a genuine always-zero padding span, not a `-1`-sentinel one |
| `0x2E` | discriminator | 89% real, values `0`/`1`/`2` — selects the target of the `0x38` array (see above) |
| `0x30` | small discrete field | 85% real, range `0`-`16`, all 17 values used — not resolved via any lookup call, meaning undetermined |
| `0x32` | ref into row #6 | 37% real, range `0`-`93` |
| `0x34` | ref into row #7 | 45% real, range `1`-`97` |
| `0x36` | ref into row #8 | 43% real, range `7`-`170` |
| `0x38`-`0x5E` | 20-entry discriminated ref array | **declared-capacity-vs-real-use decay**: slot 0 (`0x38`) 89% real down to slot 8 (`0x54`) 0.6%, slots 9-19 (`0x56`-`0x5E`) **never used** (0%) |
| `0x60`-`0x72` | 10-entry ref array into row #15 | same decay shape: slot 0 (`0x60`) 47% real, slot 1 2.7%, slot 2 0.5%, slots 3-9 never used |
| `0x74` | tri-state flag | 89% real (`0` or `1`), else `-1` |
| `0x76` | ref into row #10 | 31% real, range `17`-`198` |
| `0x78` | discriminator | **always populated**, values `0` (97%) / `1` (2.8%) / `2` (0.5%) — cleanly selects how many of the four trailing fields are populated: `0`→none, `1`→`0x7A`+`0x7C` both populated (100% of the time), `2`→all four of `0x7A`/`0x7C`/`0x7E`/`0x80` populated (100% of the time). A clean 3-way variant-payload discriminator, verified by exhaustive crosstab, not just marginal distributions |
| `0x7A` | payload field 1 | present iff `0x78≥1`; unusually wide range (`20`-`650`) vs. every other ref/index field in this row |
| `0x7C` | payload field 2 | present iff `0x78≥1`; narrow domain, only values `2`/`23` observed |
| `0x7E` | payload field 3 | present iff `0x78=2` only |
| `0x80` | payload field 4 | present iff `0x78=2` only, always exactly `2` when present |
| `0x82`-`0xA0` | dead zone (16 shorts) | always `-1` in all 1,247 records |
| `0xA2` | trailing flag | sparse — 6% real, values `0`/`1` |

**Working model:** row #16 is the richest record type decoded this session — a position/flag/route/
action quartet of cross-refs (rows #6/#7/#8/#10) layered with two declared-capacity-but-sparse ref
arrays (into the #12/#13/#14 polymorphic family, and into row #15), plus a genuinely clean 3-way
discriminated trailing payload (`0x78`) that no other decoded row in this file exhibits this cleanly.
The 18-short always-zero span (`0x0A`-`0x2C`) and 16-short always-`-1` span (`0x82`-`0xA0`) are large
enough that a first C# model should still round-trip them raw even though they carry no observed
signal.

**Update 2026-08-10 — the `0x38`-`0x5E` discriminated-ref array's real runtime purpose is now known:
it's DBSIM's entity-activation mechanism.** `data\script.dat`'s writer exports almost this entire
row (everything except GUID/`0x02`/`0x04`/`0x78`, see [`script-dat.md`](script-dat.md) block 11), and
DBSIM's reader does something concrete with the 20-entry array on load: for each populated entry, the
`0x2E` discriminator (`0`/`1`/`2`) picks one of three "live entity" flag arrays and marks the
referenced row #6/#7/#14 slot live in it — this is what actually turns a statically-declared position/
heading/entity into something DBSIM spawns and simulates, not just a UI cross-reference. So row #16
isn't only a "position/flag/route/action quartet" for authoring purposes — at runtime it's closer to
**a spawn/activation directive**: "these specific row #6/#7/#14 records are real, live objects in this
mission," gated by whatever condition/discriminator logic put a real entry in the array in the first
place.

### How this was verified

Extended the confirmed walker to capture row #16's full 164-byte records (re-checked EOF-exactness
held at 61/62), pulled all 1,247 real instances, and — because the record is large enough that a
code-only reading risked missing fields the loop never explicitly resolves — computed per-offset
non-`-1` rates and value ranges across *all* 82 short-offsets rather than only the ones the
disassembly calls out, then followed up with targeted crosstabs (`0x02`×`0x04`, `0x78`×the four
trailing fields) once the marginal distributions suggested a relationship.

## Row #12 field decode — a second 144-byte type, distinct from row #4 (`DAT_00470652`, 144 bytes/record)

The most involved row left, per the macro-structure pass's own note ("elaborate inherit copying ~6
sub-blocks"). No existing C# model to start from (unlike rows #4/#13/#14/#16), so this required a
full disassembly read rather than a hypothesis-verification pass. Extracted all **1,683 real
instances** across the 62-file corpus — the second-largest sample this session — and, as with row
#16, analyzed all 72 short-offsets individually given the record's size and the doc's prior note that
this row's real cross-refs might not match what the macro pass assumed.

Load code (`FUN_00417b67`, the `DAT_00470652`/`DAT_00470614` block): a cleanly-gated inherit branch
at `0x04` copying four `memcpy`'d blocks (20, 10, 20, and 10 shorts) plus seven individually-copied
scalar/ref fields. The fresh branch resolves four refs (`0x46`→row #6, `0x48`→row #7, `0x8A`/`0x8C`→
row #10 ×2) — matching the macro pass's guess of "#7, #10(×2)" (which also missed a #6 ref, same
omission pattern as rows #13/#14) — but real data shows **all four are nearly-to-entirely dead**
(0%, 0.1%, 0.7%, 2.4%), a much starker version of the "declared but barely used" pattern than any
other row so far.

| offset | field | real-data findings |
|---|---|---|
| `0x00` | GUID | 59% real, range `22`-`217` — see the GUID/condition/inherit three-way split below |
| `0x02` | condition ref | 43% real, range `1`-`58` — elevated-usage tier, same as rows #1/#3/#13/#14 |
| `0x04` | parent/inherit index | **48% real (809/1,683)** — the highest inheritance usage of any row decoded, edging out row #13's 30% |
| `0x06` | compound-condition partner | 3.9% real, values only `-99` or `2`; all 65 real instances co-occur with a real `0x02` (0 counterexamples) — same idiom as row #16's `0x02`/`0x04` and row #15's `0x02`/`0x06` |
| `0x08` | binary flag | always populated, `0`/`1` |
| `0x0A` | near-constant with rare large outliers | always populated, dominant `0` (91%), otherwise one of `220`/`255`/`256` — a bitmask-like shape, not a normal small-int field |
| `0x0C`-`0x2E` | dead zone (18 shorts) | **always exactly `0`** in all 1,683 records — same always-zero padding pattern as row #16's `0x0A`-`0x2C` |
| `0x30` | small discrete field | 47% real, range `0`-`20`, every value in range used |
| `0x32`-`0x44` | **unresolved 10-slot array** | the record's real workhorse: slot 0 (`0x32`) 46% real decaying to slot 9 (`0x44`) 0.1%, values in the `0`-`32` range — never touched by any lookup call in this loop, so its target domain is undetermined from load-time code alone. Population is bursty, not purely front-loaded: within a record it's typically all-or-nothing in blocks (0, 1, 3-5, 7-9, or 10 of the 10 slots populated — very few records populate exactly 2 or 6) |
| `0x46` | ref into row #6 | declared, resolved via `FUN_00415b44`, but essentially never used — 0.1% real (1/1,683) |
| `0x48` | ref into row #7 | declared, resolved via `FUN_00415bab`, but **never** used in retail data — 0/1,683 |
| `0x4A` | small discrete field | **always populated**, values `0`-`4`, dominant `0` (84%) |
| `0x4C`-`0x5A` | sparse paired array (5 pairs / 10 slots) | usage decays in matched pairs (`0x4C`&`0x4E` both 15.9%, `0x50`&`0x52` both 8.5%, `0x54`&`0x56` both 2.1%, `0x58`&`0x5A` both 0.5%, remaining pair-slots `0x5C`-`0x72` never used); first element of each pair has a wide range (`20`-`480`, wider than any ref field seen elsewhere in this file), second element a narrow one (`2`-`23`, only 4 distinct values) — reads as a `(ref-or-id, small-tag)` pair list, meaning undetermined |
| `0x74`-`0x84` | always-populated block (6 shorts) | unlike every other sub-block in this record, **100% populated in every record**, values tightly bounded `0`-`5` and trending upward across the span |
| `0x86` | constant | **always exactly `5`** in all 1,683 records |
| `0x88` | constant | **always exactly `2`** in all 1,683 records |
| `0x8A` | ref into row #10, slot 1 | declared, resolved via `FUN_00415ce0`, essentially dead — 0.7% real |
| `0x8C` | ref into row #10, slot 2 | declared, resolved via `FUN_00415ce0`, nearly dead — 2.4% real |
| `0x8E` | trailing field | **always populated**, values `100` (98.5%) or `50` (1.5%) — the same `HealthModAdjust`-shaped constant-with-rare-alternate pattern already seen at row #13's `0x64` and row #14's `0x3C` |

**A genuine three-way identity split, verified by crosstab:** every record has *at least one* of
GUID (`0x00`) or condition (`0x02`) populated — 0/1,683 records have neither. And whenever
inheritance (`0x04`) is used, the GUID is **always** also real (809/809 — 0 counterexamples). This
carves the 1,683 records into three clean groups: **809 (48%)** are named/inheritable — real GUID,
often (but not always) built via inheritance from another named record; **180 (11%)** are named but
freshly authored, no inheritance; **694 (41%)** are fully anonymous — no GUID at all, condition
present instead. Reads as two substitutable identity mechanisms for two different authoring patterns:
a reusable/referenceable template (GUID, optionally inherited) vs. a one-off conditional spawn
(condition only, never referenced again).

**Working model:** row #12 is the second-most cross-reference-heavy record in the file by declared
code (four resolved refs, same as row #16), but in practice **its declared cross-refs are almost
entirely dead** (all four ≤2.4% real) — the actual payload is the unresolved 10-slot array at
`0x32`-`0x44`, whose usage shape (46% front slot decaying to near-zero) closely mirrors row #4's
LUT-ref sub-arrays, raising a real possibility that *this* is where the old Java port's "Weapons[10]"
concept actually belongs, misattributed to row #4 in the original hypothesis — not confirmed, since
this array isn't resolved via any lookup call the way row #4's genuinely is. Combined with the
GUID/condition/inherit three-way split and the doc's earlier "spawn data for hercs, buildings...
ruins" comment, the strongest reading is still an entity/spawn-style record, but one where the
declared position/flag/action cross-refs (#6/#7/#10×2) turned out to be vestigial rather than the
record's real substance.

### How this was verified

Extended the confirmed walker to capture row #12's full 144-byte records (re-checked EOF-exactness
held at 61/62), pulled all 1,683 real instances, computed per-offset non-`-1` rates and ranges across
all 72 short-offsets (same exhaustive approach as row #16, given the record's size and the risk of
missing load-code-invisible fields), then followed up with targeted crosstabs once the marginals
suggested relationships worth confirming directly: `0x02`×`0x06` (compound condition), and GUID×
inherit×condition (the three-way identity split) — each checked as an explicit joint count, not
inferred from marginal percentages alone.

## Row #17 field decode — "LinkedRef58" (`DAT_0047064a`, 58 bytes/record)

Chosen to close out the small/medium rows — same "typed link" family as row #15, and the
macro-structure pass had only a size-based guess (`GUID+Flags[28]`) for it. Extracted all **127
real instances** across the 61 files not affected by the `DEMO2.MSN` truncation (that file's
tail is exactly this row — see "How this was verified" below).

**Structurally unusual: this record has no leading GUID field at all.** Its condition check reads
directly from offset `0x00` (every other row's condition sits at `0x02`, right after a GUID). This
makes sense once cross-checked against the macro-structure table: **row #17 is the only row nothing
else in the file references** — with no incoming refs, it has no need for the standard
insert-or-merge-by-GUID machinery every other row carries, and indeed its load loop has no dedup
step either (surviving records are just unconditionally appended).

| offset | field | real-data findings |
|---|---|---|
| `0x00` | condition ref | **rare** — 124/127 (98%) are `-1`; only 2 real records use it (adjacent flag values `79`/`80`) |
| `0x02` | ? | not read anywhere in this record's load loop, but not dead either: real binary distribution `1` (91/72%) / `0` (36/28%) |
| `0x04` | ? | not read in this loop; real distribution across 7 values (`0`-`7`), skewed toward `1` (67/53%) |
| `0x06` | discriminator | real distribution `0` (92/72%), `1` (22/17%), `3` (13/10%) — **`2` never occurs**, the same "declared-but-unexercised switch arm" pattern already confirmed for row #10's `0x06` and row #15's `0x10` |
| `0x08` | discriminated ref, per `0x06` | resolves into row #16/#12/#13/#14 per the standard 4-way motif (see row #15's decode for the same shape) |
| `0x0A` | ref into row #6 (`MapPoint22`) | **declared in the load code but never actually populated in any of the 127 real records** — always `-1`. The macro-structure pass's guess that this row cross-references #6 is real code but dead in practice |
| `0x0C` | ref into row #8 (`WaypointGroup`) | populated in 29/127 (23%) real records |
| `0x0E` | ref into the herc/unit-type LUT (`DAT_00470664`) | **dominant field** — populated in 118/127 (93%) real records, by far the most-used field in this record type |
| `0x10`-`0x39` (42 bytes) | nested pair-count array | **fully resolved this session — see below.** `0x10` is itself a pair-count (`0`/`1`/`2`), followed by that many 4-byte `(ref, tag)` pairs at `0x12`/`0x14`, `0x16`/`0x18`, … |

**Working model:** row #17 (`LinkedRef58`) is best read as "attach a specific herc/unit type" (its
dominant real field, `0x0E`) "to this waypoint group" (`0x0C`, secondary), with an optional
polymorphic entity tag (`0x06`/`0x08`) and rarely a trigger condition — plausibly a unit-spawn or
unit-assignment record tied to a patrol route, though not confirmed against a concrete consumer.

### The tail (`0x10`-`0x39`) — resolved

None of this span is touched by `FUN_00417b67`'s row #17 block (confirmed: no code references any
offset past `0x0E` in that block), so it can only be characterized from real data, not disassembly.
Re-analyzed all 127 real instances at the individual short-offset level (21 shorts, `0x10` through
`0x38`):

`0x10` is **always populated** (100%, values `0`/`1`/`2` only) and turns out to be a genuine
**pair-count discriminator** — the count of how many 2-short `(ref, tag)` pairs follow, not part of a
generic dead span at all. Verified exactly: 96 records have `0x10=0` (and every short from `0x12`
onward is `-1`), 28 records have `0x10=1` (and exactly one pair — `0x12`/`0x14` — is populated, rest
`-1`), 3 records have `0x10=2` (and exactly two pairs — `0x12`/`0x14` and `0x16`/`0x18` — are
populated, rest `-1`). This is the same nested-variable-length-array idiom already confirmed for row
#8's waypoint list, just with a 2-short pair element instead of row #8's single-short one, and a
count field embedded in the tail itself rather than in the record's fixed header.

Within each pair, the two elements have visibly different domains: the first (`0x12` in the first
pair) ranges `20`-`360` across 15 distinct values — wider than most ref fields in this file, plausibly
still LUT/GUID-shaped but not confirmed by any resolver call. The second (`0x14`) is startlingly
narrow: **only the values `6` or `7` are ever observed**, across all real pairs in the corpus. Slots
`0x1A`-`0x39` (declared capacity for at least 7 more pairs, going by the record's remaining byte
budget) are **never populated in any of the 127 real records** — the same "declared capacity far
exceeds real use" pattern found in nearly every other row in this file, just discovered here via raw
data rather than a visible loop bound in the disassembly.

With the tail resolved, row #17 is now **fully field-decoded**, closing out the last open item this
doc had for this row.

### How this was verified (rows #3/#7/#11/#17)

Same walker/method as the other decoded rows — extended the confirmed walker to capture all four
rows in a single pass (plus re-confirming rows #6/#8/#10 already captured, for the cross-reference
checks noted above), re-checked EOF-exactness held at 61/62 files first. `DEMO2.MSN`'s known
truncation (missing its final 42 bytes, isolated to row #17's tail) caused a real extraction
exception this time rather than just a byte-count mismatch, since row #17 is the very last row in
the file — handled by catching per-file and excluding that one file's row #17 record from the
sample (127 real instances drawn from the 61 unaffected files), consistent with how the macro-
structure pass already treated `DEMO2.MSN` as a known, isolated outlier.

### How this was verified (rows #4/#12/#13/#14/#16, and row #17's tail)

New session, scratchpad not preserved from the prior one, so `ES2/VOL/ZONES.VOL` was re-extracted
fresh (same throwaway-console-app-against-`HercWorks.Vol.Io.VolFileReader` pattern as every prior
extraction) and the 17-row walker was rebuilt from this doc's own confirmed table rather than from
memory of the old implementation. **First step before trusting any new extraction: re-ran the
rebuilt walker and confirmed it reproduced the exact same result already on record** — 61/62 files
land precisely on EOF, `DEMO2.MSN` throws on row #17 exactly as before — plus it reproduced the exact
same real-instance counts already published for every previously-decoded row (2,152 row #1 records,
194 row #3, 2,661 row #6, 105 row #7, 470 row #8, 335 row #9, 338 row #10, 72 row #11, 637 row #15,
127 row #17), which is a stronger correctness check than EOF-exactness alone since it confirms every
row's *record boundaries*, not just the file's final length.

With the walker re-verified, extended it to also capture rows #4 (60 real instances), #12 (1,683),
#13 (124), #14 (1,949), and #16 (1,247), plus re-captured row #17 specifically to analyze its
previously-unresolved 42-byte tail at the individual short-offset level. For the two largest/richest
records (#12, #16) — where a code-only reading of the load loop risked missing genuinely-used fields
the loop never explicitly resolves — computed per-offset non-`-1` rates and value ranges across
*every* short-offset in the record, not just the ones the disassembly calls out via a resolver
function; this is what surfaced several always-populated or clearly-structured fields (row #16's
`0x78` discriminated payload, row #12's `0x74`-`0x88` always-populated tail, both always-zero 18-short
dead zones) that a narrower, code-driven extraction would have missed entirely. Every marginal
distribution that looked like it might be part of a compound field (the `-99`-sentinel condition
pairs, the `0x08`↔`0x3C` correlation in row #14, the `0x78`-selected payload in row #16, the GUID×
inherit×condition split in row #12) was confirmed with an explicit joint crosstab before being
reported as a real relationship, not inferred from two marginal percentages alone.

## How to apply

- **The record table above is no longer a hypothesis — it's the confirmed macro-structure of the
  real file format**, byte-exact against 61 of 62 retail `.MSN` files. A real `MissionFileTransformer`
  rewrite should follow this exact row ordering/strides (including the skip-only row #5 and nested
  row #8's true on-disk width) rather than the current `MissionFileTransformer.cs`'s
  `TRAIN5.MSN`-hardcoded layout, which is now *known* to be wrong (no 189-short fixed block exists
  anywhere in the real format). **14 of 17 rows now have real field-level decodes** (#1, #3, #4, #6,
  #7, #8, #9, #10, #11, #12, #13, #14, #15, #16), plus row #17's previously-open tail is now resolved
  too, making #17 fully decoded. Most of the newly-modeled rows (#3/#4/#6/#7/#8/#9/#10/#11/#12/#15)
  have no current C# model at all, so that's greenfield work for the port; #1 has an exact size match
  to the existing `UnkHeaderEntry` type. Rows #13/#14/#16 have exact size matches to existing C#
  types (`UnkEntity102Bytes`/`MiscEntityInfo`/`UnkEntity164Bytes`) whose overall shape held up
  reasonably well under verification (real cross-refs were undercounted by the macro pass but the
  general "GUID + flags + refs + trailing scalar" shape was right). **Row #4 is the one case where
  the existing hypothesis (`UnitInfo`) was fully wrong, not just incomplete** — the real record has
  no GUID/identity field at all (contradicts `MapObject`'s assumed base class for this type) and none
  of `MapCoordId`/`UnitId`/`Weapons[10]`/`UnkFlags[36]`/`HealthModAdjust` exist in the real load code;
  any future C# model for row #4 needs to be built fresh from this doc's decode, not adapted from
  `UnitInfo.cs`.
- `ES2/VOL/ZONES.VOL` is now extracted for real use going forward: 62 `.MSN` files, 39 `.dat`, 39
  `.dba`, plus `LANG0.VOL`-style `.eng` mission strings inside the same VOL — all still unexplored.
  The extraction itself (folders: `MSN`, `DAT`, `DBA`) is a good map of what this VOL actually
  contains beyond missions.
- Record #1's condition/trigger opcode theory (`0x119`-`0x11e`) is **structurally consistent** with
  real bytes (revision field, first count, and record 0's type-discriminator/operand shape all
  decode sensibly) but the opcode semantics themselves (comparing against `DAT_00482af8`) haven't
  been independently cross-checked against a *known* campaign flag yet — that would be the next
  concrete test if trigger semantics matter before field-level decoding.
- The one outlier (`DEMO2.MSN`) is traced to an exact byte position (row #17 needs 58 bytes, only 16
  remain) but not fully root-caused — treat as a known, isolated, low-priority loose end rather than
  a reason to distrust the table, which 61/62 files (including `TRAIN5.MSN`) confirm exactly.
- `data\script.dat` is now fully resolved, see [`script-dat.md`](script-dat.md) — **it's a
  GUID-filtered, field-subset re-export of these same `.msn` row arrays**, written by `FUN_0041ac54`
  right after `.msn` parsing finishes, and read independently by both DBSIM (the real gameplay
  simulator) and VSHELL's own `ShellMap` map-editor UI. The "separate, map-rendering-focused
  structure" guess formerly here was wrong — see the superseded note in the reconciliation section
  above.
- Two distinct record types share several sizes (144 bytes: #4 vs #12; 22 bytes: #6 vs #15) — don't
  assume a byte-size match alone proves correspondence to a single current-model type; check
  cross-reference shape too, as this table did. (Confirmed for #6 vs #15: #6 is a flat
  `{GUID, X, Y, Z}` point record, structurally nothing like #15's "typed link" shape. Now also
  confirmed for #4 vs #12: despite sharing a size *and* both being candidate "spawn" records, #4 has
  no identity field and resolves three sub-arrays into a shared LUT, while #12 has the file's
  heaviest inheritance usage (48%) and a three-way GUID/condition identity split — genuinely
  different records that happen to share a byte count, same lesson as #6 vs #15.)
- **Field-level meaning is now resolved for 14 of the 17 rows**, all against real data the same
  rigorous way (extract every real instance across all 62 files, analyze per-offset distributions,
  cross-reference by GUID where possible — never reasoning from one file or from code shape alone):
  row #1 (`UnkHeaderEntry`, the trigger/flag store), row #3 (`VariantValue8`), row #4 (no stable
  name — refutes `UnitInfo`), row #6 (`MapPoint22`), row #7 (`Flag10`), row #8 (`WaypointGroup`),
  row #9 (`LinkOrReward12`), row #10 (`Action82`), row #11 (`ActionPair30`), row #12 (a second,
  distinct 144-byte type from #4), row #13 (`UnkEntity102Bytes`), row #14 (`MiscEntityInfo`), row #15
  (`LinkedRef22`), and row #16 (`UnkEntity164Bytes`) — plus row #17's previously-open 42-byte tail,
  making #17 fully decoded too. A recurring, worth-remembering finding across nearly all of them:
  **most declared array/discriminator capacity goes unused in retail missions** — row #9/#10's
  8-slot arrays never exceed ~4 real entries, row #11's 10-slot array never exceeds 1, row #12's/
  #16's declared cross-refs into #6/#7/#10 are used in the low single digits of a percent or less,
  row #17's newly-decoded tail array (declared capacity for 7+ pairs) never exceeds 2 real pairs, and
  several discriminator switches have arms that are valid code but simply never chosen by any shipped
  file (row #10's `0x06`, row #15's `0x10`, row #17's `0x06`). A real C# port should model the
  *actually-used* shape, not the full nominal capacity, while still round-tripping the raw bytes.
- **A second recurring pattern found this session: a `0x02`/`0x0X` "compound condition" pair**, where
  a second field is real if and only if the primary condition field (`0x02`) is also real, with the
  second field's value drawn from a narrow set including the sentinel `-99`. First spotted in row
  #15's `0x02`/`0x06`; now independently confirmed in row #16's `0x02`/`0x04` and row #12's `0x02`/
  `0x06`, always with a clean 100%-co-occurrence crosstab, never a counterexample across three
  separate rows and hundreds of real instances combined. Worth checking for in any future row decode
  that has an unexplained field sitting near a condition field.
- **A third recurring pattern: a trailing scalar field that's almost always one specific constant**,
  reading as a `HealthModAdjust`-style default — row #13's `0x64` (always exactly `100`), row #14's
  `0x3C` (`100` or `0`, tightly correlated with whether a type-like field earlier in the record is
  populated), and row #12's `0x8E` (`100` or, rarely, `50`). All three sit at the very end of their
  respective records. Worth checking first when a new row's final field looks numeric but low-variance.
- **Remaining undecoded rows**: only #12 (now decoded — see above) was flagged as "most involved" and
  that work is done. #2 (scratch, one-shot campaign patch application) and #5 (skip-only) are
  confirmed to need no further decoding — they're structurally closed, not merely low-priority. That
  leaves all 17 rows either field-decoded or explicitly out of scope.
