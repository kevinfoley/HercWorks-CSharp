# .MSN mission file (ZONES.VOL/MSN/*.msn) and its VSHELL load path

**Macro-structure: revision field + 17 array/skip rows in order (14 fully field-decoded), verified against all 62 real retail `.MSN` files.** Reversed from `VSHELL.EXE` disassembly and validated against real data; implemented in `HercWorks.Core.Io.Transform.Common.MissionFileTransformer`.

## Call chain — confirmed

- `FUN_0044d5bd` builds the path `msn\<name>.msn` (string `"msn\\%s.msn"` at `0047a2a6`,
  `%s` = a name substituted from `DAT_0048dc18+0x45`, with `^` sanitized to `_`), then calls
  `FUN_0041c73d(path)`.
- `FUN_0041c73d` (asserts trace to `msn_gen.cpp`) calls `FUN_00417b67(param_1)` **first, with the
  `.msn` path** — this is the raw-file parser. `FUN_0041ac54` then exports a subset of the loaded
  data as `data\script.dat` for downstream consumers (DBSIM and VSHELL's `ShellMap` UI).
- Separately, `ShellMap::ctor` (`FUN_00423f43`, vtable `&PTR_FUN_004721b0`) opens `data\mission.str`
  and `data\maplabel.str` as string tables, then reads `data\script.dat` directly via `FUN_004243d7`
  (source `shellmap.cpp`) — a UI-facing consumer of the same data exported from `.msn` parsing.
  See [`script-dat.md`](script-dat.md) for the relationship.
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

| offset | field | notes |
|---|---|---|
| `0x00` | ordinal/index | authoring-tool bookkeeping; never read at runtime |
| `0x02` | condition input | 43% populated; highest real-usage rate in file |
| `0x04` | type discriminator | 4 types: `0`/`2`/`3`/`1` (47%/39%/12%/1.5%) |
| `0x06` | flag-index / range-lower | values in increments of 50; suggests block-organized flags |
| `0x08` | operator code / result | type `0`: `0x119`-`0x11e` opcodes; type `2`: +49 offset pairs |
| `0x0A` | comparison operand | mostly `0` (71%) |
| `0x0C` | ? | **dead** — always `0` |

**Row #1:** shared flag/condition store; entries can gate on each other; organized in 50-element flag banks.

### The template-inheritance pattern

Most record types carry a "parent index" field: `-1` means "read this record's fields fresh from
the stream," any other value means "`memcpy` the already-loaded record at that index instead,"
sometimes with additional per-field overrides layered on top. This is a real, load-time
prototype/inheritance mechanism — missions can define an entity as "like entity N, but with these
fields changed" — not something the current C# port models at all.

### Record-array table — **empirically confirmed byte-exact against 61/62 real `.MSN` files**

Two corrections versus the first disassembly-only pass (caught by building a strict byte-walker and
testing it against every real file — see "Verification note" below):

- A **skip-only row** (`DAT_0047066a`) sits between the 144-byte array (#4) and the 22-byte array
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
| 12 | `DAT_00470652` | 144 (`0x90`) bytes/record | `DAT_00470614` | #6, #7, #10 (×2) — sparse in retail (≤2.4% used) but all live at runtime; real payload is a 10-slot weapon fit | **decoded — see "Row #12 field decode" below.** The mission's **mech roster**: one record per HERC it can field, with type, weapon fit and optional placement. A second, distinct 144-byte type from #4; heaviest template-inheritance usage of any decoded row (48%) |
| 13 | `DAT_00470654` | 102 (`0x66`) bytes/record | `DAT_00470618` | #6, #7 (both declared, both dead in retail), #10 (×2, only the 2nd slot real) | **decoded — see "Row #13 field decode" below.** `UnkEntity102Bytes` — real structure is a 20-flag boolean array + a mostly-inert second 20-slot span + a constant trailing field (always `100`), not the flat `Flags[49]` the old hypothesis assumed; the macro pass's "inherit only" note missed all four real cross-refs |
| 14 | `DAT_0047065c` | 62 (`0x3e`) bytes/record | `DAT_00470628` | #6, #7, #10 (×2) | **decoded — see "Row #14 field decode" below.** `MiscEntityInfo` — 4 real cross-refs, not the 3 the macro pass found (it missed #7); a type-like field at `0x08` correlates ~99% with the trailing constant field being `100` vs `0` |
| 15 | `DAT_00470658` | 22 (`0x16`) bytes/record | `DAT_00470620` | #6 (rare), #8 (dominant — 94% populated), #10 (rare), plus a **4-way** discriminated ref (0/1/2/3 → #16/#12/#13/#14, resolved in two passes since #16 loads after #15) | **decoded — see "Row #15 field decode" below.** A "typed link" record whose primary payload is a near-always-populated ref into row #8 — confirms it's structurally distinct from #6 (which is a flat position record), not just size-coincidentally 22 bytes |
| 16 | `DAT_0047065a` | 164 (`0xa4`) bytes/record | `DAT_00470624` | #6, #7, #8, #10, a **20-entry** discriminated-ref array (0/1/2 → #12/#13/#14), a 10-entry array into #15 | **decoded — see "Row #16 field decode" below.** `EntitySpawn164` — the 20-entry cross-ref array matches `MapEntIds[20]`/`MapEntities[20]` exactly; also has a compound-condition pair (`0x02`/`0x04`, `-99` sentinel), an 18-short always-zero dead zone, and a cleanly discriminated trailing payload (`0x78`: 0/1/2 → 0/2/4 populated fields) |
| 17 | `DAT_0047064a` | 58 (`0x3a`) bytes/record | `DAT_00470608` | #6 (declared, **never used in retail data**), #8, LUT `DAT_00470664` (dominant), a 4-way discriminated ref (0/1/2/3 → #16/#12/#13/#14) | **fully decoded, including the tail — see "Row #17 field decode" below.** Structurally unusual — no leading GUID field at all (this record is never referenced by anything else in the file); the 42-byte tail is a nested pair-count array, the same idiom as row #8's nested waypoint list |

`DAT_00470664` itself is never the subject of a count+array read in this function — it's used
throughout as a lookup-table size bound, strongly suggesting it's a shared table (plausibly
`HercLUT`) loaded once at VSHELL startup, not per-mission.

### Verification note

61 of 62 real `.MSN` files land on EOF with zero bytes of slack. **One outlier: `DEMO2.MSN`** undershoots by 42 bytes at row #17's tail, likely a stale/leftover developer test file with a genuinely truncated tail rather than evidence of a table error.

### Relationship to `data\script.dat`

`data\script.dat` is a GUID-filtered, field-subset re-export of these same `.msn` row arrays, written by `FUN_0041ac54` right after `.msn` parsing finishes. It is read independently by both DBSIM (the real gameplay simulator) and VSHELL's own `ShellMap` map-editor UI. For full verified block-by-block mapping and field-level detail on what each reader keeps vs. discards from each row, see [`script-dat.md`](script-dat.md) — treat that doc as authoritative.

## Row #6 field decode — "MapPoint22" (`DAT_0047064e`, 22 bytes/record)

Central spatial-reference table: `{GUID, X, Y, Z}` points (2,661 real instances across 62 files).

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | identity key |
| `0x02` | condition ref | **dead** — always `-1` |
| `0x04` | template/inherit index | **dead** — always `-1` |
| `0x06` | ? | **dead** — always `-1` |
| `0x08` | sum flag | **dead** — always `0` |
| `0x0A` | X (int32) | range 77,591–3,825,420 |
| `0x0E` | Y (int32) | range 17,968–3,800,672 |
| `0x12` | Z (int32) | range 0–35,400 (altitude) |


## Row #8 field decode — "WaypointGroup" (`DAT_00470656`, 10 fixed bytes + nested-count×2 bytes/record)

Ordered waypoint list, heavily referenced by row #15 (470 real instances across 62 files).

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | identity; 47/470 are `-1` (conditional records) |
| `0x02` | condition ref | 10% real usage; correlates exactly with GUID `-1` |
| `0x04` | parent/inherit | 2% real; aliases existing groups; only nested list is copied |
| `0x06` | ? | **dead** — always `-1` |
| `0x08` | nested count | 0–9 entries; mean 3.2 |
| nested | ref→row #6 | 2 bytes/entry; resolved to row #6 GUIDs |


Spatial validation: consecutive waypoints have median distance ~191k units (tighter than random pairs ~310k), confirming authored path structure. 24% of records form closed loops (first = last waypoint).

## Row #15 field decode — "LinkedRef22" (`DAT_00470658`, 22 bytes/record)

"Typed link" record; heavily references row #8 (637 real instances across 62 files).

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | identity key |
| `0x02` | condition ref | 5/637 real (0.8%); **compound pair** with `0x06` |
| `0x04` | parent/template | **dead** — always `-1` |
| `0x06` | condition operand | correlates 100% with real `0x02`; values {1, -99} |
| `0x08` | small int | range 0–6; meaning unclear |
| `0x0A` | small int | range 0–3; meaning unclear |
| `0x0C` | ref→row #6 | 7% real |
| `0x0E` | ref→row #8 | **94% real** — primary payload |
| `0x10` | discriminator | 0/−1/1/3 (445/159/25/8); `2` never occurs |
| `0x12` | discriminated ref | → rows #12/#13/#14/#16 per `0x10` |
| `0x14` | ref→row #10 | 2% real |


## Row #9 field decode — "LinkOrReward12" (`DAT_0047065e`, 12 bytes/record)

Dual-purpose: link (two row #6 refs) or reward (one row #6 ref + literal value). 335 real instances; 100% of row #10's sub-refs match row #9 GUIDs. Row #10's verb code correlates with the type: verb 3 is 97% link, verbs 1/2 lean reward.

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | identity key; dedup via GUID match |
| `0x02` | condition ref | **dead** — always `-1` |
| `0x04` | ? | **dead** — always `-1` |
| `0x06` | type flag | binary: 0→link (157 real), 1→reward (178 real) |
| `0x08` | ref→row #6 | always resolved; range 0–161 |
| `0x0A` | ref→row #6 OR literal | if `0x06=0`: row #6 index (87% adjacent to `0x08`); if `0x06=1`: literal value (100–20000 in round increments) |


## Row #10 field decode — "Action82" (`DAT_00470660`, 82 bytes/record)

Action/objective record; sparse payload (most 82 bytes are dead). 338 real instances; heavy cross-referencing from rows #12/#13/#14/#16.

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | identity key |
| `0x02` | condition ref | **dead** — always `-1` |
| `0x04` | ? | **dead** — always `-1` |
| `0x06` | type/discriminator | 0/1/3/4/7/9 seen; `8`/`10` never occur |
| `0x08` | verb code | 0–3; correlates with row #9 type (verb 3 → link-type, 1/2 → reward-type) |
| `0x0A–0x11` | ref[0..3]→row #9 | only 4 real slots; authored as row #9 GUIDs; 100% match rate |
| `0x12–0x19` | ref[4..7]→row #9 | **dead** — always `-1` |
| `0x1A–0x43` | (42 bytes) | **dead** — constant padding (`0000` + twenty `-1`s) |
| `0x44–0x45` | ref→herc/unit LUT | 1% real |
| `0x46–0x4D` | ref[1..4]→LUT | **dead** — always `-1` |
| `0x4E` | secondary value | 0 dominant; meaning unclear (timer? sequence index?) |
| `0x50` | polymorphic target | type chosen by `0x06` (0/1/3/4 → rows #12/#13/#14/#16); 1% real |


## Row #3 field decode — "VariantValue8" (`DAT_00470666`, 8 bytes/record)

Condition-gated variant table; same GUID with different conditions/payloads (194 real instances). Referenced by row #4.

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | identity (multiple instances per GUID possible) |
| `0x02` | condition ref | 44% real; second-highest condition usage in file (after row #1) |
| `0x04` | ? | compound pair: `-1` or `-99`; `-99` correlates 100% with real `0x02` |
| `0x06` | payload | 54 distinct values; fetched by row #4 |


## Row #7 field decode — "Heading10" (`DAT_00470650`, 10 bytes/record)

Heading record (degrees → BAM conversion). 105 real instances; simplest record type in file.

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | identity key |
| `0x02` | condition ref | **dead** — always `-1` |
| `0x04` | parent/inherit | **dead** — always `-1` |
| `0x06` | ? | **dead** — always `-1` |
| `0x08` | payload | 0/1/10 (62%/34%/4%); multiplied by 182 → degrees to BAM |


## Row #11 field decode — "ActionPair30" (`DAT_00470662`, 30 bytes/record)

Paired actions; nominal 10-slot array is dead (96% use ≤1 slot). 72 real instances.

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | identity key |
| `0x02` | condition ref | **dead** — always `-1` |
| `0x04` | ? | **dead** — always `-1` |
| `0x06` | ref→row #10 | 82% real; dominant field |
| `0x08` | timer | round numbers (10/30/60/120 etc.); seconds likely |
| `0x0A–0x1D` | nominal ref[1..9]→row #10 | **dead** — 96% use only slot 0; rest always `-1` |


## Row #4 field decode — "RewardPackage144" (`DAT_00470668`, 144 bytes/record)

**NOT UnitInfo** — no GUID field, completely refutes that C#type. Mission-level singleton (60 instances across 62 files).

| offset | field | notes |
|---|---|---|
| `0x00` | condition ref | **dead** — always `-1` |
| `0x02–0x14` | sub-array A→LUT (10 slots) | 1–4 real slots; mean 2.3 |
| `0x16–0x50` | sub-array B→LUT (30 slots) | 0–4 real slots; mean 1.8 |
| `0x52–0x8c` | sub-array C→LUT (30 slots) | 0–3 real slots; mode 1 |
| `0x8e` | ref→row #3 variant | 87% real; dominant field; fetches payload value |


## Row #13 field decode — "UnkEntity102Bytes" (`DAT_00470654`, 102 bytes/record)

Item flags + condition/inheritance (24%/30% real usage — highest combined rates in file). 124 instances.

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | 76% real; identity key |
| `0x02` | condition ref | 24% real (tier: rows #1/#3/#13) |
| `0x04` | parent/inherit | 30% real; copies blocks A/B if set |
| `0x06` | ? | **dead** — always `-1` |
| `0x08–0x30` | flags block A (20 shorts) | 100% populated; boolean: 96.5% `0`, 3.5% `1` |
| `0x30` | ref→row #6 | always `-1` in retail, but **not dead** — DBSIM reads it as this flyer's spawn-position override (see `script-dat.md`) |
| `0x32` | ref→row #7 | same, for heading |
| `0x34` | presence flag | 68% real; always `0` if present |
| `0x36` | ? | nearly always `0` |
| `0x38–0x60` | flags block B (20 shorts) | **dead** — 99.9% `-1` |
| `0x60` | ref→row #10 slot 1 | **dead** — always `-1` |
| `0x62` | ref→row #10 slot 2 | **only live ref** — 21% real |
| `0x64` | constant | always exactly `100` |


## Row #14 field decode — "MiscEntityInfo" (`DAT_0047065c`, 62 bytes/record)

Entity type + modifier. Largest sample (1,949 instances); clear `0x08`/`0x3C` correlation.

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | 99% real |
| `0x02` | condition ref | 30% real (tier: rows #1/#3/#13) |
| `0x04` | parent/inherit | 0.4% real; nearly dead |
| `0x06` | ? | **dead** — always `-1` |
| `0x08` | **base type** | 71% real; range 0–56 (43 values) — an index into `dat\BASES.DAT`'s 65-entry structure table, which names the model and its texture bank |
| `0x0A` | ref→row #6 | 6.4% sparse — this structure's spawn-position override |
| `0x0C` | ref→row #7 | 6.7% sparse — its heading |
| `0x0E` | small discrete | 100% real; 0/1/2 (64%/33%/3%) |
| `0x10–0x38` | block (20 shorts) | 3.9% sparse |
| `0x38` | ref→row #10 slot 1 | 0.4% rare |
| `0x3A` | ref→row #10 slot 2 | 0.1% dead |
| `0x3C` | health modifier | 100%: `100` (71%) or `0` (29%); **100% correlates with `0x08` real** |


## Row #16 field decode — "EntitySpawn164" (`DAT_0047065a`, 164 bytes/record)

Entity-activation directive; position/flag/route/action + 20-entry discriminated refs. 1,247 instances. DBSIM uses this to spawn live entities.

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | 100% real |
| `0x02` | condition ref | 2.5% sparse; **compound pair** with `0x04` |
| `0x04` | condition operand | 1.4% real; always `-99` when populated |
| `0x06` | binary flag | 100% real; 39/61 split |
| `0x08` | near-constant | 100% real; usually `0` |
| `0x0A–0x2C` | dead zone (18 shorts) | **always `0`** — padding |
| `0x2E` | discriminator | 89% real; 0/1/2 — selects which row the `0x38` array's entries point at (rows #12/#13/#14) |
| `0x30` | **formation id** | 85% real; range 0–16 — indexes the formation-offset table that spreads a group's members around its point (see `script-dat.md`'s placement section) |
| `0x32` | ref→row #6 | 37% real — **the group's spawn point** |
| `0x34` | ref→row #7 | 45% real — **the group's heading** |
| `0x36` | ref→row #8 | 43% real — the group's patrol route |
| `0x38–0x5E` | 20-entry discriminated refs | slot 0: 89% real → slot 8: 0.6% → slots 9–19: never used |
| `0x60–0x72` | 10-entry ref→row #15 | slot 0: 47% real → slot 3+: never used |
| `0x74` | tri-state flag | 89% real; 0/1 or `-1` |
| `0x76` | ref→row #10 | 31% real |
| `0x78` | discriminator | 100% real; 0/1/2 (97%/2.8%/0.5%); selects trailing payload |
| `0x7A` | payload 1 | if `0x78≥1`; range 20–650 |
| `0x7C` | payload 2 | if `0x78≥1`; values {2, 23} only |
| `0x7E` | payload 3 | if `0x78=2` only |
| `0x80` | payload 4 | if `0x78=2` only; always `2` |
| `0x82–0xA0` | dead zone (16 shorts) | **always `-1`** — padding |
| `0xA2` | trailing flag | 6% sparse; 0/1 |


## Row #12 field decode — "EntityTemplate144" (`DAT_00470652`, 144 bytes/record)

Entity template/spawn; highest inheritance usage (48%). Three-way identity split: GUID-based template (48%), fresh GUID (11%), or conditional-only (41%). 1,683 instances.

| offset | field | notes |
|---|---|---|
| `0x00` | GUID | 59% real (or `-1` for condition-only) |
| `0x02` | condition ref | 43% real (tier: rows #1/#3/#13/#14) |
| `0x04` | parent/inherit | 48% real (highest in file); copies 4 blocks if set |
| `0x06` | condition operand | 3.9% real; compound pair with `0x02` |
| `0x08` | binary flag | 100% real; 0/1 |
| `0x0A` | near-constant | 100% real; mostly `0` (91%); bitmask-like |
| `0x0C–0x2E` | dead zone (18 shorts) | **always `0`** — padding |
| `0x30` | small discrete | 47% real; range 0–20 |
| `0x32–0x44` | **weapon fit**, 10 slots | **real workhorse**: slot 0: 46% real → slot 9: 0.1%; bursty population. Resolved via `script.dat`: DBSIM hands this array straight to `Mech_ConfigureLoadout`, the same call the player's own fit from `player.mec` goes through |
| `0x46` | ref→row #6 | 0.1% populated in `.msn` data, but **not dead** — this is the spawn-position override DBSIM reads per mech (see `script-dat.md`); unset means "use the group's point" |
| `0x48` | ref→row #7 | same, for heading |
| `0x4A` | small discrete | 100% real; 0–4 (84% `0`) |
| `0x4C–0x5A` | sparse paired array | 5 pairs; decay: 15.9% → 0.5%; pairs have (wide-range, narrow-tag) structure |
| `0x74–0x84` | always-populated block | 100% real; 6 shorts; values 0–5, trending up |
| `0x86` | constant | always `5` |
| `0x88` | constant | always `2` |
| `0x8A` | ref→row #10 slot 1 | 0.7% dead |
| `0x8C` | ref→row #10 slot 2 | 2.4% dead |
| `0x8E` | health modifier | 100% real; `100` (98.5%) or `50` (1.5%) |

**Model:** Template/spawn with high inheritance/condition usage. The payload is the 10-slot **weapon fit** at `0x32`, plus the per-mech spawn-position and heading overrides at `0x46`/`0x48` — sparsely populated but live. Three identity patterns: reusable template (GUID+inherit), fresh template (GUID only), or conditional spawn (no GUID).

## Row #17 field decode — "UnitSpawn58" (`DAT_0047064a`, 58 bytes/record)

Unit-spawn/assignment to waypoint group. No GUID (unreferenced row). Nested pair-count array in tail. 127 instances (61 files; `DEMO2.MSN` truncated here).

| offset | field | notes |
|---|---|---|
| `0x00` | condition ref | 2% real (values 79/80 only) |
| `0x02` | ? | 100% real; binary: 72%/28% |
| `0x04` | ? | 100% real; range 0–7; mode `1` (53%) |
| `0x06` | discriminator | 0/1/3 (72%/17%/10%); `2` never occurs |
| `0x08` | discriminated ref | → rows #16/#12/#13/#14 per `0x06` |
| `0x0A` | ref→row #6 | **dead** — always `-1` |
| `0x0C` | ref→row #8 | 23% real |
| `0x0E` | ref→herc/unit LUT | **93% dominant** — core payload |
| `0x10–0x39` (42 bytes) | nested pair-count array | `0x10`: count (0/1/2); pairs at `0x12`/`0x14`, `0x16`/`0x18` |

**Nested pairs:** first element 20–360 (15 distinct values, LUT-like); second element {6, 7} only. Declared capacity (7+ pairs) never populated; actual max 2.


## How to apply

- **The record table is byte-exact against 61 of 62 retail `.MSN` files**, and is implemented: `HercWorks.Core.Io.Transform.Common.MissionFileTransformer` walks the rows in this order, including skip-only row #5 and nested row #8's 2-bytes-per-entry width. Each row has a model under `HercWorks.Core.Data.File.Msn/`.

- **Recurring pattern: most declared array/discriminator capacity goes unused in retail.** C# models should expose the actually-used shape, not full nominal capacity, while still round-tripping raw bytes.

- **Recurring pattern: `0x02`/`0x0X` "compound condition" pairs** — second field is real only when `0x02` is, drawn from a narrow set including sentinel `-99`. Confirmed in rows #12/#15/#16.

- **Recurring pattern: trailing scalar fields that are almost always a specific constant** — row #13's `0x64` (always `100`), row #14's `0x3C` (`100`/`0`), row #12's `0x8E` (`100` or `50`).

- **Note:** `DEMO2.MSN` undershoots by 42 bytes at row #17; treat as a known outlier rather than a table error.
