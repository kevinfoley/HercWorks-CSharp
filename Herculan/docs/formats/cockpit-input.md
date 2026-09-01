# Cockpit mouse input

How DBSIM routes a mouse click on the cockpit dashboard/HUD/HDD to a button's own click handler.
Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project. All addresses are DBSIM.
Symbols are in `tools/ghidra_scripts/known_symbols.json`; apply with `ES2ApplySymbolNames.java`.

Widget geometry, frames and paint logic are covered by [`cockpit-hud.md`](cockpit-hud.md),
[`mfd.md`](mfd.md) and [`heads-down-display.md`](heads-down-display.md) — this document is only
the input path: how a mouse event becomes a call into a specific widget's own handler.

Implemented in `Herculan.Engine` across three types: `CockpitScreenLayout` (window pixel to art
pixel, the step the original does not need), `CockpitWidgets` (the flat clickable list and the
rectangular hit test, §5-6) and `CockpitInput` (the queue and the press/release/hold state machine,
§3-4 and §7). `Herculan.Engine.Host`'s `Program.cs` queues the events and routes completed clicks.
Sections 1-2 and 9 are deliberately not ported — see `CockpitInput`'s own summary for what diverges
and why.

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

Three separate things all matter for a click to land: the OS-message layer never sees widgets at
all (§1-2), the cockpit's own click state machine only runs once per frame off a queued event
(§3-4), and a widget doesn't need to belong to whatever panel is showing to be hit — hidden panels
are excluded by a per-widget state byte, not by not being in the list (§5).

## 1. Win32 messages bypass MainWndProc's own switch

`MainWndProc` (`@MainWndProc$qqspvuiuil`, `00465f30`) has no `WM_MOUSEMOVE`/`WM_LBUTTONDOWN`/etc.
case at all. Before its own switch it tries up to 10 registered filter functions in order —
`WndProcHook_Register`/`WndProcHook_Unregister` manage that table (`DAT_004d3bb4`) — and any hook
returning nonzero short-circuits the rest of `MainWndProc` entirely. `Mouse_WndProcHook`
(`004808ec`) is one of four hooks found registered; the other three are sound/movie-playback
related.

`Mouse_WndProcHook` catches:

| Message | Value | Forwarded as kind |
|---|---|---|
| `WM_MOUSEMOVE` | `0x200` | 0 |
| `WM_LBUTTONDOWN` | `0x201` | 1 |
| `WM_LBUTTONUP` | `0x202` | 2 |
| `WM_RBUTTONDOWN` | `0x204` | 3 |
| `WM_RBUTTONUP` | `0x205` | 4 |

Each call forwards to `Mouse_DispatchEvent(kind, lParam, buttonFlags)`, `buttonFlags` built from
wParam's button-held bits.

## 2. A second, independent subscriber list carries it further

`Mouse_DispatchEvent` (`0048083c`) rescales the raw window-client `(x,y)` into the game's internal
coordinate space via a fixed-point factor recomputed on resize (`Mouse_RecomputeScale`, `0048078c`
— `(gameSize << 0xf) / clientSize`), packs the current L/R held bitmask into `DAT_006c5fdc`, then
calls every enabled entry of a **second, independent** 10-slot table (`DAT_006c5f5c`) whose event
mask has the event's bit set. `MouseListener_Register`/`MouseListener_Unregister`
(`0048073c`/`00480774`) manage that table.

This `{enabled, eventMask, callback}` triple-array shape is not unique to mouse input — DBSIM
reuses it for the `WndProcHook` table above and for a keyboard-binding table (read by
`FUN_00477ae0`) as well. Recognize it as a house idiom rather than three unrelated systems.

## 3. The cockpit's one listener queues, it doesn't act

`CockpitMouse_Init` (`00452abc`) runs once at cockpit setup:

- Registers `CockpitMouse_OnEvent` (`00452cb4`) via `MouseListener_Register` with mask **`0x1e`**
  — bits 1-4 (button down/up on both buttons). Bit 0, plain movement, is deliberately excluded.
- Allocates a double-buffered event queue: two 100-capacity vectors, swapped each frame by
  `CockpitMouse_ProcessQueue`.
- Sets `DAT_004d1e70 = 0x1e` — a **click-vs-drag timing gate**, in coarse UI ticks (~16ms each, so
  ~480ms). This is the same numeric literal as the event mask above but a wholly unrelated field;
  don't conflate them when reading the disassembly.

`CockpitMouse_OnEvent` doesn't process the click. It pushes a 14-byte record
`{int32 x, int32 y, uint16 buttonMask, int32 timestamp}` onto the back-buffer queue via
`CockpitMouseQueue_Push` (`00453034`, capped at 99 entries), debounced to at most once per coarse
tick. It also calls `Cursor_SyncPosition` (`00486d70`) immediately, independent of the queue, so the
drawn pointer tracks the raw event position without waiting for the frame drain (§7).

## 4. Once per frame: the real click/press/drag logic

`CockpitMouse_ProcessQueue` (`00452d18`) swaps the queue buffers and, per queued record:

- **Position changed:** hit-tests via `Widget_HitTestChildren`. If a widget is mouse-captured
  (dragging), forwards the new position straight to its drag-move vtable slot instead of
  re-hit-testing. Otherwise calls `Widget_TrackPressedWidget`, which keeps the *held* widget's
  depressed look in sync with the pointer — see §7. It is not a hover mechanism: **DBSIM has no
  hover state**.
- **Button-down edge** (bit set now, wasn't last record): calls `Widget_OnMouseDown`.
- **Button-up edge** (bit clear now, was set): only if the release lands within `DAT_004d1e70`
  ticks of the press — the click-vs-drag gate — calls `Widget_OnMouseUp`. If a drag was in
  progress instead, ends the capture and fires the captured widget's `GetValue`/`OnClick` pair
  directly rather than going through `Widget_OnMouseUp`.

## 5. One flat hit-test registry for the whole cockpit

Every clickable widget anywhere in the cockpit — the MFD's 13 buttons, the HDD's 15 widgets, the 3
console buttons, weapon-select gadgets, the two shield-balance facings — is appended to **one
shared list**, not organized per-panel. `Widget_RegisterClickable` (`00452c44`) appends a widget
pointer to the caller's own `+0x256` array (count at `+0x254`); every top-level cockpit widget's
constructor calls it once per child with the *dereferenced* `CockpitViewInstance` pointer as the
shared root. Confirmed identically in `MfdDisplay_Ctor`'s 13-button loop and `ShieldsGauge_Ctor`'s
2-facing loop.

`Widget_HitTestChildren` (`00452a00`) linear-scans that whole list with `Widget_HitTest`, skipping
any widget whose state byte (`+0x1b`) is `2`. This is how an off-screen panel's buttons don't
intercept clicks: nothing removes them from the list when their panel isn't showing, only
`Widget_Hide`/`Widget_Show` (`00452c8c`/`00452c64`) toggling that one state byte.

Widget state byte (`+0x1b`), as read by every traced `Paint` slot:

| Value | Meaning |
|---|---|
| 0 | Normal |
| 1 | Lit — either held down, or selected (a mode button's current screen). Not hover; there is none |
| 2 | Hidden — excluded from hit-testing and refused by Paint |

## 6. Hit test: rectangle or circle

`Widget_HitTest` (`00452388`) isn't purely rectangular. A per-widget flag at `+0x10` selects the
shape:

| Flag | Shape | Fields |
|---|---|---|
| 0 | Axis-aligned rect, inclusive | `+0x0`/`+0x4`/`+0x8`/`+0xc` = x0,y0,x1,y1 |
| nonzero | Circular/diamond | centre `+0x11`/`+0x13` (int16 cx,cy), radius `+0x15` (int16); test is Manhattan distance ≤ radius, not true Euclidean |

Every widget traced so far (MFD/HDD/console buttons, the shield facings) uses the rectangular
form. The circular form exists and is presumably for a knob/dial-shaped control — none identified
yet.

## 7. Press, release, click vs. drag

`Widget_OnMouseDown` (`004527a0`): on hit, stores the hit index in `Widget_PressedIndex`
(`0049dbdc`) — one global for the whole cockpit — sets the widget's state to `1` and repaints it. If
the widget's own `+0x1d` flag is `1`, begins mouse capture (`DAT_0049dbde=1`) and forwards the
position to its drag-move vtable slot (`+0x18`) immediately.

**One retail widget does use capture: the throttle slider.** Every button class leaves `+0x1d` clear,
but the shared slider base `SliderWidget_CtorBase` (`004524a8`) sets it unconditionally, and the
throttle's vertical slider child (`00447e24`) is built through it. It is why the manual's "set
throttle with the mouse by clicking on the slide and dragging it up or down" works, and why clicking
anywhere on the track jumps the knob there — the press itself dispatches the drag handler.

While capture is held, `CockpitMouse_ProcessQueue` takes a different branch on every position change:
it dispatches `+0x18` on the captured widget with the pointer position and repaints it, **without
hit-testing** — so a drag follows the pointer off the widget, off the panel and off the window.
`Widget_TrackPressedWidget` is not called at all in that branch, so a captured widget stays depressed
however far the pointer wanders.

Release under capture also takes its own branch: clear the state byte, repaint, clear
`DAT_0049dbde`, then read the widget's value (`+0x10`) and commit it (`+8`), and clear
`Widget_PressedIndex`. `Widget_OnMouseUp` is never reached, so **a drag fires no click** — including
a press-and-release that never moved.

`Widget_TrackPressedWidget` (`00452954`): called on every position change *outside* capture, and a
no-op unless `Widget_PressedIndex` is valid. It re-hit-tests and compares against that index: still on
the held widget and its state is `0`, set it to `1` and repaint; anywhere else and its state is `1`,
clear to `0` and repaint. That is a button popping back up when you drag off it and depressing again
when you come back, and it is the *only* thing this function does.

**There is no hover state anywhere in DBSIM.** Beware any symbol set that still names `00452954`
`Widget_OnMouseHover` — that reading of this toggle is wrong. Two independent facts rule hover out: the <!-- doc-lint: ok -->
`Widget_PressedIndex != -1` guard means the function cannot run unless a button is held, and
`CockpitMouse_Init`'s event mask (`0x1e`, §3) never subscribes to plain movement in the first place,
so nothing would drive a hover highlight even if the code wanted one.

`Widget_OnMouseUp` (`00452870`): if the release lands back on the widget that was pressed **or the
release was a right-button one**, calls that widget's `GetValue` vtable slot, then its `OnClick`
slot with that value, then clears the pressed state and repaints via `Widget_Repaint` (`00452a90`).

**Where `OnClick` sits differs by class.** `ShieldsGauge` and `MfdDisplay` take it at vtable slot 0.
`ConsoleButton` (`FUN_00442dc8`) and `WeaponSelectGadget` (`FUN_00442458`) take it at `+8` of their
`+0x17` vtable and forward to their owner's slot 0 — a different slot, the same shape.

**Only the left button presses.** `CockpitMouse_ProcessQueue` calls `Widget_OnMouseDown` for a left
press and not a right one, so a right click never arms a widget — the release's own re-hit-test
plus that second condition is the whole of what makes it work. Herculan arms on either button and
requires press and release on the same widget for both, which reaches the same outcome for a normal
click and diverges only for a right press dragged off its widget before release.

### The click value carries the mouse button

`GetValue`'s default implementation (`00455530`) returns the global button word `0049db6c`, which
`CockpitMouse_ProcessQueue` sets to `buttons | 1` on a left release and `buttons | 2` on a right
one. A widget whose class does not override the slot therefore receives **which button clicked it**
as its "value", and several branch on it: a weapon panel row arms its mount on bit 0 and toggles the
mount's fire-chain membership on bit 1 (see
[`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md#arming-chaining-and-linking)).
Sliders override the slot and return a real value instead (`SliderWidget_GetValueV`).

### Keyboard commands are scancodes

The same handlers are reachable from the keyboard, and the command codes that travel through
`FUN_0045fdac` to the widget tree (`FUN_00432bc8`) are **PC set-1 scancodes**, with `0x200` added
for `[Alt]` — `0x26` is `L`, `0x29` is `` ` ``, `0x11`/`0x211` are `W`/`Alt+W`, `0x1a`/`0x1b` are
`[`/`]`, `0x3b`–`0x40` are `F1`–`F6`. Codes `0x02`–`0x0b` (the number row) index the cockpit's own
ten weapon gauges at `CockpitViewInstance+0x70` and press each one's select gadget, which is how a
key and a click end up in one handler rather than two.

`Widget_Repaint` calls a widget's own Paint slot — but **the slot's numeric vtable offset is not
uniform across classes**. Confirmed at `+4` for both `MfdDisplay` (`MfdDisplay_Repaint`) and
`HddButton` (`HddButton_Paint`), via unambiguous evidence (the exact address a constructor
assigns as the object's vtable pointer). Do not assume `+4` — or any other fixed offset — holds
for a class not independently checked; `ShieldsGauge`'s own vtable puts `OnClick` at slot 0 and
`Paint` at `+4` (same convention), but the general shape of "leaf widget forwards its click to its
owner's slot 0" was only confirmed for the shield facings (§8), not exhaustively for every widget
kind.

## 8. Worked example: the shield-balance rocker

Traced end to end as a concrete proof the whole pipeline above is real, not just plausible:

1. `ShieldsGauge_Ctor` builds two facing children via `ShieldsGauge_FacingCtor`
   (`cockpit-hud.md`), registers each with `Widget_RegisterClickable`, and stores each child's
   pointer plus a count into its own `+0x18`/`+0x68` array — the same shape `MfdDisplay_Ctor` uses
   for its 13 buttons.
2. A click hits `ShieldFacing_OnClick` (`00438e3c`) via `Widget_OnMouseUp`. Gated on the left
   button bit; forwards to the owner (a pointer stashed at the facing's own `+0x24`, set to the
   parent `ShieldsGauge` at construction) as `owner->vtable[0](owner, self, buttonFlags)`.
3. `ShieldsGauge`'s vtable slot 0 is `ShieldsGauge_OnClick` (`0044380c`) — structurally identical
   to `MfdButton_OnClick`: searches its own `+0x18` table for the clicked child, then sets a state
   byte: index 0 (front) → `+0xc2=1`, index 1 (rear) → `+0xc3=1`.
4. `Shield_BalanceInputRead` (`00413bc8`, called once per frame from
   `Player_PerFrameCockpitUpdate` — gameplay, not paint) reads those same two bytes (part of a
   15-byte block starting at `+0xb5`, accessed via `ShieldsGauge_GetStateBlock`), calls
   `Shield_BalanceAdjust` (±102 of 1024, clamped) accordingly, clears the flags, recomputes
   front/rear percentages, and writes the block back via `ShieldsGauge_SetStateBlock`
   (`00443858`) — which also sets a dirty flag (`+0xb0=2`) if the values changed.
5. `ShieldsGauge_Update` (`00443748`, the per-frame HUD-paint-pass slot, separate from the click
   pipeline) checks that dirty flag and, if set, refreshes the ring palette and readouts.

So the click sets a flag; a gameplay tick consumes the flag into real sim state and a dirty bit;
the widget's own per-frame update slot is what actually repaints from that bit. This
flag-then-dirty-bit handoff between the sim tick and the paint pass is likely how other
sim-driven HUD elements (weapon damage fill, hardpoint state boxes) stay in sync too, though that
wasn't checked here.

## 9. Cursor rendering

The position the click pipeline reads is the same one the player watches: `Cursor_SyncPosition`
(`00486d70`) either moves a hardware DirectDraw cursor (when `DAT_004a365e` is set, via two
function-pointer calls — hide/show around a position update) or stashes the position for
`Screen_PresentFrame`'s software cursor draw. `Screen_PresentFrame` (`00465524`) is DBSIM's
per-frame presentation function — `StretchBlt` in windowed/GDI mode, a raw VRAM copy in fullscreen
— and in the fullscreen path also blits a cursor sprite at `GetCursorPos()`, clipped to the
viewport and colour-keyed on byte value 1, when a software cursor bitmap (`DAT_004d37a8`) is
active.

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
| `Widget_HitTestChildren` | `00452a00` | Scans the flat clickable list |
| `Widget_OnMouseDown` / `_OnMouseUp` | `004527a0` / `00452870` | Press and click state transitions |
| `Widget_TrackPressedWidget` | `00452954` | Keeps the held widget depressed only while the pointer is on it |
| `Widget_PressedIndex` | `0049dbdc` | int16 index of the widget a button is held on, -1 for none |
| `Widget_DragCapture` | `0049dbde` | Set while a `+0x1d` widget holds the pointer; routes moves to `+0x18` and suppresses the click |
| `Widget_Repaint` | `00452a90` | Calls a widget's own Paint slot |
| `Widget_RegisterClickable` | `00452c44` | Appends to the flat clickable list |
| `Widget_Show` / `Widget_Hide` | `00452c64` / `00452c8c` | Set a child's state to 0 / 2 |
| `maybe_Widget_NotifySelfAndChildren` | `00452a48` | Calls vtable slot 0 on self then every clickable child; exact purpose not confirmed |
| `Cursor_SyncPosition` | `00486d70` | Hardware/software cursor position sync |
| `maybe_Screen_PresentFrame` | `00465524` | Per-frame present; software cursor blit in fullscreen mode |
| `maybe_Mouse_WarpCursorToPoint` | `004807d0` | `SetCursorPos` wrapper; caller/trigger not traced |
| `ThrottleGauge_Ctor` | `00447b84` | Builds the throttle gauge, its slider child and its two fill bars |
| `ThrottleGauge_SetValues` / `_GetValues` | `00447d80` / `00447dd0` | The `{speedFraction, throttle}` pair at gauge `+0xb1` |
| `ThrottleGauge_OnChildValue` | `00447de0` | Slider/bar value in, negated for the vertical variant, stored to `+0xb5` |
| `ThrottleSlider_CtorV` | `00447e24` | The vertical slider child, `SLIDE_DIR == 1` — what all 9 retail `.GAU`s use |
| `ThrottleSlider_CtorFixed` | `004483c0` | The `SLIDE_DIR != 1` variant; never exercised by retail data |
| `ThrottleSlider_OnValue` | `00448378` | Commit: notifies the gauge, then sets `ThrottleLeverMode` from the value sign |
| `LedBarGraph_CtorV` | `00439344` | Vertical LED bar; the throttle builds two, ranges +0x400 and -0x400 |
| `ShieldsGauge_OnClick` | `0044380c` | Sets front/rear click flags |
| `Widget_GetButtonValue` | `00455530` | Default `GetValue`: returns the global mouse-button word `0049db6c` |
| `ShieldFacing_OnClick` | `00438e3c` | Forwards a facing's click to its owner |
| `Sim_DispatchCommand` | `0045fdac` | Every command code passes through here: the widget tree, then the player mech's vtable +0x2c |
| `Mech_HandleCommand` | `004157c8` | Mech vtable +0x2c; offers the code to the weapon manager before handling it itself |
| `CockpitWidgets_HandleCommand` | `00432bc8` | The widget tree's command handler; codes 0x02-0x0b press the ten weapon gauges |
| `ConsoleButtons_HandleCommand` | `004421a0` | Console panel's command slot: 0x26 (L) presses LINK, 0x29 (`) presses the chain button |
| `Widget_PressChild` | `00438d9c` | Dispatches a child's press slot as if clicked — how a key reaches a button |
| `ShieldsGauge_GetStateBlock` / `_SetStateBlock` | `004438e0` / `00443858` | Read/write the 15-byte live state block |
| `ShieldsGauge_Paint` / `_Update` | `00443730` / `00443748` | Paint slot; per-frame dirty-flag-gated update |
| `ShieldFacing_Paint` | `00444b5c` | Visibility test only — rings are palette-animated, not drawn |
| `maybe_ShieldFacing_FlashTimer` | `00444b70` | Two-stage counter, plausibly a post-click flash; not confirmed |

## Open

- Which widget(s), if any, use `Widget_HitTest`'s circular/diamond shape — none identified among
  the MFD, HDD, console buttons or shield facings.
- `maybe_Widget_NotifySelfAndChildren`'s actual purpose (relayout? a generic refresh cascade?) —
  only its mechanical shape (vtable slot 0 on self then children) is confirmed.
- `maybe_Mouse_WarpCursorToPoint`'s caller(s) and trigger.
- Which vtable slot the throttle and the ordinary MFD/HDD leaf buttons (as opposed to their owning
  display objects) put `OnClick` at — neither was checked. See §7 for the two shapes found so far.
- Where the command-code queue at `004d2148` is filled from. The codes' meaning is settled (§7,
  scancodes with an `0x200` Alt bank), but the raw-keystroke-to-queue step was not traced.
- The exact leaf "Button" widget class MFD buttons construct through (`FUN_004472e4` /
  `FUN_0044741c`, called from `MfdDisplay_Ctor`) — the forward-to-owner shape is inferred from the
  shield-facing case, not independently decompiled for this class.
- `maybe_ShieldFacing_FlashTimer`'s trigger and visible effect.
