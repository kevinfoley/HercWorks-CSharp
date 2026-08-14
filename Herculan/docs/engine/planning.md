# HERCULAN Engine — Planning

Living planning document for the Earthsiege 2 engine port, tracking decisions as they're made.
Started 2026-08-11. This is a working document, not a spec — update it as decisions change.

## Context

Long-term goal: a modern, cross-platform engine capable of running Earthsiege 2 using the
original game's data files. The "HercWorks" toolkit is a separate,
already-underway toolkit for reading/editing those data files. See the scope assessment in
project memory (`project-es2-engine-port-readiness`) for what RE work is and isn't done yet.

## Design principles

### Vanilla by default

All behavior matches the original game exactly by default. The only exceptions are purely
cosmetic changes with no gameplay effect (e.g. resolution). Anything that changes behavior in the future —
fixing bugs that exist in the original, raising limits, increasing mathematical precision, etc. —
will be opt-in via a settings menu.


## Settled decisions

- Name: **HERCULAN Engine**

- Language: **C#**, not C++. The deciding factor is that `HercWorks.Core`
  already represents substantial, hard-won reverse-engineering work (file formats, DBSIM sim
  math) and is directly reusable from C#. Performance is a non-issue on modern systems.
  
- Runtime: **Modern .NET (8/9/10+), not Mono.** Mono was the historical answer for cross-platform C#
  (Xamarin, Unity, old MonoGame) but modern .NET has been natively cross-platform
  (win-x64/linux-x64/osx-arm64/etc.).

### Rendering

- Start with **OpenGL**, with an eventual goal of also supporting **Vulkan** (user-selectable
  backend).
- Bindings: **Silk.NET**
- Don't over-build the GL/Vulkan abstraction layer up front. An abstraction designed against a
  single backend tends to bake in assumptions (implicit state, no explicit sync) that don't map
  cleanly to Vulkan. Get OpenGL working concretely first; generalize the render interface when
  Vulkan support is actually being added and its real requirements are visible.

### Repo & project structure

- **Single repository** (`E:\ES2Stuff`, current repo).
- **Engine lives as sibling project(s) to `HercWorks.UI`**, under`Herculan/src/`, added to the 
  existing `HercWorksMDK.sln`. Both the engine and the WinForms UI reference `HercWorks.Core` / 
  `HercWorks.Vol`. `HercWorks.TransferApi` (UI-facing DTOs) is UI-only plumbing; the engine 
  bypasses it and talks to `HercWorks.Core` domain types directly.

### Simulation object architecture

**Traditional OOP / virtual dispatch, matching the original — not ECS.** DBSIM.EXE's simulation
objects are built on a shared base-object constructor helper (`FUN_00402188`) called by every
`SimObject`-derived class right after its vtable pointer is set. See `project_es2_exe_recon`
 memory and `docs/simulation/dbsim-physics-notes.md`.

Plan: a `SimObject` abstract base class in the engine with virtual overrides mirroring the
discovered vtable shape (Mech, Rocket, Bullet, Flyer, ...), rather than a component/system model.

This decision is scoped to simulation objects specifically. Rendering/scene representation is a
separate question and isn't required to follow the same pattern.

### Physics

**Custom, exact match to the original.** Not adopting an off-the-shelf .NET physics library
(e.g. BepuPhysics) — the goal is to reproduce DBSIM's actual behavior, which has already been
substantially reverse-engineered: See `docs/simulation/dbsim-physics-notes.md` for the full detail
 — this is the primary porting target for the physics/sim subsystem, not a reference to design 
against.

### Math

**Custom, exact match to the original** — port the actual fixed-point math toolkit found in
DBSIM (Q8/Q10/Q14 fixed multiply, "integrate a rate over one tick," rate-limited "move toward,"
the sqrt-free fast 3D magnitude approximation) rather than using floating-point
`System.Numerics` throughout.
- **Nice-to-have, not required for v1:** architect this behind an abstraction so the engine could
  later switch to modern floating-point math without a large rewrite. Apply the same caution as
  the rendering-backend abstraction above — don't design the swap layer in detail before there's
  a second implementation to validate it against.

### Audio

**OpenAL via Silk.NET.**

### Target platform

Primary development/testing target is **Windows**, but OS-specific code paths should still be
abstracted from the start (consistent with the modern-.NET cross-platform decision above and
Silk.NET's cross-platform windowing) so Linux/macOS support doesn't require rework later.

### Engine internal architecture

- **Library core + thin front-end host.** Engine subsystems (rendering, scene, etc.) should be
  built as libraries with no baked-in assumption that there's exactly one game loop consuming
  them. A separate, minimal host project wires those libraries into an actual real-time game
  loop.
- Motivation: a possible future mission editor that renders the mission environment in-engine.

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
- Targets the unbuilt, highest-risk parts of the project (render pipeline, real-time
  sim loop, engine plumbing) first
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

Full solution builds with zero errors (`dotnet build HercWorksMDK.sln`).

### Milestone 1 — implemented (2026-08-11)

All three parts of the first milestone are built and the full solution compiles clean in Debug and
Release. Verified headlessly and by Kevin.

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

**Notes:**

- **Terrain triangulation matches the height query by construction.** The renderer splits each quad
  along the same diagonal the cell's selector bits choose, so drawn surface = queried surface.
- **DTS model units are world units, 1:1** — see "World scale — recovered" below. Differs from the
  WinForms viewer's 1/10 scale, which is arbitrary window framing.
- **A mech `.DTS`'s 7 roots are an LOD chain, root 0 highest.** Confirmed by triangle counts across
  SAMSON/OUTLAW/APOCA (253/251/249/210/132/54/11 for SAMSON) over identical bounds. Not stated in the
  file; engine picks root 0.
- **Matrix uniforms upload with `transpose: false`.** System.Numerics is row-vector (`v * M`),
  row-major storage; GLSL is column-vector (`M * v`), reads a uniform column-major when transpose is
  false — writing System.Numerics' row-major elements untransposed hands GL exactly the
  column-vector form of the same transform. Getting this backwards renders only the clear colour
  (translation lands in the row a column-vector multiply ignores, `w` goes negative, everything
  clips). Documented in `ShaderProgram.SetMatrix` since "row-major vs column-major" alone is the
  wrong mental model — the vector convention is the load-bearing half.
- **`Herculan.Engine` has no `System.Drawing` dependency.** `DtsMeshBuilder` is a separate type from
  the UI's `DtsGeometryBuilder` for that reason; same tree-walking rules, cross-annotated.

**Deliberately not done:** weapons/damage (`SimObject` doesn't declare the `+0x20`/`+0x70`/`+0x74`
vtable slots yet); mech locomotion (mech is stationary); backface culling (DTS winding isn't
reliable, per the WinForms viewer). Texture rendering shipped in Milestones 2-3.

**Open items surfaced:**

- **Nothing writes the terrain diagonal-selector's bit 1.** Every flag write in
  `TerrainZone_PopulateFromBitmap` (and its ASCII counterpart) masks with `& 2`, preserving bit 1 and
  clearing bit 0, and cells arrive zeroed — both loaders leave every cell's selector at 0. The query
  handles selector 2, so something else must set bit 1; not located. Engine reproduces the loaders
  exactly rather than inventing a rule. Terrain-renderer theory disproved: `Terrain_DrawCellQuad` and
  `FUN_0046ff74` only ever read `cell[+0xf]`. See `docs/formats/terrain-texturing.md`.
- **`HeightGrid` LOD field (`+0x10c`)** is scaled by `maybe_Terrain_ComputeViewDistance` (`00470910`)
  from cells to world units via `<< cellShift`, clamped to 1000 near grid edges, once/frame. Scaling
  is solid; the two output values' meaning is undecoded — see `terrain-texturing.md` before wiring to
  far-clip/haze.
- **`SimRandom`'s 56-entry seed table isn't extracted** from DBSIM's data section — algorithm is a
  literal port, seeding isn't. A roll's result also depends on generator-advance count, so treat as
  statistically faithful, not replay faithful. Currently drives only terrain material bits (unrendered).
- **Timestep is unknown; 30 Hz looks wrong.** `SimTickDelta` (`DAT_004d3be8`) is written once, by
  `Sim_MainTick` from field `0x17` of a timing struct from `FUN_0045a7f4` (untraced). At 166.667
  units/metre, mech `SpeedForward` only matches the manual's KPH figures near 15 Hz; 30 Hz doubles
  every HERC's speed. `SimWorld` still runs 30 Hz (Q8 delta 256) — a suspect default, not neutral.
  (0x500/tick rocket turn cap = ~3.4s/revolution at 15 Hz, still sane.) `Time_GetCoarseTicks`
  (`00467724`) is a separate clock: `GetTickCount() >> 4`, the 16ms UI/mission-clock timebase.
- **DBSIM's sine/cosine table isn't located** — no trig function in the current symbol set.
  `BinaryAngle`'s table is generated at Q14, not ported; fine for a camera, would drift slowly in
  anything integrating heading over many ticks. All trig routes through that one type.
- **Per-type hit-cylinder radius (`typeRecord+0x1a`) isn't mapped to a `HercSimDat` field** — the
  in-memory mech type record has more fields than the `.DAT` and offsets don't line up.
  `MechObject` uses a model-bounds-derived radius meanwhile.

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

**Discrepancy, not a problem:** at 166.667 u/m, HERC models measure 10.2m (OUTLAW) to 15.5m (OGRE),
~1.5x the manual's quoted stature (6.1m/10.4m) — bounding box (includes raised arms/antennae) vs.
quoted height; weight-class ordering matches the manual exactly. Same gap existed at the old 200
estimate too (SAMSON measured 11.8m vs quoted 9.2m).

**Independent order-of-magnitude check:** HUD speed readout (`Mech_GetDisplaySpeedKph`, `0041bb3c`)
= `speed * 315/1024`; against each mech's `SpeedForward` reproduces the manual's KPH: OUTLAW 325 →
100 (exact), SAMSON 190 → 58 (quoted 60), COLOSSUS 180 → 55, MAVERICK 285 → 88 (quoted 90). **Not a
tick-length source** — 315 looks fitted to make the fastest HERC read a round 100; inverted against
166.667 u/m it implies ~14.2 Hz, suggestive but not round enough to trust. Timestep stays open (see
below), bounded to "much nearer 15 Hz than 30" — 30 Hz would double every mech's quoted speed.

New symbols, applied via `ES2ApplySymbolNames`: `Hud_WorldUnitsToMetres`,
`Hud_UpdateWaypointIndicator`, `Hud_UpdateSpeedReadout`, `Mech_GetDisplaySpeedKph`,
`Math_Q10Multiply`, `Math_Q16Multiply`, `Math_Q16Divide`, `Math_FastMagnitude2D`,
`maybe_Math_MapRange`, `Time_GetCoarseTicks`, `Vec2_Subtract`, `Vec2_Magnitude`,
`Vec2_DistanceBetween`.

## Open questions

The items milestone 1 surfaced (terrain diagonal-selector bit 1, the PRNG seed table, the
timestep's real value, DBSIM's trig table, the hit-cylinder radius field) are listed under
"Milestone 1 — implemented" above, next to the code that works around each one.

## Milestone 2 — mech texturing (2026-08-13)

Textured mech rendering on the GPU, using the chain in `docs/formats/dts-texture-binding.md` end to
end. New: `Render/TextureAtlas` (decodes a whole `.DBA` through a `.DPL` and shelf-packs it,
CPU-only so `ZoneScene` stays headless-buildable), `Gl/GpuTexture` (upload, nearest-neighbour, no
mipmaps — a vanilla-fidelity call, the original point-samples). `MeshVertex` gained a UV,
`SceneRenderer`'s shaders sample a texture behind a `uTextureEnabled` flag, `SceneItem` carries an
optional texture handle, and `ZoneScene` picks the bank from the mech's own `.DAT` via
`HercSimDat.ModelSkinId`.

Packing into an atlas is an engine-side optimisation, not a reproduction of an original data layout
— the original ships no atlas because it uses a software renderer.

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

**Per-vertex `Textured` flag** replaces the old per-draw `uTextureEnabled` uniform, which couldn't
express a mesh mixing textured and untextured triangles (both mech and terrain meshes do — unresolved
frame indices on either). Also fixed a live defect: an unresolved mech texture poly kept its
placeholder colour but was drawn with texturing on and UVs `(0,0)`, sampling the atlas origin instead
of showing the placeholder.

`World_LoadTheater` loads `dpl\world<N>.dpl` as its first act, one palette active per theater for
everything it draws; `ZoneScene` decodes the mech's bank against the theater's palette too.

**Verified headlessly against the real install** (see the format doc for the full list): ten
descriptors parse to their exact length, five banks pack, zone 504 textures 100% of its terrain
vertices against every theater, and the per-cell rects match the documented formula's own numbers —
a cell spans 128 of 256 texels and the texture repeats every two cells. The `urban` and `ice` atlases
were rendered and eyeballed and look like what this document's terrain notes predicted before
anything was drawn.

## Milestone 4 — real missions (2026-08-13)

The scene is now the game's own object placement. Nothing about it is configured by hand: the host
is given a `script.dat` and everything else — zone, theater, theater variant, which units exist,
their types, positions, headings and weapon fits, and the player's own lance — comes out of the
mission. Milestone 1's single hardcoded mech at the middle of the zone is gone, and `ZoneScene` with
it.

**Key finding:** DBSIM reads `script.dat` **twice** — `DBSim_LoadScriptDat` (pass 1) only counts live
objects and sizes pools; `DBSim_SpawnMissionObjects` (`FUN_004253d8`, pass 2) re-opens the file and
walks blocks 7-13 again, reading positions, headings and loadouts. An earlier revision of the format
doc described pass 1 only and concluded the format discarded most of block 7-11. Full rule in
`docs/formats/script-dat.md`'s "The two-pass read" and "Placement" sections.

**Three type numberings resolved**, each the last link between a mission's numbers and a resource:

- **Mechs** — `nam\MECHS.NAM`, 21 NUL-terminated names indexed by block 7's type field
  (`MechType_InitOne` joins the name to the `dat\`/`dts\`/`bnd\` prefixes, so one name is the stats
  file, the model and the collision data). `nam\FLYERS.NAM` does the same for block 8.
- **Structures** — `dat\BASES.DAT`, 65 records of 60 bytes with one nested variable-length array.
  Record shape confirmed by construction: it consumes the retail file's 6,422 content bytes with
  zero slack. `FUN_00405ebc` is the whole model selection — one field picks the shape index, another
  picks the library, a third picks the texture bank. This also corrects a wrong entry in
  `dgs-hd0-notes.md`, which had applied this record shape to `BASES.DGS` and recorded it as
  disproven; it is `BASES.DAT`'s shape and it is exact.
- **The player's lance** — `data\player.mec`, decoded and implemented (`MecFile`). It sits at the
  spawn point block 11's record 0 exists to hold, which is why the camera can now start where the
  mission starts.

**New:** `World/Mission` + `World/MissionLoader` (the two-pass placement rule),
`World/UnitTypeNames`, `World/BaseTypeTable`, `Scene/MissionScene` (replaces `ZoneScene`),
`Scene/SceneModelLibrary` (one mesh and one atlas per distinct type, however many objects share
it), `Sim/FlyerObject`, `Sim/BaseObject`. `MechObject`'s loadout is no longer stubbed. In
HercWorks.Core, `ScriptDat`'s blocks 7/8/9 gained named fields for what pass 2 reads, and `MecFile`
replaced a never-implemented stub.

**Verified headlessly against the real install:** the 10 available `script.dat`-shaped files (see
`script-dat.md`'s "Fixed-size file structure" — these are save-slot snapshots, not necessarily 10
distinct retail missions; total retail mission count is unverified, plausibly ~50 across 5 sectors)
all build as scenes — zone, theater, rosters, groups and lance all resolve, no live roster slot goes
unclaimed by a group, every placed object lands inside its zone's bounds.
`ScriptDatTransformer` still round-trips all 10 byte-exact after the model change.

### Milestone 4's two known gaps

RE gaps, not design choices:

- ~~Formation spread — unresolved, not settled~~ — **bases solved and fixed, see Milestone 6
  below; mechs/flyers still open.** For mechs the link from formation id to `dat\MFORMS.DAT` still
  can't be confirmed (table pointer `FUN_004205cc` reads has exactly one reference in all of
  DBSIM.EXE — the read itself — in uninitialised data), so mechs still stack on their group's point.
- ~~Static structures don't draw~~ — solved, see Milestone 5 below.

## Milestone 5 — static structures (2026-08-14)

`dgs\BASES.DGS`/`BHULKS.DGS` solved. Full RE and format spec in `docs/formats/dgs-hd0-notes.md`.
Summary: each is a flat sequential list of `ClassItem`-tagged records (tag `0x02BC0001`); each
record wraps exactly one ordinary DTS chunk (`TSDetailPart`), reusable via
`DTSModelTransformer.ReadOneObject`, plus undecoded metadata (BSP/collision data, not needed to
draw). New: `HercWorks.Core.Io.Transform.Dbsim.BasesDgsTransformer`,
`HercWorks.Core.Data.File.Dgs.BaseShapeLibrary`. `SceneModelLibrary.Base()` now resolves both
`BaseShapeSource` cases. Verified against retail data: all 45 `BASES.DGS` records and all 16
`BHULKS.DGS` records parse with zero exceptions (1536 groups/8978 polys and 113 groups/786 polys
respectively).

### Remaining RE gaps

- The mech formation-offset table's load site (see above).
- Flyer texture banks — which `.DBA` DBSIM binds for a flyer is untraced, so flyers draw
  flat-shaded.
- `.SNC` audio format unsolved — blocks original game audio playback. Not needed until audio
  work starts.
- AI/behavior trees barely understood — blocks enemy mech behavior and patrol movement along the
  routes missions now resolve.

## Milestone 6 — base formation spread (2026-08-14)

Fixed: multi-structure base groups (fortress clusters, turret rings) were placing every member on
the group's single spawn point instead of spreading them out. Root-caused headlessly first, against
all 10 available missions, before touching code: every stacked group's members genuinely carry no
position of their own (`ScriptMiscEntityExport.PositionRef == -1`) — not a coordinate-resolution
bug, confirming the placement rule's documented fallback was firing as designed but incompletely
(missing the spread step DBSIM applies on top of the fallback).

RE'd the missing step: `FUN_00405c3c` (the base-group-attach function) unconditionally calls the
attached object's vtable `+0x78`, which for every base subtype is `FUN_00405c04` — a direct
structural match for `Mech_ApplyFormationOffset`, except this one's backing table
(`dat\BFORMS.DAT`) has a confirmed load site (`FUN_00405fac` opens it by the literal string
`"bforms"`), unlike the still-open mech case. Full chain, byte-exact file verification, and the
rotate-and-add math are in `script-dat.md`'s "Placement — the actual rule" point 6.

Implemented as `Herculan.Engine.World.BaseFormationTable` (parses `BFORMS.DAT`'s count + variable
per-formation records) wired into `MissionLoader.AddRoster`'s base loop, replacing the bare
`?? group.Position` fallback with `?? OffsetFromGroup(...)`, which applies the formation offset for
the object's slot index within its group (0 for the group's first-claimed member, which keeps
today's no-offset behavior) before falling back further to the bare group point. Re-verified
headlessly against all 10 missions post-fix: 18 of 18 multi-member base groups now get fully
distinct member positions (0 remain stacked), where all 18 collapsed to a single point before.

Not implemented — see `script-dat.md` point 6 for detail:
- Grid-snap (`BinaryFlag`-gated group-anchor snap) — shifts a group's shared point, doesn't cause
  stacking, not chased.
- Mech/flyer formation spread — mechs still blocked on the unconfirmed `MFORMS.DAT` load site;
  flyers untraced (not seen mattering in retail data — no multi-flyer groups observed).
