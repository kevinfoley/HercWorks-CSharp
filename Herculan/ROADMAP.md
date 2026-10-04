# Roadmap — work not yet done

Everything the HERCULAN Engine does not implement yet, in one place. This is the counterpart of [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md), which records things that *are* implemented but behave differently from retail. If a feature is missing entirely it belongs here; if it is present and wrong it belongs there.

Each entry names the doc that owns the subject. **That doc is authoritative** for how much is reverse-engineered and how much is still unknown — this file only tracks that the porting work is outstanding.

## Reverse-engineered, not ported

The mechanism is understood; what is left is engine work.

- (Deferred) **A machine's crudest LOD roots are never drawn.** Root selection is ported, but the roots that compact their node numbering — the crudest one to three of each chassis — are excluded, because drawing them puts APOCA's upper body on a knee. The original composes every root through root 0's pose array too, so by the binary it should do the same; retail does not visibly do so, and what reconciles that is not yet found. **Settle that before changing anything here**: it decides whether the truncation is a divergence to lift or retail behaviour to match. → [`docs/retail/formats/mech-shape-drawing.md`](docs/retail/formats/mech-shape-drawing.md#the-pose-array-is-root-0s)
- **A throttle lever bound to the turret pair pitching the turret while the camera has the controls.** → [`docs/retail/formats/joystick-input.md`](docs/retail/formats/joystick-input.md#open)
- **The `.hmp` MIDI path and reading `SOUND.CFG`.** → [`docs/retail/formats/audio.md`](docs/retail/formats/audio.md#open)

## Reverse-engineering still open

The engine cannot be faithful here until the original is understood.

- **How many times DBSIM has advanced its generator before any given roll.** The algorithm, the 56-entry seed table and both cursor starts are ported, so the two generators produce identical streams from the same starting point — but a roll's result depends on its position in that stream, and this engine does not yet make the same draws in the same order. Replay parity needs the call history matched, which is really a question about tick order, not about the generator. → [`docs/retail/simulation/random-generator.md`](docs/retail/simulation/random-generator.md)
- **v1.10's language folder for `campaign.str`.** A French or German install shows the campaign map's stage text in English, because v1.10's `VSHELL.EXE` is not in the Ghidra project. → [`docs/retail/retail-builds.md`](docs/retail/retail-builds.md#open)

## The shell front end
`--shell` draws the frame every tab screen shares — the tiled backdrop, the square button and the eight captioned tabs, hit-tested, latching on the six tabs that latch, gated by campaign mode and switching palette per tab — plus the startup sequence and the main menu it brings up, whose `INSTANT ACTION` flies the next demo mission, `START NEW GAME` starts a campaign from its registration screen, `CONTINUE GAME` loads the current game and `VIEW DEMO` plays a demo tape, its practice screen, whose `Begin Mission` flies the lit training mission, and the save, weapons, repair, build, armory and crew screens behind tabs 0 to 6, weapon fitting included, with the squad panel down the left of four of them, and the mission tab's campaign map and its briefing with its map, whose `Rock & Roll >` launches the mission, and the way back from it — the debrief's accounting and its report, then the next mission's load or the offer to replay the one that ended the campaign — all with the shell's music and click sounds, and its movies: the intro, the campaign map's with its location picture, the briefing's and `CREDITS`. The widget paints are ported onto an indexed software canvas, so a further screen is layout, text and hit-testing rather than new drawing code. What is missing:
- **The startup's `Performance Note` box.** Retail shows it once; the shell does not. → [`docs/retail/shell/screen-layout.md`](docs/retail/shell/screen-layout.md#the-main-menu)
- **Three shell controls:** a content button's caption nudging down while pressed, the mission screen arrows' auto-repeat, and the developer's mission-name dialog. → [`docs/retail/shell/screen-layout.md`](docs/retail/shell/screen-layout.md#open)

## Debugging features
- **A launch option that disables the AI.** Every unit but the player's stays stationary, though it can still be damaged and destroyed. → [`docs/herculan/herculan-command-line.md`](docs/herculan/herculan-command-line.md#developer)
- **Editing the current `script.dat` in the Editor**, chiefly moving Cybrid and player spawn points, to set up test scenarios quickly. The scope, and the RE question that gates adding records, are in [`docs/herculan/handoff-editor-mission-editing.md`](docs/herculan/handoff-editor-mission-editing.md). → [`docs/retail/formats/script-dat.md`](docs/retail/formats/script-dat.md)
- (Deferred) **Exact replay of an engine recording.** `--record` writes tapes `--play` replays, but the tick length, pointer rounding, held keys and a handful of inputs the tape does not carry let a replay drift from its recording; each is a choice of how this engine records, fixable without further RE. A check that catches the drift would be this engine's own design: retail's `-d` checkpoint routine has no known caller, so what it snapshots, and when, is not known. → [`docs/herculan/input-tapes.md`](docs/herculan/input-tapes.md#what-a-replay-of-an-engine-recording-does-not-reproduce), [`docs/retail/formats/tap-input-tape.md`](docs/retail/formats/tap-input-tape.md#the-checkpoint-file)

## HERCULAN's own interface
- (Deferred) **Interface languages in other scripts.** Every ImGui window builds its font atlas from Open Sans with ImGui's default glyph ranges, Latin and Latin-1 only (each `new ImGuiFontConfig(path, 16)` in `Herculan.Engine.Host` and the editor), so a `.lang` file in Cyrillic or another script would draw its text as missing glyphs. Worth doing once such a file exists: pass the ranges it needs, and check the font carries them. → [`LocalizationTable.cs`](src/Herculan.Engine.Host/Localization/LocalizationTable.cs)

## HercWorks editors
- **Edit the configuration files and zone headers in HercWorks UI.** Core reads and writes `data\keyjoy.cfg` (`KeyjoyTransformer`), `data\prefs.cfg` (`PrefsTransformer`), `data\drive.cfg` (`DriveTransformer`) and `dat\zoneNNNN.dat` (`ZoneDatTransformer`), but the UI has no form for any of them. Two things to settle first: `keyjoy.cfg`'s writer drops the comments a retail file ships with, and `prefs.cfg` should be written back as the simulator does — a read-modify-write of the bytes the form changed, never a fresh 54-byte dump. → [`docs/retail/formats/joystick-input.md`](docs/retail/formats/joystick-input.md), [`docs/retail/simulation/preferences.md`](docs/retail/simulation/preferences.md), [`docs/retail/formats/terrain-heightmap.md`](docs/retail/formats/terrain-heightmap.md)
