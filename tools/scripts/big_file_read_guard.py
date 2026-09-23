#!/usr/bin/env python3
"""PreToolUse hook: reject Bash commands that dump a large file to stdout.

Reading a big file with `cat`, or in ranges with `sed -n`, costs far more
context than the Read tool, which is paginated and de-duplicated. This hook
denies only the cases where Read is strictly better, and stays out of the way
of greps, pipelines, redirections and heredocs.

Wire up in .claude/settings.json:

    "PreToolUse": [
      { "matcher": "Bash",
        "hooks": [ { "type": "command",
                     "command": "python \"${CLAUDE_PROJECT_DIR:-.}/tools/scripts/big_file_read_guard.py\" --hook" } ] }
    ]
"""

from __future__ import annotations

import json
import os
import re
import shlex
import sys

# Files at or above this size are worth the Read tool's pagination.
SIZE_LIMIT = 20 * 1024

# A head/tail asking for this many lines is a whole-file read in disguise.
LINE_LIMIT = 200

READERS = {"cat", "bat", "more", "less", "sed", "head", "tail"}

# `;` `&&` `||` and newlines separate statements; `|` separates pipeline stages.
STATEMENT_SPLIT = re.compile(r"(?:&&|\|\||;|\n)")


def statements(command: str) -> list[str]:
    return [s.strip() for s in STATEMENT_SPLIT.split(command) if s.strip()]


def last_stage(statement: str) -> str:
    """The stage whose stdout reaches the transcript.

    `cat big | head -5` is cheap: head bounds the output. Only the final stage
    of a pipeline can flood the context.
    """
    # Split on `|` that is not part of `||` (already consumed) — plain split is
    # safe here because statements() removed `||`.
    return statement.split("|")[-1].strip()


def tokens(stage: str) -> list[str] | None:
    try:
        return shlex.split(stage, posix=True)
    except ValueError:
        return None


def file_operands(args: list[str]) -> list[str]:
    return [a for a in args if not a.startswith("-")]


def numeric_flag(args: list[str], short: str) -> int | None:
    """Value of `-n N`, `-nN`, `--lines=N`, or a bare `-N`."""
    for i, a in enumerate(args):
        if a == short and i + 1 < len(args) and args[i + 1].isdigit():
            return int(args[i + 1])
        if a.startswith(short) and a[len(short):].isdigit():
            return int(a[len(short):])
        if a.startswith("--lines="):
            tail = a.split("=", 1)[1]
            if tail.isdigit():
                return int(tail)
        if re.fullmatch(r"-\d+", a):
            return int(a[1:])
    return None


def sed_range_span(args: list[str]) -> int | None:
    """Line span of a `sed -n 'A,Bp'` script, or None if it is not a range print."""
    if "-n" not in args and not any(a.startswith("-n") for a in args):
        return None
    for a in args:
        m = re.fullmatch(r"(\d+),(\d+)p", a.strip("'\""))
        if m:
            return int(m.group(2)) - int(m.group(1)) + 1
    return None


def offending_file(stage: str, cwd: str) -> tuple[str, int, str] | None:
    """Return (path, size, why) when this stage is a costly whole-file dump."""
    if ">" in stage or "<" in stage:
        return None  # redirection or heredoc; not a read into the transcript

    args = tokens(stage)
    if not args:
        return None

    cmd = os.path.basename(args[0])
    if cmd not in READERS:
        return None

    rest = args[1:]
    operands = file_operands(rest)
    why = ""

    if cmd in ("cat", "bat", "more", "less"):
        why = f"`{cmd}` prints the whole file"
    elif cmd == "sed":
        span = sed_range_span(rest)
        if span is None or span < LINE_LIMIT:
            return None
        # The script itself is an operand; drop it.
        operands = [o for o in operands if not re.fullmatch(r"'?\d+,\d+p'?", o)]
        why = f"`sed -n` is reading a {span}-line range"
    elif cmd in ("head", "tail"):
        count = numeric_flag(rest, "-n")
        if count is None or count < LINE_LIMIT:
            return None
        why = f"`{cmd} -n {count}` is a whole-file read in disguise"

    for operand in operands:
        path = operand if os.path.isabs(operand) else os.path.join(cwd, operand)
        try:
            size = os.path.getsize(path)
        except OSError:
            continue
        if size >= SIZE_LIMIT:
            return operand, size, why
    return None


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # never break the session over a malformed payload

    if payload.get("tool_name") != "Bash":
        return 0

    command = payload.get("tool_input", {}).get("command", "")
    cwd = payload.get("cwd") or os.getcwd()

    for statement in statements(command):
        hit = offending_file(last_stage(statement), cwd)
        if hit is None:
            continue
        path, size, why = hit
        reason = (
            f"{path} is {size // 1024} KB and {why}. Use the Read tool instead: it "
            "paginates, reports line numbers, and is not re-sent when the same file "
            "is read again. Bash is still the right tool for grep, and for ranges of "
            "a file you have already read."
        )
        json.dump(
            {
                "hookSpecificOutput": {
                    "hookEventName": "PreToolUse",
                    "permissionDecision": "deny",
                    "permissionDecisionReason": reason,
                }
            },
            sys.stdout,
        )
        return 0

    return 0


if __name__ == "__main__":
    sys.exit(main())
