# AI goals — the mission-group order layer

What tells a machine *what it is for*. Every AI machine belongs to a mission group, and the group carries an ordered list of orders it works through; the order in force is what [`ai-dispatch.md`](ai-dispatch.md#choosing-a-state--mech_aiselectbehaviour-0041eb34) turns into a behaviour state, and it is also what the target filter, the combat reassess and the abandonment test read when they ask what this machine was sent to do — see [`ai-targeting.md`](ai-targeting.md).

This layer is structurally **upstream of the rest of the AI**: `Mech_AiTick`'s only caller is `Group_OrderTick`, so a machine that is not a live member of a group never thinks at all.

The file half of the subject — how block 10 and block 11 are written and read — belongs to [`script-dat.md`](../formats/script-dat.md).

## The order record — 22 bytes

`DBSim_SpawnMissionObjects` (`004253d8`) allocates `orderCount * 0x16` and fills one record per block-10 entry, resolving each of that entry's four refs to a pointer as it goes. The seven source fields land in a different order than they are read in.

| Offset | Type | Built from | Meaning |
|---|---|---|---|
| `+0x00` | short | block 10 `0x08` | **The verb.** 0–6; see below |
| `+0x02` | short | block 10 `0x0a` | Copied verbatim. No reader |
| `+0x04` | ptr | block 10 `0x0c` → block 1 | A point. No reader |
| `+0x08` | ptr | block 10 `0x0e` → block 3 | **The route** — a waypoint group. Read once, by `DBSim_BuildGroupRecord`, and only from slot 0 |
| `+0x0c` | short | block 10 `0x10` | **What kind of thing the order names**: `-1` nothing, 0 a group, 1 a mech, 2 a flyer, 3 a base |
| `+0x0e` | ptr | block 10 `0x12` | **The subject**, resolved against `+0x0c` by `FUN_00425348` — a group record for kind 0, an object for 1-3 |
| `+0x12` | ptr | block 10 `0x14` → block 5 | **A mission action.** When it fires, the group moves on |

The verb's range is the first confirmation the field is what it looks like: across the 62 retail `.MSN` files that parse, block 10's `0x08` only ever holds 0-6, which is exactly the span of the switch in `Mech_AiSelectBehaviour`.

## The group record's order fields

A mission group's own record is `0x7a` bytes, built by `DBSim_BuildGroupRecord` (`00423b34`). The fields this layer owns:

| Offset | Type | Meaning |
|---|---|---|
| `+0x04` | short | **Route cursor** — the index of the waypoint last reached |
| `+0x06` | ptr | **Route** — the waypoint group the cursor runs over |
| `+0x0c` / `+0x10` | ptr / short | Member array and count |
| `+0x12` | byte | Side — [`mission-deployment.md`](mission-deployment.md) |
| `+0x14` | ptr | Deployment action; non-null means the group is not in the mission yet |
| `+0x1c` / `+0x30` | short[10] x2 | Block 11's two interleaved 10-short arrays, copied verbatim. No reader identified |
| `+0x44` | ptr[10] | **The order array**, a null in every slot the block-11 record left unset |
| `+0x6c` | int | **The current order's index** |
| `+0x70` | byte[10] | One flag per order, set when that order reaches completion |

`Sim_MainTick` runs exactly one of two things per group per frame: `Group_DeploymentCheck` (`004236c4`) when `+0x14` is set, `Group_OrderTick` (`00423a74`) otherwise. **A group waiting to arrive runs no orders and no AI.**

### The route cursor is loaded once

`DBSim_BuildGroupRecord` sets `+0x06` from **order slot 0's** `+0x08` and zeroes `+0x04`, and nothing else in the image writes either field except `FUN_0042313c`, which only steps the index. So a group has **one route for the whole mission**, whatever its later orders name — see the rejected reading below.

`FUN_00423b0c(cursor, index)` reads a waypoint out of it: null when there is no route or the index is past the end, otherwise the block-1 point at that index. Everything that consumes a route goes through it, which is why "is there a waypoint after the current one" is spelled the same way everywhere.

`FUN_0042313c(cursor)` advances the index, and **wraps it to zero when the route closes on itself**: with more than one waypoint, an index that has just landed on the last one whose point is the same object as the first restarts at zero. A closed route is a patrol that never ends; an open one runs out, and running out is what finishes a movement order.

## The tick — `Group_OrderTick` (`00423a74`)

```
order = group.orders[group.orderIndex]
advance = false
if (order != null) {
    if (Group_IsOrderComplete(group, order)) {
        advance = true
        group.orderDone[group.orderIndex] = 1
    } else if (order.action != null && order.action.fired != 0) {
        advance = true
    }
}
if (advance && group.orderIndex < 9 && group.orders[group.orderIndex + 1] != null) {
    group.orderIndex++
    for each member: member.dwellCountdown = 0        // the block field at mech+0x52
}
for each member: Mech_AiTick(member)
```

Four things the shape settles:

- **Two ways forward, and only one of them flags the order.** Completing it sets the `+0x70` byte; a mission action firing under it does not. Nothing reads `+0x70` in the AI, so it exists for something outside this layer.
- **The advance is capped by the next slot, not by the count.** A group that finishes its last order stays on it, re-testing (and re-flagging) it every frame for the rest of the mission.
- **The dwell reset is what makes an advance visible immediately.** Zeroing every member's countdown forces each one's reassess on the very next tick, so the new order takes effect at once rather than after the old state's dwell — see [`ai-dispatch.md`](ai-dispatch.md#what-the-dwell-time-buys).
- **Every member is ticked, live or not.** There is no filter here; `Mech_AiTick` handles a machine with no descriptor by doing nothing, which is how the base groups — which have no orders and no behaviour block — pass through harmlessly.

## When an order is finished — `Group_IsOrderComplete` (`004239fc`)

Switched on the verb. Two verbs have no completion test at all and can only be ended by their action firing.

| Verb | State it installs | Completes when |
|---|---|---|
| 0 | `search/destroy` | The subject's condition is 4 — destroyed |
| 1 | `ramming` | Never |
| 2 | `guarding` | The order names a subject, **and** either its condition is 4 or no rival group is still working to it |
| 3 | `patrolling` | The route has no waypoint after the cursor |
| 4 | `sleeping` | Never |
| 5 | `travelling` / `bulldog travel` | The route has no waypoint after the cursor |
| 6 | `following` | The route has no waypoint after the cursor |

The state column is [`ai-dispatch.md`](ai-dispatch.md#choosing-a-state--mech_aiselectbehaviour-0041eb34)'s; it is repeated here only as a key.

### The subject's condition — `Group_OrderSubjectCondition` (`00412dc4`)

A 0-4 scale, where 4 means gone. Which of two readings applies is the order's `+0x0c`:

- **An object** (kinds 1-3). Destroyed (`+0x99`) or collapsed (`+0xb4`) is 4. Otherwise its own vtable `+0x40` overall-damage figure, banded: below `0x32` → 0, below `0x80` → 1, below `0xc0` → 2, else 3.
- **A group** (kind 0) is `Group_ConditionTier` (`00412c8c`), which weighs the same scale across the members. Let *lost* be the members that are removed (`+0xa4`) or destroyed (`+0x99`), and *average* the mean of every member's overall damage — **including the dead ones**, each contributing whatever its damage figure last read. Then: all lost → 4; else *lost* at or above 650/1024 of the group **or** *average* at or above `0xc1` → 3; else 250/1024 or `0x81` → 2; else *average* at or above `0x33` → 1; else 0.

So a group is written off either by losing enough machines or by having enough damage spread across the ones it keeps.

### No rival group is still working to it — `Group_NoRivalOrderOnSubject` (`00412e74`)

Walks every mission group and answers false the moment it finds one that is **on the other side**, whose own current order names **the same subject pointer**, and that is not wiped out (`Group_IsWipedOut`, `00412be4` — every member removed or destroyed). A guard order therefore ends when the thing being guarded is gone *or* when nothing hostile is assigned against it any more: the post is finished, not just survived.

The comparison is on the subject pointer, not on the guarded position, so two groups only count as rivals when the mission gave them literally the same subject.

## What else reads an order

Only four of the record's fields are ever read, and only through the current index:

| Field | Read by |
|---|---|
| `+0x00` verb | `Mech_AiSelectBehaviour`, `Mech_AiCombatReassess`, `Ai_SelectTarget`, the `patrolling` think, `FUN_00420ad4`, `FUN_00422a80` — all with the same "or `0x0b` if the slot is null" idiom |
| `+0x0c` / `+0x0e` subject | `Group_OrderTargetObject` (`004238a0`), `Group_IsOrderTarget` (`00423918`), `Group_OrderTargetPosition` (`004238d4`) — see [`ai-targeting.md`](ai-targeting.md) for what each decides |
| `+0x12` action | `Group_OrderTick` alone |

`Group_OrderTargetPosition` is the one that falls back: with no subject it answers the **first waypoint of the group's route**, which is why a group ordered to hold a place with nothing named still has somewhere to stand.

## A group with no order at all

`Mech_AiSelectBehaviour` substitutes verb `0x0b` for a null order slot, and `0x0b` matches no case in its jump table. The function is `__cdecl(mech)` and the descriptor it is about to install lives in `EDX`, which nothing on that path writes — so the value handed to `Behaviour_SetState` is the caller's `EDX`. The caller is always `Behaviour_DispatchReassess` (`00415b74`), whose last write to `EDX` is the reassess triple's third word, and that word is zero in every one of the 22 source blocks. **The default path installs a null descriptor, and `Behaviour_SetState` dereferences it two instructions later.**

Nothing reaches it in retail. Across all twelve shipped `script.dat` handoffs every mech group and every flyer group carries an order in slot 0; the groups that carry none are all structures, and a structure has no behaviour block for `Mech_AiTick` to find.

## Engine port

`Sim.MissionGroup` holds the order array, the current index, the completion flags and the route cursor, and `World.MissionOrder` is the resolved record `MissionLoader` builds from block 10. `MissionScene` resolves each order's subject once every object exists, since an order may name a group that has not been built yet when its own group is.

What differs from the original, and why:

- **A null order slot leaves the machine's state unchanged** rather than installing a null descriptor. The original's behaviour there is a crash, not a decision.
- **Only `patrolling` and `travelling` can finish a movement order.** The route cursor is advanced by `Ai_FollowRoute` alone, and `following` never calls it — see [`ai-navigation.md`](ai-navigation.md), which is the original's own behaviour rather than a gap here.
- **The action path never fires**, because no mission action does; see [`mission-deployment.md`](mission-deployment.md).

## Open questions

- **Order `+0x02` and `+0x04`.** Both are resolved at load and never read. `+0x04` is a real block-1 point in 7% of retail records and `+0x02` holds 0, 1 or 3.
- **The order-completed flags at group `+0x70`.** Written by `Group_OrderTick`, read by nothing in the AI.
- **Group `+0x1c` and `+0x30`**, the two 10-short arrays copied out of the block-11 record.
- **The group-report cluster at `00412f90` / `00413280`.** A second family that reads the same order records — `00412f90` maps a verb and the group's condition tier onto a small integer that looks like a string index, and `00413280` walks a global array of records with a comparison discriminator and writes back into a variable table. It reads as the mission-objective and status-report layer rather than the AI, and `00412f90` has no caller Ghidra can see.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| Each order carries the route the group follows while that order is in force | The order record does hold one at `+0x08`, and retail missions do give later orders their own — but only slot 0's is ever installed, at group construction. A group that advances to its second order keeps walking the first one's route, which by then is exhausted |
| A group with no orders simply keeps whatever state its machines already had | That is what the *absence* of a matching case looks like in the decompiler, where the descriptor argument reads as an unwritten parameter. In the disassembly it is `EDX`, and `EDX` is zero |
| `Group_OrderTick` skips members that are dead or removed | It ticks every member unconditionally. The filtering is `Mech_AiTick`'s, and it is by whether the machine has a behaviour descriptor at all |
