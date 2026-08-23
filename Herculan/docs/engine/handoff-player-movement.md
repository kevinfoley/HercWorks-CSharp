# Handoff — HERCULAN player movement

RE references: [`docs/simulation/mech-locomotion.md`](../simulation/mech-locomotion.md),
[`docs/formats/cockpit-hud.md`](../formats/cockpit-hud.md) (throttle gauge),
[`docs/formats/cockpit-input.md`](../formats/cockpit-input.md) §7 (drag capture),
[`docs/formats/dts-node-posing.md`](../formats/dts-node-posing.md) (node-posed geometry),
[`docs/simulation/torso-aim.md`](../simulation/torso-aim.md) (turret twist and pitch).

## Shipped

| File | Contents |
|---|---|
| `Numerics/SimTrig.cs` | DBSIM cos/atan2/asin tables, with its 1/4096-turn quantization |
| `Numerics/Transform3.cs` | 0x20-byte Q14 transform (`0047eaac`, `0047f914`, `00480330`, `0047f894`) |
| `Sim/Anim/ShapeAnimation.cs`, `AnimationThread.cs` | Animation thread, root-motion accumulator, transition search, per-node pose lookup, `SeekToPosition` |
| `Sim/Anim/ShapeInstance.cs` | The shape's three threads, and the node poses they produce together |
| `Sim/MechObject.Torso.cs` | Turret twist and pitch (`0041a550`, `0041a808`), centring (`0041e8d4`) |
| `Sim/MechTypeRecord.cs` | Load-time speed rescale, on top of a now-correctly-named `HercSimDat` |
| `Sim/MechObject.cs`, `MechObject.Locomotion.cs`, `MechControls.cs` | Throttle input, control law, gait state machine, root motion, collision, steep-ground slide, cockpit eye |
| `Content/ThrottleTrack.cs` | Throttle slider geometry — value to knob position and back |
| `Content/CockpitWidgets.cs`, `Input/CockpitInput.cs` | Draggable widgets and pointer capture |
| `Render/DtsMeshBuilder.cs`, `WorldScale.cs`, `Scene/MissionScene.cs` | Per-node geometry segments, `Transform3` to render matrix, posed placement (`004758c8`, `00476030`) |
| Host `Program.cs` | Arrow-key piloting, turret keys, throttle/slider binding, cockpit camera, one draw per posed node, `--throttle`, `--external`, `--turret` |

Tests: `MechLocomotionTests`, `MissionWalkTests`, `ThrottleGaugeTests`, `CockpitInputTests`,
`SkeletonPoseTests`, `Transform3Tests`, `SimMathTests`. 270 pass. Node posing and the turret added
none — their verification was one-off measurement, recorded in dts-node-posing.md and torso-aim.md.

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
- **Node poses use full `Transform3` composition**, including rotation, unlike `DtsMeshBuilder`'s
  load-time offset sum. The two agree exactly at rest — no retail HERC node carries a rest rotation
  — so the composition order is not scrambled.
- **A posed machine drops the `BaseOffset` lift and gains its lean**, both of them the simulation
  being let through rather than approximated. See
  [`dts-node-posing.md`](../formats/dts-node-posing.md).
- **The cockpit camera's orientation comes off the eye node**, not the machine's heading, which is
  what makes the turret turn the view. Checked first that the walk cycle rotates the eye by zero, so
  it adds the turret and nothing else — see [`torso-aim.md`](../simulation/torso-aim.md).
- **A contested node goes to the first-registered thread**, read off
  `ShapeInst_EvalAllNodeLocals`'s backwards iteration rather than stated anywhere. It only matters to
  HEADHUNT, whose twist node its walk cycle also drives.
- **The throttle's vtable slots** were read off its constructors, not dumped from the vtable — see
  cockpit-input.md's Open list.

## Next

- The throttle's two LED fill bars are decoded but not drawn; their colour ids were not traced.
- The gauge's `+0xb1` speed-fraction value: written every frame, stored by the slider child, but
  nothing was found that moves anything with it.
- Automatic Turret Tracking ([T]) and AI turret aiming — both need target selection first. Then
  Center Body (`\`), the turret servo sound, and the HUD's turret rotation indicator; see
  [`torso-aim.md`](../simulation/torso-aim.md)'s "Not ported".
- Damage terms in the control law (all exactly zero at full health), `mech+0x317` identity.
- AI obstacle avoidance (`00416274`), Razor flyer movement.

## Debug view (2026-08-22)

`Esc` opens a debug panel in the simulator host (ImGui, as the editor host uses; `Esc` no longer
quits). Two toggles and a set of animation readouts:

- **Draw skeleton** — every transform of the player's shape sampled through
  `ShapeInstance.NodeTransform`, drawn as bones plus a joint cross, depth-test off. The camera node
  is flagged in its own colour. Built on `SkeletonPose` (sim) + `SkeletonWireframe` (render). It
  shows the nodes no geometry hangs from, and overlaid on a posed machine (`V`) it is the check that
  the pose the simulation holds and the pose being drawn are the same one.
- **Steady eye** — pins the eye's height to whatever it was when the toggle went on, leaving travel,
  lean and fore/aft swing alone. A/B for whether the complaint is the eye or the machine.
- Readouts: sequence/frame/target/rate, root-motion flag, posed node count, turret twist and pitch
  with the angle actually drawn beside each (they differ, see torso-aim.md), throttle, speed, gait,
  step per frame, position, heading, lean, ground clearance, and eye rise with its running min/max
  swing (retail stride is 0.24–0.42 m).

`SkeletonPoseTests` pins the invariant that makes it evidence: the camera joint equals
`MechObject.EyePosition` exactly, for all 18 retail HERCs mid-stride, and sampling perturbs nothing.

## Host controls

Arrow keys or keypad 4/6/8/2 steer and throttle — hold Down through zero for reverse. Keypad 5 all
stop. `J`/`K` twist the turret, `I`/`M` pitch it, `Backspace` re-centres it — the manual's own
keyboard turret set, and the cockpit view looks where the turret points. Mouse drags the console
throttle slider. `C` toggles the observer camera, `V` the external chase view (placeholder geometry,
not RE'd — see `ExternalCamera`), `Esc` the debug panel.

For `--screenshot` runs that never see a keystroke: `--throttle <n>` powers up with the throttle
preset, `--turret <twist> <pitch>` holds the two turret axes (±256 each), and `--external` starts in
the external view — the only view that shows the player's own machine, and so the only one its legs
are visible in.

## Environment

Ghidra project `E:\ES2Stuff\tools\ghidra_project\ES2Recon`. Grep
`tools/analysis_out/DBSIM_full_decomp.txt` (pre-rename, use addresses) rather than re-running
headless Ghidra. `known_symbols.json` is current and has been applied to the project
(`ES2ApplySymbolNames.java`, most recently 5 renamed / 3 labeled for the node-posing pass).
