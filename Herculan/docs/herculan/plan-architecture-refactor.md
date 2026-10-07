# Plan — architecture refactor of `Herculan.Engine` and `Herculan.Engine.Host`

Make each namespace hold one kind of code, keep game rules out of the host, and point dependencies one way, without changing any algorithm, data layout or behaviour ported from the retail game.

Stage 1 is partly built; the rest is planned. Each item is re-read against the code before it is done: the review behind this plan was partly delegated, and an item's claim is a lead until then.

## Why

- **`Content` was a grab-bag.** Of its 73 files, 19 loaded data files; 50 were the cockpit's runtime state and logic, and the rest were install and input code.
- **Retail game rules live in the host, which no test project references.** By their own doc comments, `PlayerCockpitUpdate`, `PilotControls`, `ModalPanels`, `CampaignLoop` and `GameInProgress` port the player's per-frame cockpit update, the input poll's button switch, the alert panels' key handling and the save-slot load and save. [`planning.md`](planning.md#engine-internal-architecture) asks for a thin host.
- **Dependencies point both ways.** `GpuTexture` (Gl) takes a `TextureAtlas` (Render); `WeaponMount` clamps against `ChargeBarSlider.Range` (Cockpit); `SimWorld.MissionBounds` is a cockpit type; `FlyCameraObject` (Sim) writes a `Camera` (Render); Sim reads `TweakSettings.Current` at four sites; Render uses Sim in 18 files.
- **Large classes bundle unrelated jobs.** `SimWorld` (pools and tick, mission state, effects, raycasts, shot spawning, latches), `MissionScene.Load` (session bootstrap, mesh preload, write-back of shape data), `GameAudio` (audio stack plus the message-port clock and the power-up rule), `DebugPanel` (view, probes and view options, read by the frame stepper and renderer).
- **Earthsiege 1 support is under consideration.** Its executables are a different build (not Borland, an X-32 DOS extender) with a different shell (`GO.EXE`) and audio (`.SFX`, HMP through HMI SOS), but share the 3Space asset layer: VOL, the `TS*`/`GL*`/`AN*` classes, `.DTS`/`.DBA`/`.DPL`/`.DFN` headers, and 548 of 556 `.SNC` files byte-identical. A refactor that separates the asset layer from game-specific code serves that split.

## Constraints

- **Retail structure stays.** No change to an algorithm, a call order, a retail-mirrored state layout (`WeaponMount`, `BaseObject`, the `SimObject` hierarchy), `MissionLoader`'s two passes, or the sim's direct sound calls. Regrouping `SimWorld`'s fields, which gather DBSIM globals rather than mirror one struct, is allowed.
- **No speculative ES1 abstraction.** No game-version interfaces or switches until the ES1 survey (below) shows where the games differ — the same rule [`planning.md`](planning.md#rendering) applies to the render backend.
- **Folder is namespace**, as throughout both projects.
- **No nested namespace shares a short name with another namespace or a type.** A nested namespace captures qualified references and `cref`s inside its parent: `Render.Terrain` would capture `Terrain.HeightGrid` in `Camera.cs`, `Sim.Mission` would capture the `Mission` type, `Cockpit.Console` would capture `System.Console`. `Render.Cockpit` beside `Engine.Cockpit`, and `Host.Shell` beside `Engine.Shell`, already exist and are accepted.
- **Small companion types stay in their class's file** (an enum or record used only beside that class). Independent classes get their own file.
- **Shell types keep the `Shell` prefix**; dropping it would collide with Sim and Render names (`Map`, `Text`, `Palette`).

## Stage 1 — moves, renames and file splits

No logic changes; the compiler catches most mistakes.

Built:

- `Content` split: data loaders stay in `Content`; cockpit state and logic is `Cockpit` (one flat namespace, as `Sim` is); `GameInstall`, `RetailInstaller` and `LauncherLanguage` are `Install`; `JoystickCapabilities` is in `Input`.
- The GL-free camera and view state machines (`CockpitGlance`, `CockpitHitShake`, `CockpitPan`, `CockpitViewKick`, `ExternalCamera`, `ExternalViewChain`, `ExternalViewLayout`, `PlayerTrail`, `ViewCamera`) are `View`. `Camera` stays in `Render`: it produces the render-space eye and direction.
- `EngineWindow`, `ExecutableIcon`, `PrintScreenCapture` and `ScaledImGui` are `Platform`, in the engine assembly because both hosts use them.
- `CockpitReadouts` is `PlayerCockpitUpdate`, after the retail function it ports.
- `ShellWeaponUnit`, `ShellBayMachine` (with `ShellMachineStatus`), `ShellBayPilot`, `ShellLaunchRefusalDialog` and `ShellEndOfGameDialog` have their own files.

Remaining:

- Partial-file splits along retail prefixes, after reading each class whole: `WeaponMount` (charge, gauge, fire, condition), `MechObject` (movement and torso methods still in `MechObject.cs` move to the existing `.Locomotion` and `.Torso` partials; `.Combat` splits into damage and scoring), `BaseObject` (damage, death sequence), `ShellHangar` (armory, squad, grants, save), `ShellMap` (paint, intro; `ZoneRelief` to its own file), `DtsMeshBuilder`.
- `World` and `Settings` — see [Open](#open).

## Stage 2 — dependencies one way

- `ISoundSink`, `SoundId` and `SoundReach` move to Sim, so Sim stops importing Audio.
- The charge-bar range (`0x400`) is declared in Sim and `ChargeBarSlider` refers to it; `HddMapBounds` (script.dat block 1's box) moves out of `Cockpit`.
- `FlyCameraObject.ApplyTo(Camera)` moves to Render.
- `GpuTexture` takes pixels and a size; `TextureAtlas` uploads itself.
- Sim receives the tweak settings through `SimWorld` instead of reading `TweakSettings.Current`.
- No new dependency from asset-layer code (Content, Render, Gl, Platform, Audio back end) onto ES2 game code. The existing Render→Sim uses are left until the ES1 survey decides the split.

## Stage 3 — thin host

- The retail cockpit and campaign logic in `Host/Simulator/Cockpit` and `Host/Shell` (`PlayerCockpitUpdate`, `PilotControls`, `ModalPanels`, `CockpitCommands`, `CockpitKeyboard`, `PilotKeys`, `DeveloperKeys`, `CampaignLoop`'s decisions, `GameInProgress`) moves to `Engine.Cockpit` and `Engine.Shell`, returning outcomes instead of closing windows. Three seams are cut first: the pointer warp (`ModalPanels`→`SimulatorInput`), the manual and full-screen keys (`CockpitCommands`→`WindowKeys`), and `DebugPanel`. Tests are added as each piece moves; the moved code's call order is retail behaviour.
- `WorldDrawItems`, `TransientDrawItems`, `SceneUploads` and `DrawFiling` (the detail-level and part selection the draw pass ports) move to `Engine/Render`, where the editor can use them.
- `DebugPanel` splits into options, probes and view; `StagingOptions` into `StagedStart` (the `--staging` preset) and `StagedScreenshot` (the capture conditions).

## ES1 survey — gates Stage 4

Before Stage 4, enough ES1 reverse engineering to decide, per subsystem (formats, sim objects, mission start-up, shell, audio), whether ES1 matches ES2, differs in data only, or differs in structure. Three questions decide the most:

- Which compiler built ES1 — it decides whether any layout-mirroring code can be shared.
- Whether the modules both simulators name (`bullet`, `collide`, `damage`, `debris`, `flyersys`, `mechsys`) match function by function on a sample.
- Whether the fixed-point math routines are identical.

The answer, and how the two games share code (a shared core with one assembly per game, or version branches), goes in [`planning.md`](planning.md).

## Stage 4 — responsibility splits

Code moves verbatim and keeps its call order.

- `SimWorld` owns `MissionRuntime` (counters, actions, objectives, salvage), `EffectPools` (effects, debris, fires, lights) and `PlayerMissileState`; the raycasts and blast sweep become a static `HitTests`. `Tick`'s order is unchanged.
- `GameAudio` keeps building the audio stack; the message-port clock and the power-up announcement move to `Cockpit`; the `Music_*` layer leaves `SoundDirector`.
- `MissionScene.Load` splits into a sim bootstrap and a model set, last: the `Bind*` order and the `SimRandom` draws must not move, and mesh building is not to draw from `SimRandom`.

## Names considered and rejected

| Name | Why not |
|---|---|
| `Missions` for all of `World` | `World` also holds static game tables (`BaseTypeTable`, the formation tables, `TheaterDescriptor`), which are not missions. |
| `Menus` for `Host.Settings` | It holds only settings; "menus" reads as navigation. |
| `Tweaks` for `Engine.Settings` | It also holds the generic `SettingDefinition<T>` base. |
| `ShellViewport` for `ShellScreenLayout` | It is deliberately the shell's counterpart of `CockpitScreenLayout`; renaming one breaks the pair. |
| `IMusicPlayer` for `ICdAudio` | `ICdAudio` models the original's CD audio device, as its members' doc comments say, and `IMusicPlayer` reads like `IMusicSource`. |
| `MovieWindow` for `MovieHost` | `ShellHost`, `SimulatorHost` and `MovieHost` are one host per run mode, by design. |
| `Host/Windows`, `Host/Devices`, `Host/Startup` | "Windows" reads as the OS; `KeyState` also holds the replay keys; `HostSession` and `Launcher` are not start-up. |

## Verification

Every step:

- `dotnet build Herculan/HerculanEngine.sln` at 0 warnings and `dotnet test` green.
- Broken `cref`s, which the normal build does not report: build with `-p:GenerateDocumentationFile=true "-p:NoWarn=CS1591%3BCS1573"` and compare the CS1574/CS0419/CS1580/CS1584 messages against `main` (46 on `main`).
- Usings a move left unnecessary: build with `EnforceCodeStyleInBuild=true` and `dotnet_diagnostic.IDE0005.severity = warning`, and remove those in touched files.
- Extracted declarations are diffed line for line against the original file.
- `doc_lint.py` and `doc_links.py --code` report nothing new.
- Renames go through `tools/scripts/rename_symbol` where it builds.

Stages 3 and 4 move code whose order is behaviour. Their check is a replay diff: record input tapes over a few missions, dump simulation state, and compare before and after. Replays are not deterministic, so until they are, each moved piece gets unit tests first.

## Open

- **Open:** what `World` becomes — split into mission handoff and static tables (the tables' namespace name undecided), or left as it is.
- **Open:** whether `Engine.Settings` is renamed at all.
- **Open:** replay determinism, which Stages 3 and 4 need for their verification.
- **Open:** `tools/scripts/rename_symbol` fails to build on the .NET 8.0.1xx SDK (CS9057: its analyzers need compiler 4.12); renames in such an environment are done by hand and checked by the build.
- **Open:** `Host.Simulator.Cockpit` captures `Cockpit.X` inside the host; `CockpitDisplays` names `Engine.Cockpit.ThrottleTrack` for that reason. Stage 3 removes the clash.
- **Open:** the ES1 survey's three questions.
