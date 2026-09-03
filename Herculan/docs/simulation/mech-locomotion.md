# Herc locomotion — throttle, steering, and animation root motion

Reverse-engineered from `DBSIM.EXE` (`mechsys.cpp`) in the `ES2Recon` Ghidra project. Covers
ground Hercs only. The Razor (`typeRec+0x50 != 0`) takes different paths throughout — a different
control law, a different move and a real velocity vector; see
[`razor-flight.md`](razor-flight.md).

**Core fact: Hercs have no velocity vector.** All translation and all turn-in-place rotation come
from the walk/run/turn animations' root-node motion. The control law only sets a speed scalar,
a turn rate, and an animation playback rate.

## Call graph

| Address | Name | Role |
|---|---|---|
| `00460764` | `Sim_PollPlayerInput` | Reads device axes, dispatches player control |
| `0045fdac` | `Sim_DispatchCommand` | Keyboard command dispatch, by scancode |
| `00415498` | `Mech_GetSpeed` | Mech vtable `+0x38`: `Q10(2000, mech+0x28e)`, or `mech+0x2bd` for a flyer |
| `004160dc` | `Mech_ApplyThrottleInput` | Stick/key throttle → `mech+0x290`, computes desired speed |
| `00416a04` | `Mech_LocomotionTick` | Control law: speed, turn rate, animation rate, gait state machine |
| `0041693c` | `Mech_ApplyTerrainSlopeToSpeed` | Uphill/downhill speed modifier |
| `00416274` | `Mech_AiObstacleAvoidance` | AI only — skipped when `mech == DAT_004a9c08` (player) |
| `0041a360` | `Mech_MovementTick` | Per-tick physics: integrate, terrain-clamp Z, collide |
| `00418f40` | `Mech_IntegrateMotion` | Steps animation, applies root motion |
| `0040250c` | `SimObject_ApplyRootMotion` | Root-motion → world position/heading |
| `00402628` / `004027fc` | `SimObject_PushTransform` / `PopTransform` | Save/restore full transform incl. node hierarchy |
| `00418f74` | `Mech_CollisionTest` | Returns nonzero on blocked move |
| `004195c8` | `Mech_PlaceLegsOnGround` | Per-leg terrain placement |
| `0041a550` / `0041a808` | `Mech_TorsoTwistTick` / `Mech_TorsoPitchTick` | Turret aim, not locomotion — [`torso-aim.md`](torso-aim.md) |

`Mech_MovementTick` is dispatched from `Sim_MainTick`'s object loop via mech vtable `+0x18`
(`0049a29a` → `00415b38`), which forwards through the per-mech-type behaviour struct at `+0x18`.
That struct is 0x24 bytes; `0041a360`'s pointer appears at 18 sites of stride 0x24 in `.data`
starting `00499928`. Ghidra reports zero xrefs for it — the table is unmarked data.

## Mech instance fields

| Offset | Type | Meaning |
|---|---|---|
| `+0x0c/0x0e/0x10` | short×3 | Euler angles; `+0x10` is heading (yaw) |
| `+0x12` | 20 B | World rotation matrix (Q14), translation at `+0x26` |
| `+0x26/0x2a/0x2e` | int×3 | World position X/Y/Z |
| `+0x32` | short | Rotation-matrix-dirty flag |
| `+0x34` | ptr | `TSShapeInstance` |
| `+0x1f2` | ptr | Mech type record (`MECH_TYPE_DATA[i]`) |
| `+0x22c` | ptr | Animation thread (`mech[0x8b]`) |
| `+0x28c` | short | Current turn rate (per tick) |
| `+0x28e` | short | Current speed scalar |
| `+0x290` | short | Throttle setting, Q10, clamped ±0x400 |
| `+0x230/0x234` | ptr | Turret twist / pitch animation threads |
| `+0x294/0x298` | short | Turret twist rate / angle |
| `+0x296/0x29a` | short | Turret pitch rate / angle |
| `+0x2a0` | short | Animation playback rate (Q8 multiplier) |
| `+0x93` | byte | Throttle-dirty flag (input changed it this frame) |
| `+0x317` | ptr | Turbo Pod mount (id 31); adds a speed bonus that degrades with damage |

Angles are 16-bit binary angle measure: **65536 = 360°**. Confirmed by a full-sweep animation
(`OUTLAW` seq 5) stepping `0, 8190, 16380, 24570, 32760, -24570, -16380, -8190` = 8 × 8190 ≈ 65536,
and by turn-in-place keyframes of 1820 = 10.00°.

World scale is 166.667 units/metre (see `docs/engine/planning.md`).

## Mech type record

Loaded by `MechType_InitOne` (`004201a8`) as a 216-byte little-endian record into
`MECH_TYPE_DATA[i]+2`. **Record offset N = `typeRec+N+2`.** Parsed in C# by
`HercSimDataTransformer`, which expects `VolEntry.RawBytes` (the 9-byte VOL prefix already
stripped).

| rec | typeRec | C# field | Meaning |
|---|---|---|---|
| 0 | `+0x02` | `SpeedTurn` | Max turn rate (not rescaled at load) |
| 2 | `+0x04` | `SpeedReverse` | Max reverse speed (negative) |
| 4 | `+0x06` | `SpeedForward` | Max forward speed |
| 6 | `+0x08` | `SpeedAccelDecel` | Linear accel step, per tick, **not** dt-scaled |
| 8 | `+0x0a` | `DecelTurning` | Turn-rate accel step, per tick, **not** dt-scaled |
| 10 | `+0x0c` | `CameraBoneId` | Node the cockpit eye rides |
| 12 | `+0x0e` | `AnimId_Walk` | Walk sequence id |
| 14 | `+0x10` | `AnimId_Run` | Run sequence id |
| 16 | `+0x12` | `AnimId_StopMove` | Stop/step-off sequence, forward |
| 18 | `+0x14` | `AnimId_StopReverse` | Stop/step-off sequence, reverse |
| 20 | `+0x16` | `UnitOffsetYAdjust` | Ride height added to terrain height |
| 26–32 | `+0x1c`–`+0x22` | `AnimId_TorsoTwist`, `TorsoTwist*` | Turret twist sequence, rate, accel, limit |
| 34–42 | `+0x24`–`+0x2c` | `AnimId_TorsoPitch`, `TorsoPitch*` | The same for pitch — see [`torso-aim.md`](torso-aim.md) |
| 44 | `+0x2e` | `GaitThreshold` | Walk↔run threshold speed |
| 68 | `+0x46` | `AnimId_Death` | Death/fall sequence id |
| 78 | `+0x50` | `InputFlagFlyer` | 1 = Razor. Selects the flight paths and the `fm\<NAME>.FM` load — [`razor-flight.md`](razor-flight.md) |
| 84 | `+0x56` | `Unk84_val` | Whether a hit can knock this chassis' weapon mounts out — 1 on every biped, **0 on the PITBULL**. `Mech_ApplyDirectFireDamage` tests it before rolling; see [`damage-system.md`](damage-system.md#weapon-mount-destruction) |
| 108 | `+0x6e` | `GaitThresholdReverse` | Reverse-side walk↔run threshold |
| 122 | `+0x7c` | `AnimId_TurnInPlace` | Turn-in-place sequence id |
| 194 | `+0xc4` | `StrideScaleDivisor` | Stride calibration divisor |
| 196 | `+0xc6` | `StrideScaleNumerator` | Stride calibration numerator |
| — | `+0xc2` | — | HUD scale, set at load to `Q10(315 × rawSpeedForward)` |

### Load-time speed rescale

`MechType_InitOne` rescales four speed fields for non-flyers:

```
scale  = Q16Divide(rec196 × 400, rec194)
typeRec+0x04, +0x06, +0x2e, +0x6e  ×= scale     (Q16)
typeRec+0x02 (turn rate) is NOT rescaled
typeRec+0xc2 = Q10(315 × rawSpeedForward)       computed BEFORE the rescale
```

`scale` normalises the designer's speed points to the model's stride length: `simMax ×
stridePerTick` tracks `rawSpeedForward` across every Herc (see verification below). It is not
friction.

The HUD reads `speed × typeRec[0xc2] / typeRec[0x06]`, so `simMax` cancels and top speed always
displays `315 × rawSpeedForward / 1024` regardless of scale.

## Control law (`Mech_LocomotionTick`)

Speed:

```
throttle += Q8(0x91, -stickAxis)                  // 0.566/tick, clamp ±0x400
desired   = Q10(throttle < 0 ? maxRev : maxFwd, throttle)
desired  += slopeTerm                             // dot(terrainNormal, forward) / 2400
desired   = clamp(desired, maxRev, maxFwd)
RateLimitedMoveToward(speed, desired, SpeedAccelDecel)
```

`DAT_0049a06e` is **not** a gear selector. `FUN_00459d20` sets it to 1 only when the input
configuration reports a throttle control *and* the preferences page has that control assigned to
THROTTLE rather than TURRET, and to 0 otherwise; the key command and the cockpit slider that
"toggle" it only ever flip between +1 and −1, gated on that same pair. It selects the **joystick
throttle-lever mode**: 0 = none, ±1 = lever present, sign inverting its sense.

It matters because it is what gates the throttle clamp. At 0 — keyboard and plain stick — the range
is the full ±0x400, so holding the axis against its stop runs the setting from full forward through a
one-tick pause at zero and on into full reverse. That one-tick pause is the sign-crossing guard, and
it is the manual's "Centered is stopped". Non-zero also switches the handler's first block on, which
reads the axis as an absolute lever position (`|axis − 0x100| × 2`, deadbanded below 100) instead of
as a rate.

Throttle is two-way bound to the cockpit ThrottleGauge in `Player_PerFrameCockpitUpdate` (`0041b130`),
arbitrated by the `mech+0x93` dirty flag — this is why dragging the slider works.

Turn rate — a symmetric tent over speed, `T = SpeedTurn`:

```
if (inStopAnim || speed == 0) turnBase = 0
else {
    s = clamp(|speed| bumped to min 45, 45, maxFwd);  H = (maxFwd - 45) / 2
    turnBase = (s <= 45+H) ? T·(s-45+H)/(2H)
                           : T - T·(s-45-H)/(2H)
}
turnTarget = Q8(Q10(1600, turnBase), stickAxis)    // stick clamped ±0x100
RateLimitedMoveToward(turnRate, turnTarget, DecelTurning)
heading += turnRate
```

Half turn rate at crawl, peak at half top speed, half again at top speed. `Q16Divide(0x32, 0x32)`
in that branch is a dead constant (always 1.0).

**Turning in place is not produced here** — at zero speed `turnBase` is 0. The turn-in-place
branch only sets the animation rate to `Q10(350, stickAxis)`; the rotation comes from the
turn-in-place sequence's root rotation.

The remainder of `Mech_LocomotionTick` (~60% of its body) is the gait state machine, switching
between sequences `AnimId_Walk` / `AnimId_Run` / stop-forward / stop-reverse / turn-in-place /
death and maintaining `mech+0x2a0`. In steady state `animRate = speed`.

## Center Body

The manual's other half of [Backspace]: instead of bringing the turret back to the legs, it walks the
legs round under the turret. Scancode `0x2b` (`Sim_DispatchCommand`, `0045fdac`, and the identical case
in `Sim_PollPlayerInput`) latches `g_CenterBodyMode` (`004d2af4`), clears `g_CenterTurretMode` and the
ATT flag, and caches

```
g_CenterBodyTargetHeading = heading - Mech_GetTorsoTwistAngle()    // 004d2af8, short
```

— the world direction the turret is pointing in. While latched, the player's input block substitutes
its own steering and twist axis; the throttle and the pitch axis still come from the pilot.

```
bodyError   = heading - target                       // legs still to turn
turretError = (heading - twistAngle) - target        // turret drifted off the direction
steer = sign(a) x (a² >> 8),  a = Q10(100, bodyError)
twist = sign(b) x (b² >> 8),  b = Q10(0x46, turretError)
if (a² >> 8 < 0x1e && b² >> 8 < 10)  g_CenterBodyMode = 0
if (Mech_GetSpeed() < 0)  steer = -steer
Mech_ApplyThrottleInput(mech, steer, throttleAxis)
Mech_TorsoTwistTick(mech, twist);  Mech_TorsoPitchTick(mech, pitchAxis, range)
```

All 16-bit arithmetic, so both errors wrap. Squaring the gained error makes the command soft near the
target and hard away from it, which is what stops the legs hunting; the sign is put back afterwards.
Both terms reach their thresholds together, since heading meeting the target forces the twist to zero.
The extra inversion on `Mech_GetSpeed` (mech vtable `+0x38`, `00415498` — `Q10(2000, mech+0x28e)`)
sits on top of the one `Mech_ApplyThrottleInput` already does from the stick, so reversing steers the
right way.

The mode is not cancelled by steering or by the turret axes — only by its own convergence test or by
[Backspace]. It leaves a few degrees of residual twist, by design: it is not a centring command.

## Timing

Tick rate, the `SimTickDelta`/`DAT_004d3be8` formula (`FUN_004677bc`), and its Q8/125ms scale are
documented in [`dbsim-physics-notes.md`](dbsim-physics-notes.md#fixed-point-math-toolkit) — not
repeated here.

Locomotion accel constants (`SpeedAccelDecel`, `DecelTurning`) are raw per-tick steps with no
`Math_IntegrateRateOverTick`, so **the control law is frame-rate dependent**. The animation
advance and the torso rates *are* dt-scaled.

> Port note: Herculan does not reproduce this. Both constants go through
> `SimMath.ScalePerTickStep` (`step x TickDelta / 81`), exact at the original's own 40 ms tick.
> Below the vanilla tick length, a step that rounds to zero is pinned to 1 — re-check these
> constants if the engine's tick rate is ever raised above 25 Hz.

## Root motion

`SimObject_ApplyRootMotion` (`0040250c`), called once per tick from `Mech_IntegrateMotion`:

```c
setRootTransform(shape, IDENTITY);   // FUN_00478a70
advanceAnimation(shape, dt);         // FUN_00478c2c, dt = Q8(SimTickDelta, 100)
delta = shape->nodeWorldTransforms[0];
pos   = objRotationMatrix × delta.translation + pos;   // FUN_00480330
euler += eulerOf(delta.rotation);                      // FUN_0047f894
setRootTransform(shape, IDENTITY);
```

Per-frame ground movement is loaded on every frame advance by `FUN_00478de8`:

```c
seq = animList->Sequences[seqId];
if (seq->groundMovementFlag == 0) thread.groundMoveFlag = 0;
else {
    G = animList->Transforms[ seq->transformIndices[frame * numParts] ];   // part index 0 = root
    thread.groundTrans = G.translation;   // thread+0x22/0x24/0x26
    thread.groundRot   = G.rotation;      // thread+0x28/0x2a/0x2c
    thread.groundMoveFlag = 1;            // thread+0x20
}
```

`ANSequence.GroundMovement` is the enable flag. `ANAnimListTransition.GroundMovement` is a
*different* field — a gait-change hook used only when switching sequences, not the steady gait.

Application is a matched set around the fraction `thread+0x1c / thread+0x1e`
(intra-frame accumulator ÷ frame duration):

| Function | Effect |
|---|---|
| `00478fa8` | read: returns `scale(G, frac) ∘ stored` |
| `00479088` | write: `stored = scale(G, frac)⁻¹ ∘ incoming` |
| `00478e60` | frame exit: commits full `G` into `stored` |
| `00478ee8` | inverse of `00478e60`, for backward playback |

Seeding the root to identity then reading back yields
`scale(G, frac_after) ∘ scale(G, frac_before)⁻¹` — the exact delta for that tick. Over one full
frame the Herc advances by exactly `G`, ramped linearly.

**Axes:** +Y is forward in model space (matches `Mech_ApplyTerrainSlopeToSpeed`, which builds the
forward vector as `(0, speed, 0)`); root rotation Z is yaw.

### Resulting speed

```
animTicksPerSec = 3.125 × animRate
        because  dt        = Q8(SimTickDelta, 100) = 0.8 × elapsedMs
                 advance   = dt × animRate / 256           (FUN_00479614)
                 per sec   = 1000 × 0.8 × animRate / 256

worldSpeed = 3.125 × speed × (ΣG_cycle / Σticks_cycle)     world units/sec
```

Frame-rate independent — `elapsedMs` cancels.

Because `G` varies frame to frame (OUTLAW walk: 150, 240, 170, 240, 80, 380 …), world speed
**pulses with each footfall**, up to 4.75× between the slowest and fastest frame of a stride.
Averaging `G` over the cycle loses that.

### Verification

Predicted run-gait top speed vs. the HUD reading, all Hercs, no fitted parameters:

| Herc | rawMax | scale | simMax | walk u/tick | run u/tick | pred km/h | HUD km/h | pred/HUD |
|---|---|---|---|---|---|---|---|---|
| OUTLAW | 325 | 0.851 | 276 | 2.092 | 5.080 | 94.6 | 100.0 | 0.947 |
| RAPTOR2 | 215 | 0.976 | 209 | 2.862 | 5.025 | 70.9 | 66.1 | 1.072 |
| TOMAHAWK | 240 | 1.180 | 283 | 2.025 | 4.140 | 79.1 | 73.8 | 1.071 |
| SAMSON | 190 | 1.078 | 204 | 2.200 | 4.460 | 61.4 | 58.4 | 1.051 |
| COLOSSUS | 180 | 0.911 | 164 | 2.325 | 5.140 | 56.9 | 55.4 | 1.028 |
| APOCA | 200 | 0.497 | 99 | 3.800 | 8.472 | 56.6 | 61.5 | 0.920 |
| OGRE | 190 | 0.874 | 166 | 2.775 | 5.375 | 60.2 | 58.4 | 1.030 |
| MAVERICK | 285 | 0.976 | 278 | 2.862 | 5.025 | 94.3 | 87.7 | 1.076 |
| SCARAB | 180 | 1.070 | 192 | 1.833 | 3.840 | 49.8 | 55.4 | 0.899 |

Full 18-Herc run: all within 0.899–1.076, mean ≈ 1.00. Four independent quantities must be
correct for this to hold — root-motion model, the 3.125 tick constant, the load-time rescale, and
the 166.667 units/m world scale. APOCA is the tightest constraint: stride 8.472 u/tick (largest)
against scale 0.497 (smallest); without the rescale it is 2× wrong.

### Turn-in-place

Uniform across every Herc: 1820 units (10.00°) per frame, 7 frames, 100 ticks/frame = **70° per
700-tick cycle**, zero translation. At full stick `animRate = Q10(350, 256) = 87.5`, giving
27.3°/s (180° in 6.6 s). Negative stick plays the sequence backward.

## Walk/run gait discontinuity

Real and universal; confirmed against the retail build.

A run stride is ~2× a walk stride but takes 5/6 the time, and `animRate = speed` in both gaits.
Crossing `typeRec+0x2e` therefore roughly doubles actual ground speed while the HUD number moves
continuously. Per-Herc run/walk u/tick ratio: 1.76–2.43.

The HUD's 315/1024 constant is calibrated for the run gait only. Below the threshold — 50–60% of
the throttle range — a Herc physically moves about half what the readout claims. Reproduce the
mechanism, not the readout.

## Damage effects on movement

Out of scope for the locomotion milestone. Note the first term is **maximal** at full health, not
zero, so omitting it is not neutral on an undamaged machine.

- `mech+0x317` is the **Turbo Pod** (`TURB`, catalog id 31), one of the five equipment-pod slots
  filled by `FUN_0040fb2c` at loadout — see
  [reactor-energy-pool.md](reactor-energy-pool.md#equipment-pods--mech0x307-filled-by-fun_0040fb2c).
  It adds a term to desired speed *in the current direction of travel*, worth ~98% of max at full
  and fading to ~20% before cutting out entirely past 225/256 damage. A speed bonus that degrades,
  not a throttle runaway.
  > Reading the curve requires care: the health accessor returns **accumulated damage**, not health,
  > so the term runs the opposite way to how it first scans. See
  > [damage-system.md](damage-system.md#the-component-damage-system).
- Flat multiplicative penalties of 73% (`Q10 × 750`) and 39% (`Q10 × 400`), gated on damage flags at
  `mech+0x2a`, `+0xa9`, `+0xaa`, `+0xab`. The latter two are the **reactor** damage flags, which cut
  power and mobility together — see
  [reactor-energy-pool.md](reactor-energy-pool.md#reactor-damage-flags).

## Cockpit eye and bob

No dedicated bob code, and none is needed. `typeRec+0x0c` (`CameraBoneId`) is a shape **part** id.
`FUN_0041ef14` resolves it through the shape's find-by-id, takes that part's `TSBasePart.Transform`
as a transform id, and indexes the shape instance's per-node transform array at `shapeInst+0x16`
(`0x20` bytes per entry) — the same array `FUN_00402628` memcpy's `count << 5` bytes of when saving
state for a blocked step. The eye rides a node the walk cycle animates, so the bob falls out of
correct root motion.

Resolution is uniform across the fleet. Every ground HERC lands on the same chain shape, and the
parent links come from the `ANAnimList` relation pairs — the same table `DtsMeshBuilder` already
walked to place geometry:

| | camera part | transform | chain to root |
|---|---|---|---|
| 16 of 18 HERCs | 5 | 11 | 11 <- 4 <- 1 |
| MONGOOSE | 10 | 12 | 12 <- 11 <- 4 <- 1 |
| HEADHUNT | 5 | 12 | 12 <- 5 <- 1 |
| RAZOR | 12 | 1 | 1 (flyer, no animation) |

Node **1** is the one the walk, run, stop and turn sequences animate; 4 and 11 are the turret nodes
sequences 0 and 5 drive (see [`torso-aim.md`](torso-aim.md)). The bob therefore comes entirely from
node 1: with the turret held still, 4 and 11 contribute a fixed offset and no motion at all.

The chain also **rotates** only at 4 and 11. Measured over a full stride on OUTLAW, OGRE, MONGOOSE
and HEADHUNT, the camera node's world orientation does not move — zero yaw, pitch and roll swing —
so a walking machine's view bobs without tilting, and everything the pilot's frame is turned by comes
from the turret.

Measured through the port, on flat ground: standing eye height 3.2 m (STINGRAY) to 11.2 m (SAMSON),
running 4.7 m to 11.8 m (OGRE), against the 6.1-10.4 m statures the manual's HERC specs quote. A
stride swings the eye 0.24-0.42 m. Nothing here is fitted.

## Keyframe interpolation

DBSIM interpolates node poses between keyframes, by the same intra-frame fraction it ramps root
motion with. The pose pipeline is three calls, in `ShapeInstance_StepAnimation` (`00478c2c`):

| Address | Symbol | Role |
| --- | --- | --- |
| `004789a0` | `AnimThread_StepAll` | advances every thread by the timestep |
| `004799a4` | `AnimThread_EvalNodeLocals` | **the interpolator** — writes each node's local transform |
| `00478b58` | `ShapeInst_BuildWorldTransforms` | composes locals up the relation list into `shapeInst+0x16` |

Three arrays on the shape instance: `+0x12` per-node **local** transforms (stride `0xc`), `+0xe`
per-node dirty flags, `+0x16` per-node **world** transforms (stride `0x20`, indexed by transform id —
the array `Mech_TargetRelativeToPilot` and the cockpit eye read).

`AnimThread_EvalNodeLocals`, per animated column:

- reads the transform-pool index at **(sequence, frame)** and at **(nextSequence, nextFrame)** — both
  cursors the thread already keeps;
- identical indices → copy the 12-byte record straight, no blend;
- otherwise → `Anim_BlendKeyframeTransforms` (`00492600`) at
  **`(frameAccumulator * 0x400 + frameDuration / 2) / frameDuration`**, a rounded Q10 fraction — the
  same `thread+0x1c / thread+0x1e` fraction root motion is ramped by (see [Root
  motion](#root-motion)). Pose and ground movement therefore ride one clock, which is what keeps the
  gait smooth at any speed: a slow gait stretches keyframes out in time and the pose keeps moving
  between them instead of stepping.
- columns whose part id is 0 are **skipped entirely**, so transform 0 keeps its default and never
  takes an animated pose. Column 0 of every sequence carries that sequence's root motion, not a pose,
  which is what the skip exists to keep out of the node array. Across all 18 retail HERCs column 0
  holds part id 0 in every sequence, transform 0's parent is -1, and no node's chain reaches it — so
  the skip only matters to a caller that walks *all* transform ids, as a whole-skeleton pose does.

`Anim_BlendKeyframeTransforms` blends a 12-byte record (3 euler shorts, then 3 translation shorts):
rotation along the **shortest arc** (bias by `0x10000`, subtract, fold back when over `0x7fff` — the
same wrap `BinaryAngle.Delta` performs), translation as a truncating lerp on the 16-bit difference,
both `* q10 >> 10`. Fixed point throughout; no float.

`ShapeInst_ExpandRootTransform` (`00478b10`) confirms the local record's layout as
`[eulerX, eulerY, eulerZ, x, y, z]` shorts, and thread field offsets are confirmed here too: `+4`
sequence, `+6` frame, `+8` nextSequence, `+10` nextFrame, `+0x1c` frameAccumulator, `+0x1e`
frameDuration.

> Port note: `AnimTransform.Blend` ports the blend; `ShapeInstance.NodeTransform` /
> `InterpolatedLocal` / `FrameFraction` port the evaluation. The port composes lazily per requested
> node instead of building the whole array, so `ShapeInst_BuildWorldTransforms`'s dirty-flag
> machinery has no counterpart and needs none. `ShapeInst_BuildWorldTransforms`'s output array is
> what geometry is drawn through — see
> [`dts-node-posing.md`](../formats/dts-node-posing.md).

### Evaluation cadence — per tick, not per rendered frame

`ShapeInstance_StepAnimation`'s **only** caller is `SimObject_ApplyRootMotion` (`0040250c`), which
`Mech_IntegrateMotion` runs once per sim tick. So poses are re-blended once per tick.

There is no separate render rate for them to be per-frame at: `FUN_004677bc` spin-waits the whole
loop to 40 ms, so **tick and frame are the same thing in DBSIM** (see
[`dbsim-physics-notes.md`](dbsim-physics-notes.md#fixed-point-math-toolkit)). A vanilla frame always
shows a pose evaluated that same iteration, at 25 Hz.

> Port note: the engine runs a fixed 25 Hz tick (`SimWorld.TicksPerSecond`, `TickDelta` pinned to the
> vanilla 81) with rendering decoupled, so it produces the same 25 distinct poses a second the
> original does, however fast it renders; consecutive rendered frames may repeat a pose, which
> vanilla never does only because it never renders faster than it ticks. Sampling `NodeTransform` at
> render time with a sub-tick fraction would exceed the original's smoothness rather than match it,
> and is deliberately not done.

## Outstanding

- AI obstacle avoidance (`00416274`).
