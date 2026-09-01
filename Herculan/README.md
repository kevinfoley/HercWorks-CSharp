# HercWorks MDK — C# / WinForms port (in progress)

Porting [herc-works-mdk](https://github.com/Subject9x/herc-works-mdk) (Earthsiege 2
modding toolkit, MIT licensed) from Java to C#/.NET 8 + WinForms. Original project is
310 Java files / ~30,000 lines across four modules — this is being ported in stages
rather than all at once.

Bugs found in the original Java source during porting were kept bug-compatible rather than silently
fixed, per the approach described below. They are recorded in the per-round notes further down this
README. [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) is a different register: retail Earthsiege 2 bugs, and
places where the HERCULAN engine behaves differently from retail.

## Status

| Module | Java LOC | Status |
|---|---|---|
| ES2Vol → `HercWorks.Vol` | ~1,300 | **Done** — `.vol` archive reader/writer |
| ES2Core → `HercWorks.Core` | ~18,000 | **In progress** — everything done except `io/transform/dbsim` (12 files) and `io/transform/shell` (10 files) |
| ES2TransferApi | ~8,700 | Not started |
| ES2Excavator (CLI) | ~2,100 | Not started (logic will become WinForms menu actions) |
| WinForms UI | n/a | Shell in place: open/browse/unpack a `.vol` file |

### ES2Core progress detail

**`io/read/` and `io/write/` — both complete.**
- `io/read`: `DynFileReader` (load/parse DPL/DBM/DBA files), `DatFileReader` (parses `InitHerc`
  stats out of `.DAT` files), and `VolFileReader` — a near-duplicate of
  `HercWorks.Vol.Io.VolFileReader` in the Java original (same situation as `io/write`'s
  `VolFileWriter` below), ported as a thin delegating wrapper rather than re-implementing the
  same logic twice. The Java original also carried several debug-only/fully-commented-out
  methods here that added no behavior and weren't ported.
- `io/write`: `VolFileCompiler` (compiles a **brand-new** VOL from scratch, computing fresh
  offsets/sizes — distinct from the "strict" round-trip writer) and `VolFileWriter` — again a
  near-duplicate of `HercWorks.Vol.Io.VolFileWriter`, ported as a thin delegating wrapper.
  `DynFileWriter` (exports DBM images to PNG/BMP via `System.Drawing.Bitmap`, and DBM objects back
  to `.DBM` bytes) lives in `HercWorks.UI` instead, not `HercWorks.Core.Io.Write` — it's an MDK
  export feature the engine port will never call, and `Core` otherwise has no `System.Drawing`
  dependency at all.

**Bugs found and ported literally (not silently fixed) this stretch — one is more than cosmetic:**
- `DatFileReader.ParseIniHercDatStats()`: initializes a hardpoints map, then loops constructing
  `UiWeaponEntry` objects for each hardpoint — but **never actually inserts any of them into the
  map**. The map comes back empty after parsing. Unlike the earlier cosmetic/round-trip bugs,
  this one looks like it would produce visibly wrong (empty) data if exercised on real files.
  Flagged clearly in code; ported bug-compatible since I have no real game data to confirm
  whether something else compensates.
- `DatFileReader.ReplaceDatBytes()`: despite its name, the `newData` parameter is never used —
  it just concatenates the file's existing `Header` and `RawBytes` unchanged.
- `VolFileCompiler.Compile()`: writes to a hardcoded developer path
  (`E:\ES2_OS\dev\earthsiege2\VOL`) carried over directly from the Java original — flagged as a
  rough edge rather than something safe to silently redirect, since the "correct" output path is
  presumably meant to come from the caller in a real UI flow.

**Not started:** `io/transform/dbsim` (12 files), `io/transform/shell` (10 files) — the last ~22
files in ES2Core.

### ES2Core progress detail

**`data/file/*` — complete (102 of 102 files).** This is the full per-file-type data model layer:
every DBSIM/VSHELL game file type has a corresponding C# class, organized to mirror the Java
package structure:
- Top-level: `StringFile`, `StringBinaryFile`, `FileClassDefs` (the file-type registry tying
  everything together — see below)
- `bnd/` (5), `cfg/` (6) — config/binding files
- `dat/shell/` (14), `dat/sim/` (7) — herc armory/repair UI data, career missions, weapons list,
  herc simulation stats, beam/missile/projectile data, zone data
- `dbsim/` (8) — flight model, gun layout, collision, damage, paper-doll UI, world data
- `dts/` + 4 subpackages (27) — the ThreeSpace 3D model format: object headers, base
  part/group/poly hierarchy, animation sequences, BSP tree nodes, part types
- `dyn/` (5) — Dynamix bitmap/bitmap-array/palette/grid-shape/3D-model file types
- `gau/` (10) — HUD widget config (panels, buttons, labels, meters, weapon panels)
- `msn/` + `msn/script` (14) — mission files, map objects/coords, unit/entity spawn data, the six
  `UnkEntity*` byte-layout classes, mission strings
- `sav/` (3) — player save file, mission strings, .mec file

**`io/transform/common/` — complete (7 of 7 files).** The byte<->object transformers for file
types shared across DBSIM/VSHELL: `DynamixBitmapTransformer`, `DynamixPaletteTransformer`,
`DynamixBitmapArrayTransformer`, `BinStringFileTransformer`, `MissionStringFileTransformer`,
`MissionFileTransformer`, `PlayerSaveTransform`.

**`io/transform/ThreeSpaceByteTransformer`** — the shared byte-cursor base class every
reader/writer/transformer extends — was ported earlier. Applying the verified `Bytes`-library
semantics (see below) turned up two of the same "name says one thing, does another" pattern:
- `IndexSegmentLE()` calls `.byteOrder(LE).array()` — since `.array()` ignores that tag, this
  method is **byte-identical to `IndexSegment()`** despite its name. Ported literally.
- `PeekAt()` doesn't actually read/dereference anything — it just returns `index + at` as a
  number. Looks unused/unfinished in the original.
- By contrast, `IndexShortLE`/`IndexShort`/`IndexIntLE` (call `.toShort()`/`.toInt()`, not just
  `.array()`) and `WriteIntLE`/`WriteShortLE` (call `.reverse()`) genuinely are correct
  endian-aware reads/writes — confirmed by the same source-level check.

**Bugs found and ported literally (not silently fixed), same policy throughout:**
- `TSShape.jsonString()`: prints `getSequenceList()` twice instead of `getSequenceList()` then
  `getTransformList()` — looks like a copy/paste bug.
- `HMeter`'s constructor calls `setOrigin(getOrigin())` instead of `setOrigin(origin)` — assigns
  `Origin` to its own current (null/default) value rather than the constructor parameter, so the
  `origin` argument is effectively unused. Looks like a bug.
- `InitHerc.header` / `DynamixPalette.header`: both build their header bytes via
  `Bytes.from("<hex-looking-string>", UTF_8)` — despite looking like hex, this is literally the
  ASCII bytes of that 8-character text, not a decoded hex value. Ported literally both times.
- `DynamixPaletteTransformer`: the write path outputs color channels as R, B, G (blue and green
  swapped) while the read path reads them as R, G, B — a real round-trip bug (write-then-read
  would swap green and blue).
- `DynamixBitmapArrayTransformer`: reverses `FileSize` bytes on write but not on read, so the
  byte order is inconsistent between the two directions.

**Design notes:**
- Java's "rich enum" pattern (id/name/lookup-table, common throughout this codebase) has no
  direct C# equivalent — ported as sealed classes with static readonly instances (same
  `GetById`/`GetByName`-style API), not a literal `enum`. `HWidgetId` needed an extra `Name`
  property to stand in for Java's `.name()` (the enum constant's own identifier), since several
  `toString()` overrides display it. `Values()` was added to `WeaponLUT`, `HercLUT`,
  `HercExternals`, `HercInternals`, and `MissileType` to stand in for Java's `.values()`, needed
  once transformer code started iterating over all constants.
- `java.awt.Point`/`Dimension` → `System.Drawing.Point`/`Size`; `java.awt.Color` →
  `System.Drawing.Color`; Apache Commons Math's `Vector3D` → `System.Numerics.Vector3`.
- `FileClassDefs.cs` needed explicit `using` type-aliases (`T_ArmHerc`, etc.) because, like the
  Java original, every static field is named identically to the class type it points to —
  without the aliases, `typeof(ArmHerc)` inside the `ArmHerc` field's own initializer would be
  ambiguous.
- Replaced the earlier placeholder `DynamixBitmapArray` stub (written ahead of that package
  existing, several rounds back) with the real port once `data/file/dyn/` was reached.

**Not started:** `io/read` (3 files), `io/write` (3 files), `io/transform/dbsim` (12 files),
`io/transform/shell` (10 files) — ~28 files left in ES2Core, all in the same
highest-risk-but-now-well-understood byte-parsing category as `transform/common` above.

## Structure

```
HercWorksMDK.sln
src/
  HercWorks.Vol/         class library — ported ES2Vol module
    FileType.cs
    DataFile.cs
    VolEntry.cs
    VolDir.cs
    Voln.cs
    Io/VolFileReader.cs
    Io/VolFileWriter.cs
    Util/ByteOps.cs       little-endian byte helpers (replaces the Java favre 'Bytes' lib)
  HercWorks.UI/           WinForms shell (net8.0-windows)
    Program.cs
    MainForm.cs
tests/
  HercWorks.Vol.Tests/    xUnit round-trip test against a hand-built synthetic .vol
```

## Building

Requires the .NET 8 SDK (Visual Studio 2022 17.8+, or `dotnet` CLI). Open
`HercWorksMDK.sln` in Visual Studio, or:

```
dotnet build
dotnet test
dotnet run --project src/HercWorks.UI
```

I don't have a .NET SDK available in the sandbox this was written in, so this hasn't
been compiled — please flag any build errors and I'll fix them.

## Notes on the port

- **Byte handling**: the Java code used the `at.favre.lib.bytes` library with explicit
  `.byteOrder(...)` tags. All VOL numeric fields are little-endian on disk; this port
  uses a small `ByteOps` helper instead of `System.BitConverter` so the endianness is
  explicit rather than depending on host byte order.
- **Round-trip fidelity**: like the original, the "strict" writer (`PackVolToFileStrict`)
  writes header/count fields back out from the parsed object's numeric values, but
  writes file-list entries and file data back out from the *exact raw bytes* that were
  read — matching the original Java javadoc's intent ("write it back out EXACTLY as it
  was loaded").
- **Bug fix**: the original Java used `File.pathSeparator` (`;` on Windows) instead of
  `File.separator` when joining folder/file paths, which would have produced invalid
  paths. This port uses `Path.Combine` instead.
- **`byte` vs `sbyte`**: Java's `byte` is signed; all values actually used here (directory
  counts/indices) are small and non-negative, so this port uses C#'s unsigned `byte`
  throughout without any behavior difference.

## Next steps

`io/transform/dbsim` (12 files) and `io/transform/shell` (10 files) — the last per-file-type
byte transformers in ES2Core, following the same pattern as `io/transform/common`. After that,
ES2Core is fully done and the remaining work is ES2TransferApi (~8,700 lines, JSON DTO layer)
and ES2Excavator (~2,100 lines, CLI logic to become WinForms menu actions).
