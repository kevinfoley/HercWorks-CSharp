# Impact effects (`dat\EXPLOS.DAT`, DBSIM.EXE `EXPLO.CPP`)

Solved 2026-08-26; addresses are DBSIM virtual addresses. Ported in
`Herculan.Engine.Sim.{ExplosionCatalog, ImpactEffect}`, `HercWorks.Core`'s `ExplosionData`.

What happens where a shot lands. An effect is a `dts\EXPLOS.DTS` root standing still at the point of
impact, playing its flipbook of billboards ([`../formats/dts-billboards.md`](../formats/dts-billboards.md))
through exactly once. Like a tracer or a travelling round it lives in the effect pool
(`DAT_004a96a2`) that `Sim_MainTick` walks ahead of the machine list, so nothing can shoot it and
nothing collides with it.

## Resources — `FUN_00407b54`

Loaded once at startup:

- **`dba\EXPLO0.DBA`..`EXPLO14.DBA`**, fifteen banks, from the name template `explo666` at
  `00497ba0` (the loader overwrites from the sixth character on with the index). With
  `CockpitArt_LoadOnDemand` set it loads thirteen `EXPLO<n>S.DBA` banks instead.
- **`dts\EXPLOS.DTS`** (or `EXPLOS2.DTS`), 20 roots, every one a `TSCellAnimPart` of
  `TSBitmapPart`s.
- **`dat\EXPLOS.DAT`**, below.

## `dat\EXPLOS.DAT`

```
int16 shapeCount
{ int16 animSequence; int16 textureBankIndex; }[shapeCount]
int16 typeCount
byte[0x28][typeCount]
```

Retail is 964 bytes: 20 shapes, 22 types, nothing left over. `shapeCount` matches `EXPLOS.DTS`'s root
count exactly — the table's first half is one row per root, in order, and the loader writes
`shape->boundBank = banks[textureBankIndex]` straight into each shape instance's own bank pointer.

`animSequence` is the cell-animation sequence the effect drives; zero on every retail row, matching
every `TSCellAnimPart` in `EXPLOS.DTS`. Negative means the shape has no flipbook.

### Type row (0x28 bytes)

Reached as `table + typeId * 0x28` (`FUN_00407b20`).

| Offset | Field | Meaning |
|---|---|---|
| `+0x00` | `ShapeIndex` | which shape row, i.e. which `EXPLOS.DTS` root |
| `+0x02` | `FrameInterval` | ticks each flipbook frame is held; **1 on every retail row** |
| `+0x04` | `TrailEffect` | nonzero attaches a second effect object; **0 on every retail row** |
| `+0x06` | `LightMode` | nonzero attaches a light source; 0, 1 or 2 in retail |
| `+0x08`..`+0x1f` | `FrameIntensity[12]` | the light's intensity per frame; low byte passed to the light as each frame is stepped |
| `+0x20` | `ProximityRadius` (int32) | radius the effect's own query slot (`FUN_00408100`) reports a hit inside; 0 or 20000 |
| `+0x24` | `SoundId` | played as `id + 10`; negative is silent |
| `+0x26` | `ObjectClass` | 0 registers the effect under class tag 2, else 8 |

## Construction — `FUN_00407f1c`

`(effect, typeId, worldPoint, ownerObject, playSound)`. Resolves the shape through the two tables,
resets the shape instance's frame counter for the type's sequence to 0, loads the countdown from
`FrameInterval`, and optionally builds the light and the trail object. `playSound` gates the
`SoundId` call, not the effect.

## Tick — `FUN_0040813c`

```
if (CountdownTimerTick(effect+0x4a) != 0) return alive;   // counter is the short at +0x4b
frame = (frame + 1) % shapeFrameCount;
if (frame == 0) return finished;          // the flipbook wrapped: the effect is over
light?.SetIntensity(FrameIntensity[frame]);
timer = FrameInterval;                    // i.e. effect+0x4b
return alive;
```

Nothing moves it and nothing else can stop it. The frame count comes off the loaded shape
(`shape+0x20`'s per-sequence array), not off the table.

The tick argument is the record base, not the counter: `Math_CountdownTimerTick` reads the `short` at
`+1` from the pointer it is given. See the countdown-timer entry in `dbsim-physics-notes.md`.

## Which effect a shot spawns

Every `PROJ.DAT` record carries three four-entry `ImpactFX` arrays. The shot record's `+0x0a`
(see [`weapon-firing.md`](weapon-firing.md#the-shot-record)) points at all three as one twelve-entry
`short` array, indexed `group * 4 + (rand & 3)` — so all four ids in a group are equally likely and
the file's own field order is the group order.

| Group | `ProjectileData` field | Spawned by | Site |
|---|---|---|---|
| 0 | `ImpactFXShield` | a shot the struck facing's shields **fully** absorbed | `Mech_DirectFireHitTest`, base `+0` |
| 1 | `ImpactFXGround` | a shot ending on **terrain**; also an armour hit that left the struck component in the health band it was already in | `Sim_RaycastObjectList` tail, base `+8`; `Mech_ApplyDirectFireDamage` with `group == 1` |
| 2 | `ImpactFXArmor` | an armour hit that dropped the component's health band; also the only array the non-mech hit test (`FUN_00405038`) uses | `Mech_ApplyDirectFireDamage` with `group == 2`, base `+0x10` |

**All 27 retail records carry byte-identical `ImpactFXGround` and `ImpactFXArmor` arrays**, so groups
1 and 2 are indistinguishable on real data.

### The terrain impact is the raycast's own job

`Sim_RaycastObjectList` (`00426528`) keeps two flags — *something was struck* and *an object was
struck* — and at its tail spawns an effect when the first is set and the second is not:

```
point = rayTransform.TransformPoint(0, rayLength, 0);
Explosion_Construct(alloc(pool), impactFx[4 + (rand & 3)], point, owner: 0, playSound: false);
```

So a shot that ends in the dirt puts one down and a shot that ends on a machine does not, even though
the ground clipped the ray first in both cases. **The ground pseudo-object `Sim_RaycastTerrain`
installs at `DAT_004aab58` has nothing to do with it** — its fields are written and never read.
Unlike every object-hit spawn this one passes no owner and suppresses the sound.

The object-hit sites spawn from inside the hit test itself, at `transform(0, hitDistance, 0)` off the
shot's frame, and do so whether or not the sweep goes on to find something nearer.

## Engine port

`SimWorld.{Explosions, Effects, SpawnImpactEffect, PickImpactEffect}`, `MissionScene.ExplosionModels`.
Deviations:

- **No light, no sound, no trail object.** All three belong to systems that do not exist; no retail
  row asks for a trail anyway.
- **Group 2 is selected, but indistinguishably.** The split from group 1 is a change in a component's
  health band, which `MechObject.ApplyDirectFireDamage` measures either side of the write, so both
  branches are reachable. It costs nothing on retail data, per the note above: the two arrays hold
  the same rows.
- **`ProximityRadius` is unread** — nothing queries it.
- The shape frame counts are supplied to `ExplosionCatalog` by `MissionScene` after it builds the
  shapes, since they are a property of `EXPLOS.DTS` rather than of the table.
