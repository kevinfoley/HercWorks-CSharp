---
description: Check reference docs for journal-style writing and append-only rot, and fix what it finds.
---

Run the documentation linter and repair everything it reports.

```
python tools/scripts/doc_lint.py $ARGUMENTS
```

(No arguments lints `Herculan/docs`. `--staged` lints staged files only. `--code` also covers C# doc
comments. Specific paths may be passed instead.)

## How to fix each finding

The linter finds phrasing, not defects. Read the surrounding text before changing it, then apply the
rule below rather than deleting the matched words in place.

- **dated-claim** — Remove the date. If it was doing real work ("verified against build X"), say
  which build. Otherwise delete it: git records when.
- **self-history** ("previously stated", "an earlier pass") — Delete the clause and leave the current
  fact standing on its own. Nobody reading now saw the old version.
- **supersede** / **was-wrong** — Decide which of two cases applies:
  - The text records that *this document* was wrong → delete it.
  - The text warns a reader off a conclusion they could reach independently — a symbol still misnamed
    in the Ghidra project, a wrong reading still live in code, an obvious-but-false interpretation →
    **keep it**, reworded forward-looking ("the obvious reading is X; it is actually Y because Z"),
    and move it into that doc's **Rejected readings** table. Suppress the line with a trailing
    `<!-- doc-lint: ok -->` once it is phrased that way.
- **solved-banner** — Retitle the heading after its subject, not the discovery event. `## Foo —
  SOLVED` becomes `## Foo`.
- **question-heading** — Retitle by subject. A reader wants to know what the section covers, not
  which investigation question it answered.
- **changelog-lede** ("Since:", "Where this left off") — That belongs in a commit message or a
  handoff scratchpad, not a reference doc.
- **self-disclaimer** ("not re-verified", "may be out of date") — A section that disclaims its own
  accuracy should be fixed or deleted. If it cannot be verified now, say what is unknown and why,
  specifically.

## While you are in the file

The linter catches phrasing; it cannot catch a stale *fact*. When a finding sends you into a
document, also check the two things it misses:

1. **Does a later section contradict an earlier one?** That is the failure this whole ruleset exists
   to prevent. Fix the earlier text, do not append to the later.
2. **Does the doc claim something is unported that the code now does?** Grep the named type before
   trusting the claim.

Re-run the linter when done. It exits non-zero while any error-severity finding remains.
