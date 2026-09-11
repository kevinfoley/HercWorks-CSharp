# AI squadmates — the standing squad order

What the player's own three machines do when told something. A standing squad order sits **above** the mission group's order array: a nonzero `mech+0x23e` takes its own path through `Mech_AiSelectBehaviour` and the group's own verb is never read at all, so this is the only mechanism in the simulation that steers a machine off its group's route. Everything else about a player squadmate — formation, acquisition, combat — is the same machinery every AI machine runs, and belongs to [`ai-navigation.md`](ai-navigation.md), [`ai-targeting.md`](ai-targeting.md) and [`ai-combat-states.md`](ai-combat-states.md).

The screens that issue orders are [`heads-down-display.md`](../formats/heads-down-display.md) and [`mfd.md`](../formats/mfd.md); the order text is `STRINGS0.STR` group 0, [`str-strings.md`](../formats/str-strings.md).

## The two ways an order leaves the cockpit

Both end at the same per-machine handler. What differs is who hears it.

| Screen | Dispatcher | Reaches |
|---|---|---|
| [F7] command display, XMIT | `Squad_SendOrderToSlot` (`00431610`) | one comm-box slot out of `DAT_004d044c` |
| MFD FLASH COMM, XMIT | `Squad_BroadcastOrder` (`004231a4`), through `Squad_BroadcastOrderFromCockpit` (`0043166c`) | every member of the player's group (`DAT_0049b0f8`) |

`Squad_SendOrderToSlot` skips an empty slot, the player's own machine and a destroyed pilot, and withdraws message `0x22` from the pilot-and-squad port either way.

`Squad_BroadcastOrder` sends to the whole group, best-suited machine first. Each pass scores every member not yet told, keeps the highest score, breaks ties on range to the issuer, and sends to that one — then carries the answer into the next send, so **only the first recipient, or the first to accept, says anything on the radio**. Verbs 0 and 2, the two that name a single target, stop at the first acceptance; the rest go round until everyone has been told.

The score is a switch over six verbs:

| Verb | Prefers |
|---|---|
| 0, 2, 5 | a machine whose descriptor `+0x09` (committed) is **clear** — someone free to take a fight |
| 1, 8, 0xf | one where it is **set** — someone in a fight to call off |
| 4 | a machine whose radar is off |
| 7 | one whose radar is on |

## The order record — 22 bytes

One instance exists: the global at `DAT_004d0458`, which both screens write in place before transmitting. Packed and unaligned.

| Offset | Type | Meaning |
|---|---|---|
| `+0x00` | short | **The verb**, as a `STRINGS0` group 0 index |
| `+0x02` | ptr | **The issuer.** Stamped by the dispatcher with the player's machine, not by the screen that built the rest. `Squad_BroadcastOrder` ranks recipients by range to it and skips it |
| `+0x06` | int[3] | The gridpoint, for the four verbs that take one |
| `+0x12` | ptr | The subject object, for the three verbs that take one |

`HddCommandScreen_FillOrderRecord` (`0044db24`) is what settles the layout: it writes the point triple or the subject pointer according to the pick kind that `HddCommandScreen_CommitPick` (`0044db74`) latched a moment earlier, and XMIT calls the pair in that order.

## The verbs

All eighteen of `STRINGS0` group 0. Entries 0-8 are the MFD's FLASH COMM page, 10-17 the [F7] command display's own list; the two overlap in meaning but not in code, and `Mech_ReceiveSquadOrder` gives each pair its own case only where they differ.

| # | Text | Case | Effect |
|---|---|---|---|
| 0 | `ATTACK MY TARGET` | own | Engage the player's current selection (`CockpitViewInstance+0x210`) |
| 1 | `IGNORE MY TARGET` | own | Set the `+0x9a` latch and find something else |
| 2 | `HELP ME OUT!` | own | Engage whatever is nearest to shooting at the player |
| 3 | `JOIN ON ME` | with 10, 15 | Clear the order and reinstall `patrolling` |
| 4 | `SCAN FOR HOSTILES` | with 16 | Radar ACTIVE |
| 5 | `FIRE AT WILL` | own | Set the `+0xb6` latch and start a fight |
| 6 | *(no text)* | — | No case |
| 7 | `EMCON` | with 17 | Radar PASSIVE |
| 8 | `HOLD YOUR FIRE` | own | Clear `+0xb6` |
| 9 | *(no text)* | — | No case |
| 10 | `DISENGAGE` | with 3, 15 | |
| 11 | `ATTACK ENEMY` | own | Engage the record's subject |
| 12 | `DEFEND POSITION` | own | Guard the record's point or subject |
| 13 | `PATROL GRIDPOINT` | own | |
| 14 | `GOTO GRIDPOINT` | own | |
| 15 | `JOIN ON ME` | with 3, 10 | |
| 16 | `SCAN FOR HOSTILES` | with 4 | |
| 17 | `EMCON` | with 7 | |

The FLASH COMM page shows six rows, and each is two orders deep. `MfdFlashComm_SelectedVerb` (`0043f998`) resolves a row to the row index at `screen+0x32`, **plus 3** when that row's own state byte at `screen+0x2c + row` has bit 1 set — so the six positions cover verbs 0-5 or 3-8.

## Receiving one — `Mech_ReceiveSquadOrder` (`00420ad4`, mech vtable `+0x28`)

`short __cdecl(mech, order, priorReply)`. Returns 1 when the order was taken. Thirteen cases; each picks a reply id, and posts it through `Ai_PostSquadMessage` only when `priorReply` is 0, or is 1 with an order this machine accepted.

| Verb | Refused when | Otherwise |
|---|---|---|
| 0 | out of action; descriptor `+0x0c` set (already broken off); the player has nothing selected, or it is neutralised; already engaging it | `+0x23e` = 4, `+0x248` = the selection, `+0x250` = its tier, engage |
| 1 | out of action; the player has nothing selected; `+0x9a` already set | Set `+0x9a`. A machine committed **to that same target** either zeroes its dwell, if its descriptor `+0x0a` (holds place) is set, or retargets |
| 2 | out of action; nothing within 60000 of the player is shooting at it; already committed to it | Same install as verb 0, against the nearest such machine |
| 3, 10, 15 | immobilised | `Behaviour_SetState(patrolling)`, `+0x23e` = 0. The reply splits on being inside 25000 of the leader |
| 4, 16 | — | `+0x96` = 1 unless already set, `+0xb2` = 1 |
| 5 | out of action | `+0xb6` = 1. A committed machine has nothing to start; otherwise it acquires, and a machine guarding a subject it is still within 100000 of re-enters `Mech_AiSelectBehaviour` rather than leaving the post |
| 7, 17 | — | `+0x96` = 0, `+0xb2` = 0 |
| 8 | — | `+0xb6` = 0. A committed machine re-enters `Mech_AiSelectBehaviour`, **then** `+0x23e` = 0 |
| 0xb | out of action; already engaging the subject; the subject is neutralised | Same install as verb 0, against the record's subject |
| 0xc | out of action; already guarding the same thing | `Behaviour_SetState(guarding)`, `+0x23e` = 6 |
| 0xd | immobilised; `+0x23e` already 2 and the point within 2000 | `Behaviour_SetState(patrolling)`, `+0x23e` = 2 |
| 0xe | immobilised; `+0x23e` already 1 and the point within 2000 | `Behaviour_SetState(patrolling)`, `+0x23e` = 1 |

Verbs 0xc, 0xd and 0xe write `+0x240` — and 0xc also `+0x24c` — **whether or not the order was taken**, so a refused re-order still moves the post. Verb 0xc's "same thing" is the same subject object, or — with neither the old order nor the new one naming a subject — the stored point within 1000 of the new one, on the ground plane.

Verbs 0, 2, 3, 8 and 0xb clear the damage accumulator at `+0x281` ([`ai-targeting.md`](ai-targeting.md)).

The three that engage all go through `Mech_AiEngageOrderedTarget` (`0041c0f4`), which picks the state — [`ai-targeting.md`](ai-targeting.md#the-writers-of-mech0x1a4).

## The standing order

Five verbs are ever written to `mech+0x23e`: 1 move, 2 patrol, 4 engage, 6 guard, and 0 to clear. Where each is read:

| Reader | Uses |
|---|---|
| `Mech_AiSelectBehaviour` (`0041eb34`) | 1 and 2 install `patrolling`, 6 `guarding`, 4 engages `+0x248` — or, with that target destroyed, clears the verb and re-enters. [`ai-dispatch.md`](ai-dispatch.md#choosing-a-state--mech_aiselectbehaviour-0041eb34) |
| `Ai_NavigationStep` (`0041d598`) | Any nonzero verb drives at `+0x240` instead of the group's route, and is the only way to reach the Turbo Pod sprint. [`ai-navigation.md`](ai-navigation.md) |
| `Mech_AiGoalPosition` (`0041dbcc`) | 6 works to `+0x24c`, or to `+0x240` when that is null |
| `Mech_AiCombatReassess` (`0041cf18`), `Mech_AiOnTakingFire` (`0041f7b8`) | 4 names the target to keep. [`ai-targeting.md`](ai-targeting.md) |
| `Mech_BehaviourPatrolThink` (`0041d7d0`), `Mech_BehaviourGuardThink` (`0041e224`) | A nonzero verb opens the leader gate; 2 and 4 change what is acquired. [`ai-navigation.md`](ai-navigation.md) |
| `Ai_ShouldAbandonTarget` (`0041c4a8`), `Ai_ClearSquadEngageOrder` (`0041c478`) | 4 is satisfied only past `+0x250`, and clears when its target is let go. [`ai-combat-states.md`](ai-combat-states.md) |
| `Mech_SquadOrderLineIndex` (`0041bac8`) | 1, 2, 3 and 6 override the `OBJECTIVE:` line. [`heads-down-display.md`](../formats/heads-down-display.md) |

## The three latches

None of them is a squad order; all three are per-machine flags this handler is the only writer of.

| Offset | Set by | Cleared by | Read by |
|---|---|---|---|
| `+0x9a` | verb 1 | verbs 0, 2, 5; and `Mech_AiOnTakingFire` when the shooter *is* the player's selection | `Ai_IsTargetable` (`00411e80`), which refuses this machine the player's current selection while it is set |
| `+0xb6` | verb 5 | verb 8 | The leader gate in `Mech_BehaviourPatrolThink`, `Mech_BehaviourSearchDestroyThink` and `Mech_BehaviourGuardThink` — [`ai-navigation.md`](ai-navigation.md) |
| `+0xb2` | verbs 4, 16 | verbs 7, 17 | `Ai_UpdateWeaponsFree` and the combat reassess's radar step — [`ai-weapons.md`](ai-weapons.md) |

## Mech fields this slice owns

| Offset | Type | Meaning |
|---|---|---|
| `+0x23e` | short | The standing order's verb, 0 for none |
| `+0x240` | int[2] | Where the order sends the machine. Two ints, not three — the extent is derived; see `known_structs.json` |
| `+0x248` | ptr | Verb 4's target |
| `+0x24c` | ptr | Verb 6's subject, or null for bare ground |
| `+0x250` | short | Verb 4's abandon threshold — [`ai-targeting.md`](ai-targeting.md) |
| `+0x9a`, `+0xb2`, `+0xb6` | byte | Above |

## The replies

`Ai_PostSquadMessage` (`00420a98`) ids raised here: `0x0b` `0x0c` `0x0d` `0x0f` `0x11` `0x12` `0x14` `0x16` `0x17` `0x1a` `0x1b` `0x1c` `0x1d` `0x1e` `0x20` `0x26` `0x28` `0x2a`. They index the speaker's own `PILOT<n>.STR` set — [`audio.md`](../formats/audio.md#the-pilot-and-squad-channel). Which situation raises which is in the case table above and in the engine's own constants.

## Engine port

`Sim.Ai.SquadOrder.cs` holds the verb enum, the record and both dispatchers; `MechObject.Squad.cs` holds the standing-order fields, the handler, `Ai_ClearSquadEngageOrder` and `Mech_SquadOrderLineIndex`. `BehaviourState` carries descriptor `+0x3c` as `ObjectiveLine` and bit 4 as `BrokenOff`.

**What runs.** Both dispatchers deliver a real order: all eight verbs install, survive their own reassess, and drive the machine. The [F7] command display's XMIT sends to one slot and the MFD's FLASH COMM page broadcasts to the group ([`mfd.md`](../formats/mfd.md#mfdflashcomm--mode-1)), and a squadmate's reply is spoken and drawn on the pilot channel ([`audio.md`](../formats/audio.md#the-pilot-and-squad-channel)). `Herculan.Engine.Host` takes `--hdd-xmit` and `--flash-comm-xmit`, which press XMIT on the order each screen armed and report each squadmate's standing order before and after the run — a `--screenshot` run sees no keystroke and no map click, so they are the only way to reach this from the command line.

What differs from the original:

- **`Squad_BroadcastOrder`'s score is zero for the twelve verbs its switch does not cover.** The original's is a stack local assigned only inside that switch, so an uncovered verb leaves every member carrying the previous one's score. Both tie the group and walk it in range order; zero does it without reading uninitialised memory.

## Open questions

- **Verbs 3 and 5 as values of `+0x23e`.** Nothing writes either, and two readers handle them: `Mech_AiGoalPosition` works to `+0x24c` for 3 and to `+0x248` for 5, and `Mech_SquadOrderLineIndex` prints `GUARD` for 3. `Mech_AiSelectBehaviour` installs nothing for either, so a machine carrying one would keep the state it had and steer at `+0x240`.
- **Group 0 entries 6 and 9** have no text and no case, and sit exactly where the FLASH COMM page's `+3` shift lands rows 3 and 6.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| The FLASH COMM page lists six of the eighteen orders | It lists six *positions*. Each names one of two verbs on its own state bit, so the page covers 0-8 |
| `mech+0x9a` stops a squadmate shooting at the player's target | It removes that one object from the *acquisition* candidate set. A machine already holding it keeps it; what drops it is verb 1's own retarget branch, which only runs for a machine committed to that exact target |
| `Squad_BroadcastOrder` ranks the group by fitness for the order | For six verbs. For the other twelve every member scores alike and the ranking is range alone |
| The order record's point is the only thing `DEFEND POSITION` stores | It stores both: `+0x240` takes the point and `+0x24c` the subject, and `Mech_AiGoalPosition` prefers the subject, so a guard ordered onto a machine follows that machine |
