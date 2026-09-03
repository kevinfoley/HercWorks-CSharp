# The Heads-Down Display

The two-page console below the dashboard, reached by panning down from the forward view.
Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project; addresses are DBSIM. Symbols
are in `tools/ghidra_scripts/known_symbols.json`, applied with `ES2ApplySymbolNames.java`.

Surrounding cockpit, canopy art and the pan itself: [`cockpit-hud.md`](cockpit-hud.md). Caption text:
[`str-strings.md`](str-strings.md). Label placement and fonts: [`dfn-hfn-dci.md`](dfn-hfn-dci.md).
Closest precedent for the widget vocabulary: [`mfd.md`](mfd.md).

Engine implementation: `Herculan.Engine.Content.{HddLayout, HddPage, HddDamageView}`,
`Herculan.Engine.Render.Overlay2DRenderer.AddHeadsDown`. The command display's own types are listed
in its section below.

How a click on one of the widgets below reaches its own click handler:
[`cockpit-input.md`](cockpit-input.md).

## Object model

| Symbol | Address | Role |
|---|---|---|
| `Gau_PilotRosterWidget` | `00432634` | Builds the display from `.GAU` offset 1212. |
| `HddGau_ApplyCoordShift` | `0044bed0` | Pre-shifts that block; biases its origin y by `+0x28`. |
| `HddDisplay_Ctor` | `00448cc8` | Builds both pages, 15 widgets, 3 comm gauges, the title label. |
| `HddDisplay_SetPage` | `0044a5e4` | Switches page; relights the page button; applies the visibility table. |
| `HddDisplay_Repaint` | `00449a50` | Full repaint (order below). |
| `HddDisplay_SetTitle` | `0044a6dc` | Fills the title label from the string table. |
| `HddDisplay_SelectPilot` | `0044a720` | Selects one of the three comm boxes. |
| `HddButton_Ctor` / `_Paint` | `0044baac` / `0044bb38` | One sub-widget. `_Paint` switches on the widget index: 0-1 (the page buttons) take their frame and caption font from the selection flag `+0x40` and have **no pressed state**; 2-7 (arrows, magnifiers) and 13-14 (XMIT, CANCEL) take theirs from the press byte `+0x1b` and light only while held. |
| `HddGauge_LoadPilotFrames` | `0044a7c0` | Per-squadmate `pilot<n>` bank, `.OFS` offsets, and the comm box's six labels. |

Translation unit `PHDD.CPP`, from the error literals at `0049d52d`/`0049d55a`.

`HDDisplay` fields:

| Offset | Contents |
|---|---|
| `+0x18` | 15 widget pointers, `+0x18 + i*4` |
| `+0xc1` | Screen rect |
| `+0xd1` | Damage-detail column rect |
| `+0xe1` | Command-display order column rect |
| `+0xf1` | Title indicator rect |
| `+0x101` | 2 page objects, `+0x101 + page*4` |
| `+0x109` | Current page; -1 before the first switch |
| `+0x12d` | 3 comm gauges, `0x14e` bytes each |
| `+0x517` | Selected pilot slot, -1 for none; `+0x52b` the previous |
| `+0x51b` | Title label |
| `+0x51f` | Indicator-colour flag; constructor sets 1 |
| `+0x529` | Comm-box highlight mode, from block offset `0x5e` |
| `+0x548` | 5 name pointers: player, 3 squadmates, `TARGET` |
| `+0x55c` | Subject selector into the above |
| `+0x562` | `hddclip` clip-region block |

## Pages

Page index = key - 7. `HddDisplay_Ctor` ends with `HddDisplay_SetPage(obj, 0)`.

| Page | Key | Title | Ctor | Object size |
|---|---|---|---|---|
| 0 | F7 | `MAP` | `HddCommandScreen_Ctor` `0044c264` | `0x382` |
| 1 | F8 | group 12 by category | `HddDamageScreen_Ctor` `0045079c` | `0xf4` |

Page 0 is handed the screen rect and the order column; page 1 the screen rect and the damage column.

## `.GAU` block at 1212

**The whole display is authored per herc** — unlike the MFD, whose `.GAU` supplies one panel rect and
nothing else. The block runs to 1588 and is read as `int32`s from its own start:

| Index | Bytes | Contents |
|---|---|---|
| 0-1 | 1212 | Origin offset, added to every rect below |
| 2-3 | 1220 | Unread |
| 4-7 | 1228 | Screen rect |
| 8-11 | 1244 | Order column rect |
| 12-15 | 1260 | Damage column rect |
| 16-19 | 1276 | Title indicator rect |
| `0x14`+4i | 1292 | 15 widget rects |
| `0x50`+4i | 1532 | 3 comm-box marker rects |
| `0x5c` | 1580 | Arrow-button frame set, 0 or 1 |
| `0x5d` | 1584 | Unread |
| `0x5e` | 1588 | Comm-box highlight mode |

All values are authored in the 320-wide space. `HddGau_ApplyCoordShift` adds `0x28` to the origin's y
**before** shifting, then shifts every rect by `VideoMode_X/YCoordShift`; the constructor adds the
shifted origin to each rect.

**The `+0x28` bias is what puts the block on the art.** Every retail file authors the origin as
`(0, 197)`, so the bias makes it `(0, 237)` — the canvas origin every retail `.VUE` gives view 1,
i.e. the row `.HB1` is blitted at. Subtracting the `.VUE` origin back off yields art-local
coordinates, and in retail data the two cancel exactly: art-local device = authored x 2.

Screen rect is 459x201 device (230x101 authored inclusive) in every herc; only its position varies.

| Herc | Screen rect (device, art-local) | Arrow set |
|---|---|---|
| APOCA | `92,12 – 550,212` | 0 |
| COLOSSUS | `92,12 – 550,212` | 0 |
| MAVERICK | `92,32 – 550,232` | 0 |
| OGRE | `92,38 – 550,238` | 0 |
| OUTLAW | `94,84 – 552,284` | 1 |
| RAPTOR2 | `90,56 – 548,256` | 0 |
| RAZOR | `90,84 – 548,284` | 1 |
| SAMSON | `90,86 – 548,286` | 0 |
| TOMAHAWK | `90,134 – 548,334` | 1 |

Positions differ structurally, not just by offset: TOMAHAWK puts its comm boxes above the map, OUTLAW
and RAZOR stack the two page buttons vertically, APOCA/COLOSSUS/MAVERICK put the button strip below
the map rather than above it.

## Widgets

15, in the constructor's index order. Frames are `hba\HDD.HBA`; the lit frame is always the one after
the unlit.

| i | Role | Unlit frame | Caption |
|---|---|---|---|
| 0 | F7 page button | 25 | `"F7"`, composed |
| 1 | F8 page button | 25 | `"F8"`, composed |
| 2 | Arrow up | `set*8 + 5` | — |
| 3 | Arrow down | `set*8 + 7` | — |
| 4 | Arrow left | `set*8 + 9` | — |
| 5 | Arrow right | `set*8 + 11` | — |
| 6 | Zoom out (lower magnifier) | 21 | — |
| 7 | Zoom in (upper magnifier) | 23 | — |
| 8 | — | none | Degenerate zero rect in every retail file; no paint case |
| 9 | Title box | none | Holds the title label's rect; no paint case |
| 10-12 | Comm boxes | none | Rects only; the gauges paint them |
| 13 | XMIT | 0 | group 9 entry 0 |
| 14 | CANCEL | 0 | group 9 entry 1 |

Both page buttons take frames 25/26: the constructor's shared `case 0: case 1:` body re-sets the
frame base each time, so they differ only by their lit flag.

Page buttons caption themselves from the `"Fx"` literal at `0049d4f5` with byte 1 overwritten by
`'7' + index` — the same trick `MfdButton_SetCaption` uses for `F1`-`F6`. Font is
`ColorSchemePanels[12]` `DARK` lit, `[10]` `WHITE` unlit. XMIT/CANCEL use `[4]` `CPON` unlit and
`[5]` `CPPRESS` lit, centred on a caption box of the plate's own 54x20 size rather than on the widget
rect.

### Frame-to-widget confirmation

Every widget rect checked against its frame's own size in `hba\HDD.HBA`, across all nine retail
`.GAU` files — 90 checks, 54 exact. Device inclusive width is `2*(x1-x0) + 1`, so the comparison is
`rect + 1` against the frame:

| Widgets | Rect+1 | Frame | |
|---|---|---|---|
| Arrows, set 0 up/down | 58x18 | 5-8 = 58x18 | exact |
| Arrows, set 0 left/right | 18x58 | 9-12 = 18x58 | exact |
| Arrows, set 1 | 22x28 | 13-20 = 22x28 | exact |
| Magnifiers | 46x22 | 21-24 = 46x22 | exact |
| Page buttons | 26x12 | 25-26 = 26x14 | plate overhangs 2 rows |
| XMIT/CANCEL | 70x18 | 0-1 = 54x20 | plate narrower than the click rect |

The two inexact classes are inexact in the original too: the page-button overhang is the same idiom
the `PWEAPONS` row plates use, and XMIT/CANCEL's rects overlap each other by a column, so they are
hit regions rather than art extents.

`HDD.HBA` has 27 frames; `HDD.DBA` has the same 27 at different sizes — this bank is **not** a 2x
pair, unlike the rest of the `hba`/`dba` set.

| Frames | Size (`.HBA`) | Use |
|---|---|---|
| 0-1 | 54x20 | XMIT/CANCEL plate |
| 2-4 | 116x18 | Selected-order highlight bar, 3 states |
| 5-12 | 58x18, 18x58 | Arrow set 0 |
| 13-20 | 22x28 | Arrow set 1 |
| 21-24 | 46x22 | Magnifiers |
| 25-26 | 26x14 | Page buttons |

### Visibility

2 rows x 15 bytes at `HddPageWidgetVisibility` (`0049d24c`), indexed `[page][widget]`. A 0 sets the
widget's state to 2, the value `HddButton_Paint` refuses to draw at.

| Page | Widgets shown |
|---|---|
| 0 Command display | 0-7, 13-14 |
| 1 Damage detail | 0-5 |

Both rows hide widgets 10-12. That is not a contradiction: those widgets paint only the selection
highlight, and `HddGauge_LoadPilotFrames` clears the state back to 0 for each slot a squadmate
occupies. The boxes themselves are drawn by `HddDisplay_Repaint`.

## Paint order

`HddDisplay_Repaint`: visible widgets, the current page's paint, the indicator rect, the title, then
the three comm gauges. A page floods its screen rect only on a full repaint, which is why the widgets
going first does not erase XMIT and CANCEL.

## Colours

Logical ids through `dat\COLORS.DAT` (see [`cockpit-hud.md`](cockpit-hud.md)).

| Use | Id | Palette |
|---|---|---|
| Screen flood, label backgrounds | 19, and 3 on the damage screen | 16, black |
| Title indicator, normal / flagged | 13 / 15 | 102 / 13, yellow |
| Comm-box marker, deselected / selected | 13 / 15 | 102 / 13 |
| Damage subject caption plate | 6 | 98, blue |
| Order message row background | 14 | 103 |
| Comm-box name background | `COLORS.DAT[slot]` | 14 / 15 / 31 |
| Damage row ids at `HddDamageColorIds` (`0049d9ec`) | 19, 9, 15, 12 | 16, 10, 13, 14 |

## Command display — page 0

`HddCommandScreen_Ctor` (`0044c264`), repainted by `HddCommandScreen_Repaint` (`0044c894`) and
updated by `FUN_0044c960`, its vtable's slot 1. Translation unit fields are quoted off the screen
object, not the display.

| Symbol | Address | Role |
|---|---|---|
| `HddCommandScreen_Ctor` | `0044c264` | Builds the map target, 140 marker gadgets, nine label rows and the two click regions. |
| `HddCommandScreen_Repaint` | `0044c894` | Screen flood, order rows, selected-row bar, magnifiers, markers, map. |
| `HddCommandScreen_KeyDispatch` | `0044cc40` | Vtable slot 4; switches on the DOS scancode. |
| `HddCommandScreen_DrawMap` | `0044e30c` | Everything inside the viewport, in the order listed below. |
| `HddMap_DrawTerrain` | `004502e4` | Blits the raster between two projected corners. |
| `HddMap_BuildTerrainRaster` | `0044f6cc` | Builds that raster once per mission. |
| `HddCommandScreen_AddObjectMarker` | `0044e080` | One object's icon, colour and size. |
| `HddMarker_Paint` | `0044f194` | One marker gadget. |
| `HddCommandScreen_SelectOrder` | `0044d9cc` | Arms an order, or clears it. |
| `HddCommandScreen_SelectPilot` | `0044da70` | Moves the comm-box selection. |
| `HddCommandScreen_HitTestMarker` | `0044d860` | Screen point to object. |

Engine implementation: `Herculan.Engine.Content.{HddMap, HddMapView, HddMapBounds, HddMapMarker,
HddCommandScreen, HddCommandState}`, `Herculan.Engine.Render.{HddMapRaster,
Overlay2DRenderer.DrawHddMap}`.

### The map's frame of reference

**The mission box is `script.dat` block 1's bounding box.** `DBSim_LoadScriptDat` (`00424308`)
accumulates it into `DAT_004aa6c4`..`d0` (min x, min y, max x, max y) as it reads the coordinate
list, before any roster block. Everything the map does is measured against it:

- the screen copies it into its own `+0x160` rect and draws it as the manual's red mission border;
- the pan clamp is that box grown by 60000 world units on every edge;
- the terrain raster covers the grown box;
- the full zoom-out scale fits it.

**Map viewport**: the screen rect inset `4 << XCoordShift` on x and `2 << YCoordShift` on y, with its
right edge taken from the *order column's* left edge minus the same x inset. Backed by a `0x239`-byte
offscreen render target — the same object the MFD's nav map uses — centred at
`-(width >> 1), -(height >> 1)`.

**Projection.** `FUN_0044d160` installs a view projection carrying three numbers only: the centre x
and y (`+0x18`/`+0x1c`) and the scale (`+0x20`). Points go through `Raster_PerspectiveDivide` against
a focal length of `1 << DAT_0049d6bc` = 256, so

```
screen = (world - centre) * 256 / scale        // y negated: world +y is up the map
```

and `scale` is world units per pixel in 8.8 fixed point. `FUN_0044d224` then adds the viewport's own
origin and half-extent.

| Quantity | Value | Source |
|---|---|---|
| Full zoom-out scale | `min((maxX-minX)/2 / halfWidth, (maxY-minY)/2 / halfHeight) << 8` | ctor |
| Closest scale | 60000, i.e. 234 units/pixel | `FUN_0044cf9c`'s floor |
| Zoom step | `max((full - 60000) / 25, 5000)` | ctor |
| Pan step | `(((scale - 60000) >> 8) * 45000 / (full - 60000) << 8) + 5000` | `FUN_0044eea0` |

`min`, not `max`, on the fit: the tighter axis fills the viewport and the other crops. The centre is
the player's own position plus the pan offset, re-clamped every repaint so the viewport's edge never
leaves the grown box.

### Terrain raster

`HddMap_BuildTerrainRaster` (`0044f6cc`) runs once per mission, not per frame. It walks the active
height grid, turns each cell's raw height into a palette index, and Gouraud-shades two triangles per
cell into an offscreen 8-bit bitmap; `HddMap_DrawTerrain` (`004502e4`) then blits that bitmap between
two projected corners on every repaint, which is why panning and zooming cost nothing.

```
palette = min(rawHeight, 0x7f) / 8 + 16
```

Sixteen entries, 16-31 — the theater-owned half of the ramp, so the map re-colours with the theater
exactly as the terrain does. The bitmap is sized `cells * scale` where `scale` is the largest integer
fitting the grown box's cell span inside 640x400.

The cell array is `ActiveHeightGrid + 0xec`, stride `0x10`, height in byte 0; the grid's dimensions
come from `+0x100`/`+0x104` (log2) and its cell size from `+0x108`.

### Grid and border

`HddCommandScreen_DrawMap` (`0044e30c`) projects the world origin and the point 3,200,000 units out
on both axes, divides the resulting pixel span by 16, and walks lines out from the origin in both
directions until they leave the viewport. A grid square is therefore **200,000 world units — 1200
metres**. Lines are colour id 11.

The border is the mission box drawn through fill brush mode 4, which `Raster_FillRect` (`004865f8`)
answers by walking the rect's four edges as lines rather than filling it — a one-pixel frame, in id 9
(palette 10, red).

### Markers

140 icon gadgets allocated up front. `HddCommandScreen_BuildMapMarkers` (`0044ded8`) refills them per
frame — the player's route first, then one per object in the three global object lists, then one more
whose position comes from `FUN_0043495c` — and releases the rest.

Route markers take icons `0x4e`+ and start at the route's **second** point: the loop bound is
`count - 1` capped at 9 and it indexes `route[i + 1]`.

`HddCommandScreen_AddObjectMarker` (`0044e080`) picks the icon from the object's target class
(`+0x1a8`) and its group's side byte (`type+0x12`, 1 for cybrid):

| Class | Friendly | Hostile | Size | Rotates | Ranged |
|---|---|---|---|---|---|
| 0 HERC, the player | `0x2a` | — | 10 | yes | no |
| 0 HERC, squad slot *n* | `0x33 + n*9` | — | 10 | yes | no |
| 0 HERC, other | `0x21` | `0x18` | 10 | yes | no |
| 2 flyer | 6 | `0xf` | 10 | yes | no |
| 1/3 structure, `BASES.DAT +0x32` set | `0x58` | `0x59` | 6 | no | yes |
| 1/3 structure, `+0x28` in the listed set | 3 | 5 | 14 | no | yes |
| 1/3 structure, otherwise | 2 | 4 | 18 | no | yes |

The listed silhouettes are 1, 2, 6, 7, 10, 11, 15, 19, 20, 21, 22, 23, 24, 26 and 28. Sizes are the
argument to `FUN_0044f634`, which becomes the gadget's extent and, halved, the offset the icon is
drawn back by so it lands on the object.

**Rotation.** `HddMarker_Paint` (`0044f194`) buckets the object's heading into eight octants — a
heading within `0x1000` of zero is octant 0, and every other counts down from 7 in `0x2000` steps —
then adds `DAT_0049d67c[octant]` to the group's base frame and nudges the blit by
`DAT_004d1d54[octant]` and `DAT_004d1d5c[octant]` device pixels:

| Octant | 0 N | 7 NE | 6 E | 5 SE | 4 S | 3 SW | 2 W | 1 NW |
|---|---|---|---|---|---|---|---|---|
| Frame offset | 4 | 3 | 7 | 2 | 6 | 1 | 5 | 0 |
| Nudge x, y | 0, -8 | -4, -4 | -8, 0 | -4, 0 | 0, 0 | 0, 0 | 0, 0 | 0, -4 |

The frame sizes confirm it: in every nine-frame group, offsets 4 and 6 are the tall pair, 5 and 7 the
wide pair, and 0-3 the four square diagonals. A destroyed object (`+0x99`) takes the base frame with
no nudge.

**Range falloff.** A ranged marker computes an apparent size from its distance to the map centre,
measured in three dimensions with the zoom standing in for height:

```
apparent = 25000 << 7 / |(x - centreX, y - centreY, -scale)|
```

and draws a box of that size in its own colour — id 5 blue friendly, id 9 red hostile — whenever the
icon it would otherwise blit is taller. `25000 << 7 / 16` puts the crossover at 200,000 world units
out, the same 1200 m one grid square covers.

`hba\ICONS.HBA` is 90 frames: two singles, four structure icons, then eight nine-frame rotation
groups from frame 6, then ten 16x13 route markers at 78-87 and two 8x5 ticks. It is loaded lazily by
`HddMarker_Ctor` (`0044f130`) rather than with the rest of the display's art.

### The order list and its state machine

**Order rows**: the column's height divided by 9. Row 0 is the incoming-message label
(`ColorSchemePanels[2]`, background id 14); rows 1-8 are the orders, each 14 device pixels tall, left
aligned with a bare `5`-pixel margin (unshifted, unlike the MFD's FLASH COMM rows). A 2px-wide
vertical bar at the column's left edge marks the selected row in id 15, three device pixels down from
the row's top, and `HddCommandScreen_DrawOrderHighlight` (`0044dd4c`) blits the 116x18 plate — frame
2 available, frame 4 not — at the row label's own text position.

**Order text** is `STRINGS0.STR` group 0 entries 10-17, and each entry's single attribute byte is the
index of its hotkey character within its own text:

| Entry | Text | Attr | Hotkey | Scancode | Wants |
|---|---|---|---|---|---|
| 10 | `DISENGAGE` | 0 | D | `0x20` | — |
| 11 | `ATTACK ENEMY` | 0 | A | `0x1e` | a hostile unit |
| 12 | `DEFEND POSITION` | 2 | F | `0x21` | a friendly unit or a gridpoint |
| 13 | `PATROL GRIDPOINT` | 2 | T | `0x14` | a gridpoint |
| 14 | `GOTO GRIDPOINT` | 0 | G | `0x22` | a gridpoint |
| 15 | `JOIN ON ME` | 5 | O | `0x18` | — |
| 16 | `SCAN FOR HOSTILES` | 1 | C | `0x2e` | — |
| 17 | `EMCON` | 0 | E | `0x12` | — |

All eight match the manual's key bindings. `HddCommandScreen_RefreshOrders` (`0044ddec`) fonts an
available order `ColorSchemePanels[1]` `CPGREEN` with the hotkey character in `[2]` `CPRED`, an
unavailable one wholly in `[0]` `CPBLUE`, and the selected one in `[3]` `CPYLW` with no hotkey
alternate at all.

**Availability is one bit.** The screen keeps eight bytes at `+0x131`, and the only two functions
that write them set all eight: `FUN_0044edd8` to 1 when a pilot is selected and `FUN_0044edfc` to 0
when none is. So the list is either wholly live or wholly blue.

**The message row** is `STRINGS0.STR` group 32 — `SELECT PILOT`, `SELECT COMMAND`,
`DESIGNATE LOCATION`, `DESIGNATE TARGET` — chosen by `FUN_0044dc44` from the same two facts: whether
a pilot is selected, and which of the two picks the armed order wants.

**The rest of the keyboard**, from the same scancode dispatch:

| Key | Scancode | Effect |
|---|---|---|
| `,` `.` | `0x33` `0x34` | Previous / next order, wrapping; both return at once with no order armed |
| Tab | `0x0f` | Cycles the eligible unit for ATTACK ENEMY or DEFEND POSITION |
| Enter | `0x1c` | Drops the map cursor on the armed order's pick |
| X | `0x2d` | Presses XMIT |
| Backspace | `0x0e` | Presses CANCEL |
| Keypad 5 | `0x4c` | Zeroes both pan offsets |

The magnifiers and the four arrows are widget presses rather than keys: `FUN_0044a178`'s cases 2-7
route them to the four pan functions and the two zoom functions on page 0 and to the damage screen's
own subject and category steps on page 1. XMIT plays `Sound_Play(0x1a)` when the transmission
resolves to a recipient and `0x1b` when it does not.

## Damage detail — page 1

`HddDamageScreen_Ctor` (`0045079c`), updated by `HddDamageScreen_Update` (`00450c54`).

Three categories, set by `HddDamageScreen_SetView` (`00450b60`), which also sets the row count:

| View | Key | Rows | Names | `.PDG` view |
|---|---|---|---|---|
| 0 Structural | S | `0x13` | group 13, or 14 for a flyer | 0 |
| 1 Internal | I | `0xc` | group 15, or 16 for a flyer | 1 |
| 2 Weapons | W | fitted weapon count | the mech's own weapon names | none |

The flyer variant is selected by a flag at the subject type's `+0x50`.

**Rows**: 13 label pairs, so a 19-entry structural list scrolls. Each row is 8 device pixels tall at a
14-pixel pitch from the column's top. The name starts `0x1e << XCoordShift` = 60 pixels in from the
column's left edge and runs to the value column, whose width is the measured width of the literal
`"100"` (`0049da9d`) taken off the column's right edge. Both labels are `ColorSchemePanels[1]` on
background id 19.

**Paper doll**: the `.PDG` view for the category, blitted at the screen rect's top-left plus that
view's own origin, with per-region damage tints drawn over it from the region list.

**Subject caption**: `HddDamageScreen_SetSubjectCaption` (`0044ba2c`) fills an 81x15 device box 56
pixels in from the screen's left edge and 4 up from its bottom, from the display's five-name array at
`+0x548` indexed by `+0x55c`. The player draws `ColorSchemePanels[3]` on colour id 6; a squadmate
`[2]` on that pilot's own `COLORS.DAT` entry; the target `[2]` on id 15. With no subject the screen
also writes group 19 (`NO TARGET SELECTED` / `NO INFO AVAILABLE`) to a centred label.

## Squad comm boxes

Three, at widgets 10-12, backed by `0x14e`-byte gauges in a vector at `+0x12d`. Marker rects come
from block offset `0x50`; every retail file sets the highlight mode to 1, the branch that fills the
marker beside the box rather than the box itself.

`HddGauge_LoadPilotFrames` (`0044a7c0`) loads one squadmate's `pilot<n>` bank from `dba\` (hardcoded,
like `corners`) plus its 27-entry `.OFS` animation-offset table, and builds six labels relative to
the box rect, each `0x21` bytes:

| Label | Rect | Font | Text |
|---|---|---|---|
| `+0x12d` | `x0+4 .. x1-4`, `y0+8` | `[2]` `CPRED` | pilot name, background `COLORS.DAT[slot]` |
| `+0x131` | full width, `y0+32` | `[0]` `CPBLUE` | group 33 `STATUS:` |
| `+0x135` | full width, `y0+48` | `[2]` | group 28 condition |
| `+0x139` | full width, `y0+64` | `[0]` | group 33 `OBJECTIVE:` |
| `+0x13d` | full width, `y0+80` | `[2]` | group 40 current order |
| `+0x141` | `x0 .. x0+20`, bottom 20 | `[2]` | slot number, background id 15 |

Offsets are device pixels. The name's per-slot background — `COLORS.DAT` entries 0, 1, 2 = palette
14, 15, 31 — is the manual's "squad members are shown on the map in the same color that highlights
their name on the comm screen".

Paint state, matching the manual:

| Condition | Function | Draws |
|---|---|---|
| Not broadcasting | `HddGauge_PaintIdle` `0044ae78` | Box flooded id 19, five labels |
| Broadcasting | `HddGauge_PaintPilotFrame` `0044b120` | `pilot<n>` frame at its `.OFS` offset |
| Comms out | `HddGauge_PaintStatic` `0044b3b4` | `static` bank, 5 frames cycled |

`HddGauge_ConditionIndex` (`0044adf4`) averages the subject's 19 structural damage bytes into a 0-100
integrity percentage and buckets it at 90 / 74 / 51 / 1 into group 28's five conditions.

An unoccupied slot is not painted by the gauge at all: `HddDisplay_Repaint` floods the box rect inset
one device pixel with id 19 instead.

## `hddclip`

Loaded by `CockpitClipRegions_Load` from `edg\HDDCLIP.EDG` — the 320-wide clip file, shifted by
`VideoMode_X/YCoordShift` at load, not an `hdg\` counterpart. Regions are then offset by the screen
rect's own position minus the block origin. Same layout as the `.HD*`/`.ED*` files in
[`cockpit-hud.md`](cockpit-hud.md).

## Engine coverage

Drawn: page buttons with lit state, the four arrows and two magnifiers, the title indicator, page
titles, the screen flood, the paper doll per category, and 13 component rows — structural and internal
from the string table, weapons from the player's own fitted hardpoints, each reading the undamaged
`100` its value column is sized around.

Everything the command display draws is drawn: the terrain raster, the grid, the mission border,
every marker with its heading frame and its range falloff, the route waypoints, the order list with
its availability, selection, hotkey characters and highlight plate, the message row, XMIT and CANCEL,
and the three comm boxes with their names, conditions and objectives. Zoom, pan, recentring, pilot
selection and target designation are all wired to both the widgets and the keys. Three of those
labels are fed by stand-in state, below.

Not drawn: pilot video and its static — the `pilot<n>` and `static` banks ship 320-wide only (see
Open) and a squadmate has nothing to say until there is squad AI — and per-component damage
percentages and their colours on page 1.

**Stand-ins.** Three things read state the simulation does not carry yet, and none of them is the
original's behaviour:

- **A comm box's name** is the machine's own type name. The original stores a pointer per gauge,
  filled from the pilot roster in the player's save, which VSHELL owns and this engine does not read.
- **Its OBJECTIVE: line** is the last order transmitted to that slot. The original reads the
  machine's current AI state (`FUN_0041bac8`) and indexes group 40 with it, which is what the pilot is
  *doing* rather than what they were last told.
- **A transmitted order** is recorded against the slot and nothing else. There is no squad AI to
  receive it.

The map raster is built as one texel per grid cell and sampled bilinearly rather than Gouraud-shaded
into an intermediate bitmap. The colour rule is the original's exactly; what is dropped is the round
trip through a software rasterizer's scratch buffer.

`Herculan.Engine.Host` takes `--hdd [0|1]`, `--hdd-damage [0-2]`, `--hdd-pilot [0-2]` and
`--hdd-order [0-7]`, since a `--screenshot` run never sees a keystroke — and the order list only
leaves its unavailable blue once a pilot is selected. Key bindings that collide with the host's own
are gated on the relevant page being down: `[S]`/`[I]`/`[W]` on the damage page, the order hotkeys and
`[1]`-`[3]` on the command display. The one binding actually taken away rather than shared is the four
arrow keys, which scroll the map instead of steering while the command display is down; the keypad
keeps steering throughout.

## Open

- `static` and `pilot<n>` ship in `dba\` only, at 320-wide sizes, so a 640-wide mode has no matching
  art for them. `static` is loaded through the shared `dba`/`hba` folder global, which selects `hba`
  in that mode and would miss.
- Block indices 2-3 (1220) and `0x5d` (1584) are read by no constructor.
- The comm-box highlight mode's 0 branch, which fills the box rect rather than the marker, is
  unexercised by retail data.
- The extra marker `HddCommandScreen_BuildMapMarkers` appends after the object lists takes icon
  `0x57`, one past the nine route icons, and its position comes from `FUN_0043495c` —
  `CockpitViewInstance + 0x25e` when the flag at `+0x26a` is set. What sets that flag is not traced.
- `ICONS.HBA` frames 0-1, and the ninth frame of every rotation group, are addressed by nothing in
  the display — the eight octants use offsets 0-7 and a destroyed object takes offset 0. The
  briefing map is the likely consumer of the first pair.
