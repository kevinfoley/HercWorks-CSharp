# Cockpit rendering: canopy art, views, clip regions, palette, HUD

Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project. All addresses are DBSIM unless
noted. Symbols are in `tools/ghidra_scripts/known_symbols.json`; apply with `ES2ApplySymbolNames.java`.

Verified against retail data in `ES2/VOL/simvol0/{hb0,hb1,hb2,hba,hd0-3,ed0-3,vue,gau,dpl,dat}/`.

Engine implementation: `Herculan.Engine.Content.{CockpitArt, CockpitPalette, CockpitClipRegions,
HudSpriteSheet, HudFont, HudColorTable, CockpitHudState, WeaponNameTable}`,
`Herculan.Engine.Render.Overlay2DRenderer`.

How a mouse click on any of these widgets reaches its own click handler:
[`cockpit-input.md`](cockpit-input.md).

## Object model

| Symbol | Address | Role |
|---|---|---|
| `CockpitViewManagerInstance` | `004d2544` | Cockpit view state: current view, pending command, per-view assets. 0x37 bytes. Built in `Sim_InitMissionSession`. |
| `CockpitViewManager_Ctor` | `00429660` | Constructs the above; loads `dpl\cockpit` under a singleton guard. |
| `CockpitViewManager_LoadViews` | `00429834` | Whole cockpit bring-up (below). |
| `CockpitViewInstance` | `0049b088` | The GAU widget tree, owned by the manager. |
| `Gau_BuildCockpitWidgets` | `00431bf8` | Builds that tree from `gau\<HERC>.GAU`. |

Translation units: `MECHVIEW.CPP` (view manager, `00429660`–`0042ab00`), `PANEL.CPP` (widget tree,
`00431008`–`00434400`), palette module (`00430346`–`00430e40`).

`CockpitViewManagerInstance` fields:

| Offset | Contents |
|---|---|
| `+0x00` | View count, from `.VUE` (4 in every retail file) |
| `+0x04` | `.VUE` records: `viewCount x 32` bytes |
| `+0x08` | Canopy bitmap handles, 4 pointers |
| `+0x0c` | Clip-region blocks, `4 x 0x204` bytes |
| `+0x10` | Per-scanline span tables, 4 pointers |
| `+0x14` | Current view index (0-4, -1 before first switch) |
| `+0x18` | Pending view command (-1 = none) |
| `+0x1f` | `CockpitViewInstance` |
| `+0x29` | Owning mech object |
| `+0x2d` | Herc model name, 8 chars |

### `CockpitViewManager_LoadViews` sequence

1. Load `vue\<HERC>`: `int32 viewCount`, then `viewCount x 32`-byte records.
2. Allocate the four per-view slot arrays above.
3. Per view `i`: `CockpitClipRegions_Load` on `ed<i>`/`hd<i>`, then
   `ClipRegions_BuildScanlineSpans`; and unless `CockpitArt_LoadOnDemand`,
   `CockpitCanopy_LoadViewBitmap` for `db<i>`/`hb<i>`.
4. Build `CockpitViewInstance` (`00431008` → `ColorSchemePanels_LoadAll`) and
   `Gau_BuildCockpitWidgets`.
5. Install the per-herc cockpit colour scheme (see Palette).
6. Install `IMPACTCP.DPL`'s same-index scheme into the secondary palette `DAT_0049aef8` for the
   damage flash.

## Views

Four views, indexed 0-3, plus 4 = external/no-cockpit.

| View | Canopy bitmap | Blit flags | 3D viewport | Canvas origin | Clip file |
|---|---|---|---|---|---|
| 0 forward | `DB0`/`HB0` | 0 | full | `(0,0)` | `ed0`/`hd0` |
| 1 heads-down | `DB1`/`HB1` | 0 | **empty** | `(0,237)` | `ed1`/`hd1` (stub) |
| 2 glance | `DB2`/`HB2` | 0 | narrower | `(+320,0)` | `ed2`/`hd2` |
| 3 glance, opposite | `DB2`/`HB2` | **2 = mirror X** | full | `(-320,0)` | `ed3`/`hd3` |
| 4 external | none | — | full | — | default block at `DAT_004cfb1c` |

Views 2 and 3 share one bitmap handle: `CockpitCanopy_LoadViewBitmap` maps view to file index as
`view > 2 ? view - 1 : view`, and after loading file 2 stores the same handle in slot 3. View 3 is
drawn horizontally mirrored. There is no separate mirrored asset.

`CockpitView_ProcessViewCommand` (`0042a4c4`) applies ∓`0x3600` (~76°) to the pilot view yaw when
entering views 2/3 and undoes it on return to view 0.

### View switching

- `CockpitView_QueueViewCommand` (`0042a3f4`) latches a command at `+0x18`, gated on the current view.
- `CockpitView_ProcessViewCommand` (`0042a4c4`) executes it.
- `CockpitView_SetView` (`0042a1f0`) does the work: `CockpitView_ApplyViewState`, then one
  `Bitmap_Blit` of the canopy at `(0,0)`, then `FUN_004316c0` repaints every cockpit widget.
- `CockpitView_ApplyViewState` (`00429e60`) copies the view's `0x204`-byte clip block into the render
  context (`DAT_006c5ff4 + 4`), points `ActiveScanlineClipSpans` at its span table, and installs the
  `.VUE` rect into context slots `0x84`-`0x89`.

**The canopy is blitted once per view change, not per frame.** The 3D scene is then rasterized over
it every frame, span-clipped to `ActiveScanlineClipSpans`; HUD widgets repaint on top.

Command values latched at `+0x18`, and the current-view gate each requires:

| Command | Gate (current view) | Effect |
|---|---|---|
| 0 | 0 | pan down one view — forward → heads-down |
| 1 | 1 | pan up one view — heads-down → forward |
| 2 | not 4 | external view |
| 3 | 4 | return from external |
| 4 | 0 (or 3 → 6) | glance to view 2 |
| 5 | 0 (or 2 → 6) | glance to view 3 |
| 6 | 2 or 3 | return from a glance to view 0 |

Key sites: `FUN_00433a88` maps one bound key per axis (`+0x21a` heads-down, `+0x21e`/`+0x222` the two
glances) and picks the command by current view; `FUN_00432b14` reads four separate device-state bytes
at `+0x1e`-`+0x21` for commands 1/0/5/4. The manual binds `[F7]`/`[F8]` to the heads-down key and
`[F1]`-`[F6]`/`[Esc]` to the way back.

### Heads-down pan — `CockpitView_StepViewTransition` (`0042a9c0`)

Called once per frame from `Sim_EndFrame` (`0045fa98`), immediately before
`CockpitView_ProcessViewCommand`. A view change spans three frames:

1. A key calls `CockpitView_QueueViewCommand`, latching `+0x18`.
2. `CockpitView_ProcessViewCommand` installs the destination view's clip block and canvas origin on
   the back page (via `CockpitView_ApplyViewState`, no blit) and sets the transition flag `+0x1c`.
3. `CockpitView_StepViewTransition` runs the whole slide, then `+0x14 += 1` (or `-1`), `+0x18 = -1`,
   and `+0x1d = 2` — a two-frame cooldown that `CockpitView_ProcessViewCommand` decrements and
   returns on before doing anything else.

The slide itself, for commands 0/1:

```
travel = vue[dest].canvasOriginY - vue[src].canvasOriginY     -- 237, or 474 in the 640x480 modes
for (i = 0; i < travel; i += 10)
    displayOriginY += 10
    SetDisplayOrigin(page, {x, displayOriginY})               -- DAT_004a5800
displayOriginY += travel - i                                  -- final remainder step
```

Step is 10 canvas rows; `maybe_CockpitLayoutMode == 2` doubles it and forces travel to `0x1e0`. The
side-glance commands (4/5/6) use step `0x14` and scroll on x instead.

**There is no timing in this loop** — no timer, no retrace poll, no frame boundary. Its real-time
duration is whatever the host CPU makes it, and only a step *count* is recoverable: 24 steps in mode
0, 48 in modes 1/2, since the step is in device rows and the coord shift doubles the travel.

**Both views' canopies are resident in the canvas throughout.** `Sim_InitMissionSession` (`004614fc`)
calls `CockpitView_SetView(mgr, 1)` and then `CockpitView_SetView(mgr, 0)` during bring-up, so the
pan is a pure scroll and never a redraw. That order also settles the six-row overlap where the two
blits meet — `.HB1` lands at canvas row 474 and `.HB0` runs to 479, so **`.HB0` wins**.

Herculan: `Herculan.Engine.Render.CockpitPan`, `Content.CockpitViewGeometry`,
`Render.Overlay2DRenderer.DrawHeadsDown`. The pan is pinned to a fixed 0.4 s (mode 0's 24 steps at
60 Hz), expressed as a duration so both asset sets pan at one speed, and interpolated continuously
rather than in 10-row jumps.

## `.VUE` — per-view geometry

After the 9-byte VOL prefix: `int32 viewCount`, then `viewCount x` 8 `int32`s. All coordinates are
authored in the 320-wide space and shifted by `VideoMode_X/YCoordShift`.

| Field | Meaning |
|---|---|
| 0-3 | 3D viewport rect `x0, y0, x1, y1` |
| 4-5 | View centre, `cx, cy` |
| 6-7 | Canvas origin `originX, originY` |

C# port: `HercWorks.Core.Data.File.Dbsim.Vue.Entry` (fields renamed to match the above 2026-08-20;
they were `WidthMax`/`UnkOfs*` pre-RE guesses). Engine wrapper: `Content.CockpitViewGeometry`.

Every retail `.VUE` gives view 1 the canvas origin `(0,237)` — no herc differs.

`APOCA.VUE` (`viewCount = 4`):

| View | Rect | Centre | Canvas origin |
|---|---|---|---|
| 0 | `0,0 – 320,186` | `-160,-95` | `0,0` |
| 1 | `0,0 – 0,0` | `-160,-95` | `0,237` |
| 2 | `0,0 – 287,231` | `-160,-95` | `320,0` |
| 3 | `0,0 – 320,231` | `-160,-95` | `-320,0` |

View 1's zero-size rect is why the heads-down view shows no 3D. **RAZOR is the sole exception** —
`0,0 – 320,181`, matching its 2368-byte `.HD1` against every other herc's 16-byte stub.

### Cockpit canvas

`CockpitCanvasWidth`/`Height` (`004d25d2`/`004d25d6`) are 320x480 in mode 0 and 640x960 in modes 1/2 —
taller and wider than the 3D viewport (`004d25c2`/`004d25c6` = 320x240 / 640x480). The canvas is a
virtual space the views window into at their `.VUE` origins: rows 0-239 the forward cockpit, rows
237-476 the heads-down display, x ±320 the side views.

**No retail `.GAU` uses more than the forward quadrant.** Widget origins across all nine hercs span
`x:[3..298] y:[1..230]`, so the declared `HudScreenSize` of (320,400) overstates the used range and
the side views have no widgets of their own.

## `.HD0`-`.HD3` / `.ED0`-`.ED3` — 3D-viewport clip regions

`CockpitClipRegions_Load` (`0042dcf0`). Layout after the 9-byte VOL prefix, all fields little-endian
`int16`:

```
int16 rectCount
rectCount x { int16 y0, int16 y1, int16 x0, int16 x1 }      -- inclusive on all four edges
int16 blockCount
blockCount x {
    int16 firstRow, int16 rowCount,
    rowCount x { int16 xStart, int16 xEnd }                 -- one entry per scanline, inclusive
}
```

Coordinates are shifted by the caller's `(xShift, yShift)`: `(0,0)` for the `.HD*` set (already
640-wide), `VideoMode_X/YCoordShift` for `.ED*`. Inclusive ends are expanded as
`end = (end << shift) + (1 << shift) - 1`. Output is a `0x204`-byte block: `int count` plus up to 128
region pointers, rects tagged type 0 and span blocks type 2.

`ClipRegions_BuildScanlineSpans` (`0048b9a8`) flattens that into a table of
`0xf0 << VideoMode_YCoordShift` rows (240 or 480), each `{ int spanCount, ptr to spanCount x {int
start, int length} }`, sorted by start, and stores it in `ActiveScanlineClipSpans` (`004a5b10`).
The polygon rasterizer (`00468310`) indexes it by row and skips rows with zero spans.

**This is the viewport cutout mechanism.** DBSIM never colour-keys the canopy art, and palette index 0
has no special meaning in it.

Blocks may overlap and repeat — `APOCA.HD0` lists `row 204 +168` twice — which is harmless because
flattening accumulates every region per row.

Parsed extents (`hd0`/`hd2`, all nine hercs; span counts after flattening):

| Herc | hd0 rows/spans | hd2 rows/spans | hd1 spans |
|---|---|---|---|
| APOCA | 372 / 666 | 462 / 538 | 0 |
| COLOSSUS | 350 / 694 | 407 / 424 | 0 |
| SAMSON | 352 / 762 | 436 / 446 | 0 |
| MAVERICK | 450 / 1050 | 442 / 495 | 0 |
| OGRE | 388 / 734 | 447 / 467 | 0 |
| OUTLAW | 392 / 768 | 480 / 480 | 0 |
| RAPTOR2 | 334 / 720 | 434 / 477 | 0 |
| RAZOR | 480 / 948 | 480 / 630 | **584** |
| TOMAHAWK | 380 / 958 | 430 / 430 | 0 |

Every file consumes its whole body under this layout with 3 constant trailing bytes unread. RAZOR is
the only herc with a non-stub view-1 file, matching the file sizes on disk (`APOCA.HD1` is 16 bytes,
both counts zero; `RAZOR.HD1` is 2368). `APOCA.HD0` resolves to rows 0-371, matching the
independently measured index-0 bounding box on `APOCA.HB0` (`y:[0..371]`).

Every rect in every retail file has `x0 == 0`. This matters because DBSIM's flattening step feeds a
rect's fourth field to the rasterizer as a span *length* (`piVar4[1] = piVar1[3]`, against
`end - start + 1` for span blocks) while the loader's own shift arithmetic treats it as an inclusive
end. With `x0 == 0` the two readings differ by one column at the right edge and nothing else;
`CockpitClipRegions` takes the inclusive reading.

## Canopy art — `.HB0`/`.HB1`/`.HB2` and `.DB0`/`.DB1`/`.DB2`

`CockpitCanopy_LoadViewBitmap` (`00429c2c`, `MECHVIEW.CPP:0x12e`).

No literal `"hb0"`/`"db0"` string exists anywhere in `DBSIM.EXE`. The folder name is built at runtime:
the global folder literal `"dba"` (or `"hba"` when `VideoMode_UseHiResPanels == 3`) is copied to a
stack buffer and index 2 overwritten with an ASCII digit via `_itoa`, giving `db0`/`db1`/`db2` or
`hb0`/`hb1`/`hb2`. Then `ResourcePath_BuildFolderName(hercName, buf)` → `ClassItem_LoadResource`.
The same trick produces `ed<i>`/`hd<i>` from `"edg"`/`"hdg"`.

Files are `DynamixBitmapArray`s with one frame: `.DB*` 320x240 (76844 bytes), `.HB*` 640x480 (307244).

`CockpitCanopy_FreeViewBitmap` (`00429de4`) releases one view's handle, also nulling slot 3 when
freeing view 2. Used only when `CockpitArt_LoadOnDemand` (`004d2704`) is set — a low-memory mode that
loads and frees per view switch rather than keeping all four resident.

### Known defect in the retail code

The `maybe_CockpitLayoutMode == 1` branch increments byte 2 of the **shared global** `"dba"` literal
(`MOV ECX,[0x4a0a28]; INC byte ptr [ECX+2]` at `00429d3e`) rather than its local buffer. The follow-on
load still uses the unmodified local buffer, so that branch loads `db0` twice and corrupts the global
folder name for every later user. Nothing in the image writes `004d25bc`, so the path is unreachable.

## Blitting

`Bitmap_Blit` (`0048159c`) — `Bitmap_Blit(bitmap, {int x, int y}, flags)`.

| Flags | Effect |
|---|---|
| 0 | none |
| 1 | flip vertically |
| 2 | mirror horizontally |
| 3 | both |

Confirmed at the reticle corner-bracket draw (`0044401d`–`0044403a`), which blits one corner sprite
four times with flags 0/2/1/3, offsetting x by the bitmap's width field (`+6`) for flag 2 and y by its
height field (`+4`) for flag 1. `Bitmap_BlitClipped` (`004816bc`) is the same with an explicit clip
rect, used only in `maybe_CockpitLayoutMode == 2`.

## Palette

**The live 256-slot palette is the theater palette, in full.** `World_LoadTheater` (`0042e010`) calls
`Palette_LoadAndActivate` (`00430394`) with `dpl\world<N>`, and that object becomes
`ActivePaletteObject` (`0049b020`); field `+8` is its 256 x 4-byte entry array.

**`COCKPIT.DPL` contributes exactly one 24-entry window.** `CockpitViewManager_LoadViews` issues a
single call:

```
Palette_InstallRange(0x2a, 0x18, COCKPIT.DPL.entries + (schemeIndex*0x18 + 0x20)*4)
```

Live slots **42-65** ← `COCKPIT.DPL` entries `[32 + 24*schemeIndex, +24)`. No other site installs
`COCKPIT.DPL`; its remaining 232 entries are never read.

`schemeIndex` is the mech type record's `+0x52`, i.e. **offset 80 of `dat\<MECH>.DAT`** —
`HercSimDat.Unk80_ValHudId`. Retail values are a 0-8 permutation over the nine player hercs, so the
nine schemes tile `COCKPIT.DPL` entries 32-247 exactly:

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

`COCKPIT.DPL` is a 256-entry palette (1050 bytes: 9-byte prefix, `0F 00 28 00`, size `0x408`, start
index 0, count 256, 256 x 4 bytes). Entry layout is `[R][G][B][flag=1]`, 6-bit channels scaled x4 — entries 1-7
are the textbook VGA blue/green/cyan/red/magenta/brown at `0x2a`.

This supersedes the earlier "assembled from two `.DPL` files" model and
`CockpitArt.PaletteIndexOffset`. Canopy art indices are used **as authored**; there is no shift.

### Corroboration

- The measured retail values resolve to it exactly: APOCA renders canopy index `i` as
  `COCKPIT.DPL[i-10]` (slot 42 → entry 32 = scheme 0); COLOSSUS as `COCKPIT.DPL[i+14]` (slot 42 →
  entry 56 = scheme 1).
- Every `WORLD<n>.DPL` parks precisely slots 42-65 at a flat green — the exact window the cockpit
  scheme overwrites, and no wider.
- Pixel comparison of decoded `.HB0` against the two retail reference screenshots in `Reference/`,
  over opaque pixels: APOCA 69.4% exact RGB / 81.5% within 12/765 / mean channel error 11.8;
  COLOSSUS 78.7% / 89.2% / 9.2. Per source index, every scheme index bar two agrees at 85-100%.
  The two outliers (APOCA 42 and 53, COLOSSUS 42 and 46) are the largest flat fills, and their
  disagreements are confined to `x:[154..535] y:[306..466]` and `x:[208..434] y:[326..446]` — the
  scanner/MFD/button block, where retail paints live HUD content over the art. The same indices agree
  across the whole rest of the frame.

Consequences now resolved: the heading tape's index 74 is a theater colour; the shield meter's green
is a theater colour absent from `COCKPIT.DPL`; the canopy hazard stripes at index 13 render as the
theater's yellow (measured 92% agreement at `(192,192,44)`).

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

The impact/damage flash is `Palette_BeginCrossFade` between the live palette and `IMPACTCP.DPL`'s
same-index 24-entry scheme, which is why `IMPACTCP.DPL` and `COCKPIT.DPL` are the same size.

`FUN_0045dc34` (`death1`/`death2`/`world0` at base 0) is the mech-death screen flash. `FUN_0045d532`
installs half-brightness 16-entry spans at bases 32 and 64 and sets up a cross-fade; it is not part of
steady-state cockpit rendering.

## Video modes

`VideoMode_Configure` (`0045e4f4`) sets the whole block from a mode byte read out of the prefs file.

| Mode | `VideoMode_UseHiResPanels` (`004d25bb`) | `VideoMode_UseHiResBanks` (`004d25f0`) | Viewport | Canvas | Coord shifts |
|---|---|---|---|---|---|
| 0 | 0 | 0 | 320x240 | 320x480 | 0 |
| 1 | 3 | 0 | 640x480 | 640x960 | 1 |
| 2 | 3 | 1 | 640x480 | 640x960 | 1 |

`UseHiResPanels == 3` selects `.HFN` fonts, `hba\` sprite banks, `hb<n>` canopy art and `hd<n>` clip
files. `UseHiResBanks` separately selects hi-res banks for `hudhtick`, `mfd`, `radar`, `hdd`,
`pweapons`, `wpn_dmg`, `weapons`, `pdg`, `bases`, `vehicles`, `flyers` and the alert banks — which is
why two different flag idioms appear at the bank load sites.

`maybe_CockpitLayoutMode` (`004d25bc`) is never written anywhere in the image. Value 1 is the defective
path described above; value 2 routes blits through `Bitmap_BlitClipped` and moves the view origin into
`DAT_004d25da`/`de`.

## HUD sprite art — `.HBA`/`.DBA`

Every bank ships twice under the same name: `dba\NAME.DBA` for the 320-wide mode and `hba\NAME.HBA`
for the 640-wide one, exactly 2x on both axes, frame for frame, with identical frame counts. The two
folder-name literals sit adjacent to each bank name in `.rdata` (`"NAME\0hba\0dba\0"`). `corners` is
hardcoded to `dba`; `hba\CORNERS.HBA` does not exist.

Load path: `ResourcePath_BuildFolderName(name, folder)` → `Resource_Load` (`0045cdd8`) →
`ClassItem_LoadResource`.

| Bank | Owning function | Role |
|---|---|---|
| `hud` | `Gau_RovingGunsightWidget` (`0043c7d8`) | gunsight / reticle |
| `hudhtick` | `HudHeadingTape_Ctor` (`0043b57c`) | heading tick tape |
| `mfd`, `mfd_dmg`, `radar` | `MfdDisplay_Ctor` (`00445218`) | multi-function display — see [`mfd.md`](mfd.md) |
| `hdd`, `static`, `hddclip`, `pilotN` | `HddDisplay_Ctor` (`00448cc8`), `HddGauge_LoadPilotFrames` (`0044a7c0`) | heads-down display — see [`heads-down-display.md`](heads-down-display.md) |
| `pweapons`, `wpn_dmg` | `WeaponGauge_Ctor` (`0044080c`) | weapon hardpoint plates |
| `throttle` | `ThrottleGauge_Ctor` (`00447b84`) | throttle slider knob |
| `sysbuttn`, `icons`, `corners` | `maybe_SysButtonPair_Ctor` (`00434368`), `maybe_IconGadget_Ctor` (`0044f130`), `maybe_CockpitFontsAndCorners_Init` (`004544a4`) | |

Widget class names from the same string table: `HUDPipper`, `HUDCrosshairGunsight`,
`HUDRovingGunsight`, `HUDLockingGunsight`, `MFDRadar`, `MFDStatus`, `MFDMissileView`, `MFDFlashComm`,
`HDDGauge`, `HDDisplay`, `HDDDamage`, `HDDMapGadget` (see
[`heads-down-display.md`](heads-down-display.md)), `ShieldsGauge`, `EnergyPoolGauge`,
`ThrottleGauge`, `LEDBarGraphV`/`LEDBarGraphH`, `WeaponSelectGadget`, `ChainedWeaponSelectGadget`.

Frame-to-state mapping, as far as it is traced: `PWEAPONS` 0/1 are the selected/unselected row plate,
2/3 the unlit/lit console-button plate, 4/5/6 the hardpoint state box (green / red / amber), 7 a
640x80 strip with no located consumer; `WPN_DMG`'s 10 frames are damage fill levels, frame 0 the
opaque empty plate; `THROTTLE` 0 is a 2x12 tick and 1 the 28x12 knob; `RADAR`'s 10 110x110 frames are
the sweep animation; `MFD` 0-2 are 196x122 screen chrome, 3-10 five button plates in unlit/lit pairs
(see [`mfd.md`](mfd.md)).

## `.GAU` widget tree

`Gau_Load` (`00431778`, `PANEL.CPP:0x1d6`) reads a `0x6a4`-byte struct and constructs six sub-widget
vectors. The file's first two `int32`s are an origin offset added to every widget rect.
`Gau_BuildCockpitWidgets` (`00431bf8`) then builds seven top-level widgets from fixed offsets and
shifts every rect by `VideoMode_X/YCoordShift`.

GAU coordinates are authored in the 320-wide space; the engine's `CockpitArt.GauToPixelScale = 2`
maps them onto 640-wide art. See "Cockpit canvas" above for the y-range question.

## `dat\COLORS.DAT` — logical colour ids

54-byte payload, 27 `int16` palette indices. HUD data files carry a small logical id, resolved once at
load time through this table in place (`arr[i] = table[arr[i]]`). The table lives at `HudColorTable`
(`004d3c00`) in `.bss`, read at 16 distinct offsets by ~60 functions; no code materialises that address
to write it, so it is filled from the file.

Verified: the heads-down display resolves ids 19, 9, 15, 12 → palette 16, 10, 13, 14 — black, red,
yellow, green, matching the retail HDD readouts.

Consumers: `PaperDollGraphic.ViewRegion` at record offset `0x14`; `FUN_0045079c` (4-entry id array at
`DAT_0049d9ec`); `HudColorTable_Get` (`00434280`).

## LED gauges

`LEDBarGraphH`/`LEDBarGraphV`. `LedBarGraph_Ctor` (`004395c4`) installs vtable `PTR_FUN_0049bd30` over
`LedBarGraph_CtorBase` (`004390c4`), which precomputes:

```
span    = (end - start) * 0x10000 / range     // range is the caller's value scale
current = span < 1 ? end : start              // sign of span selects fill direction
```

`LedBarGraph_PaintToValue` (`004395e8`) fills to `start + (value * span >> 16)` (16.16 fixed point),
then installs field `0x24` as the draw colour and covers the remainder.

The filled span is not solid. `LedBarGraph_FillPinstripe` (`00439758`) walks the x range twice — once
over even columns, once over odd — drawing a full-height line each step: field `0x2c` paints even
columns, `0x30` odd. Two near-identical shades interleaved at one pixel read as a single shaded fill.

Both class variants fill along **x**: `LedBarGraph_CtorBase` takes start/end from the rect's `x0`/`x1`
(`param_2[0]`/`param_2[2]`), and the pinstripe walk strides columns.

`EnergyPoolGauge_Ctor` (`00444d5c`) constructs one over the `.GAU` widget rect at 564 with range
`0x400`, writing colour ids 6 and 5 into `0x2c`/`0x30` and id 19 into `0x24`. Those resolve to palette
indices 98/97/16 = `(0,116,204)`, `(0,40,160)`, `(0,0,0)` — the blue pinstripe bar retail draws
directly under the TRACK button, i.e. the **Master Energy Pool meter** (reactor charge). Its only
caller is `Gau_EnergyMeterWidget`, and the binary's own class-name table pairs `EnergyPoolGauge` with
`LEDBarGraphV` (file offset 280429) and `ShieldsGauge` with `ShieldsSelectGadget` (279148) — the LED
bar is the energy meter, and `ShieldsGauge` is a different class entirely.

A second `LEDBarGraph` per weapon row carries the energy-weapon charge field (`FUN_00442950`, range
`0x400`, colour ids `0x20`/`0x22`, remainder `0x2e`).

## `ShieldsGauge`

`ShieldsGauge_Ctor` (`004434fc`), called only by `Gau_ShieldDisplayWidget` (`00432454`) with `.GAU`
offset 616. It loads no sprite bank and **draws no geometry**: it builds a `0x40`-byte child per
facing (`ShieldsGauge_FacingCtor`, `00444aec`) whose paint slot (`FUN_00444b5c`) only tests
visibility, plus two text labels.

**The meter is lit, not drawn.** The nested concentric rings are painted into the herc's own canopy
art in palette indices 66-71 — verified on `OUTLAW.HB0`, where those six indices appear only inside
the meter bezel, three per facing, the innermost ring using the fewest pixels. The gauge's paint
(`00443730`/`00443748`) does two things per frame: rewrite those six palette slots
(`ShieldsGauge_UpdateRingPalette`) and refill the two readouts (`ShieldsGauge_UpdateReadouts`).

### Ring ramp — `ShieldsGauge_UpdateRingPalette` (`004438f0`)

Per facing (charge at object `+0xb5` and `+0xb9`), three rings light in turn as charge rises:

```
ring 1: t = v
ring 2: t = v < 0x100 ? 0 : min((v - 0x100) * 2, 0x400)
ring 3: t = v < 0x180 ? 0 : min((v - 0x180) * 4, 0x400)
colour = base * t >> 10                                  -- Q10
```

Above `0x400` (an overcharged shield) the same three tracks run again over
`base + (bright - base) * t`, with `v` taken as `charge - 0x400`.

Colour immediates at `ShieldRingColors` (`0049c9cb`): base RGB6 `(25,59,23)`, bright `(59,59,23)`.
`Palette_InstallRange(0x42, 6, ...)` reads the six entries up the stack, so **66-68 are the first
facing outermost-first and 69-71 the second**.

A facing runs 0..`0x800` with `0x400` the whole pool on one side, so an even 100/100 split parks both
at `0x200`, where all six rings resolve to RGB `(48,116,44)`. The retail screenshot's meter is
`(48,117,44)` — the one-channel difference is the palette scalar's own rounding.

### Readouts — `ShieldsGauge_UpdateReadouts` (`00444a68`)

`itoa(balance * 200 >> 10)` into the first label and its complement into the second, from the fore/aft
balance at `+0xbd`. An even split reads 100 and 100 out of a 200-point pool, which is what retail
shows. Font is `ColorSchemePanels[10]` (`WHITE`); background is `COLORS.DAT` id 19 (palette 16,
black), which is why the readouts sit on solid black.

### `.GAU` block at 616

A 16-byte header whose first two ints are an origin offset added to the rest (all-zero in every retail
file), then four ordinary `x0,y0,x1,y1` rects, all shifted by `VideoMode_X/YCoordShift` in
`FUN_00444b9c`:

| Offset | Rect |
|---|---|
| 632 | front facing's meter body |
| 648 | rear facing's meter body |
| 664 | front readout |
| 680 | rear readout |

The block ends at 696. `HShieldDisplay`/`GauFileTransformer` used to start it at 628, which rotated
every slot by one int; corrected 2026-08-17, and all nine retail `.GAU` files still round-trip
byte-exact.

## Weapon hardpoint rows

`WeaponGauge_Ctor` (`0044080c`) lazily loads `pweapons` and `wpn_dmg` and builds a two-sequence frame
table for the latter. `FUN_00440a68` wraps it with the row's two children: the select gadget
(`WeaponSelectGadget_Ctor`, `00442488`) and the value field (`FUN_00442950`, an `LEDBarGraph` over
`0x400` with colour ids `0x20`/`0x22` and remainder `0x2e`). The weapon's name arrives as a pointer
(`FUN_0040e18c`) and is `strncpy`'d 12 bytes into the gauge at `+0xb1`.

`WeaponSelectGadget_Ctor` lays out two sub-rects relative to the `.GAU` hardpoint rect, mirrored from
the right edge for a right-column slot:

| Sub-rect | Offsets from the rect, GAU |
|---|---|
| hardpoint state box | `x0+6 .. x0+9`, `y0 .. y0+7` |
| weapon-name label | `x0+11 .. x0+35`, `y0 .. y0+5` |

`WeaponSelectGadget_Paint` (`004426c0`) draws:

- the row plate, `PWEAPONS` frame 0 selected / frame 1 not, at the rect **minus one device pixel on
  both axes** — its 116x18 art overhangs the 110x12 rect evenly;
- the state box, `PWEAPONS` frames 4/5/6 by state (6x14): frame 4 green (index 14), 5 and 6 red/amber
  (108/109 and 102/104);
- the name, in `ColorSchemePanels[10]` `WHITE` when selected and `[11]` `GRAY` otherwise;
- the round count, in `[13]` `GREEN` or `[11]` `GRAY`.

The value field past the name is either that LED bar (energy weapons) or the round count (ballistic).

## Console buttons

Chain, link and auto-track are all 24x7 GAU in every retail file. `ConsoleButton_Paint`
(`00442c88`) blits `PWEAPONS` frame `2 + state` at the widget's own rect — frame 2 unlit, solid
palette index 34 (the retail blue, RGB `(77,77,182)`); frame 3 lit, index 14 green — then the caption
in `[10]` `WHITE` unlit / `[12]` `DARK` lit. The plates are **not** canopy art.

The chain button's caption is its count in Roman numerals from `ChainCountCaptions` (`0049c71c`):
`"I"`, `"II"`, `"III"` — a literal table in `.rdata`, unrelated to the string file. LINK and TRACK
are not fixed the same way: `ConsoleButton_Paint` reads them from `DAT_004d13d0`, the `.bss` array
`SimStrings_LoadAll` fills from `STRINGS0.STR` group 4 (see [`str-strings.md`](str-strings.md)),
indexed by the widget's own kind field — entry 1 for LINK, entry 2 for TRACK.

## Gunsight readouts

`Gau_RovingGunsightWidget` (`0043c7d8`) places the speed and mission-time readouts from two anchor
points in the gunsight complex's `.GAU` block, both already device-shifted:

- **1128/1132** is the *left* edge of the `SPEED:` caption. The value follows at
  `captionEnd + (2 << XCoordShift)`.
- **1120/1124** is the *right* edge of the time field. Its left edge is that minus the measured width
  of `"00000"` — a five-digit reservation — and the `TIME:` caption is right-aligned
  `(2 << XCoordShift)` before it.

Captions use `ColorSchemePanels[16]` (`HUD2`, ink 73) and values `[17]` (`HUD3`, ink 74). Those are
theater palette indices, not colours the widget picks — which is where retail's pale yellow-green
captions and cyan values come from.

## `.PDG` — paper-doll damage diagram

`PaperDoll_Load` (`004379cc`, `pdamage.cpp`) matches `PaperDollGraphic` field for field: 3 views, each
an origin/size pair plus a vector of `0x1c`-byte regions
(`{int index, PixelPoint topLeft, PixelPoint bottomRight, int colorId, int spacer}`).

Coordinates are authored in the 320-wide space and shifted by `VideoMode_X/YCoordShift`, with
`bottomRight` additionally `+1` in the 640-wide mode. Region art comes from `{herc}.HBA`/`.DBA`.

## HUD fonts

`ColorSchemePanels_LoadAll` (`00431098`) lazily loads 18 `.DFN`/`.HFN` fonts into
`ColorSchemePanels` (`0049b0ac`), then 7 `.DCI` cursors. Load order is the array index:

| 0-5 | 6-11 | 12-17 |
|---|---|---|
| `cpblue`, `cpgreen`, `cpred`, `cpylw`, `cpon`, `cppress` | `cpoff`, `cpgrey`, `cpblack`, `cporange`, `white`, `gray` | `dark`, `green`, `red`, `hud1`, `hud2`, `hud3` |

Each file is the same typeface stencilled in one palette index, so **a widget picks its text colour by
picking a font** — no colour is ever passed to a label. Consumers reach entries by absolute address:
`0049b0d4` = 10 `white`, `0049b0d8` = 11 `gray`, `0049b0dc` = 12 `dark`, `0049b0ec` = 16 `hud2`,
`0049b0f0` = 17 `hud3`.

Format, glyph layout and per-file ink indices: [`dfn-hfn-dci.md`](dfn-hfn-dci.md).

## Per-frame ordering

`maybe_Sim_RenderFrame` (`0045fb9c`): `Terrain_SetupVisibleRegion`, then `FUN_004327ac`
(`CockpitViewInstance` widget paint dispatch), then `maybe_Scene_SubmitFrameObjects` (the 3D world),
then `Player_PerFrameCockpitUpdate`, then three more paint dispatches on `CockpitViewInstance`
sub-objects (`+0x1f5`, `FUN_00433158`'s result, `+0x20b`).

## Open

- `WPN_DMG`'s fill levels and the weapon value field need per-weapon sim state the engine does not
  carry, so neither is drawn.
- Widget *state* sources generally: which frame or fill level a widget is in per frame is driven from
  the mech object, not from the `.GAU`.
- `static` and `pilot<n>` ship in `dba\` only, so the 640-wide mode has no matching art for them; see
  [`heads-down-display.md`](heads-down-display.md).
- RAZOR's non-stub view-1 3D viewport is not rendered.
