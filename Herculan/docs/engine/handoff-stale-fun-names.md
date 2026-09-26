# Handoff — `FUN_` spellings of symbols that have since been named

Scratchpad for one mechanical pass. Nothing here is a finding. Delete the file once the reverse case below is done.

## Where it stands

Every `FUN_` spelling of an address `known_symbols.json` names has been replaced across `Herculan/docs` and `Herculan/src`, except these, which stay as they are:

| Where | Address | Why |
|---|---|---|
| `docs/command-line.md` (2) | `004092dc` | VSHELL's switch parser calls it; the registered name, `Smoke_Tick`, is DBSIM's function at the same address |
| `docs/formats/dts-node-posing.md` (3) | `00476030`, `0047f914`, `0048c338` | Quoted Ghidra output |
| `maybe_` names (8 mentions) | `00416379`, `0041266a`, `0045e480`, `004045c8`, `0048c338` | Medium-confidence guesses; substituting them would put a guess in the prose |

The scan below prints exactly these, plus this file.

```python
# from the repo root
import json, re, pathlib
named = {e['address'].lower(): e for e in json.load(
    open('tools/ghidra_scripts/known_symbols.json', encoding='utf-8-sig'))['entries'] if e.get('name')}
for p in pathlib.Path('Herculan').rglob('*'):
    if p.suffix.lower() not in ('.md', '.cs') or 'bin' in p.parts or 'obj' in p.parts:
        continue
    for a in re.findall(r'(?<![A-Za-z0-9_])FUN_([0-9a-fA-F]{8})', p.read_text(encoding='utf-8-sig')):
        if a.lower() in named:
            print(p.as_posix(), a, named[a.lower()]['name'], named[a.lower()]['binary'])
```

## Remaining: the reverse case

A doc that names a symbol `known_symbols.json` does not carry, or carries under a different name. Those disagree silently and `tools/scripts/Check-Symbol.ps1` is the only thing that finds them, one symbol at a time. The `FUN_` pass turned up one, `LightManager_SetIntensity` in an `effect-lights.md` pseudo-code block for the registered `LightManager_SetSlotIntensity` (`00407048`), and fixed it; nobody has swept for the rest.

Traps that carry over from the `FUN_` pass:

- **The name is per binary, and the address is not.** The two programs share an address space; read the surrounding text for which program it is talking about.
- **A heading owns an anchor.** Run `tools/scripts/doc_links.py` after renaming one.
- **A sentence may be about the address rather than the function** — a Rejected readings row, a dump excerpt quoted verbatim. Leave a quoted dump alone.
