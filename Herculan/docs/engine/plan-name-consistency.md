# Plan — one home per name

Make a field's name and description live in one place, and have the copies that must exist checked by the build or the doc linter instead of by a reader noticing drift.

Stage 1 is built; Stages 2 to 5 are a plan.

## Why

One decoded datum is currently restated in up to ten places. Take the mech type in a mission's mech roster:

| place | spelling |
|---|---|
| [`../formats/msn-mission-file.md`](../formats/msn-mission-file.md), row #12 table | "mech type", `0x30` |
| [`../formats/script-dat.md`](../formats/script-dat.md), block 7 rows | "mech type", `0x30` / `0x28` |
| `HercWorks.Core` `.msn` model | `MechRosterEntry144.TypeIndex` |
| `HercWorks.Core` `script.dat` model | `ScriptMechRecord.TypeIndex` |
| `MissionFileTransformer`, `ScriptDatTransformer` | the parse and write order |
| `MissionGenerator` | a raw word index into a `short[]` |
| `Herculan.Engine` | `MissionPlacement.TypeIndex` |
| `HercWorks.UI` row wrapper | `ScriptMechRow.HercType` |
| `MissionScriptForm.Designer.cs` | the string `"HercType"` |
| `tools/ghidra_scripts/known_symbols.json` | prose in a function description |

The cleanup that prompted this plan found drift in almost every one of those, and three kinds caused most of it:

- **Retail docs naming C# members.** `BinaryFlag`, `SmallDiscrete`, `MiscEntityInfo` and `EntitySpawn164` sat in the format docs after the code had moved on. Rule 9 of `CLAUDE.md` forbids this, but `doc_lint.py` only catches explicit markers, not a bare type or member name.
- **String bindings in the UI.** The grids bind columns by `DataPropertyName = "TriStateFlag"`. A rename compiles cleanly and breaks the column at run time, and `HercWorks.UI` cannot be built on Linux, so a cloud session verifies UI edits by grep alone.
- **Descriptions restated per layer.** The `.msn` model, the `script.dat` export, the UI row and the engine record each carried their own comment for the same datum, and they disagreed.

Renames themselves were the other cost: `RefRow6` meant a spawn point on one type and an unread point on two others, so a text-level rename had to be scoped by hand.

## Constraints

- **The docs keep the evidence.** Value distributions, reader addresses, which offsets an export drops — that is reverse-engineering evidence and stays in the doc tables. Nothing here generates doc tables from code.
- **No new copy.** A central glossary file would be one more place to keep in step. Every step below either removes a copy or puts a check on one.
- **`MissionGenerator` stays a raw-word walk.** It filters, merges and renumbers as it reads, as VSHELL does; it is not to be rebuilt on `MissionFile`'s models.

## Stage 1 — retail docs never name C# members

`tools/scripts/doc_lint.py` rule `csharp-name`, over the retail docs and the `description` strings of `known_symbols.json`, `known_structs.json` and `known_vtables.json`:

- **C# names** are every type declared under `Herculan/src/HercWorks.Core` and `Herculan/src/Herculan.Engine`, plus every `public` property, field and method there, read by regex.
- **Retail names** are exempt: every `name` in the three `known_*.json` files, the class prefix of each (`Text` from `Text_Ctor`), and every identifier in `ES2/DBSIM.EXE` and `ES2/VSHELL.EXE`. The binaries supply the 3Space class names (`TSPoly`, `ANAnimList`) the DTS model reuses; they are gitignored, so a checkout without `ES2/` reports those few as well.
- **Single words pass.** Only a compound — two words run together, or a word and a digit (`SplashFactor`, `Unknown3`) — is flagged; `Height` or `Data` is as likely prose or a retail keyword. A `Type.Member` token is flagged on its type alone. File names (`MECHS.NAM`) and fixed-point notation (`Q10`) pass.
- In a doc the check reads backticked tokens; in a JSON description, which has no backticks, every identifier outside a quoted span — a quoted span there is literal retail text, such as a menu string (`'AutoRepair All Hercs'`) the EXEs do not carry. The JSON pass also runs `engine-mention` without "the engine", which in a plate comment means DBSIM's own 3D engine.
- Findings sit in the `--engine` listing, so a full run counts them per file and the edit hook reports only the lines an edit wrote. The hook also fires on edits to the `known_*.json` files.

With this in place a doc describes a field ("the paints-ground flag, `0x06`") and only code carries its name, so a C# rename never touches a doc.

## Stage 2 — UI bindings that fail to compile

- Replace every `DataPropertyName = "X"` in the `*.Designer.cs` files with `nameof(RowType.X)`. The designer tolerates `nameof` in the generated block; if regeneration would strip it, move the assignments into the form's constructor after `InitializeComponent()`.
- Add a GitHub Actions workflow on a `windows-latest` runner that builds `Herculan/HercWorksMDK.sln`, so a UI break shows up on the branch whichever environment made the edit.

## Stage 3 — one description per datum

The `.msn` model owns the description of a field. Every other layer that carries the same datum inherits it:

- `script.dat` export properties: `/// <inheritdoc cref="MechRosterEntry144.TypeIndex"/>`, adding only what differs (the exported offset, refs being block indices). `ScriptDat.cs` already does this for the action, order, group and objective records.
- UI row wrappers: `inheritdoc` from the export property they wrap, instead of a restated summary.
- Engine records that hold a datum straight from a Core field: `inheritdoc` the Core member, plus the engine's own behaviour where it adds any.

Where an engine name and a Core name differ for the same datum (`ScriptMechRow.HercType` against `TypeIndex`), rename to the Core name unless the difference is the point.

## Stage 4 — named word offsets for `MissionGenerator`

`MissionGenerator` reads fields as `record[37]`, `record[61]`. Give each `.msn` model class `const int` word offsets for the fields the generator touches (`MechRosterEntry144.PairCountWord = 37`, `OutOfActionWord = 38`, and so on), and have the generator use them. The offset then lives next to the field it describes, and a model restructure shows up as a compile error in the generator rather than a silent misread.

A test parses a synthetic record through `MissionFileTransformer` and through the generator's word view and asserts that each named offset reads the same value as its property.

## Stage 5 — a symbol-aware rename tool

Add `tools/scripts/rename_symbol` — a small console project on `Microsoft.CodeAnalysis.CSharp.Workspaces` that loads `HerculanEngine.sln` through `MSBuildWorkspace` and calls `Renamer.RenameSymbolAsync` for one symbol, `cref`s included. Usage: `rename_symbol <solution> <Type.Member> <NewName>`.

This works on Linux for Core, the engine and the tests. `HercWorks.UI` is outside that solution; Stage 2's `nameof` bindings make the remaining UI references compile errors that the Windows build reports.

## Order and cost

Stages 1 and 2 are the cheapest and would have prevented most of the drift the cleanup found. Stage 3 is incremental and can ride along with any edit to a model. Stages 4 and 5 are each a small self-contained piece of tooling.

## Verification

- Stage 1: a planted `` `MechRosterEntry144.TypeIndex` `` or `` `MissionFileTransformer` `` in a format doc is reported by the edit hook; a planted `` `Text_Ctor` ``, `` `Button` ``, `` `MECHS.NAM` `` or `` `Q10` `` is not. A full `--engine` run lists no findings; a quoted `'AutoRepair All Hercs'` in a JSON description passes and an unquoted `ShellHangar.AutoRepair` does not.
- Stage 2: renaming a row property without touching the designer fails the Windows build.
- Stage 4: the offset test passes, and moving a property in a model without updating its constant fails it.
- Stage 5: renaming `MissionGroup164.MemberKind` and back leaves `git diff` empty.

## Open

- **Open:** whether the Windows Forms designer preserves `nameof` in `InitializeComponent` on regeneration, which decides where Stage 2's assignments go.
- **Open:** whether `MSBuildWorkspace` loads `HerculanEngine.sln` on Linux without the WindowsDesktop SDK, given the solution does not include `HercWorks.UI`.
