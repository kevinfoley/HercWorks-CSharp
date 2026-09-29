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
- `alert-panels.md` (created during the review)
- `ai-dispatch.md`
- `ai-navigation.md`
- `ai-targeting.md`
- `component-damage.md`
- `damage-system.md`
- `dbsim-physics-notes.md`
- `hit-detection.md`
- `mech-locomotion.md`
- `mission-deployment.md`
- `mission-objectives.md`
- `preferences.md`
- `projectiles.md`
- `razor-flight.md`
- `weapon-firing.md`
- `weapon-mounts.md`

These `formats/` docs were also created during the review and need no pass either: `bases-dat.md`, `collision-spheres.md`, `dmg-damage-file.md`, `flight-model-fm.md`, `gun-layout-gl.md`.
