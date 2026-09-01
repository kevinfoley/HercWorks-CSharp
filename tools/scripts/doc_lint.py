#!/usr/bin/env python3
"""Flag journal-style writing and append-only rot in reference documentation.

Reference docs state what is true now. They are not a record of how the project
arrived there — that is what git log is for. This linter catches the phrasings
that show a correction was narrated in place instead of applied.

Usage:
    python tools/scripts/doc_lint.py                  # lint the default doc set
    python tools/scripts/doc_lint.py PATH [PATH ...]  # lint specific files/dirs
    python tools/scripts/doc_lint.py --staged         # lint staged files only
    python tools/scripts/doc_lint.py --code           # also lint C# doc comments

Exit status is 1 when anything is flagged, so it works as a pre-commit hook.

A finding is not automatically wrong. A warning that protects a future reader
from re-deriving a disproven answer is worth keeping — but it belongs in a
"Rejected readings" table, phrased forward-looking ("the obvious reading is X;
it is actually Y"), not as a history of what this document used to say. To
silence a line deliberately, put `<!-- doc-lint: ok -->` at the end of it.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_TARGETS = [os.path.join("Herculan", "docs")]

# Files that are allowed to read as a log. Handoffs are explicitly ephemeral
# scratchpads, and the review reports are records of a point in time.
EXEMPT_BASENAMES = {
    "DOCS_REVIEW.md",
    "DOCS_REVIEW_REMAINING.md",
}
EXEMPT_PATTERNS = [
    re.compile(r"handoff[-_]", re.IGNORECASE),
]

SUPPRESS = re.compile(r"<!--\s*doc-lint:\s*ok\s*-->|doc-lint:\s*ok")

# The sanctioned home for a disproven reading. Its header necessarily contains
# "wrong", so exempt the table furniture rather than making authors suppress it.
ALLOWED = re.compile(
    r"^\s*\|\s*Reading\s*\|\s*Why it is wrong\s*\|\s*$"
    r"|^\s*\|[\s:|-]+\|\s*$",
)

# (id, severity, compiled pattern, why it is flagged)
RULES: list[tuple[str, str, re.Pattern[str], str]] = [
    (
        "dated-claim", "error",
        re.compile(r"\b20\d{2}-[01]\d-[0-3]\d\b"),
        "A date in a reference doc records when you learned something, not what is true. "
        "Put it in the commit message.",
    ),
    (
        "self-history", "error",
        re.compile(
            r"\b(?:previously\s+(?:stated|documented|listed|recorded|described|said|thought|assumed|called)"
            r"|an?\s+earlier\s+(?:pass|read|reading|note|revision|version|draft)"
            r"|earlier\s+(?:passes|readings|notes|revisions)\s+of\s+this"
            r"|this\s+(?:doc|document|file|section|class|comment)\s+(?:previously|used\s+to|once)"
            r"|used\s+to\s+(?:say|state|claim|call|read|assume|record))\b",
            re.IGNORECASE,
        ),
        "Narrates what this document used to say. Nobody reading now saw that version — state the "
        "current fact and delete the history.",
    ),
    (
        "supersede", "error",
        re.compile(
            r"\b(?:this\s+supersedes|supersed(?:es|ing)\s+(?:what|the\s+earlier)"
            r"|now\s+disproved|no\s+longer\s+a\s+guess|corrects\s+this\s+doc"
            r"|retroactively\s+confirms)\b",
            re.IGNORECASE,
        ),
        "Frames the text as a correction to a previous version rather than as a statement of fact.",
    ),
    (
        "was-wrong", "error",
        re.compile(
            r"\b(?:(?:was|were|is)\s+(?:all\s+)?wrong"
            r"|got\s+(?:it\s+)?backwards"
            r"|had\s+the\s+sense\s+inverted"
            r"|turned\s+out\s+(?:to\s+be|not)"
            r"|why\s+this\s+wasn'?t\s+obvious)\b",
            re.IGNORECASE,
        ),
        "A record of a wrong turn. Keep it only as a forward-looking caution in a "
        "'Rejected readings' table.",
    ),
    (
        "solved-banner", "error",
        re.compile(r"^\s{0,3}#{1,6}\s.*\b(?:SOLVED|ANSWERED|FOUND|CONFIRMED)\b"),
        "Heading announces a discovery event. Name the subject instead — a reader wants to know "
        "what the section covers, not that you closed it.",
    ),
    (
        "question-heading", "warn",
        re.compile(r"^\s{0,3}#{1,6}\s.*\bQuestion\s+\d+\b", re.IGNORECASE),
        "Heading is numbered against an investigation, not a subject.",
    ),
    (
        "changelog-lede", "warn",
        re.compile(
            r"^\s*(?:\*\*)?(?:Since|Where\s+this\s+left\s+off|Closed\s+with\s+(?:it|them)"
            r"|Corrected\s+on\s+the\s+way|Not\s+built|Built\s+and\s+shipped)\b(?:\*\*)?\s*:",
            re.IGNORECASE,
        ),
        "Reads as a session changelog entry.",
    ),
    (
        "self-disclaimer", "warn",
        re.compile(
            r"\b(?:not\s+re-?verified|may\s+be\s+(?:less\s+complete|out\s+of\s+date|stale)"
            r"|check\s+.{0,40}\s+before\s+assuming)\b",
            re.IGNORECASE,
        ),
        "A section that disclaims its own accuracy should be fixed or deleted, not annotated.",
    ),
]

CODE_COMMENT = re.compile(r"^\s*(?:///|//|\*)")


def is_exempt(path: str) -> bool:
    base = os.path.basename(path)
    if base in EXEMPT_BASENAMES:
        return True
    return any(p.search(base) for p in EXEMPT_PATTERNS)


def iter_files(targets: list[str], include_code: bool) -> list[str]:
    exts = {".md"} | ({".cs"} if include_code else set())
    found: list[str] = []
    for target in targets:
        path = target if os.path.isabs(target) else os.path.join(REPO_ROOT, target)
        if os.path.isfile(path):
            if os.path.splitext(path)[1].lower() in exts:
                found.append(path)
            continue
        for root, dirs, files in os.walk(path):
            dirs[:] = [d for d in dirs if d not in {"obj", "bin", ".git", "node_modules"}]
            for name in sorted(files):
                if os.path.splitext(name)[1].lower() in exts:
                    found.append(os.path.join(root, name))
    return [f for f in found if not is_exempt(f)]


def staged_files() -> list[str]:
    out = subprocess.run(
        ["git", "diff", "--cached", "--name-only", "--diff-filter=ACMR"],
        cwd=REPO_ROOT, capture_output=True, text=True, check=False,
    ).stdout.split("\n")
    return [os.path.join(REPO_ROOT, p) for p in out
            if p.strip() and os.path.splitext(p)[1].lower() in {".md", ".cs"}]


def lint_file(path: str, include_code: bool) -> list[tuple[int, str, str, str, str]]:
    is_cs = path.lower().endswith(".cs")
    try:
        with open(path, encoding="utf-8") as fh:
            lines = fh.read().replace("\r\n", "\n").split("\n")
    except (OSError, UnicodeDecodeError):
        return []

    hits = []
    in_fence = False
    for n, line in enumerate(lines, 1):
        stripped = line.strip()
        if not is_cs and (stripped.startswith("```") or stripped.startswith("~~~")):
            in_fence = not in_fence
            continue
        if in_fence or SUPPRESS.search(line) or ALLOWED.match(line):
            continue
        # In C# only look at comments, so identifiers and string literals are ignored.
        if is_cs and not CODE_COMMENT.match(line):
            continue
        for rule_id, severity, pattern, why in RULES:
            # Heading rules are markdown-only.
            if is_cs and rule_id in {"solved-banner", "question-heading"}:
                continue
            m = pattern.search(line)
            if m:
                hits.append((n, rule_id, severity, m.group(0).strip(), why))
    return hits


def hook_mode() -> int:
    """PostToolUse hook: read Claude Code's hook JSON on stdin, lint the edited file.

    Emits the hook's own JSON response so the findings are fed straight back to the
    model that just wrote them, while the edit itself still stands.
    """
    import json

    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0

    tool_input = payload.get("tool_input") or {}
    tool_response = payload.get("tool_response") or {}
    path = tool_response.get("filePath") or tool_input.get("file_path") or ""
    if not path:
        return 0

    norm = path.replace("\\", "/")
    if not norm.lower().endswith(".md") or "/docs/" not in norm.lower():
        return 0
    if is_exempt(path) or not os.path.isfile(path):
        return 0

    hits = lint_file(path, include_code=False)
    if not hits:
        return 0

    rel = os.path.relpath(path, REPO_ROOT).replace("\\", "/")
    lines = [
        f"doc-lint flagged {len(hits)} issue(s) in {rel}. This is a reference document: it states "
        "what is true now, and the change history belongs in the commit message.",
        "",
    ]
    for n, rule_id, _severity, text, why in hits:
        lines.append(f"  {rel}:{n}  [{rule_id}]  \"{text}\"")
        lines.append(f"      {why}")
    lines += [
        "",
        "Fix these now. If a finding is a genuinely load-bearing caution — it stops a reader "
        "reaching a disproven conclusion on their own — keep it, reword it forward-looking "
        "(\"the obvious reading is X; it is actually Y\"), move it into that doc's "
        "'Rejected readings' table, and append <!-- doc-lint: ok --> to the line.",
        "Also check while you are in the file: does a later section now contradict an earlier "
        "one? Correct the earlier text rather than appending to the end.",
    ]

    json.dump({
        "systemMessage": f"doc-lint: {len(hits)} issue(s) in {rel}",
        "hookSpecificOutput": {
            "hookEventName": "PostToolUse",
            "additionalContext": "\n".join(lines),
        },
    }, sys.stdout)
    return 0


def main() -> int:
    if "--hook" in sys.argv:
        return hook_mode()

    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("paths", nargs="*", help="files or directories (default: Herculan/docs)")
    ap.add_argument("--staged", action="store_true", help="lint staged files only")
    ap.add_argument("--code", action="store_true", help="also lint C# doc comments")
    ap.add_argument("--quiet", action="store_true", help="print only the summary")
    args = ap.parse_args()

    if args.staged:
        files = [f for f in staged_files() if not is_exempt(f)]
        if not args.code:
            files = [f for f in files if f.lower().endswith(".md")]
    else:
        files = iter_files(args.paths or DEFAULT_TARGETS, args.code)

    # Windows consoles default to a codepage that cannot render the messages below.
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass

    total = 0
    errors = 0
    dirty = 0
    for path in files:
        hits = lint_file(path, args.code)
        if not hits:
            continue
        dirty += 1
        rel = os.path.relpath(path, REPO_ROOT).replace("\\", "/")
        if not args.quiet:
            print(f"\n{rel}")
        for n, rule_id, severity, text, why in hits:
            total += 1
            if severity == "error":
                errors += 1
            if not args.quiet:
                print(f"  {rel}:{n}  [{severity}: {rule_id}]  \"{text}\"")
                print(f"      {why}")

    if total:
        print(f"\n{total} finding(s) across {dirty} file(s); {errors} error(s).")
        print("Reference docs state what is true now. Put the change history in the commit message.")
        print("A genuinely load-bearing caution belongs in a 'Rejected readings' table, phrased")
        print("forward-looking. To keep a specific line, append: <!-- doc-lint: ok -->")
    else:
        print(f"doc-lint: clean ({len(files)} file(s) checked).")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
