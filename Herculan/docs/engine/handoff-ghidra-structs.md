# Ghidra structure typing — handoff

Giving DBSIM's simulation objects a shape in the Ghidra database, so the decompiler renders `*(short *)(param_1 + 0x222)` as `mech->shieldFront` and `(**(code **)(*param_1 + 0x20))(...)` as `obj->vtbl->DirectFireHitTest(...)`.

Handoff doc: ephemeral, and **not authoritative for status** — re-run the scripts and grep the JSON before repeating anything below. Drain it into the topic docs and delete what you move.

Stages 1 and 2 are done and drained into [`docs/simulation/sim-object-layout.md`](../simulation/sim-object-layout.md), which is where the hierarchy, the class sizes and the pointers to the three JSON inventories now live. This file carries stage 3.

## What is already in place

| File | Owns |
|---|---|
| `tools/ghidra_scripts/known_vtables.json` | `SimObjectVtable` (34 slots) and `ProjectileVtable` (6), applied at eight addresses |
| `tools/ghidra_scripts/known_structs.json` | `Transform32`, `CountdownTimer`, `BehaviourBlock`, `SimObject` — 48 fields, plus the parameter applications |
| `tools/ghidra_scripts/ES2ApplyStructures.java` | Builds the types and types the parameters. Model stage 3 on it |
| `tools/ghidra_scripts/ES2DefineFunctionAt.java` | Promotes disassembled code to a Function without clearing anything. A vtable slot with no direct caller has no function, and `ES2ApplySymbolNames` skips it |
| `tools/ghidra_scripts/ES2DumpDataAt.java` | Reports how a range is *typed*, where `ES2DumpRange` reports the bytes underneath |

`ES2ApplyStructures` checks each field's declared width against the resolved data type's own length, refuses overlaps, and refuses a field that runs past the declared size. Widen the schema rather than working around those checks.

**Which file types a parameter, and why it matters to stage 3.** Applying a signature replaces the whole parameter list, so `known_symbols.json` and `known_structs.json` must not both describe one parameter. A function that carries a `signature` owns its parameter types there — write `MechObject *this`, not `int *this` — and is not listed in `applications`. A function without one gets typed from `applications`. Many of the mech methods stage 3 will want already carry signatures (`Mech_AiTick`, `Mech_CollisionTest`, `Razor_MovementTick`, `Mech_PerTickSystemsUpdate`, `Mech_ComponentPosition`…), so this is the common case, not the corner case. `ES2ApplySymbolNames` keeps a parameter typed with a pointer into `/ES2` across a signature apply and warns when the two files disagree, but that guard exists to catch the mistake, not to make it survivable. Ordering only bites on a database that has never had these applied, and `tools/scripts/ghidra_apply_all.sh` handles it; day to day, run whichever script you need on its own.

## Stage 3 — the `MechObject` struct

Same machinery, embedding `SimObject` at offset 0 and running to `0x36a`. Everything from `0x1f2` up is the mech's own.

### Order of work

1. **Seed from the docs, not from the disassembly.** `mech-locomotion.md`, `ai-dispatch.md`, `hit-detection.md`, `damage-system.md`, `weapon-mounts.md` and `torso-aim.md` all carry mech field tables, and `MechObject.*.cs` carries more in doc comments. Collect them first; most are already cross-checked.
2. **Confirm every width with `ES2FindFieldRefs` before recording it.** Read the note below about what that tool cannot see.
3. **Give each sub-object its own type and embed it.** The component-damage header at `+0x206`, the shield block at `+0x222`, the weapon-mount manager, and the stride-3 `CountdownTimer` runs at `+0x258`/`+0x25b`/`+0x25e` and `+0x264`/`+0x267`/`+0x26a`. Flattening one into `MechObject` means recording its layout again for every class that owns one.
4. **Apply to parameters.** The labour is here, not in defining the struct: nothing renders until each function's `param_1` is typed. Automate what you can — a `known_symbols.json` entry whose description already says "mech vtable +0xNN" is by definition a mech method, so its `param_1` is `MechObject *`, and several dozen entries say exactly that.
5. **Verify by decompiling, not by dumping.** `ES2DumpDataAt` verifies a structure laid at an address; it says nothing about a struct applied to a parameter. Use `ES2DecompileContaining`.

### Done when

A function typed `MechObject *` decompiles with named field accesses and at least one named virtual call, `ES2ApplyStructures` is idempotent on a second run, and `ES2ApplySymbolNames` still reports `errors=0` afterwards.

### The hard parts

- **Width discipline gets tested.** The mech's field space is dense with byte flags and Q8/Q10 fixed-point words; this is where averaging a disagreement or assuming `int` will do real damage. Where two readers disagree, leave the field out and put the disagreement in the struct's `notes`.
- **`ES2FindFieldRefs` will come back empty for live fields.** Watcom materialises a sub-object's address once and then uses small displacements off it, so a field at `mech+0x25a` may only ever appear as `[EDX + 0x2]`. A zero-site result is not evidence the field is unused — it usually means you are searching for the wrong number. Search for the *base* offset the `LEA` uses.
- **Flyer overlaps mech but is not a subset of it.** Two pools, two lengths (`0x291` vs `0x36a`), and the flyer starts its own fields at `+0x1fa` where the mech starts at `+0x1f2` — they are siblings, both embedding `SimObject`. `MechObject.Flight.cs` documents fields under `mech+` that only a RAZOR uses; those belong in `FlyerObject`, and the C# port's decision to make it one class is a port decision, not a finding about the original.

## Left undone

- **The flyer table (`0049a5e0`) and the five structure vtables `Base_Construct` switches between are not in `known_vtables.json`.** The flyer is fully dumped and is the same 34-slot shape as `SimObjectVtable` — a two-line change whenever wanted.

## Gotchas that cost time here

- **`ES2DumpVtable` resolves any valid address.** The word after a vtable's last slot is often a pointer into an adjacent class-descriptor record — `0046b7c8` and `0040c3d8` both resolve and disassemble fine and are neither functions nor slots. A plausible code address is not evidence of a slot. Check for a function prologue and for an actual call site.
- **`ES2DisasmRange` and `ES2FindFieldRefs` parse lengths with `Integer.parseInt`**, so a `0x40` argument throws. Pass decimal.
- **Line endings differ by file type in the working tree.** The `known_*.json` files are LF and carry a UTF-8 BOM (read them with `utf-8-sig`); the `.java` files are CRLF. A patch script that matches on a bare newline silently finds nothing in a `.java`. Normalise on read, restore on write, and always use `read_bytes`/`write_bytes` — `write_text` rewrites the endings.
- **Editing a JSON by round-tripping it through `json.dumps` reformats the whole file.** `known_vtables.json` is hand-formatted one slot per line and comes back as a 250-line diff. Match and replace on the raw text instead, and check `git diff --stat` before moving on.
- **`tools/gh.sh` greps its output and truncates at 20 lines.** For a script that prints more than that, call `analyzeHeadless.bat` directly with your own filter.
- **One bad `.java` in `-scriptPath` breaks every script in the directory** with a misleading OSGi error. If everything suddenly fails, suspect the file you just added, not the environment.
- **Heredocs into the Bash tool are unreliable for long Python** — they eat a backslash level, so an escaped quote inside a string quietly changes it and a match that should hit will miss. Write the script to the scratchpad and run it by path.
