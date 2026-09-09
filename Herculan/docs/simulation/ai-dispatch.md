# AI behaviour dispatch

The structural spine of the AI: how a machine's current behaviour is represented, how the per-tick work reaches it, and how it changes. Every other `ai-*.md` doc cites state indices and mech field offsets from here rather than restating them.

What each state actually *does* is out of scope. The walking states live in [`ai-navigation.md`](ai-navigation.md) and the fighting ones in [`ai-combat-states.md`](ai-combat-states.md); target handling is [`ai-targeting.md`](ai-targeting.md), weapon choice [`ai-weapons.md`](ai-weapons.md), and where a state's orders come from [`ai-goals.md`](ai-goals.md).

## Three parallel tables

DBSIM holds 22 behaviour states as three static arrays that share one index. They are contiguous, and the third begins exactly where the second ends, which is what fixes the element count at 22.

| Address | Name | Stride | Contents |
|---|---|---|---|
| `004993a4` | `BehaviourStateTable` | `0x3e` | The state descriptors. **Zero-filled in the image** — C++ statics built at startup |
| `004998f8` | `BehaviourSlotBlocks` | `0x24` | Source member-function triples the descriptors are built from |
| `00499c10` | `BehaviourStateNames` | packed | 22 NUL-terminated names, the game's own |

`004993a4 + 22 * 0x3e = 004998f8`.

Ghidra reports no xrefs on any of the three: the descriptors are reached by immediate address, the blocks are read by an unmarked static initialiser at `00413ed4`, and the names are reached as `base + offset`.

## The 22 states

Index `N` names descriptor `004993a4 + 0x3e*N` and block `004998f8 + 0x24*N`. The move slot is `Mech_MovementTick` (`0041a360`) for every state that has one bar `player fly`, so only the exceptions are spelled out.

| # | Name | Think | Move | Reassess | `+0x04` | `+0x3c` |
|---|---|---|---|---|---|---|
| 0 | `deciding` | — | — | `0041eb34` | 0 | 3 |
| 1 | `player` | `0041c194` | walk | — | 10 | 3 |
| 2 | `player fly` | `0041c194` | `004198f4` `Razor_MovementTick` | — | 10 | 3 |
| 3 | `attacking` | `0041c594` | walk | `0041cf18` | 50000 | 0 |
| 4 | `flanking` | `0041d4e4` | walk | `0041cf18` | 50000 | 0 |
| 5 | `facing off` | `0041d41c` | walk | `0041cf18` | 50000 | 0 |
| 6 | `attacking base` | `0041c86c` | walk | `0041cf18` | 50000 | 0 |
| 7 | `attacking flyer` | `0041c9cc` | walk | `0041cf18` | 50000 | 0 |
| 8 | `patrolling` | `0041d7d0` | walk | `0041eb34` | 10 | 3 |
| 9 | `travelling` | `0041d9cc` | walk | `0041eb34` | 10 | 3 |
| 10 | `following` | `0041daac` | walk | `0041eb34` | 10 | 3 |
| 11 | `bulldog travel` | `0041d9cc` | walk | `0041eb34` | 10 | 3 |
| 12 | `search/destroy` | `0041d60c` | walk | `0041eb34` | 10 | 3 |
| 13 | `sleeping` | `0041c418` | walk | `0041eb34` | 10 | 3 |
| 14 | `skirting` | `0041dd64` | walk | `0041eb34` | 10 | 0 |
| 15 | `guarding` | `0041e224` | walk | `0041eb34` | 10 | 3 |
| 16 | `driving off en` | `0041def0` | walk | `0041eb34` | 50000 | 0 |
| 17 | `ramming` | `0041e570` `Mech_BehaviourRamThink` | `0041e488` `Mech_BehaviourRamTick` | `0041eb34` | 10 | 0 |
| 18 | `fleeing` | `0041d2c4` | walk | `0041cf18` | 15000 | 5 |
| 19 | `in limbo` | — | — | — | 10 | 6 |
| 20 | `dead` | `0041e554` | walk | — | 0 | 6 |
| 21 | `disabled` | `0041e554` | walk | — | 0 | 7 |

Notes the table makes visible:

- **`deciding` (0) has no think and no move.** It is not a state a machine runs in; it is the state a machine is *put* in so that the reassess slot resolves it into a real one.
- **`travelling` (9) and `bulldog travel` (11) share a think function.** They differ only in which descriptor — and so which timing — is installed.
- **`dead` (20) and `disabled` (21) share a think function** and still take the normal walk move.
- **The reassess slot splits the roster cleanly in two.** Combat states (3–7, 18) use `Mech_AiCombatReassess` (`0041cf18`); every other live state uses `0041eb34`, the state-selection function itself. The combat form falls back on `0041eb34` when it finds nothing to fight — see [`ai-targeting.md`](ai-targeting.md#the-combat-reassess--mech_aicombatreassess-0041cf18).

`ramming` (17) is what anchors the indexing: its two slots are the independently-identified `Mech_BehaviourRamThink` / `Mech_BehaviourRamTick` pair (see [`damage-system.md`](damage-system.md)), and `player` (1) / `player fly` (2) match the three constructor branches in [`razor-flight.md`](razor-flight.md#how-the-flyer-paths-are-reached).

## Descriptor layout — `0x3e` bytes

| Offset | Type | Field |
|---|---|---|
| `+0x00` | ptr | The state's name, into `BehaviourStateNames` |
| `+0x04` | int | **Dwell time in milliseconds** — how long the machine stays in this state before reassessing |
| `+0x08` | 16 B | 16 one-byte booleans, expanded from a 16-bit mask by `Behaviour_ExpandFlagBits` (`00415028`) |
| `+0x18` | triple | **Think** — `{func, thisDelta, vtableIndex}` |
| `+0x24` | triple | **Move** |
| `+0x30` | triple | **Reassess** |
| `+0x3c` | short | The string index the F7 comm box prints on a squadmate's `OBJECTIVE:` line — `STRINGS0` group 40, `ATTACK`/`TRAVEL`/`PATROL`/`FORM UP`/`GUARD`/`FLEE`/`DEAD`/`IMMOBILE`. Read only by `Mech_SquadOrderLineIndex` (`0041bac8`); see [`../formats/heads-down-display.md`](../formats/heads-down-display.md) |

### The flag bits

`Behaviour_ExpandFlagBits` writes one byte per bit (`bit & 1`) over two source bytes, so `+0x08` is a bitmask unpacked for cheap indexed testing. Each state's mask is an immediate in the initialiser, and only the low 6 bits are ever set:

| Mask | States |
|---|---|
| `0x00` | `deciding` |
| `0x01` | `player`, `player fly`, `patrolling`, `travelling`, `following`, `search/destroy` |
| `0x02` | `attacking`, `flanking`, `facing off`, `attacking base`, `attacking flyer` |
| `0x03` | `skirting` |
| `0x05` | `guarding` |
| `0x06` | `driving off en` |
| `0x09` | `bulldog travel`, `sleeping`, `ramming` |
| `0x12` | `fleeing` |
| `0x21` | `in limbo`, `dead`, `disabled` |

| Bit | Consumer |
|---|---|
| 0 | `Mech_AiTick` skips the dwell countdown — see below |
| 1 | **Committed to a fight.** Suppresses a fresh reaction to incoming fire; the combat reassess's leader sweep skips a member that has it; the acquisition score in [`ai-targeting.md`](ai-targeting.md) discounts a candidate that lacks it |
| 2 | **Holding a place.** Fire is answered by defending the post rather than chasing the shooter |
| 3 | **Ignore fire entirely** |
| 4, 5 | The target's *state tier*, read off the target's descriptor by `Ai_TargetStateTier`: fleeing versus finished. See [`ai-targeting.md`](ai-targeting.md#abandoning-a-target--ai_shouldabandontarget-0041c4a8) |

The initialiser copies block `+0x00 → +0x18`, `+0x0c → +0x24`, `+0x18 → +0x30`, so the block and descriptor slot orders differ and only the descriptor order is the one the dispatchers use.

## The mech's behaviour block — `mech+0x4d`

Not a pointer field: `0x45` bytes embedded in the mech, running `mech+0x4d` to `mech+0x91`. `Behaviour_SetState` (`00413e50`) is the only writer.

| Offset | Type | Set on state change to |
|---|---|---|
| `+0x00` | ptr | The new descriptor |
| `+0x04` | byte | Not written here; `Timer_CountDown` is handed `&block+0x04` and steps the int that follows it |
| `+0x05` | int | The dwell countdown: `descriptor+0x04` + `(DAT_004a9bf4 & 0xf)`, after `DAT_004a9bf4 += 0xd` |
| `+0x09` | int | 0. Incremented once per AI tick by `00413eb0` |
| `+0x0d` | 0x28 B | Zeroed |
| `+0x35` | 0x10 B | Zeroed, then `+0x36 = 1` |

`DAT_004a9bf4` is a global stepped by 13 per state change and masked to 4 bits, so the jitter is 0–15 ms and deterministic in call order rather than random.

### What the dwell time buys

`Timer_CountDown` (`004679a4`) subtracts `SimTickDelta` from the countdown each AI tick and clamps it at zero, so `descriptor+0x04` is **milliseconds**. But `Mech_AiTick` only runs the countdown when descriptor flag bit 0 is *clear*, and that is true of exactly eight states: `deciding`, the five combat states, `driving off en` and `fleeing`. **For every other state the countdown is loaded and never stepped**, so its dwell value — 10 ms throughout — never expires and never means anything. Those states end on their own terms instead, through the two paths below.

The eight that do run a clock:

- **`deciding` holds for 0 ms**, so it resolves on the tick after it is installed. It is a placeholder, not a state a machine runs in.
- **Combat states and `driving off en` hold for 50 seconds.** A machine that has committed to `attacking` or `flanking` does not re-open the decision every tick, so it cannot thrash between engagement styles while a fight is in progress.
- **`fleeing` holds for 15 seconds.**

The 0–15 ms jitter is noise against all three figures.

Two things cut a dwell short. `Group_OrderTick` (`00423a74`) zeroes every member's countdown when the group advances to its next order, and a think function returning nonzero zeroes its own — the state's way of saying it is finished.

## The AI tick — `Mech_AiTick` (`00411cec`)

One machine's whole AI frame, and the only thing that calls the three dispatchers. **Its sole caller is `Group_OrderTick` (`00423a74`), at `00423af5`**, which runs it over every member of a group that has entered the mission.

That single call site is the most consequential fact in this doc: **the AI is driven from the mission-group layer, not from `Sim_MainTick`'s object loop.** A machine that is not a live member of a group never thinks, which is consistent with a group still awaiting deployment running `Group_DeploymentCheck` *instead of* `Group_OrderTick` — see [`mission-deployment.md`](mission-deployment.md).

```
if (mech+0x4d has a descriptor) {
    if (dwell countdown == 0)        vtable +0x1c   reassess
    if (descriptor+0x08[0] == 0)     Timer_CountDown(&block+0x04)
                                     vtable +0x14   move
    if (mech+0xaf == 0) {
        if (vtable +0x18 think != 0) dwell countdown = 0
    } else {
        mech+0xaf = 0
    }
    block+0x09++
}
```

Three things worth taking from the order:

- **Reassess runs before move and think**, so a state change takes effect on the same tick it is decided.
- **Move runs before think.** The machine is integrated on its old think's decisions, not the new ones. The think is where every steering decision is made — see [`ai-navigation.md`](ai-navigation.md).
- **`mech+0xaf` suppresses think for exactly one tick** and clears itself. Whatever sets it gets a frame of movement with no new decisions.

## How the per-tick work reaches a state

Three mech vtable slots (vtable at `0049a282`), each reading a different descriptor triple. All three have the same shape: read `mech+0x4d`, and if the triple is entirely zero return 0 without calling.

| Vtable | Function | Descriptor slot | Role |
|---|---|---|---|
| `+0x14` | `Behaviour_DispatchMove` (`00415afc`) | `+0x24` | Move |
| `+0x18` | `Behaviour_DispatchThink` (`00415b38`) | `+0x18` | Think |
| `+0x1c` | `Behaviour_DispatchReassess` (`00415b74`) | `+0x30` | Reassess |

Each calls `(*slot.func)(mech + slot.thisDelta)`. Because these are pointer-to-member calls made through the dispatchers rather than vtable entries, Ghidra reports zero xrefs on every think and move function in the table above — which is why the AI reads as unreachable code until the tables are followed by hand.

*Reassess* is this engine's name for the `+0x30` slot, taken from what its two implementations do; the game's own name for it is not in the binary.

## Choosing a state — `Mech_AiSelectBehaviour` (`0041eb34`)

Three mutually exclusive paths, tested in this order.

**1. The local player** (`mech+0xa3` set). Installs `player fly` if the type record's flyer flag (`typeRec+0x50`) is set, else `player`. `Mech_Constructor` (`00415bb0`) makes the same test, so the player's state is settled at construction and re-affirmed here.

**2. A machine in the player's own group** carrying a standing squad order (`mech+0x23e` nonzero). The order verb selects:

| `mech+0x23e` | Result |
|---|---|
| 1, 2 | `patrolling` |
| 4 | `Mech_AiEngageOrderedTarget` (`0041c0f4`) against `mech+0x248` when the order target's `+0x99` is clear; otherwise clear the order and re-enter |
| 6 | `guarding` |

`mech+0x248` is the order's target object. This path is the entry point for [`ai-squadmates.md`](ai-squadmates.md); verbs 3 and 5 fall through with no state installed.

**3. Everything else** — a mission-group machine. The group record (`mech+0x45`) holds an order array at `group+0x44` and a current index at `group+0x6c`; the order's first `short` is the verb:

| Verb | State |
|---|---|
| 0 | `search/destroy` |
| 1 | `ramming` |
| 2 | `guarding` |
| 3 | `patrolling` |
| 4 | `sleeping` |
| 5 | `travelling`, or `bulldog travel` when the chassis' torso-twist limit (`typeRec+0x22`) is above `0x7d00`. It is 14000 across the whole fleet, so **`bulldog travel` is never installed** |
| 6 | `following` |

A null order entry substitutes verb `0x0b`, which matches no case — and falls through to `Behaviour_SetState` with a **null descriptor**, since the function is `__cdecl(mech)` and the descriptor it installs lives in `EDX`. See [`ai-goals.md`](ai-goals.md#a-group-with-no-order-at-all), which owns the order data along with [`msn-mission-file.md`](../formats/msn-mission-file.md).

Every path ends the same way: the machine's selected target (`mech+0x1a4`) is released, the refcount at `target+0x1a2` decremented, and `mech+0x9d` set — so **a state change always drops the target**. See [`target-selection.md`](target-selection.md).

### Transitions

`Behaviour_SetState` (`00413e50`) has **30 call sites**, which are the state machine's edge list. Which function installs which state is [`ai-combat-states.md`](ai-combat-states.md#the-transition-graph).

## Mech fields the AI owns

Fields first read or written by the dispatch layer. Fields whose meaning is settled elsewhere link out rather than being restated.

| Offset | Type | Meaning |
|---|---|---|
| `+0x45` | ptr | Mission group record — [`mission-deployment.md`](mission-deployment.md) |
| `+0x4d` | 0x45 B | The behaviour block, above |
| `+0x9d` | byte | Set whenever the selected target is released |
| `+0xa3` | byte | This is the locally-piloted machine |
| `+0xaf` | byte | Suppress think for one tick; `Mech_AiTick` clears it |
| `+0x23e` | short | Standing squad order verb |
| `+0x248` | ptr | Squad order target object |
| `+0x1a4` / `+0x1a2` | ptr / short | Selected target and its refcount — [`target-selection.md`](target-selection.md) |
| `+0x1f2` | ptr | Type record — [`mech-locomotion.md`](mech-locomotion.md#mech-instance-fields) |

## Where the slices start

The AI-relevant mech vtable slots, as entry points for the topic docs. Slots whose meaning is settled elsewhere link out rather than being restated.

| Vtable | Function | Lands in |
|---|---|---|
| `+0x40` | `Mech_GetOverallDamage` (`00415504`) | The flee check's base fear — [`ai-targeting.md`](ai-targeting.md#the-flee-check--mech_aifleecheck-0041cb94) |
| `+0x48` | `Mech_AiEnemySighted` (`00412800`) | The "enemy detected" callout — [`ai-targeting.md`](ai-targeting.md#radio-callouts) |
| `+0x4c` | `Mech_CompareCombatRating` (`0041cabc`) | This machine's combat rating against a candidate's — [`ai-targeting.md`](ai-targeting.md#relative-combat-rating) |
| `+0x50` | `Mech_AiOnTakingFire` (`0041f7b8`) | "This object just took fire" — [`ai-targeting.md`](ai-targeting.md#taking-fire--mech_aiontakingfire-0041f7b8-mech-vtable-0x50). Holds one of the 30 `Behaviour_SetState` call sites |
| `+0x64` | `Mech_AiOnLineOfFireBlocked` (`0041dd2c`) | "My shot hit something that is not what I aimed at" — the trigger for `skirting`, [`ai-combat-states.md`](ai-combat-states.md#how-it-is-reached) |
| `+0x68` | `FUN_0042200c` | "Something ran into me" — [`mech-locomotion.md`](mech-locomotion.md) |

## Open questions

- **Bits 6–15 of descriptor `+0x08`.** No state sets one, so nothing can read one.
- **Verbs 3 and 5 of the squad-order path**, which install nothing.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| The `0x24`-stride table starts at `00499928` and holds `Mech_MovementTick` at `+0x18`, one entry per mech type | Off by one triple. Blocks start at `004998f8` and the move slot is `+0x0c`; `00499928` is block 1's move. A raw byte search really does find `0041a360` at 18 sites of stride `0x24`, but those are the 18 **states** that share the walk move, not 18 mech types |
| `Mech_AiSelectBehaviour` is `__fastcall` and takes three arguments | Ghidra types it that way, and its own recursive call obliges by passing three. The prologue is `MOV EBX,[EBP+8]` and nothing else: it is `__cdecl(mech)`, and the second "parameter" is the `EDX` the fall-through installs |
| `Mech_MovementTick` is dispatched from mech vtable `+0x18` | `+0x18` is the **think** dispatcher. The move is vtable `+0x14` (`00415afc`), reading descriptor `+0x24`. For most states the think function drives locomotion itself, which is why the move slot looks like the tick entry |
| The think and move functions are dead code | Every one has zero xrefs because it is only ever reached as a pointer-to-member through `00415afc` / `00415b38` / `00415b74` |
