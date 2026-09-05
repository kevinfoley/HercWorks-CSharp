# Known Issues

## Earthsiege 2 (original retail game)

_Bugs listed in this section were tested on Windows 11. It's possible that some bugs would not occur on original 1990s hardware. Bugs in this section are reproduced as-is in HERCULAN Engine unless otherwise noted._

_Note to Claude: Detailed technical descriptions belong in their respective docs, not here. Give a short plain-English summary._

- Samson cockpit: several HUD elements are slightly misaligned (shield balance text is not centered horizontally with meter; firing chain, LINK, and TRACK buttons are slightly too high and too far outward)
- Player Herc acceleration/deceleration and turning are framerate-dependent (**fixed** in HERCULAN Engine)
- The "center legs" function moves the turret awkwardly and does not center the legs perfectly.
- Herc stats shown in the VSHELL "Build" screen may be incorrect (the Outlaw is listed as having a top speed of 80 kph, but the manual and in-game readout show 100 kph).
- Some buildings' collision volumes are shorter than the visible mesh, so shots pass through the top of the building.
- Turning with the keyboard turns at half of the speed of turning with the joystick.
- The HUD speed readout does not reflect speed accurately. Hercs have two strides, "walking" and "running". In the walking stride, the Herc moves much more slowly than the speed gauge indicates.
- An ELF or ELF2 shot paints a stray orange-white dash at the muzzle, roughly four pixels wide, where the jagged tracer branch falls through into the straight-beam draw. See [`docs/simulation/beam-visuals.md`](docs/simulation/beam-visuals.md#the-muzzle-stub-is-a-retail-fall-through).
- In the low-memory mode (`-l`, or under 12 MB physical), catalog id `0x22`'s explosion sound fails to open and plays silently — its sample exists only in the `hmi\` bank, not the `hmx\` bank low-memory mode uses. See [`docs/formats/audio.md`](docs/formats/audio.md#sample-banks).
- `Sound_Place`'s pan calculation snaps a sound placed exactly abeam the listener to the wrong side: bearing zero truncates to the hard-*left* value where every neighbouring bearing on both sides yields hard-*right*. Not reproduced in HERCULAN Engine. See [`docs/formats/audio.md`](docs/formats/audio.md#playing-a-sound).
- The Range readout of the MFD TARGET tab (F5) prints raw world units — 1 unit = 6 mm. Every other distance readout in the cockpit converts to metres first (`Hud_WorldUnitsToMetres`).
- A gun mount that has taken damage fires **faster**, not slower: damage steps its refire scale down, and the scale multiplies the refire delay, so a half-wrecked autocannon arms half the delay. Reproduced as-is. See [`docs/simulation/weapon-mounts.md`](docs/simulation/weapon-mounts.md#losing-a-mount).
- Samson: When firing lasers from the two lowest hardpoints, the beams emerge from a little above the barrels. Not sure if this happens with all lasers or if it's hardpoint specific. Only visible in third-person view.
- The Heads-Down Display's command display blinks the wrong map marker for the selected squadmate. HERCULAN Engine blinks the selected pilot's own marker instead. See [`docs/formats/heads-down-display.md`](docs/formats/heads-down-display.md#markers).
- The MFD TARGET screen (F5) can never read `SHIELDS DN` for a HERC target: the shields-down alert latch only the machine the player is flying can ever set, so a HERC reads `OK` however much armour it has lost. Reproduced as-is. See [`docs/formats/mfd.md`](docs/formats/mfd.md#viewport-and-condition-per-class).
- An explosion's damage past a drained shield facing is **four times** its `PROJ.DAT` face value. Reproduced as-is. See [`docs/simulation/damage-system.md`](docs/simulation/damage-system.md#a-mech--mech_applyexplosivedamage-004187d0).
- The effect-light allocator has no full-table guard: with all twenty slots busy, the claim overruns into the caller's own position vector and the manager's embedded light object. Not reproduced in HERCULAN Engine. See [`docs/formats/effect-lights.md`](docs/formats/effect-lights.md#the-allocator-overruns-when-all-twenty-slots-are-busy).
- The impact point of a machine-versus-machine collision mixes two coordinate frames. On level ground near sea level the two nearly agree; on a hill the blast goes off well below the machines, and its falloff reaches their legs rather than their torsos. Reproduced as-is. See [`docs/simulation/damage-system.md`](docs/simulation/damage-system.md#a-collision--mech_collisiontest-00418f74).
- A Razor nacelle strike draws its impact effect at the **left** nacelle whichever nacelle was struck. Reproduced as-is. See [`docs/simulation/razor-flight.md`](docs/simulation/razor-flight.md#contact-probes).

## HERCULAN Engine

_Note to Claude: This section is for listing outstanding issues with features which have been implemented but behave differently than retail or otherwise incorrectly. Features that haven't been tackled yet go in [`ROADMAP.md`](ROADMAP.md). Bugs in retail belong in the previous section. Detailed technical descriptions belong in documentation, not here. Give a short plain-English summary._

- Terrain detail-texture scatter differs from retail. Roughly 30% of 2x2 cell blocks are drawn with the theater bank's frame 1 instead of the plain frame 0, and which blocks is a random roll at zone load. The engine's generator is not seeded like DBSIM's, so the scatter is statistically faithful but lands on different cells. See the SimRandom seed-table entry in [`ROADMAP.md`](ROADMAP.md) for the blocker. Base pads (frames 2–12) are unaffected — those are placed from `BFORMS.DAT`, not rolled.
- Distance fog on terrain is smooth where retail's steps cell by cell. The amount of fog and the ground it lands on match; the flat step across each cell is missing. See "Engine port" in [`docs/formats/distance-fog-and-sky.md`](docs/formats/distance-fog-and-sky.md).
- `TSGouraudPoly` faces fade by an RGB blend toward the fog colour rather than by a ramp depth slice. Every other surface fogs through the ramp as retail does. Same section as above.
- Gouraud-shaded structures band differently from retail on some geometry, e.g. the type-15 octagonal tower: retail's wide dark bands are not reproducible under the engine's current shading mechanism, and the cause is unresolved. See "Unresolved: type-15 band widths" in [`docs/formats/dts-texture-binding.md`](docs/formats/dts-texture-binding.md#unresolved-type-15-band-widths). Reference captures: `Reference/Gouraud_shading_comparison_2.png`, `Reference/Scramble_Training_Base_4.png`.
- No back-face culling: retail's format has an explicit back-face skip flag that most of the fleet relies on. See "Front/back visibility test" in [`docs/formats/dts-texture-binding.md`](docs/formats/dts-texture-binding.md#implementation-status).
- A machine's fitted weapon models are drawn on the player's own HERC in the cockpit view, where its hull is not. Retail submits every machine in `GlobalMechList` with no local-player test at all (`maybe_Scene_SubmitFrameObjects`, `0042841c`); leaving the hull out is this engine's own device for keeping the torso from wrapping the camera, and the guns hang outside it.
- Lighting on HERCs may not be correct (needs review) — re-check: HERCs are almost entirely `TSShadedPoly`, so the shade-ramp and away-facing-light fixes changed their colouring too.
- When projectiles hit buildings, many hit effects seem to clip inside the building - I don't observe this in retail. **Hit geometry ruled out**, and **building LOD ruled out** (the engine draws maximum detail, which is what retail's screenshots were taken at). Most likely a draw order issue.
- Claude used a separate camera for each cockpit panel, which causes a visible distortion in the side panels, particularly when looking downward at all. Will probably need to replace with a single camera covering the full width of the window (remember to maintain the same vertical FOV as the original game!). Or maybe we can crop the camera views by window frame rather than square, so that the seam isn't visible...
- Due to nearest-neighbor scaling, text often looks bad when window height isn't an integer multiple of 240.
- Currently missing is a quirk from retail where the player's shield meter fills in over ~10 seconds at the start of a mission. Claude says there's no explanation for this in the shield code, where the shields start out at full charge, and would take ~30 seconds to fully charge from empty. The fade-in-over-10-seconds may be a HUD animation that hasn't been discovered during RE yet.
- Similarly to the previous, currently missing is an animation where weapon buttons wink on one-at-a-time when the simulation first starts.
- In the Scramble practice mission while piloting an Apocalypse, a Particle Beam Weapon is equipped to slot 8. In HERCULAN, when this PBW is fired the beam visibly clips off near the corner of the screen. This may be a camera near-clip plane issue.
- Targeting range seems to be much lower than retail, even with active radar. Possibly because there's currently no AI, so the enemies never switch their radar on.
- The preferences screen's four-way OFF / TEXT ONLY / VOICE ONLY / TEXT-VOICE setting for each message channel has no UI: the message port is always on both halves. See "The port" in [`docs/formats/audio.md`](docs/formats/audio.md#the-computers-messages).
- The message port draws whenever it has a line, where retail gates the display half on two further fields that aren't decoded. See "The port" in [`docs/formats/audio.md`](docs/formats/audio.md#the-computers-messages).
- The pilot and squad message channel — the message port's second instance, which wraps several lines instead of scrolling one — is not ported, and nothing posts to it. It needs squadmate speech, which is not ported either. See [`ROADMAP.md`](ROADMAP.md).
- The Heads-Down Display's squad comm boxes read stand-in state. See "Engine coverage" in [`docs/formats/heads-down-display.md`](docs/formats/heads-down-display.md#engine-coverage).
- The `[V]` external view is placeholder rather than retail's. See [`ROADMAP.md`](ROADMAP.md).
- The `[P]` pause is a placeholder that just stops the fixed-timestep tick loop. See [`ROADMAP.md`](ROADMAP.md).
- I think computer voiceover ("Powerup initiated", "Active radar mode", etc) is much higher-fidelity than in retail, but I can't get sound working in my retail copy on Windows 11 so I'm unable to check. The voiceover may be downsampled in retail. Will investigate later and maybe eventually add a vanilla setting.
- A plasma round's blast damage is not scaled by mission difficulty. See [`docs/simulation/projectiles.md`](docs/simulation/projectiles.md#the-plasma-branch).
- Steering a Razor from the keyboard uses hardcoded placeholder keys. See [`docs/simulation/razor-flight.md`](docs/simulation/razor-flight.md#the-keyboard).
