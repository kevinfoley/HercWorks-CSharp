# HERCULAN / HercWorks

Reimplementation of Earthsiege 2 (1996) in C#, reverse-engineered from the retail `DBSIM.EXE` and
`VSHELL.EXE`. `HercWorks.*` is the data-file toolkit; `Herculan.Engine` is the game engine.

- `Herculan/docs/formats/` — file formats
- `Herculan/docs/simulation/` — simulation behaviour
- `Herculan/docs/engine/planning.md` — architecture decisions and their rationale
- `Herculan/KNOWN_ISSUES.md` — retail bugs, and where this engine diverges from retail

Build: `dotnet build Herculan/HerculanEngine.sln` (engine) and `Herculan/HercWorksMDK.sln` (toolkit).
Tests: `dotnet test Herculan/HerculanEngine.sln`. Keep both at 0 warnings.

## Documentation rules

The docs state what is true now. How the project got there belongs in `git log`.

1. **Never narrate a correction.** No "previously stated", "an earlier pass of this doc", "corrected
   2026-08-21", "this supersedes", "now disproved", "SOLVED". When a finding invalidates existing
   text, edit that text; put what changed in the commit message.

2. **Re-read the whole doc before adding to it.** If what you are about to write contradicts a
   passage already there, fix that passage. Never let both stand.

3. **A doc-maintenance diff should usually contain deletions.** Purely additive means journal.

4. **No dates**, including "solved on" headers and status lines.

5. **One fact, one home.** The doc owns the RE evidence and derivation; the code comment owns the
   constants, the local behaviour, and a link to the doc section. Never both.

6. **Keep a disproven reading only when it protects a reader** — could someone reach that wrong
   conclusion independently (a symbol still misnamed in Ghidra, an obvious-but-false
   interpretation)? Then keep it forward-looking ("the obvious reading is X; it is actually Y
   because Z") in that doc's **Rejected readings** table. "This doc used to say X" fails the test.

7. **Verify status claims before repeating them.** "Not ported" and "unresolved" go stale silently;
   grep the named type first.

`tools/scripts/doc_lint.py` enforces 1, 4 and 6, and runs automatically after any edit under
`Herculan/docs/`. `/doc-lint` runs it over the whole set. It cannot catch 2, 3, 5 or 7.

Handoff docs (`docs/engine/handoff-*.md`) are exempt: ephemeral scratchpads, never authoritative for
status. Drain them into topic docs and delete what you moved.

## Reverse-engineering conventions

- Addresses are virtual addresses in the named binary, bare hex (`0046e87c`). Name the binary when it
  is not DBSIM.
- Cite the original symbol alongside the port: `Mech_LocomotionTick (00416a04)`.
- Say plainly when something is this engine's invention rather than read from the binary, so it is
  not later mistaken for vanilla behaviour.
