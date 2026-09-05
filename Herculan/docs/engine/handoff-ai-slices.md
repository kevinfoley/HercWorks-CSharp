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
| 1 | **Targeting** | `Ai_SelectTarget` `00411fa0`, `Mech_AiOnTakingFire` `0041f7b8`, `Mech_AiCombatReassess` `0041cf18`, `Ai_ShouldAbandonTarget` `0041c4a8` | Doc written; ported. `MissionGroup.cs` stands up the group record the AI ticks from, with the order array stubbed as null |
| 2 | **Goals** | `Group_OrderTick` `00423a74`, the order array at `group+0x44` indexed by `group+0x6c` | Started ahead of plan — see below |
| 3 | **Navigation** | `Mech_AiObstacleAvoidance` `00416274`, the travel and patrol think functions (`0041d7d0`, `0041d9cc`, `0041daac`, `0041d60c`) | Not started |
| 4 | **Weapons** | The `mech+0xb5` selection-suppression flag; the fire path in each combat think function | Not started |
| 5 | **Behaviour states** | The remaining think functions — `flanking`, `facing off`, `skirting`, `guarding`, `driving off en`, `fleeing` — and the 30 `Behaviour_SetState` call sites as the transition graph | Not started |
| 6 | **Squadmates** | `mech+0x23e` standing orders, `FUN_0041c0f4`, `Mech_ApplyFormationOffset` `00417898` | Not started |
| 7 | **Flyer AI** | The flyer behaviour path; no retail mission places an AI RAZOR, so verification is synthetic | Not started |

### Why goals moved up

The original ordering had goals fifth. The dispatch pass found that `Mech_AiTick`'s only caller is `Group_OrderTick` — **a machine that is not a live member of a mission group never thinks** — which makes the group order layer structurally upstream of every other slice rather than a peer of them. Nothing else can be observed running in the engine until it exists, and the targeting port has already had to stand up `MissionGroup.cs` to get that far.

## Working method

1. Reverse-engineer to the topic doc. Add every decoded function to `known_symbols.json` and run `ES2ApplySymbolNames` — the writeup is not the end of the job.
2. Fix any status line elsewhere in `docs/` that the slice falsifies. "Unported" and "not understood" go stale silently.
3. Review, then port.
4. Verify behaviourally, in the sim, against retail. There is no byte-exact oracle for AI, which is the whole reason for slicing.

`py tools/scripts/doc_lint.py <files>` after doc edits; handoff docs like this one are exempt.
