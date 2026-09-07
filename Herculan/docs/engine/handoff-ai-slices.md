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
| 5 | **Behaviour states** | The remaining think functions — `flanking`, `facing off`, `skirting`, `guarding`, `driving off en`, `fleeing` — and the 30 `Behaviour_SetState` call sites as the transition graph | Not started |
| 6 | **Squadmates** | `mech+0x23e` standing orders, `FUN_0041c0f4`, `Mech_ApplyFormationOffset` `00417898` | Not started |
| 7 | **Flyer AI** | The flyer behaviour path; no retail mission places an AI RAZOR, so verification is synthetic | Not started |

### What slice 3 pulled in

Two things outside the AI turned out to be load-bearing for it.

- **`Terrain_RayWalk`'s mode 1.** The obstacle probes lie flat on the ground, so the thin-ray query grazes on every tick of rolling terrain and pins the steer hard over. Mode 1 is a slope test instead — `Terrain_FaceBlocksMovement` (`0046fe40`) — and the engine had only mode 0. Ported here.
- **`Sim_RaycastShapes` (`00404ca0`).** A standing animated structure's collision radius is zero, so the proximity sweep cannot see one; without the shape half of the probe a machine walks into a building and stands there. The engine has no swept-shape cast, so it stops at the bounding radius.

### What slice 4 corrected

`mech+0x96` is the **radar mode**, not a weapons-free flag, and `Ai_UpdateWeaponsFree` is the AI's radar switch. The mission-file field feeding it (`.MSN` row #12 `+0x08`) is a radar setting. Four docs and the engine's own second copy of the byte were carrying the wrong reading.

It also closed three of `ai-targeting.md`'s "no writer found" entries: `mech+0xa5` is *no weapons left* and `Ai_ChooseWeapon` writes it, `mech+0x26b` is the ARM radar-silence timer and `Mech_DirectFireHitTest` writes it, and `mech+0xb2` is written by the squad command handler. And it traced the gun convergence's consumer, which `weapon-firing.md` had recorded as inert on every retail chassis when it is in fact live on all of them.

### Why goals moved up

The original ordering had goals fifth. The dispatch pass found that `Mech_AiTick`'s only caller is `Group_OrderTick` — **a machine that is not a live member of a mission group never thinks** — which makes the group order layer structurally upstream of every other slice rather than a peer of them. Nothing else can be observed running in the engine until it exists, and the targeting port has already had to stand up `MissionGroup.cs` to get that far.

## Leads left behind

- **The group-report cluster** at `00412f90` and `00413280`, and the visibility helpers around them (`00412ef4`, `00412d90`, `00412f5c`, `00412f28`, `00413950`, `004137b4`, `00413a08`, `00413920`, `00412d4c`). They read the same order records the AI does but produce string indices and write into a global variable table, so they read as the mission-objective and status-report layer. Not an `ai-*.md` subject; they want a doc of their own.
- **Order `+0x02` and `+0x04`, and group `+0x1c`/`+0x30`** — resolved at load, no reader found. Listed as open questions in `ai-goals.md`.
- **`FUN_0041de9c`** is the gate every combat think opens with: `mech+0xad` set stashes the current descriptor in the block scratch and installs `skirting` (14), recording the range to `mech+0x31e`. Decoded in slice 4 but left unnamed — the state it installs is slice 5's.
- **`FUN_0041c72c`** is the circling step `flanking` and `attacking base` share: on the `mech+0x5f`/`mech+0x62` timers it offsets the target's position 15000 units to one side and steers at that instead, latching `mech+0x66` so the machine alternates between circling and squaring up. Decoded in slice 4, ported nowhere.
- **`Ai_LineOfSightBlocked` (`0041dc24`)** is decoded and is what triggers `skirting`. `FUN_0041dbfc` (the circling step's own bookkeeping) and the `skirting` think itself are slice 5's.

## Working method

1. Reverse-engineer to the topic doc. Add every decoded function to `known_symbols.json` and run `ES2ApplySymbolNames` — the writeup is not the end of the job.
2. Fix any status line elsewhere in `docs/` that the slice falsifies. "Unported" and "not understood" go stale silently.
3. Review, then port.
4. Verify behaviourally, in the sim, against retail. There is no byte-exact oracle for AI, which is the whole reason for slicing.

`py tools/scripts/doc_lint.py <files>` after doc edits; handoff docs like this one are exempt.
