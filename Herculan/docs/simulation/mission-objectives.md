# Mission objectives (DBSIM.EXE)

What the mission wants done, what loses it, and the one number the whole layer produces. The records
are `script.dat` block 12 and their text is block 13 plus `data\mission.str`; see
[`../formats/script-dat.md`](../formats/script-dat.md#block-12-in-memory--76-bytes-0x4c) for the
layout and [`../formats/msn-mission-file.md`](../formats/msn-mission-file.md#row-17-field-decode--the-objective-record-dat_0047064a-58-bytesrecord)
for where an author writes them.

This is a separate mechanism from the mission **actions** in
[`mission-deployment.md`](mission-deployment.md). An action is a latch that makes something happen;
an objective is a question that is asked over and over and never makes anything happen. They share
only the mission-counter array.

## The record

An objective is **one condition asked of one subject**, plus what satisfying it does to the mission
counters. `Mission_EvaluateObjectives` (`00413280`) walks the array each poll.

| field | meaning |
|---|---|
| `+0x00` | **required.** `== 1` means the objective must be satisfied; **anything else means the opposite** — the record is a failure condition and the mission is lost the moment it comes true |
| `+0x02` | which condition is asked |
| `+0x04` | subject kind: 0 group, 1 mech, 2 flyer, 3 base |
| `+0x06` | the resolved subject |
| `+0x0a` | a block-1 point. No condition reads it, and it is `-1` in all 62 retail missions |
| `+0x0e` | a block-3 waypoint group — which of the subject group's ten orders condition 0 is about |
| `+0x12` | the resolved failure text, four `char*` |
| `+0x24`/`+0x38` | ten mission-counter refs and ten operations |
| `+0x22` | runtime: the counters have been applied |

**The counter operation codes are not the action layer's.** Here 4 sets, 5 clears, 6 increments and
7 decrements; `Action_Activate` knows only 5 and 6 and reads them as clear and increment, so the two
layers disagree on 6 with nothing to warn a reader. A slot is skipped unless **both** its ref and its
operation are non-negative. The array is [`mission-deployment.md`](mission-deployment.md#the-mission-counters--dat_004a9ef4)'s.

The counters are applied the first time the condition holds and never again; the latch is not "the
objective is met", which is re-read every poll.

## What each condition asks

The original writes the eleven cases out twice, once for a group subject and once for an object one.
They are the same eleven questions.

| code | of a group | of an object |
|---|---|---|
| 0 | the order that runs the record's waypoint group is flagged complete (`Mission_GroupOrderCompleteOnRoute`, `0041324c`) | the same, asked of the object's own group |
| 1 | written off — `Group_ConditionTier` at or past the side's threshold | destroyed or immobilised (`+0x99 \|\| +0xa4`) |
| 2 | not written off, and every living member is clear of threats | clear of threats |
| 3, 4 | the player has completed a data link (`player+0xa0`) — the subject is not looked at | as for a group |
| 6 | any member has been engaged (`+0x9e`) | engaged |
| 7 | **every** member is disarmed (`+0xa5`) | disarmed |
| 8 | condition 6 negated | condition 6 negated |
| 9, 10 | the player has *not* completed a data link | as for a group |

**Code 5 has no case in either switch**, and neither does a subject kind above 3. The original leaves
its working register untouched, so such a record silently answers whatever the record before it
answered. No retail mission reaches either: across the 127 objective records in the 62 `.MSN` files
the codes used are 1 (67), 2 (27), 0 (19), 3 (5), 6 (5), 4 (3) and 7 (1), and the subject is a group
92 times, a mech 22 and a structure 13 — never a flyer. 91 records are mandatory and 36 are failure
conditions. **Codes 8, 9 and 10 are unused as well**, so the two negations and one of the two
data-link readings are exercised by nothing that ships.

**A group's write-off threshold is not the same for both sides** (`FUN_00413920`): a human group is
written off at condition tier 3, a Cybrid one only at 4. So "wipe out this Cybrid group" means every
machine, and "this convoy did not make it" is answered a tier earlier. The tiers are
[`ai-goals.md`](ai-goals.md)'s.

### Clear of threats — `Mission_IsClearOfThreats` (`004137b4`)

The escort objective's whole test, and the gate on every conclusive mission status. It walks the live
object list, skipping anything undeployed, on the subject's own side, or already out of the fight,
and the subject is **not** clear when a survivor either:

- knows about it (`Ai_KnowsObject`, [`ai-targeting.md`](ai-targeting.md)) and is within 80000 units —
  100000 if the subject is that machine's own selected target; or
- **for a subject not on the player's group**, belongs to a group whose current order names the
  subject, and is either still on its way (its route has somewhere left to go) or already knows where
  the subject is. An assigned hunter counts at any range, which is what stops an escort being called
  safe while something walks towards it.

## The status — `Mission_Status` (`004135e8`)

The judgement, in the original's order: the player's own condition first, then the mission box, then
the objectives.

```
if (player destroyed)            2
else if (player immobilised)     3
else if (outside box + 110000)   8
else if (outside box)            7
else                             EvaluateObjectives()
```

The box is block 1's own extent, accumulated as the coordinates are read (`FUN_0041373c`); the
Heads-Down Display's map is framed by the same one
([`../formats/heads-down-display.md`](../formats/heads-down-display.md)).

`EvaluateObjectives` reduces the array to three outcomes and then splits each by whether the player
is clear:

| | player clear | player still in contact |
|---|---|---|
| a failure condition holds | **6** mission failed | 10 |
| every required record holds | **9** mission successful | 10 |
| neither | 5 | 4 |

**The mission does not conclude while the player is still in a fight.** All three outcomes collapse to
10 there, and 4, 5 and 10 announce nothing. `FUN_0042412c` writes `(status == 9)` into `results.dat`
as the mission ends, so 9 is the only success.

The first required record that is *not* satisfied is published at `DAT_004d1f1c` as the failure text
the alert panel prints — four `char*`, three from the file and an empty fourth.

## What the computer says

Four statuses carry a `SYSTEM.STR` line, posted on the **change** rather than each poll:

| status | line |
|---|---|
| 6 | `0x16` MISSION FAILED |
| 7 | `0x1e` APPROACHING MISSION ZONE BOUNDARY |
| 8 | `0x20` RULES OF ENGAGEMENT VIOLATED. MISSION ABORTED. |
| 9 | `0x17` MISSION SUCCESSFUL |

After a post the answer is held still for 500 ms so the caller's next poll cannot queue the line
twice; the running baseline catches up on the first evaluation after that, which is what sequences
the spoken line ahead of the alert panel. `MISSION OBJECTIVES COMPLETE`, `PRIMARY OBJECTIVE COMPLETE`
and `SECONDARY OBJECTIVE COMPLETE` are recorded but posted by nothing — see
[`../formats/audio.md`](../formats/audio.md#posters).

## The poll — `Mission_PollStatus` (`004131ac`)

`Sim_MainTick`'s last act, run with the player's machine and only while it is not destroyed. Two
countdowns shape it, and between them they are why a finished mission takes tens of seconds to say
so:

- **The poll interval** (`DAT_004a9ee6`) is re-armed to 10 s every time the answer is not worth
  raising, so the objectives are read about once every ten seconds. A player **outside the mission
  box** skips the interval and is read every tick, which is what makes the boundary warning prompt.
- **The alert delay** (`DAT_004a9ee9`) is armed once, the first time an alert-worthy status appears,
  and the status is not handed up until its 10 s runs out. A destroyed player skips it.

A status is worth raising when `DAT_0049935c[status]` is set — 2, 3, 6, 7, 8 and 9. The caller builds
the modal alert panel `FUN_00455934` for it, which for status 5 prints `DAT_004d1f1c` as its body.
The in-mission objectives panel is a second one, `FUN_0045751c` (`obj_alrt`), which lists block 13.

## The player think's objective arms

`Mech_BehaviourPlayerThink` (`0041c194`) carries the waypoint arm
([`player-waypoints.md`](player-waypoints.md#the-players-think--mech_behaviourplayerthink-0041c194))
and then exactly one objective arm, chosen by the mission's own selector — `script.dat`'s header at
`+0x06`. **They are the only writers of `+0x9f` and `+0xa0` in the image**, and those two flags are
what conditions 3, 4, 9 and 10 read back.

| selector | arm |
|---|---|
| 0 | **the mission target is picked up.** One-shot for the whole run: the first time the player's own selected target (`mech+0x1a4`) is what their group's current order names, post `0x19` MISSION TARGET DETECTED and raise `+0x9f`. It fires on the pilot selecting the thing, not on the sensors finding it |
| 5 | **the goal is reached.** Within 40000 ground units of `Mech_AiGoalPosition` raises `+0x9f`. Says nothing, and four times the waypoint arm's range |
| 3, 7 | **the data link**, below |
| other | nothing |

All ten retail `script.dat` files carry selector 0; the other arms are reached from the campaign's own
missions.

Selector **3** also reaches into the AI: `Ai_IsTargetable` refuses the current order's target to a
group led by the player's machine, so the squad does not shoot the thing the player came to read.
Selector 7 does not get that shield. See [`ai-targeting.md`](ai-targeting.md#is-it-a-target-at-all--ai_istargetable-00411e80).

### The data link

The player parks in front of what their order names and holds position. Holding needs four things at
once: the subject alive, its group in the mission, the player within 10000 units in three dimensions,
and the player's **aim** — body heading plus turret twist — within 45° of it. Nothing tests speed, so
the link can be held while walking past. Breaking off posts `0x38` DATA TRANSFER ABORTED and puts the
sequence back to the start.

Four lines are spoken, `0x34` to `0x37`, each after the delay the table at `0049a318` gives for the
step before it: 5000, 5000, `0xffff9c40`, 0. **The link therefore takes ten seconds of holding
station**; the third entry is negative, `Timer_CountDown` clamps at zero, and `DATA TRANSFER
COMPLETE` is queued the tick after `TRANSFERRING DATA`.

That is not what the player sees. The two are queued a tick apart but shown ten seconds apart,
because `TRANSFERRING DATA` is the one `SYSTEM.STR` entry whose display timings are 10 s and 20 s
rather than 3 s and 6 s and the port will not let a message yield before its minimum
([`../formats/audio.md`](../formats/audio.md#the-port)). So the transfer reads on screen as a long
operation while the simulation has already finished it: `+0xa0` goes up when the last line is
*queued*.

## Engine port

`World.MissionObjective` is the record and `Sim.MissionObjectiveState` its runtime half;
`Sim.MissionObjectives` is all three of the original's functions, and `Sim.MissionStatus` names the
nine values. `MissionScene` resolves each subject the way it resolves an order's;
`SimWorld.MissionBounds` is the box and `SimWorld` polls at the end of its tick.
`MechObject.PlayerThink` holds the three arms, `SimObject.MissionGoalReached` and
`SimObject.DataLinkComplete` are `+0x9f` and `+0xa0`, and `MissionGroup.OrderCompletedForRoute` is
condition 0.

Divergences:

- **`DAT_004a9d7c`, the MISSION TARGET DETECTED latch, has no reset** — two references, both in the
  player think, in `.bss`. The port holds it per machine, so it resets with the mission rather than
  with the process.
- **Neither alert panel is drawn.** The status the poll hands up is latched on
  `SimWorld.PendingMissionAlert` and nothing consumes it; block 13 and the mission text are on
  `Mission.BriefingLines` and `Mission.Text` and nothing lists them.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| Every objective the player is shown is an objective the simulation tests | Block 13 and block 12 are separate data and nothing reconciles them. An author writes the lines the player reads and the conditions the code tests independently |
| Reaching the last waypoint completes a "travel there" objective | Condition 0 reads the group's order-completed flag at `+0x70`, which `Group_OrderTick` sets — so the objective is about the **order** finishing, not about arrival |
| Meeting every objective ends the mission | It yields status 9 only while `Mission_IsClearOfThreats` also holds for the player. With a live hostile aware and near, the status is 10 and nothing is announced |
| `+0x00` is a priority, with higher meaning more important | The evaluator's only test is `== 1`. One is mandatory; every other value makes the record a failure condition, which is the opposite meaning rather than a weaker one |
| The data link finishes instantly because its third delay clamps to zero | The delay is written before the line it precedes, so the link still needs ten seconds of holding; the clamp only removes a wait after the last line is decided |
