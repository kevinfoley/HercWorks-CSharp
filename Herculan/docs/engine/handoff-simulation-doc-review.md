# Handoff: reviewing `docs/simulation/`

A review pass over the documents in [`docs/simulation/`](../simulation/). The main focus is **misplaced information**, meaning content that belongs in a different document. Contradictions and outdated statements are the secondary focus. Any document in the folder that is not listed under [Already reviewed](#already-reviewed) still needs its pass.

## Process

The coordinating session does not edit docs itself. It runs **Sonnet subagents, at most five at a time, one document each**, and starts a new one as each finishes. Documents that link to each other heavily (for example the AI docs, or the damage docs) should not be in flight at the same time.

Each subagent's prompt tells it to:

- Read `CLAUDE.md` in full; its documentation rules are binding. Then read its own document in full, and list `docs/simulation/`, `docs/formats/`, `docs/shell/`, `docs/engine/` and the top-level docs so it knows where each topic lives.
- Move misplaced content to the doc that owns the topic:
  - another simulation doc;
  - `formats/` for file and struct layouts (create a new formats doc when none covers the file);
  - `shell/` for VSHELL behaviour;
  - C# doc comments or the engine docs for engine-specific content;
  - `KNOWN_ISSUES.md` for retail bugs and engine divergences.
- Read the destination doc in full before adding to it. Merge instead of duplicating, leave a link behind, and repoint inbound links (docs and `.cs` comments) whenever a heading moves or is renamed.
- Settle a contradiction from the evidence (`es2_xref.py`, `es2_fieldscan.py`, the decompile, retail data files) where it can. Where it can't, add an `**Open:**` item. Grep `src/` before trusting any "Unported" claim.
- Handle concurrency safely, because other agents are editing at the same time:
  - change files with the Edit tool only, never Write over an existing file;
  - re-read a file and retry when an edit fails because the file changed;
  - never revert changes it did not make;
  - never run a git command that changes state.
- Edit other docs only as far as a move or fix requires, and report their other problems instead.
- Run `doc_lint.py` on every file it touched and `doc_links.py --code --quiet` at the end.
- Finish with a short report under five headings: Moved, Fixed, Open / not fixed, Other docs, Files touched.

The coordinator keeps a list of the "Other docs" findings from each report. When a document on that list comes up, it pastes those findings into that document's prompt, along with any edits earlier agents made to it. When a move lands in a document whose agent is still running, it tells that agent with SendMessage. At the end, it runs `doc_lint.py`, `doc_links.py --code` and both solution builds. Engine bugs that reviewers find go to the user, not into a code change.

## Already reviewed

- `ai-combat-states.md`
- `ai-flyers.md`
- `ai-goals.md`
- `ai-squadmates.md`
- `ai-weapons.md`
- `alert-panels.md` (created during the review)
- `ai-dispatch.md`
- `ai-navigation.md`
- `ai-targeting.md`
- `beam-visuals.md`
- `component-damage.md`
- `damage-system.md`
- `dbsim-physics-notes.md`
- `external-views.md`
- `hit-detection.md`
- `impact-effects.md`
- `mech-locomotion.md`
- `missile-lock.md`
- `mission-deployment.md`
- `mission-objectives.md`
- `preferences.md`
- `projectiles.md`
- `razor-flight.md`
- `target-selection.md`
- `torso-aim.md`
- `weapon-firing.md`
- `weapon-mounts.md`

These `formats/` docs were also created during the review and need no pass either: `bases-dat.md`, `beam-dat.md`, `collision-spheres.md`, `dmg-damage-file.md`, `explos-dat.md`, `flight-model-fm.md`, `gun-layout-gl.md`.

## Findings waiting for their document

Paste these into the prompt when the document comes up.

- `equipment-pods.md`: "Ported in `Sim.MechPods`" after the slot table breaks rule 9. The doc gained a "Where a pod reads its damage" section from `target-selection.md`.
- `structure-behaviour.md`, "What a structure is aimed at": it calls vtable `+0x30` "the accessor every shooter in the game calls". Homing weapons, the HUD indicator and line of sight use `+0x24`; `+0x30` is the camera mount and turret lead point. Narrow it.
- `sim-object-layout.md` (around line 98): the seven short mech timers `+0x258`–`+0x26c` include the missile-lock timers and the ECM roll timer at `+0x267`; name them.
- `rockets.md`: two earlier passes edited it. The spoofing-wobble bullet now links `missile-lock.md#ecm`. The `Rocket_Fire` lock gate is now keyed on `mech+0xa3` clear, which is separate from `Ai_ChooseWeapon`'s scoring bypass for subtype 3 and class 5.

These are problems in docs that were already reviewed or are outside `simulation/`. They still need fixing:

- `ai-goals.md`, "A group with no order at all": "twelve shipped `script.dat` handoffs" should be ten. Its claim that `Mech_AiSelectBehaviour` reads `EDX` is unverified; the flyer twin reads `ECX`.
- `ai-dispatch.md`, dwell-time bullet for `Mech_ReceiveSquadOrder (00420d53)`: the dwell zeroing also requires the machine to be committed (`+0x09`), and the address is unchecked.
- `weapon-firing.md`, "The shot record": the shot record has armor at `+0x04` and shield at `+0x06`, while `PROJ.DAT` has them the other way round. The doc does not say so.
- `mission-deployment.md`, worked example: "8 Cybrid HERCs in six groups" needs checking against the retail script.
- `shell/mfd-scanner.md`, Buttons table: "9 TARGET shares its case with SELECT". Cases 7 and 9 share code, but only the second mode calls `TargetSelect_Cycle`.
- `command-line.md`, the developer-keys table and Open: stale `DAT_004d2708`, `004d25a0`, `004d2572`, `FUN_0045df18`, `FUN_00401c74`.
- `formats/cockpit-views.md`: "Engine:" lines and C# type names in a retail doc (rule 9).
- `formats/dts-texture-binding.md`, "Poly types and their colour mechanisms" and the TSTexture4Poly section: they say `Raster_SetupTexturedSpan` (`00468078`) where the decompile shows `Raster_DrawPolygon` (`00468310`).
- `shell/handoff-vshell-re.md:134`: broken anchor `#next-put-something-on-screen`. `shell/mission-map.md:58`: missing image `Reference/Managment_Mission_Briefing.png`.
- C# comments that still cite `FUN_`/`DAT_` names now symbolised: `Render/ViewCamera.cs`, `ExternalViewChain.cs`, `PlayerTrail.cs`, `ExternalViewLayout.cs`, `Overlay2DRenderer.cs`, `Camera.cs`, `Sim/MechObject.cs`, `Host/Program.cs`. `Scene/SceneModelLibrary.cs:812` calls the descriptor `+0x12` flag unexplained; `dts-texture-binding.md` now explains it.
- `known_symbols.json`:
  - Dated `source` strings on `DBSim_BuildGroupRecord`, `Rocket_Fire` and the `impact-effects.md` entries.
  - The `Poly_ProjectIndexedVertices` entry repeats the false claim that the `.DTS` renderers use it.
  - `ShapeInst_EvalAllNodeLocals` says its ordering is "not independently confirmed".
  - `Mech_ComponentNearestAim` cites `ai-weapons.md`, which never names it.
  - Unnamed functions described from their bodies: `FUN_0041a6d0` and `FUN_0041a994` (servo-sound test helpers), `FUN_004327ac` (cockpit per-frame update), `FUN_00426aec` (30000/60000 contact range), `FUN_0042da08` (floods outside the 3D rect with colour 19).
- `handoff-ai-claim-audit.md`: statuses for `ai-goals.md`, `ai-squadmates.md`, the flyer `ram/guard/follow` claim and the aspect question are stale.
