# Canopy art, blitting, and the cockpit palette

Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project. All addresses are DBSIM unless noted. Symbols are in `tools/ghidra_scripts/known_symbols.json`; apply with `ES2ApplySymbolNames.java`.

Verified against retail data in `ES2/VOL/simvol0/{hb0,hb1,hb2,hba,dpl}/`.

Engine implementation: `Herculan.Engine.Content.CockpitArt`, `Content.CockpitPalette`, `Render.CockpitHitShake`.

The view manager that loads this art per view, and the `.HD`/`.ED` viewport cutout it is blitted under: [`cockpit-views.md`](cockpit-views.md). The console and HUD widgets painted over it: [`cockpit-hud-widgets.md`](cockpit-hud-widgets.md).

## Canopy art — `.HB0`/`.HB1`/`.HB2` and `.DB0`/`.DB1`/`.DB2`

`CockpitCanopy_LoadViewBitmap` (`00429c2c`, `MECHVIEW.CPP:0x12e`).

No literal `"hb0"`/`"db0"` string exists anywhere in `DBSIM.EXE`. The folder name is built at runtime: the global folder literal `"dba"` (or `"hba"` when `VideoMode_UseHiResPanels == 3`) is copied to a stack buffer and index 2 overwritten with an ASCII digit via `_itoa`, giving `db0`/`db1`/`db2` or `hb0`/`hb1`/`hb2`. Then `ResourcePath_BuildFolderName(hercName, buf)` → `ClassItem_LoadResource`. The same trick produces `ed<i>`/`hd<i>` from `"edg"`/`"hdg"` — see [`cockpit-views.md`](cockpit-views.md#hd0-hd3--ed0-ed3--3d-viewport-clip-regions).

Files are `DynamixBitmapArray`s with one frame: `.DB*` 320x240 (76844 bytes), `.HB*` 640x480 (307244).

`CockpitCanopy_FreeViewBitmap` (`00429de4`) releases one view's handle, also nulling slot 3 when freeing view 2. Used only when `CockpitArt_LoadOnDemand` (`004d2704`) is set — a low-memory mode that loads and frees per view switch rather than keeping all four resident.

### Known defect in the retail code

The `maybe_CockpitLayoutMode == 1` branch increments byte 2 of the **shared global** `"dba"` literal (`MOV ECX,[0x4a0a28]; INC byte ptr [ECX+2]` at `00429d3e`) rather than its local buffer. The follow-on load still uses the unmodified local buffer, so that branch loads `db0` twice and corrupts the global folder name for every later user. Nothing in the image writes `004d25bc`, so the path is unreachable — see [`cockpit-views.md`](cockpit-views.md#video-modes).

## Blitting

`Bitmap_Blit` (`0048159c`) — `Bitmap_Blit(bitmap, {int x, int y}, flags)`.

| Flags | Effect |
|---|---|
| 0 | none |
| 1 | flip vertically |
| 2 | mirror horizontally |
| 3 | both |

Confirmed at the reticle corner-bracket draw (`0044401d`–`0044403a`), which blits one corner sprite four times with flags 0/2/1/3, offsetting x by the bitmap's width field (`+6`) for flag 2 and y by its height field (`+4`) for flag 1. `Bitmap_BlitClipped` (`004816bc`) is the same with an explicit clip rect, used only in `maybe_CockpitLayoutMode == 2`.

## Palette

**The live 256-slot palette is the theater palette, in full.** `World_LoadTheater` (`0042e010`) calls `Palette_LoadAndActivate` (`00430394`) with `dpl\world<N>`, and that object becomes `ActivePaletteObject` (`0049b020`); field `+8` is its 256 x 4-byte entry array.

**`COCKPIT.DPL` contributes exactly one 24-entry window.** `CockpitViewManager_LoadViews` issues a single call:

```
Palette_InstallRange(0x2a, 0x18, COCKPIT.DPL.entries + (schemeIndex*0x18 + 0x20)*4)
```

Live slots **42-65** ← `COCKPIT.DPL` entries `[32 + 24*schemeIndex, +24)`. No other site installs `COCKPIT.DPL`; its remaining 232 entries are never read.

`schemeIndex` is the mech type record's `+0x52`, i.e. **offset 80 of `dat\<MECH>.DAT`** — `HercSimDat.Unk80_ValHudId`. Retail values are a 0-8 permutation over the nine player hercs, so the nine schemes tile `COCKPIT.DPL` entries 32-247 exactly:

| Herc | scheme | COCKPIT.DPL entries |
|---|---|---|
| APOCA | 0 | 32-55 |
| COLOSSUS | 1 | 56-79 |
| SAMSON | 2 | 80-103 |
| MAVERICK | 3 | 104-127 |
| OGRE | 4 | 128-151 |
| OUTLAW | 5 | 152-175 |
| RAPTOR2 | 6 | 176-199 |
| RAZOR | 7 | 200-223 |
| TOMAHAWK | 8 | 224-247 |

`COCKPIT.DPL` is a 256-entry palette (1050 bytes: 9-byte prefix, `0F 00 28 00`, size `0x408`, start index 0, count 256, 256 x 4 bytes). Entry layout is `[R][G][B][flag=1]`, 6-bit channels scaled x4 — entries 1-7 are the textbook VGA blue/green/cyan/red/magenta/brown at `0x2a`.

Canopy art indices are used **as authored**; there is no shift, and the live palette is not assembled from two `.DPL` files.

### Corroboration

- The measured retail values resolve to it exactly: APOCA renders canopy index `i` as `COCKPIT.DPL[i-10]` (slot 42 → entry 32 = scheme 0); COLOSSUS as `COCKPIT.DPL[i+14]` (slot 42 → entry 56 = scheme 1).
- Every `WORLD<n>.DPL` parks precisely slots 42-65 at a flat green — the exact window the cockpit scheme overwrites.

Consequences now resolved: the heading tape's index 74 is a theater colour; the shield meter's green is a theater colour absent from `COCKPIT.DPL`; the canopy hazard stripes at index 13 render as the theater's yellow (measured 92% agreement at `(192,192,44)`).

### Palette module

| Symbol | Address | Role |
|---|---|---|
| `ActivePaletteObject` | `0049b020` | Live palette; `+8` = entry array. `0049b024` last uploaded, `0049b028`/`0x2c`/`0x30` dirty min/max/valid. |
| `Palette_LoadAndActivate` | `00430394` | Load a `.DPL` and make it active. |
| `Palette_SetActive` | `004303b0` | Swap active object, return previous. |
| `Palette_InstallRange` | `004303c4` | Copy `count` entries to `baseIndex`, extend dirty range. |
| `Palette_ReadRange` | `00430440` | Inverse of the above. |
| `Palette_GetEntry` | `00430474` | Single entry. |
| `Palette_FlushDirtyRange` | `0043048c` | Upload dirty range; whole palette if the active object changed. |
| `Palette_CycleAnimatedRanges` | `004306ac` | 5 slots of per-frame sub-range rotation, keyed off `Time_GetCoarseTicks`. |
| `Palette_InterpolateColours` | `004307b0` | Interpolate colour pairs across N steps. |
| `Palette_BeginCrossFade` | `004308fc` | Precompute 8.8 per-channel deltas between two palette objects; either may be null (fade to/from black). |
| `Palette_StepCrossFade` | `00430b34` | Advance one frame; returns remaining ticks. |
| `Palette_InterpolateIndexRanges` | `00430d08` | Interpolates index *ranges*, not colours — terrain shading only, driven from `WORLD<n>.WLD`. |

`Palette_BeginCrossFade`'s three call sites are all the **mech-death** screen flash: `FUN_0045dc34` (`death1`/`death2`/`world0` at base 0), and `FUN_0045d532`, which installs half-brightness 16-entry spans at bases 32 and 64 before setting up its fade. Neither is part of steady-state cockpit rendering, and neither touches the secondary palette.

### The damage shake

Taking a hit shakes the view and flashes the palette, for `0x3c` coarse ticks — 0.96 s. Two functions in `MECHVIEW.CPP` own it, and it is the sibling of the footfall kick below: the cockpit's per-frame pass (`FUN_004327ac`) ticks the two one after the other.

`Cockpit_StartHitShake` (`00434010`) arms it, and returns at once in view mode 4:

```
if (view.mode == 4) return
if (endTick != 0) { Palette_RestoreFromImpact(); CockpitView_ClearShake() }
endTick = now + 0x3c
if (nextToggleTick == 0) {
    nextToggleTick = now + rand() % 10
    Palette_ActivateImpact()
    CockpitView_SetShakeBand(5 << VideoMode_YCoordShift)
}
```

**A second trigger inside the window stops the shake rather than compounding it.** The restart restores the palette and clears the view band, and then finds `nextToggleTick` still non-zero — the tick function is the only thing that clears it, on expiry — so the arm block is skipped and neither is put back. `endTick` is extended all the same. So a hit 0.3 s into a shake buys another 0.96 s of `CockpitView_StepShake` calls against a disarmed band, which move nothing: the view goes still for the rest of the window while the palette carries on flipping. This engine reproduces it; see KNOWN_ISSUES.md.

`Cockpit_HitShakeTick` (`0043408c`) runs it: while `endTick` is in the future it calls `CockpitView_StepShake(rand() % 5)` every frame and flips the palette each time `nextToggleTick` expires, rearming that at `now + rand() % 10`. On expiry it restores both. Mode 4 clears `endTick` outright, so leaving the cockpit ends a shake in progress.

**The shake is the projection centre again, not a camera move** — the same mechanism as the step kick. `CockpitView_SetShakeBand` (`0042d2f8`) copies the resting view offset to `004cfae4`/`004cfae8` and sets two limits either side of it, `+amplitude` or `-amplitude` depending on the view mode; `CockpitView_StepShake` (`0042d4a8`) then moves the y offset toward **whichever limit is farther** by up to its step argument.

**Which limit that is flips at the band's middle**, so the walk reverses every time it crosses the centre and can never settle — and, after the first step off the limit it starts on, **it never reaches either limit again**. Moving outward requires being on the near side of the middle, so the furthest reachable offset is the largest sub-middle offset plus the largest step. With the band of `5 << VideoMode_YCoordShift` the arm passes — ten device pixels in the 640-wide modes — and steps of 0-4, the offset starts at the limit and thereafter ranges over 1 to 8. **The band is wider than the excursion it produces**, which is the trap in reading the amplitude as the travel.

The flash is a whole-palette swap rather than a fade: `Palette_ActivateImpact` (`0042ea44`) makes the secondary palette `ImpactPaletteObject` (`0049aef8`) active and saves the previous one, `Palette_ToggleImpact` (`0042ea70`) alternates the two, and `Palette_RestoreFromImpact` (`0042ea94`) puts the original back. That object is loaded twice over: `World_LoadTheater` (`0042e010`) builds it from the theater's own `IMPACT<n>.DPL` — `wld\WORLD<n>.WLD`'s third trailing string — and step 6 of the cockpit load sequence ([`cockpit-views.md`](cockpit-views.md#cockpitviewmanager_loadviews-sequence)) then overwrites its 24 cockpit indices from `IMPACTCP.DPL`. So a flash recolours the world and the cockpit together, each from its own source.

Its two triggers are both damage: a direct-fire hit on either of the player's own **cockpit** components while that component still reads under `0x64` damaged ([`../simulation/damage-system.md`](../simulation/damage-system.md#direct-fire-damage-armor-then-part-deterministic-shield-gated)), and the landing at the bottom of a long slide ([`../simulation/mech-locomotion.md`](../simulation/mech-locomotion.md#the-landing)). The first is gated and sits inside that function's band-change branch, so a shot that only scuffs the cockpit's armour is not felt; the second is ungated.

**Both halves are ported.** `Render/CockpitHitShake.cs` is the band, its walk and the flash's timer, driven off `MechObject.CockpitHits` the way the step kick is driven off `Footfalls`; the host folds the offset into the projection centre beside the kick's and follows `FlashActive` into the palette.

The flash is a swap between two prebuilt sets rather than a live palette write, because this renderer resolves the palette when it loads rather than per pixel. `Scene.ImpactFlash` carries the theater's two shade-ramp lookup textures and its sky and fog colours rebuilt against `IMPACT<n>.DPL`, and `CockpitFrame.ImpactPixels` carries the canopy art decoded a second time through `IMPACT<n>.DPL` + `IMPACTCP.DPL`. Both are built once with the scene.

`TSSolidPoly` follows the swap too, and by the same table. Its surface value is a palette index and its colour is `rampRow(UnlitShade)[index]` — **one fixed row of that same `PaletteRampTable`**, read at `ShadeRamp.UnlitShade`'s row in slice 0 rather than at the light's. So the index travels on the vertex (`MeshVertex.SolidPaletteIndex`) and the lookup happens per fragment, exactly as it does for a lit textured texel. The outline pass carries its line entry's index the same way. `DtsMeshBuilder.ResolveSolidColors` still resolves the colour and it still rides on the vertex, but only as the fallback for a theater whose palette ramp did not load.

**The two lookups are the same byte**, which is what makes moving it safe rather than a recolouring: compared over every palette index of all ten theaters, through both the ordinary palette and the impact one, the table row and the baked colour agree on all 5120 pairs.

The class is small overall — 2.8% of the triangle vertices across the 55 retail `.DTS` files — but it is not spread evenly, and where it lands is combat geometry:

| | flat-solid share |
|---|---|
| `ROCKETS`, `METEOR` | 100% |
| `FLAT2` | 90% |
| `BULLETS` | 66% |
| fitted weapon models (`MECHWPNS`, `MECHWPN2`) | 11-13% |
| machines, structures and the rest | 1.5% |
| debris (`*_DEB`) | 0.2% |

The HUD follows it too, in three parts, because its colour is resolved from the palette in three different ways: `CockpitArt` resolves `COLORS.DAT`'s ids and the raw palette slots into tables at load, so it holds **two** sets and `CockpitArt.FlashActive` picks between them; the sprite sheet's plates and glyphs are re-expanded from `TextureAtlas.IndexPixels` through the flash palette; and the heads-down map's relief raster is rasterized a second time, since it resolves its colours up front rather than per draw. Twenty of `COLORS.DAT`'s twenty-seven entries move under a retail impact palette, and they move a long way — HUD green `(64,212,40)` becomes orange `(208,92,0)`, white `(228,228,228)` becomes `(252,0,0)` — so a HUD that kept its colours would be the one part of the screen visibly refusing to flash.

The shield meter's rings are the deliberate exception. Their six colours are immediates in the exe (`0049c9cb`/`0049c9ce`) that `ShieldsGauge` writes into whichever palette is active on every frame, so they read the same through a flash in the original; the engine paints those same literals into both of the canopy's buffers.

`Herculan.Engine.Host` takes `--hit-shake`, which stages one hit and holds a `--screenshot` capture until the flash is up: a shake lasts under a second and the palette alternates inside it on its own 0-9 tick timer, so a fixed frame count is as likely to photograph the theater's palette as the impact one.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `Palette_BeginCrossFade` is the damage flash | Its three call sites are all the mech-death screen flash. The damage flash is a hard alternation between two whole palettes — `Palette_ActivateImpact`/`Palette_ToggleImpact` — with no fade of any kind between them |
| The shake's amplitude is how far the view travels | The band is the limit pair the walk is bounded by, not its excursion. A step is only ever taken toward the farther limit, which reverses at the middle, so a ten-pixel band produces a one-to-eight-pixel wander — see "The damage shake" |
