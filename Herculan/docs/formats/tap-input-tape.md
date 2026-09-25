# `.TAP` input tapes

DBSIM can record a whole mission's input to a file and play it back frame for frame. A tape carries both halves of what that needs: the starting state, as a bundle of the seven files the mission runs from, and then one record per frame of everything the player did. Three finished tapes ship in `ES2/TAPES/`.

The main menu's `VIEW DEMO` plays one — see [Reaching it](#reaching-it). The format itself is settled: a parser written from the code below consumes all three retail tapes to their final byte.

## The switches

`Sim_ParseCommandLine` (`0045e73c`) is DBSIM's own argument parser. Four of its cases drive the tape:

| Switch | Effect |
|---|---|
| `-r<name>` | Record to `<name>.tap`. Writes the bundle, closes the file, sets the recording flag `004d255c` |
| `-p<name>` | Play `<name>.tap`. Unpacks the bundle over the live data files, sets the playback flag `004d255a` |
| `-D` | Play a tape chosen at random by `DemoTape_PickRandom`, and additionally set `004d25b4` — demo mode |
| `-d` | Open the checkpoint file `<name>.dmp` beside the tape — see [The checkpoint file](#the-checkpoint-file) |

The extension is forced to `tap` in the first three cases, so `<name>` is a bare stem: a tape in `tapes\` is named as `tapes\demo1`. `-d` takes no name of its own: it reads the stem and the recording flag the others left, so it only works after `-r` or `-p` on the line.

`DemoTape_PickRandom` (`0045ce9c`) loads group `0x14` of `tapes\demolist.str` and returns entry `time() % count`, or entry 0 when the table holds one name. The retail table holds `DEMO1`, `DEMO2`, `DEMO3`.

Demo mode is playback plus an abort: with `004d25b4` set, any key the player presses that makes a command code, the first axis key (`Input_KeyjoyAxisKey`'s opening test), closing the window (`WM_CLOSE` in the window procedure) or a `WM_QUIT` raises `004d25b6`, which the mission loop and `StatusAlertPanel_RunModal` read to leave the mission. That is attract-mode behaviour — run until somebody touches something. The command-code test reads the live keyboard's command word, which `FUN_0045ba8c` builds at `004d247a` each frame, not the tape's — see [Rejected readings](#rejected-readings).

## File layout

### The bundle

Seven length-prefixed blocks, written once when `-r` opens the tape and unpacked by `-p` before the mission starts. `Tape_PackFile` (`0045cc88`) writes a `uint32` size then the bytes in 500-byte chunks; a source file it cannot open writes size 0, which is why `object.str` is empty in every retail tape. `Tape_UnpackFile` (`0045cd40`) is the inverse and tolerates a null destination.

| # | Recorded from | Unpacked to | `DEMO1` | `DEMO2` | `DEMO3` |
|---|---|---|---|---|---|
| 0 | `data\script.dat` | the same | 13520 | 13520 | 13520 |
| 1 | `data\player.mec` | the same | 151 | 495 | 143 |
| 2 | `data\mission.var` | the same | 2000 | 2000 | 2000 |
| 3 | `data\prefs.cfg` | `tapes\prefs.cfg` | 54 | 54 | 54 |
| 4 | `data\restore.dat` | the same | 42 | 42 | 42 |
| 5 | `data\object.str` | the same | 0 | 0 | 0 |
| 6 | `data\keyjoy.cfg` | `tapes\keyjoy.cfg` | 518 | 518 | 518 |

The two configuration files change folder because `FUN_0045eea4`, which builds both of their paths, prefixes `data\` normally and `tapes\` once `004d255a` is set. The other five go over the live `data\` copies.

The three retail tapes are three different missions — `script.dat` byte 0, the theater, reads 1, 2 and 4 — and all three were recorded at pilot skill 3, the value at `script.dat +0x0e` ([`../simulation/difficulty.md`](../simulation/difficulty.md)).

**A replay is not fully determined by the tape.** Three things come from the playing machine's install:

- **Six preference bytes.** `-p` reads `data\prefs.cfg` and `tapes\prefs.cfg`, copies bytes 0-3 and 8-10 of the former over the latter — sound, music, the two message modes, terrain texture and the two detail settings ([`../simulation/preferences.md`](../simulation/preferences.md#what-each-byte-is)) — and writes `tapes\prefs.cfg` back. It also stores that path in the preferences path at `0049e844`, which the loader otherwise fills with `data\prefs.cfg`, so the simulator runs from the reconciled copy.
- **The keyjoy switches.** `Keyjoy_LoadConfig` (`0045b78c`) always reads `data\keyjoy.cfg`, so the tape's own copy in `tapes\` is never read. Its `Backturn` applies to the replayed axes: it is applied after the point where a frame is recorded, and playback runs that code too.
- **Everything the bundle does not carry**, which is whatever `data\` already holds — the mission's text in `mission.str` among it.

When a playback mission ends with `004d255a` still set — `-D` aborted, or the mission left before the tape ran out — the teardown in `FUN_0045f144` deletes `tapes\prefs.cfg` and `tapes\keyjoy.cfg` (`OpenFile` with `OF_DELETE`). A tape played to its end leaves them.

### The stream

Immediately after the bundle, once, comes the eight-byte joystick capability block `Input_QueryCapabilities` returns ([`joystick-input.md`](joystick-input.md#the-capability-block--input_querycapabilities-004777f8)) — so a replay knows what device the recording was made on. All three retail tapes carry `01 00 04 00 00 01 10 00`: four buttons, a rudder, a hat and no throttle. Playback reads the block into `Input_QueryCapabilities`' own buffer, which that function rebuilds from the live device on its next call.

Then one 24-byte header per frame, plus its variable tail:

| Offset | Type | Contents |
|---|---|---|
| `+0x00` | int16 | The frame's command word — the head of the player input block at `PlayerInputBlock` (`004d234a`) |
| `+0x02` | int16 x4 | The four axis values: `004d2358`, `004d235a`, `004d235c`, `004d235e` |
| `+0x0a` | int16 | `SimTickDelta`. Playback puts it in the input block's `+0x2e` (`004d2378`), where `Sim_MainTick` picks it up |
| `+0x0c` | byte | Bits 0-3 buttons 1-4 (`004d2360`-`004d2363`); bits 4-7 the four hat bytes (`004d2368`-`004d236b`), north, south, west, east |
| `+0x0d` | byte | The stick's own eight buttons, before the bindings: the live device block's `+0x1d`..`+0x24` (`004d2497`-`004d249e`) |
| `+0x0e` | byte | Bit 0 the trigger, `004d2357` ([`../simulation/weapon-firing.md`](../simulation/weapon-firing.md)) |
| `+0x0f` | byte | Zero: the writer builds the four bytes as one `uint32` with nothing above bit 16 |
| `+0x10` | uint32 | Mouse-event count |
| `+0x14` | uint32 | Command count |
| `+0x18` | 14 x n | The mouse events, in `CockpitMouseQueue_Push`'s own record layout ([`cockpit-input.md`](cockpit-input.md#3-the-cockpits-one-listener-queues-it-doesnt-act)) |
| — | int16 x n | The command codes, in the layout of the queue at `004d2148` ([`cockpit-input.md`](cockpit-input.md#how-a-keystroke-becomes-one-of-those-codes)) |

**Buttons 5-8 are not recorded.** Only `004d2360`-`004d2363` reach the header, and playback's `memset` of the input block leaves the other four zero. The four bits are written after the press-once latch has masked the build ([`joystick-input.md`](joystick-input.md#the-buttons)), so each is set on the one frame its action fires, and the trigger's own button is zero because the trigger is extracted from it. The hat bytes are zero under HAT = 2, which writes the hat onto the turret axes and clears them before they are recorded.

The raw bank at `+0x0d` is read back into locals that the playback path never uses. In the retail tapes its bit 0, the stick's trigger button, is set on exactly as many frames as the trigger bit — 180, 200 and 204.

The command word is the one key event the build took off the keyboard ring, releases included: a press and its `0x80` release sit in two different frames, and a held key's auto-repeat arrives as a run of presses with one release at the end. The command queue carries presses of its seven keys only, their releases reaching the ring and so the command word. The three retail tapes run 1499, 1901 and 3660 frames. The only codes in their queues are `0x0c`, `0x0d` and `0x1b` — `-`, `=` and `]`, three of the seven scancodes on `SimCommandWantedCodes`.

## Where it runs

Both directions live in `Input_BuildPlayerDevice` (`0045a7f4`), the per-frame input build, around the point where the live device would otherwise be read.

**Recording** reopens the tape in append mode every frame, writes the capability block if it has not yet, writes the header and the two arrays, and closes the file again. A failed open reports through the error logger as `APPINPUT.CPP:834` rather than stopping the mission. The mouse events it writes are the front buffer `CockpitMouse_ProcessQueue` returns — which is the only reason that function returns anything.

**Playback** reads the capability block once, then the header, then pushes the frame's mouse events into the cockpit queue through `CockpitMouseQueue_Push` and its command codes into `004d2148`, and unpacks the button bits back into the same globals the live path would have written. The rest of the frame is then ordinary: the command queue drains through `Sim_DispatchCommand`, the mouse queue through `CockpitMouse_ProcessQueue`.

**Live mouse input is shut off while a tape plays.** `CockpitMouse_OnEvent` queues nothing unless `004d1e5a` is set, and that byte is clear for the duration, so the tape's recorded events are the only ones the cockpit sees. The end of the tape sets it. A mouse event's position is in the game's screen space: `Mouse_DispatchEvent` (`0048083c`) scales a client position by `(backBufferWidth << 15) / clientWidth` through a Q16 multiply, which is half the back buffer and so the viewport — 640x480, or 320x240 in the low-resolution mode.

**A modal panel reads the tape too.** `Sim_MainTick` is not the only caller of `Input_BuildPlayerDevice`: the loops of all four alert panels call it once per pass — `StatusAlertPanel_RunModal` (`00455fe4`), `ObjectivesPanel_RunModal` (`00457ae4`), `PreferencesPanel_Run` (`00456d4c`) and `ControlsPanel_Run` (`00458650`) — and so does `AlertPanel_SetFocus` (`00454c7c`) when it adopts the widget under the pointer. So a recording goes on writing frames while a panel is up, and a playback goes on reading them: the keystrokes and clicks that answered the panel are on the tape and answer it again. A panel's loop never calls `Time_BeginSimTick`, so its frames come at the loop's own rate and carry the stale `SimTickDelta` of the frame before the panel; the retail tapes' mouse timestamps put that rate at 5-7 ms a frame.

A panel raised by a frame's own command runs from inside that frame's tick. `Sim_MainTick` builds the input, ticks the effect pools and the groups, and then calls `Sim_PollPlayerInput`, which dispatches the command word first — so the panel comes up there, and the rest of the tick, the machines included, waits for it. After it, the control laws read their axes from the shared input block, which now holds the panel's last build. The mission's own status alert comes up at the very end of a tick, after `Mission_PollStatus`.

All three retail tapes end in a panel, each a run of one repeated delta:

| Tape | Frames | Delta | Raised by | Answered by |
|---|---|---|---|---|
| `DEMO1` | 1015-1498 | 225 | no command on the tape — see [Open](#open) | a left click at (324, 298), pressed on 1427 and released on 1498 |
| `DEMO2` | 1587-1900 | 221 | `Q`, command `0x10` | `Enter` on 1899 |
| `DEMO3` | 3349-3659 | 249 | `Q` | left clicks at (375, 310) and (376, 306) |

Playback ends on a short read — any of the three header reads returning 0 — or when the player presses `[Ctrl]+[E]` (command `0x412`, tested on the live command word). Either way the file is closed, `004d255a` clears, live mouse input is restored and the live command word is zeroed; under `-D` the abort flag is raised as well. The live command word also still reaches `FUN_0045fd60`, so `Alt+Enter` and `-B`'s `Ctrl+B` work during playback ([`../command-line.md`](../command-line.md#dbsim)).

### Timing

**Playback is not paced.** The mission loop in `FUN_0045f144` calls `Time_BeginSimTick` (`004677bc`) — the spin until 40 ms have passed, and the only place `SimTickDelta` is measured — only while `004d255a` is clear. During playback `Sim_MainTick` (`0045f464`) instead copies the frame record's `SimTickDelta` into the global. The simulation therefore advances by exactly the time that passed when the tape was recorded, and frames come as fast as the machine can draw them: on a modern computer a demo finishes in a fraction of its recorded length.

The recorded deltas are far from the 25 Hz cap's 81. The three retail tapes were made on a machine running at about 7-9 frames a second:

| Tape | Frames | `SimTickDelta` min / median / max | Frames at the `0x1c2` clamp | Recorded length |
|---|---|---|---|---|
| `DEMO1` | 1499 | 155 / 225 / 450 | 89 | 187 s |
| `DEMO2` | 1901 | 90 / 272 / 450 | 267 | 278 s |
| `DEMO3` | 3660 | 92 / 231 / 450 | 51 | 422 s |

The recorded length is the sum of the deltas at 125/256 ms per count. A frame that took longer than the clamp's 220 ms is recorded as 220 ms, so that sum is the time the simulation saw, which is shorter than the wall time of the recording session.

`tools/scripts/patch_dbsim_tape_pacing.py` patches a retail `DBSIM.EXE` to play tapes in real time, for side-by-side comparison with this engine. It holds each simulation frame for its recorded delta and each panel frame for 6 ms. It is this project's modification, not retail behaviour.

## The checkpoint file

`-d` opens `<stem>.dmp` into `004d2562`: mode `wb` when recording, which it then closes at once while leaving the handle non-null, and `rb` when playing back. The routine built around it is `FUN_00401dc0 (ptr, size, label)`:

- **Recording:** copies `size` bytes from `ptr` onto the end of a buffer at `004a8580`, advancing the cursor `00497078` and the total `0049707c`.
- **Playback:** reads `size` bytes from the `.dmp` into that same buffer, `memcmp`s them against `ptr`, and on a mismatch sets `00497080` and formats `label` into a stack buffer that is then discarded.

The design is a state snapshot per checkpoint, compared on replay to detect drift. Nothing found writes the buffer out, so a recording's `.dmp` is left empty, and nothing found calls the routine; see [Open](#open). What `-d` is known to do in retail is create an empty `<stem>.dmp` when recording and hold one open when playing back.

## Reaching it

The main menu's `VIEW DEMO` button (`FUN_0043156f`, VSHELL) exits the shell with code 5, and `ES.EXE` answers that code by starting `dbsim` with `-D`; the tape's end returns exit code 6 and the shell comes back. `ES.EXE -r<name>` passes `-r<name>` through to every mission it launches. Nothing passes `-p`. See [`../command-line.md`](../command-line.md#the-loop), which also covers the unreferenced argument list in VSHELL that ends in `-D`.

`dbsim -eggplant -ptapes\demo1` replays a retail tape directly.

## Engine port

`--play <tape>` is `-p` and `--demo` is `-D`; `--demo` with `--play` plays that tape in demo mode. A tape is a path or a stem found in the install's `TAPES` folder. `HercWorks.Core`'s `InputTapeTransformer` reads and writes the format, byte-identical on the three retail tapes; `Input.InputTapePlayer` decodes frames and lays out the bundle, and the host feeds each frame's keystrokes, pointer, stick and trigger through the same handlers live input takes. The tape's keystrokes reach the host's key handlers through `TapeKeys`, which maps each set-1 scancode back to a key.

Where it differs from retail, by this engine's choice:

- **Paced.** Each simulation frame is held for its own recorded `SimTickDelta`, so a tape plays in the time it was recorded in; a panel frame is held for an estimated 6 ms.
- **The install is left alone.** The bundle is unpacked over a copy of `DATA` in the temp folder rather than over `DATA` itself, with `-p`'s preference reconciliation and the install's `keyjoy.cfg`, as [above](#the-bundle).
- **One keystroke per host frame.** Every recorded key press is held down for exactly one host frame with a frame of nothing between, so each auto-repeat press is a fresh key-down edge to the handlers, as each is a fresh command to retail's dispatcher.
- **The original's per-tick steps.** `SimMath.ScalePerTickStep` is off for the replay, so the acceleration and shield steps apply once per frame, as retail's do at whatever frame rate the tape was recorded at.
- **A panel's tick runs whole.** When a frame's own input raises a panel, the engine runs that frame's entire tick after the panel comes down, where retail has already ticked the effect pools before it.
- **Divergence is logged.** The host prints each frame a panel goes up or comes down on, and `InputTapePlayer.InferredPanelSpans` lists the runs of one repeated delta that mark the recording's own panels; a mismatch is the replay leaving the recording.

After the tape, `--play` hands the controls back to the player on the engine's own timestep, and `--demo` ends the mission. `[Ctrl+E]` stops either; under `--demo` any key does.

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
| `FUN_0045ba8c` | `0045ba8c` | Builds the live device block at `004d247a`, whose head is the live command word the stop and abort tests read |
| `Time_BeginSimTick` | `004677bc` | The 40 ms frame cap and `SimTickDelta` measurement; skipped during playback |
| `FUN_00401dc0` | `00401dc0` | The checkpoint: buffers a snapshot when recording, compares one when playing back |
| — | `004d2562` | The open `.dmp` checkpoint file's `FILE*` |
| `FUN_0045eea4` | `0045eea4` | Prefixes `data\`, or `tapes\` during playback, onto `prefs.cfg` and `keyjoy.cfg` |
| `Keyjoy_LoadConfig` | `0045b78c` | Reads `data\keyjoy.cfg` whatever is playing |
| — | `0049e844` | The preferences path; `tapes\prefs.cfg` during playback |

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| The `-D` abort and the `Ctrl+E` stop test the tape's own command word, so a recorded `Ctrl+E` ends playback and a retail demo aborts itself at its first command. | `Input_BuildPlayerDevice` tests `*DAT_004d2414`, which `FUN_0045ba8c` points at the live device block `004d247a`, and it tests it before the frame record is copied into `PlayerInputBlock`. The tape's command word never reaches either test. |

## Open

- **Open:** what calls the checkpoint routine `FUN_00401dc0`, and what would write its recording buffer to the `.dmp`. `es2_xref.py` finds no branch or stored pointer reaching `00401dc0` (its neighbour `Subsystem_RunPhase` returns seven callers under the same sweep), none reaching the unused `dmp` and `ab` strings at `00497088` and `0049708c` beside its own format string, and an absolute-operand search finds no reader of the mismatch flag `00497080`.

- **Open:** whether the retail tapes' missions are the `DEMO`, `DEMO_01` and `DEMO_02` entries in the campaign's own mission table ([`../shell/campaign-loop.md`](../shell/campaign-loop.md)), or the `DEMO*.MSN` files — a tape carries `script.dat`, a save formatted from a `.MSN` rather than the mission file itself, so the two are not matched up.
- **Open:** which panel `DEMO1`'s last 484 frames were recorded under. No command on the tape raises one, which leaves the status alert `Sim_MainTick` raises when the mission is decided as the candidate.
- **Unported:** recording (`-r`) and the checkpoint file (`-d`). The engine plays tapes and does not write them.
