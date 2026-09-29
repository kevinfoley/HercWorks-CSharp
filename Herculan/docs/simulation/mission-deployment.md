# Mission actions, deployment and drop pods (DBSIM.EXE)

Addresses are DBSIM virtual addresses.

A **mission action** is a `script.dat` block-5 record: a one-shot latch with consequences hanging off it. Something activates it, and everything waiting on it acts. It is the only scripting the simulation has — mission progression, reinforcement waves and the drop pods are all this one mechanism.

See [`../formats/script-dat.md`](../formats/script-dat.md) for the record layouts and for how groups are placed in the first place.

## The four ways an action activates

`Action_Activate` (`00423430`) is one-shot: it sets the action's runtime activation flag (in-memory `+0x0a`, zeroed at load), walks the ten (counter ref, operation) pairs at `+0x0c`/`+0x20` bumping (op 6) or clearing (op 5) the mission-counter array `DAT_004a9ef4`, and queues the message at `+0x34`. **The message queue is inside the counter loop**, so an action naming five counters posts its line five times and one naming none posts it not at all.

The message goes to the **pilot and squad** port (`view+0x207`, through `CockpitView_GetSquadMessagePort`, `00433158`), not the computer's ticker, as `{id, null}` — no subject. That port looks a speakerless id up in `str\COMMAND0.STR` — `COMMAND<n>.STR` in training mission `n` — not in `data\mission.str`; see [`../formats/cockpit-messages.md`](../formats/cockpit-messages.md#its-speakerless-set).

| activated by | site | condition |
|---|---|---|
| its own trigger areas | `Actions_EvaluateTriggers` (`00426b70`) | a subject stands in one of them |
| an action timer | `ActionTimer_Tick` (`004230a4`) | the timer's delay runs out |
| an object being engaged | `Detection_Sweep` (`004128f8`) | that object's `+0x1b2`, at 50000 units |
| an object being defeated | four sites below | that object's `+0x1b6` |

**None of these is the primary and the others fallbacks.** One action commonly carries two routes — in the shipped mission, action 0 has both a trigger area and a machine whose death activates it, and whichever happens first wins.

Both per-frame evaluators run from `Sim_MainTick` (`0045f464`), back to back and **after** the group pass, not before it:

```
per group: group+0x14 ? Group_DeploymentCheck : Group_OrderTick
ActionTimers_Tick (00426b48)       // every action timer
Actions_EvaluateTriggers(PlayerMech)
```

So a group waiting on an action arrives on the tick *after* it activates.

### Trigger areas — `Actions_EvaluateTriggers` (`00426b70`)

Walks the whole action array and, per action, picks whose position `Action_TestTrigger` (`004234b8`) is offered. The action's **type** (in-memory `+0x00`) selects the subject:

| type | subject |
|---|---|
| 0 | the player's mech |
| 1 | every member of the player's group |
| 2 / 3 | every member of every deployed group of side 0 (human) / side 1 (Cybrid) |
| 4 / 5 / 6 | every member of every deployed mech / flyer / base group |
| 7 / 8 / 9 | the action's own resolved target object (`+0x36`) |
| 10 | every member of the action's own resolved target group |

**The deployment gate is part of the test**: types 2-6 skip a group whose `+0x14` is still set, so an undeployed group cannot trip an action, including the one it is itself waiting on. Each sweep stops at the first subject that activates the action.

`Action_TestTrigger` returns "in the area" for an action that has already activated without testing anything, so the caller stops offering subjects. Otherwise it offers the position to each resolved block-4 area in turn (count `+0x04`, pointer array `+0x06`) and activates it on the first hit.

`DBSim_SpawnMissionObjects` (`004253d8`) resolves `+0x36` in a final pass over the array: types 7/8/9 resolve it as a mech/flyer/base roster slot, type 10 as a group, and types 0-6 have it zeroed.

#### The areas — block 4, resolved by `TriggerArea_Resolve` (`00423358`)

A block-4 record resolves to a 10-byte area ([`script-dat.md`](../formats/script-dat.md#the-13-block-structure), block 4). `TriggerArea_ContainsPoint` (`004233a4`) tests a position against it:

- **type 0** — axis-aligned XY box strictly between the two coordinates. Z is ignored, so a box catches anything standing over its footprint however high.
- **type != 0** — ground-plane distance from the coordinate is less than the stored radius.

A populated slot behind the action's first negative ref is never tested ([`script-dat.md`](../formats/script-dat.md#block-5-in-memory--58-bytes-0x3a)).

### Action timers — `ActionTimer_Tick` (`004230a4`)

The mission's timer, and the reason an action carrying no trigger area of its own is ordinary rather than dead. One block-6 record names a primary action, a delay and up to ten actions to activate:

```
if (timer.primary == null || timer.primary.activated) {
    if (Timer_CountDown(&timer.countdown) == 0) {
        for each of the ten sequence refs: if set, Action_Activate(it)
        re-arm the timer with 30000
    }
}
```

The file's delay is in seconds ([`script-dat.md`](../formats/script-dat.md#block-6-in-memory--49-bytes-0x31) has the shift). A timer with no primary runs from mission start; one with a primary runs from the moment that action activates. The re-arm goes through the same shift — 30000 seconds, about eight hours — and by then every action the timer names has activated, so the later expiry does nothing.

Chaining two of them staggers a sequence: `script6.dat` has action 1 arm a 92-second timer that activates action 2, which arms a 123-second timer that activates action 3.

### An object's own two actions — `+0x1b2` and `+0x1b6`

Every mech, flyer and structure carries two action pointers, resolved by `DBSim_SpawnMissionObjects` from its roster record's own refs ([`script-dat.md`](../formats/script-dat.md#the-two-pass-read--and-what-it-means-for-dbsim-keeps) has the offsets in blocks 7-9).

**`+0x1b2` — engaged.** Two routes. `Detection_Sweep` activates it when a hostile that already has contact on this object closes to 50000 units; both parties latch `+0x9e` and both activate their own. And a shot that reaches the shooter's own selected target raises the *shooter's* `+0x9e` (`0042671f`) and activates the *struck* object's action, from the tail of `Sim_RaycastObjectList` ([`hit-detection.md`](hit-detection.md#the-sweep--sim_raycastobjectlist-00426528)) — so shooting at what you have boxed engages it with nothing in detection range of it, the other way into mission-objective condition 6 ([`mission-objectives.md`](mission-objectives.md)).

Both routes are gated on the struck object's `obj+0xa2` being clear. That byte is a per-tick latch: `Mech_PerTickSystemsUpdate` raises it (`0041abd8`) on whatever the machine's targeting-computer pod holds a lock on, and `Sim_DetectionTick` clears it on everything at the end of the pass ([`target-selection.md`](target-selection.md)). `Action_Activate` is itself one-shot, so the gate can only suppress a duplicate inside one tick.

**`+0x1b6` — defeated.** Four sites, and they are the four ways an object stops being a threat. The first three run the object's [out-of-action report](#the-out-of-action-report) just before it; no call from the fourth has been found ([Open](#open)):

| site | when |
|---|---|
| `Mech_ComponentDamageWrite` (`00417de4`) | the machine dies (both of that function's death branches) |
| `Flyer_ComponentDamageWrite` (`00421bb4`) | component 0 goes |
| `Base_ApplyDamage` (`00404d70`) | the last component goes — [`structure-behaviour.md`](structure-behaviour.md#taking-damage--base_applydamage-00404d70) |
| `Ai_ChooseWeapon` (`0041f358`) | the machine runs out of working weapons — [`ai-weapons.md`](ai-weapons.md) |

**This is how a retail mission chains its reinforcement waves.** The shipped `script.dat` names one on five of its ten mech records; see the worked example below.

## The deployment gate — `group+0x14`

`DBSim_BuildGroupRecord` (`00423b34`) resolves the block-11 record's action ref (record `0x70`) into the group record's `+0x14` pointer. Non-null means "not deployed yet", and three places test it:

| site | effect when non-null |
|---|---|
| `maybe_Scene_SubmitFrameObjects` (`0042841c`) | the mech, flyer or base is **not submitted for drawing** |
| `Sim_MainTick` (`0045f464`) | the group runs `Group_DeploymentCheck` (`004236c4`) **instead of** `Group_OrderTick` (`00423a74`); a base's own `+0x18` tick is skipped outright |
| `Mech_CollisionTest` (`00418f74`) | the object is skipped before any distance is measured |

`Deployment_PickPointNearPlayer` (`0042354c`) and `Actions_EvaluateTriggers` apply the same test, so an arriving group never picks a landing point on top of one that has not arrived, and an undeployed group cannot trip a trigger.

**An undeployed group's placed position is therefore meaningless.** It is placed by the ordinary rules — usually on its route's first waypoint, which mission authors routinely share with the player's own squad — so several such groups commonly sit stacked on one point. Harmless in the original, because nothing above can see or touch them.

## Arrival — `Group_DeploymentCheck` (`004236c4`)

Runs every frame for every waiting group; does nothing until that group's action has activated. Once it has, the action's **verb** (in-memory `+0x02`) picks how the group turns up. Every arrival point is relative to the player and comes from `Deployment_PickPointNearPlayer`.

| verb | arrival | distance | bearing, relative to the player's heading |
|---|---|---|---|
| 2 | drop pod | 150,000 | `0x4000 - (rand & 0x7fff)` — ±90° |
| 3 | drop pod | 150,000 | `0x1000 - (rand & 0x1fff)` — ±22.5° |
| 4 | on foot | 90,000 | `-0x7000 - (rand & 0x1fff)` — behind, ±22.5° |
| 5 | on foot | 150,000 | `0x2000 - (rand & 0x3fff)` — ahead, ±45° |
| other | in place | — | — |

No retail `.MSN` authors verb 4 or 5: the verb runs 0-3 across the corpus ([`msn-mission-file.md`](../formats/msn-mission-file.md#row-10-field-decode--action82-dat_00470660-82-bytesrecord)). The two on-foot arrivals are read from the code alone.

**On foot** places the group's leader at that point facing `bearing - 0x8000` (the bearing itself, not the player's heading plus it), runs each other member through its own vtable `+0x78` formation offset, and clears `group+0x14` immediately. Only the leader is turned.

**In place** (any other verb, e.g. verb 1) just clears `group+0x14`, so the group goes live where it already stands — the one arrival for which the placed position is not a placeholder.

**Drop pod** spawns a `METEOR` and leaves `group+0x14` set; the pod clears it on landing. A byte at `group+0x13` latches the launch, so a group only ever gets one pod.

### Picking the point — `Deployment_PickPointNearPlayer` (`0042354c`)

Offsets from the player's position by the caller's distance at (player heading + the caller's bearing), then steps outward in 2,000-unit increments until the point clears three tests, in order:

1. **Deployed objects** — within their own `+0x7c` collision radius plus 5,000. **Applied only for the two walk-on verbs**: the caller's fourth argument is 1 there and 0 for a drop pod, so a pod is allowed to come down on top of a machine. That is what its landing-blast latch exists for.
2. **Structures** — `Structure_GatherWalkCandidates` (`00404ae4`) at radius 5,000, the same volume sweep a walking machine is stopped by ([`hit-detection.md`](hit-detection.md)).
3. **The ground** — `Terrain_FaceBlocksAt` (`0046fe84`) with a zero direction, which reduces it to the steepness of the face under the point; off the grid blocks ([`ai-navigation.md`](ai-navigation.md)).

The loop is unbounded in the original and cannot fail in practice, since the first point is usually clear.

## The drop pod — `METEOR`

Its own class, pool (`g_MeteorPool`, `004a972e`) and resources: `Meteor_LoadResources` (`00409a34`) loads `dts\meteor` and the `dba\impact` texture bank and binds the bank into every shape.

`Meteor_Construct` (`00409b44`) stores the group pointer at `+0x55` and puts the pod on a **slanted** approach rather than dropping it straight down. It draws a heading and a **horizontal run-in** of 70,000 plus up to 25,000 units, places itself that far short of the target along that heading, and flies in at a flat 2,000 units per tick:

```
n      = runIn / 2000                  // ticks of flight
height = (n * n >> 1) * 50             // 30,600 to 55,200 units up
vz     = -height / n
```

**The 70,000-95,000 figure is the run-in, not the altitude.** The launch height is derived from it — what a constant 50 units/tick² would need over that many ticks — and both axes are then flown at constant velocity, so the descent is a straight line. Reading the drawn figure as a height puts the pod at twice the altitude it belongs at.

`Meteor_Tick` (`00409d2c`), walked from `Sim_MainTick` over the pool *before* the group pass, has two phases:

1. **Falling** (`+0x4b == 0`). Integrates position by the velocity at `+0x45`, pitches the shape to `atan2(vz, 2000)` so it faces its fall line, plays sound `0x2f` once below absolute height 50,000, and on ground contact (`Terrain_HeightQuery`) sets the landed flag, snaps to ground height, plays sound `0x30` and detonates `Damage_ExplosiveBlastSweep(pos, 3000, 10000, 0, null)`. **If anything was in range it sets `+0x4c`** — the sweep answers on range alone, so a machine whose shields swallow the blast still trips the latch. It is the terrain query and not the flight-time count that ends the fall, so a pod aimed at ground well below the player keeps flying past its target.
2. **Landed.** Advances `+0x4d` at rate `0x5dc` per tick and drives the shape's frame counter from `+0x4d >> 10` — the pod opening. When that reaches the shape's frame count: if the pod carries a group and **`+0x4c` is clear**, it copies its own landed position onto the group's **leader** and clears `group+0x14`, which is the moment the group becomes real. A pod that landed on something delivers nothing, and the group it carried stays out of the mission for the rest of the run. Whenever the pod carries a group it then spawns a leftover effect from the theater's flat-shape pool at the site, releases its shape instance and returns 1, and `Sim_MainTick` frees it.

`Meteor_Render` (`00409cd0`) draws the plain shape (root 0) while falling and the opening animation's own shape instance at `+0x41` (root 1) once landed.

Only the leader is repositioned; the rest of the group follows under its orders. Every retail drop-pod group has exactly one member.

## The lift start

A mission start left over from Metaltech: Earthsiege, in some of whose missions the player's HERC starts underground and rides an elevator lift up to the surface: the cockpit brightens through a palette shift as it nears the top, and the ride ends in a camera shake. DBSIM keeps the whole sequence, but it draws `dba\intro`, which no retail VOL contains, and nothing found sets its gate ([Open](#open)).

`Sim_InitMissionSession` (`004614fc`) gates it on the `0xc3`-byte global block's `+0x54` (`004d2594`) and tests the flag twice:

- **`00461cc6`** — nonzero calls `LiftStart_DarkenPalette` (`0045d52c`). Zero instead sets `0049aef6` to 8, calls the empty `Palette_NoOp` (`0042eb40`), flushes the palette and renders one frame; the lift branch makes the first two of those itself and skips the others.
- **`00461e20`**, near the end of bring-up — nonzero calls `LiftStart_Rise` (`0045d840`).

### The darkening — `LiftStart_DarkenPalette` (`0045d52c`)

Copies the live palette into two new palette objects, rewrites entries 32-47 and 64-79 of the first as (G/2, R/2, B/2) — **red and green swapped as well as halved** — and installs those two spans live. It then starts a `0x78`-coarse-tick (1.92 s) cross-fade from the darkened object to the untouched copy ([`../formats/cockpit-canopy-palette.md`](../formats/cockpit-canopy-palette.md#palette-module)), and clears `0049aef4` ([Open](#open)). In ES2's palette the two spans overlap both ends of the cockpit scheme's window, slots 42-65 ([`../formats/cockpit-canopy-palette.md`](../formats/cockpit-canopy-palette.md#palette)).

### The ride — `LiftStart_Rise` (`0045d840`)

Loads `dba\intro`, the lift's art, and uses its first frame without checking the load. It then **raises** the player's machine from 4000 units below its placed height to that height, 35 units a frame, with sound `0x21` (`explo4.wav`) running. The loop only renders — `Sim_MainTick` does not run, so nothing else in the mission moves. Each frame it:

1. Puts the machine at the current height and rebuilds the view from it.
2. Projects the view-space point (0, 1000, −camera z) to a screen row. That point is at world height 0, 1000 units ahead, so the surface the lift rises to is the world's zero plane, not the terrain under the machine.
3. Renders the scene with the viewport's bottom cut to that row, or to its top while the row is at or above 0.
4. Draws the lift: `intro` from the row down, then colour id 19 from the art's bottom edge to the viewport's bottom — the whole viewport while the art is still above it.
5. Paints the cockpit overlay at `CockpitViewInstance+0x1f5`, steps the cross-fade while the row is below −200, and ends the frame with `Sim_EndFrame`.

**The brightening can stop part-way.** The fade runs on the clock from its first step, and only this loop steps it. A ride that reaches the top less than 1.92 s after the row passed −200 leaves the 32 entries where its last step put them, and even a completed fade never writes its final colours ([`../formats/cockpit-canopy-palette.md`](../formats/cockpit-canopy-palette.md#palette-module)).

At the top it stops `0x21`, plays `0x29` (`explo2.wav`) and shakes the view for `0x1e` coarse ticks (0.48 s): a band of 5 — a literal, half the damage shake's `5 << VideoMode_YCoordShift` — and one `(next & 0xffff) % 5` step a frame on the [presentation generator](random-generator.md#the-presentation-generator). **None of the shake reaches the screen.** The shake loop calls `maybe_Sim_RenderFrame` without `Sim_EndFrame`, the only per-frame present ([`../formats/cockpit-views.md`](../formats/cockpit-views.md#presentation)), so the last frame of the ride stays up while `0x29` plays. It then clears the shake, frees the two palette objects and their entry buffers, and sets `0049aef4` back to 1.

## The mission counters — `DAT_004a9ef4`

1,000 shorts, and the **campaign's** flag array for the length of a mission. It makes a round trip through `data\mission.var`: the shell writes the file from its own flag array before launch and reads it back at debrief ([`../shell/campaign-loop.md`](../shell/campaign-loop.md#the-files-crossing-between-the-two-binaries)). The simulator's two ends of it:

1. `DBSim_LoadScriptDat` (`00424308`) reads 2,000 bytes of it into `DAT_004a9ef4`, before it opens `player.mec`, and then zeroes slot 20, slot 10 and slots 21 to 42. Every other slot carries the campaign's value into the mission. Slots 21 to 49 are the weapon units the debrief grants ([`../formats/weapons-dat.md`](../formats/weapons-dat.md#campaign-grants--armory_grantcampaignweapons-004126be)), so the zeroing starts a mission with none owed but the last seven ([Open](#open)).
2. `Mission_WriteResults` (`0042412c`) writes, as the mission ends, `results.dat` and then the same 2,000 bytes back to `mission.var` ([`mission-objectives.md`](mission-objectives.md#what-the-mission-leaves-the-shell--mission_writeresults-0042412c)).

Four things write the counters during a mission: `Action_Activate`, an objective ([`mission-objectives.md`](mission-objectives.md)), an object or a whole group going out of the fight ([below](#the-out-of-action-report)), and `Mech_CreditNeutralisedTarget` (`00415710`), which adds one to slot 10 when the player puts a machine of its own group out of the fight ([`component-damage.md`](component-damage.md#what-the-attacker-is-told--mech_creditneutralisedtarget-00415710)).

The simulator reads slot 20 itself: `Mission_WriteResults` adds 25,000 kg of salvage per unit of slot 20 to the award it writes to `results.dat`.

### The out-of-action report

`Mech_ReportOutOfAction` (`00411bc8`) writes the counters an object is set to write when it goes out of the fight. `es2_xref.py` finds four calls to it, each immediately before the object's defeat action ([`+0x1b6`](#an-objects-own-two-actions--0x1b2-and-0x1b6)): both of `Mech_ComponentDamageWrite`'s branches ([disabled and dead](component-damage.md#going-out-of-the-fight)), `Flyer_ComponentDamageWrite` when component 0 goes, and `Base_ApplyDamage` when a structure's last component goes. The fourth defeat-action site, a machine running out of working weapons, is not among them ([Open](#open)).

It does two things:

1. **The group's report.** `Group_ReportIfAllOutOfAction` (`00423f30`) walks the object's group, skipping the object itself, and returns at the first member that is neither destroyed (`+0x99`) nor immobilised (`+0xa4`). If none is left standing it runs the group's own ten slots at `group+0x1c`/`+0x30`. The reporting object is skipped rather than tested because the leg branch reports before it latches `+0xa4`. Nothing latches the group's writes; they run once because each member reports once and only the last one standing finds the rest down.
2. **The object's own.** Ten slots at `obj+0x1ba` (counter refs) and `obj+0x1ce` (operations).

Each slot with a non-negative ref writes that counter by its operation. These are neither the action layer's codes nor the objective layer's ([`mission-objectives.md`](mission-objectives.md#the-record)):

| op | write |
|---|---|
| 1 | zero it |
| 2 | add one |
| `0x0d`-`0x10` | store op − `0x0c`, 1 to 4 |
| anything else | nothing |

The slots come from each object's `script.dat` record ([`../formats/script-dat.md`](../formats/script-dat.md#the-two-pass-read--and-what-it-means-for-dbsim-keeps)), copied in by `SimObject_SetOutOfActionCounters` (`00411b90`), and the group's from its block-11 record, record 0 included. `es2_xref.py` finds three calls to the copy, all in `DBSim_SpawnMissionObjects` and all on the roster side of its `index < rosterCount` test, so a machine of the player's squad does not get its slots from a record. Its slots start at zero instead: `DBSim_LoadScriptDat` builds the mech pool (`004a9bfe`) afresh for each mission with `Pool_Init` (`004719cc`, at `00425196`), which zeroes the pool's storage, and `Mech_Constructor` then sets refs 0-2 to −1 (`00415c3a`-`00415c51`). Every operation reads 0, so a squad machine's report writes nothing of its own ([Open](#open)).

Across the 62 `.MSN` missions, 32 use the mech slots, 31 the structure slots, 22 the group slots and one the flyer slots. Two uses account for all but six of the 1,258 filled slots:

- **Op `0x0d` on a machine**, 420 slots. 412 of them set a flag in 22-41, the weapon grants [`../formats/weapons-dat.md`](../formats/weapons-dat.md#campaign-grants--armory_grantcampaignweapons-004126be) reads at debrief: putting the HERC out of the fight earns one unit of that weapon. The weapon is one the machine carries in 400 of the 412; the other twelve name a weapon outside its fit, eight of them `L300` on a machine carrying weapon 8. The write is a store, so several machines setting one flag still grant one unit. The last eight slots set counter 20.
- **Op 2 on a structure**, 756 slots: counter 20, the 25,000 kg salvage bonus above, in 64 of them, and in the other 692 a counter of the structure's own from 100 up, which goes back to the campaign's flag array with the others.

The six others carry op 6 or `0x17`, which write nothing here.

## The shipped mission, end to end

A worked example, because it is the only place the four mechanisms are visible together. The live `script.dat` fields 3 actions, 1 trigger area, 0 action timers, and 8 Cybrid HERCs in six groups:

| stage | what activates it | who arrives |
|---|---|---|
| start | — | group 2, one ACHILLES, north of the player; group 8, two flyers |
| wave 1 | group 2's machine dies (`+0x1b6` → action 0), **or** it walks into action 0's circle | groups 3 (two ACHILLES) and 4 (one), **in place** |
| wave 2 | either of group 3's machines dies (`+0x1b6` → action 1) | group 5, a HEADHUNTER and a HYPERION, in place |
| wave 3 | either of group 5's machines dies (`+0x1b6` → action 2) | groups 6 and 7, one ACHILLES each, **by drop pod** |

Action 0's circle is centred at (1005988, 1058404) with radius 150,000 and its subject is type 3 — deployed **Cybrid** groups, not the player. The player spawns inside it at 64,132; group 2's ACHILLES spawns north at 252,252 and walks south, crossing in at tick 1222 (~49 s at 25 Hz). So the first wave arrives on its own if the player does nothing, and sooner if the player kills the machine. Actions 1 and 2 carry no area, so their only route is the kill.

## Rejected readings

| reading | why it is wrong |
|---|---|
| An action with no trigger area is unreachable | Three other things activate it; a mission's later actions routinely carry no area at all |
| `Meteor_Construct`'s 70,000-95,000 is the spawn altitude | It is the horizontal run-in. The altitude is derived from it and is 30,600-55,200 |
| `Deployment_PickPointNearPlayer` avoids deployed objects | Only for the walk-on verbs; a drop pod's point is picked without that test |
| `Actions_EvaluateTriggers` runs before the group pass | `Sim_MainTick` runs it after, so a group arrives a tick after its trigger |
| `obj+0x1b6` is a death action | It is also activated when a machine runs out of weapons |
| An action's message is a `data\mission.str` line | That file holds the objective text, and the id looks like a ref into it. The port it is posted to resolves a speakerless id in `COMMAND<n>.STR` |

## Open

- **Open:** what sets block `+0x54`, the lift start's gate. No absolute reference to `004d2594` exists, the block's static initialiser `Main_StaticInit` (`0045cad8`) does not store it, and of the `+0x54` writes `es2_fieldscan.py` finds, none is through a register holding the block.
- **Open:** what reads `0049aef4`, the byte the lift start clears for its duration (1 in the image). `es2_xref.py` finds only the lift's two stores.
- **Unported:** the pod's leftover ground mark, from the theater's `flat`/`flat2` shape pool.
- **Open:** why the load's zeroing stops at slot 42, leaving the grants in 43 to 49 owed from before the mission.
- **Open:** whether anything writes a player-squad machine's operation slots (`+0x1ce`-`+0x1e1`) after construction. `es2_fieldscan.py` finds no writer but `SimObject_SetOutOfActionCounters`, and even that one only at `+0x1ce`: the scan misses writes through a stepping pointer, which is how that function fills the other nine.
- **Open:** whether the weapons-out defeat in `Ai_ChooseWeapon` (`0041f358`) runs an out-of-action report. `es2_xref.py` finds no branch, stored pointer or vtable slot reaching `Mech_ReportOutOfAction` from there; that sweep would miss a call through a pointer built at run time.