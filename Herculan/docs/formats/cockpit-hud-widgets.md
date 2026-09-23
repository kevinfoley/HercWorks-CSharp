# Cockpit console widgets: HUD sprite banks, the `.GAU` tree, and the gauges

Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project. All addresses are DBSIM unless noted. Symbols are in `tools/ghidra_scripts/known_symbols.json`; apply with `ES2ApplySymbolNames.java`.

Verified against retail data in `ES2/VOL/simvol0/{hba,gau,dat}/`.

Engine implementation: `Herculan.Engine.Content.{HudSpriteSheet, HudFont, HudColorTable, CockpitHudState, WeaponRowState}`, `Herculan.Engine.Render.Overlay2DRenderer`.

The view manager and canvas these widgets are drawn onto: [`cockpit-views.md`](cockpit-views.md). Canopy art and the palette a flash swaps: [`cockpit-canopy-palette.md`](cockpit-canopy-palette.md). The front-window gunsight complex (a separate widget tree): [`cockpit-gunsight-hud.md`](cockpit-gunsight-hud.md). How a mouse click on any of these widgets reaches its own click handler: [`cockpit-input.md`](cockpit-input.md).

## HUD sprite art — `.HBA`/`.DBA`

Every bank ships twice under the same name: `dba\NAME.DBA` for the 320-wide mode and `hba\NAME.HBA` for the 640-wide one, exactly 2x on both axes, frame for frame, with identical frame counts. The two folder-name literals sit adjacent to each bank name in `.rdata` (`"NAME\0hba\0dba\0"`). `corners` is hardcoded to `dba`; `hba\CORNERS.HBA` does not exist.

Load path: `ResourcePath_BuildFolderName(name, folder)` → `Resource_Load` (`0045cdd8`) → `ClassItem_LoadResource`.

| Bank | Owning function | Role |
|---|---|---|
| `hud` | `Gau_RovingGunsightWidget` (`0043c7d8`) | gunsight / reticle — see [`cockpit-gunsight-hud.md`](cockpit-gunsight-hud.md) |
| `hudhtick` | `HudHeadingTape_Ctor` (`0043b57c`) | heading tick tape — see [`cockpit-gunsight-hud.md`](cockpit-gunsight-hud.md#heading-tape) |
| `mfd`, `mfd_dmg`, `radar` | `MfdDisplay_Ctor` (`00445218`) | multi-function display — see [`mfd.md`](mfd.md) |
| `hdd`, `static`, `hddclip`, `pilotN` | `HddDisplay_Ctor` (`00448cc8`), `HddGauge_LoadPilotFrames` (`0044a7c0`) | heads-down display — see [`heads-down-display.md`](heads-down-display.md) |
| `pweapons`, `wpn_dmg` | `WeaponGauge_Ctor` (`0044080c`) | weapon hardpoint plates |
| `throttle` | `ThrottleGauge_Ctor` (`00447b84`) | throttle slider knob |
| `sysbuttn`, `icons`, `corners` | `SystemButtons_Ctor` (`00434368`), `HddMarker_Ctor` (`0044f130`), `maybe_CockpitFontsAndCorners_Init` (`004544a4`) | |

The class names in these symbols (`ThrottleGauge`, `WeaponGauge`, …) are the classes' own, read from their Borland class records ([`borland-rtti.md`](borland-rtti.md)). [`cockpit-input.md`](cockpit-input.md#the-cockpits-own-gadget-classes) has the clickable-widget hierarchy.

Frame-to-state mapping: `PWEAPONS` 0/1 are the selected/unselected row plate, 2/3 the unlit/lit console-button plate, 4/5/6 the hardpoint state box (green / red / amber), 7 a 640x80 strip ([Open](#open)); `WPN_DMG`'s 10 frames are damage fill levels, frame 0 the opaque empty plate; `THROTTLE` 0 is a 2x12 tick and 1 the 28x12 knob; `RADAR`'s 10 110x110 frames are the sweep animation; `MFD` 0-2 are 196x122 screen chrome, 3-10 five button plates in unlit/lit pairs (see [`mfd.md`](mfd.md)); `HUD` 0 is the 45x45 reticle, 11 the 182x10 rotation-indicator track and 12/13 its 62x4 yellow and green bars (sizes in the 640-wide `hba\` banks; `dba\` is exactly half).

**`static` and `pilot<n>` ship in `dba\` only**, so the 640-wide mode has no matching art for them; see [`heads-down-display.md`](heads-down-display.md).

## `.GAU` widget tree

`Gau_Load` (`00431778`, `PANEL.CPP:0x1d6`) reads a `0x6a4`-byte struct and constructs six sub-widget vectors. The file's first two `int32`s are an origin offset added to every widget rect. `Gau_BuildCockpitWidgets` (`00431bf8`) then builds seven top-level widgets from fixed offsets and shifts every rect by `VideoMode_X/YCoordShift`. The order it builds them in is also the cockpit's click precedence — [`cockpit-input.md`](cockpit-input.md#registration-order-is-precedence) has the full sequence.

GAU coordinates are authored in the 320-wide space; the engine's `CockpitArt.GauToPixelScale = 2` maps them onto 640-wide art. See [`cockpit-views.md`](cockpit-views.md#cockpit-canvas) for the y-range question.

## `dat\COLORS.DAT` — logical colour ids

54-byte payload, 27 `int16` palette indices. HUD data files carry a small logical id, resolved once at load time through this table in place (`arr[i] = table[arr[i]]`). The table lives at `HudColorTable` (`004d3c00`) in `.bss`, read at 16 distinct offsets by ~60 functions; no code materialises that address to write it, so it is filled from the file.

Verified: the heads-down display resolves ids 19, 9, 15, 12 → palette 16, 10, 13, 14 — black, red, yellow, green, matching the retail HDD readouts.

**Not every colour number is an id.** The indirection exists for numbers that arrive in a *data file*; a colour a *constructor states as an immediate* is already a palette index and goes nowhere near this table. The weapon panel's raw 32/34/46 (`FUN_00442950`) are the clearest case, and the scanner screen uses both conventions at once: its contact colours are read out of the table at paint time while its screen background is the literal `0x11` its constructor writes — palette 17, matching the dish art's own corner pixels. Reading such an immediate as an id lands on a believable but wrong colour (`0x11` as an id is palette 24, a mid grey).

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

- **Unported:** `WPN_DMG`'s damage fill on a weapon row. The per-mount reading behind it is combined entry `32 + slot` of `Component_FillDamageReadouts`' buffer, which the engine's Heads-Down Display weapons page already prints, but the engine's weapon rows do not carry it. They also draw the row plate as the underlay instead of `WPN_DMG` frame 0, which is equivalent only while the row is undamaged.
- **Open:** what consumes `PWEAPONS` frame 7, a 640x80 strip.
- **Open:** which mech-object field picks each widget's frame or fill level per frame, for the widgets this doc does not already trace. The `.GAU` holds only geometry.
