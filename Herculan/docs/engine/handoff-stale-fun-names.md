# Handoff — `FUN_` spellings of symbols that have since been named

Scratchpad for one mechanical pass. Nothing here is a finding; it is a backlog and the traps that come with working through it. Delete the file when the count reaches zero.

## What it is

`known_symbols.json` is the register of decoded names, and it has outrun the prose. A doc or comment written before an address was named still calls it `FUN_0045a7f4`, and nothing goes back to fix it — so the same function is `FUN_0045a7f4` in one paragraph and `Input_BuildPlayerDevice` in the next. That is the failure `tools/scripts/Check-Symbol.ps1` exists to surface, one symbol at a time; this is the whole of it at once.

**1195 mentions across 560 addresses**, every one of which `known_symbols.json` gives a name:

| | Files | Mentions |
|---|---|---|
| `Herculan/docs` | 49 | 492 |
| `Herculan/src` | 147 | 703 |

974 of the mentions resolve to a DBSIM name and 221 to a VSHELL one. The worst files are `docs/simulation/weapon-mounts.md` (39), `src/Herculan.Engine/Sim/WeaponMount.cs` (35), `docs/formats/save-games.md` (35), `src/Herculan.Engine/Render/Overlay2DRenderer.cs` (30) and `docs/formats/script-dat.md` (30). The most-repeated single addresses are `FUN_00407098` → `LightManager_SelectLightsForObject` (15), `FUN_00433445` → `Repair_RefreshDetail` (9) and `FUN_00436abc` → `MessagePort_Show` (8).

## Finding them

```python
# from the repo root
import json, re, pathlib, collections
named = {e['address'].lower(): e for e in json.load(
    open('tools/ghidra_scripts/known_symbols.json'))['entries'] if e.get('name')}
for p in pathlib.Path('Herculan').rglob('*'):
    if p.suffix.lower() not in ('.md', '.cs') or 'bin' in p.parts or 'obj' in p.parts:
        continue
    for a in re.findall(r'FUN_([0-9a-fA-F]{8})', p.read_text(encoding='utf-8')):
        if a.lower() in named:
            print(p.as_posix(), a, named[a.lower()]['name'], named[a.lower()]['binary'])
```

A `FUN_` spelling this does **not** print is an address with no name yet, which is correct as it stands and must be left alone.

## Traps

- **The name is per binary, and the address is not.** No address is currently named in both, so the scan has no ambiguity to report — but plenty are named in one binary and unnamed in the other, and the two programs share an address space. `FUN_00436abc` is `MessagePort_Show` in DBSIM and an unrelated VSHELL preferences setter; a `docs/shell/` mention of it is the second of those and must not take the first's name. Read the surrounding text for which program it is talking about, and run `Check-Symbol.ps1` on the address rather than trusting the scan's single answer.
- **A heading owns an anchor.** `## Applying the bindings — FUN_0045a7f4` became `## Applying the bindings — Input_BuildPlayerDevice (0045a7f4)`, which changed the anchor every inbound link used. Grep the old anchor across the docs before renaming a heading.
- **Keep the address.** The convention is `Name (00401234)` on first mention in a section, name alone after; dropping the address entirely loses the only thing that survives a rename.
- **A name is longer than `FUN_00401234`.** A substitution can push a line well past its neighbours; that's fine, the docs are no longer hard-wrapped, but check the result still reads as one paragraph.
- **A sentence may be about the address rather than the function** — "nothing calls `FUN_...`", a Rejected readings row, a dump excerpt quoted verbatim. Leave a quoted dump alone.

## Worth doing at the same time

The reverse case is not covered by any of this: a doc that names a symbol `known_symbols.json` does not carry, or carries under a different name. Those disagree silently and `Check-Symbol.ps1` is the only thing that finds them, one symbol at a time.
