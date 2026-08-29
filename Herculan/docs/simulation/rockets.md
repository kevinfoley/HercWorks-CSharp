# DBSIM.EXE launcher rounds (`PROJ.DAT` type `Missile`)

Solved 2026-08-27; addresses are DBSIM virtual addresses. Ported in
`Herculan.Engine.Sim.{Rocket, RocketCatalog}` and `SimWorld.FireRocket`.

The third and last fire branch. A `Beam` record resolves inside the call that fired it
([`beam-visuals.md`](beam-visuals.md)); a `Bullet` record becomes a travelling shot
([`projectiles.md`](projectiles.md)); a `Missile` record becomes one of these. Every missile
launcher — `MSL6`, `MSL8`, `MSL10`, `FLYMSL`, `BMSL` — fires one.

Like a bullet it lives in the effect pool (`DAT_004a9746`) that `Sim_MainTick` walks **before** the
machine list, cannot be shot at, and does not move on the tick that spawned it.

## `dat\ROCKETS.DAT`

`Rocket_LoadTypeTable_Unguided` (`0040a818`) reads it as `int16 count` then that many 14-byte
records, and loads `dts\ROCKETS.DTS` alongside it. **Indexed by the firing `PROJ.DAT` record's
subtype id** — `Rocket_GetTypeRecord` (`0040a234`) is `table + id * 14`.

**The layout is not `BULLETS.DAT`'s.** The two files share a stride and their first two fields and
nothing else; the readers are different functions reading different offsets.

| Offset | Field | Meaning |
|---|---|---|
| `+0x00` | `ModelId` | root of `ROCKETS.DTS` |
| `+0x02` | `Lifetime` | in **ticks** — a plain `+1` counter, not the bullet's `0x200` age units |
| `+0x04` | *`ClipRadius` in the shared parser* | acceleration, per 125 ms |
| `+0x06` | *`Unk2Flag`* | the shot record's slack, which is what a bullet keeps at `+0x04` |
| `+0x08` | *`SfxFireIdBullets`* | animation frame interval; 0 = static shape |
| `+0x0a` | *`Unk3Uint16`* | which of the shape's sequences that interval steps |
| `+0x0c` | `SfxFireIdMissiles` | sound id, played as `id + 10` |

The italicised property names are `ProjMissileDatEntry`'s, which are `BULLETS.DAT`'s; the shared
parser is bug-compatible with neither file's meaning and the engine reads through named accessors.

Retail (5 records, one per `Missile` subtype id):

| id | Weapon | Shape | Life | Accel | Slack | Anim | Seq | Sfx |
|---|---|---|---|---|---|---|---|---|
| 0 | `SARH` | 0 | 80 | 250 | 200 | 256 | 0 | 5 |
| 1 | `ARH` | 0 | 80 | 250 | 200 | 256 | 0 | 5 |
| 2 | `ARM` | 0 | 80 | 250 | 200 | 256 | 0 | 5 |
| 3 | `EO` | 0 | 80 | 250 | 200 | 256 | 0 | 5 |
| 4 | `BMSL` | 1 | 80 | 250 | 300 | 0 | 0 | 5 |

## Spawning — `Rocket_Fire` (`0040a9c4`)

`Rocket_Fire(missileId, muzzleWorldPoint, aimEulerTriple, ownerMech, ownerTravelSpeed)`, called
from `WeaponMount_FireDispatch_Missile` (`0040e964`) for a mount whose `PROJ.DAT` record has
`Type == 0`. **The magazine is spent before that type test**, so a launcher pays for its round on
the same line an autocannon does.

- **The aim triple goes in verbatim.** No `ROCKETS.DAT` field is a scatter and the spawn draws no
  random numbers — a launcher does not disperse.
- **Launch speed is a literal 500** plus the machine's own travel speed (mech vtable `+0x38`). The
  record's `Speed` is not read here; it is the ceiling the burn climbs toward.
- **The target is captured once, at launch**, into `+0x56` — the machine's selected target at
  `mech+0x1a4`, and only when mech vtable `+0x6c` (`Mech_MissileAmmoCount`, `004155ac`) returns
  nonzero. **That is not an ammunition count**, despite the name: it reads `manager+0x0a[subtype]`,
  the per-subtype *lock* flags — see [`missile-lock.md`](missile-lock.md). The one bypass: a machine
  that is *not* locally simulated firing subtype 3 skips the gate outright, so an AI's
  electro-optical missile always locks. A lock also asks the target for a node handle
  (target vtable `+0x54`) into `+0x5a`, which is the point the seeker steers at.
- Plays `record[+0x0c] + 10`.

Only the `Type == 0` class is ever built. `Rocket_ConstructGuided` (`0040ac3c`) builds a second
class for `Type == 3` records, **nothing calls it**, and its vtable's per-tick slot is
`FUN_0040acb4`, a stub returning zero — an instance would never move and never die. Retail's three
`Type 3` records are unreachable data.

## Flight — `Rocket_TickUpdate` (`0040a538`)

Vtable `+0x14` of `PTR_Bullet_Draw_00498448`; draw is `Bullet_Draw`, shared with the bullet class.

1. **Animation.** When `record[+0x08]` is nonzero, a countdown at `+0x5c` steps the shape instance's
   cell-frame entry for sequence `record[+0x0a]`, modulo the shape's own frame count for that
   sequence. This is the exhaust flame — see below.
2. **Age.** `+0x54 += 1`; expire at `record.Lifetime < age`, with no impact of any kind. A rocket
   burns out, it does not detonate on a timer.
3. **Burn**, damped: `speed += IntegrateRateOverTick(record[+0x04])`, then averaged with the speed
   the tick opened at, then capped at the `PROJ.DAT` record's `Speed` (`proj+0x0a`).
4. **Guidance** — `Rocket_PlayerSteer` when the owner is locally simulated *and* the subtype is
   3, `Rocket_HomingSteer` otherwise.
5. `step = IntegrateRateOverTick(speed)` along the frame's Y axis, then a `Sim_RaycastObjectList`
   over that step alone with `record[+0x06]` as the shot record's slack — the same sweep-the-segment
   arrangement a bullet uses. Struck anything and the round ends.

**Damage is never power-scaled**: a rocket comes off a rack, not a capacitor, so the `PROJ.DAT`
figures apply at face value. The shot record's `+0x12` carries the subtype id where a bullet
hardcodes 5; that field gates an unrelated target-side alert and nothing in the engine reads it.

Also here, both unported: the proximity beep once the round is within 40000 units of the camera's
machine, and the pair of globals (`DAT_0049c394`/`DAT_0049c398`) that tell the cockpit its missile
view is over.

**On retail data the speed cap is unreachable.** Every record's rate of 250 becomes 79 at the
simulation's timestep and 39 after the damping, so 80 ticks carry a round from ~540 to ~3600
against a ceiling of 6000 — it is still accelerating when it burns out, and `PROJ.DAT`'s `Speed`
sets nothing. Because the life is a tick count while the step scales with the timestep, a rocket is
the one shot whose **range** was frame-rate dependent in the original.

## Guidance — `Rocket_HomingSteer` (`0040a254`)

A steer of the euler angles, not of a velocity, as the plasma round's is — but with a real lead and
three gates the plasma round has none of.

- **Lead.** A round holding a node handle (`+0x5a >= 0`) steers at that node's world position
  (target vtable `+0x58`); otherwise at the target's extrapolated position (vtable `+0x24`, then
  either the raw origin or `FUN_00480330` through the target's own rebuilt frame).
  `Math_EulerToward` (`00492884`) turns that into a bearing triple; the two aiming components are
  moved toward it through `Math_RateLimitedMoveToward` at **`0x500` per 125 ms**, twice the plasma
  round's cap.
- **The emission gate.** Subtype 2 (`ARM`, anti-radiation) steers only while the target has
  `+0x96` (the scanner the pilot toggles, `FUN_0041b468`) or `+0xa1` (its jammer) set.
- **The spoofing wobble.** For every subtype but 2, when the *launching* machine's `+0x9c` is set,
  an aim error inside `±0xc00` is pushed **away** by `0xc00`, so the round weaves instead of
  converging. `Mech_PerTickSystemsUpdate` (`0041aa5c`) rolls that flag each tick the machine's
  selected target is jamming (`target+0xa1`), at roughly `20 * 0x29 / 0x1000` — or a quarter of
  that when a mount's `+0x7f` is below 0x33. **This is the mechanical form of the manual's ECM.**
- Subtype 3 instead sets the owner's `+0xb5`, which suppresses the AI's weapon selection for a tick
  while its own guided missile is in the air (`FUN_0041f5a0`).

## `Rocket_PlayerSteer` (`0040a488`) — the player flying the missile

Not a "non-homing variant". It reads two axis accumulators out of the **global player input block**
at `0x4d234a` (memset each frame by `FUN_0045a7f4`, also the VCR playback sink), steers by
`Q8Multiply(0x500, axis)` per tick with no rate limit and no deadband, and zeroes them. The gate
`0x4d2357` is "missile control active", which `Rocket_TickUpdate` clears when the round ends —
this is the electro-optical missile's nose camera. With that flag clear the function instead drops
the round's target and rewrites its subtype id to 0.

## The exhaust flame

Both `ROCKETS.DTS` roots are a `TSDetailPart` over four LODs (`details = [4, 12, 45, 255]`). At the
highest, the shape is a static body plus a **two-cell `TSCellAnimPart` holding geometry** — the
cells are flat-poly cones at the tail, and their surface colours are the palette's flame range
against the body's grey:

| | model-space centre Y | surface colours |
|---|---|---|
| body | 69 (root 0) / 139 (root 1) | 200 — grey `(116,116,116)` |
| flame cell 0 | 17 / 33 | 109, 94, 87, 86 — red `(224,4,0)` through orange |
| flame cell 1 | 8 / 17 | 93, 109, 86, 88 — pale yellow `(248,236,168)` through orange |

Both roots declare one sequence of two frames (`TSShape.SequenceList == [2]`, the `shape+0x20`
array the tick mods by) and every `TSCellAnimPart` in them carries `AnimSequence == 0` — the
sequence every `ROCKETS.DAT` record names. So the record's interval of 256 really does drive them,
at one cell every four ticks. `BMSL`'s record carries zero, so its flame is frozen on cell 0.

**There is no `ROCKETS.DBA` and no bank is bound.** Unlike `Bullet_LoadResources`, the rocket
loader never writes the shapes' bound-bank pointer, and the shapes hold no `TSBitmapPart` to want
one: a rocket is entirely ramp-coloured `TSSolidPoly`/`TSShadedPoly` geometry.

## Engine port

`Sim.Rocket`, `Sim.RocketCatalog`, `SimWorld.{FireRocket, RocketsInFlight}`,
`SceneModelLibrary.Rocket`, `MissionScene.RocketModels`. Deviations:

- **Homing works.** The target comes from `TargetSelection`, gated on the subtype's lock flag as the
  original gates it. The emission gate on subtype 2 is ported. Not ported: the node handle (`+0x5a`),
  so a round steers at the target's shape centre rather than a named part; and the ECM wobble, which
  needs `mech+0x9c` read at steer time.
- **The player's branch is not ported.** There is no missile view to feed it, and the original's
  no-input state is destructive (it drops the target and rewrites the subtype mid-flight), so a
  player-flown round flies straight instead of sitting in a state the original only passes through.
- **The flame is built as one mesh per cell.** `DtsMeshBuilder.BuildRoot` takes a cell index and
  `SceneModelLibrary.Rocket` returns the cells in order; the host picks by the round's own frame
  counter. That is the engine's equivalent of `TSCellAnimPart_Render` choosing one child — see
  [`../formats/dts-billboards.md`](../formats/dts-billboards.md).
- Sound is unported throughout.
