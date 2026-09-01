# Mission deployment — triggers, drop pods and walk-ons (DBSIM.EXE)

Addresses are DBSIM virtual addresses. Partially ported in
`Herculan.Engine.Sim.SimObject.AwaitingDeployment` — the gate is honoured, arrival is not.

Not every unit a mission places is in the world when the mission starts. A `script.dat` block-11
group whose record names a block-5 action is **waiting on that action**: it exists as an object but
is undrawn, unsimulated and non-solid until the action fires, at which point the group arrives —
either on foot from off-screen or in a **drop pod** falling out of the sky. This is how the retail
missions deliver Cybrid reinforcements mid-mission.

See [`../formats/script-dat.md`](../formats/script-dat.md) for the file format and for how groups
are placed in the first place.

## The gate — `group+0x14`

`DBSim_BuildGroupRecord` (`00423b34`) resolves the block-11 record's action ref (record `0x70`) into
the group record's `+0x14` pointer. Non-null means "not deployed yet", and three places test it:

| site | effect when non-null |
|---|---|
| `maybe_Scene_SubmitFrameObjects` (`0042841c`) | the mech, flyer or base is **not submitted for drawing** |
| `Sim_MainTick` (`0045f464`) | the group runs `Group_DeploymentCheck` (`004236c4`) **instead of** `Group_OrderTick` (`00423a74`); a base's own `+0x18` tick is skipped outright |
| `Mech_CollisionTest` (`00418f74`) | the object is skipped before any distance is measured |

`Deployment_PickPointNearPlayer` (`0042354c`) applies the same test, so an arriving group never
picks a landing point on top of one that has not arrived yet.

**An undeployed group's placed position is therefore meaningless.** It is placed by the ordinary
rules — usually on its route's first waypoint, which mission authors routinely share with the
player's own squad — so several such groups commonly sit stacked on the player's spawn point.
Harmless in the original, because nothing above can see or touch them. The shipped mission-10
handoff does exactly this with its three DIABLO groups.

## Firing the action

`Actions_EvaluateTriggers` (`00426b70`), called every frame from `Sim_MainTick` with the player's
mech, walks the whole block-5 action array. Each action's **type** (in-memory `+0x00`) selects whose
position is tested:

| type | subject |
|---|---|
| 0 | the player's mech |
| 1 | every member of the player's group |
| 2 / 3 | every member of every deployed group of side 0 (human) / side 1 (Cybrid) |
| 4 / 5 / 6 | every member of every deployed mech / flyer / base group |
| 7 / 8 / 9 | the action's own resolved target object (`+0x36`) |
| 10 | every member of the action's own resolved target group |

`Action_TestTrigger` (`004234b8`) offers the subject position to each of the action's block-4
(row #9) records in turn; the first hit calls `Action_Fire` (`00423430`), which sets the action's
runtime **fired flag** (in-memory `+0x0a`, zeroed at load), bumps the mission counters its records
name, and queues the mission message at `+0x34`. The flag is one-shot.

### Trigger areas — block 4, resolved by `FUN_00423358`

A block-4 record resolves to 10 bytes: type flag at `+0`, a block-1 coordinate pointer at `+2`, and
at `+6` either a second coordinate pointer (type 0) or **the record's literal × 10** (type != 0).
`FUN_004233a4` tests a position against it:

- **type 0** — axis-aligned XY box strictly between the two coordinates. Z is ignored.
- **type != 0** — distance from the coordinate is less than the stored radius.

## Arrival — `Group_DeploymentCheck` (`004236c4`)

Runs every frame for every waiting group; does nothing until that group's action has fired. Once it
has, the action's **verb** (in-memory `+0x02`) picks how the group turns up. Every arrival point is
relative to the player and is produced by `Deployment_PickPointNearPlayer` (`0042354c`), which
offsets from the player's position at (player heading + the drawn bearing) and steps outward in
2,000-unit increments until the point clears every deployed object, `FUN_00404ae4`'s static
obstacles and the terrain validity test.

| verb | arrival | distance | bearing, relative to the player's heading |
|---|---|---|---|
| 2 | drop pod | 150,000 | `0x4000 - (rand & 0x7fff)` — ±90° |
| 3 | drop pod | 150,000 | `0x1000 - (rand & 0x1fff)` — ±22.5° |
| 4 | on foot | 90,000 | `-0x7000 - (rand & 0x1fff)` — behind, ±22.5° |
| 5 | on foot | 150,000 | `0x2000 - (rand & 0x3fff)` — ahead, ±45° |
| other | in place | — | — |

**On foot** places the group's leader at that point facing `bearing - 0x8000`, runs each other member
through its own vtable `+0x78` formation offset, and clears `group+0x14` immediately.

**In place** (any other verb, e.g. verb 1) just clears `group+0x14`, so the group goes live where it
already stands.

**Drop pod** spawns a `METEOR` and leaves `group+0x14` set — the pod clears it on landing.

## The drop pod — `METEOR`

Its own class, pool (`g_MeteorPool`, `004a972e`) and resources: `Meteor_LoadResources` (`00409a34`)
loads `dts\meteor` and the `dba\impact` texture bank and binds the bank into every shape.

`Meteor_Construct` (`00409b44`) puts it at the picked point, 70,000-95,000 units up (a 25,000-unit
random spread over a 70,000 base), on a random heading, with a downward velocity and a constant
acceleration. It stores the group pointer at `+0x55`.

`Meteor_Tick` (`00409d2c`), walked from `Sim_MainTick` over the pool, has two phases:

1. **Falling** (`+0x4b == 0`). Integrates position by the velocity at `+0x45`, pitches the shape to
   `atan2(2000, vz)` so it faces its fall line, plays sound `0x2f` once below 50,000, and on ground
   contact (`Terrain_HeightQuery`) sets the landed flag, snaps to ground height, plays sound `0x30`
   and detonates `Damage_ExplosiveBlastSweep(pos, 3000, 10000, 0, null)`. If that blast hit
   anything it sets `+0x4c`.
2. **Landed.** Advances `+0x4d` at rate `0x5dc` per tick and drives the shape's frame counter from
   `+0x4d >> 10` — the pod opening. When that reaches the shape's frame count: if the pod carries a
   group and `+0x4c` is clear, it copies its own landed position onto the group's **leader** and
   clears `group+0x14`, which is the moment the group becomes real. It then spawns a leftover effect
   from pool `004a9711` at the site, releases its shape instance and returns 1, and `Sim_MainTick`
   frees it.

Only the leader is repositioned; the rest of the group follows under its orders. Every retail
drop-pod group has exactly one member.

## What is ported

`MissionLoader` marks a placement `AwaitingDeployment` when its block-11 record's `RefRow10` is set,
and `SimObject.AwaitingDeployment` reproduces all three gate effects — undrawn (the host skips it
when building the scene), unticked (`SimWorld.Tick`) and non-solid (`MechObject.CollisionTest`).

Nothing clears the flag: triggers, arrival and the pod itself are not implemented, so a group that
waits on an action stays out of the mission for the whole run. In the shipped mission-10 handoff
that is 5 of 22 objects — three DIABLOs on verb 3 (drop pod) and two ACHILLES on verb 1 (in place).
