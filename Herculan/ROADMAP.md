# Roadmap — work not yet done

Everything the HERCULAN Engine does not implement yet, in one place. This is the counterpart of [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md), which records things that *are* implemented but behave differently from retail. If a feature is missing entirely it belongs here; if it is present and wrong it belongs there.

Each entry names the doc that owns the subject. **That doc is authoritative** for how much is reverse-engineered and how much is still unknown — this file only tracks that the porting work is outstanding.

## Reverse-engineered, not ported

The mechanism is understood; what is left is engine work.

- **The triple turret does not shoot.** Four of the five structure classes now fill their `+0x18` tick slot; type `0x22`'s three-turrets-from-one-object tick (`004045c8`) is the one left, and it needs the 11-`short` weapon descriptor table at `DAT_004a9640` dumped out of the data section. → [`docs/simulation/structure-behaviour.md`](docs/simulation/structure-behaviour.md)
- **A machine's crudest LOD roots are never drawn.** Root selection is ported, but the roots that compact their node numbering — the crudest one to three of each chassis — are excluded, because drawing them puts APOCA's upper body on a knee. The original composes every root through root 0's pose array too, so by the binary it should do the same; retail does not visibly do so, and what reconciles that is not yet found. **Settle that before changing anything here**: it decides whether the truncation is a divergence to lift or retail behaviour to match. → [`docs/formats/mech-shape-drawing.md`](docs/formats/mech-shape-drawing.md#the-pose-array-is-root-0s)
- **Terrain raycast, swept-volume mode.** Only thin-ray mode is ported; the swept-volume mode (movement collision) is not, because nothing in the engine needs it yet. → [`docs/formats/terrain-heightmap.md`](docs/formats/terrain-heightmap.md)
- **An engine recording does not replay exactly.** `--record` writes tapes `--play` replays, but the tick length, pointer rounding, held keys and a handful of inputs the tape does not carry let a replay drift from its recording. `-d`'s checkpoint file, which would catch that drift, is not ported. → [`docs/engine/input-tapes.md`](docs/engine/input-tapes.md#what-a-replay-of-an-engine-recording-does-not-reproduce)

## Reverse-engineering still open

The engine cannot be faithful here until the original is understood.

- **How many times DBSIM has advanced its generator before any given roll.** The algorithm, the 56-entry seed table and both cursor starts are ported, so the two generators produce identical streams from the same starting point — but a roll's result depends on its position in that stream, and this engine does not yet make the same draws in the same order. Replay parity needs the call history matched, which is really a question about tick order, not about the generator. → [`docs/simulation/random-generator.md`](docs/simulation/random-generator.md)

## The shell front end
`--shell` draws the frame every tab screen shares — the tiled backdrop, the square button and the eight captioned tabs, hit-tested, latching on the six tabs that latch, gated by campaign mode and switching palette per tab — plus the startup sequence and the main menu it brings up, whose `INSTANT ACTION` flies the next demo mission, `START NEW GAME` starts a campaign from its registration screen, `CONTINUE GAME` loads the current game and `VIEW DEMO` plays a demo tape, its practice screen, whose `Begin Mission` flies the lit training mission, and the save, weapons, repair, build, armory and crew screens behind tabs 0 to 6, weapon fitting included, with the squad panel down the left of four of them, and the mission tab's campaign map and its briefing with its map, whose `Rock & Roll >` launches the mission, and the way back from it — the debrief's accounting and its report, then the next mission's load or the offer to replay the one that ended the campaign — all with the shell's music and click sounds, and its movies: the intro, the campaign map's with its location picture, the briefing's and `CREDITS`. The widget paints are ported onto an indexed software canvas, so a further screen is layout, text and hit-testing rather than new drawing code. What is missing:
- **The startup's `Performance Note` box.** Retail shows it once; the shell does not. → [`docs/shell/screen-layout.md`](docs/shell/screen-layout.md#the-main-menu)
- **Indeo Video 4.1.** `ES2DROP3.AVI`, the one file in it, is not decoded; no shell movie id names it. → [`docs/formats/avi-video.md`](docs/formats/avi-video.md#open)
- **A movie that will not open.** The shell skips it, where retail asks for the CD. → [`docs/shell/screen-layout.md`](docs/shell/screen-layout.md#the-shells-movies)

## Debugging features
- **A launch option that disables the AI.** Every unit but the player's stays stationary, though it can still be damaged and destroyed. → [`docs/engine/host-flags.md`](docs/engine/host-flags.md#developer)
- **Editing the current `script.dat` in the Editor**, chiefly moving Cybrid and player spawn points, to set up test scenarios quickly. The scope, and the RE question that gates adding records, are in [`docs/engine/handoff-editor-mission-editing.md`](docs/engine/handoff-editor-mission-editing.md). → [`docs/formats/script-dat.md`](docs/formats/script-dat.md)

## HercWorks editors
- **Edit the configuration files and zone headers in HercWorks UI.** Core reads and writes `data\keyjoy.cfg` (`KeyjoyTransformer`), `data\prefs.cfg` (`PrefsTransformer`), `data\drive.cfg` (`DriveTransformer`) and `dat\zoneNNNN.dat` (`ZoneDatTransformer`), but the UI has no form for any of them. Two things to settle first: `keyjoy.cfg`'s writer drops the comments a retail file ships with, and `prefs.cfg` should be written back as the simulator does — a read-modify-write of the bytes the form changed, never a fresh 54-byte dump. → [`docs/formats/joystick-input.md`](docs/formats/joystick-input.md), [`docs/simulation/preferences.md`](docs/simulation/preferences.md), [`docs/formats/terrain-heightmap.md`](docs/formats/terrain-heightmap.md)
