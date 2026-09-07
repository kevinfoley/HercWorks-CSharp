# AI weapons — aiming, weapon choice and the fire decision

What an AI machine does once it has something to shoot at: bring the turret onto it, decide which hardpoint to use, and pull the trigger. Which object it is shooting at is [`ai-targeting.md`](ai-targeting.md); the state it is in while it does is [`ai-dispatch.md`](ai-dispatch.md); the mounts themselves and what firing one costs are [`weapon-mounts.md`](weapon-mounts.md) and [`weapon-firing.md`](weapon-firing.md).

**The player and the AI share the whole of the mechanism below the turret.** An AI machine reaches `Cockpit_TargetAnglesFromCameraBone` and the mount's own fire dispatch exactly as the player's trigger does. What is AI-only is the four functions in this doc, which stand where the player's hands are.

## The tail every combat state shares — `Ai_AimAndFire` (`0041ea7c`)

`Ai_AimAndFire(mech, aspect, target)` is the last thing each of the five combat thinks does, and `travelling` and `following` call it too — **a walking machine shoots at whatever it is watching**, without ever making it a selected target. A null `target` means `mech+0x1a4`.

Its whole body is a switch on the target's `TargetClass` (`obj+0x1a8`, [`target-selection.md`](target-selection.md)), choosing where on the target to put the shot:

| Class | Aim point |
|---|---|
| 0, a machine | `Ai_AimAndFireAtMech`, below — the only branch that can pick a component |
| 1, a structure | The base's **first surviving component**: vtable `+0x54` (`FUN_00406868`) with a null second argument returns the first index whose damage word is non-zero, and vtable `+0x58` places it |
| anything else | The object's own aim offset — vtable `+0x30`, the type record's `+0x68`/`+0x6a` — added to its position |

The last two go straight to `Ai_FireAtPoint`. Only the first has a turret gate.

### Aiming at a machine — `Ai_AimAndFireAtMech` (`0041e984`)

```
bearingError = Math_HeadingToward(target, mech) - mech.heading
if (|bearingError| >= typeRec+0x22) { Mech_CenterTorsoTick(mech, 0); return }
range = Math_DistanceBetweenPoints(mech, target)                    // 3D, unlike navigation
if (aimComponent >= 0 && 5000 <= range < 20000)  point = target.ComponentPosition(aimComponent)
else                                             point = target.AimNodeTransform origin, in world
Ai_FireAtPoint(mech, point, aspect, target)
```

- **The turret has to be able to reach it.** `typeRec+0x22` is the torso-twist limit, 14000 across the whole fleet, so a target more than ~77° off the hull's nose is not shot at — the turret is centred instead, which also zeroes the gun convergence below. The machine keeps walking; the steering that brings the target back round is [`ai-navigation.md`](ai-navigation.md)'s.
- **The aim component is only used at conversational range**, 5000 to 20000 units — 30 m to 120 m. Outside that band the AI shoots at the target's aim node and takes whatever component the hit test gives it. `Mech_AiSelectAimComponent` picks the component; see [`ai-targeting.md`](ai-targeting.md).
- Range here is `Math_DistanceBetweenPoints`, the 3D form. Every *navigation* range in the AI is the ground-plane one; every *weapon* range is this.

## The fire decision — `Ai_FireAtPoint` (`0041f5a0`)

```
range = Math_GroundDistanceBetweenPoints(mech, point)
weapon = mech+0x2ac                                                  // the latched weapon
if (weapon == 0 || !weapon.CanFire() || !WeaponMount_RangeAllows(weapon, range)) {
    if (mech+0xb5) { weapon = 0; mech+0xb5 = 0 }                     // suppressed for this tick
    else           { weapon = Ai_ChooseWeapon(mech, aspect, range, target); mech+0x2ac = 0 }
}
if (weapon == 0) { Cockpit_TargetAnglesFromCameraBone(mech, point); return }   // aim, do not fire

lead = target.Speed()                                                // vtable +0x38
if (weapon.Projectile.Speed > 0 && lead != 0)
    Math_OffsetPointByBearing(point, target.heading, range * lead / weapon.Projectile.Speed)
if (group.Side != 0 && (spread = AiAimScatter[difficulty]) != 0)
    point += (rand(spread), rand(spread), rand(spread))

residual = Cockpit_TargetAnglesFromCameraBone(mech, point)           // slews the turret
if (weapon.AmmoType != 5 || (|residual.yaw| < 1000 && |residual.pitch| < 1000))
    weapon.Fire(mech)
    if (weapon.template+0x56 is 6 or 22) mech+0x2ac = weapon         // ELF, ELF2
```

Six things it settles.

**Aiming happens whether or not anything fires.** The turret is slewed on the tick a weapon is chosen and on the tick none is, so the AI tracks continuously and shoots intermittently.

**Only a gun waits for the turret.** `AmmoType` 5 is `WeaponMount_GetAmmoType`'s "not a launcher" ([`weapon-mounts.md`](weapon-mounts.md)); those fire only once the residual aim error is inside 1000 BAM (5.5°) in *both* axes. A launcher fires the moment it is chosen — the round steers itself, so pointing it is enough.

**The lead is exact, and only a travelling shot gets one.** `range × targetSpeed ÷ projectileSpeed` along the target's own heading, from the `PROJ.DAT` record's `Speed` at `+0x0a`. A `Beam` record carries speed 0 and so takes no lead, which is right; a structure returns speed 0 from vtable `+0x38` and takes none either.

**The scatter is Cybrid-only and one-sided.** `group+0x12` is the group's side, so a machine in the *player's* squad never has its aim perturbed at all. The table at `0049a30c` is indexed by the difficulty level (`004a9ee0`, the same global `Damage_ScaleByDifficulty` reads) and holds `1000, 800, 400, 200, 0` — the enemy shoots straighter the harder the game is set. `Math_RandomBelow` draws in `[0, bound)`, so all three components are displaced in the **positive** direction only; see [`KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

**`mech+0x2ac` is the ELF latch.** Only weapon ids 6 and 22 are kept, and only while the mount stays ready and in range. That is what lets an AI machine sustain an ELF burst across ticks instead of re-rolling its choice each one — `ElfMount_CanFire`'s sustain clause needs the mount fired on the previous tick.

**`mech+0xb5` costs exactly one selection.** Its only writer is `Rocket_HomingSteer` for a subtype-3 (EO) missile, once per tick of flight — see [`rockets.md`](rockets.md). So an AI machine that has an EO missile in the air fires nothing else while it steers, which is the machine's equivalent of the pilot flying it. It does not stop a *latched* weapon: the flag is only tested on the path that re-chooses.

## Choosing a weapon — `Ai_ChooseWeapon` (`0041f358`)

Walks every mount and scores it. Highest score above a floor wins; nothing above the floor means nothing fires this tick.

```
best   = Math_MapRange(mech+0x2aa, 0, 0x400, 150, -100)          // the floor: fear lowers it
frontal = |aspect| <= 0x3fff
shield = target.ShieldByHeading(frontal)                          // vtable +0x34
jitter = target.TargetClass == 0 ? 35 : 30
for each mount:
    if (mount.Destroyed) continue
    kind = mount.AmmoType
    inRange = WeaponMount_RangeAllows(mount, range)
    if (kind == 0 && inRange && !mech.Scanner && mech+0x26b == 0) mech.Scanner = true
    if (!inRange) continue
    if (kind != 5 && kind != 3 && manager+0x0a[kind] == 0) continue         // no missile lock
    if (!mount.CanFire()) continue
    if (shield == 0)             score = proj.DamageArmor
    else if (shield < proj.DamageShield) score = proj.DamageArmor + 10000
    else                         score = proj.DamageShield
    score = Q10(100, score) - Q10(1000, template+0x34)
    score += rand(jitter) * rand(jitter)
    if (score > best) { best = score; chosen = mount }
```

**The floor is the machine's fear.** `mech+0x2aa` is written by `Mech_AiFleeCheck` alone — 0 at construction, then 1000, 600 or 300 as it climbs ([`ai-targeting.md`](ai-targeting.md#the-flee-check--mech_aifleecheck-0041cb94)). Mapped through `[0, 1024] → [150, −100]`, a calm machine needs a score over 150 and a frightened one will fire almost anything. **A tick where nothing clears the floor is a tick where the machine aims and does not shoot**, so the floor is the AI's rate of fire as much as its taste.

**The +10000 is the whole scoring model.** Retail costs at `template+0x34` run 500–600 for a launcher, 150 for a beam, 10–30 for an autocannon, 5 for an ELF; the damage term is scaled by 100/1024 while the cost is scaled by 1000/1024, so a launcher's ~156 of damage credit against its ~488 of cost is deeply negative. **A launcher is only ever worth firing on the shot that breaks the shield**, which is exactly when the bonus applies. Once the shields are down the AI falls back to guns, whose costs are small enough to stay positive on armour damage alone.

**The jitter is larger than the signal.** Two independent draws below 35 multiplied together average 289 against deterministic terms in the tens. The choice is therefore mostly noise, biased by the damage-versus-cost term and decided outright by the shield-break bonus.

**Missile lock is a hard gate.** `manager+0x0a` is the per-subtype lock array ([`missile-lock.md`](missile-lock.md)); subtypes 3 (EO) and the non-launcher 5 bypass it, every other launcher needs its own subtype locked before it can even be scored.

### Running dry — `mech+0xa5`

When every mount is either absent or destroyed, and the flag is not already up:

```
mech+0xa5 = 1
if (mech+0x1b6) Action_Fire(mech+0x1b6)          // the object's own mission action
if (DAT_004a9ee7 < 1000) DAT_004a9ee7 = 1000     // the radio cooldown
```

`mech+0xa5` is **"this machine has no weapons left"**, not a damage latch. Every "dead or dying" test in the AI reads it beside `+0xa4` and `+0x99`, which is why a disarmed machine flees, is abandoned as a target, and is skipped by the acquisition tier — see [`ai-targeting.md`](ai-targeting.md).

### The engagement envelope — `WeaponMount_RangeAllows` (`0040e5f8`)

`template+0x2c < range < template+0x30`. The upper bound is the weapon's range, 15000–75000 across the table; **the lower bound is zero for every one of the 33 retail templates**, so a minimum range exists in the format and never bites. See [`../formats/weapons-dat-sim.md`](../formats/weapons-dat-sim.md).

The same test gates the ELF latch at the top of `Ai_FireAtPoint`, and the range it is asked about there is the ground-plane one, not the 3D range `Ai_AimAndFireAtMech` measured a moment earlier.

## Radar, not weapons free — `Ai_UpdateWeaponsFree` (`0041c3c8`)

`mech+0x96` is the PASSIVE/ACTIVE radar mode ([`target-selection.md`](target-selection.md#radar-mode)), and this function is what an AI machine's radar switch is wired to:

```
mech+0x96 = mech.Group.Leader.IsPlayer ? mech+0xb2 : mech+0x97
```

`mech+0x97` is the mission file's own per-mech flag — `.MSN` row #12 `+0x08`, `script.dat` block 7 `+0x00` — so **the mission author decides whether a Cybrid patrol walks its route lit up or dark**. `mech+0xb2` is the player's squad radar order.

Four other places write the same byte, and together they are the AI's radar policy:

| Writer | Effect |
|---|---|
| `Ai_UpdateWeaponsFree`, from every navigation state | The standing setting, re-asserted each tick |
| `Mech_BehaviourTravelThink` / `Mech_BehaviourFollowThink` | ACTIVE while there is anything worth watching, otherwise the standing setting |
| `Mech_AiCombatReassess`, `Mech_BehaviourGuardThink` | ACTIVE on entering a fight, unless in the player's squad |
| `Ai_ChooseWeapon` | ACTIVE when a SARH launcher (subtype 0) comes into range — that class of missile needs its own illumination |
| `Mech_DirectFireHitTest` | ACTIVE when hit by a subtype 0 or 1 round; **PASSIVE, and held there for 6000, when hit by a subtype 2 (ARM) round** |

The last is `mech+0x26b`, and it is the anti-radiation missile working: a machine that takes an ARM hit goes dark and stays dark long enough for the seeker to lose it. It is also the one thing that can stop the SARH clause above from lighting a machine up.

## Gun convergence — `Mech_ConvergeGunsOnRange` (`0041a74c`)

Every hardpoint is toed in so that its shots cross the sight line at the range the turret is currently aiming at. It runs at the tail of `Mech_TorsoPitchTick`, for the player and the AI alike, and its argument is that tick's third parameter — the 3D distance to the aim point, which `Cockpit_TargetAnglesFromCameraBone` supplies.

```
for each mount:
    if (range == 0) { mount+0x24 = mount+0x28 = 0; continue }
    muzzle    = WeaponMount_MuzzleOffset(mount)
    muzzle.z -= typeRec+0x66                                  // the sight line's own height
    euler      = Math_EulerToward((0, range, 0), muzzle)
    mount+0x24 = euler[0]; mount+0x28 = euler[2]              // pitch and yaw; roll dropped
```

`WeaponMount_PrepareShot` is the consumer, and it applies each half only when the corresponding gate is clear:

```
if (mount+0x5f == 0 || mount+0x5b == 0) {
    aim = (mount+0x5b == 0 ? mount+0x24 : 0, 0, mount+0x5f == 0 ? mount+0x28 : 0)
    boneFrame = BuildEulerRotationMatrixQ14(aim) * boneFrame
}
```

`mount+0x5b` and `+0x5f` are shape-part lookups from the hardpoint's `.GL` `+0x02` and `+0x04`, and `WeaponMount_CtorBase` writes **zero** for a negative record value. Both fields read −1 on every retail chassis, so both gates are clear and **the convergence is live on all of them** — a machine's guns really do toe in and out as its turret walks a target through depth. Centring the turret passes range 0 and squares them up again.

## Mech and mount fields this layer owns

| Offset | Type | Meaning |
|---|---|---|
| `+0x92` | 0x25 B | A block of one-byte flags. Every field below from `+0x96` to `+0xb5` is an index into it, which is why none of them appears as a plain `[reg+disp]` access anywhere in the image |
| `+0x96` | byte | Radar mode, PASSIVE/ACTIVE — [`target-selection.md`](target-selection.md) |
| `+0x97` | byte | The mission file's standing radar setting for this machine |
| `+0xa5` | byte | No weapons left |
| `+0xb2` | byte | The player squad's radar order. Written by the squad command handler (mech vtable `+0x28`) and the Heads-Down command screen; the command path is [`ai-squadmates.md`](ai-squadmates.md)'s |
| `+0xb5` | byte | Skip weapon selection for one tick — an EO missile is in the air |
| `+0x26b` | short | Radar-silence countdown, 6000 after an ARM hit |
| `+0x2aa` | short | Fear, from `Mech_AiFleeCheck`. The weapon-score floor is mapped from it |
| `+0x2ac` | ptr | The latched mount, ELF and ELF2 only |
| `mount+0x24`, `+0x28` | short | Convergence pitch and yaw |
| `mount+0x5b`, `+0x5f` | int | The two convergence gates, from `.GL +0x02`/`+0x04` |

## Open questions

- **The aspect angle is computed and never used.** `Ai_BuildCombatGeometry` (`0041e758`) and both travel thinks build `bearingFromTargetToMe − target.heading + target.turretTwist` and hand it down two calls, and its only consumer is the shield-facing test — which is broken (below). Nothing else reads it. The sign on the twist term is also wrong for the reading the expression otherwise invites, and with no live consumer there is no behaviour to check it against.
- **`template+0x34`.** Plainly a per-shot cost the AI weighs against damage, and the retail values order the arsenal sensibly, but nothing else in the image reads it, so what units it is in is not recoverable.
- **`template+0x2c`.** A minimum engagement range, zero throughout retail data.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `mech+0x96` is a weapons-free flag, and `Ai_UpdateWeaponsFree` is the AI's trigger gate | It is the radar mode. `Rocket_HomingSteer` homes an ARM on it, `Mech_DirectFireHitTest` clears it on an ARM hit, and the detection sweep reads it as the scanner. Nothing in the fire path consults it. The mission-file field feeding it is the mission's radar setting, not a rule of engagement |
| `mech+0xa5` is a third damage latch beside `+0xa4` and `+0x99` | `Ai_ChooseWeapon` is its writer, and it means the machine has no working hardpoint left. It sits beside the damage latches in every liveness test because a disarmed machine is as finished as a crippled one |
| The AI weighs a weapon against the facing it is actually shooting at | `Ai_ChooseWeapon` evaluates the front/rear test itself and passes the **boolean** where `Mech_GetShieldByHeading` expects a heading. Both 0 and 1 fall inside that function's front quadrant, so the front shield is what comes back however the target is oriented. See [`KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md) |
| The per-hardpoint aim rotation in `WeaponMount_PrepareShot` is identity on retail chassis because `.GL +2` and `+4` read −1 | The constructor maps a negative record value to **zero**, and zero is what opens the gate. −1 in the file is what makes convergence run, not what disables it |
| Gun convergence has no traced consumer | `WeaponMount_PrepareShot` reads `mount+0x24`/`+0x28` and composes them into the firing bone's frame |
| `travelling` and `following` only point the turret at their look-at object | Both call `Ai_AimAndFire`, the same tail the combat states use. The look-at is not a *selected* target, but it is shot at |

## Engine port

`MechObject.Weapons.cs` holds `AimAndFire`, `FireAtPoint` and `ChooseWeapon`; `MechObject.Torso.cs` gains `TrackWorldPoint`, the port of `Cockpit_TargetAnglesFromCameraBone`, and the convergence pass; `WeaponMount` gains the convergence pair and `RangeAllows`.

What differs from the original, and why:

- **`mech+0x96` is `MechObject.Scanner` throughout.** The navigation slice ported it a second time as `WeaponsFree`; the two were the same byte and are now one property.
- **`mech+0xa5` is `MechObject.Disarmed`, and the AI's liveness tests read it through `SimObject.OutOfAction`** rather than through `Neutralised`. The detection sweep, the player's target selection and the group's completion test all read the latter, and none of them consults `+0xa5` in the original.
- **The one-sided aim scatter is reproduced**, since it is what the retail enemy's aim actually does. `SimWorld.Difficulty` indexes the table and nothing sets it, so the engine runs on entry 0 — the widest scatter of the five.
- **The mission action a machine fires on running dry is not.** `Ai_ChooseWeapon` calls `Action_Fire` on `mech+0x1b6`, and the engine has no per-object action to fire; the latch itself is set.
- **The gun convergence runs for the player too**, which is the original's arrangement: the range it converges on is the distance to the selected target, and centring the turret squares the guns up.
