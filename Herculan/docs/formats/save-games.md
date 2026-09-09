# Shell save files — `sav\GAMEFILE.STR` and `sav\GAME_?.SAV`

The campaign save. Written and read only by VSHELL; DBSIM never opens either file. **Every address in this doc is in `VSHELL.EXE`.**

Reversed from decompilation and validated by walking all nine retail `ES2/SAV/GAME_*.SAV` end to end: seven consume byte-exact, and the two that do not are explained by the non-truncating writer below.

## The twelve slots — `sav\GAMEFILE.STR`

A slot directory, not a save: twelve filenames and twelve display labels. Read by `FUN_0040ddc8`, written by `FUN_0040df4b`.

```
0x00   int32   physical file length - 4 (see "Streams never truncate")
0x04   int16   count -- always written as 12
       12x { int16 len; char[len] filename, NUL included; byte 0 }
       int16   count -- always written as 12
       12x { int16 len; char[len] label, NUL included; byte inUse }
```

The reader ignores the leading length and stops after the second count block. Both counts are literal `0xc` in the writer, so the slot count is fixed in code, not data.

| Slot | Filename | Label | Role |
|---|---|---|---|
| 0-9 | `GAME_0.SAV`-`GAME_9.SAV` | `" 1. KEVIN"`, `" 8. EMPTY"` | player-named saves; the label carries its own `"N. "` prefix |
| 10 | `GAME_R.SAV` | `RESUME` | campaign autosave |
| 11 | `GAME_T.SAV` | `TRAINING` | training autosave |

Slot 10 and 11 are one slot from the caller's side. `FUN_0040e37b` and `FUN_0040e4f2` both open with `if (slot == 10 && DAT_0048260c == 0) slot = 11`, so `DAT_0048260c` is the campaign/training mode flag — 1 selects `GAME_R`, 0 selects `GAME_T`. Retail `GAME_T.SAV` holds pilot `TRAINEE` and career stage 0; `GAME_R.SAV` is byte-identical in length, credits and career position to `GAME_6.SAV`, the newest ordinary save.

**An empty slot's label is completed at load time.** After reading a label the reader tests `label[4]`, and when it is NUL appends string `0x21` from `estext.bin`. A stored `" 8. "` becomes `" 8. EMPTY"` in a localized build. Once such a slot is written back the completed label is in the file, which is why every retail label already reads `EMPTY`.

The in-memory slot table is a 94-byte (`0x5e`) stride at `00482610`: filename at `+0x00` (13 bytes), label at `+0x0d` (80 bytes), in-use byte at `+0x5d`. `FUN_0040e150` builds the path to open by prefixing the filename with the string at `0046f434`.

## Streams never truncate

`FUN_0044e46c`, the write-stream open behind every file in this doc, calls `_open(path, 0x8102, 0x180)` — `O_BINARY | O_CREAT | O_RDWR`, with **no `O_TRUNC`**. Writing a shorter payload over a longer file leaves the old tail in place.

This is observable in retail data. `GAME_4.SAV` carries 164 bytes past its last field and `GAME_T.SAV` 36; `GAMEFILE.STR` carries 11, the remains of a longer label block. Consequences for any reader:

- **Parse by structure, never by file size.** Trailing bytes are not a parse failure.
- `GAMEFILE.STR`'s leading length is `lseek(fd, 0, SEEK_END) - 4` taken *after* writing (`FUN_0044e518`), so it measures the physical file including stale tail, not the payload.
- Where the game needs a clean file it deletes first: `FUN_0040f0d4` calls `_remove` on its output path before opening it for write.

The exports and copies use a different stream class whose open is `_open(path, 0x8301, 0x180)` — `O_BINARY | O_CREAT | O_TRUNC | O_WRONLY` — so they do truncate. Stale tails are a property of the save files here, not of everything the shell writes.

## `sav\GAME_?.SAV` — block order

No header, no magic, no length field: the file is the concatenation below. Written by `FUN_0040e37b` and read back field-for-field by `FUN_0040e4f2`.

| # | Bytes | Content | Writer / reader |
|---|---|---|---|
| 1 | varies | armory stock — 33 records, one per `weapons.dat` catalog id | `FUN_004121cf` / `FUN_0041215d` |
| 2 | 22 | `int16` at `0046f8d4`, then 5x `{ int16 index; int16 value }` from `0046f8d6` | same |
| 3 | 152 | career block (below) | `FUN_00412a71` / `FUN_00412bbf` |
| 4 | varies | 3 squads x 12 pilot records, then 3x `int16` at `00483b48` and 3x `int16` at `00483b4e` | `FUN_0040fc16` / `FUN_0040fc77` |
| 5 | varies | the player: `int16`, `int16`, then one pilot record | `FUN_0041016d` / `FUN_004101b8` |
| 6 | varies | hangar: `int16` count, then that many `{ int16 slot; HERC record }` | `FUN_00410658` / `FUN_0041080a` |
| 7 | 18 | 9x `int16`, the first field of each 8-byte record at `00483b62` | `FUN_00411954` / `FUN_00411989` |
| 8 | 4 | credits (`00482af4`) | inline |
| 9 | 2000 | the campaign flag array (`00482af8`) | inline |
| 10 | 2 | game state (`0048260e`) | inline |
| 11 | 20 | `004832c8` | inline |

Blocks 8, 9 and 11 are one contiguous span in memory: credits at `00482af4`, the flag array immediately after at `00482af8`, and its 2000 bytes ending exactly at `004832c8`. Block 10 comes from `0048260e`, elsewhere entirely.

The hangar holds up to 8 HERCs. Both the hangar and each HERC's mounts serialize **sparsely** — occupied entries only, each preceded by its slot index — so a reader must use the count field and cannot assume dense packing.

### Armory stock record

One per catalog id, all 33 written unconditionally:

```
byte    weapons.dat record +0x16
int16   number of owned units (weapons.dat record +0x17)
        that many 10-byte stock entries: 5x int16
```

The byte is the weapon's unlock flag and the owned units are a linked list at the catalog record's `+0x19` at runtime. See [`weapons-dat.md`](weapons-dat.md) for the catalog record those fields belong to.

### Pilot record — 59 bytes (`0x3b`) in memory

Serialized by `FUN_0040fd5f`, read by `FUN_0040fefc`, initialized by `FUN_0040fcd8` and `FUN_0040fd17`.

| Offset | On disk | Field |
|---|---|---|
| `+0x00` | `int16` | roster id, `0-11` — squad index x 4 plus a per-squad shuffle |
| `+0x02` | `int16` | name index into `esnames.bin`, `0-35` |
| `+0x04` | `int16 len` + `char[len]` | name, copied from `esnames.bin`; 30 bytes in memory |
| `+0x22` | `int16` | assigned hangar slot; `-1` when unassigned |
| `+0x24` | `byte` | on strength — gates repair billing, results accounting and the `player.mec` export |
| `+0x25` | `int16` | skill tier `0-3`, drawn against the weight table at `0046f5ec` |
| `+0x27` | `int16` | squad slot; initialized `-1` |
| `+0x29` | `int16` | rating, looked up from the tier via `0046f5f4` |
| `+0x2b` | `int16` | condition, initialized 100 and overwritten at debrief from the HERC's damage |
| `+0x2d` | `int16` | last mission, counter A |
| `+0x2f` | `int16` | last mission, counter B |
| `+0x31` | `int16` | last mission, counter C |
| `+0x33` | `int16` | career total A |
| `+0x35` | `int16` | career total B |
| `+0x37` | `int16` | career total C |
| `+0x39` | `int16` | missions flown |

The three pairs are proved by `FUN_0041000e`, which reads the per-mission counters from `results.dat` in the order A, C, B and then accumulates `+0x33 += +0x2d`, `+0x35 += +0x2f`, `+0x37 += +0x31`, `+0x39 += 1`.

### HERC record — 122 bytes (`0x7a`) in memory

Serialized by `FUN_0041123e`, read by `FUN_004110c2`.

```
int16   +0x00
int16   +0x02
66 B    +0x08 -- the status block, below
int16   +0x4a
int16   +0x78
int16   +0x4c  mount capacity
int16   +0x4e  mounts occupied
        that many { int16 slot; 10-byte stock entry }
```

**`+0x4c` and `+0x4e` are not interchangeable.** The writer loops to the capacity at `+0x4c` and emits only non-null mounts; the reader loops exactly `+0x4e` times. Reading the writer alone yields a record that desynchronizes whenever a HERC has an empty hardpoint.

#### The 66-byte status block

`FUN_00411d06(block, mode, index)` is the accessor and `FUN_00411cbd` the matching setter: mode 1 addresses `block + 0x1a + index*2`, mode 2 addresses `block + 0x2e + index*2`. That splits the block three ways, and the split is exactly `player.mec`'s `BlockA`/`BlockB`/`BlockC`, which were derived independently from the offsets DBSIM copies them to.

| Span | Size | Content |
|---|---|---|
| `+0x00`–`+0x19` | 26 | not decoded |
| `+0x1a`–`+0x2d` | 20 | 10x `int16` condition. Index 9 is the machine's overall condition — read as `(block, 1, 9)` at debrief to set the pilot's, and reset to 100 by `FUN_00411cbd(block, 1, 9, 100)` when a HERC is kept rather than scrapped |
| `+0x2e`–`+0x41` | 20 | 10x `int16` per-hardpoint condition, one per mount slot. A mount whose entry reaches 0 is destroyed and `+0x4e` decremented (`FUN_00411720`) |

Retail data agrees: both arrays hold 0–100, the per-hardpoint array carries most of the partial figures, and a machine at full health reads 100 throughout.

Because the span is copied verbatim into `player.mec`, a machine's status bytes are identical in the save and in the export ([`../shell/campaign-loop.md`](../shell/campaign-loop.md)).

### Career block — 152 bytes

| Bytes | Global | Field |
|---|---|---|
| 2 | `0046fb18` | campaign stage |
| 2 | `0046fb1a` | mission within the stage |
| 2 | `00483fe8` | populated count of the array below |
| 20 | `00483fea` | 10x `int16`, line indices into `data\mission.str`; `-1` is an empty slot |
| 2 | `00483ffe` | populated count |
| 60 | `00484000` | 30x `int16` line indices |
| 2 | `0048403c` | populated count |
| 60 | `0048403e` | 30x `int16` line indices |
| 2 | `004840b8` | — |

The three index arrays are briefing and debrief prose, assembled into single strings by `FUN_00412f97` by concatenating the `mission.str` lines each array names. A fourth 30-entry array at `0048407c` holds the *next* mission's text and is deliberately absent from the save — `FUN_004133d2` rebuilds it whenever the campaign advances.

## The slot handoff

Both career-block functions also copy the three loose working files that the shell and the simulator exchange, and the copy direction is what separates them:

| Function | Direction | Files |
|---|---|---|
| `FUN_00412a71` | save — `data\` to `sav\` | `data\script.dat` to `sav\script%d.dat`, `data\mission.str` to `sav\missn%d.str`, `data\player.mec` to `sav\player%d.mec` |
| `FUN_00412bbf` | load — `sav\` to `data\` | the same three, reversed |

`FUN_0040d4d5` (`fileutil.cpp`) is the copy itself. On load, `FUN_00412bbf` additionally calls `FUN_00412f97` to rebuild the assembled briefing text from the restored `data\mission.str`, so the three text arrays in the career block are indices that only mean anything alongside the slot's own `missn%d.str`.

For what those files carry see [`script-dat.md`](script-dat.md) and [`msn-mission-file.md`](msn-mission-file.md); for how a mission's results re-enter the save see [`../shell/campaign-loop.md`](../shell/campaign-loop.md).
