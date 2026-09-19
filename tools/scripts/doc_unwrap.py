#!/usr/bin/env python3
"""Join manually word-wrapped markdown paragraphs into single logical lines.

The docs are edited in an IDE that soft-wraps for display; a hard line wrap
in the source just makes the paragraph harder to edit later. This collapses
each paragraph (and each list item's own continuation lines) back into one
line, leaving table rows, fenced code blocks, headings and list-item
boundaries alone. A markdown hard break (a line ending in two spaces) is
preserved as a break, not joined.

Usage:
    python tools/scripts/doc_unwrap.py                  # unwrap the default doc set
    python tools/scripts/doc_unwrap.py PATH [PATH ...]  # unwrap specific files/dirs
"""

from __future__ import annotations

import os
import re
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_TARGETS = [os.path.join("Herculan", "docs")]

FENCE_RE = re.compile(r'^\s*(```|~~~)')
TABLE_ROW_RE = re.compile(r'^\s*\|')
HEADING_RE = re.compile(r'^\s{0,3}#{1,6}(\s|$)')
LIST_ITEM_RE = re.compile(r'^(\s*)([-*+]|\d+[.)])\s+')
HR_RE = re.compile(r'^\s*([-*_])(\s*\1){2,}\s*$')
BLANK_RE = re.compile(r'^\s*$')
HARD_BREAK_RE = re.compile(r'  $')


def unwrap(text: str) -> str:
    lines = text.splitlines()
    out: list[str] = []
    buf: list[str] = []
    in_fence = False

    def flush():
        if buf:
            out.append(' '.join(s.strip() for s in buf))
            buf.clear()

    for raw in lines:
        line = raw.rstrip('\n')

        if in_fence:
            out.append(line)
            if FENCE_RE.match(line):
                in_fence = False
            continue

        if FENCE_RE.match(line):
            flush()
            out.append(line)
            in_fence = True
            continue

        if BLANK_RE.match(line):
            flush()
            out.append('')
            continue

        if TABLE_ROW_RE.match(line) or HEADING_RE.match(line) or HR_RE.match(line):
            flush()
            out.append(line.rstrip())
            continue

        if LIST_ITEM_RE.match(line):
            flush()
            buf.append(line)
            if HARD_BREAK_RE.search(raw):
                flush()
            continue

        # continuation line: join onto whatever paragraph/list item is open
        buf.append(line)
        if HARD_BREAK_RE.search(raw):
            flush()

    flush()
    return '\n'.join(out) + '\n'


def iter_markdown_files(targets: list[str]) -> list[str]:
    files = []
    for target in targets:
        path = target if os.path.isabs(target) else os.path.join(REPO_ROOT, target)
        if os.path.isdir(path):
            for dirpath, _dirnames, filenames in os.walk(path):
                for name in filenames:
                    if name.endswith('.md'):
                        files.append(os.path.join(dirpath, name))
        elif os.path.isfile(path):
            files.append(path)
        else:
            print(f"doc-unwrap: no such file or directory: {target}", file=sys.stderr)
            sys.exit(1)
    return sorted(set(files))


def main():
    targets = sys.argv[1:] or DEFAULT_TARGETS
    files = iter_markdown_files(targets)

    changed = []
    for path in files:
        with open(path, encoding='utf-8') as f:
            orig = f.read()
        new = unwrap(orig)
        if new != orig:
            with open(path, 'w', encoding='utf-8', newline='\n') as f:
                f.write(new)
            changed.append(os.path.relpath(path, REPO_ROOT))

    print(f"doc-unwrap: {len(files)} file(s) checked, {len(changed)} rewrapped")
    for rel in changed:
        print(f"  {rel}")


if __name__ == '__main__':
    main()
