# AI targeting

How an AI machine acquires, shares, keeps and abandons a target.

[`ai-dispatch.md`](ai-dispatch.md) owns the 22 behaviour states, the descriptor layout, the `mech+0x4d` behaviour block and the three vtable dispatchers; state indices, descriptor addresses and descriptor flag bits are cited from there. [`target-selection.md`](target-selection.md) owns `mech+0x1a4` itself, the sensor model that decides what is *known*, and the player's own selection, which is made in the cockpit and never by this code.

`Ai_SelectTarget` is not mech-only: structures call it too — `FUN_00404100` with mask `0x30` and `FUN_004045c8` with mask `0x10` inside a `0x3000` cone — so it is the sim's one target-acquisition routine.

## The writers of `mech+0x1a4`

| Writer | When |
|---|---|
| `Mech_AiCombatReassess` (`0041cf18`) | The reassess slot of every combat state |
| `Mech_AiOnTakingFire` (`0041f7b8`) | Something hit this machine |
| `Mech_AiEngageOrderedTarget` (`0041c0f4`) | A squad order names a target |
| `Mech_BehaviourRamThink` (`0041e570`) | The ramming state, through `Ai_SelectTarget(this, 6, 0)` |
| The state think functions | Each calls `Ai_SelectTarget` for itself; 16 call sites in all |

All of them maintain the target's `+0x1a2` holder count and raise `mech+0x9d` the same way, so the rule in [`target-selection.md`](target-selection.md) holds for the AI paths as well.

## Is it a target at all — `Ai_IsTargetable` (`00411e80`)

`(this, candidate, mask)`. Rejects, in order:

- the same side (`group+0x12`);
- `+0x99` destroyed, `+0xb4` collapsed, or `+0xb7` invulnerable (`BASES.DAT +0x1e`, latched by `Base_Construct`);
- not currently known — `Ai_KnowsObject` (`00411c58`): radar-visible (`+0x95`) within **999999**, or a contact this machine holds (`this+0xc2 + candidate[0x4b]`) at any range. **The AI's knowledge test is far looser than the player's**, which caps radar at 200000 and contacts at 30000/60000;
- with `mask & 0x10`, a candidate of this machine's own object class (`+0x1a8`);
- a flyer (class 2) that is dead or dying;
- the group's own order target, while this machine's group is led by the local player and `DAT_004a9ed8 == 3`;
- when `this+0x9a` is set, whatever the player currently has selected (`CockpitViewInstance+0x210`) — the courtesy that stops the squad piling onto the player's target. `Mech_AiOnTakingFire` clears `+0x9a` when the player's target is the thing shooting at this machine.

## Acquisition — `Ai_SelectTarget` (`00411fa0`)

`(this, mask, coneLimit)` walks the live object list and returns the best-scoring candidate, or 0.

**Mask bits**, and the callers that pass them:

| Bit | Effect | Passed by |
|---|---|---|
| `0x01` | Only the group order's designated target gets the tier bonus below | `Mech_AiCombatReassess`, the think functions |
| `0x02` | Suppress the "it is shooting at me" weight | `Mech_BehaviourRamThink` (`6`) |
| `0x04` | Suppress the crowding divisor | `Mech_BehaviourRamThink`, `Mech_AiOnTakingFire` (`0x24`) |
| `0x10` | Reject candidates of this machine's own class, through `Ai_IsTargetable` | the two structure call sites |
| `0x20` | Ignore bearing: score on range alone | `Mech_AiOnTakingFire`, `FUN_00404100` |

**The tier** is a coarse bar applied before scoring: `2 * designated + alive`, where *designated* means `Group_IsOrderTarget` (`00423918`) and a group order verb of 0, and *alive* is the usual `+0xa5`/`+0xa4`/`+0x99` triple. The bar starts at 1 and drops to 0 when the group order verb is 3 (patrolling). With `mask & 1` clear every candidate counts as designated, so the bar only bites for the callers that set the bit. A candidate whose `+0xa4` is set is skipped outright **unless it is the designated target and this machine is Cybrid** — human-side machines leave a crippled target alone, Cybrids finish it.

Range is capped at **1000000** for the designated target and **100000** for anything else.

**The score** starts from bearing, `b = 0x1000 - (bearingError >> 3)`, which is 4096 dead ahead and 0 astern. `coneLimit`, when nonzero, rejects anything outside it.

```
score = Q10(70, b)                               // 0..280
if (range < 60000) score = max(score, b / max(range >> 12, 1))
score = Q10(score, W_shooting[rating])           // when candidate+0x1a4 == this, unless mask & 2
score = Q10(score, W_idle[rating])               // when its state's flag bit 1 is clear and it is not the player
score = Q10(score, W_taken[rating])              // when it holds some other target
score = Q10(score, W_class[objectClass])
if (holders != 0) score /= holders + 1           // holders = candidate+0x1a2 less this machine; unless mask & 4
```

Inside 360 m the proximity term takes over completely — at point-blank `b / 1` is an order of magnitude above the bearing term, so a close enemy outranks a better-aligned distant one.

| Table | Values (Q10) | Indexed by |
|---|---|---|
| `W_shooting` `0049933c` | 1500, 2000, 2500 | relative combat rating |
| `W_idle` `00499342` | 600, 400, 200 | relative combat rating |
| `W_taken` `00499348` | 700, 850, 1000 | relative combat rating |
| `W_class` `0049934e` | 1500, 700, 500, 500 | object class `+0x1a8` |

A machine strongly prefers what is already shooting at it, discounts anything not yet engaged — hardest when that thing outguns it — and mildly discounts what someone else already holds. A structure that is shooting at this machine is re-indexed as class 0, giving it a HERC's weight.

## Relative combat rating

**`Mech_CompareCombatRating` (`0041cabc`, mech vtable `+0x4c`)** returns the index those three weight tables share: **0** this machine's rating is the higher, **1** the two are within 400, **2** the candidate's is higher. It returns 0 for every non-HERC candidate, so structures and flyers always score against column 0.

Both ratings are jittered before the comparison, and the jitter is `rand & 1000` where `rand % 1000` was plainly meant: `AND AX,0x3e8` at `0041cadd` and `0041caf8`. Masking against `0x3e8` can only produce the 32 values that are subsets of its bits, so the jitter spans 0–1000 but lands on very few of them.

**The rating itself is `mech+0x29e`, computed by `Mech_ComputeCombatRating` (`0041edd8`)**:

```
rating = (typeRec+0x44                                                    // a per-type base
        + sum over live mounts   Q8(template+0x4e, 256 - mountDamage)     // weapon value x condition
        + sum over 19 components Q8(componentMax,  256 - damage)          // structure value x condition
        - sum of typeRec+0x7e[i] for each of 10 systems over 70% damaged
        ) >> 4
```

The weapon term reads the mount's own `+0x1c`, which `WeaponMount_CtorBase` (`0040df30`) sets to the `WEAPONS.DAT` template, so `+0x4e` is a template field. A mount counts when its vtable `+0x54` says so, and that slot is `return 1` on both the base and the pod class, so every mount counts.

**Retail states the same two numbers for all 21 chassis** — a base of 1000 at `typeRec+0x44` and a penalty of 500 at each `typeRec+0x7e[i]` — so what separates two machines is entirely their guns, their armour and their damage.

`Mech_ReadDamageReadouts` fills the three parallel readout blocks it reads — 19 components, 10 systems, 10 mounts — as Q8 damage. `Mech_PerTickSystemsUpdate` recomputes the rating whenever `mech+0x94` is clear, so it is a lazily refreshed cache that tracks battle damage: **a machine's worth as a target, and its own willingness to fight, both fall as it is shot apart.**

## Passing a contact on

`Mech_AiOnTakingFire` routes the attacker through `Detection_ShareContact` (`00412704`, via the thin `00411aec`), the same function the sensor sweep uses — see [`target-selection.md`](target-selection.md#the-sensor-model--fun_004123ac). **Being shot is a way of being spotted**: the whole side within 100000 of the attacker learns where it is, whether or not anyone had line of sight.

## Taking fire — `Mech_AiOnTakingFire` (`0041f7b8`, mech vtable `+0x50`)

`(this, attacker, damage)`. The gates, in order:

1. If the attacker is the player and this machine is in the player's group, `Mech_AiFriendlyFireComplaint` (`0041f790`) posts squad message 8 on a 40 s cooldown (`this+0x278`).
2. Return if the attacker is dead or dying, or on this machine's own side.
3. Share the contact; set the under-fire window `this+0x27d` to **30000**.
4. **A machine in the player's group accumulates the damage in `this+0x281` and ignores it until the total passes 8000.** The accumulator is cleared 30 s after the last hit, by the timer `Mech_PerTickSystemsUpdate` steps at `this+0x27c`. The player's squadmates are deliberately slow to break off.
5. Return for the local player, while the retarget cooldown `this+0x273` runs, or when the attacker is already this machine's target and its state carries flag bit 1.
6. Set the retarget cooldown to **10000**.

Then the response, chosen by the current state's descriptor flag bits:

- **Not yet engaged (bit 1 clear), in a group led by the player** — post squad message 3 if the attacker is a HERC, and clear `+0x9a` if the attacker is what the player has selected.
- **bit 2 — `guarding`, `driving off en`.** Defend the post instead of chasing the shooter: `Mech_AiGoalPosition` (`0041dbcc`) resolves the place being held — a squad order's target or point, otherwise `Group_OrderTargetPosition` (`004238d4`) — and `Ai_SelectDefenceTarget` (`0041e0e0`) picks the HERC scoring highest on `(limit - distanceToPost) / (holders + 1)`, `limit` being 90000, or 980000 when the group's current order names no object. A squad order with verb 4 overrides the pick with its own target. Installing a HERC gives `driving off en`; anything else goes to `Mech_AiEngageOrderedTarget`.
- **bit 3 — `sleeping`, `ramming`, `bulldog travel`.** Return: incoming fire is ignored entirely.
- **Otherwise** — `Ai_SelectTarget(this, 0x24, 0)`, bearing-blind and crowding-blind, because the machine is reacting rather than choosing. Under squad order verb 4 a target that is not shooting at this machine is replaced by the order's own, and the order is cleared if that has become untargetable. Finding nothing while it held something re-enters `Mech_AiSelectBehaviour`; finding something sets `+0xac` and enters the combat reassess.

## The combat reassess — `Mech_AiCombatReassess` (`0041cf18`)

The reassess slot of states 3–7 and 18. `Mech_AiEnterCombat` (`0041d5ec`) is a thin wrapper other paths call to force it.

**Radar.** `mech+0x96` goes ACTIVE here unless the machine's group is led by the local player, gated on the countdown at `mech+0x26b`; a player squadmate is put back to PASSIVE unless `mech+0xb2` is set. This is what puts a distant enemy's radar on, and so what makes it targetable by the player at long range — see [`target-selection.md`](target-selection.md#radar-mode).

**Keep or acquire.** `mech+0xac`, set by whoever just handed this machine a target, suppresses the acquisition for one pass and clears itself. Otherwise the squad order verb splits it:

- **verbs 3–6** — the squad order's target (`mech+0x248`) is installed; if it is absent or destroyed, `Ai_SelectTarget(this, 0, 0)` replaces it.
- **verbs 0–2** — `Ai_SelectTarget(this, 1, 0)`, which prefers the group order's designated target. The one exception: a machine that is *already* committed (flag bit 1) and under a movement order — squad verb 1, or squad verb 0 with a group order verb of 5 or 6 — skips acquisition and falls into `Mech_AiSelectBehaviour`, which drops the target and returns it to the order. **Being told to move ends a fight.**

Coming out of either with no target also ends in `Mech_AiSelectBehaviour`.

**The leader drags the group in.** A machine that is its group's first member (`**(group+0xc)`) runs `Mech_AiEnterCombat` on every other live member whose state's flag bit 1 is clear. One member finding a fight commits the whole group to it.

**Then the state.** `mech+0x2a2` is reset to −1, the flee check runs, and if it did not take the decision itself, the combat state follows from the target:

| Target | State |
|---|---|
| Class 1 or 3 — structure | `attacking base` (6) |
| Class 2 — flyer | `attacking flyer` (7) |
| Class 0, rating index 0 — I outgun it | `facing off` (5) |
| Class 0, rating index 1 — evenly matched | `attacking` (3) |
| Class 0, rating index 2 — it outguns me | `flanking` (4), or `facing off` when the machine's `+0xa8` or `+0xa9` is set or its type's `typeRec+0xc8` is 0xb9 or under |

**`flanking` is unreachable in retail.** `typeRec+0xc8` is record field 198, which every one of the 21 shipped `.DAT` files states as zero, so the gate never opens and a machine that is outgunned takes `facing off` instead. `+0xa9` is the softer of the two leg states, which fits a manoeuvre a crippled machine should not attempt; `+0xa8` is unidentified.

`Mech_AiSelectAimComponent` runs on the two class-0 branches that reach it.

## The flee check — `Mech_AiFleeCheck` (`0041cb94`)

Returns nonzero when it has taken the decision itself, which is how the reassess above knows to stop.

A machine that is itself out of action (`+0xa5`, `+0xa4` or `+0x99`) takes `fleeing` (18), unless its target is a structure whose `BASES.DAT +0x2e` is zero, which sends it to `Mech_AiSelectBehaviour` instead. What `+0x2e` distinguishes is not known — [`base-type-table`](../../src/Herculan.Engine/World/BaseTypeTable.cs) reads past the field without using it.

Otherwise it builds a **fear** value: `Mech_GetOverallDamage` (mech vtable `+0x40`) plus, for each of 6 components (`0049a328` = 0, 1, 4, 5, 6, 7) whose Q8 damage exceeds a band threshold, that band's penalty — `+5` over 60, `+25` over 120, `+50` over 180, cumulative, stopping at the first band no component reaches. Fear then decides against `+0x1a2`, the number of machines holding this one as their target:

| Fear | `+0x2aa` | Flees when |
|---|---|---|
| ≥ 71 | 1000 | anything at all is targeting it |
| 51–70 | 600 | 3 or more are, or 1–2 are whose combined rating beats its own |
| 31–50 | 300 | 3 or more are, whose *average* rating beats its own |
| ≤ 30 | — | never |

`Ai_SumAttackerRatings` (`0041cb44`) is the sum of `+0x29e` over every machine holding this one. `+0x2aa` is written on all three live bands and read nowhere this slice reaches.

## Which component the shot is aimed at — `Mech_AiSelectAimComponent` (`0041ce08`)

Writes `mech+0x2a2`, the component slot the machine aims at, or −1. One roll of `rand & 0x7f` picks a band:

| Roll | Band | Slots |
|---|---|---|
| — | Target has collapsed (`+0xb4`) | 0 only |
| < 0x28, **or** a targeting computer at `mech+0x30b` whose `+0x7f` is under 0xaa | Systems | 7 … 18 |
| < 0x50 | Weapon mounts | 19 … 19 + mount count |
| otherwise | Chassis | 0 … 6 |

**Weapon-mount components start at slot 19**, the count coming from the mount manager's `+0x08` (see [`weapon-mounts.md`](weapon-mounts.md#the-manager--mech0x202)). Within the band it skips slots the target no longer has — its occupancy array at `target+0x20e` — and takes the highest `Component_ReadDamagePercent + (rand & 0x3f)`, working at whatever is already most damaged with enough jitter to spread the fire.

**It reads its own damage, not the target's.** `0041cec9` passes `this+0x206` to `Component_ReadDamagePercent` while the loop walks the *target's* occupancy array, so the AI works on whichever component of **its own** hull is worst hurt, constrained to the slots the target still has. Nothing in the function reads the target's damage.

## Abandoning a target — `Ai_ShouldAbandonTarget` (`0041c4a8`)

Called from the think functions of `attacking`, `flanking`, `facing off` and `driving off en`. Three answers, in order:

1. **Under squad order verb 4** with a target object: abandon when the target's *state tier* exceeds the order's threshold at `mech+0x250`. The tier is `Ai_TargetStateTier` (`00411cb4`), read from the target's own behaviour descriptor: **2** for flag bit 5 (`in limbo`, `dead`, `disabled`), **1** for bit 4 (`fleeing`), 0 otherwise. So an order can say *chase it until it runs* or *chase it until it drops*.
2. **The group order's designated target** (`Group_IsOrderTarget`): a human-side machine abandons it dead or crippled, a Cybrid only once `+0x99` is set.
3. **Anything else**: abandon when dead or dying.

## Radio callouts

`Ai_PostSquadMessage` (`00420a98`) posts `{id, machine}` to the object `FUN_00433158` returns, through its vtable slot 0, and is suppressed for a destroyed machine unless forced. Three ids are raised from this slice:

| Id | Raised by |
|---|---|
| 1 | `Mech_AiEnemySighted` (`00412800`, mech vtable `+0x48`) |
| 3 | `Mech_AiOnTakingFire`, for a squadmate of the player hit by a HERC |
| 8 | `Mech_AiFriendlyFireComplaint`, when the player is the one shooting |

There is a **second friendly-fire site**, in `Sim_RaycastObjectList` itself rather than in `Mech_AiOnTakingFire`: when the player hits a machine on his own side but in a different group, `Group_NearestLiveMember` (`00423974`) finds that machine's nearest live groupmate within 100000 and, if it is inside 30000 of the machine that was hit, that groupmate complains instead of the victim.

`Mech_AiEnemySighted` fires once per enemy for the whole player group: `DAT_004a9b84[obj+0x4b]` is a per-object latch, set the first time either the machine or the player holds a contact on that object, and the callout is further rate-limited to one per 10 s by `DAT_004a9be9`. The local player's own machine sets the latch without ever calling out.

The channel these post to is the pilot-and-squad message port, which is not ported — see [`../formats/audio.md`](../formats/audio.md).

## Mech fields this slice owns

Fields settled elsewhere link out rather than being restated.

| Offset | Type | Meaning |
|---|---|---|
| `+0x94` | byte | Combat rating cached; clear means recompute |
| `+0x9a` | byte | Do not take the player's current selection as a target |
| `+0xa5` | byte | No weapons left; written by `Ai_ChooseWeapon` — [`ai-weapons.md`](ai-weapons.md) |
| `+0xac` | byte | A target was just handed to this machine; skip one acquisition |
| `+0xb2` | byte | Keeps a player squadmate's radar active; written by the squad command handler — [`ai-weapons.md`](ai-weapons.md) |
| `+0xb4` | byte | Collapsed — latched when the death animation finishes (`Mech_LocomotionTick`) or a fall cripples the machine outright (`004178e8`) |
| `+0xb7` | byte | Invulnerable; `Base_Construct` sets it from `BASES.DAT +0x1e` |
| `+0x250` | short | Squad order's abandon threshold |
| `+0x26b` | short | Countdown that holds the radar off, 6000 after an ARM hit — [`ai-weapons.md`](ai-weapons.md) |
| `+0x273` | int | Retarget cooldown, 10000 on reacting to fire |
| `+0x278` | int | Friendly-fire complaint cooldown, 40000 |
| `+0x27d` | int | Under-fire window, 30000; its expiry clears `+0x281` |
| `+0x281` | int | Damage accumulated from the player's group, threshold 8000 |
| `+0x29e` | short | Combat rating |
| `+0x2a2` | short | Component slot being aimed at, −1 for none |
| `+0x2aa` | short | Fear: written by the flee check, 300/600/1000, and read as the AI's weapon-score floor — [`ai-weapons.md`](ai-weapons.md) |
| `+0x30b` | ptr | Targeting computer pod — [`missile-lock.md`](missile-lock.md) |

## Engine port

`Sim.Ai.AiTargeting` holds the shared routines, `Sim.Ai.BehaviourState` the 22 descriptors and the `mech+0x4d` block, `MechObject.Ai.cs` the machine's own half, and `Sim.MissionGroup` the record the AI is driven from. `MissionScene` builds one group per block-11 index, attaching objects in placement order so the group's first member is its leader, and `SimWorld` runs the groups' AI pass alongside the object updates and ahead of the sensor sweep.

**What runs.** The behaviour block and its dwell clock, `Mech_AiTick`'s reassess dispatch, the combat reassess entire — radar, keep-or-acquire, the leader sweep, the flee check, the state install and the aim pick — `Mech_AiOnTakingFire` from the raycast's `+0x50` site, both friendly-fire sites, and `Ai_SelectTarget` with all four weight tables and the combat rating behind them. A structure's two acquisition call sites are not wired: `BaseObject` has no AI yet.

**What that adds up to in a mission.** An AI machine is constructed in `deciding` and its group's current order resolves that into the state the order asks for — [`ai-goals.md`](ai-goals.md). It still **enters combat only by being shot at**, because every non-combat state's own way in is its think function, and those belong to the slices this one does not cover; once it is in, it acquires, lights its radar, picks a combat state and holds the target for the 50 s dwell. It does not move or fire on it: the move slot is the locomotion tick every machine already runs.

Deviations, all of them things the original reads that this engine has no value for:

- **`mech+0x9a`** and **`DAT_004a9ed8`**, both of which narrow `Ai_IsTargetable`, are not modelled. Their absence can only let the AI consider more candidates than the original, never fewer.
- **`mech+0xb2`** is not modelled, so a player squadmate is always put back to passive on entering a fight. Its writer is the squad command path, which is unported.
- **`mech+0xb4`**, collapsed, is never set — the death animation is not played out, so nothing latches it.
- **The aim band's targeting-computer override is not applied.** It turns on a pod field (`+0x7f`) whose meaning is untested, the same doubt the ECM roll records, so the roll alone picks the band.
- **Squad orders are unported**, so `Mech_AiSelectBehaviour`'s second path installs nothing and `Ai_ShouldAbandonTarget`'s squad branch is unreachable. Group orders are ported; what a designated target is, and which machines have one, is [`ai-goals.md`](ai-goals.md).

Two things are reproduced rather than corrected: the `rand & 1000` jitter in the rating comparison, and the aim pick reading its own component damage.

## Open questions

- **`DAT_004a9ed8 == 3`**, which makes a player-led group's own machines refuse the group order's target in `Ai_IsTargetable`. Reads like a squad-command or difficulty mode.
- **`BASES.DAT +0x2e`**, the flee check's structure exception.
- **What sets `mech+0x9a`.** Read here against the player's group; the writer is presumably in the squadmate command path, as `mech+0xb2`'s is.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `Mech_AiOnTakingFire` is a damage function | It is called per raycast candidate from `Sim_RaycastObjectList` and takes a damage amount, which makes it look like one. It applies no damage: the amount only feeds the `+0x281` accumulator that decides whether a player's squadmate reacts at all |
| `Ai_TargetStateTier` reads offsets `+0x0c`/`+0x0d` of the target's behaviour *block* | It dereferences `target+0x4d` first, so those are offsets into the **descriptor** the block points at — expanded flag bits 4 and 5, not block fields |
| `Mech_AiSelectAimComponent` picks the target's weakest component | It walks the target's component *occupancy* array but reads `this+0x206` for the damage, which is its own |
