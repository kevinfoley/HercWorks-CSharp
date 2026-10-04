# Plan — completing symbol coverage in DBSIM and VSHELL

Name every function, global, class layout and vtable slot in `DBSIM.EXE` and `VSHELL.EXE`, and make sure each one the engine needs is either ported or recorded as open work.

## Why

HERCULAN is close to feature complete, but for a long time functions were read without being recorded, so the Ghidra databases lag what is understood. Searching outward from named functions for callees to name has stopped paying: what is left is mostly reached through vtables, data tables, the shared library layer, or not reached from named code at all.

Coverage is not the goal for its own sake. A named function whose logic is neither ported nor recorded as open work is worse than an unnamed one, because it looks finished. The rules in [Every name closes its loop](#every-name-closes-its-loop) apply to every stage below and outrank the stage ordering.

## Where coverage stands

**These numbers are a snapshot and go stale as soon as any task names anything.** `es2_naming.py stats BIN` prints each column from the current dumps (see [Stage 0](#stage-0--tooling-and-fresh-dumps)); regenerate them before relying on them rather than editing them in place.

| | DBSIM | VSHELL |
|---|---|---|
| Ghidra functions with a real name | 3035 / 3458 (87.8%) | 2142 / 2466 (86.9%) |
| Code bytes inside unnamed functions | 6.2% (34 KB) | 10.8% (43 KB) |
| Unnamed functions under 100 bytes | 334 of 423 | 199 of 324 |
| `55 8B EC` prologues outside any Ghidra function | 0 | 0 |
| Code-section gaps outside any function, not all fill bytes (class records included) | 340 (45846 bytes) | 156 (25921 bytes) |
| Vtables (with RTTI) / slots / unnamed slot targets | 218 (196) / 1659 / 27 | 94 (92) / 581 / 27 |
| Vtables with a typed slot shape in Ghidra | 93 of the dump's 185 | 92 of the dump's 94 |
| RTTI class records (with a vtable) | 255 (196) | 126 (92) |
| Class records with a `known_structs.json` layout | 10 | 0 |
| Function parameters typed with a struct | 110 | 13 |
| Distinct `DAT_` globals left in the decompile (named data entries) | 1850 (310) | 1301 (236) |

What the numbers say: every class record's destructor is named and no prologue lies outside a function. The unnamed functions are now mostly the ones Stage 1 created and could not name from a twin: static initialisers and exit routines whose globals are unnamed, and the raster driver's assembly routines, which are most of the unnamed slot targets. Class layouts and globals are barely started. Struct work is what makes every remaining decompile cheaper to read, because one layout applied to `this` across a class's methods turns its `*(short *)(param_1 + 0x1a4)` reads into named fields and its `(**(code **)(*param_1 + 0x14))()` calls into named slot calls.

## Every name closes its loop

These hold for every function, field and global named under this plan, and equally when an already-named function is re-read and found to be described or ported wrongly.

1. **A name is finished only when its logic is accounted for.** Each newly named or re-read function ends in exactly one of these states:
   - **Ported** — the C# that implements it cites it (`Mech_LocomotionTick (00416a04)`) and links the doc section, and the retail doc describes the behaviour.
   - **Recorded** — the logic the engine lacks is an `**Unported:**` bullet in the `## Open` section of the topic doc that owns it, or an `**Open:**` bullet when what it does is not yet established. A behaviour gap the player would notice is also a `KNOWN_ISSUES.md` or `ROADMAP.md` entry as those files' own scopes decide. <!-- doc-lint: ok -->
   - **Not engine-relevant** — infrastructure the engine replaces wholesale (DOS drivers, Borland runtime, VOL streams, the SOS audio layer) or a helper whose effect is fully covered by a ported caller. The `known_symbols` description says what it does; nothing else is needed.

   "Not engine-relevant" is a claim. Make it only when the function's effect is understood, never as the default for a function that was not read closely.
2. **A re-read that contradicts the docs or the C# fixes them in the same task.** Edit the doc text so it states what is true (no correction narrative, per `CLAUDE.md`), and fix the C#. When the C# fix is too large for the task, record it as an `**Unported:**` or `**Open:**` bullet in the topic doc and say so in the report; never leave the doc corrected and the code silently wrong, or the reverse. <!-- doc-lint: ok -->
3. **Every batch ends with a mentions check.** For each address the batch named, `es2_naming.py precheck` (or `mentions batch.json`) lists its doc and C# mentions. An address with none must be one of the three states above, and the batch report says which for each. `fixrefs` rewrites the `FUN_` mentions that already existed.
4. **Behaviour changes go on the play-check list.** A fix that changes what the engine does in play is added to [Play checks](#play-checks) until the user has looked at it in the running engine.
5. **Status lives in the topic docs, not here.** This plan describes the method and the order. Findings go to the docs and `known_*.json` files; remaining open work goes to the owning doc's `## Open` section.

## Stage 0 — tooling and fresh dumps

The dumps under `tools/analysis_out/` lag the database whenever another task has applied names or created functions; regenerate them with `tools/scripts/ghidra_full_decomp.py` at the start of each stage and after any batch that creates functions (this takes several minutes; it writes all five dumps, the disassembly included). The tools each stage leans on:

- **`es2_naming.py stats BIN`** prints the coverage table above; `--list` adds every prologue outside a function and every code-section gap that is not all fill bytes.
- **`es2_naming.py classes BIN`** lists every class record with its size, bases and subobject offsets, primary vtable, `+0x28` destructor and `+0x14` operator delete, beside their `known_symbols` names; `--unnamed` keeps the records whose destructor is unnamed. Stage 2 generates skeletons from it.
- **`es2_naming.py apply BIN --define a+b+...`** runs `ES2DefineFunctionAt` on the listed addresses before `ES2ApplySymbolNames`, in one headless session. With no list it defines every named `known_symbols` function that has no Ghidra function, and reports any that falls inside another function's extent.
- **`es2_naming.py body -d`** disassembles with capstone where Ghidra has no function, up to the next function entry.
- **`es2_naming.py vtables`** credits each vtable store to the Ghidra function whose extent holds it, or reports the store's own address when none does; a raw scan for the store's bytes finds stores in undisassembled code too.
- **`es2_late_entries.py`** reports early starts (a function begun on the fill bytes before its code) beside late ones, and `fixentry` repairs both.

## Stage 1 — mechanical passes

Cheap, tool-driven work that shrinks the backlog before any decompile is read by hand.

- **Cross-binary matching.** Run `es2_naming.py match` in both directions, exact first, then `--fuzzy 0.85`, and confirm each candidate with `diff`. Most of the unnamed code sits in the Dynamix library layer both EXEs link (streams, SOS audio, 3Space shapes, LZH, pools). An exact match between two unnamed functions names nothing by itself; read one side and name both. A short body (a setter, a thunk, a `return arg`) matches many unrelated functions, so a match under about ten instructions is a lead, not a name. VSHELL's AN and timer libraries are non-inlined builds and do not match; read them against the DBSIM family instead. Run the match again after creating functions, since the new ones have twins too.
- **Functions Ghidra never made.** Every `55 8B EC` prologue outside a function is one. Most gap bytes are Borland class records, which the linker places in the code section; the code among them is reached only through tables, so look for the addresses the tables store: the `_INIT_`/`_EXIT_` entries (frameless static constructors and exit routines), the `this`-adjusting thunks in DBSIM's secondary vtables, and the raster driver's routine tables (assembly that opens with `ENTER`, or with no frame at all). Check each start decodes cleanly, define the batch with `apply --define`, then regenerate the dumps. The tools README's "Repairing the database" section has the table addresses and shapes.
- **Late and early starts.** `es2_late_entries.py` for each binary, repaired with `fixentry`, dry run first. Re-address the `known_symbols` entry and every doc and C# mention before the repair.
- **Class-record destructors.** Every class record's `+0x28` destructor without a name, from `classes --unnamed`: the pool, array and stack templates and the classes without a vtable. A template's destructor is named `<Template>_<Argument>_Dtor` (`ObjPool_MECH_Dtor`).
- **Adjusting thunks.** Each is named for the class whose vtable block holds it and the method it jumps to (`SystemGadget_OnClickThunk`); the description names the base subobject and the adjustment.

## Stage 2 — class skeletons

Generate a `known_structs.json` layout for every RTTI class record that lacks one, from the record alone: the class name, its size, each base embedded at its subobject offset, and a vtable pointer typed to the class's vtable shape. Every other byte stays an unnamed run. Type `this` on every function whose name carries the class prefix, through the `applications` list. This is mechanical and covers all 381 records in one pass.

Vtable shapes come with it. DBSIM has shapes for seven families; the rest of its tables have named slot functions but no typed shape, so the decompile shows offsets at indirect calls. The families without one are the 3Space part family (`TSPartBase`, `TSPartList`, `TSGroup`, `TSBSPGroup`, `TSBSPPart`, `TSDetailPart`, `TSCellAnimPart`, `TSBitmapPart`, `TSShape`, `ANShape`, `GridShape`, `hzline`, `CONFIG_PART`), the `TSBase` and poly family, the AN sequences, the GL and stream classes, the owning cockpit displays (`PanelGauge` and below, `HUDGauge` and below, `MFDisplay`, `HDDisplay`), the message ports, the bar graphs and the alert panels. VSHELL already has shapes for the GL, stream and 3Space classes; reuse each for DBSIM after `vtables` confirms the slot functions pair up.

A skeleton names nothing it has not read, so it needs no doc. A class whose layout matters to a port gets its fields in Stage 4.

## Stage 3 — remaining functions in address order

Work each binary's unnamed functions in address order. Borland links each source file as one contiguous block, so an unnamed function sits among named siblings from the same file, and reading in order gives each one its neighbours as context, with Stage 2's types already applied. Name each block's globals as it goes; many `DAT_`s are constants and tables used only by that block.

Prefer address order to `triage --sort refs` here: the backlog is finite and mostly short functions, so completeness matters more than ranking. Apply the [stop rule](#every-name-closes-its-loop) as before: name the callees actually read, no further.

Context the vtable pass gathered for functions in this backlog:

- `0042db40` and `0044041c` are static initialisers that build global `GLRectangleRegion`s; `0043034c` is the palette module's initialiser, reached through a stored pointer at `00430e93`.
- `00453d10` writes a palette file (`PAL:` and `VGA:` chunks) through a `FileRWStream`.
- `00439a0c` is the vertical LED bar's lit-run fill and `00439c48` the bitmap LED bar's span draw.
- `00452144` is the whole body of `PanelAmbience_Paint`.

## Stage 4 — fields, class by class

Fill skeleton fields with `es2_fieldscan.py` sweeps, starting with the classes whose methods are called most and the ones the engine ports. A field gets a name once a reader or writer establishes its meaning; the rest stay unnamed with what is known in the description. This stage has no natural end: a field is never proven unread, so "complete" means every field with an established reader is named. Compare each decoded field against the C# that models the same datum, and apply [rule 2](#every-name-closes-its-loop) to every disagreement.

## Play checks

Behaviour changes made by naming passes, waiting on a look in the running engine. Remove an entry once the user has checked it.

- An AI machine left with only pods and empty magazines counts as disarmed and fires its defeat action (`MechObject.ChooseWeapon`, `WeaponMount.IsSpent`).
- Combat rating drops as ammunition runs low (`WeaponMount.CountsInCombatRating`).
- A damaged energy weapon recharges more slowly (`WeaponMount.ConditionChanged`).
- A missile tower's rockets home on its target (`SimWorld.FireRocket`).
- Structures detect all round, with no sensor arc (`Detection.InSensorArc`).

## Verification

- Stage 0: `stats` reproduces the table's counts from the current dumps.
- Stages 1–3: `es2_naming.py apply BIN` dry run reports no failures before each write, and every batch report lists each named address with its state from [rule 1](#every-name-closes-its-loop).
- Stage 2: spot-check decompiles with `ES2DecompileNamed.java`; a typed method shows field and slot names instead of offsets.
- Throughout: both solutions build with 0 warnings, `dotnet test Herculan/HerculanEngine.sln` passes, and `doc_lint.py` and `doc_links.py` are clean after doc edits.

## Open

- **Open:** what `SimObject_TickNoOp`'s return value of 1 means to the callers of the `+0x14` tick slot.
- **Open:** the roles of `TexPoly_Slot28NoOp` (`TexPoly` `+0x28`) and `CTLWindow_Slot00NoOp` (`CTLWindow` `+0x00`), each named for its empty body only.
- **Open:** what the flag `HddDamageScreen_Repaint` (0) and `HddDamageScreen_Tick` (1) pass to `HddDamageScreen_Update` selects.
