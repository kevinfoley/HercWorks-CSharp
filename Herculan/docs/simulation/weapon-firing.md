# DBSIM.EXE weapon firing: the trigger, the shot, beams

Solved 2026-08-24 from `DBSIM.EXE` in the `ES2Recon` Ghidra project; all addresses are DBSIM virtual
addresses. Ported in `Herculan.Engine.Sim.{WeaponMount, WeaponMounts, WeaponShot, MechObject}` and
`SimWorld.{Raycast, RaycastTerrain}`.

Covers how a trigger pull becomes a shot and what a beam does. The mounts it fires are in
[`weapon-mounts.md`](weapon-mounts.md); what a hit does to the target is in
[`damage-system.md`](damage-system.md); the template fields read here are in
[`../formats/weapons-dat-sim.md`](../formats/weapons-dat-sim.md).

## The trigger is polled, not dispatched

**There is no scancode case for `[Space]` anywhere.** The manual binds it to Fire Active Weapon, but
it never reaches `Sim_DispatchCommand` or `WeaponMounts_HandleCommand`. Instead:

| | |
|---|---|
| `Sim_PollPlayerInput` (`00460764`) | runs every frame, calls the next line for `LocalPlayerMech` |
| `Mech_PlayerFireTick` (`00415608`) | calls the fire entry, then lays out the HUD lead-indicator trail on a successful shot |
| `WeaponMounts_FireTrigger` (`00410dbc`) | the arbitration below |
| `WeaponMount_TriggerHeld` (`0040f8ad`) | mount vtable `+0x30` — returns the input device struct's byte at `+0x0d`, the fire button, and nothing else |

So the trigger is a **held state re-read every frame**: holding it fires again the instant the refire
delay expires and the capacitor is back over its threshold. Nothing along the path looks at edges.
Only the player's machine reaches it — AI machines fire from their own think function.

The device byte is `DAT_004d2357`, taken from whichever button the input configuration assigns the
fire action, or the default keyboard binding at `DAT_004d23e4`/`DAT_004d23f4`.

### `WeaponMounts_FireTrigger`, in order

1. The armed mount (`manager+0x1d`); its link partner if `+0x4b` is set, via the hardpoint's own
   `+0x16` offset.
2. **Both** mounts pass `+0x2c` (ready) before either fires — a pair whose second half is still
   charging does not fire its first half alone.
3. **Both** pass `+0x30` (trigger held), asked separately even though both answer from the same byte.
4. Armed fires through `+0x28`, then the partner.
5. Single-fire (`manager+0x18`) is cleared once the armed mount is no longer ready. That is the whole
   of the manual's "once you fire, the current firing chain will resume" — the chain advance in
   `FUN_00410a3c` takes the selection back on the next frame.

It also passes a "this shot is free" flag built from `DAT_004a9ed6`/`DAT_004a9edc`, which only the
ammunition class reads, and raises an alert pair when the armed mount's `+0x60` reports ammunition
type 3, which an energy mount never can.

## The fire dispatch — vtable `+0x28`

Two implementations, one per live mount class, both opening with the same prologue:

| Class | Function | Branch |
|---|---|---|
| Energy / gun | `WeaponMount_FireDispatch_GunBeam` (`0040ea58`) | `Beam` → `Bullet_FireBurst`; else a travelling `Bullet` |
| Ammunition | `WeaponMount_FireDispatch_Missile` (`0040e964`) | `Missile` → `Rocket_Fire` ([`rockets.md`](rockets.md)); else the same `Bullet` fallback |

Both also set `mount+0x44` when the hardpoint is visible (`.GL +6 < 4`) — the muzzle flash. The
ammunition class raises it on its `Bullet` branch only; a rocket comes off a rail and lights
nothing.

### The beam branch

```
power = min(template[0x38], capacitor +0x7d)      // the cost, capped at what is held
capacitor -= power
shotTransform.translation = muzzleWorldPoint      // overwrite the gun frame's origin
Bullet_FireBurst(proj.MissileId, shotTransform, template[0x30], ownerMech, power)
```

`template[0x38]` is the same field as the upper half of the readiness threshold pair, and the two
shapes that pair takes are two kinds of weapon:

- **Fixed cost.** `0x36 == 0x38` (LAS100 80/80 … LAS500 120/120): threshold is that number, cost is
  that number, every shot identical.
- **Charge-up.** `0x36 < 0x38` with `0x38` at 10000 (`PBEAM`, `EMP`, `PLAS`, 300/10000): threshold is
  `max(0x36, charge target)` and the cost is the whole capacitor, so the shot is worth as much as the
  pilot let it accumulate. The manual's *power level* is that charge target.

### The gun branches

Everything that is not a `Beam` builds a travelling `Bullet` (see
[`projectiles.md`](projectiles.md)), through one of two branches:

- **Charge-up gun**, taken when the capacitor holds *less* than the cost. It fires shots worth the
  whole charge and then either arms a burst or empties the capacitor. **Every retail energy gun
  takes this branch always**, because they all read a 10000 cost against a capacitor scaled to 1200.
- **Fixed-cost gun**, taken otherwise: subtract the cost, fire one unpowered shot. Unreachable in
  retail for the reason above.

Two multi-shot rules sit on the charge-up branch, and each identifies exactly one weapon:

| Test | Weapon | Effect |
|---|---|---|
| `template[0x3c] == 3` | catalog id 19, the big EMP (the simulator also names it `EMP`) | fires **three** shots, from barrels at `-x`, `0` and `+x` of the template's own muzzle offset |
| `template[0x3e] == 0x13` | catalog id 23, `EMP2` | arms `mount+0x4d`, so the mount fires again a quarter of a refire delay later and *then* empties — two volleys per trigger pull |

`0x3e` is `ProjDatIndex`, and `0x13` is `EMP2`'s own `PROJ.DAT` row, so that second test is a weapon
check spelled as a data comparison. The follow-up shot is dispatched from the energy arbitration
(`WeaponMounts_ArbitrateEnergy`, via `WeaponMount_AutoFireDue`), not from the trigger.

### The ammunition dispatch

**It spends `+0x7b`, not `+0x7d`** — it subtracts `template[0x38]` from the round count (5 on every
autocannon, against magazines of 500 to 2000) and clears `+0x4c` (selectable) at zero, dropping an
empty weapon out of the selection cycle. It spends **before** it looks at the projectile type, so a
launcher pays a round on the `Rocket_Fire` path too. The one thing that can skip the spend is the
"this shot is free" flag the manager passes from a pair of debug globals.

`+0x7d` is the *displayed* count, in 256ths, and lags: `WeaponMount_PushAmmoGaugeState` (`0040f330`)
decays it toward `+0x7b * 256` at 250 per 125 ms, which is what makes the cockpit counter roll rather
than jump.

## The shot record

`Bullet_FireBurst` builds it on its own stack and `Sim_RaycastObjectList` writes back into it.

| Offset | Field |
|---|---|
| `+0x00` | pointer to the ray record below |
| `+0x04` | `Q10Multiply(power, DamageArmor)` |
| `+0x06` | `Q10Multiply(power, DamageShield)` |
| `+0x08` | `SplashFactor`, the Q8 secondary-explosion fraction |
| `+0x0a` | pointer to the record's three `ImpactFX` arrays, indexed as one 12-entry array — see [`impact-effects.md`](impact-effects.md#which-effect-a-shot-spawns) |
| `+0x0e` | the owner machine, which the sweep skips |
| `+0x12` | a weapon-class code, a literal 5 on the beam path |

The ray record:

| Offset | Field |
|---|---|
| `+0x00` | pointer to the shot transform (rotation, muzzle world position in the translation) |
| `+0x04` | the ray's length — starts at the weapon's range and **is overwritten with each hit distance** as the sweep shortens it |
| `+0x08` | a literal 200, slack the range check adds before rejecting a candidate |
| `+0x0a` | the world-to-muzzle transform, cached by the sweep for every hit test to work in |

**Both damage figures are scaled Q10 by the shot's power**, against a capacitor scaled to 1200 — so a
mount holding more than 1024 makes a shot worth slightly more than the record's face value. (An
earlier note called this a Q8 scale; Q8 is `SplashFactor`'s own multiplier, one step further down in
`Mech_ApplyDirectFireDamage`.)

## Where the shot comes from — `WeaponMount_PrepareShot` (`0040e788`)

The frame is the **firing hardpoint's own model bone**, posed as it stands this tick and composed
with the machine's world transform. A beam follows the torso because the gun bone does: nothing adds
the twist or pitch angle, and nothing needs to.

The prologue also composes a per-hardpoint aim rotation over the top, from two node ids at `.GL`
`+2`/`+4`. **Both read -1 on every retail chassis**, so that rotation is the identity throughout the
retail fleet.

The muzzle point is three offsets summed in bone space:

```
template[0x40..0x44]                       // the weapon's own muzzle triple
+ hardpoint[0x10..0x14]                    // the .GL mount-point offset
+ WeaponMountTemplate_SideMuzzleOffset     // 0040f904, below
```

`WeaponMountTemplate_SideMuzzleOffset` is what makes a mirrored hardpoint pair fire from mirrored
points off one template. The template carries a lateral figure at `0x46` and a vertical one at
`0x4a`; the hardpoint's mounting code (`.GL +6`) picks one and its sign, and **only one axis is ever
nonzero**:

| `.GL +6` | Meaning | Offset |
|---|---|---|
| 0 | on top | `(0, 0, +0x4a)` |
| 1 | underneath | `(0, 0, -0x4a)` |
| 2 | left side | `(-0x46, 0, 0)` |
| 3 | right side | `(+0x46, 0, 0)` |
| 4 | invisible | `(0, 0, 0)` |

The prologue finally arms the refire timer as `Q10Multiply(mount+0x63, template[0x4c])`.
`mount+0x63` is `0x400` from the base constructor (`FUN_0040df30`) and nothing traced changes it, so
the delay is the template's own figure. `WeaponMount_RefireTick` (`0040ef94`) counts it down by
`SimTickDelta` — about 15 ticks for the 1200 most weapons carry. **`ELF` and `ELF2` carry zero**: a
continuous beam with no delay at all.

## Power level — `WeaponMount_AdjustPowerLevel` (`0040f48c`)

Energy mount vtable `+0x38`, reached by `WeaponMounts_HandleCommand` codes `0x0c`/`0x0d`/`0x4a`/`0x4e`
(`[-]`, `[=]`, keypad `[-]`, keypad `[+]`). Moves the charge target `+0x7b` by ±`0x50`, clamped to
0..1200. `WeaponMounts_IdleAllCapacitors` (`00410d04`, code `0x2c`) is the bulk counterpart, putting
every capacitor back to the idle 820.

**This is the only thing in the retail build that raises a capacitor past 820.**
`WeaponMount_DemandFullCharge` (`0040f4f0`) does the same in one step and is the obvious candidate,
but its only caller `FUN_00410d50` has no reference of any kind anywhere in the image — neither a
`CALL rel32` nor a stored address — so neither is ever reached.

For a fixed-cost weapon this changes nothing but the cockpit bar. For a charge-up weapon the target
*is* the shot strength: retail `PBEAM` at 960 does 937 damage every 48 ticks, and five presses of
`[-]` make it 546 every 28.

## Resolving the hit

`Bullet_FireBurst` calls `Sim_RaycastObjectList` (`00426528`) **before** it spawns any tracer, so the
hit is already resolved when the visual is built — see [`beam-visuals.md`](beam-visuals.md) for what
it then builds. The sweep and the per-mech hit test are documented in
[`damage-system.md`](damage-system.md); three properties matter to the caller:

- It **clips the ray at the ground first.** `Sim_RaycastTerrain` (`00428048`) walks the heightmap
  with `Terrain_RayWalk` before a single object is tested, so a machine behind a ridge cannot be shot
  through it — see
  [`../formats/terrain-heightmap.md`](../formats/terrain-heightmap.md#ray-versus-terrain--terrain_raywalk-0046e87c).
  The ray record's `+0x08` is passed along as a walk radius but the thin-ray mode never reads it.
- It **shortens the ray as it goes** rather than stopping at the first hit, so a candidate found
  later but nearer wins. It ends early only for a hit inside 500 units.
- The hit test *is* the damage application (mech vtable `+0x20`), so a candidate that is later
  superseded has still taken its damage.

A fully shield-absorbed shot still counts as a hit and still stops the ray — shields do not let fire
through to whatever stands behind.

## Not ported

- **Structures and aircraft.** Both have their own vtable `+0x20`; neither is ported, so beams pass
  through them.
- **Component damage.** Shield absorption is real; `Mech_SelectStruckComponent` and
  `Mech_ApplyDirectFireDamage` need the 29-slot component health array, which does not exist. Damage
  past shields is counted, not applied.
- **Sound.** `Bullet_FireBurst` opens with `FUN_004627dc(0x0b, muzzlePoint)`. Untraced past the call.
- **ELF and ELF2 beams draw straight.** Their tracer takes a jagged branch whose paint half is not
  decoded — see [`beam-visuals.md`](beam-visuals.md#elf-and-elf2--the-jagged-branch).
