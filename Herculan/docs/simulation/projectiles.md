# DBSIM.EXE travelling projectiles (`PROJ.DAT` type `Bullet`)

Solved 2026-08-25; addresses are DBSIM virtual addresses. Ported in
`Herculan.Engine.Sim.{Projectile, BulletCatalog}` and `SimWorld.FireBullet`.

The other half of the fire dispatch. A `Beam` record carries `Speed == 0` and is over inside the
call that fired it ([`beam-visuals.md`](beam-visuals.md)); a `Bullet` record becomes a real object
that crosses the ground over several ticks. Every autocannon, every EMP cannon and the plasma cannon
fire one. Rockets and missiles are a third family and are still unported — see
[`damage-system.md`](damage-system.md).

Like a tracer, a bullet lives in the effect pool (`DAT_004a9746`) that `Sim_MainTick` walks **before**
the machine list, not in the object list the raycast sweeps. It cannot be shot at, and a round that
leaves the barrel this tick does not move until the next.

## `dat\BULLETS.DAT`

`Bullet_LoadResources` (`0040ade0`) reads it as `int16 count` then that many 14-byte records, and
loads `dts\BULLETS.DTS` and `dba\BULLETS.DBA` alongside it. **Indexed by the firing `PROJ.DAT`
record's subtype id**, as `BEAM.DAT` is — `Bullet_GetTypeRecord` (`0040adc0`) is `table + id * 14`.

| Offset | Field | Meaning |
|---|---|---|
| `+0x00` | `ModelId` | root of `BULLETS.DTS` |
| `+0x02` | `Lifetime` | in 125 ms units; the shot is dropped when its age passes `Lifetime * 0x200` |
| `+0x04` | `ClipRadius` | the shot record's `+0x08` slack, in place of a beam's literal 200 |
| `+0x06` | *was `Unk2Flag`* | animation frame interval; 0 = static shape |
| `+0x08` | `SfxFireIdBullets` | sound id, played as `id + 10` |
| `+0x0a` | *was `Unk3Uint16`* | **firing scatter**, in binary-angle units |
| `+0x0c` | *was `SfxFireIdMissiles`* | nonzero arms a per-lifetime rate at `obj+0x61`; consumer untraced |

Retail (12 records; the five not listed are unreachable — no `Bullet` record carries their id):

| id | Weapons | Shape | Life | Radius | Anim | Scatter |
|---|---|---|---|---|---|---|
| 0 | ATC20 | 0 | 20 | 100 | 0 | 63 |
| 1 | ATC35, ATC75 | 4 | 18 | 100 | 0 | 63 |
| 2 | ATC50, ATC100 | 5 | 16 | 100 | 0 | 63 |
| 6 | EMPC | 2 | 30 | 100 | 256 | 0 |
| 7 | BEMP | 3 | 30 | 200 | 256 | 0 |
| 8 | EMP2 | 2 | 30 | 100 | 256 | 0 |
| 9 | PLAS, MFAC, MAGN | 8 | 40 | 100 | 0 | 0 |

## Spawning — `Bullet_Fire` (`0040b43c`)

`Bullet_Fire(missileId, muzzleWorldPoint, aimEulerTriple, ownerMech)`. The powered form
`Bullet_FirePowered` (`0040b5a0`) is the same call with two fields written after it.

- **Geometry is one transform.** The object holds a euler triple at `+0x0c` and a transform at
  `+0x12` whose translation *is* the position (`+0x26`); the rotation is rebuilt from the triple
  whenever the dirty flag at `+0x32` says the angles moved.
- **Scatter** displaces euler components 0 and 2 by `(scatter * 2 & random) - scatter`. The mask is
  literally `scatter * 2`, not a power of two minus one, so the retail 63 draws odd values only.
  Component 1 is roll about the shot's own axis and is left alone.
- **Speed** is `ownerMech->vtable+0x38` (travel speed) **plus** the record's `Speed`, so a round
  fired from a machine running forward flies faster.
- `Bullet_FirePowered` adds `+0x56`, the capacitor charge the shot was fired at, and — for subtype 9
  alone — `+0x5b`, the firing machine's selected target at `mech+0x1a4`.

## Flight — `Bullet_TickUpdate` (`0040b124`)

1. Advance the shape's animation frame when the record's `+0x06` is nonzero.
2. `age += IntegrateRateOverTick(0x200)`; expire at `Lifetime * 0x200` with no impact of any kind.
3. Home, if a target was attached — `Bullet_HomingSteer` (`0040aff0`).
4. `step = IntegrateRateOverTick(obj+0x52)`, taken along the frame's Y axis.
5. Build a shot record (same layout as a beam's, see
   [`weapon-firing.md`](weapon-firing.md#the-shot-record)) with **the frame as the ray and the step
   as its length**, and run `Sim_RaycastObjectList`. A bullet therefore sweeps the segment it is
   about to cross rather than testing a point, which is what stops a fast round tunnelling through a
   machine between ticks.
6. Struck anything and the shot ends; struck nothing and it moves.

**Both damage figures are scaled Q10 by `+0x56` only when that is nonzero** — an autocannon round,
spent out of a magazine rather than a charge, does the record's face value. The beam dispatch spells
the same line unconditionally but can never pass zero.

### The plasma branch

Subtype 9 is singled out by literal value. It **zeroes the shot record's two damage figures** before
the sweep — the raycast only reports contact — and calls `Damage_ExplosiveBlastSweep` with a
4000-unit radius instead, plus a proximity fuze that detonates within 2000 units of the homing target
once the bearing error exceeds a quarter turn.

Homing is a steer of the **euler angles**, not of a velocity: the bearing to the target
(`Math_EulerToward`, `00492884`) drives euler 0 and 2 through `Math_RateLimitedMoveToward` at
`0x280` per 125 ms.

## Engine port

`Sim.Projectile`, `Sim.BulletCatalog`, `SimWorld.{FireBullet, Projectiles, Impacts}`. Deviations:

- **Plasma keeps its direct-fire damage** rather than being zeroed, because there is no blast sweep
  yet; an unported explosion should cost the weapon its splash, not its shot.
- **Nothing homes.** The guidance is ported but reads the firing machine's selected target, and the
  engine has no target selection — the same result the original gives with nothing selected.
- **The shape's animation frame is not advanced.** The countdown is kept; nothing steps a
  shape-instance frame index.
- **The three EMP rounds are invisible.** `BULLETS.DTS` roots 2 and 3 are a `TSCellAnimPart` of five
  `TSBitmapPart`s — a flipbook of billboard sprites, not geometry — and the engine has no
  world-space sprite path. They simulate and do damage.
- Sound is unported throughout.

## How a round is drawn

`Bullet_Draw` (`0040a120`) is the class's vtable slot 0: it zeroes `DAT_004a5b1c` for the duration
(which is what makes a projectile's textured polys fullbright, see
[`../formats/dts-texture-binding.md`](../formats/dts-texture-binding.md)), installs the object's frame
as the model transform, renders the shape instance at `+0x34`, and restores.

Reaching it, a round is bucketed by terrain cell into `ObjList::drawTable` and then drawn from a
depth-sorted render entry that carries its distance — so it is **distance-fogged like any other
object**, not drawn at a fixed ramp row. The full path and the evidence are in
[`../formats/distance-fog-and-sky.md`](../formats/distance-fog-and-sky.md).
