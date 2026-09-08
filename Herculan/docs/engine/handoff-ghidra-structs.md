# Ghidra structure typing — handoff

Giving DBSIM's simulation objects a shape in the Ghidra database, so the decompiler renders `*(short *)(param_1 + 0x222)` as `mech->shieldFront` and `(**(code **)(*param_1 + 0x20))(...)` as `obj->vtbl->DirectFireHitTest(...)`.

Handoff doc: ephemeral, and **not authoritative for status** — re-run the scripts and grep the JSON before repeating anything below. Drain it into the topic docs and delete what you move.

Three stages were planned. Stage 1 is done. This file carries stages 2 and 3.

## Why this exists

Field offsets are the densest form of RE knowledge in the project and currently live only in prose: 37 distinct `obj+` offsets and 111 distinct `mech+` offsets are recorded across `docs/` and the engine's C# comments, and not one of them is in the Ghidra database. Every visit to the disassembly re-derives them by hand. The `known_symbols.json` readme scoped struct offsets out on the grounds that they are not fixed addresses and would need a real structure data type — correct about the mechanism, and that structure data type is what stages 2 and 3 build.

## Stage 1 — done

Vtables. Two data types under Ghidra category `/ES2`, applied at eight addresses:

| Type | Slots | Instances |
|---|---|---|
| `SimObjectVtable` | 34 | `0049a54c` base, `0049a282` mech |
| `ProjectileVtable` | 6 | `004a0b98` super-base, `004987a0` base, `00498448` rocket, `00498628` bullet, `004987c4` beam tracer, `004984fc` the dead Type-3 class |

- `tools/ghidra_scripts/known_vtables.json` — the layouts. Owns class *shape*, as `known_symbols.json` owns address→function findings.
- `tools/ghidra_scripts/ES2ApplyVtables.java` — applies them. Idempotent; second run reports `labeled=0`.
- `tools/ghidra_scripts/ES2DumpDataAt.java` — reports how a range is *typed*, where `ES2DumpRange` reports the bytes underneath. This is how a structure apply gets verified.

Run and verify:

```bash
sh tools/gh.sh ES2ApplyVtables "E:\ES2Stuff\tools\ghidra_scripts\known_vtables.json"
```

```bash
sh tools/gh.sh ES2DumpDataAt "E:\ES2Stuff\tools\analysis_out\vt.txt" 0049a282 00498628
```

**`SimObjectVtable`'s first six slots are `ProjectileVtable`, unchanged.** The two hierarchies share a base interface — slots `+0x4`, `+0xc` and `+0x10` hold the same three functions in every table in the file. Stage 2 must model that as embedding, not as two independent structs, or the shared prefix gets recorded twice and drifts.

### Left undone in stage 1

Not blockers, but they are real gaps and they will go stale silently:

1. **Five function identifications never reached `known_symbols.json`.** The deleting destructors `0040c324` (projectile base), `0046b890` (its super-base), `0040ab8d` (rocket), `0040b64d` (bullet), `0040ad2c` (dead Type-3) are described in `known_vtables.json`'s `Destructor` slot prose only. Per the split this file's readme states, address→function belongs in `known_symbols.json`. Move them.
2. **`ObjectList_Add` (`00411dd4`) and the shared base constructor (`00402188`) are still absent from `known_symbols.json`**, despite being named in prose in `planning.md`, `target-selection.md` and `SimObject.cs`. Flagged twice now; still outstanding.
3. **No doc under `Herculan/docs/` describes vtable layout.** `known_vtables.json` is the only home. That was deliberate — it is a machine-readable inventory, and scattering 34 slot meanings across ten docs is what the file exists to stop — but a doc reader currently has no pointer to it. Decide whether `docs/simulation/` gets a short `sim-object-vtables.md` that points at the JSON rather than restating it.

## Stage 2 — the `SimObject` struct

**Goal:** a sparse `SimObject` structure covering the ~19 offsets `SimObject.cs` already documents, applied to the parameters of the functions known to take one.

### Deliverables

- `tools/ghidra_scripts/known_structs.json` — new file, new schema. Not entries in `known_symbols.json`: the schema is offset/width/parent-struct, not address, and `known_symbols.json`'s "one address, one entry" invariant does not apply.
- `tools/ghidra_scripts/ES2ApplyStructures.java` — creates the types and applies them. Model it on `ES2ApplyVtables.java`, which is the closest working example of the shape.

Proposed schema, to firm up on contact with the API:

```json
{
  "structs": [{
    "name": "SimObject",
    "binary": "DBSIM",
    "size": 480,
    "vtable": "SimObjectVtable",
    "description": "...",
    "fields": [
      { "offset": 75, "width": 4, "type": "int",   "name": "listIndex",  "confidence": "high",
        "description": "obj+0x4b -- slot in the single live-object list, written by ObjectList_Add." }
    ]
  }],
  "applications": [
    { "function": "00433174", "parameter": 0, "struct": "SimObject" }
  ]
}
```

### Four rules that are not negotiable

1. **Sparse, never packed.** ~19 known offsets in an object of several hundred bytes. Create the struct with an explicit length so it is undefined-filled, then place fields with `StructureDataType.replaceAtOffset(offset, dt, length, name, comment)`. Do **not** use `add()` or `insertAtOffset()` — `add` appends sequentially (right for a vtable, wrong here) and `insertAtOffset` shifts everything after it, silently moving every field you already placed.

2. **Widths matter more than names.** A wrong name is a nuisance; a wrong width makes the decompiler lie everywhere at once — typing `+0x99` as an int swallows `+0x9a`–`+0x9c` and produces plausible, wrong output in every function that touches the object. Only place a field when the width is pinned by the access instruction. `ES2FindFieldRefs` is the tool: give it the displacement and it returns every instruction carrying it, and `MOV byte ptr` / `MOVSX EAX, word ptr` / `MOV EAX, dword ptr` settles the width. Where readers disagree on width, that is a finding — do not average it, leave the field out and note it.

3. **Inheritance is embedding.** `obj+0x99`, `+0x95`, `+0x1b6` and the rest are common to mech, flyer and base. Define `SimObject` once and embed it at offset 0 of each derived struct, mirroring the C# hierarchy, so one recorded fact serves all three. Field 0 must be `SimObjectVtable *` — that pointer is what makes an indirect call render as a named method, which is the whole payoff of stage 1.

4. **Confidence tiering carries over, and matters more here than for functions.** A guessed offset typed into a struct looks confirmed in *every* function that touches the object, forever. Keep `known_symbols.json`'s rule: low confidence gets a comment and no name; medium gets a `maybe_` prefix. A field whose meaning is unestablished stays `undefined` rather than getting a plausible name.

### Order of work

1. **Fix the size before anything else.** Find the allocation — the `operator new` literal in whatever calls `Mech_Constructor` (`00415bb0`) — and use it. Do not infer the object's length from the highest offset anyone happens to have documented.
2. Seed the field list from `SimObject.cs`'s own doc comments; every offset there is already cross-checked against a doc. Confirm each width with `ES2FindFieldRefs` before recording it.
3. Apply to parameters. The labor is here, not in defining the struct: nothing renders until each function's `param_1` is typed. Automate what you can — a `known_symbols.json` entry whose description already says "Mech vtable +0xNN" is by definition a mech method, so its `param_1` is `MechObject *`, and several dozen entries say exactly that.
4. Verify by decompiling, not by dumping. `ES2DumpDataAt` verifies a structure laid at an address; it says nothing about a struct applied to a parameter. Use `ES2DecompileContaining` on a function you typed and read the output for named fields.

**API status, so it is not mistaken for proven:** `StructureDataType`, `FunctionDefinitionDataType`, `PointerDataType`, `addDataType(..., REPLACE_HANDLER)`, `clearCodeUnits`, `createData`, `createLabel` and `setComment(..., CodeUnit.PLATE_COMMENT)` are all exercised and working in `ES2ApplyVtables.java`. `replaceAtOffset` on a sparse struct, `Parameter.setDataType(dt, SourceType.USER_DEFINED)` and `HighFunctionDBUtil` for locals are the right calls on paper and have **not** been run here.

### Done when

A function typed with `SimObject *` decompiles with named field accesses and at least one named virtual call, `ES2ApplyStructures` is idempotent on a second run, and `ES2ApplySymbolNames` still reports `errors=0` afterwards.

## Stage 3 — the `MechObject` struct

Same machinery, 111 offsets instead of 19, embedding `SimObject` at offset 0. Three things are harder:

- **Width discipline gets tested.** The mech's field space is dense with byte flags and Q8/Q10 fixed-point words; this is where averaging a disagreement or assuming `int` will do real damage.
- **Sub-objects are not fields.** The component-damage header, the shield block at `+0x222`, the weapon-mount manager and the behaviour block at `+0x4d` are structures in their own right. Give each its own type and embed it, rather than flattening its members into `MechObject` — otherwise `ComponentDamage`'s layout gets recorded once per class that owns one.
- **Flyer overlaps mech.** `Flyer_Constructor` (`004215f4`) installs its own vtable but shares most of the layout, and `MechObject.Flight.cs` documents fields under `mech+` that only a RAZOR uses. Decide whether flyer is a derived struct or the same struct with flyer-only fields — the C# port treats it as one class, which is evidence but not proof about the original.

## Gotchas that cost time here

- **`ES2DumpVtable` resolves any valid address.** The word after a vtable's last slot is often a pointer into an adjacent class-descriptor record — `0046b7c8` and `0040c3d8` both resolve and disassemble fine and are neither functions nor slots. A plausible code address is not evidence of a slot. Check for a function prologue and for an actual call site.
- **Python's `write_text` flips files to CRLF on Windows.** The repo stores LF. Use `read_bytes`/`write_bytes` in any script that edits this tree, or normalise afterwards.
- **`tools/gh.sh` greps its output and truncates at 20 lines.** For a script that prints more than that, call `analyzeHeadless.bat` directly with your own filter.
- **One bad `.java` in `-scriptPath` breaks every script in the directory** with a misleading OSGi error. If everything suddenly fails, suspect the file you just added, not the environment.
- **Heredocs into the Bash tool are unreliable for long Python.** Write the script to the scratchpad and run it by path.

## Open questions

- **The table at `004a0bb8`.** Next after the projectile super-base, `FireEffect_Dtor` in its destructor slot, but data at `+0x14` — so possibly a 5-slot sibling of `ProjectileVtable` rather than a sixth instance of it. Left out of `known_vtables.json` deliberately. Worth settling when the effect classes come up.
- **What class `004a0b98` is.** The projectile base derives from it — its destructor installs that table at `0040c353` — but the `0046bxxx` block its slots point into is shared engine code and none of it is named.
- **The eight unnamed `SimObjectVtable` slots** (`slot_0x04`, `slot_0x0c`, `slot_0x28`, `slot_0x30`, `slot_0x60`, `slot_0x68`, `slot_0x80`, `slot_0x84`). `+0x4` and `+0xc` hold the same function in every table in the file, base and projectile alike, which makes them the likeliest to be worth a name.
- **Whether the Flyer table (`0049a5e0`) and the five structure vtables `Base_Construct` switches between should be added now.** The flyer is fully dumped and is the same 34-slot shape as `SimObjectVtable` — a two-line change to the JSON whenever wanted.
