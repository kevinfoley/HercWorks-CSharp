# HERCULAN Engine — Planning

Living planning document for the Earthsiege 2 engine port, tracking decisions as they're made.
Started 2026-08-11. This is a working document, not a spec — update it as decisions change.

## Context

Long-term goal: a modern, cross-platform engine capable of running Earthsiege 2 using the
original game's data files. `Herculan` (the "HercWorks" toolkit) is a separate,
already-underway WinForms tool for reading/editing those data files — it is a stepping stone, not
the engine itself, and the engine does not depend on it. See the honest scope assessment in
project memory (`project-es2-engine-port-readiness`) for what RE work is and isn't done yet.

## Design principles

### Vanilla by default

All behavior matches the original game exactly by default. The only exceptions are purely
cosmetic changes with no gameplay effect (e.g. resolution). Anything that changes behavior —
fixing bugs that exist in the original, raising limits, increasing mathematical precision, etc. —
is opt-in, not on by default, likely surfaced later as in-game settings rather than being baked
into the engine's normal behavior.

This is the general principle the math subsystem's "nice to have: switch to modern math" goal
(below) is one instance of: default to the original's exact (fixed-point) behavior, architect
things so a non-vanilla alternative *can* be switched in later, but never make the non-vanilla
behavior the default.

## Settled decisions

### Name

**HERCULAN Engine.** Chosen over "SiegeTech Engine" (clean but generic), "Prometheus Engine"
(rejected — collides with the well-known Prometheus metrics/monitoring project and multiple
existing GitHub projects literally named `prometheus-engine`), and "Gierling Engine" (memorable
but easy to misspell, and opaque outside ES2 fandom). HERCULAN ties directly to the in-fiction
mech name, had no notable naming collisions, and reads sensibly even to someone unfamiliar with
the game.

### Language & runtime

- **C#**, not C++. The deciding factor isn't raw performance — it's that `HercWorks.Core`
  already represents substantial, hard-won reverse-engineering work (file formats, DBSIM sim
  math) and is directly reusable from C#. Rewriting that layer in C++ would cost more time than
  C++'s performance ceiling would ever save back, for a game in ES2's performance class.
- **Modern .NET (8/9/10+), not Mono.** Mono was the historical answer for cross-platform C#
  (Xamarin, Unity, old MonoGame) but modern .NET has been natively cross-platform
  (win-x64/linux-x64/osx-arm64/etc.) since .NET Core, generally outperforms Mono's JIT, supports
  NativeAOT publishing, and is where current investment goes. Mono would only be the right call
  for platforms specifically requiring it (iOS AOT, some consoles) — not applicable to a desktop
  Windows/Linux/macOS target.

### Rendering

- Start with **OpenGL**, with an eventual goal of also supporting **Vulkan** (user-selectable
  backend).
- Bindings: **Silk.NET** over OpenTK — covers GL, Vulkan, and windowing/input in one
  actively-maintained library, so adding Vulkan later doesn't mean adopting a second binding
  ecosystem.
- Don't over-build the GL/Vulkan abstraction layer up front. An abstraction designed against a
  single backend tends to bake in assumptions (implicit state, no explicit sync) that don't map
  cleanly to Vulkan. Get OpenGL working concretely first; generalize the render interface when
  Vulkan support is actually being added and its real requirements are visible.

### Repo & project structure

- **Single repository** (`E:\ES2Stuff`, current repo). Explicitly rejected git submodules —
  the usual submodule advantage (independent versioning of a dependency) doesn't apply here,
  since Core/UI/Engine are developed in lockstep by the same person against the same evolving RE
  findings. One clone, one `.sln`, atomic cross-layer commits.
- **Engine lives as sibling project(s) to `HercWorks.UI`**, under
  `Herculan/src/`, added to the existing `HercWorksMDK.sln`. Both the engine and
  the WinForms UI reference `HercWorks.Core` / `HercWorks.Vol` directly as the shared data layer.
  Neither depends on the other — this was "Option A" of two structures considered (the
  alternative, UI depending on the engine as a library, was rejected: the WinForms tool has no
  need for engine-only dependencies like Silk.NET or a physics/sim loop).
  - `HercWorks.TransferApi` (UI-facing DTOs) is UI-only plumbing; the engine bypasses it and
    talks to `HercWorks.Core` domain types directly.
- Confirmed via inspection: `HercWorks.Core` and `HercWorks.Vol` already target plain `net8.0`
  (not `net8.0-windows`) with no WinForms references, so this separation is already mostly in
  place — only `HercWorks.UI` is Windows-only (`net8.0-windows`, `UseWindowsForms`).

### Simulation object architecture

**Traditional OOP / virtual dispatch, matching the original — not ECS.** This is grounded in
actual RE evidence, not a guess about 1996-era convention in general: DBSIM.EXE's simulation
objects are built on a shared base-object constructor helper (`FUN_00402188`) called by every
`SimObject`-derived class right after its vtable pointer is set. The Mech class sets a 34-slot
vtable; rockets/bullets set smaller 6–9-slot vtables. Known virtual methods already identified
include a hit-radius getter (`obj[+0x5c]`), the damage-application method (`obj[+0x70]`), and a
part-position getter (`obj[+0x58]`) — see `project_es2_exe_recon` memory and
`docs/simulation/dbsim-physics-notes.md`.

Plan: a `SimObject` abstract base class in the engine with virtual overrides mirroring the
discovered vtable shape (Mech, Rocket, Bullet, Flyer, ...), rather than a component/system model.
Two reasons this isn't just fidelity for its own sake:
- **Scale makes ECS's advantage moot** — DBSIM's entity counts (a handful of mechs, a few dozen
  active projectiles) are far below where cache-locality/iteration-cost concerns matter.
- **It de-risks the port** — the actual vtable shape for several classes is already known from
  Ghidra, so a base-class-plus-overrides translation is close to literal, versus re-deriving the
  same behavior inside a component model designed from scratch.

This decision is scoped to simulation objects specifically. Rendering/scene representation is a
separate question and isn't required to follow the same pattern.

### Physics

**Custom, exact match to the original.** Not adopting an off-the-shelf .NET physics library
(e.g. BepuPhysics) — the goal is to reproduce DBSIM's actual behavior, which has already been
substantially reverse-engineered: the fixed-point math toolkit, hierarchical bounding-sphere
collision setup, rocket/bullet per-tick integration, terrain height query (bilinear/barycentric
with per-cell diagonal selection), and the per-mech damage formula (facing-based hit-zone budget,
per-part random-roll, linear blast-radius falloff). See
`docs/simulation/dbsim-physics-notes.md` for the full detail — this is the primary porting target
for the physics/sim subsystem, not a reference to design against.

### Math

**Custom, exact match to the original** — port the actual fixed-point math toolkit found in
DBSIM (Q8/Q10/Q14 fixed multiply, "integrate a rate over one tick," rate-limited "move toward,"
the sqrt-free fast 3D magnitude approximation) rather than using floating-point
`System.Numerics` throughout.
- **Nice-to-have, not required for v1:** architect this behind an abstraction so the engine could
  later switch to modern floating-point math without a large rewrite. Apply the same caution as
  the rendering-backend abstraction above — don't design the swap layer in detail before there's
  a second implementation to validate it against; get the fixed-point implementation working
  first, keep it behind clean types rather than raw arithmetic scattered through call sites, and
  generalize only when/if a second backend actually gets built.

### Audio

**OpenAL via Silk.NET.** Consistent with the rendering-binding choice (Silk.NET already covers
GL/Vulkan/windowing/input, and also wraps OpenAL) — one binding library rather than a separate
audio dependency. This choice doesn't depend on resolving the still-unsolved `.SNC` format; that
remains an open RE gap (see below) but doesn't block picking the playback library.

### Target platform

Primary development/testing target is **Windows**, but OS-specific code paths should still be
abstracted from the start (consistent with the modern-.NET cross-platform decision above and
Silk.NET's cross-platform windowing) so Linux/macOS support doesn't require rework later — just
isn't the near-term testing priority.

### Engine internal architecture

- **Library core + thin front-end host.** Engine subsystems (rendering, scene, etc.) should be
  built as libraries with no baked-in assumption that there's exactly one game loop consuming
  them. A separate, minimal host project wires those libraries into an actual real-time game
  loop.
- Motivation: a possible future mission editor that renders the mission environment in-engine.
  If the editor is just another thin host on top of the same engine libraries, supporting it
  later doesn't require restructuring the engine. Not designing the editor itself now — just
  keeping the core/host separation clean so it stays an option.

### First milestone

**Load one real zone's terrain, spawn one mech with a hardcoded/stubbed loadout, get a camera
moving through it using the actual ported physics/math.** Deliberately excludes weapons/combat
and textured rendering — see RE gaps below.

Chosen over a VSHELL-first milestone. The original's VSHELL→DBSIM launch order is a UI/workflow
gate, not a data dependency: VSHELL's actual job before launching DBSIM is parsing the `.msn` and
writing `data\script.dat`, which is the file DBSIM itself reads (DBSIM never touches `.msn`
directly). Since the `.msn` parser and `script.dat` export are already understood byte-exact, a
real `script.dat` can be handed to the engine directly, without building any of VSHELL's
mission-select/loadout/armory UI. Reasoning for going DBSIM-first instead:
- Targets the genuinely unbuilt, highest-risk parts of the project (render pipeline, real-time
  sim loop, engine plumbing) rather than VSHELL's UI, which is comparatively low-risk — closer in
  kind to the WinForms tool already built several times over.
- The milestone's hardcoded loadout is normal, low-risk technical debt: it gets replaced by real
  VSHELL-driven data once that layer exists, not redesigned.

### Scaffolding status

**Initial project structure created (2026-08-11).** Two new projects under
`Herculan/src/`, added to `HercWorksMDK.sln`:
- **`Herculan.Engine`** — the library core (`net8.0`, cross-platform). References
  `HercWorks.Core`/`HercWorks.Vol` directly (no `HercWorks.TransferApi`, per the repo-structure
  decision above). Packages: `Silk.NET.Windowing`, `Silk.NET.OpenGL`, `Silk.NET.Input` (audio/
  `Silk.NET.OpenAL` not added yet — not needed until audio work starts). Currently contains just
  `EngineWindow.cs`, a thin wrapper around a Silk.NET window + GL context that opens a window and
  clears the screen — proves the pipeline compiles and runs, no scene content yet.
- **`Herculan.Engine.Host`** — the thin front-end host (`net8.0` console app), per the
  "library core + thin front-end host" decision above. References `Herculan.Engine` only.
  `Program.cs` just constructs an `EngineWindow` and runs it.

Full solution builds with zero errors (`dotnet build HercWorksMDK.sln`). Not yet run/verified
interactively (opening an actual window) — that's left for manual verification rather than trying
to smoke-test a blocking windowed loop headlessly.

### Milestone 1 — implemented (2026-08-11)

All three parts of the first milestone are built and the full solution compiles clean in Debug and
Release. Verified headlessly against the real `E:\ES2Stuff\ES2` install (loading, terrain query and
camera motion all exercised without opening a window); actually looking at the rendered result is
manual verification, deliberately left to the user rather than smoke-tested here.

**What's in `Herculan.Engine` now, by area:**

- **`Numerics/`** — literal ports of DBSIM's fixed-point toolkit, one method per RE'd function:
  `SimMath` (Q8/Q10/Q14 multiply, integrate-rate-over-tick, countdown timer, rate-limited
  move-toward, the sqrt-free 3D magnitude approximation), `Vec3i` (integer world position, X/Y
  ground plane + Z up, distance via the approximation rather than a real `sqrt`), `BinaryAngle`
  (BAM angles, Q14 trig), and `SimRandom` (the additive lagged-Fibonacci generator at
  `FUN_00492dd4`). Namespace is `Numerics`, not `Math`, so it doesn't shadow `System.Math`.
- **`Terrain/`** — `HeightGrid` (the 0x129 struct, minus the 14 known-dead bytes per cell),
  `TerrainZoneLoader` (`Terrain_LoadZone` → `TerrainZone_LoadHeightmap` →
  `TerrainZone_PopulateFromBitmap`, reading the 16-byte `dat\zoneNNNN.dat` header and the
  `dba\zoneNNNN.dba` heightmap image), `TerrainMaterialTable` (`dat\mat0`). `HeightAtWorld` is a
  line-by-line port of `Terrain_HeightQuery`, integer arithmetic and all, including the original's
  east-edge index wrap — kept per "vanilla by default" rather than fixed.
- **`Content/`** — `GameContent` mounts the game's own VOLs and resolves `folder\name` lookups with
  the header's load-precedence byte deciding which archive wins; `GameInstall` finds an install.
- **`Sim/`** — `SimObject` (the OOP base the architecture decision above calls for, currently
  carrying only the slots in use), `SimWorld` (fixed-timestep tick over the object list, owns the
  timestep global), `MechObject`, `FlyCameraObject`.
- **`Render/` + `Gl/`** — `ShaderProgram`, `GpuMesh`, `SceneRenderer` (one directional light,
  ambient, distance haze), `Camera`, `TerrainMeshBuilder`, `DtsMeshBuilder`, and `WorldScale`, the
  single float boundary.
- **`Scene/ZoneScene`** — CPU-side scene assembly, so a host uploads meshes but a headless caller
  can build the same scene with no GL at all.

`Herculan.Engine.Host` stayed thin: locate install, build the scene, upload meshes, translate keys
to sim input, run a fixed-timestep accumulator, draw. It takes optional `<installPath> <zone>
<mech>` arguments and defaults to zone 504 with SAMSON.

**Things worth knowing that came out of building it:**

- **Terrain triangulation matches the height query by construction.** The renderer splits each quad
  along the same diagonal that cell's selector bits choose, so the surface drawn is the surface the
  simulation queries — which heads off an entire category of "why is the mech floating" bugs rather
  than correcting for them with an offset later.
- ~~**World scale: ~200 world units per metre**~~ — **superseded 2026-08-13, see "World scale —
  recovered" below.** The estimate was 20% low; the real figure is 166.667 units per metre, read out
  of DBSIM rather than triangulated.
- **DTS model units are world units, 1:1.** Measured, not assumed, and since **confirmed** — see
  "World scale — recovered" below. Note this differs from the WinForms viewer's 1/10 scale, which is
  arbitrary framing for its own window.
- **A mech `.DTS`'s 7 roots are an LOD chain, root 0 highest.** Confirmed by triangle counts across
  SAMSON/OUTLAW/APOCA (253/251/249/210/132/54/11 for SAMSON) over identical bounds. Nothing in the
  file says so — that's engine knowledge — so the engine picks root 0 and says why.
- **Matrix uniforms upload with `transpose: false`, and the reasoning inverts easily.** System.Numerics
  is row-vector (`v * M`) stored row-major; GLSL is column-vector (`M * v`) and reads a uniform array
  column-major when transpose is false — so writing System.Numerics' elements out in their own
  row-major order and *not* transposing hands GL the transpose, which is exactly the column-vector
  form of the same transform. The first build got this backwards and rendered nothing but the clear
  colour: the translation landed in the bottom row, where a column-vector multiply ignores it, and
  the perspective divide produced a negative `w` that clipped everything away. The note in
  `ShaderProgram.SetMatrix` spells this out because "row-major vs column-major" alone is the wrong
  mental model for it — the vector convention is the load-bearing half.
- **`Herculan.Engine` still has no `System.Drawing` dependency.** `DtsMeshBuilder` is a separate
  type from the UI's `DtsGeometryBuilder` for exactly this reason; the tree-walking rules are the
  same and each is annotated with what the other established, so they're worth keeping in sync.

**Deliberately not done, and why:** no textures (excluded from the milestone, and the DBSIM-side
atlas convention is still unconfirmed), no weapons or damage — so `SimObject` does not yet declare
the damage-related vtable slots (`+0x20`/`+0x70`/`+0x74`), since declaring them now would only mean
stubbing them everywhere; no mech locomotion (the milestone's mech is stationary); no backface
culling, matching the WinForms viewer's finding that DTS geometry isn't reliably wound.

**New open items this work surfaced** (all recorded at their call sites too):

- **Nothing writes the terrain diagonal-selector's bit 1.** Decompiling both loader paths settles
  what the physics notes left open: every flag write in `TerrainZone_PopulateFromBitmap` and its
  ASCII counterpart masks with `& 2`, preserving bit 1 and clearing bit 0, and the cells arrive
  zeroed — so both loaders leave every cell's selector at 0. Some other, not-yet-located code must
  set bit 1, since the query handles selector 2 and the value has been observed. The engine
  reproduces the loaders exactly rather than inventing a diagonal rule. **Still open after the
  2026-08-13 terrain session**, and the terrain-renderer theory for who sets it is now **disproved**:
  with the render path located, `Terrain_DrawCellQuad` and `FUN_0046ff74` both only ever read
  `cell[+0xf]`. See `docs/formats/terrain-texturing.md`.
- ~~**The `HeightGrid` LOD field (`+0x10c`) has no located consumer.**~~ **Found 2026-08-13:**
  `maybe_Terrain_ComputeViewDistance` (`00470910`) scales it from cells into world units by
  `<< cellShift`, clamping to 1000 near grid edges, once per frame. Treat the scaling as solid and
  the two output values' meaning as undecoded — see `docs/formats/terrain-texturing.md` before
  wiring it to any far-clip or haze constant.
- **`SimRandom`'s 56-entry seed table hasn't been extracted** from DBSIM's data section. The
  algorithm is a literal port; the seeding isn't. Bit-exact parity would need more than the table
  anyway — a roll's result depends on how many times the generator was already advanced — so
  anything built on it should be treated as statistically faithful, not replay faithful. Currently
  drives only terrain material bits, which nothing renders yet.
- **The real timestep value and its unit are unknown, but 30 Hz now looks wrong.** `SimTickDelta`
  (`DAT_004d3be8`) is written in exactly one place — `Sim_MainTick` copies it out of field `0x17` of
  a per-frame timing struct returned by `FUN_0045a7f4`, which was not traced further. The scale
  recovery above bounds it indirectly: at 166.667 units/metre, mech `SpeedForward` values only
  reproduce the manual's quoted KPH if the tick is near 15 Hz, and a 30 Hz tick would make every
  HERC twice its quoted speed. `SimWorld` still runs 30 Hz with a Q8 tick delta of 256; treat that
  as a suspect default to revisit when locomotion is implemented, not as a neutral one. (The known
  0x500/tick rocket turn cap becomes a revolution in ~3.4 s at 15 Hz, still sane.) Separately,
  `Time_GetCoarseTicks` (`00467724`) is *not* this clock — it is `GetTickCount() >> 4`, a 16 ms
  UI/event timebase the mission clock counts in.
- **DBSIM's own sine/cosine table hasn't been located** — no trig function appears in the current
  symbol set at all. `BinaryAngle`'s table is generated at Q14 rather than ported, which is fine for
  a camera but would show as slow drift in anything integrating a heading over many ticks. All trig
  goes through that one type so swapping in the real table is a single-file change.
- **The per-type hit-cylinder radius (`typeRecord+0x1a`) isn't mapped onto a `HercSimDat` field.**
  The in-memory mech type record is assembled from more than the `.DAT`, and its offsets don't line
  up with the parsed file's. `MechObject` takes a radius derived from model bounds meanwhile.

## Known technical debt relevant to the engine

- ~~`HercWorks.Core` uses `System.Drawing.Common`~~ — **resolved (2026-08-11).** Migrated
  `Core`'s ~18 affected files off `System.Drawing`: added cross-platform-safe
  `HercWorks.Core.Data.Struct.PixelPoint`/`PixelSize`/`RgbaColor` value types (matching field
  names/`ToArgb()` bit layout for a mechanical migration and easy conversion back to GDI+ types at
  a UI boundary), used throughout the GAU widget classes, `DynamixPalette`/`ColorBytes`, and
  `PaperDollGraphic`. `DynFileWriter` (the one file doing real pixel-level image encoding, not just
  carrying position/color data) was initially moved to `SixLabors.ImageSharp` instead of
  `System.Drawing.Bitmap` — but that surfaced a follow-on problem (next item) and was superseded by
  moving the file to `HercWorks.UI` instead. `HercWorks.Core.csproj` no longer references
  `System.Drawing.Common` or any image-encoding package at all. `HercWorks.UI`'s
  `DynamixImageRenderer.cs` (the one UI consumer of `ColorBytes.GetColor()`) converts
  `RgbaColor` → `System.Drawing.Color` at the UI boundary — appropriate since `HercWorks.UI` is
  Windows-only and free to keep using GDI+ directly. Full solution compiles with zero `CS` errors
  (verified via `dotnet build`).
- ~~`SixLabors.ImageSharp` requires a commercial license in Release builds~~ — **resolved
  (2026-08-11).** The `ImageSharp` swap above (`Core` v4.1.0) built fine in Debug but errored out
  in Release under Six Labors' split-license enforcement. Root cause turned out to be a
  misplacement, not a licensing problem to work around: `DynFileWriter` dumps parsed `.DBM` data to
  `.png`/`.bmp` files on disk for a human to inspect — an MDK export feature the engine will never
  call (the engine consumes `DynamixBitmap`/`DynamixPalette` data directly, never writes debug
  image files) — so it had no reason to live in `Core` in the first place. It was the only file in
  the whole `Core` migration doing real pixel-level image encoding; every other touched file just
  carries position/color data via the new `PixelPoint`/`PixelSize`/`RgbaColor` structs. Moved
  `DynFileWriter` to `HercWorks.UI` (same file, rewritten against `System.Drawing.Bitmap`/GDI+,
  same pattern as `DynamixImageRenderer.cs`) and dropped the `SixLabors.ImageSharp` package
  reference from `HercWorks.Core.csproj` entirely. `Core` now has zero image-related dependency of
  any kind, and the licensing question doesn't apply anywhere in the project. Verified via a clean
  `dotnet build -c Release` with no errors and no license warning.

## World scale — recovered (2026-08-13)

**1000 world units are 6 metres.** `WorldScale.WorldUnitsPerMeter` is now `1000/6` ≈ **166.667**,
replacing the estimated 200 that milestone 1 shipped with. This is not a better estimate — it is the
original's own constant, and it closes the open metres-per-texel discrepancy that was blocking
terrain texturing.

**Where it comes from.** The HUD prints distances to the player in metres, so DBSIM has to state its
scale somewhere, and `Hud_WorldUnitsToMetres` (`00434228`) is the whole of it:

```
metres = (worldUnits / 1000) * 6
```

Three call sites in two unrelated gadgets share it — the HUD waypoint indicator's
`WAYPOINT n: d M.` string (`Hud_UpdateWaypointIndicator`, `0043c3e4`) and the scanner MFD's
contact-range readout (`0043ebe0`/`0043eecc`) — and both hand it a raw difference of two world
positions (`Vec2_Subtract` then `Math_FastMagnitude2D`), so its input really is world units. A
gameplay screenshot showing `WAYPOINT 1: 72 M.` is consistent: 72 is a multiple of 6, which every
value this function can produce must be.

**What the world measures at that scale**, all previously stated 20% small:

| | world units | metres |
|---|---|---|
| terrain cell (`CellShift` 14) | 16384 | 98.3 |
| retail zone (128 x 128 cells) | 2097152 | 12580 (12.6 km) |
| zone 504's highest ground | 23393 | 140 |
| missile ground-impact blast radius | 3000 | 18 |
| mech death explosion | 2000 | 12 |
| rocket proximity warning | 40000 | 240 |
| SAMSON model, bounding box height | 2364 | 14.2 |

**DTS model units are world units — now confirmed, not just plausible.** Two fields of
`dat\<mech>.DAT`, a file the sim reads in world units, carry values that are only meaningful as
model-space measurements. COLOSSUS is the one retail mech whose model dips below model-space zero,
to `-400`, and it is the one retail mech with a nonzero `UnitOffsetYAdjust`: exactly `400`. And
`AiAimTargOffset` (how high up a target the AI aims) tracks model height across the fleet — 1500 for
OUTLAW's 1700-unit model, 2500 for everything larger (2030–2575). Nothing in the load path scales a
model: `MechType_InitOne` hands DTS points straight to the shape instance.

**The one thing that does not line up, and why it is not a problem.** Measured against 166.667,
HERC models stand 10.2 m (OUTLAW) to 15.5 m (OGRE), roughly 1.5x the heights the manual's HERC specs
quote (6.1 m and 10.4 m). The gap is bounding box versus quoted stature — a model's box includes
raised weapon arms and antennae — and the ordering by weight class matches the manual exactly. Note
this gap existed at the old estimate of 200 too (SAMSON 11.8 m against a quoted 9.2 m); the new
constant widens it rather than creating it.

**A second, independent check that the scale is at least the right order.** The HUD's speed readout
(`Mech_GetDisplaySpeedKph`, `0041bb3c`) reduces to `speed * 315/1024`, and run against each mech's
own `SpeedForward` it reproduces the manual's quoted speeds across the fleet: OUTLAW 325 -> exactly
100 KPH, SAMSON 190 -> 58 against a quoted 60, COLOSSUS 180 -> 55, MAVERICK 285 -> 88 against 90.
**Do not use it to derive the tick length**: the 315 looks fitted to make the fastest HERC read a
round 100, and inverting it against 166.667 units/metre implies a ~14.2 Hz tick, which is close
enough to a plausible 15 Hz to be suggestive and far enough from round to be untrustworthy. The
simulation timestep therefore stays an open question (see below), just a better-bounded one — a
30 Hz tick would put every mech at double its quoted speed, so **whatever the timestep is, it is
much nearer 15 Hz than 30**, and `SimWorld`'s current 30 Hz is now a known-suspect default rather
than a neutral one.

New symbols from this pass, all applied via `ES2ApplySymbolNames`: `Hud_WorldUnitsToMetres`,
`Hud_UpdateWaypointIndicator`, `Hud_UpdateSpeedReadout`, `Mech_GetDisplaySpeedKph`,
`Math_Q10Multiply`, `Math_Q16Multiply`, `Math_Q16Divide`, `Math_FastMagnitude2D`,
`maybe_Math_MapRange`, `Time_GetCoarseTicks`, `Vec2_Subtract`, `Vec2_Magnitude`,
`Vec2_DistanceBetween`.

**Process note.** Both earlier attempts at this number reasoned from constants in data files toward a
plausible scale. What actually settled it was asking where the game *tells the player* a distance —
i.e. going to the output the user can see, the same move that settled the terrain-texturing and
flat-colour questions. A screenshot of the cockpit named the two readouts (`M.` and `K/H`) whose
format strings then led straight to the conversion functions.

## Open questions

The items milestone 1 surfaced (terrain diagonal-selector bit 1, the PRNG seed table, the
timestep's real value, DBSIM's trig table, the hit-cylinder radius field) are listed under
"Milestone 1 — implemented" above, next to the code that works around each one.

**Terrain texturing is RE-solved and ready to implement** (2026-08-13, see
`docs/formats/terrain-texturing.md`): the bank name comes from the `world<N>` theater descriptor,
the frame from `mat0[material].Index`, and the per-cell UV rect from a `BlockShift` formula with a
256-texel wrap. Terrain is still drawn flat-shaded in the engine — implementing it needs the
`world<N>` descriptor's field layout decoded, to read the bank name rather than hardcode it. The
metres-per-texel discrepancy that also blocked it is **resolved**: at the recovered scale a retail
cell is 98.3 m and carries 128 texels, so a texel is 0.77 m. That is *finer* than the 0.64 m the old
estimate gave, i.e. the correction moved the number away from the "several metres per texel" a
gameplay screenshot suggested — which settles it the other way, by elimination: that impression was
320x200 screen pixels, not texels.

Two corrections that fall out of that session and affect existing engine code:
`TerrainMaterial.Index` is a texture frame index, not the self-index its doc comment claimed; and
the LOD field is re-derived **every frame** by the original from a per-detail-setting table, so the
loader's hardcoded base of 10 is the retail default entry rather than a constant. Both doc comments
are updated in place.

## Milestone 2 — mech texturing (2026-08-13)

Textured mech rendering on the GPU, using the chain in `docs/formats/dts-texture-binding.md` end to
end. New: `Render/TextureAtlas` (decodes a whole `.DBA` through a `.DPL` and shelf-packs it,
CPU-only so `ZoneScene` stays headless-buildable), `Gl/GpuTexture` (upload, nearest-neighbour, no
mipmaps — a vanilla-fidelity call, the original point-samples). `MeshVertex` gained a UV,
`SceneRenderer`'s shaders sample a texture behind a `uTextureEnabled` flag, `SceneItem` carries an
optional texture handle, and `ZoneScene` picks the bank from the mech's own `.DAT` via
`HercSimDat.ModelSkinId`.

Packing into an atlas is an engine-side optimisation, not a reproduction of an original data layout
— the original ships no atlas, because a software rasterizer pays nothing to switch frames while a
GPU pays per bind. It is also the smaller change than batching a mesh per referenced frame.

`DtsMeshBuilder.DropCoincidentTwins`' preference **inverted** as part of this: while texturing was
unimplemented it deliberately kept the untextured twin of each stacked pair, which would have hidden
every texture behind a flat poly. Now a three-way rank keeps the textured twin when it resolved, and
still falls back to the flat twin when it did not — so the no-bank path is unchanged.

Verified headlessly against the real install: all seven skin banks pack (largest 53 frames into
256x512), 21 of 22 mechs resolve every texture poly with zero out-of-range frame indices, and
building each mech with and without an atlas gives identical triangle counts with textured triangles
going 0 → 142 (SAMSON) / 198 (DIABLO) / 232 (APOCA). The decoded `HEAVY` atlas was rendered and eyeballed
— real armour plating, vents, hazard stripes, faction insignia — confirming the palette choice.
Anomalies found and left falling back rather than guessed at: TOMAHAWK has 4 degenerate polys, and 13
`TSTexture4Poly`s fleet-wide carry 3 vertices instead of 4. Both are documented in the format doc.

## Milestone 3 — terrain texturing (2026-08-13)

Terrain now draws the theater's real texture bank, using the chain in
`docs/formats/terrain-texturing.md` end to end and with nothing about it hardcoded except the
theater index (which belongs to a mission, and missions are not loaded yet).

**New:** `World/TheaterDescriptor` (parses `wld\WORLD<n>.WLD` — layout decoded this session and
verified byte-exact against all ten retail files), `World/ScriptDatHeader` (the three header fields a
mission uses to name its world: theater, zone, variant), `Render/TerrainTextureBank` (loads and packs
the named `.DBA` through the theater's palette, and implements `Terrain_ResolveCellTexture`'s
per-cell rect). `TerrainMeshBuilder` emits per-corner UVs; `MeshVertex` gained a per-vertex
`Textured` flag; `ZoneScene` carries the theater and the bank.

**The per-vertex texture flag is worth its own note.** The previous per-draw `uTextureEnabled`
uniform could not express a mesh that mixes textured and untextured triangles, and both meshes are
such a mesh — a mech has a handful of texture polys whose frame index does not resolve, and a terrain
cell can select a frame its bank does not have. It also quietly fixed a live defect: an unresolved
mech texture poly kept its placeholder colour in the vertex data but was drawn with texturing on and
UVs of `(0,0)`, so it sampled whatever sat at the atlas origin rather than showing the placeholder.

**The theater palette resolved an old shrug.** Mech texturing shipped decoding banks against
`WORLD0.DPL` because nothing named which of `WORLD0..9` went with which bank. They are not a set to
choose from: `World_LoadTheater` loads `dpl\world<N>.dpl` as its first act, one palette active per
theater for everything it draws. `ZoneScene` now decodes the mech's bank against the theater's
palette too.

**Verified headlessly against the real install** (see the format doc for the full list): ten
descriptors parse to their exact length, five banks pack, zone 504 textures 100% of its terrain
vertices against every theater, and the per-cell rects match the documented formula's own numbers —
a cell spans 128 of 256 texels and the texture repeats every two cells. The `urban` and `ice` atlases
were rendered and eyeballed and look like what this document's terrain notes predicted before
anything was drawn.

### RE gaps that block specific engine features (status current as of 2026-08-11, confirmed
against the actual repo docs — supersedes the older, more pessimistic summary in
`project-es2-engine-port-readiness` memory)

- ~~Direct-fire weapon damage (lasers, autocannons) not located~~ — **solved and documented.**
  Armor-then-part, deterministic, shield-gated formula, fully written up in
  `docs/simulation/dbsim-physics-notes.md` ("Direct-fire damage: armor-then-part, deterministic,
  shield-gated"). No longer a blocker for combat implementation.
- **DTS texture-to-DBA binding — mechanism solved, one scope caveat remains.** `.DTS` carries no
  texture references; each `TSShapeInstance` holds its own bound `.DBA` pointer, and each poly's
  `BmpTag`/`FrontColor` is a plain frame index into whichever DBA is currently bound. Full writeup
  in `docs/formats/dts-texture-binding.md`. **Caveat:** confirmed via VSHELL.EXE's 2D armory-display
  code (`dba\<code>_bod.dba` / `_wep.dba` / `_out.dba` naming convention); DBSIM.EXE's own
  DBA-selection convention for the live 3D combat view is the same underlying mechanism but not yet
  independently confirmed (DBSIM is a stripped build, harder to trace), and actual textured
  rendering isn't implemented in C# yet either. Not needed for the first milestone (untextured
  geometry only) but worth resolving the DBSIM-side convention before the milestone that adds
  textures.
- `.SNC` audio format unsolved — blocks original game audio playback. Not needed until audio
  work starts.
- AI/behavior trees barely understood — blocks enemy mech behavior. Not needed for the first
  milestone (single mech, no combat/AI).

None of the remaining gaps (`.SNC`, AI, DBSIM-side texture convention) block the first milestone
as scoped above.
