# Shell screen layout

How VSHELL puts a screen together: the canvas it authors in, where the layout numbers live, the widget tree behind every tab screen, and the shared resources the whole front end draws with. The economy behind the screens is in [`armory.md`](armory.md); the campaign state they read and write is in [`campaign-loop.md`](campaign-loop.md).

## The canvas is 640x480, and rects are inclusive

Every tab screen parents its widgets to a panel built with the rect `{0, 0, 0x27f, 0x1df}` — 0..639 by 0..479. Both corners are inclusive, which is why the far corner is one less than the dimension, and it is the convention every widget rect in the executable follows.

The shell has no second video mode. DBSIM ships each panel resource twice, `dba\`/`hba\` and `dfn\`/`hfn\`, and picks between them off a video-mode global; `SHELL0.VOL` ships one set of each, and its screens are authored at the size that set is drawn at.

## The layouts are executable literals, not data

`ServiceBay_BuildScreen` (`0043a944`, `wsrvbayi.cpp`) writes every widget rect as four immediates onto its own stack and hands the block to a widget constructor. The function opens no file: there is no `fopen`, no volume read and no ClassIO stream call anywhere in it. The same shape repeats across the other tab screens' builders.

The `gam\arm_*.dat`, `gam\rpr_*.dat` and `gam\arm_weap.dat` records are the exception, and they are narrower than they look: each positions one *content* panel against a frame of the matching `dba\` sheet ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#the-screen-layout-families)). Nothing in them describes the frame those panels sit in.

## The widget tree of a tab screen

Four levels, built in this order:

1. **The root**, textured with the backdrop bitmap the shell's global init keeps in `DAT_0046dcd4`, sized to its parent's rect rather than to a literal.
2. **A full-screen panel** at `{0, 0, 0x27f, 0x1df}`, which every other widget on the screen is parented to. Because it sits at the origin, a child's rect is also its canvas rect — parent-relative and absolute coincide for everything on the strip.
3. **The palette scope** at `{0, 0x1e, 0x27f, 0x1df}` — the canvas below the strip. It is parented to the shell's top-level window (`DAT_004810e4`, the root's own parent) rather than to the panel, and it is how the screen's palette is chosen; see [The palette](#the-palette).
4. **The strip itself**: one square button and eight tabs.

Widget fields the builders and the tab handlers write directly:

`Control_Ctor` (`00409788`) is the base every one of them goes through. It zeroes `+0x45`, sets `+0x49` to 1 and ORs `0x60` into the flags word at `+0x39`.

| Offset | Meaning |
|---|---|
| `+0x45` | the lit flag. The widget's own mouse handler toggles it 0/1, and the paint picks the button's face from it. A tab handler writes 1 and repaints before building its screen, which is what latches the active tab lit; `0043b0c8` clears it across all nine. On the palette scope, a different class, the same offset is a palette index instead |
| `+0x49` | 1 from the constructor, and the enable flag. The tab gate clears it on the three tabs the training campaign has no economy for, and the repair panel writes it alongside two greying colour fields on a test of whether the player can afford the button ([below](#the-condition-readout)) — moving with the greying, on an affordability test, is what makes it the enable flag rather than a style bit. The button's own paint reads it for one thing, whether the caption takes the pressed nudge; the base class's event handler ignores mouse events while it is clear, so a cleared widget swallows a click on it ([below](#which-widget-a-click-reaches)) |
| `+0x51` | 1 from `Panel_Ctor`, and the gate on drawing any chrome at all: `Panel_FillAndBorder` (0040a726) returns immediately when it is clear. Written 0 on both the root and the full-screen panel, which is how each shows its bitmap with no fill and no border. Not the button field of the same offset — different class, different layout past the base |

## What a tab click does

Every tab has its own handler, and the eight are the same function with three or four lines changed. Each opens by returning if `DAT_0046c08c` is clear, and then by returning if `DAT_0047581c` — which tab is up — already holds its own index: **clicking the tab you are already on is a no-op**, before the teardown, the palette and the sound alike. What follows is, in order:

1. `00439dcb` — clear the lit flag on all nine strip buttons and repaint them.
2. Write `+0x45` back to 1 on this tab and repaint it. **Only tabs 2 to 7 do this**; the main menu's and the save screen's handlers skip it and hide the strip instead ([below](#tabs-0-and-1-hide-the-strip)).
3. `00439ea7` — tear down whatever tab is currently up, dispatching on `DAT_0047581c`.
4. `0043b162(tab)` — install the tab's palette.
5. `0043cfe7(tab)` — build the shared squad roster panel, on tabs 2, 3, 4 and 6 only. Neither ARMORY nor MISSION has one.
6. The screen's own builder.
7. `DAT_0047581c = tab`, then `0042ee89` — the click sound. Tab 6's handler stores its index before its builder instead, so the crew screen's entry already runs as tab 6 ([below](#entering-the-crew-screen)).

| Tab | Handler | Builds | Roster |
|---|---|---|---|
| square button | `00439f2d` | `004317ea` (`wmain.cpp`) | |
| 0 `MAIN MENU` | `00439fdc` | `004310a0`, after `Game_SaveSlot(10, NULL)` | |
| 1 `SAVE` | `0043a0d8` | `00439b0c`, after `DAT_0048d344 = 8` | |
| 2 `WEAPONS` | `0043a1d2` | `0043f548` | yes |
| 3 `REPAIR` | `0043a2d2` | `004332ec` | yes |
| 4 `BUILD` | `0043a3d2` | `0044690d` | yes |
| 5 `ARMORY` | `0043a4d2` | `004494f7` | |
| 6 `CREW` | `0043a5ca` | `00441a01` | yes |
| 7 `MISSION` | `0043a6ca` → `0043a857` | `004441e3` | |

### Tabs 0 and 1 hide the strip

Tabs 0 and 1 take a different route to their palette: instead of `0043b162` they call `00439da0(1)` directly and then `0043b23d`, which **hides** the frame's root (`0048d440`) and the full-screen panel (`0048d448`) and installs palette 1 itself. Every strip button is that panel's child, so the strip goes with it: the main menu and the save screen each stand alone over their own backdrop-textured root, and are left only through their own buttons. The save screen's way back is [its EXIT and RESTORE](#leaving-the-save-screen), which undo exactly this.

Returning to the main menu **autosaves**: `Game_SaveSlot(10, NULL)` is the first thing tab 0's handler does after the teardown, and slot 10 is the campaign-or-training current-game slot ([`../formats/save-games.md`](../formats/save-games.md)).

The mission tab is the one that carries a sub-mode. `0043a857(mode)` stores it in `DAT_0048106c` — 0 the campaign map, 1 the briefing, 4 the debrief — and picks between the first two on whether the mission-within-stage counter `DAT_0046fb1a` is zero. The debrief value is written by the campaign layer at the end of a mission rather than by the tab.

## The tab gate

`0043b0c8` is the strip refresh, and it writes `+0x49` on five tabs from `DAT_0048260c`, the campaign/training mode flag:

| Tab | Campaign | Training |
|---|---|---|
| 2 `WEAPONS` | on | on |
| 3 `REPAIR` | on | off |
| 4 `BUILD` | on | off |
| 5 `ARMORY` | on | off |
| 6 `CREW` | on | on |

The three it gates are exactly the three that spend salvage, and the training campaign has no salvage economy ([`armory.md`](armory.md)). It writes those five and no others, so `MAIN MENU`, `SAVE`, `MISSION` and the square button are live in both. It also shows the strip's panel and parks `DAT_0047581c` at `0xffff`, so whatever tab is clicked next cannot be mistaken for the one already up.

`ServiceBay_BuildScreen` clears `+0x49` on tab 5 as it constructs it, which the refresh then overwrites either way.

## The tab strip

All nine buttons share the top and bottom edges `4` and `0x1b`. The tabs are 75 pixels wide on a 76-pixel pitch — butted one pixel apart.

| Widget | Rect | Caption | Art |
|---|---|---|---|
| square button | `{7, 4, 0x17, 0x1b}` | `?`, the literal at `00475dcd` rather than an `estext.bin` entry | `dba\online.dba` frames 0 and 1 |
| tab 0 | `{0x19, 4, 0x63, 0x1b}` | `0x13` `MAIN MENU` | `dba\mnu_bttn.dba` frames 1 and 2 |
| tab 1 | `{0x65, 4, 0xaf, 0x1b}` | `0x14` `SAVE` | as tab 0 |
| tab 2 | `{0xb1, 4, 0xfb, 0x1b}` | `0x15` `WEAPONS` | as tab 0 |
| tab 3 | `{0xfd, 4, 0x147, 0x1b}` | `0x16` `REPAIR` | as tab 0 |
| tab 4 | `{0x149, 4, 0x193, 0x1b}` | `0x17` `BUILD` | as tab 0 |
| tab 5 | `{0x195, 4, 0x1df, 0x1b}` | `0x18` `ARMORY` | as tab 0 |
| tab 6 | `{0x1e1, 4, 0x22b, 0x1b}` | `0x19` `CREW` | as tab 0 |
| tab 7 | `{0x22d, 4, 0x277, 0x1b}` | `0x1a` `MISSION` | as tab 0 |

Those captions corroborate three of the assert-string module identifications independently: tab 2 `WEAPONS` is the screen `warmingi.cpp` builds, tab 5 `ARMORY` is `warmoryi.cpp`'s and tab 6 `CREW` is `wcrewi.cpp`'s, each reached through that tab's own handler.

`ButtonIcon_Ctor` (`00409d14`) stores three frame pointers, at `+0x51`, `+0x55` and `+0x59`, and **a button has only two faces**: both of the class's paints — `0040a05d` and the subclass's `0040a26d` — pick between the first two and never read the third. Every button on the strip passes `mnu_bttn` frame 3 as that unread third, including the square one whose two faces come from a different bank.

The caption is an embedded `Text` child at `+0x4d`, sized to the button's full client rect, drawn in the font at `DAT_0046dccc` and fetched with `WeaponsBin_LookupName(DAT_0046dcc0, index)` from `estext.bin`. The paint places it one pixel above its vertically-centred row when the button is idle and one below when it is lit, so a lit caption sits two pixels lower than an idle one — the pressed nudge. The centring itself is a baseline, `(Font_CellHeight(text +0xb1) + rectHeight + 1) / 2`: `+0xb1` is the `Text` widget's font handle and `Font_CellHeight` (00453fa8) returns that font's glyph cell height ([above](#text-placement-and-colour)).

The strip is rebuilt by each tab screen's own builder at these same coordinates rather than shared between them.

## Showing and hiding a widget

**The dump's two names are the wrong way round.** `Widget_ShowRecursive` (0041f2e6), which the dump calls `Widget_HideRecursive`, is the **show**; `Widget_HideRecursive` (0041f469), which the dump calls `Widget_ShowRecursive`, is the **hide**. Bit 2 of the flags word at `+0x11` is a *hidden* bit: `Widget_HideRecursive` sets it and `Widget_ShowRecursive` clears it, each returning immediately if it is already in the state it would write.

Three things say so independently:

- **`Text_Paint` (`0040b439`) draws only when `(+0x11 & 2) == 0`.** A widget that paints when the bit is clear is visible when the bit is clear, so the call that sets the bit is the hide.
- **The repair tab's entry and teardown are a matched pair.** `Repair_Enter` (004332ec), which tab 3's handler calls to bring the screen up, calls `Widget_ShowRecursive` on its content panel and both list panels; `Repair_Leave` (004333eb), which the teardown dispatcher `00439ea7` calls on the way out, calls `Widget_HideRecursive` on the same three. Under the dump's names an entry routine would hide its own screen and a teardown would show it.
- **The repair screen's two pictures swap with the selection.** `Repair_SwapDiagram(oldColumn, newColumn)` (0043393d) calls `Widget_ShowRecursive` on the internals diagram when the selection moves into the internals list and on the exploded external picture when it moves back ([below](#the-damage-diagram)) — the right way round only if that function is the show.

`TitledPanel_Ctor` ends by hiding the panel it just built, so a screen's widgets are constructed dark and its entry routine is what puts them up. The edit-field constructor `EditField_Ctor` (0040bbf4) does the opposite and leaves its widget visible.

Both consult the widget's parent, which `Widget_Parent` (0041f283) finds by walking the `+9` chain while a node's bit 1 is clear: `+9` holds the previous sibling, and only on the first child in a list, which has bit 1 set, the parent. The show refuses to run at all while that parent is itself hidden, and the hide sets bit 4 when it is — so bit 2 is the widget's own state and bit 4 records that an ancestor is hiding it as well, which is what lets a subtree come back up in the state it went down in.

## Which widget a click reaches

Input reaches widgets through an event queue, not directly. A mouse change becomes an event of type `0x20` with a sub-code at `+0x24`, which `Mouse_OnButtonState` (00408b03) assigns: 0 a move, 2 and 1 the left button going down and up, 4 and 3 the right. `EventQueue_Pump` (00469ba4) drains the queue.

**A move picks the target and a button event goes to it.** On a move the pump hit-tests from the display's root with `Widget_HitTestTree` (00469d1c). A widget is hit only when it is not hidden (`+0x11` bit 2, [above](#showing-and-hiding-a-widget)) and the point lies inside its absolute rect, edges included. Its children are then tried in list order, and the first that is hit answers in its place, recursively; a widget none of whose children is hit is the answer itself. When the answer changes, `Pointer_Leave` (00469d74) sends the old widget a leave event (`0x10`) and `Pointer_Enter` (00469e1c) sends the new one an enter (8). A button event carries no position test of its own: it goes to whatever the last move left under the pointer, and so does any other event posted without a target, a keystroke included.

**The pointer can be locked.** While `+0x1f` of the pointer state at `DAT_005ddbd0` is set, the pump skips the hit test, so a move changes nothing and every event goes to the current target. Only edit fields set it — `EditField_HandleEvent` on a press ([below](#the-widget-that-takes-a-click-decides-what-it-does)), `SaveScreen_BeginRename` (004377d2) and `0043bc0a` — and `Pointer_Unlock` (00469cdc) clears it and hit-tests again.

**Siblings are tried newest first.** `Widget_AttachChild` (0041f134), which `Widget_SetRect` calls from every constructor, pushes a child onto the *head* of its parent's list. The only other list operation, `Widget_Detach` (0041f17a), is called by two teardowns that delete what they unlink, so no list is ever reordered. Where two siblings overlap, the one built later answers, and a child can be hit only where it lies inside its parent.

**The event then climbs to the first widget that takes mouse events.** `Event_Deliver` (00469f34) walks from the hit through `Widget_Parent` to the first widget whose event mask at `+0x39` has the event type's bit, and calls that widget's vtable slot 0 with the point made relative to the hit. `Widget_SetRect` starts the mask at `0x1f` and `Control_Ctor` adds `0x60`, which carries the mouse bit. `Text_Ctor` is built on `Window_Ctor` rather than `Control_Ctor` and clears `0x18`, leaving `0x07`. **A `Text` therefore never takes a click**: a click on a caption, a label or a value reaches its parent. `Panel` and everything built on it, `Button` among them, goes through `Control_Ctor`, and `EditField_Ctor` adds `0x360` itself, so all of those take clicks.

**The innermost widget is part of the target.** The leave event climbs the same way, so when the pointer moves from one of a row's text columns to the next, the leave sent to the first reaches the row, and puts out a press on it exactly as leaving the row would.

What that decides on the screens ported here:

| Overlap | Who answers |
|---|---|
| a [repair hotspot](#the-damage-diagram) over another | the six body areas are built first and the weapons after, each in index order, so a weapon over a body area, a higher mount over a lower one, a higher area over a lower one |
| rows 13 tall on a 12-pixel pitch — the save list, both repair lists | the lower row owns the shared border line |
| a [crew row](#the-rows)'s texts | the row; its portrait is an image panel of its own carrying the row's handler |
| a row's four [text columns](#a-row-is-four-text-columns), a button's caption | the row, the button |
| the repair screen's five [readouts](#the-repair-screen) | the readout, which is disabled and swallows it |
| the weapons screen's [picture box](#the-weapons-screen) and a picture in it | the box, which is disabled and swallows it; a picture's image panel has no handler and swallows it too |

### The widget that takes a click decides what it does

Slot 0 of a class's vtable is its event handler, and the shell's classes run five different ones. Firing is `Widget_DispatchCallback` (0041f5d4), which runs the handler at `+0x3d` — the constructor's handler argument — or discards the event when there is none. No handler hands an event on to the parent, so a widget with no handler, or a disabled one, swallows a click on it.

| Handler | Classes | Fires on | Needs the press on it | A leave cancels | Tests |
|---|---|---|---|---|---|
| `Control_HandleEvent` (004097da) | `Panel`, `FramedPanel`, `TitledPanel`, `Grid`; `Button` through `Button_HandleEvent` (00409b0f), which adds the press sound | either button's release | yes | yes | `+0x49`, `Avi_Playing`, `MovieQueue_Running` |
| `HatchedDivider_HandleEvent` (0040c3b5) | `HatchedDivider` | either button's release | yes | yes | `+0x49` |
| `ButtonIcon_HandleEvent` (00409df2) | `ButtonIcon`, the tab strip | the left press; the right release | the right button only | no | `+0x49`, `Avi_Playing`, `MovieQueue_Running` |
| `ImagePanel_HandleEvent` (0040b6da) | image panel | the left release | no | — | none |
| `EditField_HandleEvent` (0040beaf) | edit field | the left press | — | — | none |

**`Control_HandleEvent` pairs a press with its release.** A press, either button, lights `+0x45` and repaints; a release with `+0x45` lit fires, then clears it and repaints; the leave event clears it. So the click fires on the release, and only when the button went down on the same widget and the pointer never left it. `HatchedDivider_HandleEvent` is the same function without the two movie flags ([below](#input-while-a-movie-plays)).

**The strip fires on the left press.** `ButtonIcon_HandleEvent` lights `+0x45`, plays the press sound, repaints and fires, all on the press, while its auto-repeat flag `+0x61` is clear, as the constructor leaves it. The left release zeroes `+0x45` without repainting, which is why a tab stays drawn lit after the click that latched it: what is on screen is the paint the tab handler's own write of 1 triggered. The right button falls through to `Control_HandleEvent`: it lights on the press and fires on the release, and then zeroes `+0x45` and repaints after the handler has run. So a tab picked with the right button has latched itself and is then repainted unlit, and right-clicking the tab already up unlights it; recorded in [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md). The leave clears `+0x45` only while `+0x5d` is 0, and `ButtonIcon_Ctor` sets it to 1, so a tab the pointer is dragged off stays lit, and a later right release on it fires it with no press.

**An image panel fires on any left release that reaches it**, wherever the button went down.

**An edit field fires on the left press, and takes the pointer.** With its focus flag `+0xa7` clear, the press sets it, installs a WinTimer alarm for itself (`WinTimer_InstallAlarm`, 0046a0b0, with 500 and 500), [locks the pointer](#which-widget-a-click-reaches) and fires. Every later event then goes to the field, and the next press, landing there with `+0xa7` set, clears the focus, removes the alarm, releases the lock, hit-tests again and posts the press over, so it lands on whatever is under the pointer — the field itself included, which then fires again. The field ignores the right button, and while the lock holds a right click anywhere reaches the field and is dropped.

### Input while a movie plays

`avi.cpp` plays the shell's movies through MCI. `Movie_Enqueue` (0041e29c) adds one to a ten-entry ring at `00485668` when movies are on (`DAT_00482275`) — an id, a rect, a palette index and a callback — and `Movie_PlayQueue` (0041e368) plays the ring out, each entry through `Avi_Play` (0041e01c) with its palette installed and the MISSION tab lit, running the entry's callback after it. The shell's main loop (`FUN_00401525`) calls `Movie_PlayQueue` once a pass, and the startup (`FUN_004012b0`), `Game_ProcessMissionResults` and `FUN_004315ec` also call it straight after enqueuing. The campaign map's `maybe_Mission_Show` and `maybe_Mission_UpdateLocationTab` enqueue and leave the playing to the main loop.

Two flags gate input around them, and the two players are their only writers:

| Flag | Set | Cleared |
|---|---|---|
| `Avi_Playing` (`00470d70`) | by `Avi_Play` as playback starts, with the main window capturing the mouse | as playback ends |
| `MovieQueue_Running` (`00470e70`) | by `Movie_PlayQueue` when it finds the ring holding a movie | when a later call finds the ring empty, and while the insert-CD panel waits for its button |

While `Avi_Playing` is set, the window procedure (`MainWndProc`, 00404a2c) drops both button-ups and the options hotkeys, and a button-down sets `Avi_StopRequested` (`00470d60`), which ends playback, and is dropped too: **a click skips the movie and does nothing else**, as Esc and Space do. Moves are still posted. `Control_HandleEvent` and `ButtonIcon_HandleEvent` also ignore every mouse event while either flag is set, which covers the whole run of the queue and not only the movies in it; the other three handlers do not test them.

`FUN_00444e28` reads `Avi_Playing`, and would enqueue the briefing or debrief movie by the mission tab's sub-mode, but its first instruction after the prologue jumps to its epilogue.

## How a widget paints

**A widget carries its rect twice.** `Widget_SetRect` (`0041eb5c`) stores the constructor's rect verbatim into `+0x25`/`+0x29`/`+0x2d`/`+0x31` — left, top, right, bottom, **parent-relative** — and `FUN_0041ef45` derives the absolute rect into `+0x15`/`+0x19`/`+0x1d`/`+0x21`. `FUN_0041ec33` shows the relation directly: it adds the parent's `+0x15` to a child's `+0x25` to get the child's `+0x1d`. Only `Widget_MoveRect` (0041ebef) moves a widget afterwards, and it rewrites the relative pair and rederives the absolute one.

The paints in the table below are the visual vocabulary of the screens ported so far. All of them work in **widget-local coordinates**, where the extent they draw against is `+0x2d - +0x25`. Because that is a difference it is the same in either space — one less than the inclusive width — so a paint never reads an origin at all: `Widget_BeginPaint` (0041f585) opens every one of them and binds the drawing context to the widget's absolute rect and clips to it.

**Colour is always a palette index**, taken from a widget field, and the drawing context carries a `{mode, colour}` pair: mode 0 at `+0x22c` is a solid fill, mode 6 is a blit through a 256-entry lookup table. `Gfx_FillRect` (00457364) fills a rect, `Gfx_DrawLine` (004552e4) draws a line between two inclusive endpoints, and `Gfx_PlotPixel` (0045999c) plots one pixel.

**The border is a chamfer.** `Panel_FillAndBorder(widget, fill)` optionally clears the interior to a literal `0x10` — the shell's one background colour, in every paint that fills — and then draws four edges each stopping one pixel short at both ends, so the true corners stay empty, and paints the four pixels one step *inside* those corners instead. That clipped-corner box is every panel and every button in the shell. It draws nothing at all when `+0x51` is clear, which is how a screen's backdrop-textured root shows its bitmap and no chrome.

| Class | Paint | What it adds |
|---|---|---|
| `Panel` | `0040a959` | nothing — the fill and the chamfered border alone |
| `FramedPanel` | `0040a9a6` | a 50% checkerboard over the interior in `+0x55` |
| `TitledPanel` | `0040ac27` | the header strip, its hatch and title plate, a divider, and a dithered *or* filled body |
| `Text` | `0040b439` | one string, aligned, with an optional backing fill |
| edit field | `0040c14f` | one editable string, left-aligned, with an optional caret |
| image panel | `0040b772` | one bitmap at an offset, under an unfilled border ([below](#the-crew-screen)) |
| `HatchedDivider` | `0040c513` | a body of horizontal lines and an optional inner border ([below](#the-crew-screen)) |
| `Grid` | `0040b97c` | grid lines and thirty recolourable bitmap parts ([below](#the-damage-diagram)) |

`TitledPanel_Paint` fills its header strip to `+0x55` for `+0x61` rows, then — when `+0x65` is set, which the constructor does and nothing clears — lays a **diagonal hatch** over it in colour 13: bands of fourteen 45-degree lines on a 28-pixel pitch, 26 bands from five pixels left of the widget. That is over 700 pixels of hatch for a panel a third as wide, and only the clip stops the surplus; the paint relies on clipping rather than measuring. It then punches the hatch back out to `+0x55` between `+0x6d` and `+0x71`, which is the **title plate** the caption reads against, draws the header's own side edges, and closes with a divider on row `+0x61`.

**`+0x59` chooses between a filled body and a dithered one.** Set, the body is cleared to `0x10`; clear, it takes a 50% checkerboard in `+0x5d` from the header height down — and over an unpainted surface that means the shell's single backdrop bitmap shows through at half strength. The save screen takes the second path, which is why the bay is visible through its panel.

### Text placement and colour

`Font_DrawString(font, {x, y}, text)` (0045409c) draws a run: it tests the context's `+0x231` for 1 and then for 2, so **0 is left, 1 is right and 2 is centred**, against the field width at `+0x235`; then it advances glyph by glyph. `Text` takes that mode from its own `+0x45`, so a label's alignment is a constructor argument. Every field label on a screen is right-aligned and its value left-aligned, which is what makes a label's colon meet its value.

The `y` is an **ink baseline**: `Font_DrawGlyph` (00453fb4) places each glyph's top row at `y - font[+0x16]`, and `+0x16` is the `.DFN` header's `inkHeight`. `Font_CellHeight` and `FUN_00453f9c` both return `font[+0x0a]`, the glyph cell height. The two classes centre differently and neither is derived from the other — `Text` uses `H - (H + 1 - cellHeight) / 2 - 2` and the edit field `cellHeight / 2 + (H + 1) / 2`.

**A widget picks its text colour by remapping, not by choosing a pen.** The fonts carry one ink index each ([`../formats/dfn-hfn-dci.md`](../formats/dfn-hfn-dci.md)) and the shell's is `0x29`, so both text paints draw the string in whatever the font has and then re-blit the area through an identity table with entry `0x29` replaced — by `Text`'s `+0xb5`, or the edit field's `+0xbb`. One font therefore serves a label at `0x1a`, a value at `0x29` and a resting list row at `0x27`, and a selection highlight costs nothing but a different replacement.

`Text`'s `+0xc1` is an opaque-background flag and `+0xc5` the colour it clears to: a value field clears its own rect so a refresh overwrites cleanly, and a static label does not.

## The save screen

Tab 1, `SAVED GAMES`. Built by `SaveScreen_BuildScreen` (004385b0), entered by `SaveScreen_Enter` (00439b0c), its selection moved by `SaveScreen_SelectSlot` (0043795f) and its detail panel refilled by `SaveScreen_RefreshDetail` (0043712c). Every rect is four immediates on the builder's stack, and they are **parent-relative**: the panel sits in the canvas, the list and the button column in the panel, and two buttons in the list.

| Widget | Class | Rect (in its parent) | Content |
|---|---|---|---|
| root | `0040b698` | its parent's own rect | the shared backdrop; `+0x51 = 0`, so no chrome |
| content panel | `TitledPanel` | `{0x8e, 0x7f, 0x1f2, 0x1d4}` | `0x1b` `SAVED GAMES`, header 19 tall, plate `0x70`-`0xf3` |
| slot list | `FramedPanel` | `{9, 0x1c, 0x15b, 0xc6}` | |
| 10 slot rows | edit field | `{10, i*12 + 0x13, W-2, i*12 + 0x1f}` | the slot's `GAMEFILE.STR` label |
| `CANCEL` | `Button` | `{0x43, 0x93, 0xa5, 0xa2}` in the list | `0x33` |
| `ACCEPT` | `Button` | `{0xb0, 0x93, 0x112, 0xa2}` in the list | `0x34` |
| `SAVE` | `Button` | `{9, 0xe5, 0x6b, 0xf4}` | `0x1d` |
| `RESTORE` | `Button` | `{9, 0xfb, 0x6b, 0x10a}` | `0x1e` |
| `EXIT` | `Button` | `{9, 0x111, 0x6b, 0x120}` | `0x1f` |
| detail panel | `FramedPanel` | `{0x74, 0xcc, 0x15b, 0x150}` | 22 `Text` children |

The panel is centred on x=320 rather than on the canvas's own inclusive midpoint, so its left margin is 142 and its right 141. The builder overwrites three class defaults on it: `+0x59` to 0 for the dithered body, `+0x5d` to `0x10`, and `+0x55` on all three framed panels to `0x10` — which flattens their checkerboard, since it then dithers the interior colour over itself.

**The rows are 13 tall on a 12-pixel pitch**, so each overlaps its neighbour's border row, and the first and last are inset two pixels further from the left edge than the eight between them. Their right edge is computed from the list panel's absolute corners rather than written, at two pixels inside it. Each carries a permitted-character set at `+0x9f` — `"0123456789abcdefghijklmnopqrstu…"` in place of the class's `"ABCDEFGHIJKLMNOPQRSTUVWXYZ"` — so **renaming a slot is typing into its row**, and `CANCEL`/`ACCEPT` are that edit's two buttons rather than the screen's. The builder clears `+0xb3` and `+0xbf` on every row, so a row shows no caret until [a rename](#saving-is-a-rename) sets both. It also writes `+0xb7 = 4` on every row ([Open](#open)). Selection is `+0xbb`: `0x27` resting, `0x29` selected.

Three buttons are gated, each written as the trio [the repair panel uses](#the-condition-readout): `SAVE` on a row being selected and there being a game to write (`DAT_0048260a`), `RESTORE` on the selected slot's in-use byte, and both `CANCEL` and `ACCEPT` on the rename being live. `EXIT` never gates.

### Saving is a rename

`SAVE` writes nothing. Its handler (`00437bd3`) starts a rename of the selected row, and `ACCEPT` is what writes the save. `DAT_00474f40` is the rename's state: 0 idle, 2 while a rename is live, and 1 only transiently inside `SAVE`'s handler.

| Handler | What it does |
|---|---|
| `SAVE`, `00437bd3` | edit state to 1; greys `SAVE`, `RESTORE` and `EXIT`; calls `004377d2` |
| `004377d2` | returns at once if the edit state is 0. Otherwise posts an event at the selected row, moves the pointer onto the row (`Pointer_SetTarget`, 00469cbc) and [locks it there](#which-widget-a-click-reaches), so keystrokes reach the row; sets its caret flags `+0xbf` and `+0xb3`; rewrites it as `"%2d. %s"` of the slot number and the empty string at `0047526a`, so it reads ` 3. ` with the name gone; lights `CANCEL` and `ACCEPT`; edit state to 2; repaints the list |
| `ACCEPT`, `00437ffa` | `Game_SaveSlot(selected, text)`, where the text is the row's own string buffer at `+0x45` (`00437ba9`), so what was typed becomes the slot's label; `Stats_StageCurrentGame(selected)`; refreshes the detail panel; greys `CANCEL` and `ACCEPT`, lights `SAVE`, `RESTORE` and `EXIT`; edit state to 0 |
| `CANCEL`, `00437e1a` | puts the row's `GAMEFILE.STR` label back; greys `CANCEL` and `ACCEPT`, lights `SAVE`, `RESTORE` and `EXIT`; edit state to 0; `SaveScreen_SelectSlot(10)` |

Edit state 2 is what `SaveScreen_SelectSlot` refuses, so the selection cannot move off the row being renamed. `ACCEPT` and `CANCEL` light the three buttons without their usual tests. After `CANCEL` that does not last: the closing `SelectSlot(10)` deselects the row and regates `SAVE` and `RESTORE`, both dead with no row selected. After `ACCEPT` the selection stays on the slot just written.

### Leaving the save screen

With [the strip hidden](#tabs-0-and-1-hide-the-strip), `EXIT` and `RESTORE` are the only ways off the screen. Both open with `maybe_SaveScreen_Teardown` (00439d66), which parks the selection on slot 10 — past every row, so the screen next comes up with nothing selected and `SAVE` and `RESTORE` both dead — and hides the screen's root, content panel and both detail panels.

`SaveScreen_OnExit` (00437d94), `EXIT`'s handler, then goes where `DAT_0048d344` says. 0 rebuilds the main menu; `00431498` writes it, after setting the campaign mode flag to 1 — the handler of a button `0043094c` builds with `estext.bin` caption 6. 8 is `0043b162(8)` then `0043b0c8` — show the frame's root, show and regate the strip, park the current tab at `0xffff` — which leaves the bare frame up with no tab current and nothing lit; tab 1's handler writes it.

`RESTORE`'s handler (`00437d03`) is:

```
Game_LoadSlot(selectedSlot, 1)   // the whole save, and its career files into data\
Game_SaveSlot(10, NULL)          // straight back out as the current-game autosave
teardown
DAT_004778aa = 0
0043b162(8); 0043b0c8()          // EXIT's tab-strip path, whatever DAT_0048d344 holds
```

`Game_LoadSlot` sets `DAT_0048260a`, so `SAVE` is live from then on, and does not touch the campaign mode flag, so the slot-10 write lands in `GAME_R.SAV` or `GAME_T.SAV` by whichever mode the shell is already in ([`../formats/save-games.md`](../formats/save-games.md)). `DAT_004778aa` is the campaign map's once-per-load flag: `maybe_Mission_Show` (004441e3) runs two `FUN_0041e29c` calls in its map arm only while it is clear and then sets it, and `TabHandler_Mission` (0043a6ca) opens the map rather than the briefing only while it is clear and the mission-within-stage counter is zero.

### The detail panel

`SaveScreen_RefreshDetail` writes 12 value fields from the selected slot's [staging record](../formats/save-games.md#the-slot-summary--statscpps-scan), or from `estext.bin` entry 0 — the empty string — when the slot holds no save, so the labels stay and the figures blank.

| Row | Label | Value |
|---|---|---|
| `0x06` | `0x24` `Name:` | the pilot's name |
| `0x12` | `0x25` `Skill:` / `0x26` `Rank:` | `0x35 + skill` and `0x39 + rank`, two runs of four words |
| `0x24` | `0x2a` `Current` / `0x2b` `Total` | column headers, centred over the grid below |
| `0x30`-`0x48` | `0x27`-`0x29` Herc / Flyer / Base `Kills:` | six counters, per-mission in the left column and career totals in the right |
| `0x5a` | `0x2c` `Salvage:` | the pool in kilograms divided by 1000, then `0x2f` `Tons` |
| `0x66` | `0x2e` `Sector:` | `0x76 + stage` |
| `0x72` | `0x2d` `Mission:` | the counter plus one |

The label indices run out of layout order: `Salvage:`, `Sector:` and `Mission:` are drawn in that order from `0x2c`, `0x2e`, `0x2d`.

**The sector run is a fourth witness that the stage counts from one at runtime.** `0x76` is `Razor`, a chassis name; the five sector words `Alpha`, `Delta`, `Omicron`, `Bravo`, `Luna` start at `0x77`. Stage 1 lands on the first of them, and the save stores the stage from zero ([`campaign-loop.md`](campaign-loop.md)).

## The weapons screen

Tab 2, `WEAPONS`, the arming screen. Built once by `Arming_BuildScreen` (`0043e52c`, `warmingi.cpp`), entered by `Arming_Enter` (`0043f548`) and hidden by `Arming_Leave` (`0043f5d2`), the teardown dispatcher's arming arm. Rects are parent-relative. The left of the canvas is [the squad panel](#the-squad-panel), filled with the three-quarter [bay picture](#the-bay-picture).

| Widget | Class | Rect (in its parent) | Content |
|---|---|---|---|
| content panel | `TitledPanel` | `{0xf1, 0x2b, 0x278, 0x1d9}` | `0x9f` `Weapons`, header 19 tall, plate `0x66`-`0x116`, face `0x25`, filled body |
| inventory | `TitledPanel` | `{6, 0xc5, 0x183, 400}` | `0xa0` `Weapons Inventory`, header 19 tall, `+0x65 = 0` |
| picture box | `HatchedDivider` | `{6, 0x1a, 0x183, 0xc0}` | border `0x15`, `+0x55 = 0x76`, `+0x59 = 0x25`, `+0x49 = 0` |
| 26 weapon pictures | image panel | each `gam\arm_weap.dat` record's corner in the box, sized to its frame | that frame of `dba\arm_weap.dba` |
| 4 guidance pictures | image panel | the same, from the file's second list | |
| blank picture | `Panel` | `{0x14, 0x1a, 0x14a, 0x5f}` in the box | `+0x51 = 0` |
| `ARM`, `ARH`, `SARH`, `EO` | `Button` | `{0x152, 5, 0x179, 0x16}`, then `0x17` lower three times, in the box | `0xa1`-`0xa4`, border `0x22`, hidden |
| rack button | `Button` | `{0x101, 0x61, 0x179, 0x72}` in the box | built blank, border `0x22`, hidden |
| 3 description lines | `Text` | `{1, 0x7a, W, 0x88}`, `{1, 0x88, W, 0x94}`, `{1, 0x94, W, 0xa0}` in the box | centred, `0x29`, opaque in `0x25` |
| 13 rows | `Panel` | `{9, i*0xd + 0x14, 0xaf, i*0xd + 0x20}` in the inventory | border `0x10` |
| 14 rows | `Panel` | `{0xbf, i*0xd + 0x14, 0x165, i*0xd + 0x20}` in the inventory | border `0x10` |
| label | `Text` | `{0xe, 0x195, 0x76, 0x1a1}` | `0xa5` `Hard Points`, right, `0x29` |
| `<` | `Button` | `{0x7d, 0x194, 0x8b, 0x1a3}` | border `0x22` |
| `>` | `Button` | `{0x91, 0x194, 0x9f, 0x1a3}` | border `0x22` |

`W` is the box's own width, `+0x2d - +0x25`. The `ARM` to `EO` buttons step down by `0x17`, so the four are 18 tall with five rows between them.

**The picture box is black over a grey band.** Its line fill starts on row `0x76` in `0x25`, which makes everything below that row a solid band and leaves the interior above it at the paint's own `0x10` ([below](#the-crew-screen) for the class). The pictures sit in the black, the description lines in the band. The box's enable flag is cleared, so a click anywhere on it that no button takes is swallowed.

**A picture is a bare bitmap.** Each is placed at its `arm_weap.dat` record's corner and sized `{x, y, x + width, y + height}` to its frame, and the builder writes `+0x51 = 0` on it, which `ImagePanel_Ctor` has already done: that stops [the image panel's paint](#the-crew-screen) drawing a border, and the bitmap is blitted regardless. The weapon pictures are indexed by row through `Arming_RowOfWeapon` (`0043f6f7`) into `0048d5a0`, the guidance pictures by kind into `0048d608`. `None` has no record and shows the blank picture, a `Panel` whose cleared `+0x51` leaves it drawing nothing.

### The inventory rows

The rows list the 27 weapon ids of the table at `004769b0` — `arm_weap.dat`'s 26 in the file's own order, then `None` (0) — thirteen down the left and fourteen down the right, 13 tall on a 13-pixel pitch. Each is a [four-column row](#a-row-is-four-text-columns) cut at `0x8b`, `0x8c` and `0x8d`: the name, `estext.bin` `0x7e + id`, left from `2`; two one-pixel columns holding a space; and the count right-aligned from `0x8d` to one inside the row.

`Arming_RefreshRows` (`0043fbc6`) regates all 27 on every row selection. The count is `"%d"` of the weapon's owned count, `weapons.dat` record `+0x17`, or two spaces for `None` ([`../formats/weapons-dat.md`](../formats/weapons-dat.md#weaponsdat-catalog-record-29-bytes)). A weapon whose unlock flag `+0x16` is clear has its row disabled and its four columns set to `0x10`, the background, so the list shows a gap and keeps the builder's `"0"` where the count would be. An unlocked weapon's row takes `0x27` and is live while `Arming_RowLive` (`004149fb`) holds: always with no hardpoint selected, and with one only when the chassis's armory layout has a part for that weapon at `slot + 2`, the socket [the bay picture](#the-bay-picture) draws it in. A row that fails is disabled in `0x25`.

### Selecting a row

`Arming_SelectRow(row)` (`0043f71c`) is each row's handler, through 27 thunks from `00440300`, each of which sets `DAT_00476d58` for the length of the call. It does nothing for the row already lit (`DAT_00476d5a`) unless a guidance picture has been put up since (`DAT_00476d5c` not `-1`) or a hardpoint is selected. Otherwise it:

1. With a hardpoint selected, refuses a weapon [it will not fit](#fitting-a-weapon), and `Arming_FitSelected` (`0043dc44`) fits the one it accepts.
2. Runs `Arming_RefreshRows`, then puts the old row back to `0x27` or `0x25` by `Arming_RowLive` with its border `0x10`, and hides its picture and any guidance picture that is up.
3. Lights the new row: border and all four columns `0x29`, enabled whatever its unlock flag says. Shows its picture, or the blank one for `None`, and fills the three description lines from `wpn_desc.bin` entries `id * 3` to `id * 3 + 2`.
4. For ids `0xd` to `0x10` — the three missile racks and the Razor's launcher — captions the rack button with the weapon's name and shows the five buttons (`Arming_ShowGuidanceButtons`, `0043f5f7`, once, on `DAT_004769e6`); with a hardpoint selected it then runs `Arming_ShowGuidance` with that mount's kind and its second argument clear, which lights the kind's button and leaves the pictures alone. Any other id runs `Arming_HideGuidanceButtons` (`0043f644`), which puts the four guidance borders back to `0x22` and hides all five, and sets `DAT_00476d5c` to `-1`.
5. Stores the row in `DAT_00476d5a`.

A missile rack's row therefore leaves `DAT_00476d5c` where it was, with its picture hidden, so clicking the rack's row again runs the selection through.

### Guidance kinds

The four buttons call `Arming_ShowGuidance(kind, 1)` (`0043fd69`) with `ARM` 2, `ARH` 1, `SARH` 0 and `EO` 3 — the ids a fitted mount's record carries at `+0x08`, where the arming code reads 5 for an empty mount. It relights the borders, `0x22` on the kind in `DAT_00476d60` and `0x20` on the new one; with its second argument set it hides the lit row's picture, shows the kind's, fills the description lines from `wpn_desc.bin` `99 + kind * 3`, and stores the kind in `DAT_00476d5c`. It always stores the kind in `DAT_00476d60`, and `Arming_SetMountGuidance` (`0043dcb6`) writes it into the selected hardpoint's mount at `+0x08` when there is a hardpoint and a mount there.

The rack button's handler (`0044012a`) runs `Arming_SelectRow` on the lit row, which a guidance picture being up lets through: it puts the rack's own picture and description back.

`wpn_desc.bin` is three lines per weapon id: the 33 ids fill entries 0 to 98, and the four kinds 99 to 110 in id order.

### Entering the weapons screen

`Arming_Enter` shows the content panel, the inventory and the picture box. When the selected bay is `-1`, empty or holds a machine still being built, it calls `Squad_SelectBay` (`0043d64d`) with `Herc_FirstBuiltBay` (`00410c2a`). It then clears the hardpoint (`ArmingSelectedHardpoint`, `0xffff`) and runs `Arming_SelectRow(0)`. **So the screen opens on the first row, `AutoCannon 20mm`, with no hardpoint selected**, and a row click from there shows the weapon and fits nothing.

The tab handler stores 2 in `DAT_0047581c` only after the entry ([above](#what-a-tab-click-does)), so that `Squad_SelectBay` takes the arm of the tab being left. Every arm takes a finished machine, except that the crew arm refuses the Razor while the crew screen's selected row is not the player's, which leaves the screen with no bay. Recorded in [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

**This tab's arm of `Squad_SelectBay`** refuses an empty bay and an unfinished machine and accepts `-1`. It clears the hardpoint, clears part slot 12 of the old bay's picture — the slot `Arming_MarkHardpoint` (`004155db`) puts the selected socket's outline in — runs `Arming_SelectRow(0)`, and then swaps the bay pictures, relights the two roster rows, stores `DAT_00482ae5` and refreshes the readout.

### Fitting a weapon

A hardpoint is selected by `Arming_SelectHardpoint` ([below](#the-arming-and-repair-hotspots)), which the ten arming hotspots and the two steppers reach: `<` (`00440244`) calls `Arming_PreviousHardpoint` (`0043dd49`), which wraps from 0 to the last mount, and `>` (`004402a2`) calls `Arming_NextHardpoint` (`0043dd09`), which steps modulo the mount capacity `+0x4c`. It selects the row of the mount's fitted weapon and runs `Arming_MarkHardpoint`, which redraws the socket's weapon part and puts an outline in part slot 12 from a second per-chassis bank, remapping `0xba` to `99`.

With a hardpoint selected, `Arming_SelectRow` refuses a weapon the armory holds none of unless the mount already carries it; `None` is never refused. The test reads the mount's guidance kind at `+0x08` where the weapon id at `+0x00` belongs before it reads the fitted id, so a mount whose kind number equals the weapon's id skips the refusal. `Arming_FitSelected` then calls `FUN_004114ec(herc, hardpoint, weapon)` only while `DAT_00476d58` is set, which is only inside a row's own thunk — the entry and the hardpoint steppers select a row without fitting it — and in every case redraws the socket ([Open](#open)).

## The repair screen

Tab 3, `REPAIR`. Its widgets are built once by `Repair_BuildScreen` (00432037), the screen is brought up by `Repair_Enter` and taken down by `Repair_Leave`, its rows are filled by `Repair_FillRow` (004339b2), its component names set by `Repair_SetComponentNames` (00433cdf), its selection moved by `Repair_SelectHotspot` (00433eb9) and its three readout panels refilled by `Repair_RefreshDetail` (00433445). Every rect is four immediates on the builder's stack and they are parent-relative, as everywhere else.

The content panel takes the right two thirds of the canvas. The left is the [damage diagram](#the-damage-diagram) and the [squad panel](#the-squad-panel); of those this builder owns only the eight internals pictures.

| Widget | Class | Rect (in its parent) | Content |
|---|---|---|---|
| 8 internals pictures | `Grid` | `{0x10, 0x2f, 0xe0, 0x12f}` in the canvas | one per bay, [below](#the-damage-diagram) |
| content panel | `TitledPanel` | `{0xf1, 0x2b, 0x278, 0x1d9}` | `0x3d` `REPAIR`, header 19 tall, plate `0x7f`-`0x10a`, face `0x25` |
| external list | `TitledPanel` | `{4, 0x1a, 0xe5, 0x105}` | `0x3e` `External`; `+0x65 = 0`, so no hatch and no plate |
| 6 group rows | `Panel` | `{0xc, i*0xc + 0x1c, 0xd5, i*0xc + 0x28}` | `0x4e`-`0x53` |
| 10 hardpoint rows | `Panel` | `{0xc, i*0xc + 0x72, 0xd5, i*0xc + 0x7e}` | built blank |
| internal list | `TitledPanel` | `{4, 0x10b, 0xe5, 0x1a3}` | `0x3f` `Internal`, same header |
| 9 component rows | `Panel` | `{0xc, i*0xc + 0x1c, 0xd5, i*0xc + 0x28}` | `0x54`-`0x5c` |
| mode label | `Text` | `{0xef, 0x1a, 0x179, 0x26}` | `0x40` `Mode:` |
| mode readout | `Button` | `{0xff, 0x2b, 0x168, 0x3e}` | `0x41` `Manual Repair` or `0x42` `Auto Repair` |
| salvage label | `Text` | `{0xef, 0x48, 0x179, 0x54}` | `0x43` `Salvage Available:` |
| salvage readout | `Button` | `{0xff, 0x59, 0x168, 0x6b}` | the pool, net of the build queue |
| item panel | `FramedPanel` | `{0xef, 0x77, 0x179, 0xf4}` | `0x4b` `Selected Item` |
| item cost | `Button` | `{0xf, 0x24, 0x78, 0x36}` in it | under `0x49` `Salvage Required:` |
| item condition | `Button` | `{0xf, 0x4b, 0x78, 0x5d}` in it | under `0x4c` `Condition:` |
| `REPAIR` | `Button` | `{0xf, 0x68, 0x78, 0x77}` in it | `0x4d` |
| total panel | `FramedPanel` | `{0xef, 0xfb, 0x179, 0x14f}` | `0x48` `Total` |
| total cost | `Button` | `{0xf, 0x24, 0x78, 0x36}` in it | under `0x49` again |
| `REPAIR ALL` | `Button` | `{0xf, 0x3f, 0x78, 0x4e}` in it | `0x4a` |
| scrap panel | `FramedPanel` | `{0xef, 0x156, 0x179, 0x181}` | `0x47` `Scrap Herc` |
| `SCRAP` | `Button` | `{0xf, 0x17, 0x78, 0x26}` in it | `0x46` |
| `CANCEL` | `Button` | `{0x112, 0x193, 0x156, 0x1a2}` | `0x44` |

Each row carries a click handler from the 25-thunk table at `0048d1f8`, one per `(column, row)` pair, and what that pair means and which clicks are refused are [below](#the-arming-and-repair-hotspots).

**Five of the "buttons" are readouts.** A `Button` is constructed with a border colour and an enable flag, and the mode, salvage, item cost, condition and total cost boxes are all built disabled with border `0x13` and caption `0x17` where a live button takes `0x22` and `0x29`. They are boxes with a figure in them, and a click on one stops there and does nothing ([Which widget a click reaches](#which-widget-a-click-reaches)). Four of the five also set the caption's `+0xc1`, so each clears its own rect before drawing and a refresh overwrites the last figure cleanly; the mode box, which changes only with the mode flag, does not.

The three `FramedPanel`s keep the constructor's `+0x55` of `0x25`, so their bodies carry a visible checkerboard — where the save screen flattens its own to `0x10`. The content panel keeps `+0x59` at the constructor's 1, so its body is filled rather than dithered and no backdrop shows through it.

**The condition readout's damage colour never reaches the screen.** `Repair_RefreshDetail` looks the band colour up with `Repair_DamageLevelColor` (0043da0f) and writes it into that widget's `+0xb5`, and then calls `Text_SetString(widget, word, 2, 0x17, 1)` — which sets `+0xb5` from its fourth argument before painting, so the box is drawn in the same grey as every other readout. The write is dead. The *list rows* are colour-coded, because `Repair_FillRow` passes the band colour to `Text_SetString` rather than writing it beside the call. Read from the decompile only; `Text_SetString`'s argument order is corroborated by `Text_Ctor` and by `Repair_Enter`, both of which pass a widget's own `+0x45` and `+0xb5` back in to mean "keep what is there".

### A row is four text columns

`ListRow_AddColumns(panel, font, t0, …)` (0040a310) gives a row `Text` children at `+0x55`, `+0x59`, `+0x5d` and `+0x61`, each spanning from the previous one's right edge to its own, and the repair screen passes the same four edges for all 25 rows: `2`-`0x94` left-aligned, a zero-width second column that is never written, `0x94`-`0xab` centred, and `0xab` to two inside the row's right edge, right-aligned. So a row reads as a name, a number and a percentage.

`Repair_FillRow(column, row)` fills one. The name column is the component's, the last column is `HercStatus_Get` (00411d06)'s reading of it formatted `"%d%%"`, and the colour that last column is drawn in is the damage band's ([below](#the-condition-readout)). A hardpoint row additionally puts the mount number, one-based, in the third column, and takes its name from the fitted weapon — `estext.bin` `0x7e + id`, or `0x7d` `--Empty--` for an empty mount, whose percentage is replaced by the string at `0047465c` — two bytes, `20 00`, a single space, so the column reads blank rather than showing the 100 an empty slot's condition entry actually holds. **A row past the machine's mount capacity is blanked**, all three columns set to the string table's single space, rather than left showing the last machine's fitting.

### Which names a chassis shows

`Repair_SetComponentNames` holds two fifteen-entry tables of `estext.bin` indices — six group names then nine internal names — and picks the second **when the machine's chassis type is 8**, the Razor. It is a per-chassis substitution of all fifteen names at once, not a per-component list.

```
00474000   4e 4f 50 51 52 53  54 55 56 57 58 59 5a 5b 5c   walker
0047401e   4e 5d 5e 5f 60 61  62 63 56 57 58 59 5a 5b 5c   Razor
```

The Razor spends each of `0x5d`-`0x63` exactly once: the two torsos become nacelles, the chassis a fuselage, the legs wings, and the leg servos wing servos. Its cockpit and its last seven internals are the walker's. The condition arrays behind them are unchanged — a Razor's thirteen external facets group the same six ways.

It is driven by the bay selection rather than by screen entry: `Squad_SelectBay` (`0043d64d`) calls it when the selected bay changes, and `Repair_Enter` reaches it only when the bay it opens on holds nothing it can work on. The builder's own construction-time indices are the walker set, `0x4e + row` and `0x54 + row`.

### The damage diagram

Both pictures are `Grid` widgets (`Grid_Ctor`, 0040b7e0): a filled panel with a `0x22` border, grid lines every 16 pixels in `+0x6e6` = `0x22` while `+0x6e5` is set, and thirty 56-byte part slots from `+0x55`. `ESGrid_SetPart` (0040b8cf) writes a slot: a position, a frame, blit flags at `+0x89`, and ten colour remap pairs — a source index at `+0x61` and a target at `+0x75`, the target defaulting to `0x10`. `Grid_Paint` (0040b97c) draws the panel and the lines, then blits the parts in slot order, and after each one fills the part's rect, `{x, y, x + width, y + height}`, through a lookup table that is the identity except for each pair whose target is not `0x10`. **A part's colour is chosen at paint time, and by rect rather than by mask**, so a recoloured part also recolours the matching pixels of any earlier part it overlaps.

`Repair_BuildDiagrams` (004140a9) fills both pictures for all eight bays:

- **`0048d4bc[bay]`, the exploded external picture.** These are the squad panel's eight pictures, built by `Squad_BuildRosterList` at `{5, 0x2b, 0xeb, 0x130}` and moved here to `{0x10, 0x2f}` and sized `0xd0` by `0x100`, with their grid lines on. The sizing writes the far corner as `origin + size - 1` ([below](#the-bay-picture)), so they end at `{0xdf, 0x12e}`, a pixel short of the internals pictures' literal `{0xe0, 0x12f}`. Each `gam\rpr_*.dat` body record becomes the part in the slot its id names, from frame `+0x12` of `dba\rpr_<chassis>.dba` with the record's flags, remapping index `0xe`. Each fitted mount then adds the record `RepairLayout_FindWeaponPart` (`00413ccc`) finds in the weapon's group with id `slot + 6`, from `dba\rpr_wpns.dba`, remapping `0xf`; a weapon with no record for that socket draws nothing.
- **`0048d118[bay]`, the internals picture.** One part in slot 0 from the chassis's single internals record, drawing its frame of `dba\<chassis>_int.dba` with flags 0. The Razor gets a second part in slot 1: frame 1 of the same bank at `(0x1d, 0xe)`. The decompiler shows that handle as a global of its own, `0046fe0c`; it is entry 8 of the bank cache at `0046fdec`, the one the Razor's first part was just loaded into.

The blit flags are 0 or 2 in every retail record, and 2 is the mirror: each left/right pair is one frame placed twice.

**The two sides disagree.** On every chassis with torsos, group 1 (`Left Torso`) sits on the viewer's left and group 4 (`Left Leg`) on the viewer's right, with 2 and 5 opposite them; the Razor's nacelles and wings split the same way. The `rpr_hots.dat` areas follow the layout records, so the colours and the clicks are consistent with the picture and with each other, and only the pairing of sides is wrong. Recorded in [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

`Repair_ColorDiagram` (`0041469a`) colours whichever picture the selection's column has up, and `Repair_RefreshDetail` calls it on every refresh:

| Picture | Slot | Remap | Target |
|---|---|---|---|
| external | each body record's id | `0xe` | the band colour ([below](#the-condition-readout)) of group `id`, or `id - 0x10` for an id past 15 |
| external | `6 + slot`, each mount below the capacity | `0xf` | the band colour of that hardpoint |
| internals | 0 | the nine indices at `0046fe80`, `1d 1c 18 19 17 1e 16 1f 1b` | the band colour of internal 0-8, in list order |

Ids past 15 are the Razor's: its twelve body records are two parts per group, 0-5 and 16-21.

**Which of the two is up follows the selection.** `Repair_SwapDiagram` shows the internals diagram when the selection moves into the internals list and the exploded picture when it moves back, so the picture always matches the list being worked in. With no bay selected the squad panel shows its empty picture, `DAT_0048d4dc`, instead: the same widget at the builder's rect with its grid lines off.

`Hotspots_BuildOverlay` (0043c1a0) lays the clickable areas over each bay's exploded picture: the chassis's six `gam\rpr_hots.dat` areas as handlers 0-5, and for each fitted mount a panel over the weapon part's own rect (`Repair_WeaponPartRect`, `00414418`) as handler `6 + slot`. Every one is a `Panel` with `+0x51` cleared, so none of them draws. They are children of the external picture, so while the internals picture is up there is nothing on the diagram to click. Where two overlap — a weapon part over a body area, on several chassis — the one built later answers ([Which widget a click reaches](#which-widget-a-click-reaches)).

## The squad panel

`wsquadi.cpp`'s per-slot panel, built once by `Squad_BuildRosterList` (0043c999) and put up by `Squad_ShowPanel` (0043cfe7) on the WEAPONS, REPAIR, BUILD and CREW tabs. Every rect is a canvas literal.

| Widget | Class | Rect | Content |
|---|---|---|---|
| 8 pictures | `Grid` | `{5, 0x2b, 0xeb, 0x130}` | one per bay; filled and moved by the tab ([below](#the-bay-picture)) |
| empty picture | `Grid` | `{5, 0x2b, 0xeb, 0x130}` | `DAT_0048d4dc`, grid lines off |
| pilot label | `Text` | `{5, 0x134, 0x25, 0x141}` | `0x65` `Pilot:`, left, `0x1a` |
| pilot | `Text` | `{0x28, 0x134, 0x82, 0x141}` | left, `0x17`, opaque |
| skill label | `Text` | `{5, 0x141, 0x25, 0x14e}` | `0x66` `Skill:`, left, `0x1a` |
| skill | `Text` | `{0x28, 0x141, 0x82, 0x14e}` | left, `0x17`, opaque |
| condition label | `Text` | `{0x8d, 0x134, 0xea, 0x141}` | `0x67` `Condition:`, right, `0x1a` |
| condition | `Text` | `{0x8d, 0x141, 0xea, 0x14e}` | right, opaque |
| roster | `TitledPanel` | `{6, 0x154, 0xec, 0x1da}` | `0x64` `Squad Inventory`, header 19 tall, `+0x65 = 0` |
| 8 rows | `Panel` | `{9, i*0xe + 0x16, 0xe2, i*0xe + 0x23}` in the roster | border `0x10`, `0x29` on the selected bay |

The rows are 14 tall on a 14-pixel pitch, so unlike the repair lists they do not overlap. Each is a [four-column row](#a-row-is-four-text-columns) cut at `0x21`, `0x70` and `0x7b`: `"%d."` of the bay number centred, the machine's name left, `-` centred, and the crew column left. `Squad_RefreshRowNames` (`0043da47`) writes the name, `estext.bin` `0x6e + type`, in the band colour of the machine's overall condition (`HercStatus_OverallCondition`, 00411bd4), finished or not. `Squad_RefreshRowCrew` (`0043dad7`) writes the crew column in `0x27`: the assigned pilot's name, or `"%d%s"` of the build percentage and `0x6c` `% Complete` for a machine still being built.

`Squad_RefreshReadout(bay)` (`0043d38a`) fills the readout:

| Field | Bay with a pilot | Machine, no pilot | Empty bay |
|---|---|---|---|
| pilot | the pilot's name | `0x6d` `Unassigned` | blank |
| skill | `0x35 + skill` | blank | blank |
| condition | the band word `0x68 + level` of the overall condition, in the band colour; while the machine is being built, `"%d%s"` of the build percentage and `% Complete` in `0x20` | the same | blank |

A bay's pilot is `Squad_PilotForBay(00482a78, bay)` (`00410220`), which looks at exactly four records: the player's own, embedded at `+0x04`, and the three squad members the player structure points at from `+0x3f`. `Player_Read` (`004101b8`) sets those pointers on load to record `DAT_00483b48[k]` of squad `k`, so a pilot elsewhere in the squad block is never shown against a bay.

**A roster click is `Squad_SelectBay(bay)` (`0043d64d`)**, through eight thunks from `0043dde7`, each of which sets `DAT_004765be` for the length of the call. It returns at once for the bay already selected, and each tab takes the click its own way, picked by `DAT_0047581c`. The repair tab's arm refuses a bay that is empty or still being built; otherwise it hides whichever of the old bay's two pictures was up and shows the new bay's external one, relights the two rows, stores `DAT_00482ae5`, resets the selection with `Repair_SelectHotspot(0, 0)`, and refills the names, the rows and the panels. The weapons tab's arm is [above](#entering-the-weapons-screen), the build tab's [below](#scrapping-and-building-are-gated-on-the-bay), and the crew tab's [after it](#entering-the-crew-screen).

### The bay picture

`Squad_ShowPanel(tab)` starts with `Squad_BuildTabPictures(tab)` (`0043c915`), which fills the eight pictures for the tab: `Repair_BuildDiagrams` on the repair tab ([above](#the-damage-diagram)), and `Squad_BuildBayPictures` (`00414e5b`) on WEAPONS, BUILD and CREW. It then shows the selected bay's picture, or the empty one (`DAT_0048d4dc`) when no bay is selected.

`Squad_BuildBayPictures` moves each bay's picture to `{5, 0x2b}` at `0xe7` by `0x105` — `FUN_0041ec33` and `FUN_0041ece6` take a size and write the far corner as `origin + size - 1`, so the pictures end on row `0x12f`, one short of the rect they were built at — and switches their grid lines off. It fills them from `gam\arm_<chassis>.dat` ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#gamarm_dat--armory-layout)) and three banks per chassis, whose stems are not the layout files': `out`, `rap`, `tom`, `sam`, `col`, `apoc`, `ogr`, `mav` and `fly`.

| Part | Slot | Frame | Position |
|---|---|---|---|
| top half | the first layout record's id | `dba\<stem>_bod.dba`, the record's own frame once the machine is built; `dba\mt_3qtr.dba` frame 0 at 0% built; body frame 2 below 51% built and 4 from it | the record's |
| bottom half | the second record's id | as above with frames 1, 1, 3 and 5 | the record's |
| each fitted weapon | the group record's id | `dba\<stem>_wep.dba`, the record's frame | the record's, one pixel in from the load |

A weapon's record is the one in its weapon-id group whose id is `slot + 2`, found by `RepairLayout_FindWeaponPart` (`00413ccc`), and a record whose frame is `-1` draws nothing. Every part takes its record's `+0x16` as its blit flags and no colour remap, so nothing on this picture is recoloured by damage. An empty bay, and the empty picture, get `mt_3qtr`'s two frames at `(1, 1)` and `(1, 0x8c)`. Retail's layouts put the body in slots 0 and 1 and the weapons from 2.

The body banks do not all carry the construction frames: `out_bod` and `mav_bod` have two frames, `rap_bod` and `tom_bod` four, and the other five six ([Open](#open)).

### What the buttons are gated on

`Repair_RefreshDetail` writes the same greying trio ([below](#the-condition-readout)) at three of the four:

| Button | Live when |
|---|---|
| `REPAIR` | the salvage pool, net of the build queue, covers lifting the selected component one level ([`armory.md`](armory.md#what-one-repair-level-costs)) |
| `REPAIR ALL` | it covers `Repair_HercCost(herc, 100)` — the whole machine to full, a different figure |
| `SCRAP` | there is a machine, it is not the only deployable one in the eight bays (`Herc_HasSingleDeployable`, 00410add), and its chassis is available — `(&DAT_00483b62)[type * 8]`, the `herc_inf.dat` `+0x0e` flag that `Herc_GrantUnlocks` (004118c5) sets and save block 7 carries ([`../formats/save-games.md`](../formats/save-games.md)) |
| `CANCEL` | always — no trio is written for it |

"Deployable" is `FUN_00410a9d`: the bay is occupied, `+0x4a` is 100 so the machine is built, and `Herc_IsFlightworthy` (00411681) holds — both leg servos, the engine and life support all above 50.

## The crew screen

Tab 6, `CREW`. Built once by `Crew_BuildScreen` (00440eb8, `wcrewi.cpp`), which also loads `dba\c_pilots.dba` and `dba\star.dba`; entered by `Crew_Enter` (00441a01) and hidden by `Crew_Leave` (00441afa), the teardown dispatcher's crew arm. Rects are parent-relative. The left of the canvas is [the squad panel](#the-squad-panel).

| Widget | Class | Rect (in its parent) | Content |
|---|---|---|---|
| content panel | `TitledPanel` | `{0xf1, 0x2b, 0x278, 0x1d9}` | `0xa8` `PILOT ASSIGNMENT`, header 19 tall, plate `0x7e`-`0x108`, face `0x25` |
| 3 squad portraits | image panel | `{0xbc, 0x19, 0xf7, 0x50}`, `{0xfc, …, 0x137, …}`, `{0x13c, …, 0x177, …}` | the squad member's portrait at `(1, 1)`, border `0x21` |
| `CLEAR` | `Button` | `{0x99, 0x188, 0xdf, 0x197}` | `0xa9`, border `0x22` |
| label | `Text` | `{10, 0x1d, 0xb4, 0x29}` | `0xab` `Available Pilots:`, right, `0x29` |
| 4 rows | `HatchedDivider` | `{0xd, i*0x4a + 0x58, 0x178, i*0x4a + 0x9e}` | `+0x55 = 0`, `+0x5d = 1`, border `0x21` |
| row portrait | image panel | `{0xb, 9, 0x43, 0x3d}` in the row | row 0 `star.dba` frame 0, rows 1-3 the pilot's portrait |
| 3 labels | `Text` | `{100, 0xb, 0xec, 0x18}`, then 13 lower twice | `0xac` `Name:`, `0xad` `Skill:`, `0xae` `Herc:`, right |
| 3 values | `Text` | `{0xef, 0xb, 0x164, 0x18}`, then 13 lower twice | left, opaque |

A pilot's portrait is the `c_pilots` frame its roster id (`+0x00`) names; the bank has twelve, one per roster id. The three squad portraits are the three pilots the player structure points at from `+0x3f` — the squad members of [the squad panel](#the-squad-panel).

**The image panel** is the class `ImagePanel_Ctor` (0040b698) builds. Its paint, `ImagePanel_Paint` (`0040b772`), blits the bitmap at `+0x5d` at the offset `(+0x55, +0x59)` and then draws the border over it with no fill, so a 56x52 portrait at `(0, 0)` in a row's 57x53 panel loses its top row and left column to the border. The constructor clears `+0x51`, which stops the paint drawing the border and leaves the bitmap; the builder sets it on all seven.

**`HatchedDivider_Paint` (0040c513)** fills and borders the panel, draws horizontal lines in `+0x59` from row `+0x55` down to the bottom border, redraws row `+0x55` and the border in `+0x4d`, and with `+0x5d` set draws a second border one pixel inside the first. The constructor sets `+0x55` to the widget's height, which draws no lines at all; the crew builder writes 0, which makes the lines a solid body.

### The rows

Row 0 is the player; rows 1-3 are the three squad positions. `Crew_MatchRowPilots` (`00441b08`) fills the pointers at `004776e0` with the squad member whose position, pilot record `+0x27`, is the row — `Squad_MemberAtPosition` (`004102d6`) tests the three in pointer order — or null. `Crew_FillRows` (`00441c4f`) fills the values: the player's name, `0x35 + skill` and the chassis name `0x6e + type` of the player's bay (`00482a9e`), or `0x7e` `None` when that bay is empty; the same three from a squad member's record and bay; and for a row with no pilot, no portrait and `estext.bin` entry 0 in all three.

`Crew_ColourRows` (`00441857`) colours each row by whether it is below `DAT_00482a78`, the count of squad positions in play ([`../formats/save-games.md`](../formats/save-games.md#savgame_sav--block-order)):

| | Row in play | Row out of play |
|---|---|---|
| body, `+0x59` | `0x25` | `0x10` |
| labels | `0x19` | `0x1a` |
| value backing, `+0xc5` | `0x25` | `0x10` |

It writes the values' own colour too, `0x16` and `0x17`, but `Crew_FillRows` runs after it and its `Text_SetString` calls overwrite it with `0x29`, so every value is drawn in `0x29`.

**`Crew_SelectRow(row)` (`00441b85`) selects a row**, and a row's panel and its portrait both carry it as their click handler, through four thunks from `004423b0`. The row's six texts are built with no handler and take no mouse events, so a click on one reaches the row ([Which widget a click reaches](#which-widget-a-click-reaches)). It relights the border of the row and its portrait — `0x21` on the row being left, `0x29` on the new one — calls `Squad_SelectBay` (`0043d64d`) with the row's pilot's bay, the player's for row 0 and `-1` for a row with no pilot, and only then stores the row in `DAT_004776dc`. It has no early return, so clicking the selected row runs it again.

### Entering the crew screen

`Crew_Enter` runs `Crew_ColourRows` (`00441857`) and `Crew_MatchRowPilots` (`00441b08`), sets the three squad portraits, selects rows 0, 1, 2, 3 and then 0 again, runs `Crew_FillRows` (`00441c4f`), and then calls `Squad_SelectBay(bay)` (`0043d64d`) for every bay that holds a machine, in order.

The tab handler has already stored 6 in `DAT_0047581c` ([above](#what-a-tab-click-does)), so all of those calls take `Squad_SelectBay`'s crew arm. It accepts `-1`, or a bay holding a finished machine that is not the Razor unless `DAT_004776dc` is 0 — a squad member cannot be given the Razor, and on a row click the row it tests is the one being left. It swaps the bay's picture and relights the roster rows as the repair arm does, stores `DAT_00482ae5` and refills the readout; while `DAT_004765be` is set, which a roster click and `CLEAR` do, it first gives the bay to the selected row's pilot ([below](#assigning-pilots)).

**So the screen opens on the last bay that holds a finished machine**, whatever bay was selected before and whichever is the player's: the row loop ends on row 0, which lets the closing loop accept every finished machine, the Razor included. The readout under the picture is that bay's until a row is clicked.

### Assigning pilots

Three clicks change the crew, all against the selected row.

**A squad portrait** — handlers `00442151`, `004421fc` and `004422a7` — lights its own border `0x29` and the other two `0x21`, then calls `Crew_AssignSquadMember(k)` (`00441eb8`). On the player's row that returns at once, so the portrait lights and nothing moves. On a squad row, whoever holds the row's position gives it up (`Squad_SetMemberPosition(k, -1)`, `004102be`), squad member `k` takes it, and the row pointers at `004776e0` follow; the member is then given the selected bay by `Crew_AssignSelectedBay`, exactly as a roster click gives it. The lit portrait is a widget colour that no entry resets, so it stays lit across visits.

**A roster click** reaches `Crew_AssignSelectedBay` (`00442055`) from inside `Squad_SelectBay`'s crew arm ([above](#entering-the-crew-screen)), so the bay already selected, and a bay the arm refuses, assign nothing. It runs only when the selected row is the player's or has a pilot. Whoever holds the bay loses it first: the player through `Player_SetBay(-1)` (`0040e6c8`, which writes `00482a9e`), and each squad member through `Squad_SetMemberBay(k, -1)` (`0040e6d7`), which also takes them off strength. The row's pilot then takes the bay, and for a squad member `Squad_UpdateOnStrength` (`00410366`) recomputes the on-strength byte for the row's position.

**Taking a bay leaves its old pilot with none.** Nothing hands the new pilot's previous bay on, so giving the player's bay to a squad member leaves the player's `Herc:` reading `None` until a bay is clicked on row 0. With no bay selected — where selecting an empty row leaves it — the bay handed out is `-1`, and every squad member with no bay counts as its holder.

**`CLEAR`** (handler `00442352`) calls `Crew_ClearRow` (`00441f94`), which on a squad row holding a pilot calls `Squad_SetMemberBay(k, -1)`, frees the position, nulls the row pointer, and then calls `Squad_SelectBay` with the member's bay — `-1` by then — with `DAT_004765be` set. The row is empty by that point, so the assignment inside finds no pilot, and what remains is the picture going to the empty bay. On the player's row, or an empty one, it does nothing.

Both the unassign and the recompute write the on-strength byte through `Squad_SetOnStrength` (`00410327`), which moves `00482a7a`, the count of machines on strength ([`../formats/save-games.md`](../formats/save-games.md#savgame_sav--block-order)), by one whenever the byte changes.

## The build screen

Tab 4, `BUILD`, the `Herc Construction` panel. Built once by `Build_BuildScreen` (`00445758`), entered by `Build_Enter` (`0044690d`) and hidden by `Build_Leave` (`0044694e`), the teardown dispatcher's build arm. Rects are parent-relative. The left of the canvas is [the squad panel](#the-squad-panel), filled with the three-quarter [bay picture](#the-bay-picture).

| Widget | Class | Rect (in its parent) | Content |
|---|---|---|---|
| content panel | `TitledPanel` | `{0xf1, 0x2b, 0x27c, 0x162}` | `0xba` `Herc Construction`, header 19 tall, plate `0x7f`-`0x10a`, face `0x25`, dithered body in `0x25` |
| 9 blueprints | `Grid` | `{0xb2, 0x2d, 0x180, 0x12d}` | one per chassis type, border `0x27`, grid lines `0x18` ([below](#the-blueprints)) |
| chassis list | `FramedPanel` | `{0x16, 0x4b, 0x9a, 0xd1}` | face `0x10` |
| 9 rows | `Panel` | `{10, i*0xe + 4, 0x6e, i*0xe + 0x11}` in the list | `0x6e + i`, the chassis name, border `0x10` |
| stats box | `FramedPanel` | `{0x60, 0xe3, 0xab, 0x11f}` | face `0x10` |
| 2 headings | `Text` | `{0x16, 0x28, 0x9a, 0x34}`, `{0x16, 0x34, 0x9a, 0x40}` | `0xbc` `Select Herc`, `0xbd` `Type To Build`, centred, `0x1a` |
| 4 labels | `Text` | `{8, 0xe9, 0x5e, 0xf5}`, then 12 lower three times | `0xbb` `Mass`, `0xbe` `Speed`, `0xbf` `Hardpoints`, `0xc0` `Salvage Reqd`, left, `0x1a` |
| 4 figures | `Text` | `{5, 6, 0x45, 0x12}` in the stats box, then 12 lower three times | left, `0x17`, opaque |
| lower panel | `FramedPanel` | `{0xf1, 0x166, 0x27c, 0x1d9}` | face `0x10` |
| salvage label | `Text` | `{0x7d, 9, 0x10e, 0x15}` in the lower panel | `0xc1` `Salvage Available`, centred, `0x1a` |
| salvage box | `FramedPanel` | `{0x91, 0x1a, 0xfa, 0x2c}` in the lower panel | face `0x10`; its figure a `Text` at `{1, 3, 0x68, 0xf}`, centred, `0x17`, opaque |
| scrap box | `FramedPanel` | `{0x37, 0x38, 0xc1, 100}` in the lower panel | face `0x25`; `0xc2` `Scrap Herc` at `{8, 6, 0x82, 0x12}`, centred, `0x1a` |
| `SCRAP` | `Button` | `{0xf, 0x17, 0x78, 0x26}` in the scrap box | `0xc4`, border `0x22`, built disabled |
| build box | `FramedPanel` | `{0xcc, 0x38, 0x156, 100}` in the lower panel | face `0x25`; `0xc3` `Build Herc`, placed as the scrap box's title |
| `BUILD` | `Button` | `{0xf, 0x17, 0x78, 0x26}` in the build box | `0xc5`, border `0x22`, built disabled |

The builder writes `+0x4d = 0x27` over the constructor's border on every panel and blueprint. The content panel's `+0x59 = 0` and `+0x5d = 0x25` make its body a checkerboard over [the scope's black](#the-palette), which is why the labels sit on a dithered ground while the boxes holding figures are flat. The lower panel is parented to the shell's top-level window rather than to the content panel, so the two are siblings and `Build_Enter` shows each.

**The rows are 14 tall on a 14-pixel pitch.** Each is a [four-column row](#a-row-is-four-text-columns) cut at `0x62`, `0x62` and `0x62`: the name centred from `2` to `0x62`, then three empty columns, two of them zero-wide. Nine rows means every chassis type has one, the Razor included.

The four figures are `herc_inf.dat`'s first four stats for the selected chassis, as `Herc_BuildScreenRefresh` (`00446cfa`) formats them ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md)). The salvage figure is `Build_RefreshSalvage` (`00446e71`)'s `"%ld %s"` of `(CareerSalvage - Armory_QueuedTotal()) / 1000` and `0xc6` `TONS` — the same net pool [the repair screen](#the-condition-readout) quotes in kilograms.

### Choosing a chassis

`Build_SelectChassis(chassis)` (`00446c3b`) is each row's handler, through nine thunks from `00446fbf`. It returns at once for the chassis already selected, `DAT_004786e4`; otherwise it sets the old row's name to `0x27` and its border to `0x10` and hides its blueprint, sets the new row's name and border to `0x29` and shows its blueprint, stores the chassis, and runs `Build_GateButtons` and `Herc_BuildScreenRefresh`. `DAT_004786e4` is `-1` in the image and this is its only writer.

`Build_GateRows` (`00446835`) gates the rows on the chassis availability flag, `(&DAT_00483b62)[type * 8]` ([What the buttons are gated on](#what-the-buttons-are-gated-on)): a chassis without it has its row disabled and all four columns set to `0x10`, the background, so the list shows a gap where it is and a click on the gap does nothing. Every other row is enabled with all four columns at `0x27` — the selected row's name included.

`Build_Enter` runs `Build_GateRows`, `Herc_BuildScreenRefresh`, `Build_GateButtons` and `Build_FillBlueprints`, shows the content panel, the lower panel and chassis 0's blueprint, and calls `Build_SelectChassis(0)`. **So the screen always opens on chassis 0**, available or not. Coming back to the tab with chassis 0 still selected, the select returns at once, and the row keeps its lit border under the name `Build_GateRows` has just set back to `0x27`.

### Scrapping and building are gated on the bay

`Build_GateButtons` (`004469d4`) writes the [greying trio](#the-condition-readout) at both buttons from the bay the squad panel has selected, `DAT_00482ae5`:

| Bay | `SCRAP` | `BUILD` |
|---|---|---|
| empty | dead | live when the net pool is **more** than the selected chassis's price times 1000, compared unsigned |
| occupied | the repair screen's `SCRAP` test: not the only deployable machine, and its chassis available | dead |

A machine is built into an empty bay, and only an occupied one can be scrapped. A pool exactly equal to the price leaves `BUILD` dead. With no bay selected the function reads the dword before the eight-pointer array at `00482ac3` as the bay's machine.

**This tab's arm of `Squad_SelectBay` takes any bay.** Unlike the repair and crew arms it refuses nothing, an empty bay and an unfinished machine included: it swaps the bay pictures, relights the two roster rows, stores `DAT_00482ae5`, refreshes the readout, and runs `Build_GateButtons`.

`SCRAP`'s handler (`00446ee0`) opens a confirmation dialog, built by `00447328` with `estext.bin` `0xc9` as its title and `0xca`/`0xcb` as its two buttons. `BUILD`'s (`00446f3e`) calls `0040e91c` with the selected chassis and then refreshes the roster names and crew, the readout, the salvage figure and the gate ([Open](#open)).

### The blueprints

`Build_FillBlueprints` (`0041579d`) fills the nine grids from the same records and banks as the repair screen's [exploded external picture](#the-damage-diagram): each `gam\rpr_*.dat` body record, from `dba\rpr_<chassis>.dba`, in the slot its id names with the record's flags. No weapon is drawn. Every part's remap pair is `0xe` to `0xe`, which `Grid_Paint` applies because the target is not `0x10` and which changes nothing, so the parts show in their own ink. `Build_Leave` frees the nine banks (`00415928`) and `Build_Enter` loads them again.

## The arming and repair hotspots

Both screens lay clickable rects over a picture of the selected machine. The geometry comes from `gam\arm_hots.dat` and `gam\rpr_hots.dat` ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#gamarm_hotsdat-and-gamrpr_hotsdat--the-clickable-regions)), which carry position and nothing else: **an area's index within its chassis group is its identity**, because the builder passes `handlerTable[areaIndex]` as the panel's click handler. Each handler is a one-line thunk that calls a common function with its own index baked in.

`DAT_00482ae5` is the selected bay slot, 0-7 and `-1` for none, and both screens read the machine out of the eight-pointer array at `00482ac3`.

**Arming** — `Arming_SelectHardpoint(hardpoint)` (0043dbb2), ten thunks, `0043e15f`-`0043e4c8`. It returns immediately when the click is on the hardpoint already selected, then reads the mount pointer at `herc + 0x50 + hardpoint*4` — null for an empty slot, otherwise its first `int16` is the fitted weapon id — and repaints.

**Repair** — `Repair_SelectHotspot(column, row)`, twenty-five thunks in one table at `0048d1f8` (`004340b5`-`00434ac8`). Sixteen are column 0, the hotspots on the picture; nine are column 1, a list beside it. The pair is resolved into a category and an index within it:

| | `Repair_HotspotCategory` (00433410) category | `Repair_HotspotIndex` (00433431) index | Count | What it selects |
|---|---|---|---|---|
| column 0, rows 0-5 | 0 | `row` | 6 | the external component **groups** |
| column 0, rows 6-15 | 2 | `row - 6` | 10 | the per-hardpoint conditions |
| column 1, rows 0-8 | 1 | `row` | 9 | the internal components |

**That category is the status block's own accessor mode.** `HercStatus_Get(block, mode, index)` takes exactly these three: mode 0 averages an external group, mode 1 addresses the nine internals and mode 2 the ten hardpoints ([`../formats/save-games.md`](../formats/save-games.md#the-66-byte-status-block)). Three independent things agree — the counts, the mode semantics, and `rpr_hots.dat` carrying exactly six areas per chassis — so the six hotspots on the picture are the six named groups `Cockpit`, `Left Torso`, `Right Torso`, `Chassis`, `Left Leg`, `Right Leg`, in that order.

The ten weapon rows are not in `rpr_hots.dat`: the same builder loop places them from the per-chassis `rpr_*.dat` geometry, starting at handler index 6. A row is refused — the selection does not move and nothing repaints — when its slot is past the machine's mount capacity at `+0x4c`, or when the slot is empty. So an unfitted hardpoint cannot be selected on the repair screen at all.

Selection repaints the outgoing entry with `(0x10, 0x27)` and the incoming with `(0x29, 0x29)`; `0x29` is the highlight colour throughout the shell, and `0x10` the resting one.

### The condition readout

`Repair_RefreshDetail` refreshes the detail panel from the selection, writing four text fields: the selected component's repair cost, its condition, the whole machine's rebuild cost (`Repair_HercCost(herc, 100)`), and the salvage available — which is `CareerSalvage` **minus** `Armory_QueuedTotal()`, so the figure the repair screen quotes is already net of what the build queue has committed. All four suffix `estext.bin` `0xc8` `kg`.

The condition itself goes through two functions over two in-image tables. `Repair_DamageLevel(condition)` (0043d9cf) walks `004765c0` = `{89, 79, 59, 29, 0}` and returns the first index whose entry the condition is **strictly greater** than, or 5 if it is greater than none:

| Level | Condition | `estext.bin` word | Colour (`004765ca`) |
|---|---|---|---|
| 0 | 90-100 | `0x68` `Nominal` | 14 |
| 1 | 80-89 | `0x69` `Light` | 13 |
| 2 | 60-79 | `0x6a` `Moderate` | 12 |
| 3 | 30-59 | `0x6b` `Heavy` | 11 |
| 4 | 1-29 | `0x6c` `% Complete` | 10 |
| 5 | 0 | `0x6d` `Unassigned` | 39 |

**The bands are the repair ladder's.** `Repair_LevelForCondition`'s own table is `{90, 80, 60, 30, 1, 0}` tested with `<=` where this one is `{89, 79, 59, 29, 0}` tested with `<`, which partitions identically ([`armory.md`](armory.md#repair-levels)) — the word the screen prints *is* the repair level the machine would be worked to.

**The last two rows are a retail bug.** The word run in `estext.bin` is only four long: `0x67` is the label `Condition:`, `0x68`-`0x6b` are the four words, and `0x6c`/`0x6d` are `% Complete` and `Unassigned`, which belong to the build screen and the crew screen. Nothing in the table is a fifth or sixth damage word — there is no `Critical` or `Destroyed` anywhere in the 342 entries. The index really is the level plus a fixed base, read off the instruction stream rather than the decompiler: `Repair_DamageLevel` returns its loop counter in `EAX` (`xor eax,eax` … `inc eax`, with the comparison kept in `CX`/`DX`), and the caller does `mov esi,eax` / `add si,0x68` before the lookup. Recorded in [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

**Greying a button is three writes, not one.** `+0xb5` and `+0x4d` go to `0x26` when it is disabled and to `0x29`/`0x22` when it is not, and `+0x49` follows. That pairing is what makes `+0x49` the enable flag rather than a style bit: it moves with the colours, on a test of whether the player can act. `Repair_RefreshDetail` writes the trio at three of the repair screen's four buttons, and what each is tested on is [above](#what-the-buttons-are-gated-on).

## What the whole front end shares

`esglobal.cpp`'s init (`004073bc`, called with 1 on entering the shell) loads what every screen then draws with:

| Resource | Handle | Role |
|---|---|---|
| `dfn\font2.dfn` | `0046dcc4` | loaded twice, into this handle and the next |
| `dfn\font2.dfn` | `0046dcc8` | |
| `dfn\black.dfn` | `0046dccc` | every tab caption |
| `dbm\bay2a_84.dbm` | `0046dcd4` | the backdrop each screen's root is textured with |
| `bin\estext.bin` | `0046dcc0` | all UI text — 342 entries, see [`../formats/weapons-dat.md`](../formats/weapons-dat.md#the-bin-string-tables) |

`dfn\font.dfn` is in the archive and the init does not ask for it.

**There is one backdrop for the whole shell.** `0046dcd4` is written exactly once, by this init, and all eight screen builders pass that same handle as their root's image. So a screen that installs `arming.dpl` is drawing `bay2a_84` through a palette that is not its own. On the tab screens only the strip row ever shows it, and the backdrop's top 30 rows are black: everything below the strip is covered by [the palette scope's fill](#the-palette).

## The palette

**The palette is a widget, not a call.** `DAT_0048d444` is the palette scope from the widget tree above: a `Window` subclass built by `0040ca6c` (vtable `PTR_FUN_0046ef04`, allocation `0x47`) whose `+0x45` is a palette index rather than a lit flag. Its event handler `0040cab7` responds to event 2 by calling `Shell_InstallPalette(+0x45)` and committing the result. So the shell changes palette by writing `+0x45` between the two visibility calls of `00439da0(index)`, and the second of them is what fires the install.

**The scope is installed by being hidden**, which follows from [the visibility pair](#showing-and-hiding-a-widget): `00439da0` shows the scope, writes the index and hides it again, and it is the hide that posts event 2. The pair works repeatedly because each call leaves the bit where the next one needs it.

**The scope paints, and its paint is what makes the tab screens black.** Its handler sends event 4 to `PaletteScope_Paint` (`0040cb40`), which fills the whole scope rect, `{0, 0x1e, 0x27f, 0x1df}`, with `0x10`, and the show posts that event. Tabs 2-7 all install their palette through the scope (`0043b162` cases 2-7 call `00439da0`), and nothing repaints the backdrop-textured root afterwards, so each of those tabs draws its screen over a black canvas and shows black wherever its widgets leave it bare. The main menu and the save screen go through the scope too, then put up their own backdrop-textured root over the fill, which is why they show the bay.

`Shell_InstallPalette(index)` (004075b2) reads a pointer table of `dpl\*.dpl` paths at `0046dcdc`, twenty entries long:

| # | Name | Used for |
|---|---|---|
| 0 | `intr_pt1` | the intro; nothing in the tab layer selects it |
| 1 | `palette` | the shell on entry, the service bay (`0043b23d`), `MAIN MENU`, `SAVE` and `REPAIR` |
| 2 | `arming` | `WEAPONS`, `BUILD`, `ARMORY` and `CREW` |
| 3 | `cam_er` | the campaign map, stages 1-4 |
| 4 | `cam_moon` | the campaign map, stage 5 |
| 5-9 | `br_w1`-`br_w5` | the briefing, indexed by stage |
| 10-14 | `db_w1`-`db_w5` | the debrief, indexed by stage |
| 15-19 | `alph`, `delt`, `omic`, `brav`, `luna` | the theater, indexed by stage |

`0043b162(tab)` is the switch that picks one. Tab 3 takes 1, tabs 2 and 4-6 take 2, and tab 7 takes `stage + 4` for the briefing, `stage + 9` for the debrief and 3 or 4 for the map — `4` once `stage - 1 > 3`. Case 8 is not a tab: it shows the frame's root, and is what `0043b0c8`'s callers pair with the refresh.

**The stage counts from one at runtime**, where the save stores it from zero ([`campaign-loop.md`](campaign-loop.md)). Three independent tables say so: the briefing and debrief runs are five long and reached by `stage + 4` and `stage + 9`, the map's Earth-to-Moon switch fires at the same stage the theater run's `luna` sits at, and `maybe_Mission_UpdateLocationTab` carries four `dba\` location names — `alph2`, `delt1`, `omic1`, `brav1` — and branches away to a cutscene entirely when `stage - 1 == 4`. The arithmetic is unguarded in all three places.

That last function also installs the theater palette directly, as `Shell_InstallPalette(stage + 0xe)`. Because stage 5 branches away before the call, `luna` is in the table and unreached by this path.

## Engine coverage

`Herculan.Engine.Shell` draws the shell frame: the tiled backdrop, the square button and the eight captioned tabs, hit-tested, latching on the six tabs that latch, and gated by `ShellCampaignMode`. The canvas is placed by `ShellScreenLayout`, which scales the fixed 640x480 by window height and centres it, so every rect above is used exactly as the original states it. `ShellPalette` carries the twenty-entry table and the per-tab switch, which every tab click follows. `--shell-palette <name>` pins one entry, `--shell-training` runs the gated half of the strip refresh, `--shell-tab <n>` opens on a tab rather than on the main menu, and `--shell-bay <n>` picks the hangar bay the repair tab opens on.

**Clicks follow each class's own rules.** `ShellPointer` keeps what the pump and the handlers keep — the pointer's target down to the innermost widget, the pointer lock, the lit flag and an edit field's focus — and delivers each move, press and release as [Which widget a click reaches](#which-widget-a-click-reaches) says: the strip fires on the left press and the right release, the content panels, rows and buttons on either release of a press that never left them, the crew portraits on any left release, and the save rows on the left press, taking the pointer. `ShellButton` keeps the lit flag apart from the face last painted, which the strip's left release needs. The host polls the mouse once an update, so a press and its release inside one update are lost.

**The save screen is drawn**, from real files: `ShellSaveSlots` reads `sav\GAMEFILE.STR` and each `GAME_?.SAV` it marks in use, and `ShellSaveScreen` places every widget above from the same parent-relative rects and prints the detail panel from the staging record. Clicking a row moves the selection and the summary follows; `SAVE` and `RESTORE` gate as the original gates them. Tab 1 hides the strip, and `EXIT` and `RESTORE` leave as above through `ShellScreen.ReturnToFrame`, which is the `0043b162(8)`/`0043b0c8` pair; `RESTORE` parses the slot and rebuilds the hangar and the repair screen from it. The rename, and with it `SAVE`, `CANCEL` and `ACCEPT`, has no port, and neither has `RESTORE`'s autosave ([Open](#open)); `CANCEL` and `ACCEPT` stay grey because nothing starts a rename.

The main menu tab keeps the strip up here, where its handler hides it as tab 1's does: nothing is ported behind that tab, so hiding the strip would leave nothing on screen to click. That is this engine's choice, not the original's.

**The repair screen is drawn**, from a real save's hangar bay and the real price list. `ShellHangar` and `ShellBayMachine` are the eight-pointer bay array and `HercStatus_Get` over one machine's status block; `ShellRepairCosts` parses `gam\damage.dat` and expands it against `gam\herc_inf.dat`'s prices exactly as the loader does, and carries both cost functions. `ShellRepairScreen` places every widget above, fills both lists, prints the three readout panels and gates the buttons. `ShellRepairDiagrams` loads the nine layouts, `rpr_hots.dat` and their banks and paints whichever picture the selection's column has up, and `ShellSquadPanel` draws the readout and the roster. Clicking a row, a hotspot on the external picture or a roster row moves the selection or the bay and the panels follow, including the refusal of an unfitted hardpoint and of an unbuilt bay. The four buttons' actions, the manual/auto mode switch and the build queue have no port ([Open](#open)), so the salvage figure is the pool with nothing deducted.

**The crew screen is drawn and assigns**, from the same save. `ShellCrewScreen` places every widget above, fills the four rows and the three squad portraits from the pilot records `ShellHangar` carries, colours the rows by `00482a78`, and runs the entry's selection, so it opens on the bay retail opens on. A row click, a squad portrait, a roster click and `CLEAR` do what [Assigning pilots](#assigning-pilots) says, on the hangar the other tabs read, on-strength bytes and count included. `ShellBayPictures` loads the nine `arm_*.dat` layouts and their banks and draws the bay picture, and `ShellSquadPanel` draws it with the readout and the roster as the one left-hand column the four tabs share. The bay the entry starts from is the repair screen's, the only selection this engine carries between tabs.

**The weapons screen is drawn and shows**, from the same save's armory stock, `gam\arm_weap.dat`, `dba\arm_weap.dba` and `wpn_desc.bin`. `ShellWeaponsScreen` places every widget above, lists the 27 rows with their counts and unlock gaps, shows the lit row's picture and description, puts the guidance buttons up for a missile rack and shows a kind's picture from them, and opens on the first row. The roster click takes the arming arm's bay rule. No hardpoint is ever selected, so a row click fits nothing, which is what retail does with none selected; the steppers do nothing ([Open](#open)). Its bay starts from the repair screen's, as the crew and build screens' do.

**The build screen is drawn and selects**, from the same save and `gam\herc_inf.dat`. `ShellBuildScreen` places every widget above, gates the rows on the save's availability flags, prints the selected chassis's figures and the net salvage, and gates `SCRAP` and `BUILD` on the bay; `ShellRepairDiagrams` draws the blueprint from the repair screen's layouts. A row click moves the chassis and a roster click moves the bay, as [The build screen](#the-build-screen) says. Neither button acts ([Open](#open)). Its bay starts from the repair screen's, as the crew screen's does, and with no bay selected it takes the bay as empty where the original reads past the array — this engine's choice.

Until `RESTORE` loads one, the host opens the first slot the directory marks in use to have a machine to show. That is the host's own choice and not the original's, which reaches the tab only from a game already in progress.

**The widget paints run in palette indices, not in quads.** `ShellSurface` is an 8-bit indexed canvas with the primitives the paints are built from, `ShellChrome` and `ShellGrid` port the paints onto it, and the result is resolved through the palette and uploaded as one texture per repaint. That is the original's own model and two of its details depend on it: the ink remap that gives a widget its text colour cannot be done on resolved colours, and index 0 staying untouched is what lets a dithered panel body show the backdrop through it. Clipping each paint to its own widget is likewise load-bearing rather than defensive — the title bar's hatch is drawn 700 pixels wide for a 357-pixel panel.

`ShellPalette.PaintScope` is the scope's fill, and the host lays it down before painting any of tabs 2-7, ported or not, so those tabs are black below the strip as retail's are, and every tab installs its own palette. The mission tab's depends on the stage and the view, which the host takes from the loaded game: the stage plus one, and the map while the mission-within-stage counter is zero and the map has not been shown since the load, the briefing otherwise. `--shell-palette` pins one entry on every tab.

The whole content surface is painted afresh on every change, where the original repaints only the widgets that moved. Rows overlap by a pixel and whichever paints second owns the shared border row, so the selected row is painted last, which keeps its highlight whole as `Repair_SelectHotspot`'s incoming repaint does.

The engine reloads the whole of `ShellArt` to change palette, where the original re-installs one and lets the hardware palette do the rest — the art here is decoded to RGBA once per palette rather than kept as indices. Same result on screen, at a few milliseconds per click.

Not drawn: the other three tabs' content, the mouse cursor (`dba\cursor.dba`), the sounds each button plays, and the pressed nudge of a content button's caption ([Open](#open)). Nothing sets the campaign mode, so the gate is driven by a command-line flag ([Open](#open)). See [`../../ROADMAP.md`](../../ROADMAP.md).

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| A button's three frame pointers are unlit, lit and disabled | `ButtonIcon_Ctor` really does take and store three, and a third face for a widget the strip refresh can gate is the natural guess. Neither paint reads `+0x59`: both branch on the lit flag between `+0x51` and `+0x55` only. A gated tab in retail looks exactly like an idle one |
| `SHELL0.VOL`'s `dba\` and `dfn\` are the 320-wide halves of a pair, as they are in the simulator archives, and shell art must be doubled to reach the canvas | There is no `hba\` or `hfn\` in `SHELL0.VOL`. The shell has one set and authors its screens at the size that set draws at — the `{0, 0, 0x27f, 0x1df}` panel is 640x480 and the button plates fill their 75x24 rects at 1:1 |
| A screen's widget rects come from its `gam\rpr_*.dat` or `gam\arm_*.dat` file, the way a cockpit widget's come from the herc's `.GAU` | Those files exist and do carry widget geometry, which makes the inference natural. They cover the per-chassis content panels only; the screen builders that place everything else read no file at all |
| `warmingi.cpp` is a warning dialog | It is `w` + `arming` + `i`, the weapon-fitting screen, in the same naming pattern as `wsrvbayi.cpp`, `wcrewi.cpp` and `warmoryi.cpp` |
| The tab screens are drawn through `dpl\bay.dpl` | `SHELL0.VOL` carries one, and the bay screen's own name makes it the obvious candidate for the palette the bay installs. The table at `0046dcdc` does not contain it: index 1 is `dpl\palette.dpl`. Nothing traced so far selects `bay.dpl` at all |
| Exactly one tab is latched at all times | Seven of the nine handlers latch their own plate and it is easy to assume the other two do too. `MAIN MENU` and `SAVE` clear all nine, write none back and [hide the strip](#tabs-0-and-1-hide-the-strip) |
| `0043b23d` shows the frame, as its Ghidra name `ServiceBay_Show` says | It calls `Widget_HideRecursive` (0041f469) on the frame's root and panel, and the pair that undoes it — `0043b162(8)` and `0043b0c8` — calls `Widget_ShowRecursive` on the same two. The name was given under the swapped reading of those two functions ([above](#showing-and-hiding-a-widget)) |
| `0041f2e6` shows a widget and `0041f469` hides it, matching their names in the raw Ghidra dump | The dump's own names support that reading — one sets a state bit and recurses into children, the other clears it, and the names line up with which is which. They are swapped: `+0x11` bit 2 is a *hidden* bit, so the setter is the hide. `known_symbols.json` carries the corrected assignment (`Widget_ShowRecursive` at 0041f2e6, `Widget_HideRecursive` at 0041f469); only the raw dump still has it backwards. Three witnesses agree; see [Showing and hiding a widget](#showing-and-hiding-a-widget) |
| The repair screen's detail figure and its `REPAIR ALL` figure are the same cost scaled | Both say `Salvage Required:` in kg and both come from the same unit-value tables, so a per-item share of the whole is the obvious reading. They use different functions with different targets: `Repair_HercCost` prices the machine to 100, and `Repair_LevelStepCost` (00413871) prices the selected component up to the floor of the next band only ([`armory.md`](armory.md#what-one-repair-level-costs)) |
| Every widget fires on the button's release, and only after a press on it | That is `Control_HandleEvent`'s rule, and it is the base class's handler, run by the panels, the grids and every content button, so it reads as the shell's. The tab strip, the image panels and the edit fields each put a handler of their own in vtable slot 0: the strip fires on the left press, an image panel on any left release, an edit field on the left press ([The widget that takes a click decides what it does](#the-widget-that-takes-a-click-decides-what-it-does)) |
| `00445758` is the mission tab's builder, as its old Ghidra name `Mission_BuildScreen` has it | It is the largest function after `wmissini.cpp`'s assert-string anchor, so the file attribution points at the mission tab. Every widget it builds is one that `Build_Enter` (`0044690d`), tab 4's entry, shows and the teardown's build arm hides, and its captions are the `Herc Construction` run `0xba`-`0xc5`. `known_symbols.json` names it `Build_BuildScreen` |
| The palette scope draws nothing — it exists only to fire the palette install | It carries no bitmap, no caption and no chrome, and its handler's event 2 is the install. Its event 4 is a paint, `PaletteScope_Paint` (`0040cb40`), which fills its rect with `0x10`; that fill is why retail's tab screens are black ([The palette](#the-palette)) |

## Open

- **Unported:** the shell's movies — `Movie_Enqueue`, `Movie_PlayQueue` and `Avi_Play` — and with them the [input gate while one plays](#input-while-a-movie-plays).
- **Unported:** the pressed nudge of a content button's caption. `Button`'s paint (`00409b79`) moves the caption down while `+0x45` is lit and the button is enabled, as the strip's does; the engine's content buttons draw theirs in one place.
- **Unported:** selecting a hardpoint on the weapons screen — `Arming_SelectHardpoint` (`0043dbb2`), the two steppers, the arming hotspots and `Arming_MarkHardpoint`'s outline — and so fitting a weapon and writing a mount's guidance kind.
- **Open:** what `FUN_004114ec` does when `Arming_FitSelected` fits a weapon — what it takes from the armory's stock and returns to it, and whether it refuses a weapon the stock holds none of, which [the row's own test](#fitting-a-weapon) lets through for a mount whose guidance kind equals the weapon's id.
- **Unported:** the build screen's `SCRAP` confirmation dialog (`00447328`) and `BUILD`'s order through `0040e91c`.
- **Open:** what `0040e91c` writes when `BUILD` is clicked — which bay the new machine goes into, and how the order reaches the build queue ([`armory.md`](armory.md)).
- **Open:** what retail draws for a machine under construction whose body bank lacks the construction frames ([The bay picture](#the-bay-picture)). `Squad_BuildBayPictures` (`00414e5b`) indexes past them unchecked; the engine draws nothing for a missing frame.
- **Unported:** the save screen's [rename](#saving-is-a-rename) — `SAVE`, `CANCEL` and `ACCEPT` — and `RESTORE`'s slot-10 autosave and career-file copies. The shell has no save writer and no keyboard input into an edit field.
- **Open:** the edit field's keyboard handling past its dispatch. Keystrokes reach the row as the pointer's target ([Saving is a rename](#saving-is-a-rename)). `EditField_HandleEvent` passes a key (event `0x40`) to `FUN_0040bdd2` while `+0xbf` is set, acts on a command (`0x100`) only while `+0xbf` and `+0xa7` are both set — backspace (1) and the left arrow (4) both call `FUN_0040be56`, and Enter (`0x0a`) releases the lock and the focus — and hands every key and command on to the row's handler. Unread: which event `004377d2` posts at the row, how the permitted-character set at `+0x9f` filters (the full string is unread past `"…qrstu"`), whether the `" 3. "` prefix can be deleted, and what the row's handler does with a key — whether one commits or abandons the rename.
- **Open:** the meaning of the row's `+0xb7 = 4`, and whether `EditField_Paint` draws the caret from `+0xbf`, `+0xb3` or both.
- **Open:** how `ACCEPT`'s label reaches `sav\GAMEFILE.STR` and where the slot's in-use byte is set — `Game_SaveSlot`'s own body has not been read for either.
- **Unported:** the repair screen's four buttons' actions (`REPAIR`, `REPAIR ALL`, `SCRAP`, `CANCEL`) and the manual/auto repair mode switch.
- **Unported:** the armory build queue; the repair screen's salvage figure is the raw pool with nothing deducted as a result.
- **Unported:** the other three tabs' content (`MAIN MENU`, `ARMORY`, `MISSION`).
- **Unported:** the mouse cursor (`dba\cursor.dba`) and each button's click sound.
- **Unported:** setting the campaign/training mode — `FUN_0040e69e`, which `00431498` calls with 1; the gate is driven only by a command-line flag.
