# DBSIM.EXE damage system — shields and the direct-fire/explosive damage pathways

Reverse-engineered from `DBSIM.EXE` disassembly (Ghidra project `ES2Recon`). All addresses are DBSIM.EXE virtual addresses. Confirmed against the official *Earthsiege 2 - On-Line Manual.pdf* where noted. See [`weapon-firing.md`](weapon-firing.md) for how a shot gets here in the first place, [`projectiles.md`](projectiles.md) for the travelling `Bullet` family's own lifecycle, [`dbsim-physics-notes.md`](dbsim-physics-notes.md) for movement/collision/rocket math, [`../formats/terrain-heightmap.md`](../formats/terrain-heightmap.md) for the terrain heightmap this system's ground-impact checks query, [`component-damage.md`](component-damage.md) for what happens once damage reaches a component — the `.DMG` health record, the cascade, going out of the fight, salvage — and [`weapon-damage-types.md`](weapon-damage-types.md) for `PROJ.DAT`'s per-weapon damage shape and weapon-mount destruction.

How a weapon's fire turns into a mech taking damage has two different pathways — **direct fire** (deterministic, single component, shield-gated) and **explosive/area-of-effect** (random, multi-component, distance falloff, also shield-gated but via a separate parallel implementation) — that share a common raycast entry point and converge on the same final health-writing primitive.

## The shared raycast — `Sim_RaycastObjectList` (`00426528`)

A generic ray-vs-live-object-list query, not weapon-specific. Nine call sites in four functions: the launcher round's per-tick step (`Rocket_TickUpdate`, `0040a538`, once), bullet per-tick (`Bullet_TickUpdate` (`0040b124`), twice) and burst-fire (`Bullet_FireBurst` (`0040bf74`), once, below), and a flyer's airframe contact probes (`Razor_MovementTick`, five, see [`razor-flight.md`](razor-flight.md#contact-probes)). A single raycast primitive reused for weapon hit-scan **and** obstacle sensing — most likely `objlist.cpp` (shares the global live-object list, `DAT_004a9b7c`/`DAT_004a9b82`, with the confirmed-`objlist.cpp` functions at `0x004281b0`/`0x004282f8`; not confirmed by a direct assert-string tie). Confirmed **not** `fire.cpp`.

Walks the live-object list; for each candidate that passes the filter below, calls that object's vtable method at `+0x20` — for a mech, `Mech_DirectFireHitTest` (`00418ba8`), the direct-fire hit-test-and-damage function below; for a structure, `00405038`, and for a flyer, `Flyer_DirectFireHitTest` (`00421c8c`), both in [`hit-detection.md`](hit-detection.md). **The hit test and the damage application are the same call** — there is no separate "apply damage" step visible from the caller's side. `Sim_RaycastObjectList` also makes a second, unrelated vtable call per candidate (`+0x50`, `Mech_AiOnTakingFire` (`0041f7b8`)) — AI threat-tracking ("this object just took fire, update who it thinks is attacking it"), not damage.

**Reaching the shooter's own selected target is a mission event**, and the two halves land on opposite objects: the *shooter's* engaged flag `obj+0x9e` is raised (`0042671f`) and the *struck* object's engagement action at `+0x1b2` fires. So shooting at what you have boxed engages it with nothing in detection range of it — the other way into mission-objective condition 6 besides `Detection_Sweep`'s closing test. The action is gated on the struck object's `+0xa2`, a per-tick latch `Mech_PerTickSystemsUpdate` raises (`0041abd8`) on whatever the machine's targeting-computer pod holds a lock on and `Sim_DetectionTick` clears on everything at the end of the pass; since `Action_Activate` is itself one-shot, that gate can only ever suppress a duplicate inside one tick.

Hitting that same target is also what the blocked-line-of-fire report excludes: the shooter's `+0x64` is called with whatever stopped the ray, and the branch that recognises its own target passes null instead, so a machine does not report its target as blocking the shot at it.

Four properties a port has to preserve:

- The **candidate filter** is three tests, all before the vtable call: not the shot's owner (`shotData+0x0e`), not the object at `shotData+0x14`, and **not an object whose mission group still carries an action** (`*(int*)(obj[+0x45] + 0x14) != 0`). The middle one is a second exclusion, and the only caller that fills it is `Razor_MovementTick`, which puts the flyer itself there (`00419bef`) so its own airframe probes cannot strike it. The three weapon callers leave that slot alone: it is **uninitialised stack**, not an empty field, so the comparison runs against whatever the previous frame left — a port should write the shooter or null rather than drop the test. The last one matters — see [`hit-detection.md`](hit-detection.md). The team byte (`obj[+0x45][+0x12]`) is read only *after* a hit, for the AI notification and friendly-fire warnings; it does not gate the hit itself.
- Before the sweep it **caches the world-to-muzzle transform** in the ray record at `+0x0a` (copy, transpose, negate-and-rotate the translation), which is the frame every hit test works in.
- It **shortens the ray to each hit** (`rayRecord+0x04`) rather than stopping at the first, so a candidate found later but nearer wins — every subsequent candidate is tested against the shortened length. It breaks early only for a hit inside 500 units. Because damage is applied inside the hit test, a candidate that is later superseded has still taken its damage.
- It opens with a **ray-versus-terrain query**, `Sim_RaycastTerrain` (`00428048`) → `Terrain_RayWalk` (`0046e87c`) against `ActiveHeightGrid`. A ground hit clips the ray before any object is tested, so a beam cannot shoot through a hillside. The ray record's own 200 is passed down as a walk radius and has no effect on the result: `Terrain_RayWalk` forwards it to `Terrain_CellSurfaceIntersect` (`0047068c`) — its only destination — which never reads that parameter. See [`../formats/terrain-heightmap.md`](../formats/terrain-heightmap.md#ray-versus-terrain--terrain_raywalk-0046e87c). Returns `hitDistance + 1`, or 0 for a clean miss.

`bullet.cpp`'s per-tick and burst-fire functions (found by walking `Sim_RaycastObjectList`'s other callers):
- **`Bullet_TickUpdate`(instance) — per-bullet-instance tick.** Structurally parallel to the rocket's `Rocket_TickUpdate` (periodic seeker-slot reacquire, age counter, lifetime-expiry check) but bullets age at a fixed baked-in rate, `Math_IntegrateRateOverTick(0x200)` (`00467820`), not a per-type-record rate field. For bullet **type 9 specifically** (a distinct type index, not bullets as a class), there's a near-miss short-circuit before the raycast, and on a confirmed hit it calls the explosion function directly with a `4000`-unit blast radius — see "Explosive damage" below. Every other bullet type just calls the raycast and, if it returns a hit, marks itself for removal — the direct-fire damage already happened inside that raycast call.
- **`Bullet_FireBurst`(missileId, shotTransform, range, owner, power) — fire-burst / tracer spawner.** Calls the raycast once up front to get the actual (possibly shortened) travel distance, then — if that distance exceeds 5000 game units — splits the visual tracer into multiple 5000-unit segments (`BeamTracer_Ctor` (`0040b804`) spawns each), otherwise spawns one tracer for the whole distance. Pure rendering; the hit-distance math is already resolved by the raycast call at the top.

## Direct-fire damage: armor-then-part, deterministic, shield-gated

**`Mech_DirectFireHitTest` (`00418ba8`, mech vtable `+0x20`), called by `Sim_RaycastObjectList` on every raycast candidate.** In order:

1. **Coarse range check.** Rejects the candidate outright when `200 + rayLength + typeRecord[0x1a] < |muzzle - mech|`, keeping the transform work off everything nowhere near the shot.
2. **Geometry and shield absorption — `Mech_ShieldAbsorb_DirectFire` (`00413cc4`).** The mech's centre of mass (`typeRecord+0x18` above its origin) is brought into **muzzle space**, where the ray is the Y axis, so the hit is two comparisons: the centre in front and within the ray's remaining length (an *unsigned* compare, which is what rejects anything behind the muzzle), and its 2D distance off the axis under the hit radius `typeRecord+0x1a`. Then `absorbed = min(incomingDamage, remainingShieldInZone)`, with both the incoming damage and the zone's charge reduced by it. A **hard cap, not an all-or-nothing threshold** — a hit worth more than the zone holds drains it to zero and carries its excess straight through in the same hit. The facing is picked by **where the muzzle sits in the mech's frame**, so it is the shooter's bearing that exposes the rear array. See "The shield system" below.

It returns the ray's entry point into the hit cylinder, `alongAxis - (radius - offAxis)` floored at 1, which is what `Sim_RaycastObjectList` shortens the ray to. **A fully absorbed shot still returns a hit distance and still stops the ray** — shields do not let fire through to whatever is behind — and the caller spawns only a hit-spark effect.
3. **Component selection — `Mech_SelectStruckComponent` (`0040c9d4`).** Only reached if some damage penetrated shields. Tests the mech's `col\<NAME>.COL` hit-sphere model cluster by cluster to find the ONE component struck — **not** a random roll, unlike the explosion path. Decoded and ported in [`hit-detection.md`](hit-detection.md). **Missing every sphere is a clean miss**: the shield cylinder is only a gate, and the shot passes on to whatever stands behind.
4. **Damage application — `Mech_ApplyDirectFireDamage` (`004188c8`).** Takes `SplashFactor` off the top (`Math_Q10Multiply(shotData[+8], armorDamage)`, Q10 — see [`weapon-damage-types.md`](weapon-damage-types.md#weapon-type-effectiveness)) and **splits** the shot: the remainder goes to that component's general health via the mech's `+0x74` slot (`Mech_ComponentDamageWrite` (`00417de4`), see [`component-damage.md`](component-damage.md)), and the split-off share, when non-zero, becomes a 500-unit secondary explosion through this same machine's `+0x70` — a direct call, not a sweep, so it cannot reach anything standing beside it. Health is bucketed into 8 levels (`>>5` of the 0–256 Q8 percentage) for state-transition/alert purposes, plausibly matching the manual's 5-color status system (Green/Yellow/Orange/Red/Gray). A bucket change also rolls to knock out a weapon mount at that component — see [`weapon-damage-types.md`](weapon-damage-types.md#weapon-mount-destruction).

This is fundamentally different in shape from the explosion path: precisely-aimed weapons hit what you aimed at; explosions spray damage around imprecisely.

**The three type-record fields this path reads map onto `HercSimDat`.** `MechType_InitOne` reads the `.DAT` as one block at record offset 2, so runtime offset = file offset + 2:

| Runtime | File | Field | Retail values |
|---|---|---|---|
| `+0x18` | 22 | hit-cylinder centre height (`Unk22_Val750Razor0`) | 1000 heavy/medium, 750 light, 0 RAZOR |
| `+0x1a` | 24 | hit radius (`AiAimTargOffset`) | 2500 heavy, 1500 medium, 1000 SPIDER |
| `+0x4a` | 72 | leg count (`ModelLegsTotal`) | 2, except PITBULL's 4 |

The radius is deliberately generous — it only has to be wide enough that nothing which could hit is rejected, since the sphere model behind it decides. `AiAimTargOffset` was a guessed name; these two consumers identify it.

`Mech_DirectFireHitTest` is invoked only polymorphically, as `obj[+0x20](...)`. **Beams do reach it through `Sim_RaycastObjectList` and nowhere else** — the beam dispatch was traced end to end in [`weapon-firing.md`](weapon-firing.md), and it calls `Bullet_FireBurst`, which calls the raycast; no path applies damage directly to a locked target.

## Explosive damage — the `+0x70` slot

Every simulation object carries a vtable `+0x70` that says what an explosion does to it. **Three functions call it, at four call sites, and only one of them is a sweep:**

| Caller | Shape | Damage | Radius |
|---|---|---|---|
| `Damage_ExplosiveBlastSweep` (`00426a20`) | sweep of the live-object list | the caller's | the caller's |
| `Mech_ApplyDirectFireDamage` (`004188c8`) | direct, on the struck object only | the shot's `SplashFactor` share | 500 |
| `Mech_CollisionTest` (`00418f74`) | direct, on both parties — the two sites | momentum difference | 1200 |

The three implementations share the falloff's shape and nothing else. All take `(this, short damage, int *hitPoint, short blastRadius, void *attacker)`.

### The sweep — `Damage_ExplosiveBlastSweep` (`00426a20`)

Walks the live-object list; skips an object whose mission group still carries an action (see [`hit-detection.md`](hit-detection.md#the-sweep--sim_raycastobjectlist-00426528)) and the excluded object (`param_5`); calls `+0x70` on everything whose `distance - obj[+0x5c] < blastRadius`. **Surface to centre, not centre to centre** — the object's body radius is subtracted first. Nothing stops, shortens or orders the sweep: a wall between two objects shields neither.

Exactly **3 call sites**, all terminal events rather than routine fire:

1. **`Meteor_Tick` (`00409d2c`)** — the **drop pod** landing, not a missile. Detonates `(pos, 3000, 10000, 0, null)` the instant its altitude dips below the terrain. See [`mission-deployment.md`](mission-deployment.md).
2. **`Bullet_TickUpdate` (`0040b124`), the `type == 9` branch** — the **Plasma cannon**, and a bullet subtype rather than bullets as a class: `(pos, 4000, armourDamage, owner, null)`. It empties its own shot record first, so the blast is the whole of the weapon; see [`projectiles.md`](projectiles.md#the-plasma-branch).
3. **`Mech_BehaviourRamTick` (`0041e488`)** — an **AI ramming attack**, and the move slot of behaviour state 17, which a mission group reaches through order verb 1. A block — or the "something ran into me" latch at `mech+0xb1` — detonates `(pos, 3000, 2000, 0, self)` and then finishes off **every one of its own 29 components with a flat 32000** through `+0x74`: a guaranteed self-destruction, no roll and no falloff, with the blast excluding the machine itself. The state and the two functions are [`ai-combat-states.md`](ai-combat-states.md#ramming-17--mech_behaviourramthink-0041e570)'s.

### Where a component stands — the `+0x58` slot

The falloff is measured from the component, not from the object's origin, and `+0x58` is what answers where the component is. Signature `(this, short componentIndex, int *out)` for both real implementations.

- **A mech — `Mech_ComponentPosition` (`0041b60c`).** Reads the `HercPiece` fields at `+0x0c` (a shape part id) and `+0x0e` (a pointer to a point). A piece with a null point answers **the machine's own position**, which is at its feet. Otherwise the point goes through the machine's world transform, composing the part's posed node transform first when the part id is `> 0` — note strictly greater, so part 0 would stay in the object frame; no retail `.COL` uses part 0. A part the shape does not have falls back to the identity transform at `006c572c`.

**Those two fields are not in the `.DMG` file** — the loader writes `-1` and null (`HercPiece_ReadRecord`, `0040cff8`), and `Mech_ConfigureLoadout` (`004175dc`) fills them in from the `.COL`: `Collision_CollectComponentAnchors` (`0040cb84`) walks every cluster and emits `(componentIndex, nodeIndex, &cluster.boundCentre)`, and `HercPiece_BindComponentAnchors` (`0040d284`) writes each triple into the piece its component index names. The write is unguarded, so **the last cluster naming a component wins**; every retail mech `.COL` names each component exactly once, so it never bites. The point is the cluster's load-time *bounding-sphere* centre, not any one sphere.

A mech `.COL` names 6–16 of the 29 slots, so most of a HERC — every internal, and on most chassis the shoulders — is measured from the ground under the machine rather than from where the part is.
- **A structure — `Base_ComponentPosition` (`00406808`).** The component's own point from `BASES.DAT` (see [`hit-detection.md`](hit-detection.md#datbasesdat-runtime-record)) through the structure's world transform. No node to resolve: a building's parts do not move. It is **not** the `BASECOL.DAT` geometry a shot is tested against, and several types put the blast point above the spheres.
- **A flyer** inherits the base class's `00411a3c`, which is `push ebp; pop ebp; ret` — it never writes its out-parameter, and nothing reaches it, but not because the slot goes unused. Of the nine `+0x58` call sites, six are already on a mech or a structure and one is a weapon mount's own unrelated vtable. The two that dispatch on an arbitrary object are both fenced off by a component index that comes back `-1` for anything that is not a mech:
- `Rocket_HomingSteer` (`0040a2fd`) asks only when `rocket+0x5a >= 0`, and `Rocket_Fire` fills that from the target's `+0x54`, which for a flyer is `00411a44` — a bare `return -1`.
- `TargetingPod_ResolveAimPoint` (`0040e4dc`), behind `Player_ResolveTargetAimPoint`, asks at `0040e530` only when `pod+0x7d >= 0`, and `TargetingPod_ResetComponentLock` (`0040e484`) writes `-1` there for every target whose `TargetClass` (`obj+0x1a8`) is not 0 — see [`target-selection.md`](target-selection.md#component-targeting--the-targeting-pod).

A port that resolves a component position generically has to keep one of those guards, or it will ask a flyer where its component 0 is and get an answer the original never had to produce.

### A mech — `Mech_ApplyExplosiveDamage` (`004187d0`)

1. **Facing.** The bearing to the hit point against the machine's own heading, front inside `0x4000` either way — the same ±90° window `Mech_GetShieldByHeading` uses. Unlike the direct-fire path it is the machine's facing that decides, not the geometry of a ray.
2. **Shields — `Mech_ShieldAbsorb_Explosive` (`00413c68`)**, the explosion path's own implementation of what `Mech_ShieldAbsorb_DirectFire` does for direct fire. Computes `scaledDamage = Math_Q10Multiply(1000, damage)`, takes it out of the chosen zone, and returns `overflow × 0x400 / 1000` if the zone went negative and 0 otherwise. A facing that swallows the blast ends it here. Confirms shields gate both pathways, matching the manual: "shields cause missiles to explode on contact, preventing most of their blast power from reaching the HERC's armor."

**The two scales are exact inverses.** Input is multiplied by `1000/1024` and output by `1024/1000`, so a blast of face value *d* removes `0.977d` from the facing and, against an empty one, hands the components behind it *d* — the same exchange rate direct fire has, and `PROJ.DAT`'s two damage figures are directly comparable. The return is a `short`, so an overflow past 32767 would wrap; the largest blast in the game is the drop pod's 10000, which reaches about 6400 against a full facing.
3. **A roll per component**, over a fixed 29 slots. Each live one draws `rand & 0xfff < 0x802` (≈51%) to be considered at all, so two identical blasts do not wreck the same parts.
4. **Distance and falloff.** The component's `+0x58` position against the hit point; inside `blastRadius` it takes `overflow × (blastRadius - distance) / blastRadius` through `+0x74`.

### A structure — `Base_ApplyExplosiveDamage` (`00404f20`)

No shields, no facing, and its parts stand where the type record says.

```
if (typeRec[+0x1e] != 0) return                        // invulnerable
if (wreck-with-hulk) return                            // same test Base_DirectFireHitTest opens with
if (typeRec[+0x38] == 0) return                        // no BASECOL.DAT model -> immune to blasts
for (i = 0; i < typeRec[+0x12]; i++) {
    if (!alive[i]) continue
    if ((rand & 0xfff) >= 0x1004) continue             // cannot fail -- see below
    d = |vtable+0x58(i) - hitPoint|
    if (d < blastRadius) vtable+0x74(i, (blastRadius - d) * damage / blastRadius, attacker)
}
```

**The per-component roll can never fail.** `0x1004` is one above the largest value the `0xfff` mask can produce, so every live component is measured. The draw still advances the shared generator.

**A type with no `BASECOL.DAT` model is immune to explosions** however close they go off, even though direct fire still hurts it through the shape's collision volume — 40 of the 65 retail types.

### A flyer — the base slot `SimObject_ApplyExplosiveDamage` (`00411b3c`)

The flyer class does not override `+0x70`; it inherits the shared base implementation, which is why it is the simplest of the three. No roll, no component selection, no shields:

```
vtable+0x74(0, (blastRadius - (|hitPoint - obj[+0x26]| - obj[+0x5c])) * damage / blastRadius, attacker)
```

Component 0 is the only one a flyer has. There is no range test — the sweep has already made it — so the numerator cannot come out negative. The body radius subtracted is the same one the sweep subtracted, and a flyer's is zero (see [`hit-detection.md`](hit-detection.md#the-three-radius-slots)); for a class whose radius is not zero, a blast going off inside it scales by more than one.

### A collision — `Mech_CollisionTest` (`00418f74`)

**Walking into another machine hurts both of you**, and it is a direct call on each party rather than a sweep, so a third machine standing beside the crash takes nothing. The blocking half of the same function is in [`mech-locomotion.md`](mech-locomotion.md#collision).

```
if (struck.targetClass != Herc) return                     // a structure or flyer blocks, unhurt
closing = ({0, struck.speed, 0} * struck.rotation) * transpose(mover.rotation)
impulse = Q10(moverTypeRec[+0x4e], mover.speed) - Q10(struckTypeRec[+0x4e], closing.y)
damage  = Q10(300, impulse)
if (damage <= 200) return
at = ( (mover.x + struck.x)/2, (mover.y + struck.y)/2,
       min(eyeNodeZ(mover), eyeNodeZ(struck)) )
mover.vtable+0x70 (damage, at, 1200, struck)
struck.vtable+0x70(damage, at, 1200, mover)
```

`typeRec+0x4e` is the chassis' mass and `closing.y` is the struck machine's travel resolved onto the mover's forward axis, so a head-on meeting adds and being rear-ended by something slower subtracts. Both machines take the same figure. The 200 threshold is what keeps a machine shuffling against a wall from grinding itself down.

**The impact point mixes two frames.** X and Y are the world midpoint of the two machines, but Z is the lower of the two cockpit-eye nodes' *model-space* heights — roughly torso height above each machine's own feet — used as though it were a world height. On level ground near sea level the two nearly agree; on a hill the blast goes off well below the machines.

### Both pathways converge

They end in the same health-writing primitive (`Mech_ComponentDamageWrite` (`00417de4`) for a mech), and differ in shield-absorption implementation (parallel but separate code), in how many and which components get selected (one deterministic versus many random), and in whether there is a distance-falloff curve. Using the AoE formula for a laser would make it behave like a mini-explosion instead of a precise hit; using the direct-fire formula for a missile would make its splash radius meaningless — keep both as genuinely separate systems in a port.


## The shield system

**Struct layout** (confirmed in disassembly of `Shield_Init` `00413a90`): five consecutive `short` fields at `this+0x222`:

| Offset  | Field |
|---------|-------|
| `+0x222`| front charge |
| `+0x224`| rear charge |
| `+0x226`| balance, Q10 over 0–1024 (`0x200` = even) |
| `+0x228`| max — caps `front + rear`, raised by a Shield Pod |
| `+0x22a`| base max — the type's capacity before any pod; never written again |

**There is one pool, not two.** `+0x228` caps the *sum*; balance decides the split. `Shield_Init` sets both maxes to `baseValue` and both charges to `baseValue >> 1`, so a machine spawns full and evenly split.

**Capacity is a fleet-wide constant, not a per-type stat.** `baseValue` is `typeRecord+0xc0` — in asm, `ADD ESI,0x2` then `[ESI+0xbe]`, i.e. record-relative offset **190**, which is the file offset directly (`HercSimDat.ShieldMaxTotal`; the in-memory record is the 216-byte file record loaded at `+2`). Every retail HERC `.DAT` carries **3500** there; only the non-HERC SPIDER differs, at 0. A Shield Pod is the only thing that moves it.

**Capacity — `Mech_ComputeShieldCapacity` (`00417bec`).** Writes `+0x228` via `Shield_SetMax` (`00413ab8`). Called from `Mech_ConfigureLoadout`, where `Shield_RefillToBalance` (`00413ac8`) then refills to `max` at the current balance, **and again from `Mech_ComponentDamageWrite`** — so the array a machine can hold shrinks as its generator is shot, and both damage terms below are read fresh on each call rather than sampled at spawn.

```
capacity = 3500
if (generatorDamage > 0x80)                // dependent-subpiece 4, read inline
    capacity = Q10(3500, ((generatorDamage - 0x80) / 0x19) * -0x66 + 0x400)  // → 50% at worst
if (ShieldPod && podDamage < 225)
    capacity += Q10(1024 - 204*(podDamage/51), 3500)                         // → doubles at best
```

`podDamage` is `Component_ReadDamagePercent(mech+0x206, pod.GL[+0x17] + 19)` — the pod's own hardpoint component, like any other mount's, so it is shooting the hardpoint a pod sits on that degrades it.

The pod's share is a fraction of the *undamaged* base, so a battered machine still gets the full pod bonus. The pod curve is shared verbatim with the Energy Pod — see [equipment-pods.md](equipment-pods.md).

**Getter:** `Mech_GetShieldByHeading` (`004154d0`, mech vtable `+0x34`) — given a heading angle, returns `+0x222` within ±90° of front, else `+0x224`.

Both absorption implementations (`Mech_ShieldAbsorb_DirectFire` (`00413cc4`) direct fire, `Mech_ShieldAbsorb_Explosive` (`00413c68`) explosions) are a hard cap — `min(damage, remainingCharge)` — not a threshold gate. A hit exceeding what a zone holds drains it to zero and its excess carries through in the same hit.

### Recharge tick — `Shield_RechargeTick` (`00413b38`)

Called once per mech per tick from `Mech_PerTickSystemsUpdate`, with whatever the weapon mounts left unclaimed. See [reactor-energy-pool.md](reactor-energy-pool.md) for where that budget comes from.

```
deficit  = max - (front + rear)
granted  = min(request, 5, deficit)         // CMP word ptr [EBP+0xc],0x5
newTotal = front + rear + granted
front   := moveToward(front, Q10(balance, newTotal), deficit < 0 ? 10000 : 0x41)
rear     = newTotal - front
return request - granted
```

**5 units per tick is the recharge-rate constant** and it is per *tick*, not per unit time. At 3500 capacity and DBSIM's hard 25 Hz cap that is 700 ticks — **28 s from empty**, confirmed against retail. The front slew runs whether or not anything was granted, which is why moving the balance redistributes charge on an already-full array. The `10000` step is effectively a snap, reachable only when `max` drops below the charge held.

**The draw does not scale with capacity.** `max` is read only to size the deficit; the 5 is an immediate, so a Shield Pod's doubled array costs the pool the same 5 per tick and takes twice as long — 56 s from empty — to get there. That is the manual's "without increasing the drain on your Master Energy Pool", and the same page's "You cannot divert extra power to the shields": the shield system has exactly one rate and nothing, pod or player, moves it. The clause names the absence of a scaling the code never had, but it is not vacuous for the family — the Turbo Pod *does* buy its charge out of the same leftover budget, on the pool turn a Shield Pod inherits as a no-op ([equipment-pods.md](equipment-pods.md#what-each-class-actually-overrides)).

### Balance-adjustment input — player's own mech only

`Player_PerFrameCockpitUpdate` (`0041b130`, run per frame for `LocalPlayerMech`) calls `Shield_BalanceInputRead` (`00413bc8`) unconditionally. That function:

- copies the gauge's 15-byte state block (`ShieldsGauge_GetStateBlock`, UI slot `+0x1e9`), and if either click flag is set calls `Shield_BalanceAdjust`, clearing the flag (once per press);
- writes back `(front << 10) / baseMax`, `(rear << 10) / baseMax` and the raw balance.

`Shield_BalanceAdjust` (`00413af8`) adds `±0x66` (102) to `+0x226`, clamped to `[0, 0x400]` — a tenth of the range per press, so five presses from centre put everything on one facing. The manual binds `[` to rear and `]` to forward; direction 1 is the `+0x66` case, and balance is the front's share. Nothing is spent moving the balance.

### The cockpit widget shows charge and balance in two different places

- **Rings = charge.** `ShieldsGauge_UpdateRingPalette` (`004438f0`) reads the state block's `+0xb5`/`+0xb9` (the two `(charge << 10) / baseMax` fractions) and rewrites palette slots 66–71 every frame. The rings are painted into the herc's canopy art; the widget draws no geometry. Dividing by *base* max is what makes a Shield Pod drive the rings past `0x400` into their overcharged colours instead of renormalising.
- **Numbers = balance.** `ShieldsGauge_UpdateReadouts` (`00444a68`) reads `+0xbd` — the balance — and prints `balance * 200 >> 10` and the literal complement `200 - that`. **The pair always sums to 200 regardless of charge**; an empty array still reads 100/100. Reading them as a charge percentage is the natural mistake.

Shield recharge is a background trickle on every mech, AI and player alike. Balance adjustment is player input layered on top, touching only the balance field, which the recharge tick reads back on the next tick. The two never call each other.


## Open items

- **`Sim_RaycastObjectList` (`00426528`)'s and `Razor_MovementTick`'s exact source translation unit** unconfirmed by a direct assert string — the `objlist.cpp`/`flyersys.cpp` attributions are architecturally well-supported (shared object-list usage; a function that touches nothing but flyer state) but not proven the way `rocket.cpp`/`collide.cpp` were.

## Port notes

The traps, not a summary — everything else here is stated once above and does not need repeating.

1. **The two post-shield damage models are structurally different, not two settings of one.** Direct fire hits exactly one deterministically-selected component with no distance falloff; explosive damage sweeps the object list and rolls each of a machine's 29 components at ~51% odds with linear falloff. Using the explosive formula for a beam turns it into a mini-explosion.
2. **Shield absorption is implemented twice in the original**, once per pathway, and a port needs both gated — `absorbed = min(damage, remainingCharge)`, so damage bleeds through the instant a hit exceeds what is left in that zone, not only once the zone is empty.
3. **Rates are per tick, not per second.** The 5-unit shield recharge cap is per tick; at 25 Hz and the fleet-wide 3500 capacity a full rebuild is 700 ticks, or 28 s.
4. **`+0x70` is not "the splash weapon path".** Two of its four callers are not weapons at all — a drop pod landing and two machines colliding — and one of the weapon callers is a direct call on the struck object rather than a sweep.

## Ported

`Herculan.Engine.Sim.MechObject.Combat` (the hit test and `Mech_ApplyDirectFireDamage`), `Sim.ShieldCharge`, `Sim.MechObject.Power` (capacity and reactor rate), and `MechTypeRecord.HitRadius`/`HitCenterHeight`/`LegCount`/`Mass`. The parts of `Mech_ComponentDamageWrite` that change behaviour and the whole `+0x206` component-damage header are [`component-damage.md`](component-damage.md)'s port; weapon-mount destruction is [`weapon-damage-types.md`](weapon-damage-types.md)'s.

The explosive pathway is ported entire. `SimWorld.ExplosiveBlastSweep` is the sweep; `SimObject.ExplosiveDamage` is the `+0x70` slot, overridden by `MechObject`, `BaseObject` and `FlyerObject` for the three implementations. The `+0x58` accessors are `MechObject.ComponentPosition` (over an anchor table `BuildComponentAnchors` fills from the `.COL`, which is where the original's loadout step puts it) and `BaseObject.ComponentPosition`. `ShieldCharge.AbsorbExplosion` is the explosion path's shield step, and `SplashFactor`'s share is diverted rather than dropped. The collision call site is `MechObject.CollisionDamage`.

Of the sweep's three call sites the plasma round and the drop pod's landing (`Sim.MeteorObject`, [`mission-deployment.md`](mission-deployment.md)) are both reachable; the ram belongs to a behaviour state that does not exist yet. The sweep returns whether it caught anything, which only the pod reads — a pod that lands on something delivers nothing.

Not ported: the Shield Pod's own damage term in `Mech_ComputeShieldCapacity`.

`MechObject.ShieldsDownAlert` is a pure one-shot: it lacks the `+0xb0` clear the original's per-tick systems update runs above 1500 charge, so in this engine `SHIELDS CRITICAL` announces once per mission and the MFD's `SHIELDS DN` never goes out again ([`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md)).

Both by-products of the collision path are live in the original: the "something ran into me" latch at `obj+0xb1`, ported, is what a ramming machine detonates on, and `mech+0x2b0` is the nearby-structure record below.

### The collision path's structure record — `mech+0x2b0`

`Mech_CollisionTest` clears it on entry and, for each candidate whose `TargetClass` is 1 and whose body radius contains the machine, stores that structure (`00418fb2`/`00419016`). It is a render-side hand-off, not an aim or lock-on aid: `maybe_Scene_SubmitFrameObjects` reads it every frame (`00428519`) and, when it is set, submits the machine through `FUN_004283b4(mech, structure+0x1e8)` instead of the ordinary `FUN_0042837c(mech, GetBodyRadius())` — a machine standing inside a building's footprint is bucketed with the building rather than by its own radius. Not ported; the engine's scene pass does not have the bucket this feeds.
