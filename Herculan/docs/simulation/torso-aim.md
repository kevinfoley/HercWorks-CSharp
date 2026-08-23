# Torso aim — turret twist and pitch (DBSIM.EXE)

The manual calls it the **turret**: the part of a HERC carrying the pilot and the weapons, aimed
independently of the legs. DBSIM's own field and symbol names say "torso"; they are the same thing.

Reverse-engineered from `DBSIM.EXE`. Movement of the machine itself is in
[`mech-locomotion.md`](mech-locomotion.md); how a posed node reaches the screen is in
[`dts-node-posing.md`](../formats/dts-node-posing.md).

**Core fact: the turret has no rotation of its own.** The type record names a sequence per axis, each
one a single full sweep of one node, and the twist/pitch angle selects a *position* within that
sequence. Nothing rotates the torso; an animation is seeked to match the angle.

## Call graph

| Address | Name | Role |
|---|---|---|
| `0041a550` | `Mech_TorsoTwistTick` | One tick of the twist axis |
| `0041a808` | `Mech_TorsoPitchTick` | The same on the pitch axis |
| `0041e8d4` | `Mech_CenterTorsoTick` | The [Backspace] centring command |
| `00479238` | `AnimThread_SeekToPosition` | Angle → frame + intra-frame offset |
| `0041a6d0` / `0041a994` | — | Servo loop sound, gated on `mech+0xa3` |
| `0041a74c` | — | Gun convergence, run from the pitch tick |
| `00415488` | `Mech_GetTorsoTwistAngle` | Twist-angle accessor, mech vtable `+0x3c` |

Three callers, all once per tick: `Sim_PollPlayerInput` (`00460764`) for the player,
`Mech_TargetRelativeToPilot`'s caller for AI aiming, and `Mech_CenterTorsoTick` for centring.
`Mech_MovementTick` does **not** call them — the turret is driven from the input path, between the
throttle and the move.

## Instance fields

| Offset | Meaning |
|---|---|
| `+0x230` | Twist animation thread |
| `+0x234` | Pitch animation thread |
| `+0x294` / `+0x298` | Twist rate / angle |
| `+0x296` / `+0x29a` | Pitch rate / angle |

Both angles are binary angle measure relative to the machine's own heading.

## Type record fields

Two record fields previously carried names nothing read. Both are **sequence ids**:

| rec | typeRec | C# field | Meaning |
|---|---|---|---|
| 26 | `+0x1c` | `AnimId_TorsoTwist` | Twist sequence (was `InputTorsoRazrFlag`) |
| 28 | `+0x1e` | `TorsoTwistSpeed` | Twist rate at full stick |
| 30 | `+0x20` | `TorsoRotateAccel` | How fast the twist rate may build |
| 32 | `+0x22` | `TorsoTwistDegreeMax` | Twist limit, applied symmetrically |
| 34 | `+0x24` | `AnimId_TorsoPitch` | Pitch sequence (was `InputFlagsTorso`) |
| 36 | `+0x26` | `TorsoPitchMaxRate` | Pitch rate at full stick |
| 38 | `+0x28` | `TorsoPitchRate` | How fast the pitch rate may build |
| 40 | `+0x2a` | `TorsoPitchMax` | Pitch limit looking up |
| 42 | `+0x2c` | `TorsoPitchMin` | Pitch limit looking down, negative |

Fleet values: twist rate 1000 and limit 14000 (76.9°) on all 18; twist accel 1000 except RAPTOR2's
250. Pitch rate 800 except RAPTOR2's 700; pitch range 3500/−2000 on OUTLAW, MAVERICK, STINGRAY and
MONGOOSE, 6000/−4000 on the rest.

Unlike the locomotion accel pair, both accel fields go through `Math_IntegrateRateOverTick`, so they
are already time-based and need no rescale.

## The tick

Twist and pitch are the same code, differing only in which fields and which limits they use:

```
target = Q8(axis, maxRate)                  // axis is ±0x100, the same stick units as steering
if (|rate| < |target|)  rateLimitedMoveToward(rate, target, integrateOverTick(accel))
else                    rate = target       // and the angle integrates the new rate over the whole tick
angle = clamp(angle + integrateOverTick((rateBefore + rate) / 2), limitMin, limitMax)
```

`|x|` saturates rather than wraps: `-0x8000` yields `0x7fff`.

**Only acceleration is rate-limited.** The moment the stick asks for less than the turret is already
doing, the rate snaps to it — so releasing the stick stops the turret dead, and so does reversing it.
The angle integrates the mean of the rate before and after, a trapezoid rule while ramping and a
plain step while not.

### Snap-to-target

Both ticks take a target angle and an enable flag. When enabled, the turret stops dead on the tick
its angle moves onto or across the target:

```
if ((angle - target >= 0 && before - target < 0) || (angle - target <= 0 && before - target > 0)) {
    angle = target;  rate = 0;
}
```

Normal piloting passes it **disabled**. It exists for the centring command.

### Angle to pose

Each tick ends by seeking the axis's thread:

```
AnimThread_SeekToPosition(thread, sequenceId, (unsigned)angle >> 2)
```

`AnimThread_SeekToPosition` (`00479238`) sums the sequence's frame durations, scales the position by
`Q14 x (total - 1)`, and walks the frames subtracting durations to land on a frame plus an
intra-frame offset, which it installs with `AnimThread_SetSequence`. The shift is on the **unsigned**
angle, so a whole turn spans the sequence exactly once and a negative angle lands in its far end
rather than off the front.

The threads themselves never play: `Mech_Constructor` gives every thread a rate of zero and only the
locomotion tick ever raises one, so `AnimThread_Advance` returns immediately for these two.

### Three threads per machine

`Mech_Constructor` (`00415bb0`) builds them in this order, skipping any whose sequence id is
negative: locomotion on `typeRec+0x12` at `mech+0x22c`, twist on `+0x1c` at `+0x230`, pitch on
`+0x24` at `+0x234`.

The order matters. `ShapeInst_EvalAllNodeLocals` (`004789f4`) runs the threads **last-registered
first**, each overwriting the local transform of every node its sequence covers with no regard for
what is already there, so the **first**-registered thread's writes are the ones left standing:
locomotion outranks the turret.

It decides nothing on 17 of the 18 retail HERCs — locomotion covers parts 1,2,3,5–10, twist covers 4
and pitch covers 11 (MONGOOSE 11 and 12), disjoint. **HEADHUNT is the exception**: its twist node is
5, which its own locomotion sequences also animate, so its twist is overridden while it is moving.
That is the retail data's own behaviour.

### The angle is not the drawn direction

The two drift apart by up to ~7%, because the sequences' keyframes are not evenly spaced. OUTLAW's
twist sequence (8 frames of 100 ticks, node 4, rotation about Z only) steps
`0, −7280, −15470, −23660, −31850, −40238, −48428, −56618` — summing to exactly −65536, one full
turn, but in uneven strides. At the 14000 limit the eye ends up 13004 round.

Nothing is inconsistent as a result: `Mech_TargetRelativeToPilot` (`0041ef14`) reads the camera
node's own composed transform, so the HUD, the aim and the view all agree with the drawn pose. The
angle field is control state, not a direction.

## Centring — [Backspace]

`Mech_CenterTorsoTick` (`0041e8d4`) drives both axes from the angles themselves and enables the snap,
so the turret runs home fast, eases off as it arrives, and stops exactly on centre:

```
twistAxis = -clamp(Q10(0xfa, twistAngle), ±0x100)   // pitch likewise
```

It is a **mode**, not a keypress. Scancode `0x0e` latches `DAT_004d2588` and clears the ATT flag;
the input path clears it again the moment either turret axis is non-zero, which is why it tests the
axes before it tests the mode. Nothing clears it on arrival — with the turret centred the axes are
zero and nothing moves, so it simply idles until the pilot takes the turret back.

Scancode `0x2b` (`\`, "Center Body") sets the opposite flag `g_CenterBodyMode` (`004d2af4`), which
turns the legs under the turret rather than the turret back to the legs. It substitutes the steering
and the twist axis both, and is documented with the rest of the steering in
[`mech-locomotion.md`](mech-locomotion.md#center-body).

## The pilot's frame

`Mech_TargetRelativeToPilot` (`0041ef14`) composes the camera node's world transform with the
machine's and brings a target into that frame to place it on the HUD. **That frame's orientation is
what "the direction the pilot is looking" means in DBSIM** — the camera node hangs below both turret
nodes (see [`mech-locomotion.md`](mech-locomotion.md#cockpit-eye-and-bob)'s chain table), so twist and
pitch turn the view with nothing having to add them to it.

## HERCULAN Engine implementation

| File | Contents |
|---|---|
| `Sim/Anim/ShapeInstance.cs` | The shape's threads and the node poses they produce together |
| `Sim/Anim/AnimationThread.cs` | `SeekToPosition`, `TryGetLocal` |
| `Sim/MechObject.Torso.cs` | Both ticks, the centring command |
| `Sim/MechObject.cs` | The three threads, `EyeTransform` |
| `Sim/MechControls.cs` | `TorsoTwist`, `TorsoPitch`, `CenterTorso`, `CenterBody` |
| `Content/RotationIndicator.cs`, `Render/Overlay2DRenderer.cs` | The HUD rotation indicator — see [`cockpit-hud.md`](../formats/cockpit-hud.md#front-window-hud--the-gunsight-complex) |

`MechObject.EyeTransform` is the pilot's whole frame, orientation included; `EyePosition` is its
translation. The host takes the cockpit camera's yaw and pitch from it rather than from the machine's
heading.

**Verified:** the eye's yaw tracks the twist angle across the travel and its pitch tracks the pitch
angle (OUTLAW, 3443 drawn against a 3500 limit up, −2133 against −2000 down — the pitch keyframes are
near-uniform where the twist ones are not). The rate ramps to maximum in about five ticks, holds, and
snaps to zero the tick the stick is released. Centring returns 14000 to 0 and stops there.

**The walk cycle does not rotate the eye at all** — measured at zero yaw, pitch and roll swing over a
full stride on OUTLAW, OGRE, MONGOOSE and HEADHUNT, so taking the camera's orientation from the eye
node changes nothing about a machine with its turret centred. One real difference it does introduce:
MONGOOSE's camera node has a −570 (−3.1°) rest pitch that a heading-only camera discarded.

Host keys follow the manual's keyboard turret set — `J`/`K` twist, `I`/`M` pitch, `Backspace`
centres. `--turret <twist> <pitch>` holds the axes for a `--screenshot` run.

## Not ported

- **Automatic Turret Tracking** ([T]), the third branch of the input path's turret block. Needs
  target selection.
- **AI turret aiming**, the caller that feeds `Mech_TargetRelativeToPilot`'s output back into these
  two ticks with a snap target derived from the target's bearing.
- **`FUN_0041a74c`**, gun convergence: the pitch tick's third argument is the range to the current
  target, and it writes a per-gun aim vector from it using record field 100.
- **The servo sound** (`0041a6d0` / `0041a994`): sound 0x21, started when the axis exceeds 0xc0 and
  the angle is still changing, stopped when the axis centres or the angle stops.
