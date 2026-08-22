# Handoff — HERCULAN player movement

RE references: [`docs/simulation/mech-locomotion.md`](../simulation/mech-locomotion.md),
[`docs/formats/cockpit-hud.md`](../formats/cockpit-hud.md) (throttle gauge),
[`docs/formats/cockpit-input.md`](../formats/cockpit-input.md) §7 (drag capture).

## Shipped

| File | Contents |
|---|---|
| `Numerics/SimTrig.cs` | DBSIM cos/atan2/asin tables, with its 1/4096-turn quantization |
| `Numerics/Transform3.cs` | 0x20-byte Q14 transform (`0047eaac`, `0047f914`, `00480330`, `0047f894`) |
| `Sim/Anim/ShapeAnimation.cs`, `AnimationThread.cs` | Animation thread, root-motion accumulator, transition search, per-node pose lookup |
| `Sim/MechTypeRecord.cs` | Load-time speed rescale, on top of a now-correctly-named `HercSimDat` |
| `Sim/MechObject.cs`, `MechObject.Locomotion.cs`, `MechControls.cs` | Throttle input, control law, gait state machine, root motion, collision, steep-ground slide, cockpit eye |
| `Content/ThrottleTrack.cs` | Throttle slider geometry — value to knob position and back |
| `Content/CockpitWidgets.cs`, `Input/CockpitInput.cs` | Draggable widgets and pointer capture |
| Host `Program.cs` | Arrow-key piloting, throttle/slider binding, cockpit camera, `--throttle` |

Tests: `MechLocomotionTests`, `MissionWalkTests`, `ThrottleGaugeTests`, `CockpitInputTests`,
`Transform3Tests`, `SimMathTests`. 233 pass.

**Verified without fitted parameters:** predicted top speed / HUD readout = **0.83–1.05** across all
18 HERCs. Turn-in-place 26.9°/s vs. the reference doc's 27.3. Cockpit eye height 3.2–11.8 m against
the manual's 6.1–10.4 m quoted statures. Throttle knob position matched against
`Screenshots/Simulator1.jpg` at all three positions (centre stopped, top forward, bottom reverse).

## Corrections this pass made

1. **`DAT_0049a06e` is not a gear selector.** `Input_SetThrottleLeverMode` (`00459d20`) settles it:
   it is the joystick throttle-lever mode, 0 when no lever is configured. At 0 the throttle clamp in
   `Mech_ApplyThrottleInput` spans the full ±0x400, which is what lets the keyboard run through zero
   into reverse. The previous pass ported it as a gear and so **could not reverse at all**.
   `MechControls.Gear` → `ThrottleLever`; the host's `[X]` binding is gone; the symbol table and
   mech-locomotion.md are corrected.
2. **`HThrottle`'s "four detent points" are two rects** — the forward and reverse LED fill bars,
   handed to `LedBarGraph_CtorV` with ranges ±0x400. The gauge reads the block from `.GAU` offset
   **1000**, not 1016. See cockpit-hud.md's throttle section.
3. **Drag capture exists.** cockpit-input.md §7 previously stated no retail widget is draggable; the
   slider base `004524a8` sets the `+0x1d` flag, and the throttle is built through it.
4. **`CameraBoneId` is a shape *part* id**, resolved to a transform id through the part's
   `TSBasePart.Transform`. The eye now rides it instead of a `boundingBox × 0.85` estimate, which had
   OUTLAW's eye about 3 m too high.
5. **`HercSimDat` field renames** — `AnimId_TorsoPitch`→`AnimId_StopReverse`,
   `Unk44_MoveAnimRate`→`GaitThreshold`, `LegsCritFlags1`→`AnimId_Death`,
   `Unk108_camExtVal1`→`GaitThresholdReverse`, `Unk122_mdlFlagVal`→`AnimId_TurnInPlace`,
   `PhysicsFrictionCoef`/`PhysicsFrctionAccel`→`StrideScaleDivisor`/`StrideScaleNumerator`.
   `LegsCritFlags2` (offset 70) keeps its old name — nothing traced reads it either way.
6. **`.GAU` offset 1072 identified** as the throttle centre-tick x nudge, previously recorded as
   "read by the throttle constructor and left unexplained".

Carried over from the first pass and still standing: `SimWorld.TickDelta` 81 @ 25 Hz,
`MissionScene.TransformOf`'s heading sign, `HeightGrid.DiagonalSelectorAt`'s writer, a mech powering
up in its stop sequence.

## Decisions taken

- **Frame-rate model:** fixed 25 Hz tick, decoupled from rendering, `TickDelta` pinned at 81 — see
  [`dbsim-physics-notes.md`](../simulation/dbsim-physics-notes.md#fixed-point-math-toolkit) for the
  RE'd `FUN_004677bc` formula this matches.
- **Accel/decel timing (deviation):** `SpeedAccelDecel`/`DecelTurning` go through
  `SimMath.ScalePerTickStep` instead of the original's unscaled per-tick steps — see
  [`mech-locomotion.md`](../simulation/mech-locomotion.md#timing) for the deviation detail and the
  pinned-to-1 caveat.
- **Player's own mech is not drawn in the cockpit view.** The eye node is well inside the torso, so
  its geometry would fill the canopy. Taken from observed retail behaviour — the exclusion was *not*
  traced in DBSIM's own submit path.
- **Node poses use full `Transform3` composition**, including rotation, unlike `DtsMeshBuilder`,
  which sums translations only and documents rotation as unverified. Cross-checked: the two agree on
  eye height to a fraction of a metre, so the composition order is not scrambled.
- **The throttle's vtable slots** were read off its constructors, not dumped from the vtable — see
  cockpit-input.md's Open list.

## Next

- Leg/torso node posing in the renderer.
- The throttle's two LED fill bars are decoded but not drawn; their colour ids were not traced.
- The gauge's `+0xb1` speed-fraction value: written every frame, stored by the slider child, but
  nothing was found that moves anything with it.
- Torso twist (`0041a550`) and pitch (`0041a808`) — sequences 0 and 5, nodes 4 and 11, which are two
  of the three nodes in the camera chain. Porting them will move the eye.
- Damage terms in the control law (all exactly zero at full health), `mech+0x317` identity.
- AI obstacle avoidance (`00416274`), Razor flyer movement.

## Debug view (2026-08-22)

`Esc` opens a debug panel in the simulator host (ImGui, as the editor host uses; `Esc` no longer
quits). Two toggles and a set of animation readouts:

- **Draw skeleton** — every transform of the player's shape sampled through
  `AnimationThread.NodeTransform`, drawn as bones plus a joint cross, depth-test off. The camera node
  is flagged in its own colour. This is the only view of the animation system there is: the mesh is
  baked at the rest pose, so nothing else on screen moves when a cycle plays. Built on
  `SkeletonPose` (sim) + `SkeletonWireframe` (render); needs none of the per-node render path, and in
  particular none of `ResolveGroupOffset`'s unapplied rotation, since the runtime thread's rotations
  are already there.
- **Steady eye** — pins the eye's height to whatever it was when the toggle went on, leaving travel,
  lean and fore/aft swing alone. A/B for whether the complaint is the eye or the machine.
- Readouts: sequence/frame/target/rate, root-motion flag, throttle, speed, gait, step per frame,
  position, heading, lean, ground clearance, and eye rise with its running min/max swing (retail
  stride is 0.24–0.42 m).

`SkeletonPoseTests` pins the invariant that makes it evidence: the camera joint equals
`MechObject.EyePosition` exactly, for all 18 retail HERCs mid-stride, and sampling perturbs nothing.

## Host controls

Arrow keys or keypad 4/6/8/2 steer and throttle — hold Down through zero for reverse. Keypad 5 all
stop. Mouse drags the console throttle slider. `C` toggles the observer camera, `V` the external
chase view (placeholder geometry, not RE'd — see `ExternalCamera`), `Esc` the debug panel.
`--throttle <n>` powers up with the throttle preset, for `--screenshot` runs that never see a
keystroke.

## Environment

Ghidra project `E:\ES2Stuff\tools\ghidra_project\ES2Recon`. Grep
`tools/analysis_out/DBSIM_full_decomp.txt` (pre-rename, use addresses) rather than re-running
headless Ghidra. `known_symbols.json` is current and has been applied to the project
(`ES2ApplySymbolNames.java`, 14 renamed / 2 labeled this pass).
