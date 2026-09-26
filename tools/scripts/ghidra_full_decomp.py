#!/usr/bin/env python3
"""Regenerate the whole-program decompilation dumps and report how much of DBSIM is named.

Runs `ES2DumpFullDecomp` headless against the ES2Recon project, once per binary, writing
`tools/analysis_out/<BINARY>_decomp_full.c`. The two runs are sequential because headless Ghidra
locks the project. Each dump is written to a temporary file and moved into place only when the
script reports `SCRIPT-OK`, so a failed or cancelled run leaves the previous dump intact.

It then counts the DBSIM functions whose name in the dump is the one `known_symbols.json` records
for that address -- the names this project assigned, as opposed to `FUN_` placeholders and the
names Ghidra supplies itself (Borland runtime functions, Win32 import thunks, `entry`).

Usage:
    python tools/scripts/ghidra_full_decomp.py              # dump both binaries, then count
    python tools/scripts/ghidra_full_decomp.py --no-dump    # count from the existing DBSIM dump
    python tools/scripts/ghidra_full_decomp.py --binary DBSIM
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GHIDRA = os.path.join(REPO_ROOT, "tools", "ghidra_12.1.2_PUBLIC", "support", "analyzeHeadless.bat")
PROJECT = os.path.join(REPO_ROOT, "tools", "ghidra_project")
SCRIPTS = os.path.join(REPO_ROOT, "tools", "ghidra_scripts")
OUT_DIR = os.path.join(REPO_ROOT, "tools", "analysis_out")
KNOWN_SYMBOLS = os.path.join(SCRIPTS, "known_symbols.json")

BINARIES = ["DBSIM", "VSHELL"]
TIMEOUT_SECONDS = 60  # per function, passed through to ES2DumpFullDecomp

# The banner ES2DumpFullDecomp writes above each function: "   NAME @ ADDR  [thunk]".
HEADER = re.compile(r"^   (\S+) @ ([0-9a-fA-F]{8})(.*)$")


def dump_path(binary: str) -> str:
    return os.path.join(OUT_DIR, f"{binary}_decomp_full.c")


def run_dump(binary: str) -> bool:
    final = dump_path(binary)
    tmp = final + ".tmp"
    cmd = [GHIDRA, PROJECT, "ES2Recon", "-process", f"{binary}.EXE", "-noanalysis",
           "-scriptPath", SCRIPTS, "-postScript", "ES2DumpFullDecomp", tmp, str(TIMEOUT_SECONDS)]
    print(f"=== {binary}: decompiling (several minutes)", flush=True)
    ok = False
    with subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                          text=True, errors="replace") as proc:
        for line in proc.stdout:
            if "SCRIPT-" in line or "progress:" in line or "ERROR" in line:
                print("   " + line.strip(), flush=True)
            if "SCRIPT-OK" in line:
                ok = True
    if ok and proc.returncode == 0 and os.path.exists(tmp):
        os.replace(tmp, final)
        print(f"   wrote {os.path.relpath(final, REPO_ROOT)}")
        return True
    if os.path.exists(tmp):
        os.remove(tmp)
    print(f"   FAILED (exit {proc.returncode}); {os.path.relpath(final, REPO_ROOT)} left unchanged")
    return False


def read_functions(path: str) -> dict[str, tuple[str, bool]]:
    """Address -> (name, is_external) for every function banner in a dump."""
    functions = {}
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            m = HEADER.match(line)
            if m:
                functions[m.group(2).lower()] = (m.group(1), "[external]" in m.group(3))
    return functions


def report_names(binary: str) -> None:
    path = dump_path(binary)
    if not os.path.exists(path):
        sys.exit(f"{os.path.relpath(path, REPO_ROOT)} does not exist; run without --no-dump first")

    with open(KNOWN_SYMBOLS, encoding="utf-8-sig") as f:
        entries = json.load(f)["entries"]
    known = {e["address"].lower(): e["name"] for e in entries
             if e.get("binary") == binary and e.get("type") == "function" and "name" in e}

    functions = {a: n for a, (n, ext) in read_functions(path).items() if not ext}
    manual = [a for a, n in functions.items() if known.get(a) == n]
    placeholder = [a for a, n in functions.items() if n.startswith("FUN_")]
    other = len(functions) - len(manual) - len(placeholder)
    renamed_since = [a for a, n in functions.items() if a in known and known[a] != n]
    no_function = sorted(a for a in known if a not in functions)

    total = len(functions)
    game = total - other  # excludes Ghidra's own library and import-thunk names
    print(f"\n=== {binary} function names ({os.path.relpath(path, REPO_ROOT)})")
    print(f"   functions in the database   {total:5}")
    print(f"   manually named              {len(manual):5}  {100 * len(manual) / total:5.1f}% of all,"
          f" {100 * len(manual) / game:5.1f}% excluding library/import names")
    print(f"   FUN_ placeholders           {len(placeholder):5}")
    print(f"   library/import/entry names  {other:5}")
    if renamed_since:
        print(f"   {len(renamed_since)} known_symbols.json names differ from the database"
              " (run ES2ApplySymbolNames): " + " ".join(sorted(renamed_since)))
    if no_function:
        print(f"   {len(no_function)} known_symbols.json names have no function at their address"
              " in the database: " + " ".join(f"{a} ({known[a]})" for a in no_function))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--no-dump", action="store_true", help="skip Ghidra; count from the existing dump")
    parser.add_argument("--binary", choices=BINARIES, action="append",
                        help="dump only this binary (repeatable; default both)")
    args = parser.parse_args()

    ok = True
    if not args.no_dump:
        for binary in args.binary or BINARIES:
            ok = run_dump(binary) and ok
    report_names("DBSIM")
    report_names("VSHELL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
