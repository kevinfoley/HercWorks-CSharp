# `.TAP` input tapes

DBSIM can record a whole mission's input to a file and play it back frame for frame. A tape carries both halves of what that needs: the starting state, as a bundle of the seven files the mission runs from, and then one record per frame of everything the player did. Three finished tapes ship in `ES2/TAPES/`.

Nothing in the shipped shell can start one — see [Reaching it](#reaching-it). The format itself is settled: a parser written from the code below consumes all three retail tapes to their final byte.

## The switches

`Sim_ParseCommandLine` (`0045e73c`) is DBSIM's own argument parser. Three of its cases drive the tape:

| Switch | Effect |
|---|---|
| `-r<name>` | Record to `<name>.tap`. Writes the bundle, closes the file, sets the recording flag `004d255c` |
| `-p<name>` | Play `<name>.tap`. Unpacks the bundle over the live data files, sets the playback flag `004d255a` |
| `-D` | Play a tape chosen at random by `DemoTape_PickRandom`, and additionally set `004d25b4` — demo mode |

The extension is forced to `tap` in all three cases, so `<name>` is a bare stem: a tape in `tapes\` is named as `tapes\demo1`.

`DemoTape_PickRandom` (`0045ce9c`) loads group `0x14` of `tapes\demolist.str` and returns entry `time() % count`, or entry 0 when the table holds one name. The retail table holds `DEMO1`, `DEMO2`, `DEMO3`.

Demo mode is playback plus an abort: with `004d25b4` set, the first command code off the tape's own stream **or** the first axis key the player touches (`Input_KeyjoyAxisKey`'s opening test) raises `004d25b6`, which `StatusAlertPanel_RunModal` reads to leave the mission. That is attract-mode behaviour — run until somebody touches something.

## File layout

### The bundle

Seven length-prefixed blocks, written once when `-r` opens the tape and unpacked by `-p` before the mission starts. `Tape_PackFile` (`0045cc88`) writes a `uint32` size then the bytes in 500-byte chunks; a source file it cannot open writes size 0, which is why `object.str` is empty in every retail tape. `Tape_UnpackFile` (`0045cd40`) is the inverse and tolerates a null destination.

| # | File | `DEMO1` | `DEMO2` | `DEMO3` |
|---|---|---|---|---|
| 0 | `data\script.dat` | 13520 | 13520 | 13520 |
| 1 | `data\player.mec` | 151 | 495 | 143 |
| 2 | `data\mission.var` | 2000 | 2000 | 2000 |
| 3 | `data\prefs.cfg` | 54 | 54 | 54 |
| 4 | `data\restore.dat` | 42 | 42 | 42 |
| 5 | `data\object.str` | 0 | 0 | 0 |
| 6 | `data\keyjoy.cfg` | 518 | 518 | 518 |

So a tape restores the mission, the player's machine, the mission variables, the preferences, the restore point and the key bindings. Everything a replay needs in order to be deterministic is in the file; nothing is taken from the installation.

The three retail tapes are three different missions — `script.dat` byte 0, the theater, reads 1, 2 and 4 — and all three were recorded at pilot skill 3, the value at `script.dat +0x0e` ([`../simulation/difficulty.md`](../simulation/difficulty.md)).

`-p` also reconciles preferences against the tape's own copy: it reads `data\prefs.cfg` and `tapes\prefs.cfg`, copies bytes 0-3 and 8-10 from the former over the latter, and writes `tapes\prefs.cfg` back.

### The stream

Immediately after the bundle, once, comes the eight-byte joystick capability block `Input_QueryCapabilities` returns ([`../simulation/preferences.md`](../simulation/preferences.md)) — so a replay knows what device the recording was made on. `DEMO1` states one stick, four buttons and no throttle lever.

Then one 24-byte header per frame, plus its variable tail:

| Offset | Type | Contents |
|---|---|---|
| `+0x00` | int16 | The frame's command word — the head of the player input block at `PlayerInputBlock` (`004d234a`) |
| `+0x02` | int16 x4 | The four axis values: `004d2358`, `004d235a`, `004d235c`, `004d235e` |
| `+0x0a` | int16 | `SimTickDelta` |
| `+0x0c` | 4 bytes | Button and mode bits, one per bit: `004d2360`-`004d236b` in the first byte, a second bank in the next, and the missile-control gate `004d2357` at bit 0 of the third |
| `+0x10` | uint32 | Mouse-event count |
| `+0x14` | uint32 | Command count |
| `+0x18` | 14 x n | The mouse events, in `CockpitMouseQueue_Push`'s own record layout ([`cockpit-input.md`](cockpit-input.md#3-the-cockpits-one-listener-queues-it-doesnt-act)) |
| — | int16 x n | The command codes, in the layout of the queue at `004d2148` ([`cockpit-input.md`](cockpit-input.md#how-a-keystroke-becomes-one-of-those-codes)) |

The three retail tapes run 1499, 1901 and 3660 frames. The only command codes appearing anywhere across all three are `0x0c`, `0x0d` and `0x1b` — `-`, `=` and `]`, three of the seven scancodes on `SimCommandWantedCodes`, which is the whole of what that queue ever carries.

## Where it runs

Both directions live in `Input_BuildPlayerDevice` (`0045a7f4`), the per-frame input build, around the point where the live device would otherwise be read.

**Recording** reopens the tape in append mode every frame, writes the capability block if it has not yet, writes the header and the two arrays, and closes the file again. A failed open reports through the error logger as `APPINPUT.CPP:834` rather than stopping the mission. The mouse events it writes are the front buffer `CockpitMouse_ProcessQueue` returns — which is the only reason that function returns anything.

**Playback** reads the capability block once, then the header, then pushes the frame's mouse events into the cockpit queue through `CockpitMouseQueue_Push` and its command codes into `004d2148`, and unpacks the button bits back into the same globals the live path would have written. The rest of the frame is then ordinary: the command queue drains through `Sim_DispatchCommand`, the mouse queue through `CockpitMouse_ProcessQueue`.

**Live mouse input is shut off while a tape plays.** `CockpitMouse_OnEvent` queues nothing unless `004d1e5a` is set, and that byte is clear for the duration, so the tape's recorded events are the only ones the cockpit sees. The end of the tape sets it.

Playback ends on a short read — any of the three header reads returning 0 — or on command `0x412`, which is `[Ctrl]+[E]`: a stop key that can be recorded into the tape itself. Either way the file is closed, `004d255a` clears, live mouse input is restored and the command word is zeroed; under `-D` the abort flag is raised as well.

Playback in retail appears to have no framerate limit, so the three demos that ship with the game will play back much too quickly on a modern computer.

## Reaching it

Nothing in the retail install passes any of the three switches. `ES.EXE` launches only `vshell -eggplant`, and VSHELL builds DBSIM's argument list from a fixed set — `dummy`, `-eggplant`, `-Z`, `-s`, `-v3`, `-h`, `-F`, `-G`, `-m`, `-D` — appending `-D` when the word at `00482282` is non-zero. The only write to that word anywhere in VSHELL is the `= 0` in its options-block initialiser; the block is only ever touched field by field at absolute addresses, so no bulk load can reach it either, and a field scan for the offset finds no rebased access.

That is a null result and stays one, but on the available evidence the demo mode cannot be started from the shipped shell, and the three tapes are content nobody could see — [`../cut-content.md`](../cut-content.md#miscellaneous-features).

The tapes are readable and the switches are live, so `dbsim -ptapes\demo1` replays a retail session.

## Symbol reference

| Symbol | Address | Role |
|---|---|---|
| `Sim_ParseCommandLine` | `0045e73c` | DBSIM's argument parser; owns `-r`, `-p` and `-D` |
| `DemoTape_PickRandom` | `0045ce9c` | Picks a tape name from `tapes\demolist.str` |
| `Tape_PackFile` / `Tape_UnpackFile` | `0045cc88` / `0045cd40` | One bundle block out and in |
| `Input_BuildPlayerDevice` | `0045a7f4` | Per-frame input build; both the record and the playback point |
| `PlayerInputBlock` | `004d234a` | The input block a frame record is a snapshot of |
| `TapeRecording` / `TapePlayback` | `004d255c` / `004d255a` | The two mode flags |
| `TapeFile` | `004d2566` | The open tape's `FILE*` |
| `TapeStem` | `004d255e` | The name the `tap` extension is forced onto |
| `DemoMode` / `DemoAbort` | `004d25b4` / `004d25b6` | `-D`'s extra flag, and the abort it raises |
| `CockpitMouseLive` | `004d1e5a` | Gates `CockpitMouse_OnEvent`; clear while a tape plays |

## Open

- Whether the retail tapes' missions are the `DEMO`, `DEMO_01` and `DEMO_02` entries in the campaign's own mission table ([`../shell/campaign-loop.md`](../shell/campaign-loop.md)), or the `DEMO*.MSN` files. A tape carries `script.dat`, which is a save formatted from a `.MSN` rather than the mission file itself, so the two were not matched up.
- The second bank of button bits at `+0x0d` of a frame header: playback reads it back into locals rather than into named globals, so which control each bit carries was not traced.
