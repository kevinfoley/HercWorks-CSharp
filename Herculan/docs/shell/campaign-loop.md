# The campaign loop

How VSHELL starts a campaign, hands a mission to DBSIM, and folds the result back into the save. **Every address in this doc is in `VSHELL.EXE`**; the shell's source module is named in the assertion strings for each function cited.

The shell and the simulator are separate processes that never run at the same time. They communicate entirely through loose files in `data\`, and the shell is *relaunched* when the mission ends — `FUN_00401525` (`vshell.cpp`) responds to a `-r` command-line switch (`"-r -R Returning from sim"` in the usage text) by loading slot 10 and running the debrief immediately:

```
FUN_0040e4f2(10, 0);   // load the campaign autosave
FUN_0040eae7();        // consume results.dat
```

## The files crossing between the two binaries

| File | Written by | Read by | Carries |
|---|---|---|---|
| `data\mission.var` | shell, `FUN_0040e9cb` | DBSIM | the 2000-byte campaign flag array |
| `data\player.mec` | shell, `FUN_0040f0d4` | DBSIM | the player's HERC, wingmen and their fits |
| `data\script.dat` | shell, `WriteScriptDatFile` | DBSIM | the mission itself |
| `data\mission.var` | DBSIM, `FUN_0042412c` | shell, `FUN_0040ea59` | the same flags, mutated by the mission |
| `data\results.dat` | DBSIM | shell, `FUN_0040eae7` | outcome, salvage, damage and per-pilot counters |

`mission.var` appears twice deliberately: it is a round trip. The shell writes the flag array out before launch and reads the same file back at debrief.

### The campaign flag array is the `.msn` condition store

`maybe_CampaignFlagArray` (`00482af8`, 1000 `int16`) is the same array three separate systems touch:

- the `.msn` condition/trigger opcodes compare against it while filtering record arrays at load ([`../formats/msn-mission-file.md`](../formats/msn-mission-file.md));
- it is persisted in every save slot, immediately after the salvage pool ([`../formats/save-games.md`](../formats/save-games.md));
- it is the entire content of `data\mission.var`, in both directions.

Slot 0 is overwritten at debrief with the mission's outcome code (`_maybe_CampaignFlagArray = DAT_00482ae9`). On the simulator side the same array is `DAT_004a9ef4`, which an activating action bumps or clears and `FUN_0042412c` dumps to `mission_var` at mission end — see [`../simulation/mission-deployment.md`](../simulation/mission-deployment.md).

It is also what unlocks weapons: the mission-load path grants a pending unlock when its flag slot holds the expected value, setting the weapon's `weapons.dat` `+0x16` byte and clearing the slot ([`../formats/weapons-dat.md`](../formats/weapons-dat.md)).

## Starting a campaign — `FUN_0040e2ed`

Takes the pilot name and the mode flag (`DAT_0048260c`: 1 campaign, 0 training; the training entry points pass the literal `TRAINEE`). It loads `gam\weapons.dat`, generates the pilot roster, loads `gam\hercs.dat`, initializes the career position, and seeds the salvage pool:

```
DAT_00482af4 = rand(0..10) * 1000 + 100000;
```

Retail `GAME_T.SAV` holds exactly 107,000 salvage — an untouched training start. The pool is in kilograms and every screen divides by 1000 to print tons ([`armory.md`](armory.md#one-currency-two-units)).

The two catalog loads also stock the player: `gam\weapons.dat`'s trailing block gives the armory 39 weapon units ([`../formats/weapons-dat.md`](../formats/weapons-dat.md#file-level-format)) and `gam\hercs.dat` puts four Outlaws and one part-built Razor in the hangar ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#gamhercsdat--the-starting-hangar)).

### The pilot roster is generated, not authored — `FUN_0040fa31`

Three squads of twelve. For each squad the routine draws two independent shuffles (`FUN_0040f95c`, a Fisher-Yates over 4 and over 3), and pilot `[row][col]` takes:

```
rosterId = shuffle4[col] + squad*4          // 0-11, stored at pilot +0x00
nameIndex = shuffle3[row] + rosterId*3      // 0-35, stored at pilot +0x02
```

The name string itself is copied out of `esnames.bin` at that index. That file holds exactly 36 names — `DUGGAN`, `BRUTUS`, `BUTCHER`, `RIGGS` … `HOYLE` — so the index space is used exactly once over, and every pilot's name is unique within a run. Skill tier comes from `FUN_0040f9dc`, a weighted draw against the table at `0046f5ec`.

## The campaign table — `gam\career.dat`

Loaded once by `LoadCareerDat` (`00412906`), which first opens `missions.bin` as a string table and keeps the handle in `0046fb34`. A flat stage table, verified byte-exact against the retail 148-byte file:

```
int16   stageCount -- 6
per stage:
  int16   campaign index
  int16   missionCount
  int16 x missionCount -- name indices into missions.bin
```

`FUN_00412864` resolves each index through `missions.bin` as it loads, so the file stores indices rather than names. In memory a stage is 8 bytes: the campaign index, the count, and a pointer to the resolved strings. Retail's six stages:

| Stage | Campaign index | Missions | Contents |
|---|---|---|---|
| 0 | 5 | 11 | `TRAIN1`–`TRAIN8`, `DEMO`, `DEMO_01`, `DEMO_02` |
| 1 | 0 | 10 | `C1_01`–`C1_10` |
| 2 | 1 | 10 | `C2_01`–`C2_10` |
| 3 | 2 | 10 | `C3_01`–`C3_10` |
| 4 | 3 | 10 | `C4_01`–`C4_10` |
| 5 | 4 | 10 | `C5_01`–`C5_10` |

Stage 0 is training and stages 1–5 are the five campaign chapters, which is why `FUN_00412a2f` seeds a new career at stage 1 and a new training run at stage 0. It also explains the two bounds in the advance below: `stage > 4` means "already in the final chapter", and the campaign is complete once the stage index reaches the stage count of 6.

The names in `missions.bin` carry their directory — `MSN\C1_01.MSN` — so they are paths ready to open, not bare mission names.

The player's position is the pair `(0046fb18, 0046fb1a)` — stage, then mission within stage — which is the first thing in the save's career block. Retail `GAME_T.SAV` sits at `(0, 4)`, part-way through training.

## Launching a mission — `FUN_0040f0d4`

1. `FUN_0040e9cb` writes the flag array to `data\mission.var`.
2. `_remove` deletes the previous output file.
3. Opens the export and writes `int16 0` and the squad count (`00482a7a`), then one entry per machine via `FUN_004106b7`: the player's first, then every squad member whose on-strength byte (`+0x24`) is set, each preceded by two fields taken from its pilot.

This is `data\player.mec`, and the record it emits is the one `HercWorks.Core.Data.File.Sav.MecFile` already models from the other end — decoded from DBSIM's reader without reference to this writer. The two derivations agree field for field:

| `MecEntry` | Written from |
|---|---|
| *(file header)* `PlayerEntryIndex` | literal `0` — the player is always entry 0 |
| *(file header)* entry count | `00482a7a` |
| `Unk00` | the pilot's `esnames.bin` name index (pilot `+0x02`) |
| `Unk02` | the pilot's skill tier (pilot `+0x25`) |
| `MechType` | HERC record `+0x00` |
| `SlotCount` | HERC record `+0x4c`, the mount capacity |
| `WeaponRefs[]` | per slot, the mounted unit's id — **`0` when the hardpoint is empty** |
| `WeaponAmmoTypes[]` | per slot, the unit's `+0x08` — **`5` when the hardpoint is empty** |
| `Unk3A` | literal `0` |
| `BlockA`/`BlockB`/`BlockC` (26+20+20) | one contiguous 66-byte span, HERC record `+0x08`–`+0x49`, the same bytes the save stores for that machine |

The player's own entry reads its two leading fields from `00482a7e` and `00482aa1`, which are the same two pilot fields reached directly: the player structure at `00482a78` embeds its pilot record at `+0x04`, putting the name index at `00482a7e` and the skill tier at `00482aa1`.

The `5` filler that [`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md) observes in every non-launcher slot is written here — it is this function's literal default for a hardpoint with no unit mounted. Where a unit *is* mounted the field is that unit's own ammo type, which the armory sets to `1` for a missile rack and `5` for everything else ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#the-weapon-unit-record)), so `5` reaches the file by two routes and means the same thing on both.

### The trailing weapon table

After the last entry the export writes one more block, `FUN_00412253`:

```
int16   33 (0x21), written as a literal
33 x byte   weapons.dat record +0x16, one per catalog id, walked at the 29-byte stride
```

35 bytes, and they close the gap in the retail sample: `MecFile`'s note that a 263-byte `player.mec` has only 228 bytes of entries leaves exactly 35 unaccounted for. They are this table, not slack — and an export cannot carry a stale tail anyway, since the export and copy streams open with `_open(path, 0x8301, 0x180)`, `O_BINARY|O_CREAT|O_TRUNC|O_WRONLY`. Only the save-slot stream skips `O_TRUNC` ([`../formats/save-games.md`](../formats/save-games.md)).

The byte is the weapon's unlock flag, the same one every save slot stores for all 33 catalog ids ([`../formats/weapons-dat.md`](../formats/weapons-dat.md)).

**Nothing traced reads this table back.** VSHELL reopens `data\player.mec` in exactly one place — `FUN_00424db0`, the map screen — and reads only the leading two `int16`, the player entry index and the squad size, before closing it and moving on to `data\mforms.dat` for formation layout. It never reaches the entries, let alone the table. On the simulator side, `MecFile`'s note records DBSIM's reader stopping at the last entry. The save file, not the export, is where the flags are authoritative: the export is regenerated from it at every launch, so the copy here is duplicated state.

Both path strings are referenced as bare addresses (`0046f511`, `0046f521`), so the decompile does not show their text; read out of the binary they are two separate copies of the same literal, `data\player.mec`. The function removes that file and immediately recreates it.

The whole layout is verified byte-exact against every retail `player.mec` — the loose `DATA` copy and all nine `sav\player%d.mec` — 143 to 467 bytes, 1 to 4 entries each, every one consuming to the last byte with the 33-flag table present.

## The debrief — `FUN_0040eae7`

The longest function in `game.cpp` and the whole of the post-mission accounting.

1. Read `data\mission.var` back into the flag array.
2. Open `data\results.dat` and read: `int16` outcome to `00482ae9`, an `int32` added to the salvage pool, then `int16 count` and that many `{ int16, int16 }` salvage pairs, each applied by `FUN_0041229d` in campaign mode only.
3. Copy the outcome into flag slot 0.
4. For the player, then for each on-strength squad member in order: read the HERC's 66-byte status block over its `+0x08` span (`FUN_00411720`, which also destroys any mount whose condition arrived at 0), set the pilot's condition from that machine's overall condition (`FUN_00411d06(block, 1, 9)`), read the pilot's three mission counters and accumulate them (`FUN_0041000e`), and settle the machine (`FUN_00410c7c`) — a HERC below 30 condition is scrapped out of the hangar for its salvage value, anything above is reset to 100.
5. Deduct repairs — `FUN_0040e804`, charged per surviving pilot.
6. Advance the campaign, then set the next screen from the result.

Written out, the file is:

```
int16   outcome
int32   salvage awarded to the pool
int16   salvageCount
        salvageCount x { int16, int16 }
per machine, the player's first then each on-strength squad member in order:
  66 B    the HERC status block, read straight over the record's +0x08 span
  int16   counter A   -> pilot +0x2d
  int16   counter C   -> pilot +0x31
  int16   counter B   -> pilot +0x2f
```

DBSIM writes this file, so there is no writer in VSHELL to mirror it against, but the retail `data\results.dat` walks exactly: 152 bytes = the 8-byte header, no salvage, and two 72-byte machine blocks — matching the two entries in the `player.mec` beside it.

### Pilot progression

After the results stream is closed the debrief calls `FUN_004103ba(00482a78)`, the squad-wide progression pass. It runs `FUN_00410066` on the player when the player's HERC condition is non-zero, then on every on-strength squad member whose condition is non-zero; a squad member at zero condition is replaced by a fresh pilot from `FUN_0040fb4f` and the squad count drops.

`FUN_00410066(pilot, isPlayer)` advances two independent ladders, both capped at 3 and both keyed on the pilot's counters ([`../formats/save-games.md`](../formats/save-games.md#pilot-record--59-bytes-0x3b-in-memory)):

```
divisors = (pilot +0x27 == 0) ? { kills: 20, missions: 10 }
                             : { kills: 10, missions: 15 };
if (pilot +0x25 < 3 && !isPlayer && (pilot +0x33 + pilot +0x35) % divisors.kills == 0)
    pilot +0x25 += 1;                 // skill
if (pilot +0x29 < 3 && pilot +0x39 % divisors.missions == 0)
    pilot +0x29 += 1;                 // rank
```

Two consequences worth holding onto:

- **The player's skill never changes.** The player is the one call site passing `isPlayer = 1`, and the `jnz` at `0041009f` jumps clear of the skill block on that argument — so the level chosen on the registration screen stands for the whole career, while the player's rank still advances on missions flown.
- **Skill counts machines only.** The test sums Herc and Flyer kills and ignores Base kills entirely.

The counters have already been accumulated and `+0x39` already incremented by the `FUN_0041000e` loop above, so a pilot's first debrief tests `missionsFlown == 1`. The kill test has no such offset: a squad pilot whose Herc and Flyer totals are still `0` satisfies `0 % divisor == 0` and takes a skill step on every mission survived until the cap.

That last point is confirmed in the instruction stream — the sum is never tested, only the remainder:

```
004100a1  0f bf 41 33       movsx eax, word [ecx+0x33]   ; career Herc kills
004100a5  0f bf 51 35       movsx edx, word [ecx+0x35]   ; career Flyer kills
004100a9  03 c2             add   eax, edx               ; the sum -- never examined again
004100b0  99                cdq
004100b1  f7 fb             idiv  ebx
004100b3  85 d2             test  edx, edx               ; the remainder, and nothing else
004100b5  75 04             jnz   004100bb
004100b7  66 ff 41 25       inc   word [ecx+0x25]        ; skill++
```

Seven jumps in the function resolve to four targets — `00410088`, `00410090`, `004100bb`, `004100d9` — and the instruction-length chain from the entry point hits all four, ending at the `ret` at `004100dc`, so the decode is bounded on every branch.

### Where the debrief goes next

`FUN_00412dc7` advances `(stage, mission)` and returns the branch, which lands in `0048260e`:

| Condition | State | Effect |
|---|---|---|
| player's HERC condition reached 0 | 3 | campaign over |
| advance returned 0 — mission failed at a stage boundary, or past stage 4 | 0 | back to the shell |
| advance returned 1 — succeeded past the last stage | 1 | autosave to slot 10, then play AVIs `0x53` and `0x54` |
| otherwise | 2 | continue to the next mission's briefing |

**The position advances whether or not the mission was won.** `FUN_00412dc7` increments the mission index before it inspects the outcome; the outcome only chooses the branch. Rolling past a stage's mission count resets the mission index to 0 and increments the stage.

Two scripted events are hard-coded into the advance, keyed on the position *after* it increments: stage 1 mission 3 calls `FUN_0040e6c8(4)`, and stage 1 mission 6 calls `FUN_0040e7cd(8)` — both squad-roster operations.

Every path that leaves a campaign in a resumable state autosaves through `FUN_0040e37b(10, NULL)`, which is why `GAME_R.SAV` mirrors the newest ordinary save.
