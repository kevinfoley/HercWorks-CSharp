# Handoff — symbol names the docs and `known_symbols.json` disagree on

Scratchpad for a Ghidra pass. Nothing here is a finding. Every item below needs the binary: `git log` could not settle any of them (each doc name and its register entry first appear in the same commit, or the register never held the doc's name). Resolve an item by fixing the register, the docs, or both, then delete it. Delete the file when it is empty.

`tools/scripts/Check-Symbol.ps1 <address>` lists every mention of an address before you rename it. `tools/scripts/doc_links.py` catches a broken anchor if the rename touches a heading.

## Doc and register give different functions

- **`Sfx_StopAll` vs `Sfx_ServiceFinishedVoices` (`004647dc`).** `docs/formats/audio.md:161` says `Sfx_StopAll` (`004647dc`) stops every sample through `sosDIGIStopSample`; the register says `004647dc` sweeps the voice table asking which voices have *finished* and clears their `0x100` flag. Those are two different functions. Either the doc's address is wrong and `Sfx_StopAll` is an unregistered function elsewhere, or one description is wrong. `Herculan.Engine/Audio/SoundDirector.cs:559` also cites `Sfx_StopAll`, without an address.

## Doc uses a name the register marks `maybe_`

The doc presents the bare name as settled; the register holds it as a guess. If the doc's evidence holds up, drop `maybe_` in the register (and re-apply with `ES2ApplySymbolNames.java`); if not, the doc should say what is unconfirmed, in its `## Open` section.

| Doc name | Address | Register confidence | Mentions |
|---|---|---|---|
| `Scene_SubmitFrameObjects` | `0042841c` | medium | `distance-fog-and-sky.md:67`, `terrain-heightmap.md:74`, `terrain-texturing.md:154`, `mission-deployment.md:102`, `Program.cs:4015` |
| `Sim_RenderFrame` | `0045fb9c` | high — yet still `maybe_`, so the register contradicts itself | `cockpit-hud-widgets.md:406`, `mech-shape-drawing.md:85`, `terrain-texturing.md:150` |
| `Terrain_SetDistanceBands` | `00428bc0` | medium: the consumer of `DAT_004cfa0c` is not traced | `terrain-texturing.md:99`, `:153` |
| `Terrain_ComputeViewDistance` | `00470910` | medium | `terrain-texturing.md:102`, `:155` |
| `Salvage_QueueDestroyedWeapon` | `00426ac8` | medium: the list's consumer is not traced | `component-damage.md:162`, `weapon-damage-types.md:108` |
| `CockpitFontsAndCorners_Init` | `004544a4` | medium | `cockpit-hud-widgets.md:25` |
| `TSShapeInstance_PrepareRenderContext` | `0042fa18` | medium | `distance-fog-and-sky.md:59` |
| `Raster_SelectRenderTarget` | `00481118` | medium | `dts-billboards.md:65` |
| `SaveScreen_Teardown` | `00439d66` (VSHELL) | medium | `docs/shell/screen-layout.md:192` |
| `Mission_Show` | `004441e3` (VSHELL) | medium: identified from its dispatch position only | `docs/shell/screen-layout.md:206` |

Nine more `maybe_` names are still spelled `FUN_` in the docs because the `FUN_` cleanup would not put a guess into prose: `00416379` (`msn-mission-file.md`), `0041266a` (`weapons-dat.md`), `0045e480` (`dts-texture-binding.md`), `004045c8` (`ai-targeting.md`), `0048c338` (`dts-node-posing.md`), and `00441afa`, `00441eb8`, `00441f94`, `00442055` (VSHELL, the crew screen's actions, `docs/shell/screen-layout.md`). Confirming one is also the cue to replace its `FUN_` spelling.

## Doc names a function the register does not carry

The doc gives the address; the register has no entry there. Confirm and register them.

| Name | Address | Where |
|---|---|---|
| `Palette_ReadRange` | `00430440` | `cockpit-canopy-palette.md:83` |
| `Palette_GetEntry` | `00430474` | `cockpit-canopy-palette.md:84` |
| `SliderWidget_DragToPointH`, `_GetValueH`, `_SetValueH`, `_RecomputeScaleH` | `004524f8`, `00452544`, `0045255c`, `004525a8` | `cockpit-input.md:394` (the four `V` twins are registered) |
| `Bitmap_BlitClipped` | `004816bc` | `cockpit-canopy-palette.md:36`, `cockpit-views.md:232` |
| `Pool_Init` | `004719cc` | `sim-object-layout.md:53`, `ai-combat-states.md:304` |
| `Group_NearestLiveMember` | `00423974` | `ai-targeting.md:201` |
| `HddDisplay_ServiceCommBoxes` | `0044b5f8` | `Herculan.Engine/Content/SquadCommChannel.cs:5`, still spelled `FUN_0044b5f8` there |

## Doc names a function with no address

- **`AnimThread_SetSequence`** — `torso-aim.md:90`. Called by `AnimThread_SeekToPosition` (`00479238`), so the address is one call site away.
- **`Base_ExplosionSequenceTick`** — `Herculan.Engine/Sim/BaseObject.cs:382`. The register has no `Base_Explosion*` entry at all.

## Named for the wrong binary

- **`004092dc` in `docs/command-line.md` (2 mentions).** VSHELL's switch parser calls it after `-v`/`-?`; the register's only name for the address is DBSIM's `Smoke_Tick`, so it stays `FUN_004092dc` until VSHELL's function there is named.
