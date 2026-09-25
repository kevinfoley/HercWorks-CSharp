# Key bindings

The simulator's keyboard, by what each key does. Keys the manual does not list are marked **(not in the manual)**. The technical side — how a keystroke becomes a command code, and which handler answers it — is [`formats/cockpit-input.md`](formats/cockpit-input.md#keyboard-commands-are-scancodes); the joystick's bindings are [`formats/joystick-input.md`](formats/joystick-input.md).

"Keypad" means the numeric keypad with Num Lock off. The arrow keys and the keypad's arrows are the same keys to the game.

## Driving

| Key | What it does |
|---|---|
| `Up`, `Down` | Throttle forward and back. Holding `Down` takes the throttle through zero into reverse. |
| `Left`, `Right` | Steer. |
| Keypad `5` | All stop. |
| `J`, `K` | Twist the turret left and right. |
| `I`, `M` | Pitch the turret up and down. |
| `Backspace` | Centre the turret on the body and turn Automatic Turret Tracking off. |
| `\` | Centre the legs under the turret. |
| Keypad `+`, `-` | Throttle, in the RAZOR. |

## Weapons

| Key | What it does |
|---|---|
| `Space` | Fire, for as long as it is held. |
| `1` … `0` | Select weapon row 1 to 10. |
| `Alt+1` … `Alt+0` | Add that row to the current firing chain, or take it out. |
| `W`, `Alt+W` | Next and previous weapon. |
| `L` | Link the selected weapon to the identical one on the opposite hardpoint. |
| `` ` `` (the manual's `[~]`) | Next firing chain. |
| `=`, `-`, and keypad `+`, `-` | **(not in the manual)** Raise or lower the selected energy weapon's power level, one step a press. A weapon starts at 960 of 1200 and a step is 80, so three presses reach the top. For a weapon that charges up before it fires, such as the particle beam, a higher level is a stronger shot that takes longer to charge; its damage over time stays about the same. For a laser it changes nothing but the charge bar. See [`simulation/weapon-firing.md`](simulation/weapon-firing.md#power-level--weaponmount_adjustpowerlevel-0040f48c). |

## Targeting and shields

| Key | What it does |
|---|---|
| `Enter` | Select the next target. |
| `'` | Select the nearest target. |
| `;` | Clear the target. |
| `Tab` | Step through the target's components, with a Targeting Pod. |
| `R` | Radar active or passive. |
| `Alt+R` | Radar range, in active mode. |
| `T` | Automatic Turret Tracking on or off. Turning it off also centres the turret. |
| `[`, `]` | Move shield power toward the rear or the front. |

## Displays and views

| Key | What it does |
|---|---|
| `F1` … `F6` | MFD screen: STATUS, FLASH COMM, NAV MAP, SCANNER, TARGET, MISSILE CAM. Also returns from the Heads-Down Display. |
| `F7`, `F8` | Heads-Down Display: command display, damage detail. |
| `F9`, `F10` | Look out of the left and right windows. |
| `Esc` | Back to the forward view from a side window or the Heads-Down Display. |
| `V` | External views. |
| `D` | Status of the other HERCs. |
| `Alt+D` | Drop a nav marker where you stand. |

On FLASH COMM:

| Key | What it does |
|---|---|
| `A`, `G`, `H`, `O`, `C`, `E`, `F` | Pick an order: attack my target, ignore my target, help me out, join on me, scan for hostiles, EMCON, fire at will / hold your fire. |
| `.`, `,` | Step through the orders. |
| `X` | Transmit. |
| `Alt` + an order's key | Pick and transmit that order from any screen. |

On the Heads-Down Display's command display:

| Key | What it does |
|---|---|
| `1`, `2`, `3` | Pick a squadmate, left to right. |
| `D`, `A`, `F`, `T`, `G`, `O`, `C`, `E` | Pick an order. |
| `,`, `.` | Step through the orders. |
| `+`, `-` | Zoom the map. |
| Arrows | Scroll the map. |
| Keypad `5` | Put the map back on your HERC. |
| `X`, `Backspace` | Transmit, cancel. |

On the damage detail: `S`, `I` and `W` show structural, internal and weapon systems.

## Panels and the game

| Key | What it does |
|---|---|
| `Q` | How the mission stands, with a way out of it. |
| `P` | Pause. |
| `Ctrl+Q` | Leave the game. |
| `F11` | Mission objectives. |
| `F12`, `Alt+P` | Preferences. |
| `/` (the manual's `[?]`) | The on-line manual. |
| `Enter`, `Esc` | Close a panel with its first button. |
| `Alt+Enter` | Switch between full screen and a window. |

While an input tape plays back, `Ctrl+E` stops it; see [`formats/tap-input-tape.md`](formats/tap-input-tape.md).

## Developer keys

**(not in the manual)** Keys the programmers used for testing, which DBSIM answers only when it is started with [`-SPRUNKNOWN`](launch-options.md#developer-mode--sprunknown). `Alt+S` is the exception: it also works while a recording is being made or played back. The retail code behind each key is [`command-line.md`](command-line.md#-sprunknown-the-developer-keys).

The `Ctrl+Alt+number` keys choose how far the move and turn keys go; each mission starts on the `Ctrl+Alt+4` size. The move and turn keys act on the HERC the camera is on, which is your own until `Ctrl+N` or `Ctrl+P` moves the camera, and they take it straight through anything in the way. The arrows keep their ordinary job under `Alt` and `Ctrl`, so `Alt+Left`/`Right` and `Ctrl+Left`/`Right` also steer your HERC, and `Alt+Up`/`Down` also move its throttle. Holding any of these keys repeats it at the keyboard's repeat rate, so a held move key keeps moving.

| Key | What it does |
|---|---|
| `Alt+S` | Freezes and unfreezes the simulation. Everything stops but your own controls: the throttle still moves and your HERC still turns, fires and moves its turret, but it covers no ground, and what it fires waits in the air. Whether it can turn depends on where the throttle is. |
| `Alt+keypad +` | Runs the simulation for one frame, then freezes it. |
| `Alt+Up`, `Alt+Down` | Moves the HERC forward or back. |
| `Alt+Left`, `Alt+Right` | Moves the HERC sideways. |
| `Ctrl+Left`, `Ctrl+Right` | Turns the HERC on the spot. |
| `Ctrl+Alt+1` … `Ctrl+Alt+9` | Sets the size of each move and turn, from smallest to largest. |
| `Ctrl+N`, `Ctrl+P` | Moves the camera to the next or previous object in the mission. |
| `Ctrl+F` | Switches camera mode like `V`, but the outside camera follows the object chosen with `Ctrl+N`/`Ctrl+P` instead of your HERC. |
| `Ctrl+T` | Takes your HERC off the controls: it stops firing and ignores the steering, throttle and turret keys, and those keys drive the camera instead. `Ctrl+N` and `Ctrl+P` do this too. Press again to take the controls back. |
| `Ctrl+Alt+.`, `Ctrl+Alt+,` | Chooses which part of a machine `Ctrl+Alt+D` hits (30 to choose from). |
| `Ctrl+Alt+D` | Damages the chosen part of whatever the camera is on — your own HERC until the camera has been moved. |
| `Ctrl+Alt+N` | Hits a nearby Cybrid machine with a massive amount of damage. |

## HERCULAN Engine

The engine takes the keys above and adds its own:

| Key | What it does |
|---|---|
| `C` | Switch between piloting and a free camera: `W`, `A`, `S`, `D` move, `R`, `F` rise and fall, the arrows look, `Shift` goes faster. |
| `Esc` | In the forward view, raise the menu bar with the debug and tweak panels; press again to back out. |
| Left mouse drag | Swing the camera round the HERC in the external view. |

`V` is a single orbiting external view that `V` toggles, where retail steps through several. During a tape replay, `C`, `Esc`'s menu bar and `Ctrl+E` stay with the player's own keyboard and every other key comes from the tape; under `--demo` any key ends the demo.

`--developer` turns on the [developer keys](#developer-keys), with three differences from retail:

- The move and turn keys only move and turn. Under `Alt` or `Ctrl` the arrows neither steer nor move the throttle, so a sideways move looks like one rather than being hidden in a turn.

- `Ctrl+N` and `Ctrl+P` put the camera in the orbiting external view round the chosen object, where retail views from the object itself; choosing your own HERC again returns to the cockpit.
- `Ctrl+F` does nothing, and `Ctrl+T`, `Ctrl+N` and `Ctrl+P` take the controls off your HERC without handing them to the camera. Both wait on retail's external cameras; see [`ROADMAP.md`](../ROADMAP.md).
