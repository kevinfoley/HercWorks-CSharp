# Cockpit mouse input

How DBSIM routes a mouse click on the cockpit dashboard/HUD/HDD to a button's own click handler. Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project. All addresses are DBSIM. Symbols are in `tools/ghidra_scripts/known_symbols.json`; apply with `ES2ApplySymbolNames.java`.

Widget geometry, frames and paint logic are covered by [`cockpit-views.md`](cockpit-views.md), [`cockpit-canopy-palette.md`](cockpit-canopy-palette.md), [`cockpit-hud-widgets.md`](cockpit-hud-widgets.md), [`cockpit-gunsight-hud.md`](cockpit-gunsight-hud.md), [`mfd.md`](mfd.md) and [`heads-down-display.md`](heads-down-display.md) — this document is only the input path: how a mouse event becomes a call into a specific widget's own handler.

Implemented in `Herculan.Engine` across three types: `CockpitScreenLayout` (window pixel to art pixel, the step the original does not need), `CockpitWidgets` (the flat clickable list and the rectangular hit test, §5-6) and `CockpitInput` (the queue and the press/release/hold state machine, §3-4 and §7). `Herculan.Engine.Host`'s `Program.cs` queues the events and routes completed clicks. Sections 1-2 and 9 have no counterpart by design: the host's windowing replaces them. Of §10's three screen-edge strips only the vertical one exists, as `CockpitWidgets.VisibleHeadsDownViewEdge` ([Open](#open)). `CockpitInput`'s own summary says what diverges and why.

## Overview

```
Win32 mouse message
  -> Mouse_WndProcHook            (registered into MainWndProc's own filter chain)
  -> Mouse_DispatchEvent          (rescale to game space, fan out to mouse listeners)
  -> CockpitMouse_OnEvent         (the cockpit's one listener; mask = button edges only)
  -> CockpitMouseQueue_Push       (queued, not handled synchronously)
  -> CockpitMouse_ProcessQueue    (drained once per frame)
       -> Widget_HitTestChildren  (linear scan of one flat, cockpit-wide widget list)
       -> Widget_OnMouseDown / Widget_OnMouseUp / Widget_TrackPressedWidget
       -> the hit widget's own OnClick vtable slot
```

Three separate things all matter for a click to land: the OS-message layer never sees widgets at all (§1-2), the cockpit's own click state machine only runs once per frame off a queued event (§3-4), and a widget doesn't need to belong to whatever panel is showing to be hit — hidden panels are excluded by a per-widget state byte, not by not being in the list (§5).

## 1. Win32 messages bypass MainWndProc's own switch

`MainWndProc` (`@MainWndProc$qqspvuiuil`, `00465f30`) has no `WM_MOUSEMOVE`/`WM_LBUTTONDOWN`/etc. case at all. Before its own switch it tries up to 10 registered filter functions in order — `WndProcHook_Register`/`WndProcHook_Unregister` manage that table (`DAT_004d3bb4`) — and any hook returning nonzero short-circuits the rest of `MainWndProc` entirely. `Mouse_WndProcHook` (`004808ec`) is one of four hooks found registered. `sfxWndProc` (`00462294`) is another — see [`audio.md`](audio.md); the remaining two are movie-playback related.

`Mouse_WndProcHook` catches:

| Message | Value | Forwarded as kind |
|---|---|---|
| `WM_MOUSEMOVE` | `0x200` | 0 |
| `WM_LBUTTONDOWN` | `0x201` | 1 |
| `WM_LBUTTONUP` | `0x202` | 2 |
| `WM_RBUTTONDOWN` | `0x204` | 3 |
| `WM_RBUTTONUP` | `0x205` | 4 |

Each call forwards to `Mouse_DispatchEvent(kind, lParam, buttonFlags)`, `buttonFlags` built from wParam's button-held bits.

## 2. A second, independent subscriber list carries it further

`Mouse_DispatchEvent` (`0048083c`) rescales the raw window-client `(x,y)` into the game's internal coordinate space via a fixed-point factor recomputed on resize (`Mouse_RecomputeScale`, `0048078c` — `(gameSize << 0xf) / clientSize`), packs the current L/R held bitmask into `DAT_006c5fdc`, then calls every enabled entry of a **second, independent** 10-slot table (`DAT_006c5f5c`) whose event mask has the event's bit set. `MouseListener_Register`/`MouseListener_Unregister` (`0048073c`/`00480774`) manage that table.

This `{enabled, eventMask, callback}` triple-array shape is not unique to mouse input — DBSIM reuses it for the `WndProcHook` table above and for the keyboard's own subscriber table, `KeyListenerTable` (`006bb93c`, §7). Recognize it as a house idiom rather than three unrelated systems.

## 3. The cockpit's one listener queues, it doesn't act

`CockpitMouse_Init` (`00452abc`) runs once at cockpit setup:

- Registers `CockpitMouse_OnEvent` (`00452cb4`) via `MouseListener_Register` with mask **`0x1e`** — bits 1-4 (button down/up on both buttons). Bit 0, plain movement, is deliberately excluded.
- Allocates a double-buffered event queue: two 100-capacity vectors, swapped each frame by `CockpitMouse_ProcessQueue`.
- Sets `DAT_004d1e70 = 0x1e` — a **click-vs-drag timing gate**, in coarse UI ticks (~16ms each, so ~480ms). This is the same numeric literal as the event mask above but a wholly unrelated field; don't conflate them when reading the disassembly.

`CockpitMouse_OnEvent` doesn't process the click, and drops it entirely unless `CockpitMouseLive` (`004d1e5a`) is set — the gate that hands the queue over to a replaying tape ([`tap-input-tape.md`](tap-input-tape.md#where-it-runs)). Otherwise it pushes a 14-byte record `{int32 x, int32 y, uint16 buttonMask, int32 timestamp}` onto the back-buffer queue via `CockpitMouseQueue_Push` (`00453034`, capped at 99 entries), debounced to at most once per coarse tick. It also calls `Cursor_SyncPosition` (`00486d70`) immediately, independent of the queue, so the drawn pointer tracks the raw event position without waiting for the frame drain (§7).

## 4. Once per frame: the real click/press/drag logic

`CockpitMouse_ProcessQueue` (`00452d18`) swaps the queue buffers and, per queued record:

- **Position changed:** hit-tests via `Widget_HitTestChildren`. If a widget is mouse-captured (dragging), forwards the new position straight to its drag-move vtable slot instead of re-hit-testing. Otherwise calls `Widget_TrackPressedWidget`, which keeps the *held* widget's depressed look in sync with the pointer — see §7. It is not a hover mechanism: **DBSIM has no hover state**.
- **Button-down edge** (bit set now, wasn't last record): calls `Widget_OnMouseDown`.
- **Button-up edge** (bit clear now, was set): only if the release lands within `DAT_004d1e70` ticks of the press — the click-vs-drag gate — calls `Widget_OnMouseUp`. If a drag was in progress instead, ends the capture and fires the captured widget's `GetValue`/`OnClick` pair directly rather than going through `Widget_OnMouseUp`.

## 5. One flat hit-test registry for the whole cockpit

Every clickable widget anywhere in the cockpit — the MFD's 13 buttons, the HDD's 15 widgets, the console buttons, weapon-select gadgets, the two shield-balance facings — is appended to **one shared list**, not organized per-panel. `Widget_RegisterClickable` (`00452c44`) appends a widget pointer to the caller's own `+0x256` array (count at `+0x254`); every top-level cockpit widget's constructor calls it once per child with the *dereferenced* `CockpitViewInstance` pointer as the shared root. Confirmed identically in `MfdDisplay_Ctor`'s 13-button loop and `ShieldsGauge_Ctor`'s 2-facing loop.

`Widget_HitTestChildren` (`00452a00`) linear-scans that whole list with `Widget_HitTest` and returns the index of the **first** entry the point falls in whose state byte (`+0x1b`) is not `2`. This is how an off-screen panel's buttons don't intercept clicks: nothing removes them from the list when their panel isn't showing, only that one state byte changes. Every such change in the image is a direct store; the two library helpers for it, `Widget_Show` and `Widget_Hide` (`00452c64`/`00452c8c`), have no call site, no vtable slot and no stored pointer anywhere in the PE.

### Registration order is precedence

First-hit-wins makes the order the list is built in the whole of the tie-break: where two visible rects contest a pixel, the widget registered earlier takes the click. `Gau_BuildCockpitWidgets` (`00431bf8`) fixes that order, and it is not the order the panels are drawn in:

| # | Registered by | Widgets |
|---|---|---|
| 1 | `SystemButtons_Ctor` (`00434368`) | The two `SystemGadget`s below |
| 2 | `ConsoleButtons_Ctor` (`00441dd0`) | Four `WeaponRangeSelectGadget`s from `.GAU` 484/500/516/532 — the chain selector, LINK, TRACK, and a fourth whose rect is zero in every retail file |
| 3 | `EnergyPoolGauge_Ctor` (`00444d5c`) | none — its LED bar is not clickable |
| 4 | `ShieldsGauge_Ctor` (`004434fc`) | The two facings, front then rear (§8) |
| 5 | `MfdDisplay_Ctor` (`00445218`) | Buttons 0-12 in index order, then the `MFDListGadget` over the screen area |
| 6 | `ThrottleGauge_Ctor` (`00447b84`) | The slider (§7) |
| 7 | `Gau_RovingGunsightWidget` (`0043c7d8`) | The `HUDRovingGunsightGadget` click surface over the 3D window |
| 8 | `HddDisplay_Ctor` (`00448cc8`) | `HddCommandScreen_Ctor`'s order-column list then its map list, **then** the display's own 15 buttons — both page objects are built before the buttons, and the damage detail registers nothing |
| 9 | `WeaponMounts_BuildGauges` (`00410644`) | Per mount, in mount order: the row's select gadget, then an energy row's charge bar |
| 10 | `CockpitView_BuildScrollTriggers` (`00433770`) | The three edge strips (§10), on the first cockpit frame |

Step 9 is the closing call of `Gau_BuildCockpitWidgets`: it walks the machine's weapon-mount array and dispatches each mount's own gauge-factory slot (`+0x64`), so a row's clickables are registered by the mount rather than by the cockpit. An energy row registers **two** — `ChainedWeaponSelectGadget` first and `WeaponChargeBar` second — which is why the select gadget takes the click where the charge bar overlaps it.

### Where retail rects overlap

Four places, measured across all nine retail cockpits. First-hit-wins resolves each in favour of the earlier registration:

| Contested | Extent | Taken by |
|---|---|---|
| FLASH COMM row *n* against *n+1* | the shared bottom line, every herc | the upper row ([`mfd.md`](mfd.md#mfdflashcomm--mode-1)) |
| HDD `XMIT` against `CANCEL` | 2 `.GAU` units, every herc | `XMIT`, widget 13 |
| HDD up/down arrow against left/right | a 3x3 device corner, the six hercs on arrow set 0 | the up/down arrow, widgets 2-3 ([`heads-down-display.md`](heads-down-display.md#widgets)) |
| The bottom edge strip against a console instrument | MAVERICK's `[F6]`, RAPTOR2's throttle, RAZOR's `TRACK` | the instrument (§10) |

MFD buttons 7 and 10 share a rect but never contest it: no mode shows both ([`mfd.md`](mfd.md#button-visibility)).

### The two system buttons

`SystemButtons_Ctor` (`00434368`) builds a pair of `SystemGadget`s from hardcoded coordinates rather than from the `.GAU` — 12x11 GAU units each, at x 305-317 and x 291-303, y 2-13, so they sit in the forward view's top-right corner. Their art is the `sysbuttn` bank, and the constructor leaves them in state **3**, which `Widget_HitTestChildren` treats as clickable and `SystemGadget_Paint` (`00434748`) draws as the plain frame.

`SystemButtons_OnChildClick` (`004345a0`) matches the clicked child against the pair at `CockpitViewInstance+0x246`/`+0x24a` and, unless a `.TAP` is replaying:

| Child | Effect |
|---|---|
| 0, the right-hand button | `OnlineManual_Raise` then `Help_Show` — the same two calls `Sim_DispatchCommand` makes for the `?` key, so the button and the key are one path |
| 1, the left-hand button | `Video_ToggleFullscreen` (`004666c4`), then repaints the shield gauge |

`Video_ToggleFullscreen` is a real mode switch, not a window maximize: from windowed it sets `004d25e2`, takes the window topmost at the game resolution, `ClipCursor`s the pointer into it and centres it; from fullscreen it restores the window rect saved on the way in. `Help_Show` calls it first when that flag is set, so raising the manual drops the game out of fullscreen.

Neither overlaps a widget the engine has ([Open](#open)).

Widget state byte (`+0x1b`):

| Value | Meaning |
|---|---|
| 0 | Normal |
| 1 | Lit — either held down, or selected (a mode button's current screen). Not hover; there is none |
| 2 | Excluded from hit-testing by `Widget_HitTestChildren`, whatever the class does about drawing |
| 3 | A fourth state, still hit-testable. The alert family's `PanelButton` rests a preferences or controls option row in it, and `SystemGadget_Ctor` leaves both system buttons in it |

`Widget_NotifySelfAndChildren` (`00452a48`) walks that same list — vtable slot 0 on the owner, then on each registered child — but nothing reaches it: its one caller (`00452bac`) has no rel32 branch, no stored pointer and no vtable slot anywhere in the image. The cascades the cockpit actually runs are per class (§7).

**What state 2 does to drawing is per class.** The cockpit widget classes traced here refuse to paint in it, which is how an off-screen panel's buttons stay invisible. `PanelButton_Paint` (`00454ff8`) has no state test at all — it indexes a four-entry plate-frame table at `+0x30` and a four-entry caption-font table at `+0x40` with the state — so a state-2 panel button draws its third frame in `INACTIVE`, which is what a greyed controls row looks like ([`../simulation/preferences.md`](../simulation/preferences.md#the-capability-block)).

## 6. Hit test: rectangle or circle

`Widget_HitTest` (`00452388`) isn't purely rectangular. A per-widget flag at `+0x10` selects the shape:

| Flag | Shape | Fields |
|---|---|---|
| 0 | Axis-aligned rect, inclusive | `+0x0`/`+0x4`/`+0x8`/`+0xc` = x0,y0,x1,y1 |
| nonzero | Circular/diamond | centre `+0x11`/`+0x13` (int16 cx,cy), radius `+0x15` (int16); test is Manhattan distance ≤ radius, not true Euclidean |

**The point it is given is in cockpit-canvas space, not screen space.** `Widget_OnMouseDown` and `Widget_OnMouseUp` subtract the root widget's own rect origin from the event position first, and that origin is moved by the view-change delta on every view change, so the same widget rect answers a different part of the screen in each view — see §10, where it is the whole of how one edge strip serves two opposite edges. The two also add `DAT_004d25da`/`de` under a flag, but that path is dead: [`cockpit-views.md`](cockpit-views.md#video-modes) shows the mode byte that would write those globals can never be set, so the term is always zero.

**Nothing in DBSIM ever selects the second form.** `Widget_CtorRect` (`00452478`) writes `+0x10 = 0`, and all sixteen leaf-widget constructors run it; the only other widget-rect setter in the image (`004526c4`, which `Gau_BuildCockpitWidgets` and `AlertPanel_CtorBase` use) writes 0 too. Nothing writes that byte again, so the circular branch is library code the game does not reach.

## 7. Press, release, click vs. drag

`Widget_OnMouseDown` (`004527a0`): on hit, stores the hit index in `Widget_PressedIndex` (`0049dbdc`) — one global for the whole cockpit — sets the widget's state to `1` and repaints it. If the widget's own `+0x1d` flag is `1`, begins mouse capture (`DAT_0049dbde=1`) and forwards the position to its drag-move vtable slot (`+0x18`) immediately.

**One retail widget does use capture: the throttle slider.** Every button class leaves `+0x1d` clear, but the shared slider base `SliderWidget_CtorBase` (`004524a8`) sets it unconditionally, and the throttle's vertical slider child (`00447e24`) is built through it. It is why the manual's "set throttle with the mouse by clicking on the slide and dragging it up or down" works, and why clicking anywhere on the track jumps the knob there — the press itself dispatches the drag handler.

While capture is held, `CockpitMouse_ProcessQueue` takes a different branch on every position change: it dispatches `+0x18` on the captured widget with the pointer position and repaints it, **without hit-testing** — so a drag follows the pointer off the widget, off the panel and off the window. `Widget_TrackPressedWidget` is not called at all in that branch, so a captured widget stays depressed however far the pointer wanders.

Release under capture also takes its own branch: clear the state byte, repaint, clear `DAT_0049dbde`, then read the widget's value (`+0x10`) and commit it (`+8`), and clear `Widget_PressedIndex`. `Widget_OnMouseUp` is never reached, so **a drag fires no click** — including a press-and-release that never moved.

`Widget_TrackPressedWidget` (`00452954`): called on every position change *outside* capture, and a no-op unless `Widget_PressedIndex` is valid. It re-hit-tests and compares against that index: still on the held widget and its state is `0`, set it to `1` and repaint; anywhere else and its state is `1`, clear to `0` and repaint. That is a button popping back up when you drag off it and depressing again when you come back, and it is the *only* thing this function does.

**There is no hover state anywhere in DBSIM.** Beware any symbol set that still names `00452954` `Widget_OnMouseHover` — that reading of this toggle is wrong. Two independent facts rule hover out: the <!-- doc-lint: ok --> `Widget_PressedIndex != -1` guard means the function cannot run unless a button is held, and `CockpitMouse_Init`'s event mask (`0x1e`, §3) never subscribes to plain movement in the first place, so nothing would drive a hover highlight even if the code wanted one.

`Widget_OnMouseUp` (`00452870`): if the release lands back on the widget that was pressed **or the release was a right-button one**, calls that widget's `GetValue` vtable slot, then its `OnClick` slot with that value, then clears the pressed state and repaints via `Widget_Repaint` (`00452a90`).

**Only the left button presses.** `CockpitMouse_ProcessQueue` calls `Widget_OnMouseDown` for a left press and not a right one, so a right click never arms a widget — the release's own re-hit-test plus that second condition is the whole of what makes it work. The two buttons are therefore not symmetric: a left press dragged off its widget fires nothing, while a right press fires on whatever the release happens to be over, and a right press lights nothing on the way down because no widget was ever marked pressed.

### The leaf-widget vtable

Every widget in the clickable list carries its vtable at `+0x17` — the offset its own class descriptor states — inherited down from one base and overridden a slot at a time. The slot numbers are fixed by the three functions that call them — `Widget_OnMouseUp` reads `+0x10` then calls `+8`, `Widget_OnMouseDown` calls `+0x18`, `Widget_Repaint` calls `+4` — so no class is free to move them. **A button's table is seven slots and a slider's is eight**, and that extra `+0x1c` is the one place the family's shape is not uniform.

| Slot | Role | Base implementation (`0049dbb6`) |
|---|---|---|
| `+0x00` | Paint: redraw the widget's own content | `00452ab2`, empty |
| `+0x04` | Invalidate | `00452ab7`, empty; every cockpit button class takes `Widget_InvalidateDeferred` (`00455524`) |
| `+0x08` | `OnClick`, or commit a value | `0045555e`, empty |
| `+0x0c` | Per-frame tick: runs the deferred paint | `0045554a`, empty |
| `+0x10` | `GetValue` | `Widget_GetButtonValue` (`00455530`) |
| `+0x14` | `SetValue` | `0045553b`, empty |
| `+0x18` | Drag-move | `00455540`, empty |
| `+0x1c` | Recompute scale — sliders only, absent from the base table | — |

**`OnClick` is therefore always `+8`**, for the throttle slider and the ordinary MFD/HDD leaf buttons as much as for `ConsoleButton` (`FUN_00442dc8`) and `WeaponSelectGadget` (`FUN_00442458`). What varies is which implementation sits there:

- Most leaves inherit `Widget_ForwardClickToOwner` (`00438e3c`) unchanged from the intermediate class at vtable `0049dee4` — gated on the left-button bit, it calls `owner->vtable[0](owner, self, buttonFlags)` through the owner pointer its constructor stored at `+0x24`. That is the same function the shield facings use (§8) and the same one the MFD's momentary button class gets; it is a base-class default, not a per-class handler.
- The MFD's latching button class and `HddButton` override `+8` to flip their own `+0x40` lit flag first and refuse a second press while lit ([`mfd.md`](mfd.md#two-button-classes)).
- The throttle slider overrides it with `ThrottleSlider_OnValue`, along with `+0x10`/`+0x14`/`+0x18`/`+0x1c` for the real slider value and drag.

An **owning** display object is a different class altogether, with its own shorter vtable — stored at offset 0 rather than `+0x17`, and headed by the click handler, which is why a forwarded click lands at *its* slot 0. `ShieldsGauge`'s is `0049ca1d`, four slots of `{OnClick, Paint, Update, 00452344}`; `MfdDisplay`'s is `0049cfa0`, five. Those objects are not in the clickable list and never see `Widget_Repaint`.

### The second vtable

A concrete widget carries a **second** vtable pointer, because it has a second base: the family is multiply inherited, and every gadget is `CTLButtonControl` (or `CTLHSlider`/`CTLVSlider`) **plus `PanelGadget`**. The class records name that mixin outright — `PanelGadget` is 8 bytes with its vptr at `+0x00` rather than `+0x17`, and `PanelSliderGadget` derives from it. Both tables live in the class's one vtable block ([`borland-rtti.md`](borland-rtti.md#vtable-block)): the primary table above, 7 slots for a button class and 8 for a slider, then the pair of constants — the `PanelGadget` subobject's offset, and the distance to the mixin's table, `0x24` for buttons and `0x28` for sliders — then the mixin's 4-slot table.

**The mixin adds no behaviour.** Three of its four slots are adjustor thunks — `ADD dword ptr [ESP+4], -<subobject offset>; JMP <primary implementation>` — and the fourth is the click sound:

| Slot | Contents |
|---|---|
| `+0x00` | Thunk onto primary `+0x00`, Paint |
| `+0x04` | Thunk onto primary `+0x0c`, the per-frame tick |
| `+0x08` | `Widget_ClickSound` (`00438e2c`), called with the subobject pointer, which it ignores |
| `+0x0c` | Thunk onto primary `+0x08`, `OnClick` |

The button family puts that subobject at `+0x20` — the `-0x20` its thunks subtract, and the `+0x20` §8 reaches the click sound through. **The slider family differs twice**: its subobject is at `+0x3e`, so its thunks subtract `0x3e`, and its sound slot holds `00439014`, an empty stub. That is `PanelSliderGadget`'s one and only change to what it inherits: the console click is declared once, in `PanelGadget`'s own table (`0049dec8`), and unsaid once, in `PanelSliderGadget`'s (`0049df4c`).

So a control's sound is decided by which mixin it carries, and **a class that carries neither is silent for want of the base rather than for want of an override**: `ScrollTrigger`, `HDDisplayGadget`, `HDDMapGadget` and `HUDRovingGunsightGadget` have no second table at all, their blocks ending at the primary table's last slot. Fifteen tables hold `Widget_ClickSound` — `PanelGadget`'s and the fourteen button classes that inherit it — and `known_vtables.json` names the class each one belongs to ([`audio.md`](audio.md#sounds-a-cockpit-control-makes)).

Because every class record names its class and links to its vtable ([`borland-rtti.md`](borland-rtti.md)), the family is enumerable rather than discovered a control at a time, and `tools/ghidra_scripts/known_vtables.json` carries the result: `CockpitWidgetVtable`, `CockpitSliderWidgetVtable` and `PanelGadgetMixinVtable`, with all 51 tables named and typed by `ES2ApplyVtables.java`.

**The word after a button class's last slot is not an eighth slot.** Four of the button tables (`CTLButtonControl`, `HDDisplayGadget`, `HDDMapGadget`, `HUDRovingGunsightGadget`) are followed by the next block's record pointer where an over-long reading would put one. Where a block has the mixin pair, it states the primary length itself: `0x24` gives 7 slots and `0x28` gives 8. **VSHELL's own `CTL` family has no pair anywhere**: its widgets are singly inherited.

### The cockpit's own gadget classes

Every clickable cockpit widget is one leaf of a single hierarchy rooted at `CTLBox`:

```
CTLBox -> CTLControl -> CTLButtonControl -> PanelSelectGadget -> PanelStateGadget
                     |                   -> PanelListGadget
                     |                   -> HDDisplayGadget -> HDDMapGadget
                     |                   -> HUDRovingGunsightGadget
                     |                   -> ScrollTrigger
                     -> CTLSliderControl -> CTLHSlider -> PanelHSliderGadget
                                         -> CTLVSlider -> PanelVSliderGadget

PanelGadget -> PanelSliderGadget                    (the second base, mixed in at the leaves)
```

The four classes hanging straight off `CTLButtonControl` are the ones that take no `PanelGadget`: they are click surfaces rather than buttons, and they are the silent ones.

| Class | Size | Base | Constructor | What it is |
|---|---|---|---|---|
| `SystemGadget` | `0x60` | `PanelSelectGadget` | `SystemGadget_Ctor` (`00434664`) | The online-manual and fullscreen-toggle buttons in the forward view's top-right corner (§5) |
| `ScrollTrigger` | `0x24` | `CTLButtonControl` | `CockpitView_BuildScrollTriggers` (`00433770`) | One of the three screen-edge view strips (§10). Silent — no `PanelGadget` |
| `WeaponSelectGadget` | `0x46` | `PanelSelectGadget` | `WeaponSelectGadget_Ctor` (`004421dc`) | A pod row — the class without chain membership, which only `PodGauge_Ctor` builds ([`../simulation/equipment-pods.md`](../simulation/equipment-pods.md)) |
| `ChainedWeaponSelectGadget` | `0x67` | `WeaponSelectGadget` | `ChainedWeaponSelectGadget_Ctor` (`00442488`) | A weapon row, from `EnergyWeaponGauge_Ctor` and `AmmoWeaponGauge_Ctor` ([`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md#arming-chaining-and-linking)) |
| `WeaponRangeSelectGadget` | `0x41` | `PanelStateGadget` | `WeaponRangeSelectGadget_Ctor` (`00442c00`) | The weapon-range gauge's button |
| `WeaponSliderGadget` | `0x7e` | `PanelHSliderGadget` | `WeaponSliderGadget_Ctor` (`00442956`) | Unreferenced — no retail gauge builds one |
| `ShieldsSelectGadget` | `0x40` | `PanelSelectGadget` | `ShieldsGauge_FacingCtor` (`00444aec`) | A shield facing (§8) |
| `MFDSelectGadget` | `0x40` | `PanelSelectGadget` | `MFDSelectGadget_Ctor` (`004472e4`) | An MFD momentary button |
| `MFDStateGadget` | `0x41` | `PanelStateGadget` | `MFDStateGadget_Ctor` (`0044741c`) | An MFD latching button ([`mfd.md`](mfd.md#two-button-classes)) |
| `MFDListGadget` | `0x28` | `PanelListGadget` | `MFDListGadget_Ctor` (`004475d4`) | The MFD's screen-content click region |
| `HDDSelectGadget` | `0x41` | `PanelStateGadget` | `HddButton_Ctor` (`0044baac`) | A Heads-Down Display button |
| `HDDisplayGadget` | `0x24` | `CTLButtonControl` | `HDDisplayGadget_Ctor` (`0044be94`) | |
| `HDDMapGadget` | `0x5b` | `HDDisplayGadget` | `HddMarker_Ctor` (`0044f130`) | The F7 map's clickable surface ([`heads-down-display.md`](heads-down-display.md)) |
| `HDDListGadget` | `0x2c` | `PanelListGadget` | `HDDListGadget_Ctor` (`0044f650`) | The command screen's two order lists |
| `ThrottleVSliderGadget` | `0x86` | `PanelVSliderGadget` | `ThrottleSlider_CtorV` (`00447e24`) | The throttle slider all nine retail `.GAU`s use (§7) |
| `ThrottleHSliderGadget` | `0x86` | `PanelHSliderGadget` | `ThrottleSlider_CtorFixed` (`004483c0`) | Its horizontal twin, never exercised by retail data |
| `HUDRovingGunsightGadget` | `0x20` | `CTLButtonControl` | `Gunsight_ClickSurface_Ctor` (`0043c120`) | The click surface over the 3D view ([`hud-target-indicator.md`](hud-target-indicator.md)) |
| `AlertSelectGadget` | `0x50` | `PanelSelectGadget` | `PanelButton_Ctor` (`00454f64`) | An alert-panel option row ([`../simulation/preferences.md`](../simulation/preferences.md)) |
| `AlertSliderGadget` | `0x7e` | `PanelHSliderGadget` | `AlertSliderGadget_Ctor` (`004550b0`) | An alert-panel slider |

### Painting is deferred two frames

`Widget_Repaint` does not paint. It calls slot `+4`, and for every cockpit button class that slot is `Widget_InvalidateDeferred` (`00455524`), whose whole body is `this->[0x1f] = 2` — a counter, not a draw. Slot `+0x0c`, called once per frame down the widget tree, is what acts on it:

| `+0x1f` | What slot `+0x0c` does |
|---|---|
| 2 | Calls slot 0 — the widget's own content refresh, re-resolving its caption and picking its frame and colour — then decrements |
| 1 | Calls `Widget_DrawToCockpit` (`0043122c`), then decrements. That call copies the widget's rect to the other display page on the `-b` paged path and returns at once otherwise ([`cockpit-views.md`](cockpit-views.md#the--b-paged-path)), so on a retail launch stage 2 paints the widget and stage 1 does nothing |
| 0 | Nothing |

A class with no content to rebuild skips the first stage: `ShieldFacing_FlushDeferredPaint` (`00444b70`) decrements at 2 without calling slot 0, and blits at 1.

The tree walk is per class rather than generic — each composite gauge implements its own "slot 0 on all my children" and "slot `+0x0c` on all my children" pair (`00442058` and `0044207c` for the console panel).

### The click value carries the mouse button

`GetValue`'s default implementation (`00455530`) returns the global button word `0049db6c`, which `CockpitMouse_ProcessQueue` sets to `buttons | 1` on a left release and `buttons | 2` on a right one. A widget whose class does not override the slot therefore receives **which button clicked it** as its "value", and several branch on it: a weapon panel row arms its mount on bit 0 and toggles the mount's fire-chain membership on bit 1 (see [`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md#arming-chaining-and-linking)). Sliders override the slot and return a real value instead (`SliderWidget_GetValueV`).

### Keyboard commands are scancodes

The same handlers are reachable from the keyboard, and the command codes that travel through `Sim_DispatchCommand` to the widget tree (`CockpitWidgets_HandleCommand`) are **PC set-1 scancodes**, with `0x200` added for `[Alt]` and `0x400` for `[Ctrl]` — `0x26` is `L`, `0x29` is `` ` ``, `0x11`/`0x211` are `W`/`Alt+W`, `0x1a`/`0x1b` are `[`/`]`, `0x3b`–`0x40` are `F1`–`F6`, and `0x0f` is `Tab` — [`../simulation/target-selection.md`](../simulation/target-selection.md#component-targeting--the-targeting-pod). Codes `0x02`–`0x0b` (the number row) index the cockpit's own ten weapon gauges at `CockpitViewInstance+0x70` and, per gauge, call `WeaponMounts_SelectByGauge` **and then** press its select gadget with the left-button bit — which is how a key and a click end up in one handler rather than two, and why a number key on a pod's row toggles the pod ([`../simulation/equipment-pods.md`](../simulation/equipment-pods.md#only-two-pods-have-a-button)). `[Alt]` and a number is the separate `0x202`–`0x20b` bank, answered by the weapon manager rather than by the gauge.

The `0x400` bank is fixed by the manual: `0x410` raises the `EXIT EARTHSIEGE?` prompt, and the manual's controls page gives that as `[Ctrl]+[Q]` against `0x10` for `[Q]` alone. Every command in the manual's `MISC.` block is decoded, and all of them agree with the dispatcher — [`../simulation/mission-objectives.md`](../simulation/mission-objectives.md#the-pause-panel--004561c0):

| Command | Key | Raises |
|---|---|---|
| `0x19` | `P` | `PAUSE` |
| `0x57` | `F11` | the objectives panel |
| `0x10` | `Q` | the mission-status alert |
| `0x410` | `Ctrl+Q` | `EXIT EARTHSIEGE?` |
| `0x58`, `0x219` | `F12`, `Alt+P` | the preferences panel, `prf_alrt` (`004566c4`) via `PreferencesPanel_Raise` (`0045cfd4`), which pauses the simulation with `DAT_004d2576` while it is up — [`../simulation/preferences.md`](../simulation/preferences.md) |
| `0x35` | `/` | the on-line manual |

**The on-line manual is a Windows help file, not a game screen.** `Sim_DispatchCommand`'s `0x35` case builds `<language>\es2guide.hlp` — `Language_GetFolderName` (`0045efe0`) reads one byte from `data\language.cfg` and answers `ENGLISH`, `FRENCH`, `GERMAN` or `SPANISH`, and the retail CD ships the first three — then calls `WinHelpA(hwnd, path, HELP_CONTENTS, 0)` after dropping the display out of the way. The manual's controls page writes the key as `?`, which is `[Shift]` and `/`; `SimCommandMask` clears the `0x800` Shift bit, so both spellings arrive as `0x35`. The dispatcher also has a `0x835` case jumping to the same handler, which that mask makes unreachable.

### How a keystroke becomes one of those codes

`Key_WndProcHook` (`00477ae0`) sits in the same ten-slot filter table ahead of `MainWndProc` as `Mouse_WndProcHook` (§1), and catches `WM_KEYDOWN`/`WM_KEYUP` (`0x100`/`0x101`) and `WM_SYSKEYDOWN`/`WM_SYSKEYUP` (`0x104`/`0x105`):

1. The virtual key code indexes a 223-entry table at `VkToScancode` (`004a1104`), which is where the set-1 scancodes come from — `VK_F1` → `0x3b`, `VK_F11` → `0x57`, `VK_P` → `0x19`, `VK_OEM_4` → `0x1a`, `VK_TAB` → `0x0f`. A zero entry is an unmapped key and the message is passed on, unless `[Alt]` is down.
2. Modifier bits are OR'd on from `GetKeyState`: **`0x800` for `[Shift]`**, `0x200` for `[Alt]`, `0x400` for `[Ctrl]`. A key-*up* message sets `0x80` as well.
3. `0x20f`, `0x401` and `0x201` are intercepted by `FUN_004668b0` and never become commands.
4. The code is then offered to a ten-slot `{enabled, wantedCodes, callback}` table at `006bb93c` — §2's triple-array idiom again. `wantedCodes` points at a NUL-terminated list of the **low bytes** that subscriber wants; the first callback returning 1 consumes the code.
5. Anything no subscriber claimed falls into a 256-entry ring at `006bb734`, which is what the alert panels read through `Input_ReadDeviceEvent` (`0045c084`).

`SimInput_SetEnabled` registers the simulator's two subscribers:

| Callback | Wanted codes | Role |
|---|---|---|
| `Input_KeyjoyAxisKey` (`0045a308`) | 30 bytes at `0049eb33`: `47 48 49 4b 4d 4f 50 51 32 24 25 17 4a 4e 39`, then the same fifteen with `0x80` set | The keypad, `M`, `J`, `K`, `I` and `Space` held as axes, into the three 15-byte key-state blocks at `004d2418` |
| `SimCommandQueue_Push` (`0045a47c`) | 7 bytes at `0049eb5d`: `1c 0c 0d 1a 1b 4e 4a` | `Enter`, `-`, `=`, `[`, `]`, keypad `+` and `-` — appended to the command queue at `004d2148` |

`Input_BuildPlayerDevice` (`0045a7f4`) drains that queue once per frame, masking each code with `0049eae0` = **`0x47ff`** before handing it to `Sim_DispatchCommand` and then resetting the count. That mask is why there is no `[Shift]` bank: bit `0x800` is discarded, folding a shifted key onto the plain one. Everything not on those two lists reaches `Sim_DispatchCommand` as the single command word at the head of the player input block, which `Sim_PollPlayerInput` (`00460764`) dispatches first thing each frame.

That same function records and replays both queues to a `.TAP` input tape — the mouse queue of §3 and this command queue are the two halves of a frame's record. See [`tap-input-tape.md`](tap-input-tape.md).

## 8. Worked example: the shield-balance rocker

Traced end to end, as a concrete check of the whole pipeline above:

1. `ShieldsGauge_Ctor` builds two facing children via `ShieldsGauge_FacingCtor` ([`cockpit-hud-widgets.md`](cockpit-hud-widgets.md#shieldsgauge)), registers each with `Widget_RegisterClickable`, and stores each child's pointer plus a count into its own `+0x18`/`+0x68` array — the same shape `MfdDisplay_Ctor` uses for its 13 buttons.
2. A click hits `Widget_ForwardClickToOwner` (`00438e3c`) — the facing's `+8` slot, and the base-class default the MFD and HDD leaf buttons share — via `Widget_OnMouseUp`. Gated on the left button bit; forwards to the owner (a pointer stashed at the facing's own `+0x24`, set to the parent `ShieldsGauge` at construction) as `owner->vtable[0](owner, self, buttonFlags)`. It then repaints itself and calls slot `+8` of its second vtable at `+0x20`, which is `Widget_ClickSound` in every class that carries a `PanelGadget` — so the rocker sounds `0x11` before anything has been decided by the click (see [`audio.md`](audio.md#sounds-a-cockpit-control-makes)).
3. `ShieldsGauge`'s vtable slot 0 is `ShieldsGauge_OnClick` (`0044380c`) — structurally identical to `MfdButton_OnClick`: searches its own `+0x18` table for the clicked child, then sets a state byte: index 0 (front) → `+0xc2=1`, index 1 (rear) → `+0xc3=1`.
4. `Shield_BalanceInputRead` (`00413bc8`, called once per frame from `Player_PerFrameCockpitUpdate` — gameplay, not paint) reads those same two bytes (part of a 15-byte block starting at `+0xb5`, accessed via `ShieldsGauge_GetStateBlock`), calls `Shield_BalanceAdjust` (±102 of 1024, clamped) accordingly, clears the flags, recomputes front/rear percentages, and writes the block back via `ShieldsGauge_SetStateBlock` (`00443858`) — which also sets a dirty flag (`+0xb0=2`) if the values changed.
5. `ShieldsGauge_Update` (`00443748`, the per-frame HUD-paint-pass slot, separate from the click pipeline) checks that dirty flag and, if set, refreshes the ring palette and readouts.

**The `[` and `]` keys join at step 2, not at step 4.** `Mech_HandleCommand` (`004157c8`) answers scancodes `0x1a`/`0x1b` with a single `Widget_PressChild(CockpitViewInstance+0x1e9, key != 0x1b, 1)` — the shield gauge, child 1 for `[` and child 0 for `]`, with the left-button bit as the flags. That dispatches the facing's own press slot, which is `Widget_ForwardClickToOwner` again. So the key and the click are one code path from step 2 onward: same flag byte, same click sound, and the same ~10-coarse-tick auto-release (`FUN_00453078`) that pops the widget back up afterwards. Nothing in the image calls `Shield_BalanceAdjust` except `Shield_BalanceInputRead`, and nothing writes `+0xc2`/`+0xc3` except `ShieldsGauge_OnClick`.

RAZOR is the exception on the key side only: `Mech_HandleCommand` is a mech vtable slot and the flyer class installs a stub there (`004215c0`), so the brackets do nothing in a RAZOR — but its facings are still built and still take clicks, over what is an altimeter rather than a shield meter in that cockpit (see [`herc-catalogs.md`](herc-catalogs.md) and `HShieldDisplay`).

So the click sets a flag; a gameplay tick consumes the flag into real sim state and a dirty bit; the widget's own per-frame update slot is what actually repaints from that bit.

## 9. Cursor rendering

The position the click pipeline reads is the same one the player watches: `Cursor_SyncPosition` (`00486d70`) stores the position in one of two slots, chosen by `DAT_004a365e`, and when that byte is set brackets the store with `g_RasterRoutines` slots 31 and 30; driver 3, the only raster driver the image installs, fills both with empty stubs. `Screen_PresentFrame` (`00465524`) copies the back buffer's viewport window to the screen ([`cockpit-views.md`](cockpit-views.md#presentation)) — `StretchBlt` in windowed/GDI mode, a row copy into the locked DirectDraw surface in fullscreen — and in the fullscreen path also blits a cursor sprite at `GetCursorPos()`, clipped to the viewport and colour-keyed on byte value 1, when a software cursor bitmap (`DAT_004d37a8`) is active.

`Mouse_WarpCursorToPoint` (`004807d0`) runs the conversion the other way — game space back to client coordinates, `ClientToScreen`, `SetCursorPos` — and three places use it, all of them putting the pointer somewhere known and all gated on a live mouse device:

- `Gau_RovingGunsightWidget` (`0043c7d8`) centres it on the gunsight as the cockpit is built, so a fresh mission starts with the pointer on the reticle.
- `AlertPanel_SetFocus` (`00454c7c`) moves it to the centre of the widget being focused — in the alert family focus *is* the pointer, which is how a keyboard walk down a preferences or controls panel works ([`../simulation/preferences.md`](../simulation/preferences.md)).
- `AlertPanel_Leave` (`004548ac`) restores the position the panel saved at its own `+0x302` when it was raised.

## 10. The screen edges are three widgets

The manual: *"Change views with the mouse by clicking on the screen edge leading to the view you want."* That is not a special case anywhere in the input path — it is three ordinary `ScrollTrigger` widgets in the same flat clickable list as every button, hit-tested by the same `Widget_HitTestChildren`.

`CockpitView_BuildScrollTriggers` (`00433770`) allocates and registers them. The rects it builds are the forward view's, `W` and `H` being the display context's own width and height and the band thicknesses scaled by `VideoMode_XCoordShift`/`YCoordShift`:

| Field | Rect as built | Strip in the forward view |
|---|---|---|
| `+0x21a` | `{0, H-1-(3<<Y), W-1, H-1}` | Bottom, full width, 3 rows thick |
| `+0x21e` | `{0, 0, 5<<X, H-1}` | Left, full height, 5 columns wide |
| `+0x222` | `{W-1-(5<<X), 0, W-1, H-1}` | Right, full height, 5 columns wide |

**They are built lazily, on the first cockpit frame rather than in a constructor.** The cockpit tick (`004327ac`) calls the builder once, gated on a one-shot flag at `+0x23e`, which is why the three sit apart from the rest of the widget build — and, because registration is precedence (§5), why anything else occupying that band wins the click. Three cockpits do: MAVERICK's `[F6]` button, RAPTOR2's throttle and RAZOR's `TRACK` button all reach into the bottom `3 << YCoordShift` rows, and a click there works the instrument instead of changing view. The other six leave the band clear.

### A strip changes edge with the view

**Widget rects are in cockpit-canvas space, not screen space.** `Widget_OnMouseDown` and `Widget_OnMouseUp` convert a screen point before hit-testing by subtracting the **root widget's own rect origin** — `point - root->rect.x0/y0` — and that origin moves with the view. `CockpitWidgets_TranslateForView` (`0043271c`) runs on every view change with the origin delta between the outgoing view and the incoming one, and its first act is `Widget_TranslateRootForView` (`00452734`, reached through the argument-forwarding thunk at `00452bc4`), which adds that delta to the root's rect and to the origin pair at `+0x22b`/`+0x22f`. So in the heads-down view the root origin is `(0,-237)` and a click at screen row 1 is tested as canvas row 238.

That is the whole of why one widget serves opposite edges. The heads-down view's canvas origin is `237` and the forward view's window is `240` rows tall, so the **canvas band at rows 236-239 is the bottom of the forward view and the top of the heads-down view at the same time**. The bottom strip is built into that band and never leaves it.

The three strips also get `CockpitView_FlipScrollTriggers` (`00433968`), which `CockpitWidgets_TranslateForView` calls once `+0x23e` says they exist. It classifies each axis of the delta — at least a band's worth (`>= 6`, or `< -5`) sets a sign and a compensating shift of one full `W` or `H`, anything smaller leaves the axis alone — then **reflects the band across its own edge** (`y0 = y1; y1 = y0 + height` for a downward delta, mirrored for an upward one) and translates by the delta net of that shift. For the bottom strip on a vertical pan the reflection and the translation cancel and the rect is left where it was, the root origin doing all the work. **For the side strips it is the reflection that matters**: the left strip's canvas band `x 0..5` would otherwise sit at screen `x 320..325` in the left window, off the display; reflected to `x -5..0` against a root origin of `(+320,0)` it lands on the **right** edge instead.

Worked through for all four views, every strip ends up on the edge facing the view it leads to:

| In view | Top | Bottom | Left | Right |
|---|---|---|---|---|
| 0, forward | — | pan down to heads-down | glance to view 3 | glance to view 2 |
| 1, heads-down | pan back up | — | hit, but the handler ignores it | hit, but the handler ignores it |
| 3, left window | — | — | — | return to forward |
| 2, right window | — | — | return to forward | — |

A dash is a click that hits no strip at all. The heads-down view is the one place a strip is hit and does nothing: the two side strips stretch across that view, and `CockpitView_HandleEdgeTrigger` has no case for them from view 1.

`ScrollTrigger_OnClick` (`00434df0`) is the whole of the class: `CockpitView_HandleEdgeTrigger(this->[0x20], this)`. The handler compares the widget pointer against the three fields and picks a view command by the current view ([`cockpit-views.md`](cockpit-views.md#view-switching) has what each command does):

| Strip | From view 0 | From the view it leads to |
|---|---|---|
| Bottom | command 0, pan down to heads-down | view 1 → command 1, pan back up |
| Left | command 5, glance to view 3 | view 3 → command 6, return |
| Right | command 4, glance to view 2 | view 2 → command 6, return |

**Each strip serves exactly two views**, and the handler does nothing from any other. Those pairs are the manual's rule in both directions: with the canvas mapping above, the edge you click is always the one facing the view you are asking for, going out and coming back.

`ScrollTrigger` carries no `PanelGadget`, so `+0x20` is a plain owner pointer rather than a second vtable and a strip makes no console click.

Herculan: the bottom strip is `CockpitWidgets.VisibleHeadsDownViewEdge`; the side strips are `Render.CockpitScreenLayout.SideViewEdgeAt`, on the window's edges rather than the forward view's (see [`KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md)).

## Symbol reference

| Symbol | Address | Role |
|---|---|---|
| `WndProcHook_Register` / `_Unregister` | `00465ee8` / `00465f0c` | 10-slot raw-message filter table ahead of `MainWndProc`'s switch |
| `Mouse_WndProcHook` | `004808ec` | Catches the 5 mouse messages, forwards by kind |
| `Mouse_Init` | `00480588` | Registers the hook above |
| `Mouse_DispatchEvent` | `0048083c` | Rescales + fans out to mouse listeners |
| `Mouse_RecomputeScale` | `0048078c` | Client-to-game coordinate factor, recomputed on resize |
| `MouseListener_Register` / `_Unregister` | `0048073c` / `00480774` | 10-slot mouse-event subscriber table |
| `CockpitMouse_Init` | `00452abc` | Registers the cockpit's one listener, sets up the event queue and timing gate |
| `CockpitMouse_OnEvent` | `00452cb4` | The listener callback; queues, syncs cursor position |
| `CockpitMouseQueue_Push` | `00453034` | Appends one event record to the back buffer |
| `CockpitMouse_ProcessQueue` | `00452d18` | Once-per-frame drain: press/release/drag logic |
| `Widget_HitTest` | `00452388` | Rect or circular/diamond point test |
| `SliderWidget_CtorBase` | `004524a8` | Shared slider base; the only ctor that sets the `+0x1d` drag-capture flag |
| `SliderWidget_DragToPointV` | `004525d8` | Vertical drag: clamps the pointer into the track, puts the knob bottom there |
| `SliderWidget_GetValueV` / `_SetValueV` | `00452628` / `00452644` | Vertical value from/to knob position |
| `SliderWidget_RecomputeScaleV` | `00452694` | Q16 pixels-per-unit over the knob travel |
| `SliderWidget_DragToPointH` / `_GetValueH` / `_SetValueH` / `_RecomputeScaleH` | `004524f8` / `00452544` / `0045255c` / `004525a8` | The horizontal twins |
| `Widget_HitTestChildren` | `00452a00` | Scans the flat clickable list; first hit wins, so registration order is precedence (§5) |
| `Gau_BuildCockpitWidgets` | `00431bf8` | Builds the seven top-level gauges in the order that becomes the clickable list's own |
| `SystemButtons_Ctor` / `_OnChildClick` | `00434368` / `004345a0` | The online-manual and fullscreen buttons, and what each one does |
| `Video_ToggleFullscreen` | `004666c4` | Fullscreen/windowed switch behind the left system button, and the one `Help_Show` runs first |
| `ConsoleButtons_Ctor` | `00441dd0` | The four `WeaponRangeSelectGadget`s under the weapon panel |
| `WeaponMounts_BuildGauges` | `00410644` | Dispatches each mount's gauge-factory slot; the closing call of `Gau_BuildCockpitWidgets` |
| `Widget_OnMouseDown` / `_OnMouseUp` | `004527a0` / `00452870` | Press and click state transitions |
| `Widget_TrackPressedWidget` | `00452954` | Keeps the held widget depressed only while the pointer is on it |
| `Widget_PressedIndex` | `0049dbdc` | int16 index of the widget a button is held on, -1 for none |
| `Widget_DragCapture` | `0049dbde` | Set while a `+0x1d` widget holds the pointer; routes moves to `+0x18` and suppresses the click |
| `Widget_Repaint` | `00452a90` | Calls a widget's own Paint slot |
| `Widget_RegisterClickable` | `00452c44` | Appends to the flat clickable list |
| `Widget_Show` / `Widget_Hide` | `00452c64` / `00452c8c` | Set a child's state to 0 / 2 — library helpers with no call site, no vtable slot and no stored pointer; every hide in the image is a direct store to `+0x1b` |
| `Widget_NotifySelfAndChildren` | `00452a48` | Calls vtable slot 0 on self then every clickable child; unreachable — its only caller (`00452bac`) has none of its own |
| `Widget_CtorRect` | `00452478` | Base widget constructor: copies the rect and clears the hit-shape byte |
| `Widget_DrawToCockpit` | `0043122c` | Stage 1 of the deferred paint: copies a widget's rect to the other display page, on the `-b` paged path only |
| `Cursor_SyncPosition` | `00486d70` | Stores the pointer position |
| `Mouse_WarpCursorToPoint` | `004807d0` | `SetCursorPos` wrapper: gunsight centring, alert-panel focus and the panel's saved position |
| `Screen_PresentFrame` | `00465524` | Copies the back buffer's viewport window to the screen; software cursor blit in fullscreen mode |
| `ThrottleGauge_Ctor` | `00447b84` | Builds the throttle gauge, its slider child and its two fill bars |
| `ThrottleGauge_SetValues` / `_GetValues` | `00447d80` / `00447dd0` | The `{speedFraction, throttle}` pair at gauge `+0xb1` |
| `ThrottleGauge_OnChildValue` | `00447de0` | Slider/bar value in, negated for the vertical variant, stored to `+0xb5` |
| `ThrottleSlider_CtorV` | `00447e24` | The vertical slider child, `SLIDE_DIR == 1` — what all 9 retail `.GAU`s use |
| `ThrottleSlider_CtorFixed` | `004483c0` | The `SLIDE_DIR != 1` variant; never exercised by retail data |
| `ThrottleSlider_OnValue` | `00448378` | Commit: notifies the gauge, then sets `ThrottleLeverMode` from the value sign |
| `LedBarGraph_CtorV` | `00439344` | Vertical LED bar; the throttle builds two, ranges +0x400 and -0x400 |
| `ShieldsGauge_OnClick` | `0044380c` | Sets front/rear click flags |
| `Widget_GetButtonValue` | `00455530` | Default `GetValue`: returns the global mouse-button word `0049db6c` |
| `Widget_ForwardClickToOwner` | `00438e3c` | The base `OnClick` at slot `+8`: left button only, calls the owner's slot 0 |
| `Widget_InvalidateDeferred` | `00455524` | The base `Invalidate` at slot `+4`: sets the deferred-paint counter `+0x1f` to 2 |
| `Sim_DispatchCommand` | `0045fdac` | Every command code passes through here: the widget tree, then the player mech's vtable +0x2c |
| `Mech_HandleCommand` | `004157c8` | Mech vtable +0x2c; offers the code to the weapon manager before handling it itself |
| `CockpitWidgets_HandleCommand` | `00432bc8` | The widget tree's command handler; codes 0x02-0x0b press the ten weapon gauges |
| `ConsoleButtons_HandleCommand` | `004421a0` | Console panel's command slot: 0x26 (L) presses LINK, 0x29 (`) presses the chain button |
| `Widget_PressChild` | `00438d9c` | Dispatches a child's press slot as if clicked — how a key reaches a button |
| `CockpitView_BuildScrollTriggers` | `00433770` | Builds the three screen-edge view strips, once, on the first cockpit frame |
| `ScrollTrigger_OnClick` | `00434df0` | The strip's whole behaviour: hands itself and its owner to the handler below |
| `CockpitView_HandleEdgeTrigger` | `00433a88` | Which view command a strip queues, by which strip and the current view |
| `CockpitWidgets_TranslateForView` | `0043271c` | Moves the root, flips the strips and offsets the rest, on every view change |
| `Widget_TranslateRootForView` | `00452734` | Moves the root's rect by the view delta — the screen-to-canvas mapping the hit test uses |
| `Widget_OffsetRect` | `0045240c` | Adds a delta to a widget rect's four ints |
| `CockpitView_FlipScrollTriggers` | `00433968` | Reflects and shifts the three strips so an edge strip changes edge with the view |
| `Widget_ClickSound` | `00438e2c` | `push 0x11; call Sound_Play` — the console click; in `PanelGadget`'s table and the fourteen button classes that inherit it, and nowhere else |
| `ShieldsGauge_GetStateBlock` / `_SetStateBlock` | `004438e0` / `00443858` | Read/write the 15-byte live state block |
| `ShieldsGauge_Paint` / `_Update` | `00443730` / `00443748` | Paint slot; per-frame dirty-flag-gated update |
| `ShieldFacing_Paint` | `00444b5c` | Visibility test only — rings are palette-animated, not drawn |
| `ShieldFacing_FlushDeferredPaint` | `00444b70` | The facing's slot `+0xc`: stage 2 decrements, stage 1 blits |
| `Key_WndProcHook` | `00477ae0` | Keyboard messages to command codes: scancode table, modifier bits, subscriber filter, ring |
| `KeyListener_Register` | `00477a0c` | Ten-slot `{enabled, wantedCodes, callback}` keyboard subscriber table (`006bb93c`) |
| `Input_KeyjoyAxisKey` | `0045a308` | The sim's axis-key subscriber; 30 codes at `0049eb33` into the key-state blocks |
| `SimCommandQueue_Push` | `0045a47c` | The sim's command subscriber; 7 codes at `0049eb5d` into the queue at `004d2148` |
| `SimInput_SetEnabled` | `0045b670` | Registers both of the above, or clears the key-state blocks |
| `Input_BuildPlayerDevice` | `0045a7f4` | Builds the frame's input block; drains `004d2148` through `Sim_DispatchCommand`; records and replays |
| `Input_ReadDeviceEvent` | `0045c084` | Pops the unclaimed-key ring at `006bb734` |
| `VkToScancode` | `004a1104` | 223 int32: Win32 virtual key to set-1 scancode |
| `KeyListenerTable` | `006bb93c` | Ten `{enabled, wantedCodes, callback}` keyboard subscribers |
| `KeyEventRing` | `006bb734` | 256 int16 ring for codes no subscriber claimed |
| `KeyjoyWantedCodes` / `SimCommandWantedCodes` | `0049eb33` / `0049eb5d` | The two sim subscribers' wanted-code lists |
| `SimCommandMask` | `0049eae0` | `0x47ff`, ANDed into every queued command before dispatch |
| `Language_GetFolderName` | `0045efe0` | `data\language.cfg`'s first byte to `ENGLISH`/`FRENCH`/`GERMAN`/`SPANISH` |
| `OnlineManual_Raise` | `0045f054` | Builds `<language>\es2guide.hlp` and hands it to `Help_Show` |
| `Help_Show` | `004668c0` | `WinHelpA(hwnd, path, HELP_CONTENTS, 0)`, after clearing the display |

## Open

- **Unported:** the two system buttons (§5), the online manual and the fullscreen toggle.
- **Open:** whether other sim-driven HUD elements (weapon damage fill, hardpoint state boxes) use the shield rocker's flag-then-dirty-bit handoff between the sim tick and the paint pass (§8).

