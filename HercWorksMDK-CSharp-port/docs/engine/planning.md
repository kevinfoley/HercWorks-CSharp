# HERCULAN Engine — Planning

Living planning document for the Earthsiege 2 engine port, tracking decisions as they're made.
Started 2026-08-11. This is a working document, not a spec — update it as decisions change.

## Context

Long-term goal: a modern, cross-platform engine capable of running Earthsiege 2 using the
original game's data files. `HercWorksMDK-CSharp-port` (the "HercWorks" toolkit) is a separate,
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
  `HercWorksMDK-CSharp-port/src/`, added to the existing `HercWorksMDK.sln`. Both the engine and
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

## Open questions

None currently open — see next steps below.

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
