# The Multi-Function Display

Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project. Addresses are DBSIM. Symbols
are in `tools/ghidra_scripts/known_symbols.json`; apply with `ES2ApplySymbolNames.java`.

The console screen the F1-F6 keys switch between six screens. Surrounding cockpit:
[`cockpit-hud.md`](cockpit-hud.md). Caption text: [`str-strings.md`](str-strings.md).

Engine implementation: `Herculan.Engine.Content.{MfdLayout, MfdMode, SimStringTable}`,
`Herculan.Engine.Render.Overlay2DRenderer.AddMfd`.

## Object model

| Symbol | Address | Role |
|---|---|---|
| `Gau_MfdPanelWidget` | `004324c8` | Builds the display from `.GAU` offset 728, then `MfdDisplay_SetMode(obj, 3)`. |
| `MfdGau_ApplyCoordShift` | `00447650` | Pre-shifts the `.GAU` block by `VideoMode_X/YCoordShift`. |
| `MfdDisplay_Ctor` | `00445218` | Builds screen, 13 buttons, 2 labels, 6 screen objects. |
| `MfdDisplay_SetMode` | `00446e38` | Switches screen; relights the mode column; applies the button visibility table. |
| `MfdDisplay_Repaint` | `00446138` | Full repaint: buttons, background, buttons, screen, title. |
| `MfdDisplay_Update` | `00446328` | Per-frame update; dispatches the current screen's update slot. |
| `MfdButton_OnClick` | `0044681c` | Button dispatch — indices 0-5 call `SetMode(i)`. |
| `MfdButton_SetCaption` | `00447358` | `"F%d"` for indices < 6, caption table otherwise. |
| `MfdButton_Repaint` | `004474e4` | Re-fonts a caption: `DARK` when lit, `WHITE` when not. |

Object fields, base `MfdDisplay_Ctor`'s `param_1`:

| Offset | Contents |
|---|---|
| `+0x18` | 13 button pointers, `+0x18 + i*4` |
| `+0xb1` | Screen-shared state block, 0x10 bytes |
| `+0xbd` | Scanner range, from `_DAT_004d1cf4[+0xc5]` |
| `+0xc1` | Current mode; -1 before the first switch |
| `+0xc5` | Scanner zoom index, 0-2; init 2 |
| `+0xc9` | Title label |
| `+0xcd` | 6 screen objects, `+0xcd + mode*4` |
| `+0xeb` | Inset screen rect `x0, y0, x1, y1` |
| `+0xfb` | Base panel widget, covering the inset rect |
| `+0x329` | Message label |
| `+0x331`-`+0x341` | Radar sweep frame table and `radar` bank handle |

## Modes

Mode index = F-key - 1. `Gau_MfdPanelWidget` boots the display at mode 3.

| Mode | Key | Title | Screen ctor | Object size | Class |
|---|---|---|---|---|---|
| 0 | F1 | `STATUS` | `MfdStatusScreen_Ctor` `0043a2e0` | 0x42 | `MFDStatus` |
| 1 | F2 | `FLASH COMM` | `MfdFlashCommScreen_Ctor` `0043f5d8` | 0x49 | `MFDFlashComm` |
| 2 | F3 | `NAV MAP` | `MfdMapScreen_Ctor` `00440494` | 0x28 | `MFDMap` |
| 3 | F4 | `SCANNER` | `MfdRadarScreen_Ctor` `0043e70c` | 0x524 | `MFDRadar` |
| 4 | F5 | `TARGET` | `MfdStatusScreen_Ctor` `0043a2e0` | 0x42 | `MFDStatus` |
| 5 | F6 | `MISSILE CAM` | `MfdMissileViewScreen_Ctor` `0043facc` | 0x4a | `MFDMissileView` |

Modes 0 and 4 share one constructor and one class — the target screen is the status screen pointed
at another object.

Scanner ranges are `_DAT_004d1cf4` = 50000 / 100000 / 200000 world units = 300 / 600 / 1200 m at
1000 units = 6 m. Index 2 is the default, matching the retail screenshot's `RNG: 1200`.

## Geometry

One rect comes from the herc's `.GAU`; everything inside is hardcoded in DBSIM.

**`.GAU` offset 728** — the MFD block. 728/732 are an origin offset added to the rest, zero in all
nine retail files. 744-951 hold 13 rect-shaped slots that `MfdGau_ApplyCoordShift` coordinate-shifts
but no constructor reads; zero in every retail file. 952 is the panel rect
(`GAUFile.MfdPanel`), read as `param_2[0x38..0x3b]`.

Panel rect is 115x60 exclusive / 116x61 inclusive in every herc — only its position varies:

| Herc | Panel rect | Herc | Panel rect |
|---|---|---|---|
| APOCA | `102,173 – 217,233` | RAPTOR2 | `102,176 – 217,236` |
| COLOSSUS | `102,163 – 217,223` | RAZOR | `102,1 – 217,61` |
| MAVERICK | `102,179 – 217,239` | SAMSON | `102,167 – 217,227` |
| OGRE | `100,167 – 215,227` | TOMAHAWK | `102,176 – 217,236` |
| OUTLAW | `161,150 – 276,210` | | |

**Screen inset.** The constructor applies `x0 += 0x12 << XCoordShift` and leaves `y0`, `x1`, `y1`,
then works relative to that origin. The strip left of the inset holds the F-key column, which is why
its table x values are negative. The inset region is 98x61 GAU inclusive = **196x122 device** =
exactly the size of `MFD` bank frames 0-2.

Coordinates below are GAU (320-wide) units relative to the inset origin, inclusive on all edges.
Device pixels are 2x (`CockpitArt.GauToPixelScale`).

### Buttons

13 buttons from four parallel `int16` tables: `0049cacc` x0, `0049cae6` y0, `0049cb00` x1,
`0049cb1a` y1.

| i | Rect | Size | `MFD` frames | Caption |
|---|---|---|---|---|
| 0-5 | `-16, 1+10i – -2, 8+10i` | 15x8 | 3 / 4 | `F1`-`F6` |
| 6 | `0,0 – 0,0` | — | 3 / 4 | `MODE` |
| 7 | `50,2 – 95,11` | 46x10 | 5 / 6 | `SELECT` |
| 8 | `4,36 – 30,45` | 27x10 | 7 / 8 | `RANGE` |
| 9 | `4,47 – 30,56` | 27x10 | 7 / 8 | `TARGET` |
| 10 | `50,2 – 95,11` | 46x10 | 5 / 6 | `XMIT` |
| 11 | `4,14 – 30,23` | 27x10 | 9 / 10 | `PASS` |
| 12 | `4,25 – 30,34` | 27x10 | 9 / 10 | `ACTIVE` |

The lower frame of each pair is unlit, the upper lit. Every rect size matches a `dba\MFD.DBA` frame
size exactly, which is the layout's primary confirmation. Index 6 is a degenerate rect no visibility
row selects. Indices 7 and 10 share a rect: one top-right button under two names.

### Button visibility

6 rows x 13 bytes at `0049cbd8`. `MfdDisplay_SetMode` and `MfdDisplay_Repaint` index it as
`table[mode][button]`; the decompiler folds the `+6` entry offset into the symbol, so it reads as
`DAT_0049cbde + mode * 13`. Indices 0-5 are 1 in every row.

| Mode | Aux buttons shown |
|---|---|
| 0 Status | 7 `SELECT` |
| 1 FlashComm | 10 `XMIT` |
| 2 NavMap | none |
| 3 Scanner | 8 `RANGE`, 9 `TARGET`, 11 `PASS`, 12 `ACTIVE` |
| 4 TargetStatus | 7 `SELECT` |
| 5 MissileCam | none |

### Screen background

`MFD` frames 0-2 are three pieces of screen chrome, all 196x122: **0** two boxes split by a central
divider, **1** one box spanning the content area, **2** one small box in the top-left corner with the
rest open. `MfdDisplay_Repaint` selects by mode from the bank's frame-pointer array:

| Modes | Frame |
|---|---|
| 0, 1, 4 | 1 |
| 3 | 2 |
| 2, 5 | none — the blit is skipped |

**Frame 0 is never used as a background.** The nav map and missile cam fill the whole screen with
their own image; the map's paint floods its rect first (below).

Verified against the retail reference crops: status, flash-comm and target show vertical borders only
at the content area's outer edges, while the scanner shows an extra border at panel x 100-101 —
frame 2's box edge at frame-local x 64-65 plus the inset.

### Fixed labels

| Label | Rect | Font | Align |
|---|---|---|---|
| Title | `4,0 – 40,9` | `WHITE` (`ColorSchemePanels[10]`) | left |
| Message | `22,46 – 74,52` | `DARK` (`[12]`) | centre |

The message label carries the incoming-transmission caption `MfdDisplay_Update` fills from the same
object that drives FLASH COMM's talking-head frames; blank outside a transmission.

### Label placement

`Label_SetRect` (`00438884`) anchors, `Label_SetText` (`00438920`) places. Together, given a rect,
an alignment flag and a `short[4]` margin:

```
anchorX = flags & 2 ? (x1 - x0) / 2 + x0 + margin[0]     -- centre
        : flags & 4 ? x1 - margin[0]                     -- right
        :             x0 + margin[0]                     -- left
anchorY = (y1 - y0) / 2 + y0 + cellHeight / 2 + margin[2] + 1

width   = measure(text) - (1 << XCoordShift)
textX   = flags & 2 ? anchorX - width / 2 : flags & 4 ? anchorX - width : anchorX
textY   = anchorY - cellHeight
```

There is no vertical alignment flag: every label is vertically centred in its rect, and `textY`
reduces to `(y0 + y1) / 2 - cellHeight / 2`. Retail uses alignment 1 (left) for the title, the status
labels and the flash-comm rows, and 2 (centre) for button captions. All margins are zero except the
flash-comm rows'.

## Screens

### `MFDStatus` — modes 0 and 4

`MfdStatusScreen_Ctor` (`0043a2e0`) loads the `bases`, `vehicles` and `flyers` sprite banks — target
silhouettes — and builds a wireframe viewport plus five labels.

- Wireframe viewport `45,13 – 95,58` (102x92 device).
- Five labels at x0 6, right edge 45, height 6. y0 from `0049bd8e` = 16, 23, 32, 40, 49. Font
  selector `0049bd98` = 0,1,0,1,1 into `{ColorSchemePanels[10] WHITE, [14] RED}`. Background
  `COLORS.DAT` id 0x11.
Label text sources, all confirmed by xref:

| Label | Text | Source |
|---|---|---|
| 0 | `ID:` / `TARGET:` / `DIST:  ` | `DAT_004d1570`-`78` = group 20 |
| 1 | Subject name | group 17 `YOU`, or the type-name groups 22-24, 26, 27 |
| 2 | `STATUS:` | `DAT_004d157c` = group 21 |
| 3 | Condition | `DAT_004d1698[state]` = group 28 |
| 4 | Integrity | composed, below |

`MfdStatusScreen_SetCondition` (`0043b260`) writes labels 3 and 4. Label 4 is the literal `"[ "`,
then `itoa((0x100 - damage) * 100 >> 8)`, then `"% ]"` — `[ 100% ]` undamaged. When the subject is
unreadable it writes `XXXXXX` and `XXX` instead.

**Group 10 is not the condition table.** It holds a near-identical five-string set (`OK`, `INT DMG`,
`SHLD DWN`, `CRITICAL`, `WASTED`) and is dead data: `SimStrings_LoadAll` is the only reference to its
array in the image.

Labels 1 and 3 are re-fonted at paint time from IFF and damage state — a friendly name draws green
where the constructor installs red. That override is not traced.

The paper doll blits at `pdgView.origin + viewportTopLeft + (0x11, 2)` device pixels, then per-region
damage tints draw over it at the same origin from the `.PDG` region list. The paint reaches one view
record through the mech type without computing an index; only view 2 fits, views 0 and 1 being 96x162
device against a 102x92 viewport.

### `MFDFlashComm` — mode 1

`MfdFlashCommScreen_Ctor` (`0043f5d8`) builds six order rows.

Row block, device pixels relative to the inset origin: rect `2,0xd – 0x60,0x3a` GAU, top-left nudged
in by `1 << XCoordShift` and bottom-right out by the same, giving x 6-190 and y0 28. Rows step
`7 << YCoordShift` = 14 device. Both nudges use `XCoordShift` on the y axis — no effect in any retail
video mode.

Font `ColorSchemePanels[1]` `CPGREEN`, alternate `[2]` `CPRED` at `+0x21` for orders the squad cannot
take, background id 0x11, text margin `2 << XCoordShift` = 4 device. Text is the first six of the 18
squadmate orders.

`MfdDisplay_Update` draws the transmitting pilot's frames at `inset + (0x14, 0)` GAU.

### `MFDMap` — mode 2

`MfdMapScreen_Ctor` (`00440494`) takes the whole inset rect, allocates a 0x239-byte offscreen render
target and centres it at `-((x1 - x0) >> 1)`, `-((y1 - y0) >> 1)`. No labels, no aux buttons.

Its paint (`004405e4`) floods the rect with `COLORS.DAT` id 19 (palette 16, black) before rasterizing
terrain, which is why no screen chrome is blitted for this mode.

## Paint order

`MfdDisplay_Repaint`: mode buttons 0-5, background, all visible buttons 0-12, the current screen's
paint, then the title. The background covers only the inset rect and the mode column sits left of it,
so the first pass is not overdrawn.

## Engine coverage

Layout and switching only. Drawn: screen background, F-key column with lit state, per-mode aux
buttons, titles and captions, the status labels and paper doll, the flash-comm order list, the nav
map's background flood. Not drawn: radar sweep, target data, missile camera, map terrain, per-region
damage tints, transmission frames — all need sim state or a map rasterizer.

`Herculan.Engine.Host` takes `--mfd <0-5>` to pick the initial screen, since a `--screenshot` run
never sees a keystroke.

## Open

- `mfd_dmg` (7 frames, 192x118) is built into three animation sequences of 3/2/3 frames by
  `MfdDisplay_Ctor` from count table `0049cb40` and six frame-index tables at `0049cb4c`-`0049cb88`.
  Trigger and meaning not traced; consistent with display-damage static.
- `MFD` frames 11-13 (182x16), 14 (110x110), 15 (51x50), 16 (10x10), 17 (6x54), 18 (14x7) have no
  located consumer. 14 duplicates the `radar` bank's frame size.
- The paint-time font override on status labels 1 and 3.
- Screens for modes 3, 4 and 5 beyond their button and background layout.
