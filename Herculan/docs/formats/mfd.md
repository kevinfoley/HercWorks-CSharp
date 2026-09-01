# The Multi-Function Display

Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project. Addresses are DBSIM. Symbols
are in `tools/ghidra_scripts/known_symbols.json`; apply with `ES2ApplySymbolNames.java`.

The console screen the F1-F6 keys switch between six screens. Surrounding cockpit:
[`cockpit-hud.md`](cockpit-hud.md). Caption text: [`str-strings.md`](str-strings.md).

Engine implementation: `Herculan.Engine.Content.{MfdLayout, MfdMode, SimStringTable}`,
`Herculan.Engine.Render.Overlay2DRenderer.AddMfd`.

How a click on one of the buttons below reaches `MfdButton_OnClick`:
[`cockpit-input.md`](cockpit-input.md).

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
| `MfdButton_SetCaption` | `00447358` | Repaint for the **momentary** button class (indices 7-10): frame from the shared press byte `+0x1b`, caption `"F%d"` for indices < 6 and the caption table otherwise, never re-fonted. |
| `MfdButton_Repaint` | `004474e4` | Repaint for the **latching** button class (indices 0-5, 11-12): frame and `DARK`/`WHITE` caption both from the button's own `+0x40` selection flag, never from the press byte. |

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

### Two button classes

`MfdDisplay_Ctor` switches on the button index and builds its 13 buttons through **two different
classes**, which is why some MFD buttons show a pressed state and others do not:

| Class | Ctor | Indices | Repaint | Frame comes from |
|---|---|---|---|---|
| Latching | `0044741c` | 0-5, 11, 12 | `MfdButton_Repaint` (`004474e4`) | its own selection flag `+0x40` |
| Momentary | `004472e4` | 7, 8, 9, 10 | `MfdButton_SetCaption` (`00447358`) | the shared press byte `+0x1b` |

So the F-key column and the two scanner toggles (PASS, ACTIVE) **have no pressed state at all** —
blue when unselected, green when selected — while SELECT, RANGE, TARGET and XMIT light *only* while
held and have no selected state.

`MfdButton_Repaint`'s caption re-font test, `index < 6 || index - 0xb < 2`, covers exactly the
latching class's indices: it is a class invariant restated, not a rule of its own. Only latching
buttons ever re-font, which is why holding SELECT does not darken its caption.

Index 6 has **no case in the switch** and so takes whichever class the previous iteration left on the
stack. It is the degenerate zero rect no mode shows.

Per-button fields, base a button pointer from `+0x18`:

| Offset | Contents |
|---|---|
| `+0x28` | Button index 0-12 — what both the ctor switch and the caption re-font test key on |
| `+0x2c` | Caption label |
| `+0x30` | Two sprite pointers, unlit then lit |
| `+0x40` | Selection flag, **latching class only**. Set by the button's click handler (`FUN_004474a8`), never by a press — the press path sets the shared widget state byte `+0x1b` instead (see [`cockpit-input.md`](cockpit-input.md) §7) |

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

`MfdButton_OnClick` gives **7 `SELECT` and 9 `TARGET` one shared case**, which branches on the
current mode: mode 0 steps the status screen's own subject cursor (`+0x318`, a squad roster), every
other mode calls `TargetSelect_Cycle`. So F5's SELECT and F4's TARGET are the same action, and both
do what [Enter] does.

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
anchorX = flags & 2 ? ((x1 - x0) >> 1) + x0 + margin[0]  -- centre
        : flags & 4 ? x1 - margin[0]                     -- right
        :             x0 + margin[0]                     -- left
anchorY = ((y1 - y0) >> 1) + y0 + (inkHeight >> 1) + margin[2] + 1

width   = measure(text) - (1 << XCoordShift)
textX   = flags & 2 ? anchorX - (width >> 1) : flags & 4 ? anchorX - width : anchorX
textY   = anchorY - inkHeight
```

There is no vertical alignment flag: every label is vertically centred in its rect. Retail uses
alignment 1 (left) for the title, the status labels and the flash-comm rows, and 2 (centre) for
button captions. All margins are zero except the flash-comm rows'.

`inkHeight` is the font's own `0x1a` header field (11 for `.HFN`), **not** its cell height (13) — see
[`dfn-hfn-dci.md`](dfn-hfn-dci.md), "`inkHeight` and label placement". All the arithmetic is integer,
both shifts included; doing it in floating point shifts a label up to a pixel on either axis.

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
| 4 | Integrity or range | composed, below |

`MfdStatusScreen_SetCondition` (`0043b260`) writes labels 3 and 4. Label 4 is the literal `"[ "`,
then `itoa((0x100 - damage) * 100 >> 8)`, then `"% ]"` — `[ 100% ]` undamaged. When the subject is
unreadable it writes `XXXXXX` and `XXX` instead.

**Group 10 is not the condition table.** It holds a near-identical five-string set (`OK`, `INT DMG`,
`SHLD DWN`, `CRITICAL`, `WASTED`) and is dead data: `SimStrings_LoadAll` is the only reference to its
array in the image.

#### The subject

`MfdDisplay_Update` (`00446328`) and `MfdDisplay_SetMode` both park the screen's subject in the
display's shared state block at `+0xb9`, refreshed every 30 coarse ticks: for mode 0 the entry
`+0x308[+0x318]` the SELECT button cycles, for mode 4 `CockpitView+0x210`, the current selection.
Both screens read the same field, so **the two modes differ only in their subject**. The screen
latches it at `+0x3e` and holds a dead one for 300 ticks before dropping to the empty state.

Everything the paint (`FUN_0043a5a0`) chooses is a property of that subject, not of the mode:

| Test | Effect |
|---|---|
| Subject is the viewing object (`CockpitView+0x203`) or one of the three squadmates (`FUN_00433134`) | Label 0 is `ID:` and label 1 the pilot's name — `YOU` for the machine being flown; otherwise `TARGET:` and the type name |
| Group record's side byte (`obj+0x45` → `+0x12`) | Label 1's font: `ColorSchemePanels[1]` `CPGREEN` for a friendly, `[2]` `CPRED` for a Cybrid. **This is the paint-time override**; the constructor's own `RED` for that label is never used |
| Same byte | A friendly gets the integrity readout in label 4; a hostile gets group 20 entry 2 `DIST:  ` with the range appended (`FUN_00492780` between the two origins) |
| Target class `obj+0x1a8` | Which branch below draws the viewport, and how the condition is worked out |

With no subject at all the paint writes `TARGET:` and group 26 `NONE` in `ColorSchemePanels[0]`
`CPBLUE`, and blanks labels 2-4. A class the switch does not recognise gets `TARGET:` and group 27
`UNKNOWN` in the same font, with labels 2-4 left as they were.

#### Viewport and condition, per class

| Class | Viewport | Condition |
|---|---|---|
| 0 HERC | The type's paper doll, `pdgView.origin + viewportTopLeft + (0x11, 2)` device, then per-region damage tints from the `.PDG` region list at the same origin. The paint reaches one view record through the mech type without computing an index; only view 2 fits, views 0 and 1 being 96x162 device against a 102x92 viewport | Scanned: `DESTROYED` if `obj+0x99`; else `CRITICAL` when all twelve dependent readings from `FUN_004151a4` are `>= 0x81`, `INT DAMAGE` when any is non-zero; else `SHIELDS DN` if `mech+0xb0` (the shields-down alert latch, which only the player's own machine ever sets), else `OK` |
| 2 flyer | `flyers` bank frame 0, centred in the viewport by its own frame size | `FUN_00438700(damage)`: intact ≥ 90% `OK`, ≥ 74% `SHIELDS DN`, ≥ 51% `INT DAMAGE`, ≥ 1% `CRITICAL`, else `DESTROYED` |
| 1, 3 structure | `bases` or `vehicles` bank, frame = the type record's `+0x28`, centred the same way | as above |

Damage is the object's vtable `+0x40` as a Q8 fraction — `FUN_0040db2c` over every component and
dependent for a machine, the component sum for a structure.

`BASES.DAT +0x28` is both the silhouette frame and the type-name index: into group 23's 31 structure
names when `+0x32` is 0, and group 24's four vehicle names when it is not. Confirmed by construction
— every structure type states 0-30 and every vehicle type 0-3.

A bank the game ships only at 320-wide (`flyers` is the one here) is blitted doubled, guarded on
`VideoMode_UseHiResPanels == 3 && VideoMode_UseHiResBanks == 0`.

Also drawn, but unreachable here: with a hostile subject and a component id at `CockpitView+0x27e`,
the paint outlines that component's `.PDG` region in `COLORS.DAT` id 16. The id only ever comes from
a targeting computer pod (`mech+0x30b`), which is not ported.

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

### `MFDRadar` — mode 3

The plan view, its turret wedge and its contact list: [`mfd-scanner.md`](mfd-scanner.md).

## Paint order

`MfdDisplay_Repaint`: mode buttons 0-5, background, all visible buttons 0-12, the current screen's
paint, then the title. The background covers only the inset rect and the mode column sits left of it,
so the first pass is not overdrawn.

## Engine coverage

Drawn: screen background, F-key column with lit state, per-mode aux buttons, titles and captions, the
flash-comm order list, the nav map's background flood, and **both status screens driven from a live
subject** — `Herculan.Engine.Content.MfdStatusSubject`, one record for F1 and F5 as in the original.
The scanner is drawn too — see its own doc. Not drawn: the mode-switch sweep animation, the missile
camera, map terrain, per-region damage tints and transmission frames, all of which need sim state, a
map rasterizer or an animation path.

`Herculan.Engine.Host` takes `--mfd <0-5>` to pick the initial screen and `--target` to acquire one,
since a `--screenshot` run never sees a keystroke.

Status-screen deviations: there is no pilot roster, so only the machine being flown reads `ID:`/`YOU`
and a squadmate reads `TARGET:` plus its type name; a flyer's name comes from `FLYERS.DAT`
`NameBytes` (the same `+0x12` the paint reads) and a HERC's from its type name.

## Open

- `mfd_dmg` (7 frames, 192x118) is built into three animation sequences of 3/2/3 frames by
  `MfdDisplay_Ctor` from count table `0049cb40` and six frame-index tables at `0049cb4c`-`0049cb88`.
  Trigger and meaning not traced; consistent with display-damage static.
- `MFD` frames 11-13 (182x16) have no located consumer. **Frames 14-18 do**: they are the whole of
  the scanner screen, see [`mfd-scanner.md`](mfd-scanner.md). Frame 14 matching the `radar` bank's
  frame size is not a coincidence either — that bank holds the sweep played over the same dish.
- Per-region damage tints on the paper doll. The `.PDG` region list and the per-component readings
  are both decoded; the tint colour comes from `FUN_00438624`, which is not.
- Mode 5, the missile camera, beyond its button and background layout.
