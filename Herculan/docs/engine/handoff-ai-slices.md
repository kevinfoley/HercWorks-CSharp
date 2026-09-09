# AI port — doc structure and slice plan

Working plan for reverse-engineering and porting the DBSIM AI. Handoff doc: ephemeral, and **not authoritative for status** — grep the named types before repeating anything in the Status column. Drain it into the topic docs and delete what you move.

## Document structure

Six docs, split by what each owns. The rule throughout is one fact, one home: cite across, never restate.

| Doc | Owns |
|---|---|
| `ai-dispatch.md` | The spine. The 22 behaviour states, descriptor layout and flag bits, the `mech+0x4d` behaviour block, the three vtable dispatchers, `Mech_AiTick`, and the AI-owned mech field offsets. Every other `ai-*.md` cites state indices and offsets from here |
| `ai-targeting.md` | Acquisition, targetability, combat rating, contact sharing, taking fire, the combat reassess, the flee check, aim-component choice, abandonment, radio callouts |
| `ai-navigation.md` | Route and waypoint following, obstacle avoidance, terrain handling, arrival and stop conditions |
| `ai-weapons.md` | Weapon choice, engagement envelope, fire decisions, gun convergence for AI |
| `ai-goals.md` | The mission-group order layer: the order array, how orders advance, and how a verb becomes a behaviour state |
| `ai-combat-states.md` | The nine thinks that are not navigation: the five combat states, `skirting`, `driving off en`, `fleeing`, and the two inert ones. The combat geometry block, the move step, the circling step, and the `Behaviour_SetState` transition graph |
| `ai-squadmates.md` | The player's own squad — standing orders at `mech+0x23e`, formations in motion, mutual support. Written only if this proves to be its own mechanism rather than a special case of `ai-goals.md` |

Docs outside this set that the AI work amends rather than owns: `target-selection.md` (the shared selection mechanism and `mech+0x1a4`), `mech-locomotion.md` (the locomotion model), `mission-deployment.md` (group arrival), `damage-system.md`.

## Slices

Each slice is reverse-engineered to its topic doc, reviewed, then ported. The RE half is where a wrong reading is cheapest to catch, so the doc lands before any engine code.

| # | Slice | Entry points | Status |
|---|---|---|---|
| 0 | **Dispatch** | `BehaviourStateTable` `004993a4`, `Mech_AiTick` `00411cec`, `Mech_AiSelectBehaviour` `0041eb34` | Doc written |
| 1 | **Targeting** | `Ai_SelectTarget` `00411fa0`, `Mech_AiOnTakingFire` `0041f7b8`, `Mech_AiCombatReassess` `0041cf18`, `Ai_ShouldAbandonTarget` `0041c4a8` | Doc written; ported |
| 2 | **Goals** | `Group_OrderTick` `00423a74`, the order array at `group+0x44` indexed by `group+0x6c` | `ai-goals.md` written; ported. Moved ahead of plan — see below |
| 3 | **Navigation** | `Mech_AiObstacleAvoidance` `00416274`, the travel and patrol think functions (`0041d7d0`, `0041d9cc`, `0041daac`, `0041d60c`) | `ai-navigation.md` written; ported. Also took `guarding`'s think and `Terrain_RayWalk`'s mode 1 — see below |
| 4 | **Weapons** | `Ai_AimAndFire` `0041ea7c`, `Ai_FireAtPoint` `0041f5a0`, `Ai_ChooseWeapon` `0041f358`, `Mech_ConvergeGunsOnRange` `0041a74c` | `ai-weapons.md` written; ported. Corrected the radar reading — see below |
| 5 | **Behaviour states** | The remaining think functions and the 30 `Behaviour_SetState` call sites as the transition graph | `ai-combat-states.md` written; ported. Found `skirting`'s caller — see below |
| 5b | **Death and disablement** | `Mech_ComponentDamageWrite` `00417de4`'s two out-of-the-fight branches, `Mech_LocomotionTick`'s immobilised arm | Ported. Closed `ai-dispatch.md`'s `+0x3c` question and `ai-goals.md`'s group `+0x1c`/`+0x30` — see below |
| 6 | **Squadmates** | `mech+0x23e` standing orders, `FUN_0041c0f4`, `Mech_ApplyFormationOffset` `00417898` | Not started |
| 7 | **Flyer AI** | The flyer behaviour path; no retail mission places an AI RAZOR, so verification is synthetic | Not started |

### What slice 3 pulled in

Two things outside the AI turned out to be load-bearing for it.

- **`Terrain_RayWalk`'s mode 1.** The obstacle probes lie flat on the ground, so the thin-ray query grazes on every tick of rolling terrain and pins the steer hard over. Mode 1 is a slope test instead — `Terrain_FaceBlocksMovement` (`0046fe40`) — and the engine had only mode 0. Ported here.
- **`Sim_RaycastShapes` (`00404ca0`).** A standing animated structure's collision radius is zero, so the proximity sweep cannot see one; without the shape half of the probe a machine walks into a building and stands there. The engine has no swept-shape cast, so it stops at the bounding radius.

### What slice 4 corrected

`mech+0x96` is the **radar mode**, not a weapons-free flag, and `Ai_UpdateWeaponsFree` is the AI's radar switch. The mission-file field feeding it (`.MSN` row #12 `+0x08`) is a radar setting. Four docs and the engine's own second copy of the byte were carrying the wrong reading.

It also closed three of `ai-targeting.md`'s "no writer found" entries: `mech+0xa5` is *no weapons left* and `Ai_ChooseWeapon` writes it, `mech+0x26b` is the ARM radar-silence timer and `Mech_DirectFireHitTest` writes it, and `mech+0xb2` is written by the squad command handler. And it traced the gun convergence's consumer, which `weapon-firing.md` had recorded as inert on every retail chassis when it is in fact live on all of them.

### What slice 5 corrected

- **`skirting` is reachable, and `Sim_RaycastObjectList` is what reaches it.** Mech vtable `+0x64` looked uncalled because the decompiler renders the slot in decimal (`+ 100`); four call sites exist. The state fires when a machine's own shot stops on something that is not what it aimed at.
- **`Ai_LineOfSightBlocked`'s two nonzero answers are not "shape" and "terrain".** `1` is anything the machine cannot get past, `2` is ground it could walk over — which is what makes `skirting` arc around the first and drive straight at the second. `ai-navigation.md` carried the wrong pair.
- **`BASES.DAT +0x2e` is read in two places, not one**, and both treat nonzero as *this target is dangerous*. It closed `ai-targeting.md`'s open question and gained the base type table a field.

### Why goals moved up

The original ordering had goals fifth. The dispatch pass found that `Mech_AiTick`'s only caller is `Group_OrderTick` — **a machine that is not a live member of a mission group never thinks** — which makes the group order layer structurally upstream of every other slice rather than a peer of them. Nothing else can be observed running in the engine until it exists, and the targeting port has already had to stand up `MissionGroup.cs` to get that far.

### What slice 5b corrected

The three inert states existed in the table and nothing installed them, so a machine kept fighting after losing its legs or its cockpit. Fixing that pulled in four things outside the AI:

- **`Mech_LocomotionTick` owns the consequences of being immobilised**, not the AI. It zeroes throttle and steer, skips obstacle avoidance, and plays the machine's death animation — a chassis' one non-cyclic sequence, which the engine had been flattening into a looping one. [`mech-locomotion.md`](../simulation/mech-locomotion.md#going-down), [`dts-node-posing.md`](../formats/dts-node-posing.md#cyclic-and-one-shot-sequences).
- **The defeat action fires once per machine, not once per way of stopping it** — the death branch samples the immobilised flag *before* its own recursive finish-off and skips the action if the legs already fired it.
- **`script.dat` block 7 `+0x84` is a starting condition**, and under 20% the mission places a machine as a wreck. [`damage-system.md`](../simulation/damage-system.md#starting-condition--mech_applystartingcondition-004178e8).
- **Descriptor `+0x3c` is a display string index**, not a behaviour parameter: the F7 comm box's `OBJECTIVE:` line.

Two method failures worth not repeating, both of which produced a confidently wrong "this is never used":

- **A scalar search for a field offset is defeated by a rebased base pointer.** `Mech_LocomotionTick` and `Mech_PlaceLegsOnGround` both hold `typeRecord + 2` in a register, so `+0x46` is read as `[ESI + 0x44]` and a search for `0x46` comes back clean. Read the prologue for `ADD reg, k` and search `displacement - k` too.
- **Sweeping one setter's call sites is not enumerating what sets a field.** `AnimThread_SetSequence` has no call site passing the death sequence; `AnimThread_SetTarget` (`00479570`) does. Both existing C# doc comments on `MechTypeRecord.DeathSequence` and `MechObject.Collapsed` already said so, and were not consulted.

## Leads left behind

- **`mech+0xb3`**, raised by `Mech_ApplyStartingCondition` on its two worst grades and read nowhere traced, and **`obj+0x38`**, a byte set when a chassis that leaves no wreck is sunk. Both left out of the port rather than guessed at.
- **`Mech_CreditNeutralisedTarget` (`00415710`) has no derived signature.** Ghidra renders it `__thiscall` with a leading parameter the vtable call sites do not support; the argument list needs reading off the disassembly before the prototype can be recorded.
- **The group-report cluster** at `00412f90` and `00413280`, and the visibility helpers around them (`00412ef4`, `00412d90`, `00412f5c`, `00412f28`, `00413950`, `004137b4`, `00413a08`, `00413920`, `00412d4c`). They read the same order records the AI does but produce string indices and write into a global variable table, so they read as the mission-objective and status-report layer. Not an `ai-*.md` subject; they want a doc of their own.
- **Order `+0x02` and `+0x04`** — resolved at load, no reader found. Listed as an open question in `ai-goals.md`. Group `+0x1c`/`+0x30` are answered: they are the group's own mission-variable slots, run by `Group_ReportIfAllOutOfAction` (`00423f30`) once every member is out of the fight.
- **`mech+0x5d`**, written zero by the circling step and read nowhere, and **`mech+0x9e`**, set by `Sim_RaycastObjectList` when a shot reaches the shooter's own target. Listed as open questions in `ai-combat-states.md`.
- **Most of the shipped mission's AI machines start out of the world**, in groups awaiting deployment, so only one group exercises the AI until the first mission action fires. Each wave that arrives puts more of them under a think — see [`mission-deployment.md`](../simulation/mission-deployment.md).

## Working method

1. Reverse-engineer to the topic doc. Add every decoded function to `known_symbols.json` and run `ES2ApplySymbolNames` — the writeup is not the end of the job.
2. Fix any status line elsewhere in `docs/` that the slice falsifies. "Unported" and "not understood" go stale silently.
3. Review, then port.
4. Verify behaviourally, in the sim, against retail. There is no byte-exact oracle for AI, which is the whole reason for slicing.

`py tools/scripts/doc_lint.py <files>` after doc edits; handoff docs like this one are exempt.
