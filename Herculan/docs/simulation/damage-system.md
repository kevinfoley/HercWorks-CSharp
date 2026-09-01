# DBSIM.EXE damage system — shields, components, weapon effectiveness

Reverse-engineered from `DBSIM.EXE` disassembly (Ghidra project `ES2Recon`). All addresses are
DBSIM.EXE virtual addresses. Confirmed against the official *Earthsiege 2 - On-Line Manual.pdf*
where noted. See [`weapon-firing.md`](weapon-firing.md) for how a shot gets here in the first place,
[`projectiles.md`](projectiles.md) for the travelling `Bullet` family's own lifecycle,
[`dbsim-physics-notes.md`](dbsim-physics-notes.md) for movement/collision/rocket math, and
[`../formats/terrain-heightmap.md`](../formats/terrain-heightmap.md) for the terrain heightmap this
system's ground-impact checks query.

How a weapon's fire turns into a mech taking damage has two different pathways — **direct fire**
(deterministic, single component, shield-gated) and **explosive/area-of-effect** (random,
multi-component, distance falloff, also shield-gated but via a separate parallel implementation)
— that share a common raycast entry point and converge on the same final health-writing primitive.

## The shared raycast: `FUN_00426528`

A generic ray-vs-live-object-list query, not weapon-specific. Exactly 5 call sites: the launcher
round's per-tick step (`Rocket_TickUpdate`, `0040a538`), bullet per-tick and burst-fire
(`FUN_0040b124`/`FUN_0040bf74`,
below), and the flyer terrain-avoidance autopilot
(`FUN_004198f4`, see [`../formats/terrain-heightmap.md`](../formats/terrain-heightmap.md#consumers-outside-the-terrain-system)).
A single raycast primitive reused for weapon hit-scan **and** obstacle sensing — most likely
`objlist.cpp` (shares the global live-object list, `DAT_004a9b7c`/`DAT_004a9b82`, with the
confirmed-`objlist.cpp` functions at `0x004281b0`/`0x004282f8`; not confirmed by a direct
assert-string tie). Confirmed **not** `fire.cpp`.

Walks the live-object list; for each candidate that passes the filter below, calls that
object's vtable method at `+0x20` — for a mech, `FUN_00418ba8`, the direct-fire hit-test-and-damage
function below; for a structure, `00405038`, and for a flyer, `FUN_00421c8c`, both in
[`hit-detection.md`](hit-detection.md). **The hit test and the damage
application are the same call** — there is no
separate "apply damage" step visible from the caller's side. `FUN_00426528` also makes a second,
unrelated vtable call per candidate (`+0x50`, `FUN_0041f7b8`) — AI threat-tracking ("this object
just took fire, update who it thinks is attacking it"), not damage.

Four properties a port has to preserve:

- The **candidate filter** is three tests, all before the vtable call: not the shot's owner
  (`shotData+0x0e`), not the object at `shotData+0x14`, and **not an object whose mission group
  still carries an action** (`*(int*)(obj[+0x45] + 0x14) != 0`). The middle one excludes nothing on
  the beam path, which never writes that field. The last one matters — see
  [`hit-detection.md`](hit-detection.md). The team byte
  (`obj[+0x45][+0x12]`) is read only *after* a hit, for the AI notification and friendly-fire
  warnings; it does not gate the hit itself.
- Before the sweep it **caches the world-to-muzzle transform** in the ray record at `+0x0a` (copy,
  transpose, negate-and-rotate the translation), which is the frame every hit test works in.
- It **shortens the ray to each hit** (`rayRecord+0x04`) rather than stopping at the first, so a
  candidate found later but nearer wins — every subsequent candidate is tested against the shortened
  length. It breaks early only for a hit inside 500 units. Because damage is applied inside the hit
  test, a candidate that is later superseded has still taken its damage.
- It opens with a **ray-versus-terrain query**, `Sim_RaycastTerrain` (`00428048`) →
  `Terrain_RayWalk` (`0046e87c`) against `ActiveHeightGrid`. A ground hit clips the ray before any
  object is tested, so a beam cannot shoot through a hillside. The ray record's own 200 is passed
  through as a walk radius but the thin-ray mode never reads it. Solved — see
  [`../formats/terrain-heightmap.md`](../formats/terrain-heightmap.md#ray-versus-terrain--terrain_raywalk-0046e87c).
  Returns `hitDistance + 1`, or 0 for a clean miss.

`bullet.cpp`'s per-tick and burst-fire functions (found by walking `FUN_00426528`'s other callers):
- **`FUN_0040b124`(instance) — per-bullet-instance tick.** Structurally parallel to the rocket's
  `FUN_0040a538` (periodic seeker-slot reacquire, age counter, lifetime-expiry check) but bullets
  age at a fixed baked-in rate, `FUN_00467820(0x200)`, not a per-type-record rate field. For
  bullet **type 9 specifically** (a distinct type index, not bullets as a class), there's a
  near-miss short-circuit before the raycast, and on a confirmed hit it calls the explosion
  function directly with a `4000`-unit blast radius — see "Explosive damage" below. Every other
  bullet type just calls the raycast and, if it returns a hit, marks itself for removal — the
  direct-fire damage already happened inside that raycast call.
- **`Bullet_FireBurst`(missileId, shotTransform, range, owner, power) — fire-burst / tracer spawner.**
  Calls the raycast once up front to get the actual (possibly shortened) travel distance, then —
  if that distance exceeds 5000 game units — splits the visual tracer into multiple 5000-unit
  segments (`FUN_0040b804` spawns each), otherwise spawns one tracer for the whole distance. Pure
  rendering; the hit-distance math is already resolved by the raycast call at the top.

## Direct-fire damage: armor-then-part, deterministic, shield-gated

**`FUN_00418ba8` (mech vtable `+0x20`), called by `FUN_00426528` on every raycast candidate.** In
order:

1. **Coarse range check.** Rejects the candidate outright when
   `200 + rayLength + typeRecord[0x1a] < |muzzle - mech|`, keeping the transform work off everything
   nowhere near the shot.
2. **Geometry and shield absorption — `FUN_00413cc4`.** The mech's centre of mass
   (`typeRecord+0x18` above its origin) is brought into **muzzle space**, where the ray is the Y
   axis, so the hit is two comparisons: the centre in front and within the ray's remaining length
   (an *unsigned* compare, which is what rejects anything behind the muzzle), and its 2D distance
   off the axis under the hit radius `typeRecord+0x1a`. Then
   `absorbed = min(incomingDamage, remainingShieldInZone)`, with both the incoming damage and the
   zone's charge reduced by it. A **hard cap, not an all-or-nothing threshold** — a hit worth more
   than the zone holds drains it to zero and carries its excess straight through in the same hit.
   The facing is picked by **where the muzzle sits in the mech's frame**, so it is the shooter's
   bearing that exposes the rear array. See "The shield system" below.

   It returns the ray's entry point into the hit cylinder, `alongAxis - (radius - offAxis)` floored
   at 1, which is what `FUN_00426528` shortens the ray to. **A fully absorbed shot still returns a
   hit distance and still stops the ray** — shields do not let fire through to whatever is behind —
   and the caller spawns only a hit-spark effect.
3. **Component selection — `Mech_SelectStruckComponent` (`0040c9d4`).** Only reached if some damage
   penetrated shields. Tests the mech's `col\<NAME>.COL` hit-sphere model cluster by cluster to find
   the ONE component struck — **not** a random roll, unlike the explosion path. Decoded and ported
   in [`hit-detection.md`](hit-detection.md). **Missing every sphere is a clean
   miss**: the shield cylinder is only a gate, and the shot passes on to whatever stands behind.
4. **Damage application — `FUN_004188c8`.** Takes `SplashFactor` off the top
   (`Math_Q10Multiply(shotData[+8], armorDamage)`, Q10 — see "Weapon-type effectiveness" below) and
   **splits** the shot: that share goes toward destroying the specific weapon
   mount at that location if one is present (which, if it fails, can trigger a secondary
   small-radius explosion via the mech's own `+0x70` vtable slot — the same function the AoE path
   uses, i.e. a destroyed weapon mount can itself explode and splash nearby components), and the
   remainder goes to that component's general health via the mech's `+0x74` vtable slot
   (`FUN_00417de4`, below). Health is bucketed into 8 levels (`>>5` of the 0–256 Q8 percentage)
   for state-transition/alert purposes, plausibly matching the manual's 5-color status system
   (Green/Yellow/Orange/Red/Gray).

This is fundamentally different in shape from the explosion path: precisely-aimed weapons hit what
you aimed at; explosions spray damage around imprecisely.

**The three type-record fields this path reads map onto `HercSimDat`.** `MechType_InitOne` reads the
`.DAT` as one block at record offset 2, so runtime offset = file offset + 2:

| Runtime | File | Field | Retail values |
|---|---|---|---|
| `+0x18` | 22 | hit-cylinder centre height (`Unk22_Val750Razor0`) | 1000 heavy/medium, 750 light, 0 RAZOR |
| `+0x1a` | 24 | hit radius (`AiAimTargOffset`) | 2500 heavy, 1500 medium, 1000 SPIDER |
| `+0x4a` | 72 | leg count (`ModelLegsTotal`) | 2, except PITBULL's 4 |

The radius is deliberately generous — it only has to be wide enough that nothing which could hit is
rejected, since the sphere model behind it decides. `AiAimTargOffset` was a guessed name; these two
consumers identify it.

`FUN_00418ba8` is invoked only polymorphically, as `obj[+0x20](...)`. **Beams do reach it through
`FUN_00426528` and nowhere else** — the beam dispatch was traced end to end in
[`weapon-firing.md`](weapon-firing.md), and it calls `Bullet_FireBurst`, which calls the raycast; no
path applies damage directly to a locked target.

## Explosive damage: blast sweep, random per-component roll, distance falloff, shield-gated

**`FUN_00426a20` — the area-of-effect sweep.** Walks the live-object list, skips inactive objects
and the excluded object (`param_5`), and for every other live object: computes its hit-radius via
a vtable call (`obj+0x5c`), computes distance from the impact point, and if
`distance − hitRadius < blastRadiusParam`, calls that object's `+0x70` vtable method — for a mech,
`FUN_004187d0` — with `(weaponType, impactPos, blastRadius, extra)`.

Exactly **3 call sites in all of DBSIM.EXE**, all genuinely explosive/terminal events, not routine
weapon fire:
1. **`Meteor_Tick` (`00409d2c`)** — the **drop pod** landing, not a missile: checks altitude against
   terrain height every tick via `Terrain_HeightQuery` (`0046e07c`), and the instant it dips below
   ground, detonates `FUN_00426a20(pos, 3000, 10000, 0, null)`. Its only caller is `Sim_MainTick`'s
   walk of the meteor pool. See
   [`mission-deployment.md`](mission-deployment.md) — this function was previously filed here as a
   missile's ground impact, which it is not.
2. **`FUN_0040b124`, only inside the bullet `type == 9` branch** — a distinct bullet subtype, not
   bullets as a class, calling `FUN_00426a20(pos, 4000, ..., owner, null)` on a hit. This is the
   **Plasma cannon** — confirmed concretely below (`MissileId 9`).
3. **`FUN_0041e48e`** — a mech's own death/destruction handler: once confirmed dead, it drops to
   the ground, triggers one `FUN_00426a20(pos, 3000, 2000, 0, self)` (a death explosion that can
   splash nearby objects), then unconditionally slams **every one of its own remaining components
   with a flat 32000 damage** via direct `+0x74` calls — no random roll, no falloff; guaranteed-
   destruction cleanup, not the live-combat formula.

**`FUN_004187d0(this, weaponType, hitPos, blastRadius, extra)` — the per-mech AoE damage
formula.** In order:
1. Computes the angular difference between the mech's facing and the direction to the hit point,
   classifying it front (`< 0x4000`, ±90° in BAM units) or rear.
2. **Shield absorption — `FUN_00413c68`, the explosion path's own separate implementation of
   the same concept `FUN_00413cc4` implements for direct fire.** Picks the front or rear shield
   value by the classification above, computes `scaledDamage = (weaponDamage × 1000) >> 8` (a
   different scaling constant than the direct-fire path — genuinely separate code, not a shared
   subroutine), subtracts it from that shield zone, and if the zone goes negative, clamps it to 0
   and returns a scaled overflow amount (`overflow × 0x400 / 1000`); otherwise returns 0 (fully
   absorbed, no structural damage). Confirms shields gate both damage pathways, matching the
   manual: "shields cause missiles to explode on contact, preventing most of their blast power
   from reaching the HERC's armor."
3. If shield overflow is positive, **iterates up to 29 component slots** (the same indexing space
   the direct-fire path's component selection uses — see below), and for each occupied slot: rolls
   a per-component random chance (`FUN_00492dd4(...) & 0xfff < 0x802`, ≈51%) to even be considered
   for this hit — splash damage doesn't hit every component in range, each independently rolls.
   For components that pass, gets that component's world position (`this+0x58` vtable call),
   computes distance from the hit point, and if within `blastRadius`, applies damage via the
   mech's `+0x74` vtable slot (the same `FUN_00417de4` the direct-fire path's final step uses)
   with:
   ```
   amount = shieldOverflow × (blastRadius − distance) / blastRadius
   ```
   **Linear distance falloff** — full overflow amount at zero distance, scaling to zero exactly at
   `blastRadius`.

**Both pathways converge on the same final health-writing function, `FUN_00417de4`** — they
differ in shield-absorption implementation (parallel but separate code), in how many/which
components get selected (one deterministic vs. many random), and in whether there's a
distance-falloff curve. Using the AoE formula for a laser would make it behave like a
mini-explosion instead of a precise hit; using the direct-fire formula for a missile would make
its splash radius meaningless — keep both as genuinely separate systems in a port.

## The shield system

**Struct layout** (confirmed in disassembly of `Shield_Init` `00413a90`): five consecutive `short`
fields at `this+0x222`:

| Offset  | Field |
|---------|-------|
| `+0x222`| front charge |
| `+0x224`| rear charge |
| `+0x226`| balance, Q10 over 0–1024 (`0x200` = even) |
| `+0x228`| max — caps `front + rear`, raised by a Shield Pod |
| `+0x22a`| base max — the type's capacity before any pod; never written again |

**There is one pool, not two.** `+0x228` caps the *sum*; balance decides the split. `Shield_Init`
sets both maxes to `baseValue` and both charges to `baseValue >> 1`, so a machine spawns full and
evenly split.

**Capacity is a fleet-wide constant, not a per-type stat.** `baseValue` is `typeRecord+0xc0` — in
asm, `ADD ESI,0x2` then `[ESI+0xbe]`, i.e. record-relative offset **190**, which is the file offset
directly (`HercSimDat.ShieldMaxTotal`; the in-memory record is the 216-byte file record loaded at
`+2`). Every retail HERC `.DAT` carries **3500** there; only the non-HERC SPIDER differs, at 0. A
Shield Pod is the only thing that moves it.

**Capacity at loadout — `FUN_00417bec`.** Runs once, from `Mech_ConfigureLoadout`, and writes
`+0x228` via `FUN_00413ab8`; `FUN_00413ac8` then refills to `max` at the current balance.

```
capacity = 3500
if (bodyDamage > 0x80)                     // dependent-subpiece 4
    capacity = Q10(3500, ((bodyDamage - 0x80) / 0x19) * -0x66 + 0x400)   // → 50% at worst
if (ShieldPod && podDamage < 225)
    capacity += Q10(1024 - 204*(podDamage/51), 3500)                     // → doubles at best
```

The pod's share is a fraction of the *undamaged* base, so a battered machine still gets the full pod
bonus. The pod curve is shared verbatim with the Energy Pod — see
[reactor-energy-pool.md](reactor-energy-pool.md#equipment-pods--mech0x307-filled-by-fun_0040fb2c).

**Getter:** `Mech_GetShieldByHeading` (`004154d0`, mech vtable `+0x34`) — given a heading angle,
returns `+0x222` within ±90° of front, else `+0x224`.

Both absorption implementations (`FUN_00413cc4` direct fire, `FUN_00413c68` explosions) are a hard
cap — `min(damage, remainingCharge)` — not a threshold gate. A hit exceeding what a zone holds
drains it to zero and its excess carries through in the same hit.

### Recharge tick — `Shield_RechargeTick` (`00413b38`)

Called once per mech per tick from `Mech_PerTickSystemsUpdate`, with whatever the weapon mounts left
unclaimed. See [reactor-energy-pool.md](reactor-energy-pool.md) for where that budget comes from.

```
deficit  = max - (front + rear)
granted  = min(request, 5, deficit)         // CMP word ptr [EBP+0xc],0x5
newTotal = front + rear + granted
front   := moveToward(front, Q10(balance, newTotal), deficit < 0 ? 10000 : 0x41)
rear     = newTotal - front
return request - granted
```

**5 units per tick is the recharge-rate constant** and it is per *tick*, not per unit time. At 3500
capacity and DBSIM's hard 25 Hz cap that is 700 ticks — **28 s from empty**, confirmed against
retail. The front slew runs whether or not anything was granted, which is why moving the balance
redistributes charge on an already-full array. The `10000` step is effectively a snap, reachable
only when `max` drops below the charge held.

### Balance-adjustment input — player's own mech only

`Player_PerFrameCockpitUpdate` (`0041b130`, run per frame for `LocalPlayerMech`) calls
`Shield_BalanceInputRead` (`00413bc8`) unconditionally. That function:

- copies the gauge's 15-byte state block (`ShieldsGauge_GetStateBlock`, UI slot `+0x1e9`), and if
  either click flag is set calls `Shield_BalanceAdjust`, clearing the flag (once per press);
- writes back `(front << 10) / baseMax`, `(rear << 10) / baseMax` and the raw balance.

`Shield_BalanceAdjust` (`00413af8`) adds `±0x66` (102) to `+0x226`, clamped to `[0, 0x400]` — a
tenth of the range per press, so five presses from centre put everything on one facing. The manual
binds `[` to rear and `]` to forward; direction 1 is the `+0x66` case, and balance is the front's
share. Nothing is spent moving the balance.

### The cockpit widget shows charge and balance in two different places

- **Rings = charge.** `ShieldsGauge_UpdateRingPalette` (`004438f0`) reads the state block's
  `+0xb5`/`+0xb9` (the two `(charge << 10) / baseMax` fractions) and rewrites palette slots 66–71
  every frame. The rings are painted into the herc's canopy art; the widget draws no geometry.
  Dividing by *base* max is what makes a Shield Pod drive the rings past `0x400` into their
  overcharged colours instead of renormalising.
- **Numbers = balance.** `ShieldsGauge_UpdateReadouts` (`00444a68`) reads `+0xbd` — the balance —
  and prints `balance * 200 >> 10` and the literal complement `200 - that`. **The pair always sums
  to 200 regardless of charge**; an empty array still reads 100/100. Reading them as a charge
  percentage is the natural mistake.

Shield recharge is a background trickle on every mech, AI and player alike. Balance adjustment is
player input layered on top, touching only the balance field, which the recharge tick reads back on
the next tick. The two never call each other.

## The component damage system

**Flyers have one too.** `FUN_004215f4` allocates the same header at `flyer+0x200` with literal
counts of **1 and 1** — one main component, one dependent — which is exactly what `SKIMMER.DMG`
ships. The counts are hard-coded at each constructor, not read from the file.

**`this+0x206` is a header of pointers, not inline arrays.** Allocator `FUN_0040d2cc`, called as
`FUN_0040d2cc(this+0x206, 0x1d /*29*/, 0x16 /*22*/)`:

| Offset (abs) | Field |
|---|---|
| `+0x206` | **pointer** to a 22-`short` dependent-subpiece **damage** array, zeroed = undamaged |
| `+0x20a` | **pointer** to a 29-`short` main-component **damage** array, zeroed = undamaged |
| `+0x20e` | **pointer** to a 29-`short` active/occupancy-flag array, all bytes `0x01` at init |
| `+0x21e` | `short` count = 29 |
| `+0x220` | `short` count = 22 |

Every accessor (`FUN_0040dbc0`/`FUN_0040da38`/`FUN_00417de4`/`FUN_00417bec`/…) treats `this+0x206`
as `(int*)` and does an extra pointer dereference before indexing.

- The 22-entry array = accumulated damage on **fine sub-piece / dependent** components (see the
  aggregation formula below).
- The 29-entry array = accumulated damage on the **main component slots**, the same indexing space
  both damage pathways' component selection uses.
- The 29-entry flag array = **occupancy/active flag per component slot**, not a second depleting
  health pool. Zeroed for a slot when that component (typically a weapon mount) is destroyed.

`FUN_0040d354(this+0x206, mechThis, damageDataPtr, collisionRegistration)` — a second constructor
call — wires up `this+0x212` (and neighboring fields) as a pointer into per-component **maximum/
reference** data (sourced from the mech's own `damage.dat`-derived pointer plus its collision
registration record from `FUN_0040cd88`, tying this system to the collision bounding-sphere tree
in [`dbsim-physics-notes.md`](dbsim-physics-notes.md#collision-system--hierarchical-bounding-sphere-construction-collidecpp)).

**Read: `Component_ReadDamagePercent` (`0040dbc0`) — accumulated damage as Q8 (0–256), 0 = pristine,
256 = destroyed.** Note the sense: it returns damage, not health, so every caller's curve runs the
opposite way to how a `…HealthPercent` name would suggest. Looks up the component's max-reference record
(18 bytes, via `this+0x212`), starts with its own damage (main 29-entry array) and max values, then
**aggregates in every dependent sub-component** listed in that record (walking a list, adding each
dependent's damage from the 22-entry array and max from a parallel max-side array) before computing
`(totalDamage << 8) / totalMax`. An entry holding `-1` (destroyed) substitutes its max, so it reads
as fully damaged. A single displayed component's reading can be the aggregate of several finer
sub-parts — e.g. a "leg" reading as leg proper plus whatever finer actuator/joint pieces are
modeled underneath it (exact sub-piece breakdown per component not traced).

**Write and cascade: `Component_ApplyDamageAndCascade` (`0040da38`)**, called from
`Mech_ComponentDamageWrite` (`00417de4`, mech vtable `+0x74`) and `FUN_00421bb4` (the flyer's) — the
shared endpoint both damage pathways call into:

```
destroyed = Component_AddDamage(&mainDamage[i], piece.Armor, &damage)   // FUN_0040d3ec
if (destroyed) {
    drained = Component_SpillIntoDependents(piece, subDamage, damage, subMax)   // FUN_0040cf44
    if (drained && (piece.DestructionFlags & 1)) {
        Component_DestroyAndCascade(i)                     // FUN_0040d434
        drain the pending BoneId queue through the same call
    }
}
```

- `FUN_0040d3ec` **adds** damage, stores `-1` rather than the max once the entry is finished, and
  **writes the excess back into `damage`**. An entry already at `-1` absorbs nothing, so a lost part
  cannot be shot again.
- `FUN_0040cf44` pours that excess into the component's dependents, **one at a time, weighted and
  random**: each live dependent contributes its `CritChance` to a total, a draw under that total
  picks the one that takes the hit, and if that spill destroys it the remainder goes round again. It
  returns true only once no live dependents are left — which is why a component with internals still
  intact does not cascade even after its own armour is gone.
- `FUN_0040d434` writes `-1`, clears the active flag, finishes off everything under it with a flat
  32000, and queues every live piece whose `BoneId` names this component. The original drains that
  queue iteratively rather than recursing.
- `FUN_0040d9f8` ("is component *i* destroyed **and** all of its dependents too", via `FUN_0040cf10`)
  is the stricter test the mech's death gate asks of its two cockpit slots.

### The 18-byte record — `.DMG`'s `HercPiece`

`HercWorks.Core.Data.File.Dbsim.HercSimDamage.HercPiece`, loaded from `dmg\[herc].DMG` (confirmed
by tracing `FUN_0040d160`'s caller `FUN_00415bb0`, the mech constructor, which builds the filename
from the mech's own name string plus extension).

The whole file, per `HercPiece_LoadTable` (`0040d09c`) — **no padding anywhere**:

```
subCount, subCount * int16 dependent max armour
pieceCount, pieceCount * 18-byte HercPiece
```

Retail: 22 dependents and 29 pieces for every HERC, 1 and 1 for `SKIMMER`. Only dependent slots
0–11 carry a nonzero maximum, and the pieces reference no index above 11.

| Offset | Field | Evidence |
|---|---|---|
| `+0x00` | `short` `Armor` (max health) | `FUN_0040dbc0`'s `local_10 = *psVar5` |
| `+0x02` | `signed char` — index into a debris/effect lookup (`FUN_004089bc`), sentinel `-1` = none | `FUN_0040d434`, on destruction, selects a specific debris/effect variant |
| `+0x03` | `signed char` — index into a slot array on the mech itself (`*(int*)(mechThis+0x34)+8`, byte `[index]` written `=2` on destroy — a state-transition write, same "2" code seen for a damaged/destroyed HUD slot elsewhere) | `FUN_0040d434`, guarded by `-1 < value` (`-1` = no HUD slot) |
| `+0x04` | `signed char` `BoneId` — the **index of the parent component** this one hangs off, `-1` for none. Destroying component *n* queues every still-live piece whose `BoneId` is *n*. Retail: ACHILLES' leg chain runs 7→9→11, and its two weapon brackets (4, 5) carry components 19–25. SPIDER sets `-1` throughout, so nothing on it cascades | `FUN_0040d434`'s trailing loop |
| `+0x05` | `byte` `DestructionFlags` bitfield: bit0=has dependents to cascade, bit1=alt destruction-effect mode, bit2=one-shot "major alert already fired" latch, bit3=triggers secondary effect callback | `FUN_0040da38`/`FUN_0040d434` |
| `+0x06` | `short` dependent sub-component count | `FUN_0040cff8` (loader), `FUN_0040dbc0`'s loop bound |
| `+0x08` | `int` pointer to the dependent list (4 bytes/entry: index at sub-offset `+2`) | `FUN_0040cff8`/`FUN_0040dbc0` |
| `+0x0c` | `short` sentinel `0xffff` (runtime-only) | `FUN_0040cff8` |
| `+0x0e` | `int` zero (runtime-only) | `FUN_0040cff8` |

`+0x02`/`+0x03` together form the C# port's `DebrisFlags` (one `short`); `+0x05` is
`DestructionFlags`.

### Component naming and index semantics

The Java author's own doc comment on `HercSimDamage.cs` lists real component names in array order:
`COCKPIT/FRONT`, `COCKPIT/REAR`, `SHOULDER/LEFT`, `SHOULDER/RIGHT`, `WEPN_BRACK/LEFT`,
`WEPN_BRACK/RIGHT`, `TORSO`, `LEG/LEFT/UPPER`, `LEG/RIGHT/UPPER`, ...

- **Indices 0–1 (`COCKPIT/FRONT`/`REAR`)** — individually checked (`FUN_0040d9f8`) as the mech's
  death-trigger gate.
- **Indices 4–5 (`WEPN_BRACK/LEFT`/`RIGHT`)** — ordinary weapon-mount slots inside this same
  29-entry array (see "Weapon mounts" below for the separate runtime ammo/heat state).
- **Dependent-array (22-entry) slots read by literal offset in `FUN_00417de4`**, not by a loop.
  0 and 1 are the front leg servos, joined by 10 and 11 (the rear pair) when
  `typeRecord+0x4a` is 4; the pair(s) are averaged before being compared against `0x8d` (crippled)
  and `0x50` (an alert only), and half of them destroyed immobilises the machine. 4 is the shield
  generator, which `Mech_ComputeShieldCapacity` reads — so shooting it shrinks the array the machine
  can hold, and that recompute happens **here as well as at spawn**. 5 is the reactor, latching the
  two output-damage flags. 8 and 9 are life support and the pilot: either destroyed, or either
  cockpit slot fully gone, and the machine dies.

`FUN_00417de4` itself, beyond wrapping the health write above, does per-subsystem percentage
tracking with 8-level bucketing and fires distinct alert sounds at multiple thresholds (~55%,
~31%, fully destroyed). It tracks the leg/limb subset in pairs (above) and — the mech-death
trigger — **if enough limbs are fully destroyed, kills the mech outright**: clears its target,
calls a destruction handler, sets flags, and finishes off remaining components via a recursive
self-call with a flat 30000 damage. It also processes weapon-mount ratios (from `this+0x202`, see
"Weapon mounts" below) and a distinct "torso"-like aggregate with its own thresholds (75%/50%).

## Structural / Internal / Weaponry

The manual describes a HUD/HDD display split into "structural, internal, and weaponry" categories.
This is **three different mechanisms**, not a 3-way partition of one index space:

- **Structural** = most of the 29-slot `HercPiece` array — named body pieces (torso, legs, feet,
  shoulders).
- **Weaponry** = a *subset of that same array*, distinguished only by name/position
  (`WEPN_BRACK/LEFT`/`RIGHT`). Weapon-specific runtime state (ammo, heat) lives elsewhere, in the
  weapon-mount-manager object (`this+0x202`), not in this health record.
- **Internal** = a *wholly separate*, smaller table, `HercInternals` (Left/Right Leg Servos,
  Sensor Array, Targeting Computer, Shield Generator, Engine, Hydraulics, Stabilizers, Life
  Support, Pilot) — reached *probabilistically* through a struck structural/weaponry piece's own
  `MappedInternals`/`CritChance` list, not directly targetable. An Internal system has no health
  slot of its own in the 29-component array; damaging it is a chance-based side effect of hitting
  whichever structural piece maps to it.

"Armor" in the manual's "where shields leave off, armor takes over... duranium plates" sense maps
to the per-component `Armor` field on `HercPiece` (`this+0x20a`/`this+0x206`) — not a separate
third depleting pool distinct from "structure." Genuinely still open: whether shields
differentiate by weapon type anywhere (checked, not found in the shield-absorption functions
themselves — see "Weapon-type effectiveness" below), and the remaining `+0x02`/`+0x03` sub-byte
semantics within `DebrisFlags`.

## Weapon-type effectiveness

The manual: "Two weapons are effective against shields: EMP cannons disrupt the shield matrix, and
the ELF is so incredibly powerful that it punches through shields as if they are not even there,"
"energy weapons... are effective against shields... Projectile weapons have longer range and do
more damage to enemy armor, but have little effect on a target with shields," Lasers have "limited
effectiveness against shields," ATCs are "fast and hard enough to penetrate most armor plating."

**Not found in code.** The one candidate — `FUN_004188c8`'s
`Math_Q10Multiply(shotData[+8], armorDamage)` — is `SplashFactor`, the secondary-explosion split
documented below, not a per-weapon-type effectiveness scale. The whole of the manual's claim that
lives in code is the two separate `DamageShield`/`DamageArmor` figures each `PROJ.DAT` record
carries; nothing scales either by the *target's* defence type. The shield absorption functions were
also checked and carry no weapon-type term.

The shot descriptor (`shotData`, the same struct `FUN_00418ba8`/`FUN_004188c8` consume) is built in
`Bullet_FireBurst` right before the `FUN_00426528` raycast call from the firing weapon's `PROJ.DAT`
record — layout in [`weapon-firing.md`](weapon-firing.md#the-shot-record); `+0x04` is the figure
`FUN_004188c8` applies to structure and armor, `+0x06` the one `FUN_00418ba8` feeds into shields.

The record table is `PROJ.DAT` (`HercWorks.Core.Data.File.Dat.Sim.ProjectileData`). Cross-checked against
the real retail `ES2\VOL\simvol0\dat\PROJ.DAT` (984 bytes: 9-byte VOL prefix +
`[Total:u16=27][27×36-byte records]` + 1 trailing marker byte): values line up with the manual —
- Entries with `DamageShield ≫ DamageArmor` (e.g. 2000/400, 8000/2000) — EMP-shaped.
- Entries with `DamageArmor ≫ DamageShield` (e.g. 400/1600, 3000/7200) — ordinary
  Autocannon-shaped.
- Several `DamageShield ≥ DamageArmor` entries with `Speed=0` (no travel time) — beam-shaped.
- The first 3 entries (60/360, 120/480, 180/600, `Speed=5000`) match ATC20/35/50.

**`DamageShield`/`DamageArmor` are the weapon's own base damage stats against each defense type**
— the value scaled against them (`shotPower`) is not a "raw damage" the file further adjusts.

`shotPower` is the capacitor charge the shot was fired at, `min(template+0x38, mount+0x7d)`, and the
scale is **Q10** — against a capacitor scaled to 1200, so a mount holding more than 1024 makes a shot
worth slightly more than the record's face value. `SplashFactor`'s own multiply below is Q10 as well
(`Math_Q10Multiply`, `0047dfa4`).

**`SplashFactor` (`Unk2_val`, short-index 4, `shotData+8`) — a per-weapon splash/secondary-
explosion trigger, not a third damage-type multiplier.** Consumer, `FUN_004188c8`:
```c
uVar1 = Q10mul(shotData+8 /*SplashFactor*/, shotData+4 /*armor-scaled damage*/);
call obj[+0x74](obj, part, armorDamage - uVar1, ...);      // general component health takes the REMAINDER
if (uVar1 != 0) call obj[+0x70](obj, uVar1, ..., blastRadius=500, ...);  // secondary explosion, same formula explosive weapons use
```
A Q10 **fraction of the already shield-absorbed armor damage** diverted into a small
(500-unit-radius) secondary explosion, reusing the same blast-sweep formula as "Explosive damage"
above, instead of applying straight to the struck component's health. Zero means no secondary
explosion — the guard (`if (uVar1 != 0)`) skips it and the full armor-damage amount goes straight
to health. Real nonzero values (`500` or `1000`) appear scattered across several weapons, most
consistently for one whole weapon family (uniform `DamageShield==DamageArmor`) — a plausible match
for Electron Flux, not proven.

**The loader:** `FUN_0040fc8c` opens `"wpntex"`, `"mechwpn2"`, `"weapons"` (count + 88-byte
records — plausibly a per-hardpoint mount-template table, not traced further), then `"proj"` and
reads its count + 36-byte records in one flat read into `DAT_004a9980`, linear-searched by
`FUN_0040ffc8(category, subtypeId)` — `PROJ.DAT`'s in-memory copy is keyed by `(category, id)`
(matching `Projectile.Type`/`MissileId`), not by flat array index.

### `Type` — a firing-mechanism selector

Traced all 5 callers of `FUN_0040ffc8` and both callers of `FUN_0040bf74`. Each caller hardcodes a
literal category constant, and each corresponds to a genuinely different projectile *class*
(different vtable, different construction):

| `Type` | Constructor | Object kind | Real `PROJ.DAT` shape |
|---|---|---|---|
| `0` | `Missile_Construct` (`0040a948`) | the launcher round (14-byte type table `ROCKETS.DAT`, vtable `PTR_Bullet_Draw_00498448`) — see [`rockets.md`](rockets.md) | 5 entries, `SplashFactor=500` uniformly, real `Speed`, armor≫shield |
| `2` | `Bullet_Construct` (`0040af6c`) | the travelling gun round (own 14-byte type table `BULLETS.DAT`, own vtable `PTR_FUN_00498628`) — see [`projectiles.md`](projectiles.md) | mixed: ATC20/35/50-shaped progression *and* EMP-shaped high-shield entries — `SplashFactor=0` for all but `MissileId=9` (Plasma cannon, below) |
| `3` | `Rocket_ConstructGuided` (`0040ac3c`) | **dead code** — nothing calls it, and its vtable's per-tick slot is `FUN_0040acb4`, a stub returning zero, so an instance would never move and never die | 3 entries, shield==armor exactly, `SplashFactor` 1000/500/500, all unreachable |
| `4` | `Bullet_FireBurst` (`0040bf74`) | **no persistent simulated object at all** — resolves its raycast hit synchronously inside the call itself, then spawns pure-visual tracer segments | every `Type=4` record has `Speed=0`, no exceptions |

`Type=4`'s "no persistent object, resolves at the call site, always `Speed=0`" combination is the
concrete mechanical definition of a beam/hitscan weapon. Only `0` and `2` are live classes: the
ammunition dispatch (`WeaponMount_FireDispatch_Missile`) tests for `Type == 0` and sends everything
else to `Bullet_Fire`, and `Rocket_Fire` always builds the `Type 0` class.

Mapping onto the weapon taxonomy — flagged as a reasoned hypothesis from mechanism + shape except
where noted confirmed:
- **`Type 4` (beam) → Lasers + PBW.** Two unusually low-damage `Type 4` entries (150/200, 200/300,
  both far below the others' 1000+ values) plausibly fit Electron Flux, not confirmed.
- **`Type 2` (real flight time, no splash) → Autocannons + EMP** — accounts for every `Type 2`
  entry except the one Plasma outlier.
- **`Type 0` (5 entries) → the game's Missile weapons**, confirmed: its five subtype ids are the
  five `ROCKETS.DAT` records, and the four the `MSL` launchers reach are `SARH`/`ARH`/`ARM`/`EO`
  while `BMSL` takes the fifth. `Type 3`'s three entries are data for a class that never runs.

**Plasma cannon — confirmed.** The one `Type 2` outlier (`DamageShield==DamageArmor==3000`,
`SplashFactor=1000`) is `MissileId 9`. The `Bullet` class's vtable (`PTR_FUN_00498628`) per-tick
slot (`+0x14`) is `FUN_0040b124`, whose `type == 9` branch (checked via
`*(char*)(this+0x41) == '\t'`) calls the explosion formula directly instead of the ordinary
single-target hit path — `this+0x41` is exactly where every projectile constructor
(`Missile_Construct`/`Bullet_Construct`/`Rocket_ConstructGuided`) stores its own `MissileId`
argument, so this is
checking `MissileId==9` on a live `Bullet` instance. `(Type=2, MissileId=9)` is mechanically a
`Bullet` (real flight time, unlike true `Beam`s) that explodes with splash on impact (unlike every
other `Bullet`), matching the manual's Plasma description exactly.

A weapon's `(Type, MissileId)` pair is set upstream, in the mount template table
([`../formats/weapons-dat-sim.md`](../formats/weapons-dat-sim.md)) via each template's
`ProjDatIndex` — the engine looks records up by key, never by array position. `MissileId` also
indexes `BULLETS.DAT`/`ROCKETS.DAT` for model data.

### Beam-weapon dispatch

Beam weapons need no special hit-test call: they go through the same `FUN_00426528` raycast every
other weapon uses, just synchronously, once, at fire time, with no persisting object afterward. The
dispatch itself is in [`weapon-firing.md`](weapon-firing.md#the-fire-dispatch--vtable-0x28).

**Do not conflate two similar-looking fields.** The shot-record field `shotData+0x12` (hardcoded `5`
for `FUN_0040bf74`'s bullets) is a different numbering scheme from `PROJ.DAT`'s own `Type` field.
`shotData+0x12` gates an unrelated target-side alert/timer effect; `PROJ.DAT`'s `Type` is what
determines beam-vs-projectile behaviour, at the mount's fire-dispatch decision, not in the shot
record's own flag byte.

## Weapon mounts

`this+0x202` is a **pointer to a separately-allocated weapon-mount-manager object** (own vtable;
size `0x14` or `0x35` bytes depending on the `this+0xa3` "locally-simulated" flag), allocated in
`FUN_004175dc` (the mech loadout-(re)configuration function, called on spawn/equip changes):
`*(int**)(this+0x202) = malloc(...)`, thereafter accessed via `(**(vtable)(*(this+0x202)))`
virtual calls. It's referenced throughout `FUN_00417de4` for computing ammo/heat-style ratios, by
`FUN_00415558` (a "find the next occupied weapon slot" iterator, walking a 7-entry table
`DAT_0049a060`), and is where the shield-recharge tick's energy-arbitration vtable call goes (see
"The shield system" above). When a weapon mount is destroyed (inside `FUN_004188c8`'s damage-split
logic), its `this+0x20e` active-flag is cleared and its owning object is released. Matches the
manual's "Weaponry" HDD damage category. (`this+0x20e`'s per-slot indices, the weapon mount active
flags, are a *different*, already-documented array from `this+0x202` itself — see "The component
damage system" above.)

## Open items

- **`FUN_00426528`'s and `FUN_004198f4`'s exact source translation unit** unconfirmed by a direct
  assert string — the `objlist.cpp`/`flyersys.cpp` attributions are architecturally well-supported
  (shared object-list usage, sensor/autopilot semantics) but not proven the way `rocket.cpp`/
  `collide.cpp` were.
- **`debris.cpp`'s `FUN_0040874c`** — a load-time debris-piece-list loader, reading per-piece
  records via `FUN_004083f8`. Two angle-like `short` fields are each multiplied by `0xb6` (182)
  after loading, unless the raw value is sentinel `-1`. `65536 / 360 ≈ 182.04`, so `×182` is very
  likely a degrees→BAM conversion applied to debris piece orientation at load time — not
  cross-checked against a second `×182` site.

## Port notes

The traps, not a summary — everything else here is stated once above and does not need repeating.

1. **The two post-shield damage models are structurally different, not two settings of one.** Direct
   fire hits exactly one deterministically-selected component with no distance falloff; explosive
   damage sweeps the object list and independently rolls each of up to 29 components at ~51% odds
   with linear falloff. Using the explosive formula for a beam turns it into a mini-explosion.
2. **Shield absorption is implemented twice in the original**, once per pathway, and a port needs
   both gated — `absorbed = min(damage, remainingCharge)`, so damage bleeds through the instant a
   hit exceeds what is left in that zone, not only once the zone is empty.
3. **Component health is a dependency graph, not a flat HP list.** A component's reading aggregates
   its dependents, and destroying one cascades into them.
4. **Rates are per tick, not per second.** The 5-unit shield recharge cap is per tick; at 25 Hz and
   the fleet-wide 3500 capacity a full rebuild is 700 ticks, or 28 s.

## Ported

`Herculan.Engine.Sim.MechObject.Combat` (the hit test, `Mech_ApplyDirectFireDamage`, and the parts
of `Mech_ComponentDamageWrite` that change behaviour: the shield-capacity recompute, leg grading,
the death gate, the reactor flags), `Sim.ComponentDamage` (the whole `+0x206` header — the three
arrays, the aggregate read, the spill and the cascade), `Sim.ShieldCharge`, `Sim.MechObject.Power`
(capacity and reactor rate), and `MechTypeRecord.HitRadius`/`HitCenterHeight`/`LegCount`.

Not ported: the explosive blast sweep (so `SplashFactor`'s share is dropped rather than diverted —
every retail beam states zero, so nothing is lost today), weapon-mount destruction (components
19–28 index the mount manager as `component - 19`), the Shield Pod's own damage term in
`Mech_ComputeShieldCapacity`, every alert sound, and the debris a destroyed component throws.
