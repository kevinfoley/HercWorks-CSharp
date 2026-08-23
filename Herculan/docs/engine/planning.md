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
  `FUN_0046ff74` only ever read `cell[+0xf]`. See `docs/formats/terrain-heightmap.md`.
- **`HeightGrid` LOD field (`+0x10c`)** is scaled by `maybe_Terrain_ComputeViewDistance` (`00470910`)
  from cells to world units via `<< cellShift`, clamped to 1000 near grid edges, once/frame. Scaling
  is solid; the two output values' meaning is undecoded — see `terrain-heightmap.md` before wiring to
  far-clip/haze.
- **`SimRandom`'s 56-entry seed table isn't extracted** from DBSIM's data section — algorithm is a
  literal port, seeding isn't. A roll's result also depends on generator-advance count, so treat as
  statistically faithful, not replay faithful. Currently drives only terrain material bits (unrendered).
- **Timestep — resolved (2026-08-21).** DBSIM ticks at a fixed 25 Hz. `SimTickDelta`/
  `DAT_004d3be8` is computed by `FUN_004677bc` (the earlier `FUN_0045a7f4` lead below was never
  the actual writer), a Q8 value where `0x100` = 125 ms — formula in
  `docs/simulation/dbsim-physics-notes.md`'s "Fixed-point math toolkit". `SimWorld.TickDelta` is
  pinned to `81`, what that formula evaluates to at 25 Hz, replacing the milestone-1-era 30 Hz
  default this bullet used to flag as suspect. `Time_GetCoarseTicks` (`00467724`) is a separate
  clock: `GetTickCount() >> 4`, the 16ms UI/mission-clock timebase.
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
166.667 u/m it implies ~14.2 Hz. That back-solved estimate undershot: the timestep was later
resolved directly from `FUN_004677bc` at a fixed 25 Hz (see "Open items" above), confirmed
independently by `mech-locomotion.md`'s root-motion speed verification (predicted/HUD ratio
0.899–1.076 across all 18 HERCs). This estimate did correctly rule out 30 Hz.

New symbols, applied via `ES2ApplySymbolNames`: `Hud_WorldUnitsToMetres`,
`Hud_UpdateWaypointIndicator`, `Hud_UpdateSpeedReadout`, `Mech_GetDisplaySpeedKph`,
`Math_Q10Multiply`, `Math_Q16Multiply`, `Math_Q16Divide`, `Math_FastMagnitude2D`,
`maybe_Math_MapRange`, `Time_GetCoarseTicks`, `Vec2_Subtract`, `Vec2_Magnitude`,
`Vec2_DistanceBetween`.

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
  picks the library, a third picks the texture bank.
- **The player's lance** — `data\player.mec`, decoded and implemented (`MecFile`). It sits at the
  spawn point block 11's record 0 exists to hold.

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
`"bforms"`). Full chain, byte-exact file verification, and the rotate-and-add math are in
`script-dat.md`'s "Placement — the actual rule" point 6.

Implemented as `Herculan.Engine.World.BaseFormationTable` (parses `BFORMS.DAT`'s count + variable
per-formation records) wired into `MissionLoader.AddRoster`'s base loop, replacing the bare
`?? group.Position` fallback with `?? OffsetFromGroup(...)`, which applies the formation offset for
the object's slot index within its group (0 for the group's first-claimed member, which keeps
today's no-offset behavior) before falling back further to the bare group point.

## Milestone 7 — mech formation spread (2026-08-14/15)

Fixed the mech half of Milestone 6: `dat\MFORMS.DAT`'s load site, `Mech_LoadResources`
(`FUN_0041fdb0`), had never been auto-disassembled (no call-graph edge into it), so its write to
`Formation_GetSlotOffset`'s table pointer (`_DAT_004a9df0`) was invisible to XREF search, an
analysis gap. Found via raw byte-scan for the string `"mforms"`, then for that
address as a `PUSH`-immediate operand. Registered into DBSIM's subsystem-loader table (live code).
`dat\MFORMS.DAT`: 142 bytes = 2-byte count (5) + five fixed 28-byte formations, byte-exact.
Implemented as `Herculan.Engine.World.MechFormationTable`, wired into `MissionLoader.AddRoster`.
Verified: 26/26 multi-mech groups get distinct positions, 0 exceptions.

Also attempted base grid-snap and **reverted it same-day**: the decompiled formula
(`script-dat.md` point 6) passed the distinct-positions check but visually shifted real structures
tens of thousands of world units off their pads, confirmed against the mission editor. Field
mapping or a scale factor is wrong; not re-attempted without a visual check next time.

Not implemented: flyer formation spread (`FUN_00421ee8` untraced; no multi-flyer groups observed).

## Milestone 8 — cockpit canopy + HUD graphics (2026-08-15)

First 2D rendering of any kind: the player's cockpit canopy art (`(herc).HB0`/`.HB2`) and GAU HUD
widget layout, drawn over a live 3D view. Full RE and real-data verification in
`docs/formats/cockpit-hud.md` (Phase 0); summary here.

**Modernization call, flagged as such per "vanilla by default":** the original shows one view at a
time, panned via `F9`/`F10`. This milestone draws **three views simultaneously, side by side**
(left/front/right) instead, since modern wide monitors don't need panning — purely cosmetic, no
sim-behavior change. `Render/CockpitViewLayout.cs` computes each side panel's yaw offset from FOV and
aspect ratio (`halfFovX(viewport) = atan(tan(fovY/2) * aspect)`) so the three tile edge-to-edge with
no seam regardless of window size.

**RE findings (Phase 0), all in `cockpit-hud.md`.** Two of the four were superseded by the
2026-08-17 pass below — see that section; the surviving two:
- GAU coordinates are authored in the 320-wide space and map onto `.HB0`/`.HB2`'s 640x480 pixel space
  via a plain, uniform **2x scale on both axes** (`CockpitArt.GauToPixelScale`).
- `.HB0` = forward view, `.HB2` = a distinct side view, mirrored at draw time for the opposite panel
  with no separate mirrored asset. Later confirmed to be exactly what DBSIM does.

**New types:** `Content/CockpitArt.cs` (loads/decodes `.HB0`/`.HB2`/`.GAU`, bakes the viewport-hole
alpha), `Gl/Overlay2DVertex.cs` + `Gl/GpuOverlayMesh.cs` (a lighter 2D vertex/mesh pair — forcing flat
HUD quads through the 12-float lit-3D `MeshVertex` layout would waste bandwidth for no benefit, same
reasoning as `WireframeRenderer`'s own separate shader), `Render/Overlay2DRenderer.cs` (draws the
cockpit-art quad with alpha blending, depth test off, then GAU widgets as flat-color outline/fill
placeholders — center panel only, since the console instruments live in the front view — and restores
GL state afterward), `Render/CockpitViewLayout.cs`. `Gl/GpuTexture.cs` gained a raw-RGBA-pixels
constructor (no atlas packing needed for one 640x480 frame). `Render/SceneRenderer.cs` split into
`Clear()` (call once per frame) and `Render(camera, items, viewportX, viewportY, viewportWidth,
viewportHeight)` (no longer clears, takes an explicit sub-rect) — each panel's own `gl.Viewport` call
confines its rasterization with no scissor-rect bookkeeping needed.

**Verification aid:** `Herculan.Engine.Host` gained a `--screenshot <path>` flag — captures a
dependency-free 24bpp BMP via `glReadPixels` after 30 frames and exits (no System.Drawing/ImageSharp
dependency, matching the engine's existing no-imaging-dependency precedent).

**Verified against the real install** (COLOSSUS, zone 555): palette renders correctly (recognizable
console art, magenta energy-meter stripes intact); left/right panels are true mirrors of each other;
all HUD placeholders (MFD bezel, weapon-slot banks, shield fill, chain/link/autotrack buttons,
throttle, reticle) land exactly on their physical console graphics; the three panels tile with no
visible seam even where the front and side images meet (checked at both boundaries, pixel-cropped).

**Bug caught during this verification, not before:** the first working build placed widgets using the
cockpit texture's own native 640x480 pixel coordinates directly as panel-viewport pixel coordinates —
correct only when a panel's viewport happens to be exactly 640x480. Since the texture is stretched to
fill each panel's own (different, non-4:3) viewport, every widget drifted off its console art. Fixed
by scaling widget positions by `viewportSize / cockpitTextureSize` per axis, same as the quad itself
already gets implicitly via UV interpolation. Caught by the visual verification screenshot, not by
inspection — worth remembering that GAU-widget-over-cockpit-art correctness needs a real render, not
just "it compiles."

Not implemented: GAU widgets are not interactive (no input wiring); no HUD font/icon assets (outline
placeholders only, per plan — kept out of scope on follow-up review too, see below); `.HB1`'s
rear/overhead view is not drawn anywhere.

### Milestone 8 follow-up (2026-08-15, same day): three real defects found on user review

The first build's own screenshot-based verification above missed three things a side-by-side
comparison against a real reference screenshot caught:

1. **Cockpit art was stretched to fill each panel, distorting its aspect ratio.** Fixed:
   `Overlay2DRenderer` now fits by height and preserves the native 640x480 aspect ratio, centering
   horizontally — narrower panels (the common case for 3 side-by-side panels at a normal window
   width) crop the art's left/right edges via GL's own clipping (no explicit UV math needed); wider
   (ultrawide) panels show the live 3D view at the flanks instead of stretching art to cover them.
   Widget placement uses the same transform, so it stays aligned regardless of panel aspect ratio.
2. **Side panels were opaque black instead of showing the 3D view through the canopy.** Root cause:
   the viewport-hole flood fill (`CockpitArt.CutViewportHole`) was only ever applied to `Front`
   (`.HB0`), never to `Side` (`.HB2`). Fixed by applying it to `Side` too, seeded at its geometric
   center — confirmed index 0 there across every retail herc checked.
3. **Palette colors were visibly wrong.** A genuine RE gap, not just a code bug. The interim model —
   two `.DPL` files merged, plus a single `CockpitArt.PaletteIndexOffset = 14` standing in for a
   per-herc ramp selector — was wrong in both halves and is superseded below.

HUD sprite art is now drawn from the game's own `hba\*.HBA` banks (`HudSpriteSheet`), and
`dat\COLORS.DAT`'s logical-colour-id table is decoded (`HudColorTable`). The "still open" items this
entry listed — gauge colours, `.DFN`/`.HFN` text, frame-to-state mapping — are resolved by the two
follow-ups below.

### Milestone 8 follow-up (2026-08-17): cockpit rendering fully reverse-engineered

Full RE in `docs/formats/cockpit-hud.md`, rewritten as the reference for this subsystem. What changed
in the engine:

- **Palette (`CockpitPalette`) inverted.** The live palette is the theater palette in full; only slots
  42-65 are replaced, by this herc's own 24-entry window of `COCKPIT.DPL` selected by
  `dat\<MECH>.DAT` offset 80. The nine schemes tile `COCKPIT.DPL` entries 32-247 exactly. Fixes the
  canopy for all nine hercs rather than only COLOSSUS, and with it the heading tape (theater index 74)
  and the hazard stripes (theater yellow at index 13).
- **`CockpitArt.PaletteIndexOffset`/`ShiftCanopyIndex` deleted.** Canopy indices decode as authored.
- **Viewport cutout is data-driven (`CockpitClipRegions`, new).** Parses the herc's own
  `hd0`/`hd2` per-scanline span files — the same data DBSIM's rasterizer is span-clipped to. The
  border flood-fill over black pixels survives only as a fallback, reported via
  `CockpitArt.ClipRegionsLoaded`.
- **`CockpitArt.ColorSchemeIndex`** exposed and logged by the host.

**Verified** by pixel-comparing decoded `.HB0` against the retail reference screenshots in
`Reference/`: APOCA 69.4% exact RGB / mean channel error 11.8, COLOSSUS 78.7% / 9.2 over opaque
pixels, with every scheme index bar two agreeing at 85-100% and both outliers' disagreements confined
to the scanner/MFD block where retail paints live HUD content over the art. Also verified that all
nine hercs' schemes and both clip files load, and that RAZOR is the only herc with a non-stub view-1
clip file (matching its file sizes on disk).

**Not changed, deliberately:** the three-panel simultaneous layout stays. DBSIM switches one of four
views at a time on a keypress; that divergence is the modernization call above, and the RE does not
contradict it — the mirror-for-the-opposite-side approach already in use is precisely what DBSIM does
(`Bitmap_Blit` flag 2 on view 3). Side panels still draw no GAU widgets, which the RE confirms is
correct: widget origins across all nine hercs span `x:[3..298] y:[1..230]`, entirely inside the
forward view's quadrant of the cockpit canvas.

### Milestone 8 follow-up (2026-08-17): HUD instruments

Four cockpit defects addressed; RE in `docs/formats/cockpit-hud.md` and `docs/formats/dfn-hfn-dci.md`.

- **Energy meter re-anchored.** The LED pinstripe bar was drawn at the shield display's rect; it
  belongs at `.GAU` offset 564 (`EnergyPoolGauge`, the Master Energy Pool meter under the TRACK
  button). Nothing is drawn at the shield display any more.
- **`.DFN`/`.HFN` decoded, `HudFont` added.** Glyph pool plus per-glyph offset and width arrays,
  `width * cellHeight` bytes each, one ink index per file. Glyphs pack into the existing HUD sprite
  atlas, so text costs no extra texture bind. This unblocked every readout below.
- **Weapon rows drawn** (`PWEAPONS` plate, hardpoint state box, slot number, weapon name). Names come
  from `SHELL0.VOL`'s `gam\WEAPONS.DAT` via `WeaponNameTable`, which reads that archive directly
  rather than mounting it — SHELL0 ships `DBA`/`DPL`/`DFN` folders whose names collide with
  SIMVOL0's, so mounting it would let shell art shadow simulator art.
- **Shield meter lit, not drawn.** Its rings are canopy art in palette indices 66-71;
  `CockpitPalette.InstallShieldRamp` reproduces `ShieldsGauge`'s per-frame six-entry palette write.
  Matches the retail screenshot to within one channel of the palette scalar's own rounding.
- **Console buttons and gunsight readouts** (`I`/`LINK`/`TRACK` plates and captions, `SPEED:` and
  `TIME:`) drawn from their own `.GAU` anchors in the fonts the original picks.
- **`.GAU` shield block corrected** from 628 to 616 (`HShieldDisplay`, `GauFileTransformer`). All nine
  retail files still round-trip byte-exact.

`CockpitHudState` carries the readouts' live values. Only the hardpoint names are real so far;
everything else sits at power-up defaults until the sim carries the state behind it.

**Not done:** the MFD still shows its blank screen frame. Compositing the `RADAR` frame over it needs
the MFD's sub-widget rects, which are in a `.GAU` region that is not decoded.

## Milestone 9 — Heads-Down Display (2026-08-20)

Leg 1: place `.HB1` and pan to it. RE in `docs/formats/cockpit-hud.md`, "Heads-down pan".

- **`CockpitViewGeometry`** reads `vue\<HERC>.VUE` for each view's canvas origin in device pixels.
  `HercWorks.Core`'s `Vue.Entry` field names were pre-RE guesses and are renamed to match.
- **`CockpitPan`** drives the transition. The original's slide loop
  (`CockpitView_StepViewTransition`, `0042a9c0`) is untimed, so there is no original duration to
  port — only a step count. Pinned at a fixed 0.4 s (mode 0's 24 steps at 60 Hz), continuous rather
  than stepped.
- **The whole composite slides**, 3D viewports included, reproducing the original's scroll over a
  640x960 cockpit canvas with `.HB0` at row 0 and `.HB1` at row 474. Draw order puts the HDD first so
  `.HB0` wins the six-row overlap, as the original's blit order does.
- **`Overlay2DRenderer.DrawHeadsDown`** fits the 4:3 art by height and stretches its outermost pixel
  columns into the side margins on wider windows — a Herculan addition; the original had no margins.
- Host: `[F7]`/`[F8]` down, `[F1]`-`[F6]` up, `--hdd` starts panned down for `--screenshot` runs.

Leg 2: the display's own GUI. Full RE in `docs/formats/heads-down-display.md`.

- **The whole display is authored per herc**, unlike the MFD: the `.GAU` block at 1212 carries an
  origin, four region rects, 15 widget rects, 3 marker rects and two mode values. `HddLayout` reads
  it out of `GAUFile.Remainder` and re-bases it onto the `.HB1` art. The block's origin y is biased
  `+0x28` before shifting, which lands it on the herc's own `.VUE` view-1 canvas origin — the check
  that confirms the block.
- **Widget-to-frame mapping confirmed by size**, the same method the MFD's button table passed: 90
  rect/frame checks across all nine retail files, 54 exact, the 36 misses being the two classes the
  original does not match either.
- **Both pages drawn**: command display (map viewport, 8 orders with their hotkey characters,
  XMIT/CANCEL) and damage detail (paper doll per category, 13 component rows). Comm boxes draw the
  unoccupied-slot fill.
- **Label placement corrected**, cockpit-wide. `Label_SetRect` centres the font's `0x1a` *ink height*
  (11), not its cell height (13), and works in integers throughout; `HudFont.Place` now owns that
  rule for the MFD, the HDD and the console readouts alike. Every label had been sitting 1.5 device
  pixels high. `.HFN` header fields `0x16` and `0x1a` move out of dfn-hfn-dci.md's open questions.
- Host: `[F7]`/`[F8]` select the page as well as panning, `[S]`/`[I]`/`[W]` switch damage category
  while that page is down, `--hdd [0|1]` and `--hdd-damage [0-2]` for `--screenshot` runs.

**Not done:** the map's terrain raster and its 140 markers, pilot video and static, order
availability and selection, per-component damage — all need sim state. Nor is RAZOR's non-stub view-1
3D viewport rendered.

## External view — placeholder (2026-08-22)

`[V]` toggles a chase camera while piloting: cockpit not drawn, the player's own HERC drawn, eye
10 m behind it, 6 m up, aimed 4 m up its hull, floored 2 m above the ground under it.

**Not reverse-engineered.** All five numbers are this engine's own, and the binding is a toggle
rather than the manual's own cycle through several external cameras. DBSIM's external view placement,
transitions, terrain handling and overlay chrome are unrecovered. `Render/ExternalCamera.cs` is the
single place the real rule replaces the guess — the host only asks it to place a `Camera`.

## Debug panel + skeleton view (2026-08-22)

`Esc` in the simulator host opens an ImGui debug panel; it no longer quits. Full description in
`docs/engine/handoff-player-movement.md`, "Debug view".

Why ImGui in the game host: the cockpit's font and sprite banks are the original's art placed from
the original's own layout files, and bending them into a live settings panel costs more than adding a
toolkit already in the tree. Nothing the game itself draws goes through ImGui.

Why a skeleton as well as posed geometry: it shows the nodes no geometry hangs from, and overlaid on
a posed machine it is the check that the pose the simulation holds and the pose being drawn are the
same one. It was built first, when the eye was the animation system's only observable output.

## Keyframe interpolation — SOLVED + SHIPPED (2026-08-22)

Poses are now blended between the playing keyframe and the frame playback is headed for, by the same
elapsed-frame fraction root motion was already ramped by. Full RE in
`docs/simulation/mech-locomotion.md`, "Keyframe interpolation"; the original's own blend
(`Anim_BlendKeyframeTransforms`, `00492600`) is ported to `AnimTransform.Blend`.

Found by looking for the builder of the `shapeInst+0x16` node-transform array, which the previous
pass had recorded as the one place any interpolation could live. It was, and it did. Symbols
applied via `ES2ApplySymbolNames` (7 new + one pre-existing entry extended; re-runs report 0 renames).

Pose cadence checked and settled: the original evaluates poses once per sim tick, and its whole loop
is capped at 25 Hz, so tick and frame are the same thing there. The engine's fixed 25 Hz tick
produces the same 25 poses a second — matching, not approximating.

## Node-posed geometry — SOLVED + SHIPPED (2026-08-22)

A machine's geometry is now drawn one node at a time, so the walk cycle that was already carrying it
across the ground also moves its legs. Full RE and port detail in
[`docs/formats/dts-node-posing.md`](../formats/dts-node-posing.md).

The mechanism was traced, not assumed: `TSGroup_RenderPolys` (`004758c8`) resolves the group's own
`TSBasePart.Transform` and composes that node's world transform with the object-to-view one
(`00476014` → `00476030`) before drawing a single poly. `DtsMeshBuilder.BuildSegments` splits a shape
accordingly and `MissionScene.PosedTransformOf` is the composition.

This also settles `ResolveGroupOffset`'s long-standing "rotation deliberately unapplied" note: no
retail HERC node has a rotation in its rest pose, so the load-time translation sum and the runtime
composition agree to the vertex. Rotation is what an animated node acquires.

## Turret twist and pitch — SOLVED + SHIPPED (2026-08-22)

A HERC's turret now aims independently of its legs, and the cockpit view looks where it points. Full
RE in [`docs/simulation/torso-aim.md`](../simulation/torso-aim.md).

The turret has no rotation of its own: each axis owns an animation thread whose sequence is one full
sweep of one node, and the angle selects a position within it (`AnimThread_SeekToPosition`,
`00479238`). A HERC therefore runs three threads at once, and `ShapeInstance` is the port of the
shape-level node evaluation that puts them together — first-registered thread wins a contested node,
as `ShapeInst_EvalAllNodeLocals` leaves it.

Two more record fields recovered from names nothing read: 26 `InputTorsoRazrFlag` and 34
`InputFlagsTorso` are the twist and pitch **sequence ids**. Symbols applied via
`ES2ApplySymbolNames`.

Cockpit camera orientation now comes from `MechObject.EyeTransform` — the camera node's own composed
frame, which is the frame `Mech_TargetRelativeToPilot` aims in — rather than from the machine's
heading. Measured first: the walk cycle rotates the eye by exactly zero, so this adds the turret and
nothing else.

## Reactor and Master Energy Pool — SOLVED + SHIPPED (2026-08-23)

Full RE in [`docs/simulation/reactor-energy-pool.md`](../simulation/reactor-energy-pool.md).

The reactor is a rate (`mech+0x256`), the pool a capacitor (`mech+0x292`, 0–10000, starts full).
`Mech_PerTickSystemsUpdate`'s first five statements are the whole model: integrate the rate, offer
`pool - 500` to the weapon mounts, offer the remainder to the shields, put `leftover + 500` back.
Consumption is an overwrite, not a subtraction, and 500 is a hard reserve.

Neither reactor output nor shield capacity varies per HERC — 20/tick and 3500 are literals shared by
the whole fleet (checked against every retail `.DAT`). Equipment pods are the only way to move
either, and both pod bonuses **double** their stat on the same five-step damage curve. Reactor output
is computed **once at spawn** (`FUN_00417d08` has one reference in the binary), so mid-mission
reactor damage never changes it.

Shipped: `MechObject.Power.cs`, `ShieldCharge.cs`, `MechPods.cs`, the cockpit energy meter (was
hard-coded full), live shield rings (the ramp was installed once at load with a nominal charge, so
they could never move), and `[`/`]` shield balance.

### Corrections this milestone

- **`Component_ReadHealthPercent` → `Component_ReadDamagePercent`.** It returns accumulated damage,
  not health: the arrays are zeroed at init and `FUN_0040d3ec` *adds*, capping at max and storing
  `-1` on destruction. The old name inverted the sense of every caller. `Component_AllocHealthArrays`
  and `Mech_ComponentHealthWrite` renamed to match; applied via `ES2ApplySymbolNames`.
- **`mech+0x317` is the Turbo Pod**, not an unidentified subsystem, and its speed term is a bonus
  that *fades* with damage rather than a runaway that grows — a direct consequence of the rename.
  Corrected in [`mech-locomotion.md`](../simulation/mech-locomotion.md).
- **Shield capacity is not a per-mech-type stat.** Every retail HERC carries 3500 at record offset
  190.
- **The cockpit's shield numbers show balance, not charge.** `ShieldsGauge_UpdateReadouts` prints
  `balance * 200 >> 10` and its literal complement, so the pair always sums to 200 whatever the
  charge. The rings are the charge indicator.

### Method note

Three conclusions in this milestone were wrong on the first pass and were caught by user review
against the retail build, not by static analysis: shield capacity read from the wrong premise, a
readout misread as charge, and a frame-rate explanation that a 25 Hz cap ruled out. Each was
resolved by disassembling the function rather than trusting the decompiler — `Shield_Init`'s
`ADD ESI,0x2` / `[ESI+0xbe]` pair, folded by the decompiler into `+0xc0`, is the representative
case. Prefer raw disassembly for any load-bearing offset or constant.
