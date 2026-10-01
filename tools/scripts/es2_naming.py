#!/usr/bin/env python3
"""Helpers for working down the function-naming backlog in known_symbols.json.

Subcommands (BIN is DBSIM or VSHELL):

  triage BIN [--limit N] [--max-lines L] [--sort named|sites|callers]
      The es2_unnamed_callees backlog with each target's decompile size and its own unnamed
      callee count, so short leaf functions can be taken first. Columns: named callers, direct
      call/branch sites in the whole binary, distinct calling functions (fns). --sort orders by
      named callers (default), by sites, or by fns.
  body BIN addr... [-d] [--full]
      Decompile bodies with known_symbols names substituted for FUN_/DAT_ labels, the header
      banner, blank lines and local declarations dropped (--full keeps them). -d appends the
      disassembly.
  callers BIN addr... [-n CTX] [--max M]
      Every decompile line naming FUN_/DAT_<addr> (or its known name), with the enclosing function.
  precheck BIN addr...
      The known_symbols entry and every Herculan docs/src/tests mention of each address.
  sym BIN regex...
      known_symbols entries whose name or address matches.
  rtti BIN vtable...
      Borland class of a vtable: typeinfo = dword at vt-0xc, size = dword ti+0, name = C string
      at ti + word[ti+6].
  insert batch.json [--write]
      Insert new entries after the '"entries": [' line, validating schema, uniqueness, maybe_ vs
      medium, and control characters. Dry run without --write.
  edit edits.json [--write]
      [{"binary","address","field","value"} or {"binary","address","fields":{...}}]: rewrite that
      one entry's block, all fields of one edit before validating (so a low entry can take a
      confidence and a name together). A name change also renames the old name, or the FUN_/DAT_
      label of a newly named entry, elsewhere in the file (word boundary).
  relink batch.json [--write]
      Rewrite FUN_/DAT_<addr> mentions of the batch's named entries in the descriptions of other
      entries of the same binary (insert does this itself).
  mentions batch.json
      FUN_/DAT_/bare-address mentions of the batch's named addresses in docs/src/tests.
  fixrefs batch.json [--write]
      Rewrite `FUN_a` to `Name` (`a`) in docs and <c>FUN_a</c> / bare FUN_a in C#, and list the
      mentions it cannot rewrite safely (bare addresses, other shapes) for a manual edit.
  repl spec.json [--write]
      [[relpath, old, new, count]]: exact replacements asserting the count; BOM and newlines kept.
  apply BIN [--write]
      Run ES2ApplySymbolNames headless (-readOnly unless --write) and print only the summary,
      errors and warnings.
  fixentry BIN addr[+addr...] [--write]
      Repair functions Ghidra starts past their prologue (es2_late_entries.py finds them): one
      headless session runs ES2MergeFunctionAt <addr> auto for each true entry, then
      ES2ApplyStructures and ES2ApplySymbolNames (the merge drops the old functions' signatures
      and struct-typed parameters), then ES2CheckFunctionEntries. -readOnly unless --write; a
      FAILED or SHORT line in the dry run means do not write. Re-address the known_symbols entry
      first when the name sits on the late address.

Write batch/spec JSON files with the Write tool; heredocs eat backslashes.
"""

from __future__ import annotations

import json
import os
import pickle
import re
import shutil
import struct
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import es2_unnamed_callees as uc  # noqa: E402

REPO = uc.REPO
ANALYSIS = uc.ANALYSIS
SYMBOLS = uc.SYMBOLS
HDR = re.compile(r"^/\* =+\n   (\S+) @ ([0-9a-f]{8})[^\n]*\n   =+ \*/\n", re.M)
FIELDS = ["address", "binary", "type", "confidence", "name", "description", "source", "signature"]
CACHE = os.path.join(tempfile.gettempdir(), "es2_naming_cache")
LABEL = re.compile(r"\b(?:thunk_)?(FUN|DAT|PTR|LAB|_DAT)_([0-9a-f]{8})\b")
DECL = re.compile(r"^  [A-Za-z_][\w ]*?[\w*]+ \**[A-Za-z_]\w*(?:\s*\[\w+\])?;(?:\s*/\*.*\*/)?$")


def entries():
    with open(SYMBOLS, encoding="utf-8-sig") as f:
        return json.load(f)["entries"]


def names(binary):
    return {e["address"].lower(): e["name"] for e in entries() if e["binary"] == binary and e.get("name")}


def bodies(binary):
    src = os.path.join(ANALYSIS, f"{binary}_decomp_full.c")
    os.makedirs(CACHE, exist_ok=True)
    cache = os.path.join(CACHE, f"{binary}_bodies.pkl")
    if os.path.exists(cache) and os.path.getmtime(cache) > os.path.getmtime(src):
        with open(cache, "rb") as f:
            return pickle.load(f)
    with open(src, encoding="utf-8", errors="replace") as f:
        text = f.read()
    ms = list(HDR.finditer(text))
    d = {}
    for i, m in enumerate(ms):
        end = ms[i + 1].start() if i + 1 < len(ms) else len(text)
        d[m.group(2)] = text[m.start():end]
    with open(cache, "wb") as f:
        pickle.dump(d, f)
    return d


def compact(body, known):
    lines = body.splitlines()
    m = HDR.match(body)
    if m:
        lines = body[m.end():].splitlines()
    out = []
    in_plate = False
    for l in lines:
        if not out and not in_plate and l.startswith("/* ") and "*/" not in l:
            in_plate = True
            continue
        if in_plate:
            in_plate = "*/" not in l
            continue
        if not l.strip() or DECL.match(l) or l.startswith("/* WARNING: Globals starting"):
            continue
        out.append(l)
    text = "\n".join(out)
    return LABEL.sub(lambda m: known.get(m.group(2), m.group(0)), text)


def disasm(binary, addr):
    out, grab = [], False
    with open(os.path.join(ANALYSIS, f"{binary}_disasm_full.txt"), encoding="utf-8", errors="replace") as f:
        for line in f:
            if not grab:
                if line.startswith("; FUNCTION ") and line.rstrip().endswith("@ " + addr):
                    grab = True
                    out.append(line)
            elif line.startswith("; FUNCTION ") or line.startswith(";====="):
                break
            else:
                out.append(line)
    return "".join(out)


def addrs(args):
    return [a.lower().zfill(8) for a in args]


def cmd_triage(args):
    binary = args[0]
    limit = int(args[args.index("--limit") + 1]) if "--limit" in args else 0
    maxl = int(args[args.index("--max-lines") + 1]) if "--max-lines" in args else 10 ** 9
    sort = args[args.index("--sort") + 1] if "--sort" in args else "named"
    assert sort in ("named", "sites", "callers"), sort
    rows = uc.analyse(binary, uc.CONFIDENCE_RANK["high"], False, None)
    backlog = {r["address"] for r in rows}
    _, branches = uc.parse_disasm(binary)
    # Distinct functions anywhere in the binary that CALL each backlog target.
    callers = {}
    for c, sites in branches.items():
        for _, k, t in sites:
            if k == "CALL" and t in backlog:
                callers.setdefault(t, set()).add(c)
    for r in rows:
        r["all_callers"] = len(callers.get(r["address"], ()))
    if sort == "sites":
        rows.sort(key=lambda r: (-r["total_call_sites"], -r["all_callers"], r["address"]))
    elif sort == "callers":
        rows.sort(key=lambda r: (-r["all_callers"], -r["total_call_sites"], r["address"]))
    known = names(binary)
    d = bodies(binary)
    shown = 0
    print("address   named sites fns lines unnamedCallees  callers")
    for r in rows:
        a = r["address"]
        n = len(compact(d[a], known).splitlines()) if a in d else -1
        if n > maxl:
            continue
        sub = sorted({t for _, k, t in branches.get(a, []) if k == "CALL" and t not in known})
        print(f"{a}  {r['named_caller_count']:>5} {r['total_call_sites']:>5} {r['all_callers']:>3} {n:>5} {len(sub):>3}"
              f"{'(' + ','.join(t for t in sub if t in backlog) + ')' if any(t in backlog for t in sub) else ''}"
              f"  {', '.join(sorted(r['named_callers'])[:3])}")
        shown += 1
        if limit and shown >= limit:
            break


def cmd_body(args):
    binary, rest = args[0], args[1:]
    dis, full = "-d" in rest, "--full" in rest
    known = names(binary)
    d = bodies(binary)
    for a in addrs(x for x in rest if not x.startswith("-")):
        if a not in d:
            print(f"### {a} not in decompile")
        else:
            print(f"### {a} {known.get(a, '')}")
            print(d[a] if full else compact(d[a], known))
        if dis:
            print(disasm(binary, a))


def cmd_callers(args):
    binary, rest = args[0], args[1:]
    ctx = int(rest[rest.index("-n") + 1]) if "-n" in rest else 0
    mx = int(rest[rest.index("--max") + 1]) if "--max" in rest else 10 ** 9
    rest = [x for i, x in enumerate(rest) if not x.startswith("-") and (i == 0 or rest[i - 1] not in ("-n", "--max"))]
    known = names(binary)
    d = bodies(binary)
    for a in addrs(rest):
        alts = [r"(?:FUN|DAT|PTR|LAB|_DAT)_" + a]
        if a in known:
            alts.append(re.escape(known[a]))
        pat = re.compile(r"\b(?:" + "|".join(alts) + r")\b", re.I)
        total = 0
        print(f"##### {a} {known.get(a, '')}")
        for fa, body in d.items():
            if fa == a:
                continue
            lines = body.splitlines()
            for i, l in enumerate(lines):
                if pat.search(l):
                    total += 1
                    if total <= mx:
                        lo, hi = max(0, i - ctx), min(len(lines), i + ctx + 1)
                        print(f"  [{known.get(fa, fa)}] " + " | ".join(x.strip() for x in lines[lo:hi]))
        print(f"  total {total}" + (f" (showed {mx})" if total > mx else ""))


def repo_texts():
    files = {}
    for sub in ("docs", "src", "tests"):
        for dp, dn, fn in os.walk(os.path.join(REPO, "Herculan", sub)):
            dn[:] = [x for x in dn if x not in ("bin", "obj")]
            for f in fn:
                if f.endswith((".md", ".cs")):
                    p = os.path.join(dp, f)
                    with open(p, encoding="utf-8", errors="replace") as fh:
                        files[p] = fh.read().splitlines()
    return files


def cmd_precheck(args):
    binary = args[0]
    es = entries()
    texts = repo_texts()
    for a in addrs(args[1:]):
        print(f"== {a}")
        for e in es:
            if e["address"].lower() == a and e["binary"] == binary:
                print(f"  ENTRY [{e['confidence']}] {e.get('name')}: {e.get('description', '')[:240]}")
        pat = re.compile(r"(?<![0-9A-Za-z])" + a + r"(?![0-9A-Za-z])", re.I)
        for p, lines in texts.items():
            for i, l in enumerate(lines):
                if pat.search(l):
                    print(f"  {os.path.relpath(p, REPO)}:{i + 1}: {l.strip()[:220]}")


def cmd_sym(args):
    binary = args[0]
    for p in args[1:]:
        r = re.compile(p, re.I)
        for e in entries():
            if e["binary"] == binary and (r.search(e.get("name") or "") or r.search(e["address"])):
                print(f"{e['address']} [{e['type']}/{e['confidence']}] {e.get('name')}: {e.get('description', '')}\n")


def pe(binary):
    with open(os.path.join(REPO, "ES2", f"{binary}.EXE"), "rb") as f:
        d = f.read()
    o = struct.unpack_from("<I", d, 0x3c)[0]
    n = struct.unpack_from("<H", d, o + 6)[0]
    so = struct.unpack_from("<H", d, o + 20)[0]
    secs = []
    for i in range(n):
        vs, va, rs, ro = struct.unpack_from("<IIII", d, o + 24 + so + i * 40 + 8)
        secs.append((va + 0x400000, vs, ro, rs))

    def off(v):
        for va, vs, ro, rs in secs:
            if va <= v < va + max(vs, rs):
                return v - va + ro
        return None
    return d, off


def cmd_rtti(args):
    d, off = pe(args[0])
    for a in args[1:]:
        o = off(int(a, 16) - 0xc)
        ti = struct.unpack_from("<I", d, o)[0] if o is not None else 0
        to = off(ti)
        if to is None:
            print(a, "unmapped", hex(ti))
            continue
        size = struct.unpack_from("<I", d, to)[0]
        no = struct.unpack_from("<H", d, to + 6)[0]
        print(f"{a}: ti={ti:08x} size=0x{size:x} name={d[to + no:to + no + 64].split(b'\0')[0].decode('latin1')}")


def read_symbols_text():
    with open(SYMBOLS, "rb") as f:
        raw = f.read()
    assert raw[:3] == b"\xef\xbb\xbf", "expected BOM"
    text = raw[3:].decode("utf-8")
    nl = "\r\n" if "\r\n" in text else "\n"
    if nl == "\r\n":
        assert text.count("\r\n") == text.count("\n"), "mixed newlines"
    return text.replace("\r\n", "\n"), nl


def write_symbols_text(text, nl):
    with open(SYMBOLS, "wb") as f:
        f.write(b"\xef\xbb\xbf" + text.replace("\n", nl).encode("utf-8"))


def block(e):
    ordered = {k: e[k] for k in FIELDS if k in e}
    return "\n".join("    " + l for l in json.dumps(ordered, indent=2, ensure_ascii=False).split("\n"))


def check_unique(text):
    d = json.loads(text)
    seen = set()
    for e in d["entries"]:
        k = (e["binary"], e["address"].lower())
        assert k not in seen, ("duplicate address", k)
        seen.add(k)
    nk = [(e["binary"], e["name"]) for e in d["entries"] if e.get("name")]
    dup = {x for x in nk if nk.count(x) > 1} if len(nk) != len(set(nk)) else set()
    assert not dup, ("duplicate (binary, name)", dup)
    return d


def cmd_insert(args):
    text, nl = read_symbols_text()
    with open(args[0], encoding="utf-8") as f:
        new = json.load(f)
    old = json.loads(text)["entries"]
    existing = {(e["binary"], e["address"].lower()) for e in old}
    taken = {(e["binary"], e.get("name")) for e in old if e.get("name")}
    blocks = []
    for e in new:
        assert set(e) <= set(FIELDS), e
        for k in ("address", "binary", "type", "confidence", "description", "source"):
            assert k in e, (k, e)
        assert re.fullmatch(r"[0-9a-f]{8}", e["address"]), e["address"]
        assert e["binary"] in ("DBSIM", "VSHELL") and e["type"] in ("function", "data")
        assert (e["binary"], e["address"]) not in existing, ("duplicate address", e["address"])
        c = e["confidence"]
        assert c in ("high", "medium", "low")
        if c == "low":
            assert "name" not in e, e["address"]
        else:
            n = e["name"]
            assert re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", n), n
            assert n.startswith("maybe_") == (c == "medium"), (n, c)
            assert (e["binary"], n) not in taken, ("duplicate name", n)
            taken.add((e["binary"], n))
        existing.add((e["binary"], e["address"]))
        for v in e.values():
            assert not re.search(r"[\x00-\x1f]", v), ("control char", e["address"])
        blocks.append(block(e) + ",")
    anchor = '"entries": [\n'
    assert text.count(anchor) == 1
    text2 = relink(text.replace(anchor, anchor + "\n".join(blocks) + "\n", 1), new)
    d = check_unique(text2)
    fn = sum(1 for e in new if e.get("name") and e["type"] == "function")
    dn = sum(1 for e in new if e.get("name") and e["type"] == "data")
    print(f"{len(new)} entries ok ({fn} named functions, {dn} named data); total {len(d['entries'])}")
    if "--write" in args:
        write_symbols_text(text2, nl)
        print("written")


ENTRY_BLOCK = re.compile(r"    \{\n(?:      .*\n)*?    \}")


def rename_mentions(text, binary, address, old, new):
    """Replace old with new in the description and source of the other entries of the same binary.
    Names are per binary, so another binary may use the same name for its own function; mentions in
    its entries are listed for a manual check instead of being rewritten."""
    rx = re.compile(r"\b" + re.escape(old) + r"\b")
    out, pos, n = [], 0, 0
    for m in ENTRY_BLOCK.finditer(text):
        e = json.loads(m.group(0))
        if e["address"] == address and e["binary"] == binary:
            continue
        hits = [k for k in ("description", "source") if rx.search(e.get(k, ""))]
        if not hits:
            continue
        if e["binary"] != binary:
            print(f"  check {e['binary']} {e['address']} {e.get('name')}: mentions {old}")
            continue
        for k in hits:
            n += len(rx.findall(e[k]))
            e[k] = rx.sub(new, e[k])
        out.append(text[pos:m.start()])
        out.append(block(e))
        pos = m.end()
    out.append(text[pos:])
    if n:
        print("renamed", old, "->", new, ":", n, "mentions in other", binary, "entries")
    return "".join(out)


def relink(text, new):
    """Rewrite FUN_/DAT_/PTR_<addr> in same-binary descriptions to each newly named entry's name."""
    for e in new:
        if e.get("name"):
            prefix = "FUN_" if e["type"] == "function" else "DAT_"
            text = rename_mentions(text, e["binary"], e["address"], prefix + e["address"], e["name"])
    return text


def cmd_relink(args):
    text, nl = read_symbols_text()
    with open(args[0], encoding="utf-8") as f:
        text = relink(text, json.load(f))
    check_unique(text)
    if "--write" in args:
        write_symbols_text(text, nl)
        print("written")


def cmd_edit(args):
    text, nl = read_symbols_text()
    with open(args[0], encoding="utf-8") as f:
        edits = json.load(f)
    for ed in edits:
        pat = re.compile(r'    \{\n      "address": "' + ed["address"] + r'",\n      "binary": "' + ed["binary"]
                         + r'",\n(?:      .*\n)*?    \}', re.M)
        ms = list(pat.finditer(text))
        assert len(ms) == 1, (ed["address"], len(ms))
        m = ms[0]
        e = json.loads(m.group(0))
        old = e.get("name")
        fields = ed.get("fields") or {ed["field"]: ed["value"]}
        for k, v in fields.items():
            assert k in FIELDS and k not in ("address", "binary"), k
            assert not re.search(r"[\x00-\x1f]", v)
            e[k] = v
        if e.get("name"):
            assert e["name"].startswith("maybe_") == (e["confidence"] == "medium"), (e["name"], e["confidence"])
        else:
            assert e["confidence"] == "low", ed["address"]
        text = text[:m.start()] + block(e) + text[m.end():]
        if old and e.get("name") and old != e["name"]:
            text = rename_mentions(text, ed["binary"], ed["address"], old, e["name"])
        elif not old and e.get("name"):
            prefix = "FUN_" if e["type"] == "function" else "DAT_"
            text = rename_mentions(text, ed["binary"], ed["address"], prefix + ed["address"], e["name"])
    d = check_unique(text)
    print("ok", len(d["entries"]))
    if "--write" in args:
        write_symbols_text(text, nl)
        print("written")


def cmd_mentions(args):
    named = batch_names(args[0])
    if not named:
        return
    pat = re.compile(r"(?<![0-9A-Za-z])(?:FUN_|DAT_|PTR_|LAB_|_DAT_)?(" + "|".join(named) + r")(?![0-9A-Za-z])")
    for p, lines in repo_texts().items():
        for i, l in enumerate(lines):
            for m in pat.finditer(l):
                print(f"{os.path.relpath(p, REPO)}:{i + 1}: [{m.group(0)} -> {named[m.group(1)]}] "
                      f"...{l[max(0, m.start() - 60):m.end() + 40]}...")


def batch_names(path):
    """address -> current known_symbols name for a batch's named entries. A batch file can hold a
    name that was later changed, and an address named in both binaries is ambiguous in prose, so
    such addresses are left out and reported."""
    with open(path, encoding="utf-8") as f:
        b = json.load(f)
    current = {(e["binary"], e["address"]): e.get("name") for e in entries()}
    by_addr = {}
    for e in entries():
        if e.get("name"):
            by_addr.setdefault(e["address"], set()).add(e["binary"])
    named = {}
    for e in b:
        n = current.get((e["binary"], e["address"]))
        if not n:
            continue
        if len(by_addr.get(e["address"], ())) > 1:
            print(f"  skip {e['address']}: named in both binaries")
            continue
        named[e["address"]] = n
    return named


def cmd_fixrefs(args):
    """Rewrite the usual reference shapes to a batch's new names in docs (.md) and C# (.cs):
    `FUN_a` -> `Name` (`a`) in markdown, <c>FUN_a</c> -> <c>Name</c> (<c>a</c>) and a bare FUN_a
    -> Name (a) in C#. Anything else naming the address is listed for a manual edit."""
    named = batch_names(args[0])
    if not named:
        return
    alt = "|".join(named)
    md = re.compile(r"`(?:FUN|DAT|PTR)_(" + alt + r")`( \(`\1`\))?")
    cs_c = re.compile(r"<c>(?:FUN|DAT|PTR)_(" + alt + r")</c>( \(<c>\1</c>\))?")
    cs_bare = re.compile(r"(?<![0-9A-Za-z_])(?:FUN|DAT|PTR)_(" + alt + r")(?![0-9A-Za-z])( \(\1\))?")
    left = re.compile(r"(?<![0-9A-Za-z])(?:FUN_|DAT_|PTR_)?(" + alt + r")(?![0-9A-Za-z])")
    changed = 0
    for p, _ in repo_texts().items():
        with open(p, "rb") as fh:
            raw = fh.read()
        bom = raw[:3] == b"\xef\xbb\xbf"
        t = (raw[3:] if bom else raw).decode("utf-8")
        if p.endswith(".md"):
            t2 = md.sub(lambda m: f"`{named[m.group(1)]}` (`{m.group(1)}`)", t)
            # Inside parentheses already: "(`Name` (`a`))" reads better as "(`Name`, `a`)".
            t2 = re.sub(r"\(`(\w+)` \(`(" + alt + r")`\)\)",
                        lambda m: f"(`{m.group(1)}`, `{m.group(2)}`)" if named[m.group(2)] == m.group(1) else m.group(0), t2)
        else:
            t2 = cs_c.sub(lambda m: f"<c>{named[m.group(1)]}</c> (<c>{m.group(1)}</c>)", t)
            t2 = cs_bare.sub(lambda m: f"{named[m.group(1)]} ({m.group(1)})", t2)
        if t2 != t:
            changed += 1
            print("fix", os.path.relpath(p, REPO))
            if "--write" in args:
                with open(p, "wb") as fh:
                    fh.write((b"\xef\xbb\xbf" if bom else b"") + t2.encode("utf-8"))
        for i, l in enumerate(t2.splitlines()):
            for m in left.finditer(l):
                if not re.search(re.escape(named[m.group(1)]), l):
                    print(f"  manual {os.path.relpath(p, REPO)}:{i + 1}: [{m.group(0)} -> {named[m.group(1)]}] "
                          f"...{l[max(0, m.start() - 60):m.end() + 40]}...")
    print(changed, "files", "written" if "--write" in args else "(dry run)")


def cmd_repl(args):
    with open(args[0], encoding="utf-8") as f:
        spec = json.load(f)
    files = {}
    for rel, old, new, cnt in spec:
        p = os.path.join(REPO, rel)
        if p not in files:
            with open(p, "rb") as fh:
                raw = fh.read()
            bom = raw[:3] == b"\xef\xbb\xbf"
            files[p] = [bom, (raw[3:] if bom else raw).decode("utf-8")]
        t = files[p][1]
        assert t.count(old) == cnt, (rel, old, t.count(old))
        files[p][1] = t.replace(old, new)
    print("ok", len(spec), "replacements in", len(files), "files")
    if "--write" in args:
        for p, (bom, t) in files.items():
            with open(p, "wb") as fh:
                fh.write((b"\xef\xbb\xbf" if bom else b"") + t.encode("utf-8"))
        print("written")


def cmd_apply(args):
    binary = args[0]
    bat = os.path.join(REPO, "tools", "ghidra_12.1.2_PUBLIC", "support", "analyzeHeadless.bat")
    cmd = [bat, os.path.join(REPO, "tools", "ghidra_project"), "ES2Recon", "-process", f"{binary}.EXE",
           "-noanalysis"] + ([] if "--write" in args else ["-readOnly"]) + [
        "-scriptPath", os.path.join(REPO, "tools", "ghidra_scripts"),
        "-postScript", "ES2ApplySymbolNames", SYMBOLS]
    r = subprocess.run(cmd, capture_output=True, text=True, errors="replace")
    keep = re.compile(r"renamed|labeled|skipped|error|warn|exception|fail|summary", re.I)
    for l in (r.stdout + r.stderr).splitlines():
        if keep.search(l) and "bundle event for non-GhidraBundle" not in l:
            print(l.strip()[:300])
    print("exit", r.returncode, "(read-only)" if "--write" not in args else "(written)")


def cmd_fixentry(args):
    binary = args[0]
    addrs = [a.lower() for a in re.split(r"[+,]", args[1]) if a]
    assert all(re.fullmatch(r"[0-9a-f]{8}", a) for a in addrs), addrs
    write = "--write" in args
    scripts = os.path.join(REPO, "tools", "ghidra_scripts")
    bat = os.path.join(REPO, "tools", "ghidra_12.1.2_PUBLIC", "support", "analyzeHeadless.bat")
    tmp = tempfile.mkdtemp(prefix="es2_fixentry_")
    try:
        cmd = [bat, os.path.join(REPO, "tools", "ghidra_project"), "ES2Recon", "-process", f"{binary}.EXE",
               "-noanalysis"] + ([] if write else ["-readOnly"]) + ["-scriptPath", scripts]
        for a in addrs:
            cmd += ["-postScript", "ES2MergeFunctionAt", a, "auto", os.path.join(tmp, f"merge_{a}.txt")]
        cmd += ["-postScript", "ES2ApplyStructures", os.path.join(scripts, "known_structs.json"),
                "-postScript", "ES2ApplySymbolNames", SYMBOLS,
                "-postScript", "ES2CheckFunctionEntries", os.path.join(tmp, "check.txt")] + addrs
        r = subprocess.run(cmd, capture_output=True, text=True, errors="replace")
        keep = re.compile(r"ES2Apply\w+\.java>.*(errors|summary|=)|ERROR|exception|fail", re.I)
        for l in (r.stdout + r.stderr).splitlines():
            if keep.search(l) and "bundle event for non-GhidraBundle" not in l:
                print(l.strip()[:300])
        bad = False
        for a in addrs:
            p = os.path.join(tmp, f"merge_{a}.txt")
            text = open(p, encoding="utf-8").read() if os.path.exists(p) else "FAILED: no merge output\n"
            bad |= bool(re.search(r"^(FAILED|SHORT)", text, re.M))
            print(f"== {a}\n{text.rstrip()}")
        p = os.path.join(tmp, "check.txt")
        print("== check\n" + (open(p, encoding="utf-8").read().rstrip() if os.path.exists(p) else "(no check output)"))
        print("exit", r.returncode, "(written)" if write else "(read-only)", "-- PROBLEMS ABOVE" if bad else "")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


COMMANDS = {"triage": cmd_triage, "body": cmd_body, "callers": cmd_callers, "precheck": cmd_precheck,
            "sym": cmd_sym, "rtti": cmd_rtti, "insert": cmd_insert, "edit": cmd_edit,
            "mentions": cmd_mentions, "relink": cmd_relink, "fixrefs": cmd_fixrefs, "repl": cmd_repl, "apply": cmd_apply,
            "fixentry": cmd_fixentry}

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if len(sys.argv) < 3 or sys.argv[1] not in COMMANDS:
        print(__doc__)
        sys.exit(2)
    COMMANDS[sys.argv[1]](sys.argv[2:])
