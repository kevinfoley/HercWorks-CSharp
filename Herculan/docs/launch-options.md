# Launch options (retail)

Earthsiege 2 is three programs. **ES.EXE** is the launcher. **VSHELL.EXE** is the front end: the menus, the campaign, the armory and the debriefing. **DBSIM.EXE** is the simulator: it runs one mission and exits. The launcher starts the front end, and every time the front end hands over a mission it starts the simulator, then the front end again when the mission ends.

Each program accepts options typed after its name, separated by spaces:

```
ES.EXE -s -L
```

Normally only ES.EXE is started by hand, and it passes the right options on to the other two. The front end and the simulator can be started directly with their own options, as long as `-eggplant` is among them.

Letter case matters for ES.EXE and the simulator: `-s` and `-S` are different options there. The front end ignores case and also accepts `/` in place of `-`.

This page is for players. The technical detail behind each option is in [`command-line.md`](command-line.md) and the docs it links.

## Launcher — ES.EXE

| Option | What it does |
|---|---|
| `-s` | Turns sound off, in both the front end and the simulator. |
| `-L` | Low-memory mode in the simulator; see `-l` [below](#memory). |
| `-r<name>` | Records every mission flown to `<name>.tap`; see [Recording and playback](#recording-and-playback). |
| `-a` | Passes `-a` to the front end; see [Open](#open). |
| `-SPRUNKNOWN` | Developer mode. The front end gets the mission picker (`-@`) and the simulator gets the developer keys (`-SPRUNKNOWN`), both described below. |
| `-X` | Only after `-SPRUNKNOWN`. Skips the front end entirely and flies the current mission over and over. |

Before starting anything, ES.EXE checks the computer's memory. With too little virtual memory it shows a message and quits; with under 8 MB of RAM it only warns.

The launcher also picks the music: the first mission after it starts plays CD track 2, the next track 3, and so on up to track 6, then back to 2.

## Front end — VSHELL.EXE

| Option | What it does |
|---|---|
| `-eggplant` | Required. Without it the front end says it cannot be run directly, and stops. |
| `-f` | French in place of English. |
| `-g` | German in place of English. |
| `-s` | Turns sound off. |
| `-m` | Turns the mouse off. |
| `-k` | Turns the keyboard off. |
| `-X3`, `-X4` | Goes straight to the debriefing for the mission just flown, then on to the campaign. The launcher passes these after a mission. |
| `-@` | Before each campaign mission, leaves up a developer panel titled DEBUG that names the next mission, with a button to fly a different mission file instead. Without `-@` the front end dismisses that panel itself. |
| `-r` | Listed in the program's own help text as "Returning from sim", but does nothing: `-X` does that job. |
| `-l`, `-d` | Accepted, but do nothing. |
| `-v` | Listed in the help text as displaying the version number. It also turns sound off. |
| `-?` | Listed in the help text as displaying the list of options. It also turns sound off. |

`-a` and any `-e…` other than `-eggplant` are recognised too; see [Open](#open).

## Simulator — DBSIM.EXE

### Required

| Option | What it does |
|---|---|
| `-eggplant` | Required. Without it the simulator says it is not meant to be run on its own, and stops. |

### Display

| Option | What it does |
|---|---|
| `-v0` | Low resolution: the 3D view is drawn at 320×240 and scaled up. |
| `-v1` | 640×480, but with the low-resolution versions of the instruments and their artwork. |
| `-v2` | 640×480 with the high-resolution artwork throughout. Any higher digit is the same. |
| `-Z1` | Full screen. |
| `-Z0` or `-Z` | In a window. |
| `-b` | Switches to an older way of putting frames on screen that this version of the game never finished. Once a mission starts the picture stops updating; see [Open](#open). Do not use. |

Without `-v` or `-Z` the simulator uses the settings saved from its preferences panel. The saved setting can only choose between the `-v0` and `-v2` looks; `-v1` is reachable only from the command line. See [`simulation/preferences.md`](simulation/preferences.md#the-video-mode-and-full-screen-bytes) and [`formats/cockpit-views.md`](formats/cockpit-views.md#video-modes).

### Sound, music and language

| Option | What it does |
|---|---|
| `-s` | Starts without sound. |
| `-R<n>` | Chooses the CD music track for the mission. The track played is the remainder of *n* ÷ 5, plus 2: `-R0` plays track 2 (the default), `-R1` track 3, up to `-R4` for track 6. The launcher counts up through these one mission at a time. See [`formats/audio.md`](formats/audio.md#which-track-and-whether-there-is-one). |
| `-F` | French text and speech. |
| `-G` | German text and speech. |
| `-E` | Spanish. The disc carries some Spanish text but no Spanish speech. |

### Cockpit

| Option | What it does |
|---|---|
| `-C<HERC>` | Shows the named HERC's cockpit whatever HERC is being piloted, e.g. `-CRAZOR`. Letter case does not matter. Recognised names: `ROADRUNNER`, `OUTLAW`, `RAPTOR2`, `TOMAHAWK`, `PATRIOT`, `PANTHER`, `SAMSON`, `COLOSSUS`, `APOCA`, `RAZOR`, `MAVERICK`, `OGRE`, `TEST3`; any other name is ignored. Only nine of those HERCs have cockpit artwork on the disc; see [Open](#open). |
| `-t<n>` | Puts pilot number *n* from the game's pilot list in the first squad message box on the heads-down display, in place of its usual pilot. See [`formats/heads-down-display.md`](formats/heads-down-display.md#squad-comm-boxes). |

### Memory

| Option | What it does |
|---|---|
| `-l` | Low-memory mode, for machines with little RAM. Cockpit artwork is loaded only when it is needed, sound gets half the usual memory, and the ground under bases is left unpainted. The simulator switches this on by itself on a machine with less than 12 MB. See [`formats/audio.md`](formats/audio.md#memory-budget-and-eviction) and [`formats/terrain-texturing.md`](formats/terrain-texturing.md#base-formation-pads). |

### Recording and playback

The simulator can record everything the player does during a mission to a file and play it back later. Three such recordings ship as demos, and **VIEW DEMO** on the main menu plays one of them. See [`formats/tap-input-tape.md`](formats/tap-input-tape.md).

| Option | What it does |
|---|---|
| `-r<name>` | Records the mission to `<name>.tap`. |
| `-p<name>` | Plays `<name>.tap` back. |
| `-D` | Plays one of the shipped demo recordings, chosen at random. Player input stops it. This is what VIEW DEMO does. |
| `-d` | Used with `-r` or `-p`. Saves checkpoints of the game's state beside the recording, and during playback compares against them to catch a replay that has drifted from the original. |

### Developer mode: `-SPRUNKNOWN`

Turns on a set of keys the programmers used for testing. The `Ctrl+Alt+number` keys choose how far the movement keys move and turn; until one is pressed, the movement keys do nothing.

| Key | What it does |
|---|---|
| `Alt+S` | Freezes and unfreezes the whole simulation. |
| `Alt+keypad +` | While frozen, runs the simulation for one frame. |
| `Alt+Up`, `Alt+Down` | Moves your HERC forward or back. |
| `Alt+Left`, `Alt+Right` | Moves your HERC sideways. |
| `Ctrl+Left`, `Ctrl+Right` | Turns your HERC on the spot. |
| `Ctrl+Alt+1` … `Ctrl+Alt+9` | Sets the size of each move and turn, from smallest to largest. |
| `Ctrl+N`, `Ctrl+P` | Moves the camera to the next or previous object in the mission. |
| `Ctrl+F` | Switches camera mode like `V`, but the outside camera follows the object chosen with `Ctrl+N`/`Ctrl+P` instead of your HERC. |
| `Ctrl+T` | Takes your HERC off the controls: it stops firing and ignores the throttle. `Ctrl+N` and `Ctrl+P` do this too. Press again to take the controls back. |
| `Ctrl+Alt+.`, `Ctrl+Alt+,` | Chooses which part of a machine `Ctrl+Alt+D` hits (30 to choose from). |
| `Ctrl+Alt+D` | Damages the chosen part of whatever the camera is on — your own HERC until the camera has been moved. |
| `Ctrl+Alt+N` | Hits a nearby Cybrid machine with a massive amount of damage. |

`Alt+S` also works while a recording is being made or played back, without developer mode.

### Other developer aids

| Option | What it does |
|---|---|
| `-B` | `Ctrl+B` stops the program for a debugger, which crashes it when no debugger is attached. Also stops `Alt+Enter` switching full screen while a recording plays back. |
| `-m` | Clears a second, monochrome debugging monitor, through a driver that does not ship with the game. Without that driver it does nothing. |

### Options with no effect

`-S`, `-P` and `-X<n>` are accepted and do nothing. `-T<n>`, `-V<n>`, `-W<n>`, `-a` and `-c` are accepted with no effect found, and neither the launcher nor the front end ever passes them; see [Open](#open).

## Open

- **Open:** whether the simulator's `-T<n>`, `-V<n>`, `-W<n>`, `-a` and `-c` do anything; see [`command-line.md`](command-line.md#open).
- **Open:** what the front end's `-a` and non-`eggplant` `-e…` options do.
- **Open:** what `-C` does with the four names that have no cockpit artwork, and what `-E` does without Spanish speech files.
- **Open:** where the front end's `-v` and `-?` text appears; it is written to standard output, which a Windows program normally does not have.
- **Open:** `-b` has not been tried against retail; the expected behaviour on each Windows family is in [`formats/cockpit-views.md`](formats/cockpit-views.md#open).
