# AI combat states

The nine behaviour thinks that are not navigation: the five a machine fights in, the two it disengages in, and the two it stands still in. What state a machine is in and how the think is reached is [`ai-dispatch.md`](ai-dispatch.md); which object it fights is [`ai-targeting.md`](ai-targeting.md); how it shoots once it is pointed is [`ai-weapons.md`](ai-weapons.md); the walking states are [`ai-navigation.md`](ai-navigation.md).

**A combat state decides one thing: where to stand.** Every one of them ends in the same two calls — `Ai_CombatMoveStep` to walk and `Ai_AimAndFire` to shoot — and differs only in the steering and standoff it hands the first of those. The target is already chosen when the think runs, and the weapon is chosen inside the second.

## The shape they share

```
if (Ai_BeginSkirtIfBlocked(mech)) return 0                    // 0041de9c, below
Ai_BuildCombatGeometry(mech, &geom, null)                     // 0041e758
<the state's own steering and standoff, written into geom>
Ai_CombatMoveStep(mech, &geom)                                // 0041e828
Ai_AimAndFire(mech, geom.aspect, null)                        // 0041ea7c
if (Ai_ShouldAbandonTarget(mech) || mech is out of action) return Ai_ClearSquadEngageOrder(mech)
return 0
```

`attacking flyer` omits the skirt gate and `fleeing` omits it and the abandonment test; everything else is verbatim. A nonzero return zeroes the state's own dwell countdown, which is a state saying it is finished — see [`ai-dispatch.md`](ai-dispatch.md#what-the-dwell-time-buys).

**"Out of action" is the same three bytes throughout the AI**: `mech+0xa5` no weapons left, `mech+0xa4`, `mech+0x99` destroyed. `Ai_ClearSquadEngageOrder` (`0041c478`) is the tail: it returns 1, and on the way clears a standing squad engage order (`mech+0x23e == 4`) whose target is the one being let go.

## The geometry block — `Ai_BuildCombatGeometry` (`0041e758`)

`0x18` bytes on the think's own stack, built once per tick and then amended by the state before the move step reads it.

| Offset | Type | Field |
|---|---|---|
| `+0x00` | `int16` | Bearing to the target |
| `+0x02` | `int16` | **Bearing error** — that bearing less the machine's heading. The one field a state rewrites to steer |
| `+0x04` | `int16` | Its magnitude, saturating `-0x8000` at `0x7fff` |
| `+0x06` | `int16` | The target's **aspect**: the bearing back to this machine in the target's turret frame |
| `+0x08` | `int16` | Its magnitude |
| `+0x0a` | `int32` | **3D** range, `Math_DistanceBetweenPoints`. Every navigation range in the AI is the ground-plane one; this is not |
| `+0x0e` | `int16` | **Approach flag**: `1` forward, `-1` reverse, `0` stand. Starts 0 |
| `+0x10` | `int32` | Near standoff. Starts 15000 |
| `+0x14` | `int32` | Far standoff. Starts 30000 |

The aspect is `targetTurretTwist + (bearingToTarget - 0x8000) - targetHeading`, and its only consumer is `Ai_ChooseWeapon`'s shield-facing test — see [`ai-weapons.md`](ai-weapons.md#open-questions). The two travel thinks build the same quantity by hand rather than through this function.

## The move step — `Ai_CombatMoveStep` (`0041e828`)

```
if      (range < nearStandoff) approach = |bearingError| > 0x3fff ? 1 : -1     // open the range
else if (range > farStandoff)  approach = |bearingError| < 0x4001 ? 1 : -1     // close it
geom+0x04 = |bearingError|                                                    // after the tests
if (approach != 0) { mech+0x5a = approach; speed = approach > 0 ? 0x100 : -0x100 }
else                                                                speed = 0
Mech_LocomotionTick(mech, bearingError >> 6, speed, 0)
```

Four things it fixes.

- **The standoff pair is a ring, and the approach flag is how the machine holds it.** Too near and it moves away from the target, too far and it moves toward it, and the sign is chosen so that either happens whichever way the machine is facing: a machine with its back to something it wants to leave drives forward, a machine facing it reverses. Between the two ranges the caller's own flag stands, and `0` means stand and shoot.
- **A state that wants to steer somewhere other than at the target rewrites `+0x02`.** Steering is the same `error >> 6` the navigation layer uses, clamped by the control law at the axis stop.
- **The two range tests read the magnitude the geometry left**, before the recompute on the next line — so they judge by where the *target* is, not by where the state has pointed the machine.
- **Speed is `±0x100` and nothing else.** A combat state never cruises; it is at the stop or stopped.

`mech+0x5a` is the behaviour block's scratch, and the approach flag written there is read nowhere. It is a `short` laid over the same bytes as the `int` timer `fleeing` steps at `mech+0x5b`, so a fleeing machine's side-switch clock has its low byte rewritten every tick by whichever sign the flag took. The clock is 4000 ms; the aliasing moves it between 3840 and 4095.

## `attacking` (3) — `Mech_BehaviourAttackThink` (`0041c594`)

The only state that reads the *target's* facing to decide where to stand, and it does it in two halves split on `|aspect| < 8000`.

**The target is looking at me** (`|aspect| < 8000`, about 44°). Take a point 18000 units off the target's own beam:

```
approach = (0x8000 - |bearingError|) >= 4000 ? 1 : -1
side     = (bearingError < 0) == (|bearingError| > 0x4000) ? +0x4000 : -0x4000
point    = Math_OffsetPointByBearing(target.position, target.heading + side, 18000)
if (approach == -1) point = 2*mech.position - point          // reflect it through myself
bearingError = Math_HeadingToward(point, mech.position) - mech.heading
```

The side works out to the target's right when it lies ahead-right or behind-left of the machine and to its left otherwise, so the machine always cuts toward the nearer beam rather than crossing the target's nose. `approach` is `-1` only for a target within about 21° of dead astern, and that branch **reflects the aim point through the machine's own position**: the steer reverses, and with the reverse speed the move step gives it the machine backs onto the same point instead of turning around to walk at it.

**The target is not looking at me.** No point is built at all: `approach = 0` when the target is within 45° of the nose and `-1` otherwise, and the bearing error is left pointing straight at it. So the machine squares up and shoots, and gives ground only while it is still turning.

The standoff pair is left at 15000/30000, which is the only combat state that uses the built-in ring.

## `flanking` (4) — `Mech_BehaviourFlankThink` (`0041d4e4`)

The shared shape with `Ai_CircleStep` (below) between the geometry and the move. Nothing else.

**No retail mission can install it.** The combat reassess gates `flanking` on `typeRec+0xc8`, which is zero on all 21 chassis — see [`ai-targeting.md`](ai-targeting.md#the-combat-reassess--mech_aicombatreassess-0041cf18). The circling step it is built out of is reachable, because `attacking base` shares it.

## `facing off` (5) — `Mech_BehaviourFaceOffThink` (`0041d41c`)

The simplest of them.

```
approach  = |bearingError| > 0x1fff ? -1 : 1
standoffs = 16000 / 35000
```

Walk in when pointed at the target, back off when more than 45° off it, and hold a slightly wider ring than `attacking`'s. It never steers anywhere but at the target, which is what "facing off" is: the machine that is outgunned keeps its front armour toward the machine that outguns it.

## `driving off en` (16) — `Mech_BehaviourDriveOffThink` (`0041def0`)

The skirt gate, then `facing off`'s think verbatim. What separates the two states is entirely in their descriptors: `driving off en` carries the "holding a place" flag bit and reassesses through `Mech_AiSelectBehaviour` rather than the combat reassess, so after its 50 s dwell it goes back to its order instead of looking for another fight. It is what a guard and a machine taking fire at its post are put into — see [`ai-navigation.md`](ai-navigation.md) and [`ai-targeting.md`](ai-targeting.md#taking-fire--mech_aiontakingfire-0041f7b8-mech-vtable-0x50).

## `attacking base` (6) — `Mech_BehaviourAttackBaseThink` (`0041c86c`)

```
if (target.typeRec+0x2e == 0) { aspect = |aspect| = 32000 }
Ai_CircleStep(mech, &geom)
standoffs = 25000 / -20536
```

The negative far standoff is the point: a range can never be below it, so the move step's "close it" arm runs on every tick the machine is outside 25000 and its "open it" arm on every tick inside. **The machine holds a 25000-unit ring and is never allowed to stand still.**

Forcing the aspect to 32000 makes `Ai_CircleStep` see a target that is not facing it, whatever the geometry says, so the machine stands off rather than circling. Which structures escape that is `BASES.DAT +0x2e`, below.

The completion test is the one place a state does not simply ask whether its target is finished:

```
if (squad order 4 with a target, or Group_IsOrderTarget(group, target))  done = target+0x99
else                                                                     done = target is out of action
```

A structure the mission actually named has to be **destroyed**; any other structure only has to be out of action. So a machine sent to level a specific building stays on it to the end, and one that picked a building up on its own lets go as soon as it stops mattering.

### `BASES.DAT +0x2e`

Nonzero on 8 of the 65 types, and read in exactly two places — here and the flee check's structure exception ([`ai-targeting.md`](ai-targeting.md#the-flee-check--mech_aifleecheck-0041cb94)).

| Types | Name | `+0x2e` |
|---|---|---|
| 11, 35 | MISSILE TOWER | 2 |
| 8, 32 | GUN TOWER | 1 |
| 47 | MOBILE MISSILE | 1 |
| 3 | GENERATOR | 1 |
| 10, 34 | TRANSPORT | 1 |
| the other 57 | | 0 |

Both readers treat any nonzero value alike, and both treat it as *this target is dangerous*: a crippled machine flees from one instead of pressing the attack, and an attacking machine circles one instead of standing off. The three armed types head the list, which is what makes the reading; what the generator and the transport are doing in it, and why the missile tower alone states 2, is not settled. See [`hit-detection.md`](hit-detection.md) for the rest of the record.

## `attacking flyer` (7) — `Mech_BehaviourAttackFlyerThink` (`0041c9cc`)

No skirt gate — there is nothing to walk around under a flyer — and the standoffs are 0 and 1000000, which puts both of the move step's arms out of reach and leaves the approach flag entirely to the state:

```
approach = |bearingError| >= 2000            ? -1
         : |aspect| > 2000 && range > 35000  ?  1
         : |aspect| > 2000 || range > 35000  ?  0
         :                                     -1
```

A machine only advances on a flyer it is pointed within 11° of *and* that is both far off and not pointed back at it; anything else is a stand or a retreat. Turning under a flyer is what the machine mostly does, since the bearing gate is tight enough that it is rarely satisfied while the flyer is manoeuvring.

It gives up on range: past 150000 units the think returns finished, before it even looks at whether the flyer is alive.

## `fleeing` (18) — `Mech_BehaviourFleeThink` (`0041d2c4`)

It runs *from* something, which is not the same as having it as a target. The first thing it does is move the selected target into the block scratch at `mech+0x61` and release the selection — decrementing `target+0x1a2` and setting `mech+0x9d`, the ordinary release — so a fleeing machine holds no target while it runs. The geometry, the aim and the completion test are all built against the stashed pointer instead.

```
if (|bearingError| < 0x3000) { approach = -1; bearingError -= 0x8000 }
else {
    if (Timer_CountDown(mech+0x5a) == 0) { mech+0x5b = 4000; mech+0x5f = rand & 1 }
    approach = 1; bearingError += mech+0x5f ? -0x6000 : +0x6000
}
standoffs = 0 / 10000000
TurboPod_Engage(mech+0x317)
```

Two legs. While the threat is still within 67.5° of the nose the machine reverses with its steering pointed a half turn away, which turns it; once it has turned, the other leg runs it off at 135° to the threat, flipping sides every 4 seconds so it does not run in a straight line. The Turbo Pod is engaged on every tick — this is the one place in the AI that uses one on mission orders, since [`ai-navigation.md`](ai-navigation.md)'s sprint needs a standing squad order.

**It still shoots at what it is running from.** `Ai_AimAndFire` is called against the stash, and `Mech_AiFleeCheck` has already set `mech+0x2aa` to 300, 600 or 1000, which drops `Ai_ChooseWeapon`'s score floor to near or below zero — so a fleeing machine fires almost anything it still has.

The state ends when the thing it is running from is out of action. Nothing else ends it but the descriptor's 15 s dwell.

## `skirting` (14) — `Mech_BehaviourSkirtThink` (`0041dd64`)

The state a machine enters when its own shots are hitting something that is not what it aimed at.

### How it is reached

`Mech_AiOnLineOfFireBlocked` (`0041dd2c`, mech vtable `+0x64`) copies the machine's current target position to `mech+0x31e` and sets `mech+0xad`. Its one caller is the tail of `Sim_RaycastObjectList` (`00426528`), and the test there is:

```
if (something was struck && shooter has a target
    && groundRange(shooter, hitPoint) < range(shooter, target)
    && |bearing(hitPoint) - bearing(target)| < 0x2000)
        shooter->vtable+0x64(hitObject)
```

So a shot that stops on terrain or on a third object, nearer than the intended target and within 45° of the same line, tells the shooter its line of fire is blocked. The base and flyer classes leave the slot empty, so only a machine reacts.

`Ai_BeginSkirtIfBlocked` (`0041de9c`) is the gate at the top of every combat think and turns that flag into the state:

```
if (mech+0xad) {
    stashed = mech+0x4d.descriptor
    Behaviour_SetState(mech, skirting)                        // zeroes the block scratch
    mech+0x5a = stashed
    mech+0x67 = Math_DistanceBetweenPoints(mech, mech+0x31e)
    return true                                               // the think does nothing else
}
```

The state it interrupted is stashed in the scratch it just cleared, which is what makes `skirting` an excursion rather than a decision: the machine goes back to exactly the state it left.

### The state

```
if (mech+0x5e == 0) {                                                  // first tick
    mech+0x60 = (bearingTo(stash) - heading) < 0                       // which way to go round
    mech+0x5e = 1; mech+0x63 = 5000; mech+0x61 = 1
}
if (Timer_CountDown(mech+0x62) == 0) {
    mech+0x63 = 5000
    mech+0x61 = Ai_LineOfSightBlocked(mech)
    if (mech+0x61 == 0) { Behaviour_SetState(mech, mech+0x5a); mech+0xad = 0 }
    else                  mech+0x67 = groundRange(mech, stash)
}
point = mech+0x31e
if (mech+0x61 == 1) point = OffsetPointByBearing(point, bearingFrom(stash, mech) ± 0x4000, mech+0x67)
Mech_LocomotionTick(mech, (bearingTo(point) - heading) >> 6, 0x100, 0)
Mech_CenterTorsoTick(mech, 0)
```

- **It walks at a point 90° off its own line to the stash**, at the range it currently stands at — an arc around the obstruction, always to the same side, chosen once from where the machine stood when the state began.
- **The line of sight is re-tested every 5 seconds, not every tick**, and only a *clear* reading ends the state. `Ai_LineOfSightBlocked` ([`ai-navigation.md`](ai-navigation.md#line-of-sight--ai_lineofsightblocked-0041dc24)) answers 1 for anything the machine cannot get past and 2 for ground it could simply walk over, and **only the 1 gets the arc**: on a 2 the machine drives straight at the stash and crests the rise that is in the way.
- **It does not shoot and it does not steer around anything else.** The torso is centred, the throttle is at the stop, and nothing but the 5 s clock can end it.
- The stash is a *position*, taken once. The state never looks at the target again, so a machine skirting after a moving target walks to where that target was.

## `sleeping` (13) — `Mech_BehaviourSleepThink` (`0041c418`)

Release the target, `Mech_LocomotionTick(mech, 0, 0, 0)`, `Ai_UpdateWeaponsFree`. A sleeping machine stands with its throttle at zero and its radar on whatever the mission file set, and holds no target — but it is still ticked, still detectable, and still answers fire through its vtable `+0x50` like any other.

## `dead` (20) and `disabled` (21) — `Mech_BehaviourInertThink` (`0041e554`)

`Mech_LocomotionTick(mech, 0, 0, 0)`. Nothing else. The two states share the think and differ only in their descriptors' `+0x3c`.

## The circling step — `Ai_CircleStep` (`0041c72c`)

Shared by `flanking` and `attacking base`, and the only thing in the AI that alternates between two manoeuvres on a clock. It reads a hysteresis byte at `mech+0x66` and two timers in the block scratch: `mech+0x60`, the break-off timer, and `mech+0x63`, the interval between break-offs.

```
if (mech+0x60 still running) {                      // breaking off
    nearStandoff = 0; approach = -1; bearingError = 0; mech+0x66 = 0; mech+0x63 = 4000
    return
}
if (mech+0x63 expired && mech+0x288 > 100) mech+0x60 = 1000
threshold = mech+0x66 ? 0x4000 : 0x6000
if (|aspect| < threshold) {                         // the target is facing me: circle
    side  = bearingError >= 0 ? +6000 : -6000
    point = OffsetPointByBearing(target.position, bearingToTarget - 0x8000 + side, 15000)
    nearStandoff = 0; approach = 1
    bearingError = Math_HeadingToward(point, mech) - mech.heading
    mech+0x66 = 0
} else {                                            // it is not: square up
    mech+0x66 = 1
    approach = |mech.turretTwist| < 2000 ? 0 : -1
}
```

- **The circle is a point 15000 units out from the target on the machine's own approach line, rotated 33° to one side** — near enough a tangent, so the machine walks a wide arc around a target that is pointing at it and closes on one that is not.
- **Which side it circles to is the side it is already turning toward**, re-chosen every tick, so a machine that overshoots reverses its arc rather than committing.
- **The break-off costs a second and is bought with damage.** `mech+0x288` is the running total of damage taken; once it passes 100 the machine spends one second in every four reversing in a straight line with no steering at all. An undamaged machine never breaks off.
- **The hysteresis is one-sided.** The threshold is 0x6000 (135°) while the machine is circling and 0x4000 (90°) once it has squared up, so a target has to turn further to start the circle than to stop it.
- The square-up arm gates on the machine's *own* turret twist rather than on any range: it stands still while the turret is within 2000 BAM of centre and reverses while it is not, so the machine walks backwards until its hull has caught up with where its guns are already pointing.

## The transition graph

`Behaviour_SetState`'s 30 call sites, which are the state machine's whole edge list. The four sites in `004215f4`, `00421bb4`, `00422bf0` and `00422d00` install descriptors outside the mech table (`0x499cf8`, `0x499e60`, `0x499d34`) and belong to the flyer path.

| Installed by | States |
|---|---|
| `Mech_Constructor` (`00415bb0`) | `deciding`, `player`, `player fly` |
| `Mech_ComponentDamageWrite` (`00417de4`) | `disabled`, `dead`, `in limbo` |
| `Mech_AiEngageOrderedTarget` (`0041c0f4`) | `attacking flyer`, `attacking base` |
| `Mech_AiFleeCheck` (`0041cb94`) | `fleeing` ×2 |
| `Mech_AiCombatReassess` (`0041cf18`) | `attacking`, `flanking` or `facing off`, by combat rating |
| `Mech_BehaviourSearchDestroyThink`, `Mech_BehaviourPatrolThink` | `fleeing` |
| `Mech_BehaviourGuardThink` (`0041e224`) | `fleeing`, `driving off en` |
| `Mech_BehaviourSkirtThink` (`0041dd64`) | whatever it stashed |
| `Ai_BeginSkirtIfBlocked` (`0041de9c`) | `skirting` |
| `Mech_AiSelectBehaviour` (`0041eb34`) | `patrolling`, `guarding`, and the order-verb table |
| `Mech_AiOnTakingFire` (`0041f7b8`) | `driving off en` |
| The squad command handler (`00420ad4`) | `patrolling` ×3, `guarding` |

Five states have no installer of their own and can only be reached through `Mech_AiSelectBehaviour`'s order-verb table: `search/destroy`, `travelling`, `following`, `sleeping` and `ramming`. `bulldog travel` is in that table and still unreachable — see [`ai-dispatch.md`](ai-dispatch.md#choosing-a-state--mech_aiselectbehaviour-0041eb34).

## Fields this layer owns

The behaviour block's scratch (`mech+0x5a` to `mech+0x81`, zeroed by every `Behaviour_SetState`) is a union: each state lays its own fields over it, and the same bytes mean different things in two states. Only the uses below exist.

| Offset | State | Meaning |
|---|---|---|
| `+0x5a` | every combat state | The approach flag the move step wrote. No reader |
| `+0x5a` | `skirting` | The descriptor to go back to |
| `+0x5b` | `fleeing` | Side-switch countdown, 4000 ms |
| `+0x5d` | `Ai_CircleStep` | Written zero on the square-up arm. No reader |
| `+0x5e` | `skirting` | The state has started |
| `+0x5f` | `fleeing` | Which side to run to |
| `+0x60` | `Ai_CircleStep` | Break-off countdown, 1000 ms |
| `+0x60` | `skirting` | Which way round to go |
| `+0x61` | `skirting` | The last line-of-sight reading |
| `+0x61` | `fleeing` | The object being run from |
| `+0x63` | `Ai_CircleStep` | Interval between break-offs, 4000 ms |
| `+0x63` | `skirting` | Line-of-sight re-test countdown, 5000 ms |
| `+0x66` | `Ai_CircleStep` | Circling or squared up — the aspect threshold's hysteresis |
| `+0x67` | `skirting` | Range to the stashed point |

Fields outside the block:

| Offset | Type | Meaning |
|---|---|---|
| `+0xad` | byte | The line of fire is blocked. Written by `Mech_AiOnLineOfFireBlocked`, cleared when `skirting` ends |
| `+0x288` | int | Total damage taken — [`damage-system.md`](damage-system.md). Read here as the gate on the circling break-off |
| `+0x31e` | `int32`×3 | Where the target was when the line of fire was found blocked |

## Open questions

- **`mech+0x5d`**, written zero by the circling step's square-up arm and read nowhere.
- **`BASES.DAT +0x2e`'s generator and transport entries**, and why the missile tower alone states 2 when both readers only test for zero.
- **`mech+0x9e`**, set by `Sim_RaycastObjectList` when the object a shot struck is the shooter's own target. No reader found.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `skirting` is unreachable, because nothing calls mech vtable `+0x64` | The decompiler renders the slot in decimal (`*(code **)(*p + 100)`), so a search for `+ 0x64` finds nothing. Four call sites exist; the one on the mech vtable is `Sim_RaycastObjectList`'s blocked-line-of-fire test |
| A combat think chooses its own speed | Every one of them takes `±0x100` or 0 from `Ai_CombatMoveStep`. The cruise speed at `mech+0x252` is the navigation layer's and is never read in a fight |
| `Ai_CombatMoveStep`'s range tests read the bearing the state just steered to | The magnitude at `geom+0x04` is recomputed *after* both tests, so they see the bearing to the target and the steer sees the state's own point |
| `fleeing` keeps the machine it is running from as its target | It moves the pointer into the block scratch and releases the selection on its first tick. Nothing holds a target while it flees, which is why the machine is not counted among that target's holders |
| `attacking` walks a circle like `flanking` does | It builds one aim point per tick off the target's beam and steers at it; there is no timer and no alternation. The circling step is `flanking`'s and `attacking base`'s alone |

## Engine port

`MechObject.CombatStates.cs` holds the nine thinks, the geometry block, the move step and the circling step; `BehaviourState` gains a `ThinkSlot` for each; `SimWorld.Raycast` gains the blocked-line-of-fire notification.

What differs from the original, and why:

- **The block scratch is named fields, not a union.** Two states never run at once, so the aliasing carries no behaviour — except `fleeing`'s clock, which the approach flag really does rewrite in the original and which is reproduced by rounding the reload the same way.
- **`flanking` is ported and unreachable**, exactly as in retail: the type field its gate reads is zero on every chassis. It is here because `attacking base` shares the circling step, and because a modded chassis could open the gate.
- **`attacking flyer` has nothing to fly against.** `FlyerObject` answers `TargetClass.Flyer` and the acquisition can pick one, but no retail mission places an AI flyer to fight — see [`razor-flight.md`](razor-flight.md).
- **The skirt stash is a `Vec3i` and the state to return to is a `BehaviourState`**, rather than a raw descriptor pointer in the scratch.
- **`Math_OffsetPointByBearing`'s distance is an `int`.** `skirting` passes a range that does not fit the `short` the earlier port used.
- **The Turbo Pod engage `fleeing` makes is left out.** The pod's speed bonus is not modelled at all — see [`mech-locomotion.md`](mech-locomotion.md) — so there is nothing for the call to reach.
- **Every state change goes through one helper that clears the scratch**, because the original's zeroing of the block is what makes a freshly installed state start from nothing, and named C# fields do not get that for free.

Observed running mission 1: a machine on an `attacking base` order cycles `attacking base` → `skirting` → `attacking base` as its shots stop on the compound's other buildings, and can orbit the ring for a minute at a time when the building it is on has others all the way round it. That is the mechanism working as written rather than a divergence — nothing in `skirting` bounds it, since its dwell flag keeps the reassess from ever running.
