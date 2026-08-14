# `data\script.dat` — the real DBSIM gameplay handoff format

**Format summary:** A GUID-filtered, field-subset re-export of the same in-memory `.msn` row arrays populated by `FUN_00417b67` (the `.msn` parser). Written by `FUN_0041ac54` (`WriteScriptDatFile`, VSHELL) immediately after `.msn` parsing completes. DBSIM reads `script.dat` (not `.msn`) for actual gameplay simulation. Every one of `script.dat`'s 13 count-prefixed record blocks maps 1:1 to one of [`msn-mission-file.md`](msn-mission-file.md)'s 17 already-decoded rows.

## Call chain — confirmed

- `WriteScriptDatFile` (`FUN_0041ac54`, VSHELL, `msn_gen.cpp`) — called from `FUN_0041c73d` right
  after `FUN_00417b67` (`.msn` parser) finishes, once per mission load. Writes `data\script.dat`: a
  fixed 20-byte header + 13 count-prefixed record blocks, from the same `DAT_0047xxxx` globals the
  `.msn` parser fills.
- `DBSim_LoadScriptDat` (`FUN_00424308`, DBSIM) — sim-side reader, **pass 1**. Opens
  `data\player.mec`, `data\mission.str`, `data\script.dat` (in that order) during world init; reads
  the same 13-block structure, same order, matching strides.
- `DBSim_SpawnMissionObjects` (`FUN_004253d8`, DBSIM) — **pass 2**, builds the world. Re-opens
  `data\script.dat` from the top, skips blocks 1-6, walks blocks 7-13 again: constructs one object
  per slot pass 1 marked live, reads its position, heading, loadout and action links from its own
  record. See "The two-pass read" below.
- `ShellMap_LoadScriptDat` (`FUN_004243d7`, VSHELL, `shellmap.cpp`), called from
  `ShellMap_Constructor` (`FUN_00423f43`) — VSHELL's map-editor reader, independent of DBSIM's. Same
  13-block structure and order; keeps a different subset per block (see per-block table). Confirms
  record shape/strides via a second, independently compiled reader.

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
genuinely distinct files there — 10 total). **Note:** these 10 are snapshots of the format, not 10
distinct retail missions — how many distinct missions exist is a separate, unverified question.

**Every file is exactly 13,520 bytes** despite wildly different real record counts per block (e.g.
row #16's count ranges 7-40 across the corpus) — a fixed-size preallocated buffer, not a
tightly-packed variable-length file like `.msn`.

All 10 parse cleanly, zero desync in the block sequence; only 1 lands exactly on EOF, the rest carry
stale trailing bytes past block 13's declared end (the buffer is reused without truncation). **Read
only through block 13's declared end; ignore trailing bytes.**

## The two-pass read — and what it means for "DBSIM keeps"

DBSIM reads `script.dat` **twice**, and the two passes want different things:

| pass | function | what it does |
|---|---|---|
| 1 | `DBSim_LoadScriptDat` (`00424308`) | Counts. Keeps blocks 1-6 (the shared reference tables: coordinates, headings, waypoint groups, links, actions, action pairs). For blocks 7-9 it keeps **only the type field** of each record, and for block 11 it keeps **only which slots each record activates**. It then allocates one object pool per class, sized to the live count. Nothing is placed. |
| 2 | `DBSim_SpawnMissionObjects` (`004253d8`) | Builds. Re-opens the file, skips blocks 1-6, and re-reads blocks 7-13 in full — constructing each live object and reading its position ref, heading ref, weapon fit and action links straight out of its own record, then grouping them from block 11. |

**Pass 1 alone looks like `script.dat` carries no placement data** (blocks 7-9 reduced to one field
each, block 10 discarded, block 11 just flipping activation flags) — that describes pass 1 only, not
the format. Pass 2 reads the rest.

Per record type, what pass 2 reads (offsets into the exported record, not the `.msn` source row):

| block | record | offset | field |
|---|---|---|---|
| 7 (mechs) | 134B | `0x28` | mech type → index into `nam\MECHS.NAM` |
| | | `0x2a`-`0x3d` | weapon fit, 10 slots → `Mech_ConfigureLoadout` |
| | | `0x3e` | ref → block 1 (position) |
| | | `0x40` | ref → block 2 (heading) |
| | | `0x42`-`0x69` | two more 10-slot arrays → `FUN_00411b90` |
| | | `0x80`/`0x82` | refs → block 5 (actions) |
| 8 (flyers) | 92B | `0x28` | ref → block 1 (position) |
| | | `0x2a` | ref → block 2 (heading) |
| | | `0x2c` | flyer type → index into `nam\FLYERS.NAM` |
| 9 (bases) | 52B | `0x00` | base type → index into `dat\BASES.DAT`'s 65-entry table |
| | | `0x02` | ref → block 1 (position) |
| | | `0x04` | ref → block 2 (heading) |
| 11 (groups) | 156B | `0x28` | discriminator: 0/1/2 → block 7/8/9 |
| | | `0x2a` | **formation id** (see below) |
| | | `0x2c` | ref → block 1 (the group's own spawn point) |
| | | `0x2e` | ref → block 2 (the group's heading) |
| | | `0x32`-`0x59` | 20 member refs into the block the discriminator names |
| | | `0x5a`-`0x6d` | 10 refs → block 10 (route links) |
| | | `0x70` | ref → block 5 (action) |

## Placement — the actual rule

1. **Existence.** Every block-11 record *past the first* activates its members. A roster slot no
   block-11 record names never spawns, which is why a mission's rosters are routinely bigger than
   its live object count (the retail `script.dat` fields 7 of its 13 mechs).
2. **Position.** `FUN_00423b34` builds each block-11 record into a group record carrying its point,
   heading and member list; `FUN_00417aa8` (mechs) / `FUN_00421ee8` (flyers) / `FUN_00405c3c`
   (bases) then attach each member, filling in the member's position **only if it does not already
   have one**. So a roster record's own position ref wins where set, and the group's is the
   fallback. In every retail file the roster refs are unset, so in practice objects stand at their
   group's point.
3. **Groups with no point** fall back to their route: the group's block-10 links resolve to a
   waypoint group, whose first waypoint is the spawn point (`FUN_00423b0c`). Patrol lances are
   placed this way.
4. **Ground height** is not in the file. Mechs and bases get `Terrain_HeightQuery` plus the type's
   own foot offset (`typeRecord+0x16`, and +5000 when `typeRecord+0x50` is set); flyers get no query
   at all — they hold the spawn coordinate's Z, or 5000 units when that is zero.
5. **The player's lance is not in `script.dat`.** Block 11's **record 0** exists only to hold its
   spawn point — pass 1 skips it when marking activation, and pass 2 overwrites its member list with
   the entries read from `data\player.mec`. That file's own format is decoded in
   `HercWorks.Core`'s `MecFile`.
6. **Formation spread** is applied per member: the member's slot index *within the group's
   `DiscriminatedRefs` array* (0-19, not a compacted live-member count — `FUN_00423b34` passes the
   raw loop index straight through) goes to the object's own vtable `+0x78`, and that offset is
   rotated by the group leader's heading before being added to the group's point. Slot 0 (the first
   member the group claims) always takes no offset, so it lands exactly on the group's point.

   - **Mechs — unresolved.** Vtable `+0x78` is `Mech_ApplyFormationOffset` (`FUN_00417898`), which
     reads `Formation_GetSlotOffset(formationId, slot)` (`FUN_004205cc`) — a table of 28 bytes per
     formation, seven (x, y) short pairs. `dat\MFORMS.DAT` fits exactly (142 content bytes = a count
     plus five 28-byte formations; formation 0's seven pairs are a symmetric wedge at 10,000-unit
     steps), but the pointer `FUN_004205cc` reads has exactly one reference in all of DBSIM.EXE —
     that read — and lives in uninitialised data, so nothing links the file to the table. Not
     implemented in the engine.
   - **Bases — resolved and implemented.** Vtable `+0x78` is `FUN_00405c04` for every base subtype
     (confirmed across all five base vtables: `0x497940`/`0x4979d4`/`0x4978ac`/`0x497784`/
     `0x497818`, each derived from the common base vtable `0x49a54c`). When the member's slot is
     nonzero it calls `FUN_00405b9c(formationId, slot)`, which — unlike the mech table — reads
     through a **confirmed load site**: `FUN_00405fac` (`base.cpp`) streams `dat\BFORMS.DAT`
     (literally opened via the string `"bforms"`) into the global table `FUN_00405b9c` indexes.
     Byte-exact verified: the retail file is 3,186 content bytes; walking it as a count (17,
     matching the block-11 formation id field's documented 0-16 range) followed by, per formation, a
     slot count + that many 10-byte (x:int32, y:int32, trailing:int16) entries + an 8-byte grid-snap
     pair + a variable-length trailer, consumes exactly 3,186 bytes with nothing left over; formation
     0's seven offsets are a visibly symmetric wedge (one point dead ahead, three mirrored left/right
     pairs behind). The rotate-and-add step (`Formation_RotateAndAddOffset`, `FUN_00411d64`, calling
     `FUN_0047eaac`/`FUN_0047fedc`) reduces — for a heading-only, no-pitch-no-roll object, which every
     base is — to a plain 2D rotation: `worldDX = dx·cosθ − dy·sinθ`, `worldDY = dx·sinθ + dy·cosθ`,
     added to the group's point. Implemented in `Herculan.Engine.World.BaseFormationTable` and wired
     into `MissionLoader.AddRoster`'s base loop; verified against all 10 available missions
     (`DATA\script.dat` plus `SAV\script{0-6,10,11}.dat`) — every multi-member base group now gets
     distinct member positions (18 of 18 checked), where every one previously collapsed onto its
     group's single point.
   - **Not implemented: grid-snap.** When the block-11 record's own `BinaryFlag` (`0x06` in the
     `.msn` row) is set, `FUN_00405c3c` additionally snaps the group's *shared* point to a
     per-formation grid, using three `BFORMS.DAT` fields this reader skips (a cell-size class and two
     axis multipliers), before the per-member offset above is added. That step moves the whole
     group's anchor by one amount shared by every member, so it does not cause the stacking bug this
     section fixes — it wasn't chased further. `BinaryFlag` is set on roughly a third of retail
     block-11 records (39/61 split per the row #16 field table below), including several of the
     multi-member base groups checked above, so this is a real, if lower-priority, gap.
   - **Flyers — unfixed either way.** `FUN_00421ee8` is the flyer attach equivalent; not traced.
     Retail missions were not seen putting more than one flyer in a group, so it has not mattered in
     practice.

## The 13-block structure

The "DBSIM keeps" column below describes **pass 1 only** — see the two-pass section above for what
pass 2 goes back for.

| # | `.msn` row | on-disk shape | GUID-filtered? | DBSIM pass 1 keeps | VSHELL `ShellMap` keeps |
|---|---|---|---|---|---|
| header | — | fixed 20 bytes, 10 shorts from unrelated `DAT_004854xx` globals (not part of the 17-row `.msn` table at all) | — | parses into several scalar fields, one passed on to a later call | parses into several scalar fields, one passed on to a later call |
| 1 | #6 `MapPoint22` | count + count×12B (X,Y,Z int32 triple only — GUID/condition/etc. dropped) | yes | full (min/max bbox tracked live as read) | full |
| 2 | #7 `Flag10` | count + count×2B (the `0x08` payload short only) | yes | full, **× 182 (`0xb6`) at load** — the same degrees→BAM conversion already confirmed elsewhere in DBSIM; this reframes row #7's payload as **a heading/orientation in degrees**, not a difficulty tier | full, **not** multiplied (VSHELL just displays/edits it) |
| 3 | #8 `WaypointGroup` | count + per record: nested-count (2B) + nested-count×2B (waypoint refs into block 1) | yes | full, nested refs resolved to block-1 pointers (stride 3 ints) | full, same resolution |
| 4 | #9 `LinkOrReward12` | count + count×6B (`0x06` type flag, `0x08` ref1, `0x0A` ref2/literal) | yes | full, resolved into a 10-byte in-memory record | **skipped** (seek past, discarded) |
| 5 | #10 `Action82` | count + count×74B (`0x06` type, `0x08` verb, `0x0A`-`0x19` ref[0..7] into row 9, `0x1C`/`0x1E`-stride interleaved 20-short span, `0x44`-`0x4D` herc-LUT ref[0..4], `0x4E` secondary, `0x50` target) | yes | reads all 74B but only **keeps** type, verb, the 8 refs (resolved to row-9 pointers), the 40-byte interleaved span, secondary (decremented by 1), and target — **the herc-LUT refs are read then discarded**, DBSIM has no use for the cosmetic/economy LUT | **skipped** (seek past, discarded) |
| 6 | #11 `ActionPair30` | count + count×24B (`0x06` ref into row10, `0x08` type/timer, `0x0A`-`0x1D` 10-slot ref array into row10) | yes | full, resolved via `DBSim_BuildActionPairRecord` (`FUN_00423104`) into target+type+10×ref | **skipped** (seek past, discarded) |
| 7 | #12 (144B type) | count + count×134B (`0x08`-`0x2F` 40B span, `0x30` `SmallDiscrete`, `0x32`-`0x45` 20B span, `0x46`/`0x48` 2 shorts, two 20-short interleaved spans, `0x74`-`0x87` 20B span, 4 trailing shorts; `SmallDiscrete2` at `0x4A` is the one field of row #12 skipped/not exported) | yes | reads all 134B but keeps **only `SmallDiscrete` (`0x30`)**, the mech type — confirmed via the writer's own assert string on this field ("Invalid mech type"). Pass 2 comes back for the rest | **full 134B kept** — the map editor needs the whole record (name, position refs, etc.) to render/edit a placed unit |
| 8 | #13 `UnkEntity102Bytes` | count + count×92B (`0x08`-`0x33` `FlagsA`+refs, `0x34` `BinaryField`, `0x38`-`0x5F` `FlagsB`, `0x60`-`0x64` refs+`UnkVal_100`; `Unk36` at `0x36` is skipped/not exported) | yes | reads all 92B but keeps only **`BinaryField` (`0x34`)**, the flyer type. Pass 2 comes back for the rest | **skipped** (seek past, discarded) |
| 9 | #14 `MiscEntityInfo` | count + count×52B (`0x08` `TypeLikeScalar`, `0x0A`-`0x3D` refs+`SparseBlock`+`TrailingField`) | yes | reads all 52B but keeps only **`TypeLikeScalar` (`0x08`)** — the base type, an index into `dat\BASES.DAT`'s 65-entry table. Pass 2 comes back for the rest | **full 52B kept** |
| 10 | #15 `LinkedRef22` | count + count×14B (`0x08`-`0x14`, the 7 fields `msn-mission-file.md` decoded as small-int/refs/discriminator) | yes | reads all 14B and **discards it entirely** — DBSIM has no use for the "attach route to entity" authoring metadata | **full 14B kept** — this is exactly the UI-relevant "which route/position/entity is this linked to" data a map editor needs |
| 11 | #16 `UnkEntity164Bytes` | count + count×156B (two 40B/20B spans, a 20-entry nested cross-ref array with a 3-way discriminator, trailing shorts) | yes | **this is DBSIM's entity-activation mechanism**: for each populated cross-ref entry, the discriminator (0/1/2) marks the referenced **block-7/block-8/block-9** slot as a *live, simulated* object (via `DAT_004aa7ae`/`DAT_004aa8da`/`DAT_004aa93e`+`DAT_004aaa56` flag arrays), turning declared roster entries into things DBSIM actually spawns. **Record 0 is skipped here** — it is the player-lance placeholder. Pass 2 comes back for the group's own position/heading/route | full 156B kept, cross-refs resolved to annotate the kept row-#14 records (a UI/display-oriented resolution, not the "activation" one) |
| 12 | #17 `LinkedRef58` | count (unfiltered — all records, matching row #17's "no GUID field" nature) + count×54B | **no** | reads all 54B and **discards it entirely** | **skipped** (seek past, discarded) |
| 13 | #4 (no stable name) | flat tail: **one** count (how many of row #4's 10-slot sub-array A are populated, from its front — assumes no gaps) + that many×2B (the populated LUT-ref prefix itself) | n/a — single mission-level record, not a per-entity array | full — **this is the mission's herc/weapon unlock package** reaching DBSIM, matching `msn-mission-file.md`'s row #4 "working model: per-mission reward/unlock package" | not read (VSHELL's `ShellMap` reader stops after block 12; it has no use for player loadout data) |

## Verification

Three independent real readers (`DBSim_LoadScriptDat`, `DBSim_SpawnMissionObjects` and
`ShellMap_LoadScriptDat`) agree on identical block order and strides. Byte-walker tested against 10
real files (`ES2\DATA\script.dat` + 9 distinct save-slot snapshots); all parse cleanly with zero
desync, and `ScriptDatTransformer` round-trips all 10 byte-exact through end of block 13.

The placement decode is verified end to end by building all 10 as scenes in the HERCULAN Engine:
every one resolves its zone, theater, rosters, groups and player lance with no unclaimed live slots,
and every placed object lands inside its zone's bounds.

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
- `HercWorks.Core.Data.File.Sav.MecFile` + `MecFileTransformer` — `data\player.mec`, the player's lance.
- `Herculan.Engine.World.ScriptDatHeader` — the engine-side header port.
- `Herculan.Engine.World.MissionLoader` — the two-pass placement rule above, producing a `Mission`
  of resolved placements. `UnitTypeNames` (`nam\MECHS.NAM`/`FLYERS.NAM`) and `BaseTypeTable`
  (`dat\BASES.DAT`) resolve the three type numberings.
- Blocks 7-9 name the fields pass 2 reads (type, position ref, heading ref, and block 7's weapon
  fit) and round-trip the rest raw as `HeadBytes`/`TailBytes`. Blocks 5 and 11 split an interleaved
  source span into parallel `ArrayA`/`ArrayB` (even source offsets in A, odd in B) to match the
  writer's on-disk order.
