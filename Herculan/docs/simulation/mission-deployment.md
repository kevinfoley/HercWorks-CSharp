# Mission actions, deployment and drop pods (DBSIM.EXE)

Addresses are DBSIM virtual addresses. Ported in `Herculan.Engine.Sim`: `MissionActionState`,
`MissionActionTimerState`, `MissionTriggers`, `MissionGroup.DeploymentCheck`, `Deployment` and
`MeteorObject`.

A **mission action** is a `script.dat` block-5 record: a one-shot latch with consequences hanging
off it. Something activates it, and everything waiting on it acts. It is the only scripting the
simulation has — mission progression, reinforcement waves and the drop pods are all this one
mechanism.

See [`../formats/script-dat.md`](../formats/script-dat.md) for the record layouts and for how
groups are placed in the first place.

## The four ways an action activates

`Action_Activate` (`00423430`) is one-shot: it sets the action's runtime activation flag (in-memory
`+0x0a`, zeroed at load), walks the ten (counter ref, operation) pairs at `+0x0c`/`+0x20` bumping
(op 6) or clearing (op 5) the mission-counter array `DAT_004a9ef4`, and queues the message at
`+0x34`. **The
message queue is inside the counter loop**, so an action naming five counters posts its line five
times and one naming none posts it not at all.

| activated by | site | condition |
|---|---|---|
| its own trigger areas | `Actions_EvaluateTriggers` (`00426b70`) | a subject stands in one of them |
| an action timer | `ActionTimer_Tick` (`004230a4`) | the timer's delay runs out |
| an object being engaged | `Detection_Sweep` (`004128f8`) | that object's `+0x1b2`, at 50000 units |
| an object being defeated | four sites below | that object's `+0x1b6` |

**None of these is the primary and the others fallbacks.** One action commonly carries two routes —
in the shipped mission, action 0 has both a trigger area and a machine whose death activates it, and
whichever happens first wins.

Both per-frame evaluators run from `Sim_MainTick` (`0045f464`), back to back and **after** the group
pass, not before it:

```
per group: group+0x14 ? Group_DeploymentCheck : Group_OrderTick
FUN_00426b48      // every action timer
Actions_EvaluateTriggers(PlayerMech)
```

So a group waiting on an action arrives on the tick *after* it activates.

### Trigger areas — `Actions_EvaluateTriggers` (`00426b70`)

Walks the whole action array and, per action, picks whose position `Action_TestTrigger`
(`004234b8`) is offered. The action's **type** (in-memory `+0x00`) selects the subject:

| type | subject |
|---|---|
| 0 | the player's mech |
| 1 | every member of the player's group |
| 2 / 3 | every member of every deployed group of side 0 (human) / side 1 (Cybrid) |
| 4 / 5 / 6 | every member of every deployed mech / flyer / base group |
| 7 / 8 / 9 | the action's own resolved target object (`+0x36`) |
| 10 | every member of the action's own resolved target group |

**The deployment gate is part of the test**: types 2-6 skip a group whose `+0x14` is still set, so an
undeployed group cannot trip an action, including the one it is itself waiting on. Each sweep stops
at the first subject that activates the action.

`Action_TestTrigger` returns "in the area" for an action that has already activated without testing
anything, so the caller stops offering subjects. Otherwise it offers the position to each resolved
block-4 area in turn (count `+0x04`, pointer array `+0x06`) and activates it on the first hit.

`DBSim_SpawnMissionObjects` (`004253d8`) resolves `+0x36` in a final pass over the array: types 7/8/9
resolve it as a mech/flyer/base roster slot, type 10 as a group, and types 0-6 have it zeroed.

#### The areas — block 4, resolved by `TriggerArea_Resolve` (`00423358`)

A block-4 record resolves to 10 bytes: type flag at `+0`, a block-1 coordinate pointer at `+2`, and
at `+6` either a second coordinate pointer (type 0) or **the record's literal × 10** (type != 0).
`TriggerArea_ContainsPoint` (`004233a4`) tests a position against it:

- **type 0** — axis-aligned XY box strictly between the two coordinates. Z is ignored, so a box
  catches anything standing over its footprint however high.
- **type != 0** — ground-plane distance from the coordinate is less than the stored radius.

The resolved list stops at the **first negative ref**, not at the eighth slot: the load pass counts
up to the first `-1` and allocates exactly that many, so a populated slot behind a gap is never
tested.

### Action timers — `ActionTimer_Tick` (`004230a4`)

The mission's timer, and the reason an action carrying no trigger area of its own is ordinary rather
than dead. One block-6 record names a primary action, a delay and up to ten actions to activate:

```
if (timer.primary == null || timer.primary.activated) {
    if (Timer_CountDown(&timer.countdown) == 0) {
        for each of the ten sequence refs: if set, Action_Activate(it)
        re-arm the timer with 30000
    }
}
```

The delay is the file's stored value `<< 11`, so its unit is 2.048 s. A timer with no primary runs
from mission start; one with a primary runs from the moment that action activates. The re-arm is
through the same shift — about seventeen hours — and by then every action the timer names has
activated, so the later expiry does nothing.

Chaining two of them staggers a sequence: `script6.dat` has action 1 arm a 92-second timer that
activates action 2, which arms a 123-second timer that activates action 3.

### An object's own two actions — `+0x1b2` and `+0x1b6`

Every mech, flyer and structure carries two action pointers, resolved by
`DBSim_SpawnMissionObjects` from its roster record's own refs (block 7 `0x80`/`0x82`, block 8
`0x56`/`0x58`, block 9 `0x2e`/`0x30`).

**`+0x1b2` — engaged.** `Detection_Sweep` activates it when a hostile that already has contact on this
object closes to 50000 units; both parties latch `+0x9e` and both activate their own. It is gated on
`obj+0xa2` being clear — **no writer of that byte has been located**, so what would suppress the
activation is open.

**`+0x1b6` — defeated.** Four sites, and they are the four ways an object stops being a threat:

| site | when |
|---|---|
| `Mech_ComponentDamageWrite` (`00417de4`) | the machine dies (both of that function's death branches) |
| `Flyer_ComponentDamageWrite` (`00421bb4`) | component 0 goes |
| `Base_ApplyDamage` (`00404d70`) | the last component goes — [`hit-detection.md`](hit-detection.md) |
| `Ai_ChooseWeapon` (`0041f358`) | the machine runs out of working weapons — [`ai-weapons.md`](ai-weapons.md) |

**This is how a retail mission chains its reinforcement waves.** The shipped `script.dat` names one
on five of its ten mech records; see the worked example below.

## The deployment gate — `group+0x14`

`DBSim_BuildGroupRecord` (`00423b34`) resolves the block-11 record's action ref (record `0x70`) into
the group record's `+0x14` pointer. Non-null means "not deployed yet", and three places test it:

| site | effect when non-null |
|---|---|
| `maybe_Scene_SubmitFrameObjects` (`0042841c`) | the mech, flyer or base is **not submitted for drawing** |
| `Sim_MainTick` (`0045f464`) | the group runs `Group_DeploymentCheck` (`004236c4`) **instead of** `Group_OrderTick` (`00423a74`); a base's own `+0x18` tick is skipped outright |
| `Mech_CollisionTest` (`00418f74`) | the object is skipped before any distance is measured |

`Deployment_PickPointNearPlayer` (`0042354c`) and `Actions_EvaluateTriggers` apply the same test, so
an arriving group never picks a landing point on top of one that has not arrived, and an undeployed
group cannot trip a trigger.

**An undeployed group's placed position is therefore meaningless.** It is placed by the ordinary
rules — usually on its route's first waypoint, which mission authors routinely share with the
player's own squad — so several such groups commonly sit stacked on one point. Harmless in the
original, because nothing above can see or touch them.

## Arrival — `Group_DeploymentCheck` (`004236c4`)

Runs every frame for every waiting group; does nothing until that group's action has activated. Once it
has, the action's **verb** (in-memory `+0x02`) picks how the group turns up. Every arrival point is
relative to the player and comes from `Deployment_PickPointNearPlayer`.

| verb | arrival | distance | bearing, relative to the player's heading |
|---|---|---|---|
| 2 | drop pod | 150,000 | `0x4000 - (rand & 0x7fff)` — ±90° |
| 3 | drop pod | 150,000 | `0x1000 - (rand & 0x1fff)` — ±22.5° |
| 4 | on foot | 90,000 | `-0x7000 - (rand & 0x1fff)` — behind, ±22.5° |
| 5 | on foot | 150,000 | `0x2000 - (rand & 0x3fff)` — ahead, ±45° |
| other | in place | — | — |

**On foot** places the group's leader at that point facing `bearing - 0x8000` (the bearing itself,
not the player's heading plus it), runs each other member through its own vtable `+0x78` formation
offset, and clears `group+0x14` immediately. Only the leader is turned.

**In place** (any other verb, e.g. verb 1) just clears `group+0x14`, so the group goes live where it
already stands — the one arrival for which the placed position is not a placeholder.

**Drop pod** spawns a `METEOR` and leaves `group+0x14` set; the pod clears it on landing. A byte at
`group+0x13` latches the launch, so a group only ever gets one pod.

### Picking the point — `Deployment_PickPointNearPlayer` (`0042354c`)

Offsets from the player's position by the caller's distance at (player heading + the caller's
bearing), then steps outward in 2,000-unit increments until the point clears three tests, in order:

1. **Deployed objects** — within their own `+0x7c` collision radius plus 5,000. **Applied only for
   the two walk-on verbs**: the caller's fourth argument is 1 there and 0 for a drop pod, so a pod
   is allowed to come down on top of a machine. That is what its landing-blast latch exists for.
2. **Structures** — `Structure_GatherWalkCandidates` (`00404ae4`) at radius 5,000, the same volume
   sweep a walking machine is stopped by ([`hit-detection.md`](hit-detection.md)).
3. **The ground** — `Terrain_FaceBlocksAt` (`0046fe84`) with a zero direction, which reduces it to
   the steepness of the face under the point; off the grid blocks
   ([`ai-navigation.md`](ai-navigation.md)).

The loop is unbounded in the original and cannot fail in practice, since the first point is usually
clear.

## The drop pod — `METEOR`

Its own class, pool (`g_MeteorPool`, `004a972e`) and resources: `Meteor_LoadResources` (`00409a34`)
loads `dts\meteor` and the `dba\impact` texture bank and binds the bank into every shape.

`Meteor_Construct` (`00409b44`) stores the group pointer at `+0x55` and puts the pod on a **slanted**
approach rather than dropping it straight down. It draws a heading and a **horizontal run-in** of
70,000 plus up to 25,000 units, places itself that far short of the target along that heading, and
flies in at a flat 2,000 units per tick:

```
n      = runIn / 2000                  // ticks of flight
height = (n * n >> 1) * 50             // 30,600 to 55,200 units up
vz     = -height / n
```

**The 70,000-95,000 figure is the run-in, not the altitude.** The launch height is derived from it —
what a constant 50 units/tick² would need over that many ticks — and both axes are then flown at
constant velocity, so the descent is a straight line. Reading the drawn figure as a height puts the
pod at twice the altitude it belongs at.

`Meteor_Tick` (`00409d2c`), walked from `Sim_MainTick` over the pool *before* the group pass, has
two phases:

1. **Falling** (`+0x4b == 0`). Integrates position by the velocity at `+0x45`, pitches the shape to
   `atan2(vz, 2000)` so it faces its fall line, plays sound `0x2f` once below absolute height 50,000,
   and on ground contact (`Terrain_HeightQuery`) sets the landed flag, snaps to ground height, plays
   sound `0x30` and detonates `Damage_ExplosiveBlastSweep(pos, 3000, 10000, 0, null)`. **If anything
   was in range it sets `+0x4c`** — the sweep answers on range alone, so a machine whose shields
   swallow the blast still trips the latch. It is the terrain query and not the flight-time count that
   ends the fall, so a pod aimed at ground well below the player keeps flying past its target.
2. **Landed.** Advances `+0x4d` at rate `0x5dc` per tick and drives the shape's frame counter from
   `+0x4d >> 10` — the pod opening. When that reaches the shape's frame count: if the pod carries a
   group and **`+0x4c` is clear**, it copies its own landed position onto the group's **leader** and
   clears `group+0x14`, which is the moment the group becomes real. A pod that landed on something
   delivers nothing, and the group it carried stays out of the mission for the rest of the run. It
   then spawns a leftover effect from the theater's flat-shape pool at the site, releases its shape
   instance and returns 1, and `Sim_MainTick` frees it.

`Meteor_Render` (`00409cd0`) draws the plain shape (root 0) while falling and the opening
animation's own shape instance at `+0x41` (root 1) once landed.

Only the leader is repositioned; the rest of the group follows under its orders. Every retail
drop-pod group has exactly one member.

## The mission counters — `DAT_004a9ef4`

1,000 shorts: `FUN_0042412c` writes 2,000 bytes of the block to `mission_var` as a mission ends, so
these are the **campaign's** variables and their reader is outside the simulation. Two things write
them during a mission: `Action_Activate`, and a group's own completion hook `FUN_00423f30` (ops 1 clear,
2 increment, 0x0d-0x10 set to op − 0x0c), which is not ported.

The reader is VSHELL's `MissionVar_Read` (`0040ea59`), which loads the file straight back into the
same array — `00482af8` there, the store the `.msn` condition opcodes test and every save slot
carries. VSHELL also writes `mission_var` from that array before launching a mission
(`MissionVar_Write`, `0040e9cb`); whether DBSIM reads it at mission start, rather than only writing
it at the end, is a question for the DBSIM side. See
[`../shell/campaign-loop.md`](../shell/campaign-loop.md).

## The shipped mission, end to end

A worked example, because it is the only place the four mechanisms are visible together. The live
`script.dat` fields 3 actions, 1 trigger area, 0 action timers, and 8 Cybrid HERCs in six groups:

| stage | what activates it | who arrives |
|---|---|---|
| start | — | group 2, one ACHILLES, north of the player; group 8, two flyers |
| wave 1 | group 2's machine dies (`+0x1b6` → action 0), **or** it walks into action 0's circle | groups 3 (two ACHILLES) and 4 (one), **in place** |
| wave 2 | either of group 3's machines dies (`+0x1b6` → action 1) | group 5, a HEADHUNTER and a HYPERION, in place |
| wave 3 | either of group 5's machines dies (`+0x1b6` → action 2) | groups 6 and 7, one ACHILLES each, **by drop pod** |

Action 0's circle is centred at (1005988, 1058404) with radius 150,000 and its subject is type 3 —
deployed **Cybrid** groups, not the player. The player spawns inside it at 64,132; group 2's ACHILLES
spawns north at 252,252 and walks south, crossing in at tick 1222 (~49 s at 25 Hz). So the first wave
arrives on its own if the player does nothing, and sooner if the player kills the machine. Actions 1
and 2 carry no area, so their only route is the kill.

## What is ported

`MissionLoader` resolves blocks 4, 5 and 6 and every action ref — a group's `0x70`, an order's
`+0x12`, a timer's, and each roster record's own two. `MissionScene` builds the runtime states and
binds them. `SimWorld` holds the action array, the timer array, the counters and the pod pool, and
ticks them in `Sim_MainTick`'s order.

`MissionGroup.AwaitingDeployment` is `group+0x14` and `SimObject.AwaitingDeployment` is a *read* of
it, so the two cannot disagree; all three gate effects are honoured.

Not ported, and each is a gap in something else rather than in this layer:

- **The message an action queues.** The id is decoded (`+0x34`, already decremented at load) and
  carried, but it names a `data\mission.str` line and that file is not loaded.
- **The pod's leftover ground mark**, which comes from the theater's `flat`/`flat2` shape pool.
- **The counters' reader**, which is the campaign layer.
- `obj+0xa2`, the suppressor on the engagement action — see above.

The two walk-on verbs are implemented but unexercised: no mission has been found that uses them.
Flyers do not move in this engine, so a flyer group can never trip a trigger area it would reach in
retail — which can make a trigger activate later here than it does there.

## Rejected readings

| reading | why it is wrong |
|---|---|
| An action with no trigger area is unreachable | Three other things activate it; a mission's later actions routinely carry no area at all |
| `Meteor_Construct`'s 70,000-95,000 is the spawn altitude | It is the horizontal run-in. The altitude is derived from it and is 30,600-55,200 |
| `Deployment_PickPointNearPlayer` avoids deployed objects | Only for the walk-on verbs; a drop pod's point is picked without that test |
| `Actions_EvaluateTriggers` runs before the group pass | `Sim_MainTick` runs it after, so a group arrives a tick after its trigger |
| `obj+0x1b6` is a death action | It is also activated when a machine runs out of weapons |
