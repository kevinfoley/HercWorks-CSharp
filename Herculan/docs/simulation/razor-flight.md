# Razor flight — flight model, contact probes, and the flight ceiling

The RAZOR is a **HERC-class object with a flyer flag**, not an instance of the `Flyer` class the
SKIMMER and the ground vehicles use. It is built by `Mech_Constructor`, carries a mech's 29-component
damage array and a mech's weapon mounts, and appears on the target list as `TargetClass.Herc`. What
the flag (`typeRec+0x50`, `HercSimDat.InputFlagFlyer`, file offset 78) changes is which code paths it
takes, and it changes nearly all of them.

[`mech-locomotion.md`](mech-locomotion.md) covers the walker paths; nothing in it applies to a RAZOR.

## Call graph

| Address | Name | Role |
| --- | --- | --- |
| `0041bb9c` | `Razor_ApplyFlightInput` | Input hand-off. Replaces `Mech_ApplyThrottleInput` **and both turret ticks** |
| `00466a54` | `FlightModel_Step` | The flight model. Settles throttle, airspeed, drag, angular rates, attitude, velocity |
| `004198f4` | `Razor_MovementTick` | The per-tick move. Replaces `Mech_MovementTick` |
| `0041b130` | `Player_PerFrameCockpitUpdate` | Its throttle exchange has a flyer branch — see [Throttle](#throttle) |
| `0041bb3c` | `Mech_GetDisplaySpeedKph` | Flyer branch maps airspeed through `Math_MapRange` (`0047de3c`) |
| `00415498` | `Mech_GetSpeed` | Returns `mech+0x2bd` for a flyer, a scaled `mech+0x28e` for a walker |
| `00467a24` | `Math_RateLimitedMoveTowardInt` | 32-bit twin of `Math_RateLimitedMoveToward`; airspeed is an int |
| `004669dc` / `00466a1c` | `Math_IntegrateVec3IntOverTick` / `...ShortOverTick` | `Math_IntegrateRateOverTick` over a vec3 |
| `00466984` | `Math_MeanVec3Short` | Component-wise mean of two vec3s, into the static at `004d3bdc` |

Ported as `MechObject.Flight.cs` and `FlightModelRecord.cs`.

### How the flyer paths are reached

`Mech_Constructor` (`00415bb0`) picks one of three **behaviour class** instances by
(is this the local player `mech+0xa3`, does the type record set the flyer flag):

| Condition | Behaviour instance |
| --- | --- |
| Not the player | `004993a4` |
| Player, walker | `004993e2` |
| Player, flyer | `00499420` |

Each instance holds three pointer-to-member-function triples `{func, thisDelta, vtableIndex}` copied
in by its own constructor from a 0x24-stride block of source triples. The block at `0049991c` is the
walker set and its `+0x0c` slot is `Mech_MovementTick`; `FlyerBehaviourSlots` (`00499940`) is the
next block and its `+0x0c` slot is `Razor_MovementTick`. Because these are member pointers reached
through dispatchers (`00415b38` and its siblings) rather than vtable entries, Ghidra reports no xrefs
on either move function.

Which *instance* takes which block is inferred rather than traced: `004198f4` occurs at exactly one
address in the whole image, in the block immediately after the one holding `Mech_MovementTick`, and
`Mech_Constructor`'s third branch is the only flyer-gated one.

**Only the player's RAZOR flies.** An AI-controlled one takes the not-the-player branch and the
walker move, which would walk it. No retail mission places one.

The input side is gated separately, in `Sim_PollPlayerInput` (`00460764`), on the flyer flag alone.

## `fm\<NAME>.FM` — the flight model file

54 bytes, read straight into the type record at `typeRec+0x1dc` by `MechType_InitOne` (`004201a8`),
and only for a type whose record sets the flyer flag. Two files ship: `RAZOR.FM` and `SKIMMER.FM`.
Parsed by `FlightModelTransformer`; the sim-side view with the derived field is `FlightModelRecord`.

| Offset | Type | Field | RAZOR | SKIMMER | Role |
| --- | --- | --- | --- | --- | --- |
| 0 | i16 | `MaxPitchRate` | 400 | 600 | Pitch rate cap, and the Q8 gain from full elevator |
| 2 | i16 | `MaxRollRate` | 1500 | 1400 | Roll rate cap, and the gain from full aileron |
| 4 | i16 | `MaxYawRate` | 400 | 600 | Yaw rate cap, and the gain from full rudder |
| 6 | i16 | `MaxPitchAccel` | 200 | 250 | Cap on the pitch command per tick |
| 8 | i16 | `MaxRollAccel` | 400 | 300 | Cap on the roll command **and on the yaw command** |
| 10 | i16 | `ThrustResponse` | 100 | 100 | How fast airspeed closes on the throttle's demand |
| 12 | — | — | 0 | 0 | Unread |
| 14 | i32 | *(derived)* | 0 | 0 | Zero on disk; the loader writes the ceiling slope here |
| 18 | i16 | `AngularDamping` | 500 | 500 | Q10 of each axis' rate bled off per tick |
| 22 | i16 | `PitchLevelShift` | 16 | 16 | Right shift, attitude → self-levelling pitch command |
| 26 | i16 | `RollLevelShift` | 5 | 6 | The roll counterpart |
| 30 | i16 | `BankTurnShift` | 4 | 4 | Right shift, bank angle → heading rate |
| 34 | i32 | `CeilingAtMaxSpeed` | 60000 | 120000 | Flight ceiling at `AirSpeedMax` |
| 38 | i32 | `CeilingAtMinSpeed` | 6000 | 6000 | Flight ceiling at `AirSpeedMin`, above ground level |
| 42 | i32 | `AirSpeedMax` | 1500 | 1000 | Airspeed at full throttle |
| 46 | i32 | `AirSpeedMin` | 250 | 500 | Airspeed at idle — a floor, not a stall speed |
| 50 | i32 | `LateralDrag` | 300 | 400 | Q10 of the sideways and vertical velocity shed per tick |

The three shift fields are *read* by the flight model as 32-bit loads at offsets 22/26/30, so each
occupies four bytes; only the low half is ever non-zero, which is why the parser reads them as `i16`
and skips the rest.

### Bytes 12-17 are not padding

Offsets 14-17 are a slot the file leaves zero and the *loader* fills in. `MechType_InitOne` finishes
its flyer branch with

```c
typeRec[+0x1ea] = Q16Divide(CeilingAtMaxSpeed - CeilingAtMinSpeed, AirSpeedMax - AirSpeedMin);
```

which is `typeRec+0x1dc + 14`, i.e. the middle of the record it has just read. That is the ceiling's
slope against airspeed, and it makes the whole field block a mixture of file content and derived
state. `FlightModelRecord.CeilingPerSpeed` holds it engine-side so the parsed file stays untouched.

## Flight state — `mech+0x2b9`

`Mech_Constructor` zeroes 0x4e bytes from here, seeds the airspeed at 1000 and copies the machine's
throttle into `+0x2d7`. Every flyer path addresses the block through a single pointer.

| Offset | Type | Meaning |
| --- | --- | --- |
| `+0x2b9` | i32 | Body velocity X — sideslip. **-X is port**, see [Contact probes](#contact-probes) |
| `+0x2bd` | i32 | Body velocity Y — **airspeed**. What `Mech_GetSpeed` returns for a flyer |
| `+0x2c1` | i32 | Body velocity Z — vertical |
| `+0x2c5` | i32 x3 | World velocity. What `Razor_MovementTick` integrates into the position |
| `+0x2d1` | i16 | Pitch rate |
| `+0x2d3` | i16 | Roll rate |
| `+0x2d5` | i16 | Yaw rate |
| `+0x2d7` | i16 | Throttle setting, ±0x400 — **not** the same field as the walker's `mech+0x290` |
| `+0x2d9` | ptr | Back-pointer to the object's own transform block at `mech+0x0c` |
| `+0x2dd` | i16 x10 | Last tick's rotation matrix, transposed |
| `+0x2f1` | i32 x3 | Last tick's position, negated and rotated — the inverse translation |
| `+0x2fd` | i32 | The heading rate the current bank is producing |

`mech+0x28e`, the walker speed scalar, is **never written** on a flight path. That is deliberate —
`Mech_GetSpeed` branches specifically to avoid it — and it is why the cockpit throttle gauge's speed
bar sits dead on a RAZOR (see [`KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md)).

## Axis remapping

The device layer hands the same four axes to both control paths. A flyer reads them as an aircraft's:

| Device axis | Walker | Flyer |
| --- | --- | --- |
| `+0x0e` stick X | Steering | **Aileron** |
| `+0x10` stick Y | Throttle | **Elevator** |
| `+0x12` | Turret twist | **Rudder** |
| `+0x14` | Turret pitch | **Throttle** |

Neither turret tick is on this path, so **a RAZOR's turret never moves** and its guns point where its
nose points. The throttle has to move off stick Y because on an aircraft the primary stick axes are
pitch and roll, and it lands on the axis a walker has no other use for.

### The keyboard

`Input_BuildKeyboardAxes` (`0045a4b0`) produces **two signed axis pairs, not four independent axes**:
held keys 0-7 accumulate into the first pair, keys 8-13 into the second, each key adding its own
`(dx, dy)` entry shifted left 7 — ±0x80, half a stick's travel, the constant
`MechControls.KeyboardAxis` already carries.

Those pairs are *sources*, not destinations. `Input_BuildSourceTable` (`0045a5c0`) registers them in
a source-pointer table at `004d2394` alongside the four joystick axes and the buttons, and a binding
selects which source each game axis reads — which is why `Sim_PollPlayerInput` reads some axes
through a pointer-to-pointer.

**No arrow-key-to-axis mapping can be recovered from the executable.** The per-key `(dx, dy)` table
at `0049eb6d` is all zeroes in the image and is filled at runtime from a saved key configuration.
Note that the retail game only includes a menu for adjusting joystick bindings, not keyboard bindings.

## Control law (`FlightModel_Step`)

Nothing in it moves the aircraft; it produces the world velocity `Razor_MovementTick` integrates.
The order below is the function's own.

### Throttle

An analogue throttle axis is read as a position, `axis << 3` clamped to ±0x400. Everything else is a
rate: `IntegrateRateOverTick(Q8(100, axis))` accumulated into `+0x2d7` and clamped the same way.
Unlike the walker's throttle lever there is no inverted sense and no clamp to one side of zero.

`Razor_ApplyFlightInput` then copies `+0x2d7` onto `mech+0x290` and sets the `mech+0x93` dirty flag,
but **only on a tick the throttle axis moved**. The reverse direction — gauge to flight model — is in
`Player_PerFrameCockpitUpdate`, which with the dirty flag clear writes the gauge's value to
`mech+0x2d7` as well as `mech+0x290`, gated on the flyer flag. That single line is the only path by
which the cockpit slider reaches the flight model, and on a keyboard-only setup it may be the only
working throttle control the player has.

### Airspeed

```
demand = AirSpeedMin + Q10(AirSpeedMax - AirSpeedMin, (throttle + 0x400) >> 1)
demand -= Q10(pitch < 0 ? 250 : 62, pitch)
RateLimitedMoveTowardInt(airspeed, demand, IntegrateRateOverTick(ThrustResponse))
```

**Airspeed is not thrust and pitch is not momentum.** Attitude biases the speed the throttle *asks
for*, four times as strongly nose-down as nose-up, and the aircraft slews toward it at a fixed rate.
A dive is fast and a climb is slow, but level out and the speed returns to whatever the throttle
wants. There is no energy to trade.

### Sideslip drag

The sideways and vertical components of body velocity — forward excluded, which is what makes this
drag rather than braking — are rotated into world space, scaled by `LateralDrag`, rotated back
through **last tick's** frame, and subtracted. This is what keeps the aircraft flying where it is
pointing instead of drifting round its own turns.

Only the two ground-plane components are scaled; the world-vertical one is subtracted at an
effective coefficient of 1 (`00466c26`-`00466c67` scales two of the three). The asymmetry is
load-bearing: a RAZOR sheds vertical speed far harder than sideslip, which is why it settles onto its
flight path rather than floating.

### Angular rates

| Axis | Command with input | Command without | Damping |
| --- | --- | --- | --- |
| Pitch | `Q8(MaxPitchRate, elevator)` | `-pitch >> PitchLevelShift` | `-Q10(AngularDamping, pitchRate)` |
| Roll | `Q8(MaxRollRate, aileron)` | `-roll >> RollLevelShift` | as above, but only when the stick fights the roll already under way |
| Yaw | `Q8(MaxYawRate, -rudder)` | — | always `-Q10(AngularDamping, yawRate)` |

**Pitch self-levelling is switched off on retail data.** Both files state a shift of 16, and a 16-bit
angle shifted 16 is nothing. An aircraft holds the attitude it is trimmed to and bleeds only its
pitch *rate* away — which is why a RAZOR left nose-up climbs until the ceiling stops it. Roll
self-levels for real, and its branch stops the wings exactly at level rather than letting the term
overshoot into a wallow.

Each command is clamped to its acceleration limit, the damping is added *outside* that clamp, the
sum is integrated, and the resulting rate is clamped to its rate limit. Yaw borrows the roll axis'
acceleration limit; the file has only two.

### Turning is banking

```
bankTurnRate = |roll| < 0x4000 ? -roll >> BankTurnShift
                               : (short)(roll - 0x8000) >> BankTurnShift
```

The rudder yaws the airframe about its own axis, but what swings the nose round the sky is the bank.
The rate is read straight off the bank angle and applied to the heading **on top of** the integrated
attitude, so a banked RAZOR turns about the world's vertical axis and not its own. Past a quarter
turn of bank the sense inverts, measured from the half turn, so an inverted aircraft turns the way
its wings say.

### Attitude

The rotation is integrated as a **matrix**, not as three angles: a delta matrix is built from the
mean of this tick's rates and last tick's (`Math_MeanVec3Short`), composed onto the current rotation,
and the euler triple read back out of the result. That is what keeps a RAZOR flyable through a
vertical climb where integrating the angles directly would gimbal, and it is the only place in the
simulation that composes a rotation this way. The matrix the function writes is then invalidated
immediately by the heading change, so the euler round-trip is what actually survives.

Finally the world velocity is re-expressed in the new body frame. That costs forward speed whenever
the airframe rotates; `Q10(900, loss)` of it is handed straight back, so a hard turn scrubs about 12%
and no more.

### The flight ceiling

```
ceiling = CeilingAtMinSpeed + Q16(airspeed - AirSpeedMin, CeilingPerSpeed)
```

**Altitude is bought with speed.** The RAZOR's ceiling runs from 6000 world units (36 m) at its 250
idle airspeed to 60000 (360 m) at its 1500 maximum. A pilot who wants height has to go and get it at
full throttle; one who throttles back is pushed back down.

Nothing clamps to it. Past the ceiling the model builds a push proportional to the overshoot and
resolves it through the current bank — cosine onto pitch, the quarter-turn shift onto yaw — so the
push is toward the *ground* however the aircraft is lying. It only ever lowers the pitch command, so
it can refuse a climb but never force a dive.

`CeilingAtMaxSpeed` is therefore not the flat "max altitude" its position in the file suggests: it is
only reached at the top of the speed range.

## Contact probes

`Razor_MovementTick` has **no swept body test and no terrain clamp on the airframe as a whole**. Six
points are checked instead, each against the ground beneath it and — bar the fuselage — swept forward
as a ray one tick's travel long through `Sim_RaycastObjectList`, so a wing catches a building as
readily as a hillside.

The components are the game's own, from `STRINGS0` group 14, the flyer damage-readout list the
Heads-Down Display takes in place of the walker's group 13 (see
[`heads-down-display.md`](../formats/heads-down-display.md)):

| Component | Name | Probe point | Clearance | Ground test | Reaction |
| --- | --- | --- | --- | --- | --- |
| 7 | `L WING ARMOR` | `(-1000, -700, -100)` | 300 | yes | Roll away, `Q10(4000, depth)` or a flat 4000 |
| 8 | `R WING ARMOR` | `(1000, -700, -100)` | 300 | yes | as above, opposite sign |
| 4 | `L NACELLE ARMOR` | `(-450, -500, 0)` | 150 | no | Flat roll kick of 8000 |
| 5 | `R NACELLE ARMOR` | `(450, -500, 0)` | 150 | no | as above, opposite sign |
| 0 | `COCKPIT ARMOR` | `(0, 1000, 0)` | 200 | yes | Pitch **up**, `Q10(2000, depth)` or a flat 2000 |
| 6 | `FUSELAGE ARMOR` | the machine's origin | — | yes | Position snapped back to the ground |

Component 4 being the *left* nacelle settles the frame's handedness: its probe sits at negative X, so
**-X is port and +X starboard**.

Damage scales with speed on a ground contact (`Q10(airspeed, 500)` for a wing, 1000 for the cockpit,
5000 for the fuselage) and is a flat figure on an object contact. The shield figure is always 8000.
A contact kicks the rate *and* applies it to the attitude in the same tick, leaving the rate standing
for the flight model to damp out afterwards.

Destroying the cockpit or the fuselage latches `mech+0xa4` — the same byte a walker loses its legs
to — and with it set the aircraft stops integrating position altogether. It is down where it fell.

### The look-ahead

A seventh point at `(0, 15000, -1500)` — far ahead and well below — pulls the nose up when the ground
rises into it, at a hundredth of the cockpit probe's gain. **It only runs on an intact airframe**:
both nacelles and the cockpit have to be alive, so a RAZOR that has lost any of the three flies
straight into the hill.

### The shot record

Contacts go through `Mech_ApplyDirectFireDamage` with `AirframeContactShot` (`0049a170`), a shot
record assembled in the executable's statics and refilled per probe rather than taken from a fired
weapon. Two of its fields matter beyond the damage figures:

- `+0x0e`, the attacker, is **NULL** — flying into a hillside is nobody's kill.
- `+0x14` is the aircraft itself. This is `Sim_RaycastObjectList`'s *second* exclusion, distinct from
  the attacker; the beam path writes only the first, so the airframe probe is what makes the second
  reachable at all.

Its impact effects come from `AirframeContactImpactFx` (`0049a158`), a `PROJ.DAT`-shaped 12-entry
table held in the image rather than in a file: shield `{11,11,11,11}`, ground and armour both
`{0,1,4,5}`.

## HUD speed

`Mech_GetDisplaySpeedKph` (`0041bb3c`) branches on the flyer flag. A walker divides its speed scalar
by the type's top speed; a flyer maps airspeed from `[0, AirSpeedMax]` onto `[0, typeRec+0xc2]`
through `Math_MapRange` (`0047de3c`), because a flyer's record does not describe the walker top speed
the other branch needs. Both land on the same readout scale, so the gauge reads the same way for
either chassis. A RAZOR at full throttle reads 83 km/h.

## Engine note

`Razor_MovementTick` closes by pitching the looping engine hum (catalog id `0x2d`, `herceng1.wav`)
at `FastMagnitude3D(bodyVelocity) * 16 + 28000` in 16.16, clamped to 16 bits, and re-placing it at
the machine. It runs for the player's machine alone and is silenced on death. The hum is started by
`Cockpit_PowerUpSound` and is the flyer's, not the walker's, despite the sample's name — see
[`../formats/audio.md`](../formats/audio.md).

## Not ported

- The gun-convergence pass (`maybe_Mech_ConvergeGunsOnRange`, `0041a74c`) that closes
  `Razor_MovementTick`. It is weapon aiming and has no counterpart in the engine for walkers either.

The wreckage a fatal contact sheds is ported — group 3 at the contact point, and only from the
cockpit and fuselage probes, the two that can end the flight. See
[`destruction-effects.md`](destruction-effects.md#spawn-sites).

## Rejected readings

| Reading | Why it is wrong |
| --- | --- |
| `004198f4` is a flyer terrain-avoidance autopilot | It is the flyer's whole per-tick move, the counterpart of `Mech_MovementTick`. The terrain probes are its collision model, not an assist; the pull-up look-ahead is one of seven points |
| The `.FM` fields either side of `MaxRollRate` are all roll parameters | The field order invites it, but only three concern roll. `AngularDamping` (18) damps every axis, and `LateralDrag` (50) is sideslip drag applied to velocity rather than rotation |
| `CeilingAtMaxSpeed` (34) is a flat maximum altitude | Nothing clamps to it. It is the far end of a ramp the loader derives at offset 14, reached only at `AirSpeedMax` |
| Bytes 12-17 of `.FM` are zero padding | They are zero *on disk*. The loader writes the ceiling slope into 14-17 |
| The cockpit throttle slider does nothing on a RAZOR | It works. `Player_PerFrameCockpitUpdate` has a flyer-gated line writing the gauge value to `mech+0x2d7`. What is dead is the gauge's *speed* bar, which reads the walker scalar |
| The RAZOR is an instance of the `Flyer` class | That class is the SKIMMER's. The RAZOR is a `Mech` with `typeRec+0x50` set |
