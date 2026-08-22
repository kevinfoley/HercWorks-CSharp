# The Heads-Down Display

NOTE TO CLAUDE: This should be a reference document, not a personal journal.

The two-page console below the dashboard, reached by panning down from the forward view.
Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project; addresses are DBSIM. Symbols
are in `tools/ghidra_scripts/known_symbols.json`, applied with `ES2ApplySymbolNames.java`.

Surrounding cockpit, canopy art and the pan itself: [`cockpit-hud.md`](cockpit-hud.md). Caption text:
[`str-strings.md`](str-strings.md). Label placement and fonts: [`dfn-hfn-dci.md`](dfn-hfn-dci.md).
Closest precedent for the widget vocabulary: [`mfd.md`](mfd.md).

Engine implementation: `Herculan.Engine.Content.{HddLayout, HddPage, HddDamageView}`,
`Herculan.Engine.Render.Overlay2DRenderer.AddHeadsDown`.

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

`HddCommandScreen_Ctor` (`0044c264`).

**Map viewport**: the screen rect inset `4 << XCoordShift` on x and `2 << YCoordShift` on y, with its
right edge taken from the *order column's* left edge minus the same x inset. Backed by a `0x239`-byte
offscreen render target — the same object the MFD's nav map uses — centred at
`-(width >> 1), -(height >> 1)`. Zoom scale fits the whole zone; the step is
`(scale - 60000) / 25`, floored at 5000.

**Markers**: 140 icon gadgets allocated up front. `HddCommandScreen_BuildMapMarkers` (`0044ded8`)
fills them per frame — up to 9 waypoints from the player's route (icons `0x4e`+), one per object in
the three global object lists, then the player (icon `0x57`) — and releases the rest.

**Order rows**: the column's height divided by 9. Row 0 is the incoming-message label
(`ColorSchemePanels[2]`, background id 14); rows 1-8 are the orders, each 14 device pixels tall, left
aligned with a bare `5`-pixel margin (unshifted, unlike the MFD's FLASH COMM rows). A 2px-wide
vertical bar at the column's left edge marks the selected row in id 15.

**Order text** is `STRINGS0.STR` group 0 entries 10-17, and each entry's single attribute byte is the
index of its hotkey character within its own text:

| Entry | Text | Attr | Hotkey |
|---|---|---|---|
| 10 | `DISENGAGE` | 0 | D |
| 11 | `ATTACK ENEMY` | 0 | A |
| 12 | `DEFEND POSITION` | 2 | F |
| 13 | `PATROL GRIDPOINT` | 2 | T |
| 14 | `GOTO GRIDPOINT` | 0 | G |
| 15 | `JOIN ON ME` | 5 | O |
| 16 | `SCAN FOR HOSTILES` | 1 | C |
| 17 | `EMCON` | 0 | E |

All eight match the manual's key bindings. `HddCommandScreen_RefreshOrders` (`0044ddec`) fonts an
available order `ColorSchemePanels[1]` `CPGREEN` with the hotkey character in `[2]` `CPRED`, an
unavailable one wholly in `[0]` `CPBLUE`, and the selected one in `[3]` `CPYLW`.

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
titles, the screen and map-viewport floods, the eight orders with their hotkey characters,
XMIT/CANCEL, the three comm boxes in their unoccupied-slot fill, the paper doll per category, and 13
component rows — structural and internal from the string table, weapons from the player's own fitted
hardpoints, each reading the undamaged `100` its value column is sized around.

Not drawn: the map's terrain raster and its 140 markers, pilot video and static, order availability
and selection, per-component damage percentages and their colours, comm-box name/status/objective
labels. All need squad, targeting or damage state the sim does not carry.

`Herculan.Engine.Host` takes `--hdd [0|1]` and `--hdd-damage [0-2]`, since a `--screenshot` run never
sees a keystroke. `[S]`/`[I]`/`[W]` are gated on the damage page being down: two of the three collide
with this host's camera keys.

## Open

- `static` and `pilot<n>` ship in `dba\` only, at 320-wide sizes, so a 640-wide mode has no matching
  art for them. `static` is loaded through the shared `dba`/`hba` folder global, which selects `hba`
  in that mode and would miss.
- Block indices 2-3 (1220) and `0x5d` (1584) are read by no constructor.
- The comm-box highlight mode's 0 branch, which fills the box rect rather than the marker, is
  unexercised by retail data.
- The map's own rasterizer and marker icon set.
