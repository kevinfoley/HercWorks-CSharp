# Known Issues

## Earthsiege 2 (original retail game)

_Bugs listed in this section were tested on Windows 11. It's possible that some bugs would not occur on original 1990s hardware. Bugs in this section are reproduced as-is in HERCULAN Engine unless otherwise noted._

- Samson cockpit: several HUD elements are slightly misaligned (shield balance text is not centered horizontally with meter; firing chain, LINK, and TRACK buttons are slightly too high and too far outward)
- Player Herc acceleration/deceleration and turning are framerate-dependent (**fixed** in HERCULAN Engine)
- The "center legs" function moves the turret awkwardly and does not center the legs perfectly.
- Herc stats shown in the VSHELL "Build" screen may be incorrect (the Outlaw is listed as having a top speed of 80 kph, but the manual and in-game readout show 100 kph).
- Some buildings' collision volumes are shorter than the visible mesh, so shots pass through the top of the building.
- Turning with the keyboard turns at half of the speed of turning with the joystick.
- The HUD speed readout does not reflect speed accurately. Hercs have two strides, "walking" and "running". In the walking stride, the Herc moves much more slowly than the speed gauge indicates.
- An ELF or ELF2 shot paints a stray orange-white dash at the muzzle, roughly four pixels wide, where the jagged tracer branch falls through into the straight-beam draw. See [`docs/simulation/beam-visuals.md`](docs/simulation/beam-visuals.md#the-muzzle-stub-is-a-retail-fall-through).
- In the low-memory mode (`-l`, or under 12 MB physical) samples load from `SIMSOUND.VOL`'s `hmx\` bank instead of `hmi\`. `EXPLO5.WAV` is in `hmi\` only, so catalog id `0x22` fails to open and that explosion is silent. See [`docs/formats/audio.md`](docs/formats/audio.md#sample-banks).
- The Range readout of the MFD TARGET tab (F5) prints raw world units — 1 unit = 6 mm. Every other distance readout in the cockpit converts to metres first (`Hud_WorldUnitsToMetres`).

## HERCULAN Engine

_Note to Claude: This section is for listing outstanding issues with features which have been implemented but behave differently than retail or otherwise incorrectly. Features that haven't been tackled yet go in [`ROADMAP.md`](ROADMAP.md)._

- Terrain detail-texture scatter differs from retail. Roughly 30% of 2x2 cell blocks are drawn with
  the theater bank's frame 1 instead of the plain frame 0, and which blocks is a random roll at zone
  load. The engine's generator is not seeded like DBSIM's, so the scatter is statistically faithful
  but lands on different cells. See the SimRandom seed-table entry in [`ROADMAP.md`](ROADMAP.md) for
  the blocker. Base pads (frames 2–12) are unaffected — those are placed from `BFORMS.DAT`, not
  rolled.
- Distance fog on terrain is smooth where retail's steps cell by cell. The amount of fog and the
  ground it lands on match; the flat step across each cell is missing. See "Engine port" in
  [`docs/formats/distance-fog-and-sky.md`](docs/formats/distance-fog-and-sky.md).
- `TSGouraudPoly` faces fade by an RGB blend toward the fog colour rather than by a ramp depth slice.
  Every other surface fogs through the ramp as retail does. Same section as above.
- Gouraud-shaded structures band differently from retail. On the type-15 octagonal tower, retail draws six narrow bands (~4 px, ramp-8 entries 9 down to 4) then four wide ones (28, 29, 29, 59 px, entries 3 down to 0); the engine cannot produce the wide dark bands, because `Light_ComputeShadeForFace` is negative at both corners of the away-facing facet and so must flat-fill entry 0 there. The sun direction, the light intensity and the ramp entry sequence are each excluded as the cause. See "Unresolved: type-15 band widths" in `docs/formats/dts-texture-binding.md`. Reference captures: `Reference/Gouraud_shading_comparison_2.png`, `Reference/Scramble_Training_Base_4.png`.
- Lighting on HERCs may not be correct (needs review) — re-check: HERCs are almost entirely `TSShadedPoly`, so the shade-ramp and away-facing-light fixes changed their colouring too.
- When projectiles hit buildings, many hit effects seem to clip inside the building - I don't observe this in retail. **Hit geometry ruled out**, and **building LOD ruled out** (the engine draws maximum detail, which is what retail's screenshots were taken at). Most likely a draw order issue.
- Claude used a separate camera for each cockpit panel, which causes a visible distortion in the side panels, particularly when looking downward at all. Will probably need to replace with a single camera covering the full width of the window (remember to maintain the same vertical FOV as the original game!). Or maybe we can crop the camera views by window frame rather than square, so that the seam isn't visible...
- Due to nearest-neighbor scaling, text often looks bad when window height isn't an integer multiple of 240.
- Currently missing is a quirk from retail where the player's shield meter fills in over ~10 seconds at the start of a mission. Claude says there's no explanation for this in the shield code, where the shields start out at full charge, and would take ~30 seconds to fully charge from empty. The fade-in-over-10-seconds may be a HUD animation that hasn't been discovered during RE yet.
- Similarly to the previous, currently missing is an animation where weapon buttons wink on one-at-a-time when the simulation first starts.
- In the Scramble practice mission while piloting an Apocalypse, a Particle Beam Weapon is equipped to slot 8. In HERCULAN, when this PBW is fired the beam visibly clips off near the corner of the screen. This may be a camera near-clip plane issue.
- Targeting range seems to be much lower than retail, even with active radar. Possibly because there's currently no AI, so the enemies never switch their radar on.
- A sound placed exactly abeam the listener pans to the correct side, where retail snaps it to the opposite one. `Sound_Place` computes the front half of its pan as `(ushort)(bearing * -2)`, which for a bearing of zero — a source precisely to the left or right, with no forward component at all — yields the hard-*left* value while every neighbouring bearing on both sides yields the hard-*right* one; the continuous value there is `0x10000` and only the truncation to sixteen bits inverts it. `SoundDirector.Place` computes `0x10000 - 2 * bearing` clamped instead. The engine reaches that exact zero far more often than the original does — DBSIM's forward component comes out of a full camera matrix carrying pitch and roll, where an exact zero is a coincidence, and the engine's comes out of a plain horizontal rotation, where it is simply what "abeam" means — so reproducing it would put an audible snap to the far channel on anything passing the player. See [`docs/formats/audio.md`](docs/formats/audio.md#playing-a-sound).
- The sound catalog's memory budget is not reproduced: every sample loads at startup rather than being cached on demand and evicted under a cap. No audible difference on retail data — see "Engine coverage" in [`docs/formats/audio.md`](docs/formats/audio.md#engine-coverage).
- The cockpit power-up always announces `POWERUP INITIATED. ALL SYSTEMS NOMINAL.` Retail picks between that and the internal-damage line by testing each of the ten heads-down gauges against `0x5a`, through two accessors that are not decompiled, so what that figure is a percentage of is unknown and the threshold is not transcribed. Only reachable by taking an already-damaged machine, which no mission start does. See "Engine coverage" in [`docs/formats/audio.md`](docs/formats/audio.md#engine-coverage).
- The preferences screen's four-way OFF / TEXT ONLY / VOICE ONLY / TEXT-VOICE setting for each message channel has no UI: the message port is always on both halves. Its byte only ever distinguishes three behaviours in the code, so which of the four labels means what is not read from the binary. See "The computer's messages" in [`docs/formats/audio.md`](docs/formats/audio.md#the-computers-messages).
- The message port draws whenever it has a line, where retail gates the display half on two fields of the object `FUN_00429820` returns: a mode enum at `+0x14`, which suppresses the line in mode 4 while the lifecycle runs on, and a byte at `+0x1c` the paint returns on. That object is not decoded, so neither gate is transcribed rather than guessed at.
- The pilot and squad message channel — the message port's second instance, which wraps several lines instead of scrolling one — is not ported, and nothing posts to it. It needs squadmate speech, which is not ported either. See [`ROADMAP.md`](ROADMAP.md).
- The `[V]` external view is placeholder rather than retail's. See [`ROADMAP.md`](ROADMAP.md).
- The `[P]` pause is a placeholder that just stops the fixed-timestep tick loop. See [`ROADMAP.md`](ROADMAP.md).
- I think computer voiceover ("Powerup initiated", "Active radar mode", etc) is much higher-fidelity than in retail, but I can't get sound working in my retail copy on Windows 11 so I'm unable to check. The voiceover may be downsampled in retail. Will investigate later and maybe eventually add a vanilla setting.

## HercWorks toolkit — inherited from the Java original

_Bugs present in the original Java source that are still reproduced in the C# port. Ported bug-compatible rather than silently fixed, per the porting policy in `README.md`. Bugs that have since been fixed are not listed here._

- `VolFileCompiler.Compile()` writes to the hardcoded developer path `E:\ES2_OS\dev\earthsiege2\VOL`, carried over from the Java original. **The only one of these with real teeth** — it is reachable code and will write to the wrong place, or fail, on any machine but the original author's. The output path should come from the caller.
- `ThreeSpaceByteTransformer.PeekAt(int at)` returns `Index + at` — an offset, not the byte at that offset. It dereferences nothing. Unused (no callers), so inert; looks unfinished in the original rather than wrong.
- `DatFileReader.ReplaceDatBytes(byte[] newData, DataFile targetFile)` never uses `newData`; it concatenates the target's existing `Header` and `RawBytes` unchanged, so it does not replace anything. Unused (no callers).
- `InitHerc.Header` is built as the 8 ASCII bytes of the string `"661FAF55"` rather than the 4 hex-decoded bytes `66 1F AF 55`, ported literally from the Java original. **Probably wrong:** `DynamixPalette.Header` had the identical construction and was confirmed wrong against a real `.DPL` (`SHELL0\DPL\ALPHA.DPL` starts `0F 00 28 00`, the decoded value, not the 8-byte ASCII string). Needs a real `INIT.DAT` to confirm before changing.
