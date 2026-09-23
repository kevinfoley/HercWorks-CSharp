# Cockpit rendering: canopy art, views, clip regions, palette, HUD

Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project. All addresses are DBSIM unless noted. Symbols are in `tools/ghidra_scripts/known_symbols.json`; apply with `ES2ApplySymbolNames.java`.

Verified against retail data in `ES2/VOL/simvol0/{hb0,hb1,hb2,hba,hd0-3,ed0-3,vue,gau,dpl,dat}/`.

Engine implementation: `Herculan.Engine.Content.{CockpitArt, CockpitPalette, CockpitClipRegions, HudSpriteSheet, HudFont, HudColorTable, CockpitHudState, WeaponRowState}`, `Herculan.Engine.Render.Overlay2DRenderer`.

How a mouse click on any of these widgets reaches its own click handler: [`cockpit-input.md`](cockpit-input.md).

## Object model

| Symbol | Address | Role |
|---|---|---|
| `CockpitViewManagerInstance` | `004d2544` | Cockpit view state: current view, pending command, per-view assets. 0x37 bytes. Built in `Sim_InitMissionSession`. |
| `CockpitViewManager_Ctor` | `00429660` | Constructs the above; loads `dpl\cockpit` under a singleton guard. |
| `CockpitViewManager_LoadViews` | `00429834` | Whole cockpit bring-up (below). |
| `CockpitViewManagerPublished` | `004cfa20` | The same manager object again, stored at the tail of `CockpitViewManager_LoadViews` by `CockpitViewManager_Publish` (`00429810`) and read back by `CockpitViewManager_Published` (`00429820`). How a module that does not have the manager to hand reaches it — the message port and the joystick's `HDD VIEW` action both do. |
| `CockpitViewInstance` | `0049b088` | The GAU widget tree, owned by the manager. |
| `Gau_BuildCockpitWidgets` | `00431bf8` | Builds that tree from `gau\<HERC>.GAU`. |

Translation units: `MECHVIEW.CPP` (view manager, `00429660`–`0042ab00`), `PANEL.CPP` (widget tree, `00431008`–`00434400`), palette module (`00430346`–`00430e40`).

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
3. Per view `i`: `CockpitClipRegions_Load` on `ed<i>`/`hd<i>`, then `ClipRegions_BuildScanlineSpans`; and unless `CockpitArt_LoadOnDemand`, `CockpitCanopy_LoadViewBitmap` for `db<i>`/`hb<i>`.
4. Build `CockpitViewInstance` (`00431008` → `ColorSchemePanels_LoadAll`) and `Gau_BuildCockpitWidgets`.
5. Install the per-herc cockpit colour scheme (see Palette).
6. Install `IMPACTCP.DPL`'s same-index scheme into the secondary palette `DAT_0049aef8` for the damage flash.

## Views

Four views, indexed 0-3, plus 4 = external/no-cockpit.

| View | Canopy bitmap | Blit flags | 3D viewport | Canvas origin | Clip file |
|---|---|---|---|---|---|
| 0 forward | `DB0`/`HB0` | 0 | full | `(0,0)` | `ed0`/`hd0` |
| 1 heads-down | `DB1`/`HB1` | 0 | **empty** | `(0,237)` | `ed1`/`hd1` (stub) |
| 2 glance | `DB2`/`HB2` | 0 | narrower | `(+320,0)` | `ed2`/`hd2` |
| 3 glance, opposite | `DB2`/`HB2` | **2 = mirror X** | full | `(-320,0)` | `ed3`/`hd3` |
| 4 external | none | — | full | — | default block at `DAT_004cfb1c` |

Views 2 and 3 share one bitmap handle: `CockpitCanopy_LoadViewBitmap` maps view to file index as `view > 2 ? view - 1 : view`, and after loading file 2 stores the same handle in slot 3. View 3 is drawn horizontally mirrored. There is no separate mirrored asset.

`CockpitView_ProcessViewCommand` (`0042a4c4`) applies ∓`0x3600` (~76°) to the pilot view yaw when entering views 2/3 and undoes it on return to view 0.

### View switching

- `CockpitView_QueueViewCommand` (`0042a3f4`) latches a command at `+0x18`, gated on the current view.
- `CockpitView_ProcessViewCommand` (`0042a4c4`) executes it.
- `CockpitView_SetView` (`0042a1f0`) does the work: `CockpitView_ApplyViewState`, then one `Bitmap_Blit` of the canopy at `(0,0)`, then `FUN_004316c0` repaints every cockpit widget.
- `CockpitView_ApplyViewState` (`00429e60`) copies the view's `0x204`-byte clip block into the render context (`DAT_006c5ff4 + 4`), sets that context's clip mode (`+0x208`) to **2** — the region-list mode, which makes even sprite blits follow the cutout scanline by scanline — points `ActiveScanlineClipSpans` at its span table for the polygon rasterizers, and installs the `.VUE` rect into context slots `0x84`-`0x89`. Only the target box is drawn through this context; see [`hud-target-indicator.md`](hud-target-indicator.md).

**The canopy is blitted once per view change, not per frame.** The 3D scene is then rasterized over it every frame, span-clipped to `ActiveScanlineClipSpans`; HUD widgets repaint on top.

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

Two device paths reach those commands. `CockpitView_HandleEdgeTrigger` (`00433a88`) answers a **mouse click on one of three screen-edge strips** — `+0x21a` the bottom one, `+0x21e`/`+0x222` the left and right — picking the command by current view; they are ordinary widgets, and [`cockpit-input.md`](cockpit-input.md#10-the-screen-edges-are-three-widgets) has their rects and the full mapping. `CockpitView_PollViewDevice` (`00432b14`) reads four device-state bytes at `+0x1e`-`+0x21` for commands 1/0/5/4, the joystick hat's up/down/left/right. The manual binds `[F7]`/`[F8]` to heads-down, `[F9]`/`[F10]` to the left and right windows and `[Esc]` to the way back.

### Heads-down pan — `CockpitView_StepViewTransition` (`0042a9c0`)

Called once per frame from `Sim_EndFrame` (`0045fa98`), immediately before `CockpitView_ProcessViewCommand`. A view change spans three frames:

1. A key calls `CockpitView_QueueViewCommand`, latching `+0x18`.
2. `CockpitView_ProcessViewCommand` installs the destination view's clip block and canvas origin on the back page (via `CockpitView_ApplyViewState`, no blit) and sets the transition flag `+0x1c`.
3. `CockpitView_StepViewTransition` runs the whole slide, then `+0x14 += 1` (or `-1`), `+0x18 = -1`, and `+0x1d = 2` — a two-frame cooldown that `CockpitView_ProcessViewCommand` decrements and returns on before doing anything else.

The slide itself, for commands 0/1:

```
travel = vue[dest].canvasOriginY - vue[src].canvasOriginY     -- 237, or 474 in the 640x480 modes
for (i = 0; i < travel; i += 10)
    displayOriginY += 10
    SetDisplayOrigin(page, {x, displayOriginY})               -- DAT_004a5800
displayOriginY += travel - i                                  -- final remainder step
```

Step is 10 canvas rows; `maybe_CockpitLayoutMode == 2` doubles it and forces travel to `0x1e0`. The side-glance commands (4/5/6) use step `0x14` and scroll on x instead.

**There is no timing in this loop**. Its real-time duration is whatever the host CPU makes it, and only a step *count* is recoverable: 24 steps in mode 0, 48 in modes 1/2, since the step is in device rows and the coord shift doubles the travel.

**Both views' canopies are resident in the canvas throughout.** `Sim_InitMissionSession` (`004614fc`) calls `CockpitView_SetView(mgr, 1)` and then `CockpitView_SetView(mgr, 0)` during bring-up, so the pan is a pure scroll and never a redraw. That order also settles the six-row overlap where the two blits meet — `.HB1` lands at canvas row 474 and `.HB0` runs to 479, so **`.HB0` wins**.

Herculan: `Herculan.Engine.Render.CockpitPan`, `Content.CockpitViewGeometry`, `Render.Overlay2DRenderer.DrawHeadsDown`. The pan is pinned to a fixed 0.4 s (mode 0's 24 steps at 60 Hz), expressed as a duration so both asset sets pan at one speed, and interpolated continuously rather than in 10-row jumps.

## `.VUE` — per-view geometry

After the 9-byte VOL prefix: `int32 viewCount`, then `viewCount x` 8 `int32`s. All coordinates are authored in the 320-wide space and shifted by `VideoMode_X/YCoordShift`.

| Field | Meaning |
|---|---|
| 0-3 | 3D viewport rect `x0, y0, x1, y1` |
| 4-5 | Projection centre, `cx, cy` — **stored negated**, see below |
| 6-7 | Canvas origin `originX, originY` |

C# port: `HercWorks.Core.Data.File.Dbsim.Vue.Entry` (fields renamed to match the above; they were `WidthMax`/`UnkOfs*` pre-RE guesses). Engine wrapper: `Content.CockpitViewGeometry`.

The rect is the **outer bound** on where the 3D scene may reach, and the `.HD<n>` scanline spans below are the canopy-shaped hole inside it: two mechanisms over one view, both applied. `Content.CockpitViewGeometry.WorldViewport` reads it, and the host draws each panel's whole 3D pass — sky, world, beams, sprites — under a GL scissor set from it, before the canopy quad goes over the top with the spans already punched into its alpha. The two agree on retail data (`APOCA.HD0` resolves to rows 0-371 against a rect of `0,0 - 640,372`), so the scissor changes nothing that is visible on a herc whose canopy is opaque outside its rect — which is what makes the spans sufficient on their own and the rect easy to miss.

Each of the three panels the engine shows at once carries its own view's rect: the forward panel view 0, the unmirrored side panel view 2, and the mirrored side panel view 3, whose rect is reflected about the view width exactly as its art is. The two glances share a canopy bitmap but not a rect — view 3's runs the full width where view 2's stops short of it, on every retail herc — so pairing the mirrored panel with view 2's rect would clip a band off its outer edge that retail does not.

Every retail `.VUE` gives view 1 the canvas origin `(0,237)` — no herc differs.

`APOCA.VUE` (`viewCount = 4`):

| View | Rect | Centre | Canvas origin |
|---|---|---|---|
| 0 | `0,0 – 320,186` | `-160,-95` | `0,0` |
| 1 | `0,0 – 0,0` | `-160,-95` | `0,237` |
| 2 | `0,0 – 287,231` | `-160,-95` | `320,0` |
| 3 | `0,0 – 320,231` | `-160,-95` | `-320,0` |

View 1's zero-size rect is why the heads-down view shows no 3D. **RAZOR is the sole exception** — `0,0 – 320,181`, matching its 2368-byte `.HD1` against every other herc's 16-byte stub.

### The projection centre is not the middle of the view

Fields 4-5 are where the view axis lands on screen, and `FUN_0048c5c4` is the projection's last step: `screenX = x + centreX`, `screenY = centreY - y`. Anything running straight away from the eye — a beam leaves its muzzle parallel to the view axis — vanishes at that point, and it is where the gunsight reticle is drawn. **It is not the centre of the viewport rect, and not the centre of the view window.** APOCA's is 95 rows down a 240-row view, 45 above the window's middle.

The value reaches the projection through three steps, all of which cancel to a plain negation:

1. `CockpitView_ApplyViewState` (`00429e60`) copies the record's first six ints into the render context at `+0x210..+0x224`, then adds the view's canvas origin into the last pair.
2. `FUN_0048c1d8` computes `centre = viewportTopLeft - thatPair`.
3. Every retail viewport rect starts at `(0,0)`, and the side glances' canvas origins of ±320 cancel against their own window origins.

So the centre relative to a view's own window is `(-cx, -cy)` authored — `(160, 95)` for APOCA. Retail `cy` runs 95 (APOCA, RAPTOR2) to 146 (RAZOR); `cx` is 160 for every herc and every view. All = four views of a herc carry the same pair.

`FUN_0048c1d8` also installs, from the same view struct: `+0x1a` the perspective shift (`(width << shift) / z` is the whole of the divide), `+0x1e` the near plane, `+0x22` the orthographic divisor. `2^shift` is the focal length in pixels, which fixes the field of view against the view's row count. `Sim_InitMissionSession` (`004614fc`) picks the shift as 9 when the mode's canvas width (`DAT_004d30c4`) reaches 1201 and 8 otherwise, and passes it as the third argument of `View_Ctor` (`0048bc98`), which stores it at `+0x1a`. The constructor's other fields: render target `+0x16`, near plane `+0x1e`, and through `View_CtorBase` (`0048bb64`) the position `int[3]` at `+4` and three `short` angles at `+0x10`. Both work out to the same angle — 256 px across a 240-row view, 512 across a 480-row one, 50.2 degrees vertical. Engine: `Render.Camera.FocalLengthPixels`.

Engine: `Content.CockpitViewGeometry.ProjectionCenter`, applied via `Render.Camera.PrincipalPoint` as an off-centre frustum.

### Cockpit canvas

`CockpitCanvasWidth`/`Height` (`004d25d2`/`004d25d6`) are 320x480 in mode 0 and 640x960 in modes 1/2 — taller and wider than the 3D viewport (`004d25c2`/`004d25c6` = 320x240 / 640x480). The canvas is a virtual space the views window into at their `.VUE` origins: rows 0-239 the forward cockpit, rows 237-476 the heads-down display, x ±320 the side views.

**No retail `.GAU` uses more than the forward quadrant.** Widget origins across all nine hercs span `x:[3..298] y:[1..230]`, so the declared `HudScreenSize` of (320,400) overstates the used range and the side views have no widgets of their own.

## `.HD0`-`.HD3` / `.ED0`-`.ED3` — 3D-viewport clip regions

`CockpitClipRegions_Load` (`0042dcf0`). Layout after the 9-byte VOL prefix, all fields little-endian `int16`:

```
int16 rectCount
rectCount x { int16 y0, int16 y1, int16 x0, int16 x1 }      -- inclusive on all four edges
int16 blockCount
blockCount x {
    int16 firstRow, int16 rowCount,
    rowCount x { int16 xStart, int16 xEnd }                 -- one entry per scanline, inclusive
}
```

Coordinates are shifted by the caller's `(xShift, yShift)`: `(0,0)` for the `.HD*` set (already 640-wide), `VideoMode_X/YCoordShift` for `.ED*`. Inclusive ends are expanded as `end = (end << shift) + (1 << shift) - 1`. Output is a `0x204`-byte block: `int count` plus up to 128 region pointers, rects tagged type 0 and span blocks type 2.

`ClipRegions_BuildScanlineSpans` (`0048b9a8`) flattens that into a table of `0xf0 << VideoMode_YCoordShift` rows (240 or 480), each `{ int spanCount, ptr to spanCount x {int start, int length} }`, sorted by start, and stores it in `ActiveScanlineClipSpans` (`004a5b10`). The polygon rasterizer (`00468310`) indexes it by row and skips rows with zero spans.

**This is the viewport cutout mechanism.** DBSIM never colour-keys the canopy art, and palette index 0 has no special meaning in it.

Blocks may overlap and repeat — `APOCA.HD0` lists `row 204 +168` twice — which is harmless because flattening accumulates every region per row.

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

Every file consumes its whole body under this layout with 3 constant trailing bytes unread. RAZOR is the only herc with a non-stub view-1 file, matching the file sizes on disk (`APOCA.HD1` is 16 bytes, both counts zero; `RAZOR.HD1` is 2368). `APOCA.HD0` resolves to rows 0-371, matching the independently measured index-0 bounding box on `APOCA.HB0` (`y:[0..371]`).

Every rect in every retail file has `x0 == 0`. This matters because DBSIM's flattening step feeds a rect's fourth field to the rasterizer as a span *length* (`piVar4[1] = piVar1[3]`, against `end - start + 1` for span blocks) while the loader's own shift arithmetic treats it as an inclusive end. With `x0 == 0` the two readings differ by one column at the right edge and nothing else; `CockpitClipRegions` takes the inclusive reading.

## Canopy art — `.HB0`/`.HB1`/`.HB2` and `.DB0`/`.DB1`/`.DB2`

`CockpitCanopy_LoadViewBitmap` (`00429c2c`, `MECHVIEW.CPP:0x12e`).

No literal `"hb0"`/`"db0"` string exists anywhere in `DBSIM.EXE`. The folder name is built at runtime: the global folder literal `"dba"` (or `"hba"` when `VideoMode_UseHiResPanels == 3`) is copied to a stack buffer and index 2 overwritten with an ASCII digit via `_itoa`, giving `db0`/`db1`/`db2` or `hb0`/`hb1`/`hb2`. Then `ResourcePath_BuildFolderName(hercName, buf)` → `ClassItem_LoadResource`. The same trick produces `ed<i>`/`hd<i>` from `"edg"`/`"hdg"`.

Files are `DynamixBitmapArray`s with one frame: `.DB*` 320x240 (76844 bytes), `.HB*` 640x480 (307244).

`CockpitCanopy_FreeViewBitmap` (`00429de4`) releases one view's handle, also nulling slot 3 when freeing view 2. Used only when `CockpitArt_LoadOnDemand` (`004d2704`) is set — a low-memory mode that loads and frees per view switch rather than keeping all four resident.

### Known defect in the retail code

The `maybe_CockpitLayoutMode == 1` branch increments byte 2 of the **shared global** `"dba"` literal (`MOV ECX,[0x4a0a28]; INC byte ptr [ECX+2]` at `00429d3e`) rather than its local buffer. The follow-on load still uses the unmodified local buffer, so that branch loads `db0` twice and corrupts the global folder name for every later user. Nothing in the image writes `004d25bc`, so the path is unreachable.

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

The flash is a whole-palette swap rather than a fade: `Palette_ActivateImpact` (`0042ea44`) makes the secondary palette `ImpactPaletteObject` (`0049aef8`) active and saves the previous one, `Palette_ToggleImpact` (`0042ea70`) alternates the two, and `Palette_RestoreFromImpact` (`0042ea94`) puts the original back. That object is loaded twice over: `World_LoadTheater` (`0042e010`) builds it from the theater's own `IMPACT<n>.DPL` — `wld\WORLD<n>.WLD`'s third trailing string — and step 6 of the load sequence above then overwrites its 24 cockpit indices from `IMPACTCP.DPL`. So a flash recolours the world and the cockpit together, each from its own source.

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

## Video modes

`VideoMode_Configure` (`0045e4f4`) sets the whole block from a mode argument.

| Mode | `VideoMode_UseHiResPanels` (`004d25bb`) | `VideoMode_UseHiResBanks` (`004d25f0`) | Viewport | Canvas | Coord shifts |
|---|---|---|---|---|---|
| 0 | 0 | 0 | 320x240 | 320x480 | 0 |
| 1 | 3 | 0 | 640x480 | 640x960 | 1 |
| 2 and up | 3 | 1 | 640x480 | 640x960 | 1 |

**The argument is the player's only on the command line.** The first call — `WinMain`'s, passing 0 — discards what it was given and reads `data\prefs.cfg` instead, taking option 4 and mapping it to **0 for a stored 1 and 3 for anything else**, so the file reaches mode 0 or the last row and never the middle one. That first call also latches a once-only gate, so the later `-v<n>` call keeps its own argument, and `-v1` is the only way to the low-res banks at 640x480. See [`../simulation/preferences.md`](../simulation/preferences.md#the-video-mode-and-full-screen-bytes).

`UseHiResPanels == 3` selects `.HFN` fonts, `hba\` sprite banks, `hb<n>` canopy art and `hd<n>` clip files. `UseHiResBanks` separately selects hi-res banks for `hudhtick`, `mfd`, `radar`, `hdd`, `pweapons`, `wpn_dmg`, `weapons`, `pdg`, `bases`, `vehicles`, `flyers` and the alert banks — which is why two different flag idioms appear at the bank load sites.

**`maybe_CockpitLayoutMode` (`004d25bc`) cannot be written.** It is BSS, so zero from load. All 23 occurrences of the dword in the file are the `MOVSX` byte reads themselves, and no address in the surrounding block `004d2580`-`004d2602` is ever address-taken — for every one of them the raw dword count equals the count of absolute `[mem]` operands — so no register can hold a pointer into the block and no base-plus-displacement store, `memset`, `memcpy` or `fread` can reach it either. The control for that method is `004d25bb` (`VideoMode_UseHiResPanels`), one byte away in the same block, which `VideoMode_Configure` does write and the method does find. Both tested values are therefore unreachable: value 1 is the defective path described above, and value 2 would route blits through `Bitmap_BlitClipped` and put the view origin in `DAT_004d25da`/`de` rather than `DAT_004cfa24`/`28`. **So `DAT_004d25da`/`de` are never written**, and the offset `Widget_OnMouseDown` and `Widget_OnMouseUp` add from them ([`cockpit-input.md`](cockpit-input.md#10-the-screen-edges-are-three-widgets)) is always zero.

## HUD sprite art — `.HBA`/`.DBA`

Every bank ships twice under the same name: `dba\NAME.DBA` for the 320-wide mode and `hba\NAME.HBA` for the 640-wide one, exactly 2x on both axes, frame for frame, with identical frame counts. The two folder-name literals sit adjacent to each bank name in `.rdata` (`"NAME\0hba\0dba\0"`). `corners` is hardcoded to `dba`; `hba\CORNERS.HBA` does not exist.

Load path: `ResourcePath_BuildFolderName(name, folder)` → `Resource_Load` (`0045cdd8`) → `ClassItem_LoadResource`.

| Bank | Owning function | Role |
|---|---|---|
| `hud` | `Gau_RovingGunsightWidget` (`0043c7d8`) | gunsight / reticle |
| `hudhtick` | `HudHeadingTape_Ctor` (`0043b57c`) | heading tick tape |
| `mfd`, `mfd_dmg`, `radar` | `MfdDisplay_Ctor` (`00445218`) | multi-function display — see [`mfd.md`](mfd.md) |
| `hdd`, `static`, `hddclip`, `pilotN` | `HddDisplay_Ctor` (`00448cc8`), `HddGauge_LoadPilotFrames` (`0044a7c0`) | heads-down display — see [`heads-down-display.md`](heads-down-display.md) |
| `pweapons`, `wpn_dmg` | `WeaponGauge_Ctor` (`0044080c`) | weapon hardpoint plates |
| `throttle` | `ThrottleGauge_Ctor` (`00447b84`) | throttle slider knob |
| `sysbuttn`, `icons`, `corners` | `SystemButtons_Ctor` (`00434368`), `HddMarker_Ctor` (`0044f130`), `maybe_CockpitFontsAndCorners_Init` (`004544a4`) | |

Those widget class names are not loose strings: each is the name field of a Borland class descriptor record, which also carries the object size, the base class and the vtable the class installs. `tools/scripts/es2_classes.py` dumps all 221 of them, and [`cockpit-input.md`](cockpit-input.md#the-second-vtable-and-the-class-record-beside-it) has the record layout and the clickable-widget hierarchy.

Frame-to-state mapping, as far as it is traced: `PWEAPONS` 0/1 are the selected/unselected row plate, 2/3 the unlit/lit console-button plate, 4/5/6 the hardpoint state box (green / red / amber), 7 a 640x80 strip with no located consumer; `WPN_DMG`'s 10 frames are damage fill levels, frame 0 the opaque empty plate; `THROTTLE` 0 is a 2x12 tick and 1 the 28x12 knob; `RADAR`'s 10 110x110 frames are the sweep animation; `MFD` 0-2 are 196x122 screen chrome, 3-10 five button plates in unlit/lit pairs (see [`mfd.md`](mfd.md)); `HUD` 0 is the 45x45 reticle, 11 the 182x10 rotation-indicator track and 12/13 its 62x4 yellow and green bars (sizes in the 640-wide `hba\` banks; `dba\` is exactly half).

## `.GAU` widget tree

`Gau_Load` (`00431778`, `PANEL.CPP:0x1d6`) reads a `0x6a4`-byte struct and constructs six sub-widget vectors. The file's first two `int32`s are an origin offset added to every widget rect. `Gau_BuildCockpitWidgets` (`00431bf8`) then builds seven top-level widgets from fixed offsets and shifts every rect by `VideoMode_X/YCoordShift`. The order it builds them in is also the cockpit's click precedence — [`cockpit-input.md`](cockpit-input.md#registration-order-is-precedence) has the full sequence.

GAU coordinates are authored in the 320-wide space; the engine's `CockpitArt.GauToPixelScale = 2` maps them onto 640-wide art. See "Cockpit canvas" above for the y-range question.

## `dat\COLORS.DAT` — logical colour ids

54-byte payload, 27 `int16` palette indices. HUD data files carry a small logical id, resolved once at load time through this table in place (`arr[i] = table[arr[i]]`). The table lives at `HudColorTable` (`004d3c00`) in `.bss`, read at 16 distinct offsets by ~60 functions; no code materialises that address to write it, so it is filled from the file.

Verified: the heads-down display resolves ids 19, 9, 15, 12 → palette 16, 10, 13, 14 — black, red, yellow, green, matching the retail HDD readouts.

**Not every colour number is an id.** The indirection exists for numbers that arrive in a *data file*; a colour a *constructor states as an immediate* is already a palette index and goes nowhere near this table. The weapon panel's raw 32/34/46 (`FUN_00442950`) are the clearest case, and the scanner screen uses both conventions at once: its contact colours are read out of the table at paint time while its screen background is the literal `0x11` its constructor writes — palette 17, matching the dish art's own corner pixels. Reading such an immediate as an id lands somewhere plausible but wrong (`0x11` as an id is palette 24, a mid grey).

Consumers: `PaperDollGraphic.ViewRegion` at record offset `0x14`; `FUN_0045079c` (4-entry id array at `DAT_0049d9ec`); `HudColorTable_Get` (`00434280`).

## LED gauges

`LEDBarGraphH`/`LEDBarGraphV`. `LedBarGraph_Ctor` (`004395c4`) installs vtable `PTR_FUN_0049bd30` over `LedBarGraph_CtorBase` (`004390c4`), which precomputes:

```
span    = (end - start) * 0x10000 / range     // range is the caller's value scale
current = span < 1 ? end : start              // sign of span selects fill direction
```

`LedBarGraph_PaintToValue` (`004395e8`) fills to `start + (value * span >> 16)` (16.16 fixed point), then installs field `0x24` as the draw colour and covers the remainder.

The filled span is not solid. `LedBarGraph_FillPinstripe` (`00439758`) walks the x range twice — once over even columns, once over odd — drawing a full-height line each step: field `0x2c` paints even columns, `0x30` odd. Two near-identical shades interleaved at one pixel read as a single shaded fill.

Both class variants fill along **x**: `LedBarGraph_CtorBase` takes start/end from the rect's `x0`/`x1` (`param_2[0]`/`param_2[2]`), and the pinstripe walk strides columns.

`EnergyPoolGauge_Ctor` (`00444d5c`) constructs one over the `.GAU` widget rect at 564 with range `0x400`, writing colour ids 6 and 5 into `0x2c`/`0x30` and id 19 into `0x24`. Those resolve to palette indices 98/97/16 = `(0,116,204)`, `(0,40,160)`, `(0,0,0)` — the blue pinstripe bar retail draws directly under the TRACK button, i.e. the **Master Energy Pool meter**. It is fed `(pool << 10) / 10000` by `Player_PerFrameCockpitUpdate` — see [../simulation/reactor-energy-pool.md](../simulation/reactor-energy-pool.md). Its only caller is `Gau_EnergyMeterWidget`, and the binary's own class-name table pairs `EnergyPoolGauge` with `LEDBarGraphV` (file offset 280429) and `ShieldsGauge` with `ShieldsSelectGadget` (279148) — the LED bar is the energy meter, and `ShieldsGauge` is a different class entirely.

A second `LEDBarGraph` per weapon row carries the energy-weapon charge field (`FUN_00442950`, range `0x400`) — but with raw palette indices `0x20`/`0x22` and remainder `0x2e`, not `COLORS.DAT` ids. See [Weapon hardpoint rows](#weapon-hardpoint-rows).

## Throttle gauge

`ThrottleGauge_Ctor` (`00447b84`), called only by `Gau_ThrottleWidget` (`0043254c`).

The constructor is handed `.GAU` offset **1000**, not 1016, and treats the whole block from there as one widget record. `FUN_004488cc` shifts ints `[4..0xf]` left by the video mode's coordinate shift before it ever sees them, so the geometry below is in device pixels (`.GAU` units x2 at 640x480):

| int | file offset | Role |
|---|---|---|
| `[0]`,`[1]` | 1000, 1004 | Origin the rest is measured from. Zero in all 9 retail files, which is why it reads as an always-zero "null widget" slot until the constructor is traced |
| `[4..7]` | 1016-1028 | Slider **track** rect, `x0,y0,x1,y1` |
| `[8..11]` | 1032-1044 | **Forward fill bar** rect — `LedBarGraph_CtorV` (`00439344`) with range `+0x400` |
| `[12..15]` | 1048-1060 | **Reverse fill bar** rect — same, range `-0x400` |

| `[0x10]` | 1064 | `SLIDE_DIR`. 1 in every retail file, selecting `ThrottleSlider_CtorV` (`00447e24`); the 0 branch (`004483c0`, a fixed 12px knob spanning the track's full height) is never exercised |
| `[0x12]` | 1072 | x nudge for the centre tick, shifted by the ctor itself rather than at load |

Ints `[8..15]` are **two rects, not four points**. That explains both things the point reading found odd: "points" 1 and 2 always sit close together because they are the bottom of the upper bar and the top of the lower one, and the x alternates between two values because those are each bar's left and right edge. On OUTLAW they are two 4x20 strips inside the 14x49 track, one either side of centre.

**Neither bar is ever drawn.** `ThrottleSlider_CtorV` keeps them as private fields (`+0x7e`, `+0x82`) and never registers them with the widget tree, so nothing dispatches their paint; `LedBarGraph`'s own draw routines (`00439398`, `00439460`) have no callers anywhere in the image. The slider's paint (`ThrottleSlider_PaintV`, `0044819c`) reads them only through `FUN_004390b8`, which returns the object's rect, and unions those rects into the region it invalidates. The bars are a cut feature whose construction was left in — see the speed fraction below, which is what would have filled them.

### Slider geometry

`ThrottleSlider_CtorV` builds the knob over the track: full track width, height taken from bank frame 1 (28x12 in every retail bank), limits `+/-0x400`, and an initial position centred on the track — `knobBottom = trackBottom - (trackHeight - knobHeight)/2`. That is the manual's "Centered is stopped". `SliderWidget_RecomputeScaleV` (`00452694`) then precomputes

```
scale     = (trackHeight - knobHeight) * 0x10000 / 0x800     // Q16 device px per throttle unit
knobBottom(v) = trackBottom - ((v + 0x400) * scale >> 16)     // 00452644
```

so `+0x400` puts the knob at the top and `-0x400` at the bottom. **Up is forward** — corroborated by `Reference/Simulator1.jpg`, where the knob sits at the track's top while the HUD reads 61 K/H.

The original reaches that convention through two sign flips that cancel: `SliderWidget_GetValueV` (`00452628`) reads the knob's *top* against the track's top, so it returns the negation of what `_SetValueV` was given, and `ThrottleGauge_OnChildValue` (`00447de0`) negates again for the vertical variant (gated on the gauge's `+0xc1 == 0`). Net effect, and what `ThrottleTrack` exposes: positive is forward, linear in knob position.

### Sprites

Bank `throttle` (`.HBA`/`.DBA`), 2 frames: **0** is a 2x12 tick, **1** the 28x12 knob.

The gauge captures the tick's blit position **once**, in the constructor, at the knob's neutral height plus `[0x12]`, and nothing writes it again — so it is a static centre-detent marker beside the track. Matches `Simulator1.jpg`.

### Live values and the two-way binding

`ThrottleGauge_GetValues` (`00447dd0`) returns gauge `+0xb1`, a pair of ints:

| offset | Value |
|---|---|
| `+0xb1` | Speed as a Q10 fraction of max, `(mech+0x28e << 10) / (speed < 0 ? -maxRev : maxFwd)` |
| `+0xb5` | Throttle setting, Q10 `+/-0x400` — the same number as `mech+0x290` |

`Player_PerFrameCockpitUpdate` (`0041b130`) writes both once a frame via `ThrottleGauge_SetValues` (`00447d80`), and arbitrates the throttle against the `mech+0x93` dirty flag: flag clear, the gauge drives `mech+0x290`; flag set, the machine's throttle is handed back for the gauge to follow and the flag is cleared. Whichever moved last wins, which is what makes the slider track the keyboard and the keyboard pick up where a drag left off.

**`+0xb1` drives nothing.** `ThrottleGauge_SetValues` marks the slider child dirty when it changes, and `ThrottleSlider_PaintV` copies it into `+0x7a` and `+0x4a` and does nothing further with it — so its only observable effect is to force a repaint whenever the machine's speed changes. It is the other half of the cut feature the two fill bars are: the knob shows the throttle asked for, the bars would have shown the speed actually reached.

The slider is the **only draggable widget in a retail cockpit** — see cockpit-input.md §7. `ThrottleSlider_OnValue` (`00448378`) also sets `ThrottleLeverMode` (`0049a06e`) from the committed value's sign, but gated on a joystick throttle control being configured. See mech-locomotion.md for what that global actually is.

## `ShieldsGauge`

`ShieldsGauge_Ctor` (`004434fc`), called only by `Gau_ShieldDisplayWidget` (`00432454`) with `.GAU` offset 616. It loads no sprite bank and **draws no geometry**: it builds a `0x40`-byte child per facing (`ShieldsGauge_FacingCtor`, `00444aec`) whose paint slot (`FUN_00444b5c`) only tests visibility, plus two text labels.

**The meter is lit, not drawn.** The nested concentric rings are painted into the herc's own canopy art in palette indices 66-71 — verified on `OUTLAW.HB0`, where those six indices appear only inside the meter bezel, three per facing, the innermost ring using the fewest pixels. The gauge's paint (`00443730`/`00443748`) does two things per frame: rewrite those six palette slots (`ShieldsGauge_UpdateRingPalette`) and refill the two readouts (`ShieldsGauge_UpdateReadouts`).

### Ring ramp — `ShieldsGauge_UpdateRingPalette` (`004438f0`)

Per facing (charge at object `+0xb5` and `+0xb9`), three rings light in turn as charge rises:

```
ring 1: t = v
ring 2: t = v < 0x100 ? 0 : min((v - 0x100) * 2, 0x400)
ring 3: t = v < 0x180 ? 0 : min((v - 0x180) * 4, 0x400)
colour = base * t >> 10                                  -- Q10
```

Above `0x400` (an overcharged shield) the same three tracks run again over `base + (bright - base) * t`, with `v` taken as `charge - 0x400`.

Colour immediates at `ShieldRingColors` (`0049c9cb`): base RGB6 `(25,59,23)`, bright `(59,59,23)`. `Palette_InstallRange(0x42, 6, ...)` reads the six entries up the stack, so **66-68 are the first facing outermost-first and 69-71 the second**.

A facing runs 0..`0x800` with `0x400` the whole pool on one side, so an even 100/100 split parks both at `0x200`, where all six rings resolve to RGB `(48,116,44)`. The retail screenshot's meter is `(48,117,44)` — the one-channel difference is the palette scalar's own rounding.

### Readouts — `ShieldsGauge_UpdateReadouts` (`00444a68`)

`itoa(balance * 200 >> 10)` into the first label and its complement into the second, from the fore/aft balance at `+0xbd`. An even split reads 100 and 100 out of a 200-point pool, which is what retail shows. Font is `ColorSchemePanels[10]` (`WHITE`); background is `COLORS.DAT` id 19 (palette 16, black).

### `.GAU` block at 616

A 16-byte header whose first two ints are an origin offset added to the rest (all-zero in every retail file), then four ordinary `x0,y0,x1,y1` rects, all shifted by `VideoMode_X/YCoordShift` in `FUN_00444b9c`:

| Offset | Rect |
|---|---|
| 632 | front facing's meter body |
| 648 | rear facing's meter body |
| 664 | front readout |
| 680 | rear readout |

The block ends at 696. It starts at 616, not 628 — starting it one int later rotates every slot and leaves a spurious leftover int at 692. All nine retail `.GAU` files round-trip byte-exact under this reading.

## Weapon hardpoint rows

**Which mount owns which row is the mount's business, not the panel's**: the gauge factory is called with the `gl\<HERC>.GL` record's fire-chain byte as its `.GAU` weapon-slot index, so row order and mount order are different orderings. See [`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md).

Three gauge classes, one per mount class, all built on `WeaponGauge_Ctor` (`0044080c`), which lazily loads `pweapons` and `wpn_dmg` and builds a two-sequence frame table for the latter:

| Class | Factory → ctor | Value field |
|---|---|---|
| energy | `FUN_00432074` → `FUN_00440a68` | `LEDBarGraph` (`FUN_00442950`) |
| ammunition | `FUN_00432124` → `FUN_00440f78` | round count, `itoa` (`FUN_004411b4`) |
| pod | `CockpitView_CreatePodGauge` → one of three `PodGauge` classes | none — the name label widens over both fields — except the Turbo Pod's |

All three `strncpy` 12 bytes of the mount's name (`FUN_0040e18c`) into the gauge at `+0xb1`. The pod class instead seeds an 11-char buffer with a space, appends the name, then appends `STRINGS0.STR` group 3 (`" POD"`) into the room left — `" SHIELD POD"`. A destroyed mount's row prints group 2 (`"OFFLINE"`) in place of the name.

**The Turbo Pod's row is the exception.** `TurboPodGauge_Ctor` (`00441a34`) overwrites that buffer with a plain 11-char `strncpy` of the name — so the row reads `TURBO`, not `" TURBO POD"` — rebuilds the name label at `x0+6 .. x0+34`, `y0+1 .. y0+5` and gives the freed right-hand end an `LedBarGraph` over `pod+0x7d`, its charge. The bar's range is 2500 where `TurboPod_ChargeTick` caps the charge at 2000, so a fully charged Turbo Pod shows four fifths of a bar. Which pod gets which gauge class, and why only two of the five have a button at all, is in [`../simulation/equipment-pods.md`](../simulation/equipment-pods.md#only-two-pods-have-a-button).

Sub-rects, all relative to the `.GAU` hardpoint rect and mirrored from its right edge when the constructor's slot-mask byte is set (that byte lands in the `.GAU`'s confirmed-zero padding in every retail file, so retail never mirrors):

| Sub-rect | Offsets from the rect, GAU | Built by |
|---|---|---|
| hardpoint state box | `x0+6 .. x0+9`, `y0 .. y0+7` | `ChainedWeaponSelectGadget_Ctor` (`00442488`) |
| weapon-name label | `x0+11 .. x0+35`, `y0 .. y0+5` | `ChainedWeaponSelectGadget_Ctor` |
| value field | `x0+36 .. x0+53`, `y0 .. y0+5` | `FUN_00440a68` / `FUN_00440f78` |
| pod name label | `x0+11 .. x0+53`, `y0 .. y0+5` | `PodGauge_Ctor` (`00441524`) |
| Turbo Pod name label | `x0+6 .. x0+34`, `y0+1 .. y0+5` | `TurboPodGauge_Ctor` (`00441a34`) |

The two pod labels are the only sub-rects that are ever painted rather than merely written in: a pod row with its button on floods its label with `COLORS.DAT` id 12 and prints the name over it in the `dark` font ([`../simulation/equipment-pods.md`](../simulation/equipment-pods.md#only-two-pods-have-a-button)). Both edges are inclusive, so the Turbo Pod's plate is 57x9 device pixels against a plain pod's 85x11. The label's *text* does not follow its rect — every row on the panel prints its name at the same `x0+11`, the Turbo Pod's included, which is why that plate has green to the left of the `T`.

`FUN_00442950` then drops the bar's own top edge one GAU unit below the value field's, and builds it over `0x400` with colour **palette indices** `0x20`/`0x22` and remainder `0x2e` written straight into the bar object — not `COLORS.DAT` ids, which is why a capacitor bar is blue where the energy meter is grey.

`WeaponSelectGadget_Paint` (`004426c0`) draws:

- `WPN_DMG` frame 0 as the row underlay, then the slot number (`FUN_00442394`);
- the name, in `ColorSchemePanels[10]` `WHITE` when selected and `[11]` `GRAY` otherwise;
- the slot number again, recoloured `[13]` `GREEN` when selected / `[11]` `GRAY`;
- the state box, `PWEAPONS` 6x14 frames — **only when the mount is armed or in the current fire group**, otherwise the box area is filled with the row background. Frame 4 (green, index 14) when the mount is ready, frame 5 (red) when it is not — including when the selected target is outside the weapon's range, which is also what makes the firing chain skip it ([`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md#readiness--weaponmounts_mountisready-00410970)). A pod is in no fire group, so a pod row never has one;
- last, the row plate: `PWEAPONS` frame 0 selected / frame 1 not, at the rect **minus two device pixels on both axes**. The 116x18 art is not a plate but a frame — a 112x14 hole of palette index 0 is punched out of it, so what fills a row is the console bitmap showing through and all the sprite contributes is a two-pixel bezel. That offset lands the hole's top-left corner exactly on the rect, which is what puts the 14-pixel state box and an engaged pod's plate inside it.

The three state flags come from `WeaponMounts_PerFrameUpdate` (`00410b40`), the mount manager's per-frame pass.

## Console buttons

Chain, link and auto-track are all 24x7 GAU in every retail file. `ConsoleButton_Paint` (`00442c88`) blits `PWEAPONS` frame `2 + state` at the widget's own rect — frame 2 unlit, solid palette index 34 (the retail blue, RGB `(77,77,182)`); frame 3 lit, index 14 green — then the caption in `[10]` `WHITE` unlit / `[12]` `DARK` lit. The plates are **not** canopy art.

The chain button's caption is its count in Roman numerals from `ChainCountCaptions` (`0049c71c`): `"I"`, `"II"`, `"III"` — a literal table in `.rdata`. LINK and TRACK are not fixed the same way: `ConsoleButton_Paint` reads them from `DAT_004d13d0`, the `.bss` array `SimStrings_LoadAll` fills from `STRINGS0.STR` group 4 (see [`str-strings.md`](str-strings.md)), indexed by the widget's own kind field — entry 1 for LINK, entry 2 for TRACK.

## Front-window HUD — the gunsight complex

Everything drawn over the live 3D view, rather than on the console, belongs to one widget: `Gau_RovingGunsightWidget` (`0043c7d8`), built from `.GAU` offset **1088**. Its own ints:

| int | file offset | Role |
|---|---|---|
| `[0]`,`[1]` | 1088, 1092 | Origin added to every child rect. Zero in all 9 retail files |
| `[2]`,`[3]` | 1096, 1100 | The complex's own bottom-right — `320, 117` or `320, 157` |
| `[4..7]` | 1104-1116 | **Heading tape** rect. `100,y - 220,y+17` in every file, so 120x17 centred on the 320-wide HUD. The rotation indicator is derived from it, below |
| `[8]`,`[0xa]`,`[0xb]` | 1120, 1128, 1132 | Speed and time readout anchors — see below |
| `[0xc]`,`[0xd]` | 1136, 1140 | Reticle point |
| `[0xe]` | 1144 | Half-extent of child 4's rect about the reticle point. Zero in all 9 retail files, and unread by that child's paint |
| `[0xf..0x12]` | 1148-1163 | Rect shared by children 0, 5 and 6 — `GAUFile.GunsightArea`, the target arrow's safe area |
| `[0x13..0x16]` | 1164-1179 | The **`ATT` legend's** rect — see below |
| `[0x17..0x1a]` | 1180-1195 | A second label of the same kind, at the widget's `+0x107`. Neither gunsight paint reaches it |
| `[0x1b]`,`[0x1c]` | 1196, 1200 | Top-left of the floating scanner repeater — `GAUFile.HudScanner`, a bare point with no size. Per herc; see [`mfd-scanner.md`](mfd-scanner.md) |

The complex also builds two `ColorSchemePanels[12]` (`dark`) labels of its own, at `+0x103` and `+0x107`. The first is the manual's **`ATT` legend**: while the weapon manager's auto-track flag (`manager+0xb3`) is set, both paints blit `HUD` bank frame 14 as its plate and set its text to `STRINGS0.STR` group 37 entry 0 — see [`../simulation/torso-aim.md`](../simulation/torso-aim.md) for the tracker itself.

`Gunsight_AddChild` (`0043d5a4`) appends to a pointer array at the widget's `+0xd7`, so construction order *is* child index. `Gunsight_Paint` (`0043d5c8`) walks that array calling each child's slot 0, then draws two things that are not children at all: the **floating scanner repeater** (`FUN_0043e0ec` into `FUN_0043f2b0`) and `FUN_0043dd70`, which works from a second derived point at the widget's `+0x113` — the reticle plus `(0x46, -0x12)` device — and is not traced.

All nine children derive from `FUN_0043b344`, a bare rect holder. Children 4, 5 and 6 additionally receive the 38-byte state block described in [`hud-target-indicator.md`](hud-target-indicator.md), at `+0x14`.

| # | Ctor | What it is |
|---|---|---|
| 0 | `FUN_0043c120` | The clickable gunsight surface. Registers a child gadget with the cockpit's click list — the "gunsight click" entry into `TargetSelect_SetObject`. Its paint (`FUN_0043c1dc`) only tracks the cursor against its rect |
| 1 | `HudHeadingTape_Ctor` (`0043b57c`) | Heading tick tape, bank `hudhtick`, limits ±`0xe38` |
| 2 | `HudRotationIndicator_Ctor` (`0043b438`) | The rotation indicator, limits ±`0x38e3` |
| 3 | `HudSlideBar_CtorHidden` (`0043b54c`) | The pitch axis's slide bar — **paint slot is a no-op** (`0043b574`), so it is never drawn |
| 4 | `FUN_0043b344`, vtable `0049c124` inline | The reticle |
| 5 | `FUN_0043b928` (vtable `0049c1c4`) | The target box and its off-screen arrow |
| 6 | `FUN_0043c240` | Constructed and fed the state block, but its **paint slot is `ret`** (`FUN_0043c260`) |
| 7, 8 | `HudWaypointIndicator_Ctor` (`0043c268`) | The two waypoint indicators, below. 7 takes the `+0x45` flag that makes it the nav marker's |

Children 4 and 5: [`hud-target-indicator.md`](hud-target-indicator.md).

### Live values

`Player_PerFrameCockpitUpdate` (`0041b130`) calls `Gunsight_SetValues` (`0043d98c`) once a frame with three shorts — `mech+0x10` (heading), `mech+0x298` (twist angle), `mech+0x29a` (pitch angle). The widget caches them at `+0xb1`/`+0xb3`/`+0xb5` and forwards each one's **delta** to a child's `AddDelta` slot (`+0xc`):

| value | child | delta sent |
|---|---|---|
| heading | 7 | `new - old` |
| twist | 2 | `old - new` |
| pitch | 3 | `old - new` |

Children 2 and 3 are slide bars, and `HudSlideBar_AddDelta` (`0043b3f8`) does `value -= delta` clamped to the bar's limits, so they *track* their angle. Both start at zero, which is where the machine's angles start.

**The heading's delta reaches a waypoint indicator, and does nothing.** Child 7 overrides slot `+0xc` with `HudWaypointIndicator_ShiftLimits` (`0043c3d0`), which adds the delta to `+0x24` and `+0x26` rather than to a value — and `Hud_UpdateWaypointIndicator` reads neither, only the range `+0x2c` that an equal shift of both leaves alone. The heading tape, child 1, is not driven from here at all.

The same call copies the whole 38-byte state block into children 4 and 5. Everything in it past the three angles is filled by the gunsight's own update slot, `Gunsight_UpdateAndPaint` (`0043d6dc`), from the target block at `CockpitView+0x26c` — see [`hud-target-indicator.md`](hud-target-indicator.md). That slot also drives child 1 and runs each child's slot `+4`.

### Rotation indicator

The manual's sliding green bar. It has **no `.GAU` rect of its own**: `Gau_RovingGunsightWidget` derives it from the heading tape's rect with literals — `+15, -10` from its top-left, 90 wide and 4 tall, all in `.GAU` units and shifted by the video mode.

`HudRotationIndicator_Paint` (`0043b4a4`) draws two `HUD`-bank frames:

```
track:  frame 11 (182x10) at (rect.x0, rect.y0 - (2 << YCoordShift))
bar:    frame 13 while |value| <= 299, frame 12 otherwise
        x = rect.x0 + (value - min) * rectWidth / range  (+1 if value < 0)  - (15 << XCoordShift)
        y = rect.y0
```

Frame 13 is green and 12 yellow; the 299 threshold is about 1.6°, so any deliberate movement trips it. The trailing `-15` undoes the `+15` the rect carries, which centres the 31-unit-wide bar on the mapped point. The ±`0x38e3` limit is about 80°, deliberately wider than any herc's own 14000 twist limit, so the bar never reaches the ends of its track.

### Heading tape

Child 1, the manual's Heading Indicator. `HudHeadingTape_Recompute` (`0043b5dc`) caches the rect's width at `+0x28` and the `hudhtick` bank's last and first frames at `+0x48`/`+0x4c`; `HudHeadingTape_SetHeading` (`0043b654`) converts the angle it is given into a frame pair and a sub-frame offset:

```
total   = Math_Q16Multiply(heading, framePixels)     // +0x40, bankFrames * rectWidth in Q16
frame   = total / rectWidth,  offset = total % rectWidth   // both wrapped at bankFrames
+0x44   = rect.x0 - offset
+0x48   = bank[frame],  +0x4c = bank[frame + 1]
```

The paint (`0043b6dc`) narrows the canvas clip to the rect and blits those two frames at `+0x44` and `+0x44 + rectWidth`, so the tape is a strip of full-width frames sliding through a window: the whole compass, degree labels included, is art, and the angle picks which slice of it shows. Retail's bank is nine 256x16 frames against a 240-device-pixel window, so the pair always covers it with no seam.

**The angle is the heading negated.** `Gunsight_UpdateAndPaint` reads the viewing object's `mech+0x10` and calls child 1's `+0xc` with `-heading`. Without that sign the strip would run opposite to the simulation's own bearings, and a tick would slide one way while the waypoint diamond naming the same bearing slid the other. Because the art's degrees rise left to right, the negation is also what makes the readout count *up* as the machine turns right, the ordinary compass convention, out of headings that run counter-clockwise.

#### Power-up wind-up

On taking a machine the tape starts at north and winds round to the real heading. Two fields of the shared widget base carry it — `+0x8c` armed, `+0x8d` done — cleared by the base constructor (`00438b20`) and set by `Widget_BeginPowerUpAnimation` (`00438ddc`), which also stamps `+0x90` with `Time_GetCoarseTicks`. While armed and not done, `Gunsight_UpdateAndPaint` substitutes a ramp for the heading:

```
ramp = (ushort)((coarseTicks - +0x90) * 0x32)
heading <= 0x8000:  angle = ramp,   done when heading <= ramp
heading >  0x8000:  angle = -ramp,  done when -ramp <= heading
```

`0x32` a coarse tick is about 17°/s, so the longest wind-up is some ten seconds. It always takes the short way round: below half a turn the angle climbs from north, above it the angle descends. `Cockpit_PowerUpTick` (`00432924`) arms the gunsight on the first tick after `Cockpit_PowerUpSound` stamps the sequence's start, and arms the ten heads-down gauges on their own delays.

**Two things stop it, which is why it is not seen every mission.**

- **A flyer never winds up.** `Gau_BuildCockpitWidgets` (`00431bf8`) ends with a branch taken when the piloted machine's type record has `InputFlagFlyer` set — `mech+0x1f2 -> +0x50`, the RAZOR alone (see [`../simulation/mech-locomotion.md`](../simulation/mech-locomotion.md)'s type-record table). It arms *and* immediately marks done the gunsight and the ten gauges, and sets `cockpit+0x245`, which stops `Cockpit_PowerUpSound` ever stamping the start time. So a RAZOR cockpit reads true from its first frame; the same flag gates the engine hum, [`audio.md`](audio.md#the-cockpit-power-up).
- **A heading past half a turn never winds up either.** The descending branch is done as soon as `-ramp <= heading`, and on the frame the widget is armed `ramp` is still zero — which is at or below every heading in that half. The arm and the first paint fall in the same pass, so a machine facing anywhere past `0x8000` is done before it has moved. The climbing branch survives that frame, since a climbing zero is below every heading but zero itself. Listed in [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

Only the tape is ramped. The waypoint indicators over it go on reading the true heading throughout, so they and the compass visibly disagree for as long as the wind-up lasts.

### Waypoint indicators

Children 7 and 8, the manual's Waypoint Indicator. Both are `HudWaypointIndicator_Ctor` (`0043c268`) — `HudRotationIndicator_Ctor`'s object with vtable `0049c154`, a label child, and the `±0xe38` limits the gunsight hands them. **Neither has a `.GAU` rect of its own**: the complex passes both of them the heading tape's rect (offset 1104), so a mark rides the same span of bearing the compass under it does. The `+0x45` flag separates them:

| Child | `+0x45` | Subject | Colour id | Caption |
|---|---|---|---|---|
| 7 | 1 | `NavMarker_Position` (`0043495c`) | `DAT_004d3c1e`, id 15 → palette 13 yellow | none |
| 8 | 0 | The player group's route, or `mech+0x1a4` on a branch that never runs | table entry 0 → palette 14 green | `WAYPOINT n: d M.` |

What each points at, and the branch that never runs, are [`../simulation/player-waypoints.md`](../simulation/player-waypoints.md).

`Hud_UpdateWaypointIndicator` (`0043c3e4`) is the shared paint. It takes the ground range with `Vec2_DistanceBetween` and the bearing with the `Math_Atan2Guarded(dx, dy) - 0x4000` that `Math_HeadingToward` is, then works the error `mech.heading - bearing` as an unsigned short:

| Error | Shape |
|---|---|
| ≤ `0xe38` or ≥ `0xf1c8` | Diamond, on the tape |
| `0xe39`-`0x7fff` | Arrow past the rect's **right** end, pointing right |
| `0x8000`-`0xf1c7` | Arrow past its **left** end, pointing left |

Simulation headings run counter-clockwise, so a positive error is a subject off to the player's right — which is the end its arrow parks at, and the side of centre its diamond sits on.

Both shapes are filled polygons through `Raster_DrawPolygonDispatch`, not sprites, and both hang off the rect's **top** edge lifted `4 << YCoordShift`. The diamond's centre is `rect.x0 + rectWidth/2 + error * rectWidth / 0x1c70` — the widget's own `+0x28` width over its `+0x2c` range — so it reaches the rect's ends exactly at the limits; it is `5 << XCoordShift` by `5 << YCoordShift` about that point. An arrow's base sits `2 << XCoordShift` past the rect's end with its tip `4 << XCoordShift` further out and its base `5 << YCoordShift` tall.

The caption is the label child, given the tape's rect dropped `3 << YCoordShift` and centred in it — which puts the line below the compass while the marks sit above. Font is `ColorSchemePanels[17]` (`HUD3`, the same face the speed and time *values* use). The text is `STRINGS0.STR` group 37 entry 1 (`"WAYPOINT "`, trailing space included) then the waypoint number, `": "`, the range in metres and `" M."`. The number is the route cursor plus one and the range is `Hud_WorldUnitsToMetres`, so it is always a multiple of six — see [`../engine/planning.md`](../engine/planning.md#world-scale).

### Speed and time readouts

`Gau_RovingGunsightWidget` places these from two anchor points in the same block, both already device-shifted:

- **1128/1132** is the *left* edge of the `SPEED:` caption. The value follows at `captionEnd + (2 << XCoordShift)`.
- **1120/1124** is the *right* edge of the time field. Its left edge is that minus the measured width of `"00000"` — a five-digit reservation — and the `TIME:` caption is right-aligned `(2 << XCoordShift)` before it.

Captions use `ColorSchemePanels[16]` (`HUD2`, ink 73) and values `[17]` (`HUD3`, ink 74). Those are theater palette indices, not colours the widget picks — which is where retail's pale yellow-green captions and cyan values come from.

## `.PDG` — paper-doll damage diagram

`PaperDoll_Load` (`004379cc`, `pdamage.cpp`) matches `PaperDollGraphic` field for field: 3 views, each an origin/size pair plus a vector of `0x1c`-byte regions (`{int index, PixelPoint topLeft, PixelPoint bottomRight, int colorId, int spacer}`).

Coordinates are authored in the 320-wide space and shifted by `VideoMode_X/YCoordShift`, with `bottomRight` additionally `+1` in the 640-wide mode, so a region covers the full 2x2 device footprint of each source pixel. Region art comes from `{herc}.HBA`/`.DBA`, frame `n` for view `n`.

The two nameless fields are what makes a region a damage region:

| Field | Offset | Meaning |
|---|---|---|
| `colorId` | `0x14` | The colour the art drew that body part in — a `COLORS.DAT` id, resolved to a palette index in place at load. Retail uses 9, 12, 15, 20, 24 and 25 |
| `spacer` | `0x18` | Recolour mode. **Every retail region states 0**; modes 1-3 are unexercised |

### Tinting

A region is not filled. `PaperDoll_RecolorRect` (`00437e94`) walks the region's rect a pixel at a time and, in mode 0, rewrites only the pixels still holding `colorId`, which is why the outlines and detail drawn over a limb survive its recolour. Modes 1 and 3 do the same without the doubled pixel step; mode 2 adds the tint to every pixel that is not the id-19 background. Modes 0 and 1 skip the walk when the two colours are equal, so an undamaged region costs nothing.

`PaperDoll_RecolorRectFromArt` (`00438230`) is the same four modes reading the source bitmap instead of the raster, for a screen that repaints a region without having repainted what is under it first — the Heads-Down Display's route, where the MFD takes the first.

`Damage_PickRegionTint` (`00438624`) chooses the colour from one Q8 damage reading, on `Damage_ToConditionState`'s own bands:

| Intact | State | Tint (`COLORS.DAT` id → palette) |
|---|---|---|
| ≥ 90% | 0 | 12 → 14 green |
| ≥ 74% | 1 | 15 → 13 yellow |
| ≥ 51% | 2 | 20 → 12 orange |
| ≥ 1% | 3 | 9 → 10 red |
| 0 | 4 | 18 → 20 grey |

Which reading a region takes is the caller's business, and the two callers disagree: see [`mfd.md`](mfd.md#viewport-and-condition-per-class) for the status screen's compact view and [`heads-down-display.md`](heads-down-display.md#damage-detail--page-1) for the damage detail's.

## HUD fonts

`ColorSchemePanels_LoadAll` (`00431098`) lazily loads 18 `.DFN`/`.HFN` fonts into `ColorSchemePanels` (`0049b0ac`), then 7 `.DCI` cursors. Load order is the array index:

| 0-5 | 6-11 | 12-17 |
|---|---|---|
| `cpblue`, `cpgreen`, `cpred`, `cpylw`, `cpon`, `cppress` | `cpoff`, `cpgrey`, `cpblack`, `cporange`, `white`, `gray` | `dark`, `green`, `red`, `hud1`, `hud2`, `hud3` |

Each file is the same typeface stencilled in one palette index, so **a widget picks its text colour by picking a font** — no colour is ever passed to a label. Consumers reach entries by absolute address: `0049b0d4` = 10 `white`, `0049b0d8` = 11 `gray`, `0049b0dc` = 12 `dark`, `0049b0ec` = 16 `hud2`, `0049b0f0` = 17 `hud3`.

Format, glyph layout and per-file ink indices: [`dfn-hfn-dci.md`](dfn-hfn-dci.md).

## Per-frame ordering

`maybe_Sim_RenderFrame` (`0045fb9c`): `Terrain_SetupVisibleRegion`, then `FUN_004327ac` (`CockpitViewInstance` widget paint dispatch), then `maybe_Scene_SubmitFrameObjects` (the 3D world), then `Player_PerFrameCockpitUpdate`, then three more paint dispatches on `CockpitViewInstance` sub-objects (`+0x1f5`, `FUN_00433158`'s result, `+0x20b`).

## Open

- `WPN_DMG`'s fill levels are not drawn. The per-mount reading behind them exists — combined entry `32 + slot` of `Component_FillDamageReadouts`' buffer, which the Heads-Down Display's weapons page already prints — but the cockpit's weapon rows do not carry it. The engine also draws the row plate as the underlay instead of `WPN_DMG` frame 0, which is equivalent only while the row is undamaged.
- Widget *state* sources generally: which frame or fill level a widget is in per frame is driven from the mech object, not from the `.GAU`.
- `static` and `pilot<n>` ship in `dba\` only, so the 640-wide mode has no matching art for them; see [`heads-down-display.md`](heads-down-display.md).
- RAZOR's non-stub view-1 3D viewport is not rendered.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `Palette_BeginCrossFade` is the damage flash | Its three call sites are all the mech-death screen flash. The damage flash is a hard alternation between two whole palettes — `Palette_ActivateImpact`/`Palette_ToggleImpact` — with no fade of any kind between them |
| The shake's amplitude is how far the view travels | The band is the limit pair the walk is bounded by, not its excursion. A step is only ever taken toward the farther limit, which reverses at the middle, so a ten-pixel band produces a one-to-eight-pixel wander — see "The damage shake" |
