# Structure behaviour

What a `BASES.DAT` structure does per tick. Hit detection is [`hit-detection.md`](hit-detection.md) and coming apart is [`damage-system.md`](damage-system.md#going-out-of-the-fight). This doc owns the `+0x18` tick slot and the five classes that fill it.

## Timer units

Every countdown in this doc is in the simulation's own timer unit, and **it is not a millisecond**. `Math_CountdownTimerTick` (`00467944`) and `Timer_CountDown` (`004679a4`) subtract `SimTickDelta`, which `Time_BeginSimTick` (`004677bc`) sets to `clamp((elapsedMs << 8) / 125, 0x40, 0x1c2)` after waiting out its own 40 ms frame cap — Q8 with 1.0 = 125 ms, and 81 on hardware that keeps up.

So one count is 125/256 ms ≈ 0.49 ms, and a reload of 10000 expires in **about 4.9 seconds**. Reading these constants as milliseconds overstates every interval in the simulation by a factor of about two.

| Constant | Units | Real time |
|---|---|---|
| Retarget `+0x21d` | 10000 | 4.9 s |
| Firing window `004973e0` | 10000 | 4.9 s each way |
| Refire `+0x211` | 1500 | 0.73 s |
| Ground vehicle back-off `+0x223` | 3000 | 1.5 s |

## Five classes, one switch

`Base_Construct` (`00405314`) switches on the type index and installs one of five vtables. The case labels below are the switch's own.

| Class | Vtable | `+0x18` tick | `BASES.DAT` type indices |
|---|---|---|---|
| Plain | `00497940` | `Base_ThinkTick` `00403ca8` | 0-4, 7, 9, `0x0c`-`0x1c`, `0x1f`, `0x21`, `0x24`-`0x2c` |
| Radar mast | `004979d4` | the same | 5, 6, `0x1d`, `0x1e` |
| Armed | `004978ac` | `00404100` | 8, `0x0b`, `0x20`, `0x23` |
| Triple turret | `00497784` | `004045c8` | `0x22` |
| GroundVehicle | `00497818` | `0046a5d0` | `0x2d`-`0x34`, `0x37`-`0x3d` |

Six indices — `0x0a`, `0x35`, `0x36`, `0x3e`-`0x40` — match no case, so nothing is constructed. Only three slots differ across the five tables: the destructor, this one, and `GetTorsoTwistAngle`.

The library the shape comes from is picked per case too — the four Radar and four Armed cases construct from `dts\BASES_AN.DTS` and every other case from `dgs\BASES.DGS`. Nothing reads `+0x06` to decide it, though the two agree on every retail type.

## The animation threads

Whatever class it ended up, every structure then runs the same tail of `Base_Construct`:

```
for (i = 0; i < 2; i++) {
    if (i >= typeRec+0x06) continue
    structure+0x1f9[i] = ShapeInst_AddThread(this, i)     // sequence i
    AnimThread_SetPlaybackRate(thread, typeRec+0x20[i])
    if (i == 1) AnimThread_SeekToPosition(thread, 1, 6000)
}
```

So **`BASES.DAT +0x06` is a thread count**, 0, 1 or 2, and `+0x20` is a `short` pair giving each thread its playback rate. Thread `i` plays sequence `i`, and thread 1 is seeded parked a little over a third of the way through its own sequence rather than at its start.

The eight types that state a non-zero count are the eight the constructor draws from `BASES_AN.DTS`, and each of those roots carries an `ANAnimList` with exactly as many sequences as its type asks for:

| Type | Class | Root | Threads | Rates | Sequence frames |
|---|---|---|---|---|---|
| 5, `0x1d` | Radar mast | 0, 4 | 1 | 100 | 12 |
| 6, `0x1e` | Radar mast | 1, 5 | 2 | 30, 100 | 5, 12 |
| 8, `0x20` | Armed (gun) | 2, 6 | 2 | 0, 0 | 9, 12 |
| `0x0b`, `0x23` | Armed (launcher) | 3, 7 | 2 | 0, 0 | 9, 12 |

A rate of zero leaves the thread parked for something else to position it, which is exactly what an armed structure's turret seek does with both of its. The radar masts state real rates instead and spin freely — **that, and not the cell flipbook, is what turns a radar dish**: all four radar types state `-1` for their flipbook sequence and have no flipbook at all.

A thread only advances when something calls `SimObject_ApplyRootMotionIfEnabled`, and only two things do: the plain tick and the turret seek.

### The root motion is inert on retail data

`SimObject_ApplyRootMotionIfEnabled` (`00402604`) forwards to `SimObject_ApplyRootMotion` (`0040250c`), which seeds the root node to identity, steps every thread by `dt`, reads the root back, and adds what came out to the object's position and euler triple. It is the same call a HERC's locomotion makes, and for a HERC it is the whole source of translation.

For a structure it moves nothing: **none of `BASES_AN.DTS`'s eleven sequences sets the ground-movement flag**, so the transform read back is always identity. What the call does for a structure is the stepping — playing a dish's sweep, and re-posing the turret nodes a seek has moved.

## The plain tick — `Base_ThinkTick` (`00403ca8`)

Death sequence, then, while the structure still stands:

```
if (typeRec+0x24 >= 0 && Math_CountdownTimerTick(&structure+0x1f6) == 0) {
    structure+0x1f7 = typeRec+0x26                 // reload the interval
    cells[typeRec+0x24] = (cells[typeRec+0x24] + 1) % shape.sequenceCells[typeRec+0x24]
}
if (typeRec+0x06 != 0) SimObject_ApplyRootMotionIfEnabled(this, 100)
```

`BASES.DAT +0x24` is a **cell sequence index** and `+0x26` its **frame interval**, in the simulation's timer unit (see [Timer units](#timer-units) — 256 of them is a frame every 125 ms). The array it steps is the same per-sequence cell array damage moves, so an idle animation and a collapsed part are one mechanism pointed at different sequences.

Eight retail types state a sequence, all of them sequence 0 on a 256-count interval — a frame every 125 ms: 8, 9, `0x0a`, `0x0b`, `0x1a`, `0x20`, `0x22`, `0x23`. **Only two of the eight reach this function** — 9 and `0x1a`, the two that are Plain. Types 8, `0x0b`, `0x20` and `0x23` are Armed and `0x22` is the triple turret; each of those ticks steps the same cell array from its own firing path instead, as a muzzle flash rather than a loop. `0x0a` matches no case and is never built. So the free-running flipbook belongs to exactly two structures in the game.

The second arm is the animation step, for any type that states threads at all.

## The armed tick — `00404100`

A structure that has already fallen hands the whole tick to `Base_ThinkTick`, so a wrecked tower is an ordinary building again. While it stands: death sequence, then

- Retarget on a 10000-unit countdown at `+0x21d` — about five seconds, see [Timer units](#timer-units) — through `Ai_SelectTarget(this, 0x30, 0)`, reject own class, ignore bearing. Drops a target past 60000.
- Step the muzzle-flash cell on the `+0x1f6` countdown, gated on that countdown's own value at `+0x1f7` still being non-zero, and reload it **only while the cell has not wrapped back to 0**. So the flash plays the sequence through once and stops. Firing kicks it by writing 1.
- Lead the target: aim point from its vtable `+0x30`, then `Math_OffsetPointByBearing` along the target's heading by `range * targetSpeed / projectileSpeed`, the speed out of `PROJ.DAT` record 2. **Only a gun tower leads** — the launcher form never looks up a projectile speed, leaving the term zero, because its rounds track. Skipped past 40000.
- Aim through `Base_AimTurret` (`00403eec`), which also drives the turret animation.
- Fire from `(±300, 400, 0)` in the turret node's frame, both barrels, on a 1500 ms refire countdown at `+0x211`. A type whose `+0x2e` is 2 fires `Rocket_Fire(0, …)` and everything else `Bullet_Fire(2, …)`.
- Gated on the aim error being inside ±1000 in both axes, on the range being inside 40000, and on the firing window being open.

`BASES.DAT +0x2e` is read here as a **value**, not the flag [`ai-combat-states.md`](ai-combat-states.md#basesdat-0x2e) reads it as: 0 unarmed, 1 gun, 2 launcher. Retail states 1 on six types and 2 on two.

**The countdown at `+0x86` is a firing window, not a barrel selector.** Each expiry flips the flag at `+0x21b` and reloads from the pair at `004973e0`, both of whose entries are 10000 — **about five seconds**, not ten, see [Timer units](#timer-units) — so a tower fires for five seconds, holds for five, and repeats. Fire is gated on the flag being set.

**It is the launcher that rolls, not the gun.** A gun tower fires both barrels every time it is allowed to. A launcher rolls `rand & 0x1f == 0` for the first barrel and, only if that failed, again for the second — so it puts at most one round up per opportunity and usually none.

### What a structure is aimed at

The structure's vtable `+0x30` (`0040351c`) is the accessor every shooter in the game calls to decide where on an object to aim. The base form (`00411a74`) zeroes both out triples; the structure's writes `BASES.DAT +0x2c` into the Z of the offset, and callers add that to the position unrotated. **All 65 retail types state one**, 1000 to 2000 world units, so a building is never shot at the ground point its model origin sits on.

That is the last field of the record to be read; `+0x00` and the six bytes at `+0x18` are still skips.

### What the turret's AI is, and is not

`Ai_SelectTarget(this, 0x30, 0)` on a five-second timer is the whole of it. Specifically, a tower does **not**:

- **shoot back at whoever hit it.** Nothing on the structure side writes `+0x1a4` except this tick and the triple turret's; `Base_ApplyDamage` does not, so there is no structure counterpart to `Mech_AiOnTakingFire`.
- **hold a behaviour state.** A structure has no behaviour block, which is also why `Ai_SelectTarget`'s "not engaged" weight null-checks past it.
- **take squad orders**, or feed the flee check: `Ai_SumAttackerRatings` walks `GlobalMechList`, so a tower holding a machine as its target adds nothing to what that machine thinks is shooting at it.

What a tower's target *does* reach is the two readers that take `+0x1a4` off the shared base rather than off a HERC: `Ai_SelectTarget`'s scoring — where an armed structure is the only thing that can trip the "re-index a class-1 candidate as a HERC" branch — and `Mission_IsClearOfThreats`, which widens its threat radius from 80000 to 100000 for an object that holds the subject. See [`ai-targeting.md`](ai-targeting.md).

The turret also scores its own candidates against **column 1** of the four weight tables, not column 0: vtable `+0x4c` is dispatched on the asker and the structure table installs a bare `return 1`.

## The turret seek — `Base_AimTurret` (`00403eec`) and `00403d5c`

`Base_AimTurret` transforms the aim point into the turret's own frame — the object's world transform inverted, then shape part 2's, which on all four armed roots is transform 2, the barrel node hanging off the traversing base at transform 1 — and takes the aim error from `Math_EulerToward`. `00403d5c` then runs both axes:

```
in   = clamp(angle >> 3, +/-0x100)
step = Q8(Q8(|in|, in), gain[axis])
RateLimitedMoveToward(&turretRate[axis], step, rateLimit[axis])
turretAngle[axis] = clamp(turretAngle[axis] + turretRate[axis], min[axis], max[axis])
AnimThread_SeekToPosition(thread[axis], axis, (unsigned)turretAngle[axis] >> 2)
```

and finishes with `SimObject_ApplyRootMotionIfEnabled(this, 100)`, which is what re-poses the nodes it just moved.

| Axis | Error term | Sequence | Gain `004973e4` | Rate limit `004973e8` | Stops `004973ec`/`004973f0` |
|---|---|---|---|---|---|
| 0 — elevation | `EulerToward.X` | 0, 9 frames | 2000 | 200 | ±4000 |
| 1 — traverse | `-EulerToward.Z` | 1, 12 frames | 2500 | 800 | full `short` |

**The traverse is the negated axis**, and the one with no stops: a full `short` range is no limit at all in binary angle, so a base turret traverses freely and only its elevation is held, to a little over 20°. The position is seeked as an *unsigned* Q14 fraction, so a negative angle lands in the far end of the sequence rather than off its front. **A structure's turret aims the way a HERC's does**: the angle seeks a position in a full-sweep animation rather than rotating a node — see [`torso-aim.md`](torso-aim.md).

## The triple turret — `004045c8`

Not ported. Type `0x22` alone, and the only object in the game that **aims three turrets from one object**: it rewrites its own heading field to `heading + 0x1555`, evaluates three turrets `0x5554` (120°) apart, and restores the original heading at the end. Each turret runs three weapon slots against `DAT_004a9640`, an 11-`short` descriptor table: slot 0 fires `Rocket_Fire(3, …)` and slots 1 and 2 `Bullet_FireBurst(3, …)` through `WeaponMountTemplate_GetByWeaponId(8)`. Its acquisition is `Ai_SelectTarget(this, 0x10, 0x3000)` — **the `0x3000` bearing cone** [`ai-targeting.md`](ai-targeting.md) names as the base turret's.

A slot only fires while its own component is undamaged, and the missile slot installs the target on `this+0x1a4` across the `Rocket_Fire` call and clears it again straight after, purely so the round picks up a lock — the object holds no target otherwise.

Type `0x22` states no animation threads, so whatever it aims, it does not aim it by seeking one.

## The ground vehicle tick — `0046a5d0`

The mobile ground units, and the only structure class that moves. Gated on the group's first member answering `targetClass == 3`, so a ground-vehicle type dropped into a group led by anything else is an ordinary building.

```
if (typeRec+0x2e == 0) Base_ThinkTick(this)
else { 00404100(this); if (no target) TurretSeek(this, -turretAz, turretEl) }
if (!destroyed) {
    save position, pitch, heading
    GroundVehicle_Advance(this)              // 0046a70c
    SimObject_ConformToTerrain(this)       // 004029d8
    if (GroundVehicle_CollisionTest(this)) { // 0046a510
        restore the save; speed = 0
        back-off timer +0x223 = 3000, reverse flag +0x227 = (speed > 0)
    }
}
```

So a ground vehicle fights with the armed tick and moves with its own, and its block handling is `Mech_MovementTick`'s: restore the step and arm a back-off rather than detonate. **The turret seek's second caller is here** — with nothing acquired the two turret angles are fed straight back in negated, which walks the turret to centre.

- **`GroundVehicle_CollisionTest` (`0046a510`)** is `Mech_CollisionTest`'s two object sweeps standing alone — the same group `+0x14` action gate, the same asymmetric vtable `+0x5c` against `+0x7c` radius pair, and the same `Structure_GatherWalkCandidates` volume sweep behind them, whose result is the return value. It drops the machine's other two arms: there is no terrain test, so a ground vehicle drives up anything, and a block does no damage to what was hit. It does still call the blocker's vtable `+0x68`, which makes it the **second writer** of the `obj+0xb1` latch a ramming machine detonates on — see [`ai-combat-states.md`](ai-combat-states.md#the-charge--mech_behaviourramtick-0041e488).
- **`GroundVehicle_Advance` (`0046a70c`)** picks the group leader (`0046a4b8`: the first of up to four group members that is neither immobilised nor destroyed — so unlike a HERC group, a convoy promotes when the vehicle in front goes down), steers as leader or follower, then steps the position forward by `+0x220` along model Y, through the vehicle's whole frame rather than its heading alone.
- **Leader (`0046a8e4`)** drives the group's route: no waypoint after the cursor means steer 0 and speed 0; otherwise drive at it on the bearing between the two waypoints, and advance the cursor on arrival.
- **Follower (`0046a95c`)** keeps formation on the leader through its own vtable `+0x78` slot. Inside 90° of the leader's heading it matches speed — `leaderSpeed - alongTrackError >> 5`, clamped to `+0x100`/`-0x96` — and drives at a point 20000 ahead of its own post along the leader's heading, with no lateral steering term at all; outside it, it abandons the leader's heading and turns at the post itself at `distance >> 5`. The leader's speed it matches is that object's own `+0x220`, not its vtable `+0x38`, which answers zero for every structure.
- **The post is anchored on one object and rotated by another.** `Base_ApplyFormationOffset` (`00405c04`) is handed the *able* leader's position, but `Formation_RotateAndAddOffset` (`00411d64`) reaches past its caller for the group's member array slot 0 and rotates the `BFORMS.DAT` offset by that object's heading. The array is never compacted, so once the vehicle in the lead slot is destroyed a convoy is anchored on its new leader while still dressed on the wreck's last heading.
- **`SimObject_ConformToTerrain` (`004029d8`)** samples the ground at ±r forward and ±r right (`r` from vtable `+0x10`, the shape's own radius), takes pitch from `Math_Atan2Bam(2r, forward - back)` and roll from the left/right pair, and sets Z to the mean of the four samples. This is how a vehicle sits on a slope, and it is the only thing in the simulation that writes a structure's pitch and roll.

### The control law — `0046a798` and `0046a854`

Everything above decides a steer and a speed and hands the pair to `0046a798`, the ground vehicle's `Mech_LocomotionTick`:

```
if (Math_CountdownTimerTick(&this+0x222) != 0)          // a back-off is running,
    speed = this+0x227 ? -200 : 200                     // and overrides the speed, not the steer
steer = clamp(steer, +/-0x100)
Math_RateLimitedMoveToward(&this+0x220, speed, 0x1e)    // the speed slews
heading += Q8(200, steer)                               // the steer *is* the heading change
```

**There is no turn rate and no inertia in the heading**: the clamped steer becomes heading within the same tick, at a little over 200 binary-angle units at full lock. Only the speed is rate-limited. Nothing here is scaled by the tick length — the steer gain, the speed slew and the forward step are all per-call constants, as the turret seek's are.

`0046a854` is the drive-to-point both steering halves call, and the counterpart of `Ai_DriveToPoint`. Same 10000-unit arrival range, and two differences:

- **It does not steer at the point it is given.** It steers at a point offset from that one along the caller's stated bearing by `9000 - range`, so while the vehicle is further out than 9000 the aim point sits *short* of the destination, back down the incoming line, and inside 9000 it swings past. Against the leader's route bearing that pulls a convoy onto the leg between two waypoints instead of letting each vehicle cut its own corner.
- **Its steering gain is four times a HERC's** — the bearing error over 16, not over 64.

A speed of zero means "none stated" and takes `0xaa`, the same default `Ai_DriveToPoint` uses.

## Engine port

`SimObject` carries the shape instance, its animation data and the euler triple, as the original's common base carries `obj+0x34` and `obj+0x0c`; the threads themselves stay on the class that names them, so `MechObject` keeps its three and `BaseObject` an indexed pair. `SceneModelLibrary.BaseAnimation` flattens one root of `BASES_AN.DTS` — per root, because the file's eight roots are eight unrelated structures with eight separate animation lists.

Ported: the construction tail, the plain tick including `BaseObject.StepAnimation`, the turret seek and aim, the armed tick with its lead, its firing window and its two projectile forms, and the ground vehicle's whole move half — `BaseObject.GroundVehicle.cs`, which is the class gate, the two steering arms, the control law, the terrain conform and the collision sweep. A gun tower acquires, traverses onto a machine inside 40000 and hits it; a ground convoy follows its group's route, leans into the slope under it and backs off whatever it runs into.

`Target` is on `SimObject`, as `+0x1a4` is on the original's shared base, with the holder-count bookkeeping in that one setter and an `OnTargetChanged` hook for what a HERC and an aircraft each add. `BaseObject.AimPoint` is the `+0x2c` offset. Neither the `+0x9d` changed flag nor a behaviour state exists on a structure, because nothing in the original reads either.

**Not ported:** the triple turret (`004045c8`).

Three things to know about what *is* ported:

- **The turret is drawn moving, from the same node poses the simulation aims with.** `SimObject_InstallModelTransform` (`00401fe4`) and `TSGroup_BindNodeTransform` (`00476014`) are the original's pair, and they are the same for every class: the structure classes install `Shape_DrawAtDetailLevel` (`004033e4`) in their vtable's `+0` unchanged, which hands the whole shape to the shape instance's own render, and that composes each group's node transform in front of the object's. The engine's counterpart is `MissionScene.PosedTransformOf`, over the `MeshSegment`s `SceneModelLibrary.Base` now builds for an `AnimatedLibrary` type. A segment carries a `CellGate` of its own, so the per-node and per-cell splits are one split and the damage states come with it.
- **`StepAnimation` applies the root delta's translation and heading only**, dropping the pitch and roll a HERC adds. That costs nothing while the delta stays identity, which on retail data it always does.
- **The retail mission handoff exercises none of the move half.** Its one ground vehicle (type `0x38`) rides in a group whose first member is a plain building, so the class gate keeps it parked — and it stands overlapping an armed tower, which would block every step it tried to take even if the gate let it move. The path was checked by standing that vehicle clear and giving it a route of its own. The follower arm is unexercised: a second mobile ground vehicle to hold station on exists in no mission this engine can load today, because `MissionLoader` reads the `script.dat` handoff and not the campaign's `.MSN` files.
