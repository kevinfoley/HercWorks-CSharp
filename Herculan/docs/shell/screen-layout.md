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
3. **The palette scope** at `{0, 0x1e, 0x27f, 0x1df}` — the canvas below the strip. It is parented to the root rather than to the panel, and it is how the screen's palette is chosen; see [The palette](#the-palette).
4. **The strip itself**: one square button and eight tabs.

Widget fields the builders and the tab handlers write directly:

`Control_Ctor` (`00409788`) is the base every one of them goes through. It zeroes `+0x45`, sets `+0x49` to 1 and ORs `0x60` into the flags word at `+0x39`.

| Offset | Meaning |
|---|---|
| `+0x45` | the lit flag. The widget's own mouse handler toggles it 0/1, and the paint picks the button's face from it. A tab handler writes 1 and repaints before building its screen, which is what latches the active tab lit; `0043b0c8` clears it across all nine. On the palette scope, a different class, the same offset is a palette index instead |
| `+0x49` | 1 from the constructor, and the enable flag. The tab gate clears it on the three tabs the training campaign has no economy for, and the repair panel writes it alongside two greying colour fields on a test of whether the player can afford the button ([below](#the-condition-readout)) — moving with the greying, on an affordability test, is what makes it the enable flag rather than a style bit. The button's own paint reads it for one thing, whether the caption takes the pressed nudge; what stops a cleared widget responding is in the base class's click dispatch and is not read |
| `+0x51` | 1 from `Panel_Ctor`, and the gate on drawing any chrome at all: `FUN_0040a726` returns immediately when it is clear. Written 0 on both the root and the full-screen panel, which is how each shows its bitmap with no fill and no border. Not the button field of the same offset — different class, different layout past the base |

## What a tab click does

Every tab has its own handler, and the eight are the same function with three or four lines changed. Each opens by returning if `DAT_0046c08c` is clear, and then by returning if `DAT_0047581c` — which tab is up — already holds its own index: **clicking the tab you are already on is a no-op**, before the teardown, the palette and the sound alike. What follows is, in order:

1. `00439dcb` — clear the lit flag on all nine strip buttons and repaint them.
2. Write `+0x45` back to 1 on this tab and repaint it. **Only tabs 2 to 7 do this**; the main menu's and the save screen's handlers skip it, so while either of those is up the whole strip is drawn unlit.
3. `00439ea7` — tear down whatever tab is currently up, dispatching on `DAT_0047581c`.
4. `0043b162(tab)` — install the tab's palette.
5. `0043cfe7(tab)` — build the shared squad roster panel, on tabs 2, 3, 4 and 6 only. Neither ARMORY nor MISSION has one.
6. The screen's own builder.
7. `DAT_0047581c = tab`, then `0042ee89` — the click sound.

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

Tabs 0 and 1 take a different route to their palette: instead of `0043b162` they call `00439da0(1)` directly and then `0043b23d`, which re-shows the root and the full-screen panel and installs palette 1 itself. The result is the same entry either way.

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

The three it gates are exactly the three that spend salvage, and the training campaign has no salvage economy ([`armory.md`](armory.md)). It writes those five and no others, so `MAIN MENU`, `SAVE`, `MISSION` and the square button are live in both. It also parks `DAT_0047581c` at `0xffff`, so whatever tab is clicked next cannot be mistaken for the one already up.

`ServiceBay_BuildScreen` clears `+0x49` on tab 5 as it constructs it, which the refresh then overwrites either way.

## The tab strip

All nine buttons share the top and bottom edges `4` and `0x1b`. The tabs are 75 pixels wide on a 76-pixel pitch — butted one pixel apart.

| Widget | Rect | Caption | Art |
|---|---|---|---|
| square button | `{7, 4, 0x17, 0x1b}` | none | `dba\online.dba` frames 0 and 1 |
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

The caption is an embedded `Text` child at `+0x4d`, sized to the button's full client rect, drawn in the font at `DAT_0046dccc` and fetched with `WeaponsBin_LookupName(DAT_0046dcc0, index)` from `estext.bin`. The paint places it one pixel above its vertically-centred row when the button is idle and one below when it is lit, so a lit caption sits two pixels lower than an idle one — the pressed nudge. The centring itself is a baseline, `(FUN_00453fa8(text +0xb1) + rectHeight + 1) / 2`: `+0xb1` is the `Text` widget's font handle and `FUN_00453fa8` returns that font's glyph cell height ([above](#text-placement-and-colour)).

The strip is rebuilt by each tab screen's own builder at these same coordinates rather than shared between them.

## How a widget paints

**A widget carries its rect twice.** `Widget_SetRect` (`0041eb5c`) stores the constructor's rect verbatim into `+0x25`/`+0x29`/`+0x2d`/`+0x31` — left, top, right, bottom, **parent-relative** — and `FUN_0041ef45` derives the absolute rect into `+0x15`/`+0x19`/`+0x1d`/`+0x21`. `FUN_0041ec33` shows the relation directly: it adds the parent's `+0x15` to a child's `+0x25` to get the child's `+0x1d`. Only `FUN_0041ebef` moves a widget afterwards, and it rewrites the relative pair and rederives the absolute one.

Five paints are the whole visual vocabulary of a shell screen. All of them work in **widget-local coordinates**, where the extent they draw against is `+0x2d - +0x25`. Because that is a difference it is the same in either space — one less than the inclusive width — so a paint never reads an origin at all: `FUN_0041f585` opens every one of them and binds the drawing context to the widget's absolute rect and clips to it.

**Colour is always a palette index**, taken from a widget field, and the drawing context carries a `{mode, colour}` pair: mode 0 at `+0x22c` is a solid fill, mode 6 is a blit through a 256-entry lookup table. `FUN_00457364` fills a rect, `FUN_004552e4` draws a line between two inclusive endpoints, and `FUN_0045999c` plots one pixel.

**The border is a chamfer.** `FUN_0040a726(widget, fill)` optionally clears the interior to a literal `0x10` — the shell's one background colour, in all five paints — and then draws four edges each stopping one pixel short at both ends, so the true corners stay empty, and paints the four pixels one step *inside* those corners instead. That clipped-corner box is every panel and every button in the shell. It draws nothing at all when `+0x51` is clear, which is how a screen's backdrop-textured root shows its bitmap and no chrome.

| Class | Paint | What it adds |
|---|---|---|
| `Panel` | `0040a959` | nothing — the fill and the chamfered border alone |
| `FramedPanel` | `0040a9a6` | a 50% checkerboard over the interior in `+0x55` |
| `TitledPanel` | `0040ac27` | the header strip, its hatch and title plate, a divider, and a dithered *or* filled body |
| `Text` | `0040b439` | one string, aligned, with an optional backing fill |
| edit field | `0040c14f` | one editable string, left-aligned, with an optional caret |

`TitledPanel_Paint` fills its header strip to `+0x55` for `+0x61` rows, then — when `+0x65` is set, which the constructor does and nothing clears — lays a **diagonal hatch** over it in colour 13: bands of fourteen 45-degree lines on a 28-pixel pitch, 26 bands from five pixels left of the widget. That is over 700 pixels of hatch for a panel a third as wide, and only the clip stops the surplus; the paint relies on clipping rather than measuring. It then punches the hatch back out to `+0x55` between `+0x6d` and `+0x71`, which is the **title plate** the caption reads against, draws the header's own side edges, and closes with a divider on row `+0x61`.

**`+0x59` chooses between a filled body and a dithered one.** Set, the body is cleared to `0x10`; clear, it takes a 50% checkerboard in `+0x5d` from the header height down — and over an unpainted surface that means the shell's single backdrop bitmap shows through at half strength. The save screen takes the second path, which is why the bay is visible through its panel.

### Text placement and colour

`FUN_0045409c(font, {x, y}, text)` draws a run: it tests the context's `+0x231` for 1 and then for 2, so **0 is left, 1 is right and 2 is centred**, against the field width at `+0x235`; then it advances glyph by glyph. `Text` takes that mode from its own `+0x45`, so a label's alignment is a constructor argument. Every field label on a screen is right-aligned and its value left-aligned, which is what makes a label's colon meet its value.

The `y` is an **ink baseline**: `FUN_00453fb4` places each glyph's top row at `y - font[+0x16]`, and `+0x16` is the `.DFN` header's `inkHeight`. `FUN_00453fa8` and `FUN_00453f9c` both return `font[+0x0a]`, the glyph cell height. The two classes centre differently and neither is derived from the other — `Text` uses `H - (H + 1 - cellHeight) / 2 - 2` and the edit field `cellHeight / 2 + (H + 1) / 2`.

**A widget picks its text colour by remapping, not by choosing a pen.** The fonts carry one ink index each ([`../formats/dfn-hfn-dci.md`](../formats/dfn-hfn-dci.md)) and the shell's is `0x29`, so both text paints draw the string in whatever the font has and then re-blit the area through an identity table with entry `0x29` replaced — by `Text`'s `+0xb5`, or the edit field's `+0xbb`. One font therefore serves a label at `0x1a`, a value at `0x29` and a resting list row at `0x27`, and a selection highlight costs nothing but a different replacement.

`Text`'s `+0xc1` is an opaque-background flag and `+0xc5` the colour it clears to: a value field clears its own rect so a refresh overwrites cleanly, and a static label does not.

## The save screen

Tab 1, `SAVED GAMES`. Built by `FUN_004385b0`, entered by `FUN_00439b0c`, its selection moved by `FUN_0043795f` and its detail panel refilled by `FUN_0043712c`. Every rect is four immediates on the builder's stack, and they are **parent-relative**: the panel sits in the canvas, the list and the button column in the panel, and two buttons in the list.

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

**The rows are 13 tall on a 12-pixel pitch**, so each overlaps its neighbour's border row, and the first and last are inset two pixels further from the left edge than the eight between them. Their right edge is computed from the list panel's absolute corners rather than written, at two pixels inside it. Each carries a permitted-character set at `+0x9f` — `"0123456789abcdefghijklmnopqrstu…"` in place of the class's `"ABCDEFGHIJKLMNOPQRSTUVWXYZ"` — so **renaming a slot is typing into its row**, and `CANCEL`/`ACCEPT` are that edit's two buttons rather than the screen's. The rows clear `+0xb3`, so they show no caret. Selection is `+0xbb`: `0x27` resting, `0x29` selected.

Three buttons are gated, each written as the trio [the repair panel uses](#the-condition-readout): `SAVE` on a row being selected and there being a game to write (`DAT_0048260a`), `RESTORE` on the selected slot's in-use byte, and both `CANCEL` and `ACCEPT` on the rename being live. `EXIT` never gates. `FUN_00437d94`, its handler, returns to the main menu or to the tab strip on `DAT_0048d344` — 0 and 8 respectively, and the save tab sets 8.

### The detail panel

`FUN_0043712c` writes 12 value fields from the selected slot's [staging record](../formats/save-games.md#the-slot-summary--statscpps-scan), or from `estext.bin` entry 0 — the empty string — when the slot holds no save, so the labels stay and the figures blank.

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

## The arming and repair hotspots

Both screens lay clickable rects over a picture of the selected machine. The geometry comes from `gam\arm_hots.dat` and `gam\rpr_hots.dat` ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#gamarm_hotsdat-and-gamrpr_hotsdat--the-clickable-regions)), which carry position and nothing else: **an area's index within its chassis group is its identity**, because the builder passes `handlerTable[areaIndex]` as the panel's click handler. Each handler is a one-line thunk that calls a common function with its own index baked in.

`DAT_00482ae5` is the selected bay slot, 0-7 and `-1` for none, and both screens read the machine out of the eight-pointer array at `00482ac3`.

**Arming** — `FUN_0043dbb2(hardpoint)`, ten thunks, `0043e15f`-`0043e4c8`. It returns immediately when the click is on the hardpoint already selected, then reads the mount pointer at `herc + 0x50 + hardpoint*4` — null for an empty slot, otherwise its first `int16` is the fitted weapon id — and repaints.

**Repair** — `FUN_00433eb9(column, row)`, twenty-five thunks in one table at `0048d1f8` (`004340b5`-`00434ac8`). Sixteen are column 0, the hotspots on the picture; nine are column 1, a list beside it. The pair is resolved into a category and an index within it:

| | `FUN_00433410` category | `FUN_00433431` index | Count | What it selects |
|---|---|---|---|---|
| column 0, rows 0-5 | 0 | `row` | 6 | the external component **groups** |
| column 0, rows 6-15 | 2 | `row - 6` | 10 | the per-hardpoint conditions |
| column 1, rows 0-8 | 1 | `row` | 9 | the internal components |

**That category is the status block's own accessor mode.** `FUN_00411d06(block, mode, index)` takes exactly these three: mode 0 averages an external group, mode 1 addresses the nine internals and mode 2 the ten hardpoints ([`../formats/save-games.md`](../formats/save-games.md#the-66-byte-status-block)). Three independent things agree — the counts, the mode semantics, and `rpr_hots.dat` carrying exactly six areas per chassis — so the six hotspots on the picture are the six named groups `Cockpit`, `Left Torso`, `Right Torso`, `Chassis`, `Left Leg`, `Right Leg`, in that order.

The ten weapon rows are not in `rpr_hots.dat`: the same builder loop places them from the per-chassis `rpr_*.dat` geometry, starting at handler index 6. A row is refused — the selection does not move and nothing repaints — when its slot is past the machine's mount capacity at `+0x4c`, or when the slot is empty. So an unfitted hardpoint cannot be selected on the repair screen at all.

Selection repaints the outgoing entry with `(0x10, 0x27)` and the incoming with `(0x29, 0x29)`; `0x29` is the highlight colour throughout the shell, and `0x10` the resting one.

### The condition readout

`FUN_00433445` refreshes the detail panel from the selection, writing four text fields: the selected component's repair cost, its condition, the whole machine's rebuild cost (`Repair_HercCost(herc, 100)`), and the salvage available — which is `CareerSalvage` **minus** `Armory_QueuedTotal()`, so the figure the repair screen quotes is already net of what the build queue has committed. All four suffix `estext.bin` `0xc8` `kg`.

The condition itself goes through two functions over two in-image tables. `FUN_0043d9cf(condition)` walks `004765c0` = `{89, 79, 59, 29, 0}` and returns the first index whose entry the condition is **strictly greater** than, or 5 if it is greater than none:

| Level | Condition | `estext.bin` word | Colour (`004765ca`) |
|---|---|---|---|
| 0 | 90-100 | `0x68` `Nominal` | 14 |
| 1 | 80-89 | `0x69` `Light` | 13 |
| 2 | 60-79 | `0x6a` `Moderate` | 12 |
| 3 | 30-59 | `0x6b` `Heavy` | 11 |
| 4 | 1-29 | `0x6c` `% Complete` | 10 |
| 5 | 0 | `0x6d` `Unassigned` | 39 |

**The bands are the repair ladder's.** `Repair_LevelForCondition`'s own table is `{90, 80, 60, 30, 1, 0}` tested with `<=` where this one is `{89, 79, 59, 29, 0}` tested with `<`, which partitions identically ([`armory.md`](armory.md#repair-levels)) — the word the screen prints *is* the repair level the machine would be worked to.

**The last two rows are a retail bug.** The word run in `estext.bin` is only four long: `0x67` is the label `Condition:`, `0x68`-`0x6b` are the four words, and `0x6c`/`0x6d` are `% Complete` and `Unassigned`, which belong to the build screen and the crew screen. Nothing in the table is a fifth or sixth damage word — there is no `Critical` or `Destroyed` anywhere in the 342 entries. The index really is the level plus a fixed base, read off the instruction stream rather than the decompiler: `FUN_0043d9cf` returns its loop counter in `EAX` (`xor eax,eax` … `inc eax`, with the comparison kept in `CX`/`DX`), and the caller does `mov esi,eax` / `add si,0x68` before the lookup. Recorded in [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

Three buttons on the panel are gated by affordability, each written as the same trio — `+0xb5` and `+0x4d` to `0x26` when disabled, `0x29`/`0x22` when enabled, and `+0x49` to 0 or 1. That pairing is what makes `+0x49` the enable flag rather than a style bit: it moves with the greying, on a test of whether the player can pay.

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

**There is one backdrop for the whole shell.** `0046dcd4` is written exactly once, by this init, and all eight screen builders pass that same handle as their root's image. So a screen that installs `arming.dpl` is drawing `bay2a_84` through a palette that is not its own — which is never visible in retail, because those screens' content covers the canvas. Anything that draws the frame without the content sees it.

## The palette

**The palette is a widget, not a call.** `DAT_0048d444` is the palette scope from the widget tree above: a `Window` subclass built by `0040ca6c` (vtable `PTR_FUN_0046ef04`, allocation `0x47`) whose `+0x45` is a palette index rather than a lit flag. Its event handler `0040cab7` responds to event 2 by calling `FUN_004075b2(+0x45)` and committing the result. So the shell changes palette by writing `+0x45` between the two visibility calls of `00439da0(index)`, and the second of them is what fires the install.

**Which of those two calls shows and which hides is unresolved**, and the evidence pulls both ways. `Widget_ShowRecursive` (`0041f469`) sets the `+0x11` visibility bit and posts event 2, which is the event this palette scope's callback answers — so it is the show. But `Text_Paint` runs only when that same bit is *clear*, and it is reached by event 4, which only `Widget_HideRecursive` (`0041f2e6`) posts. Settling it needs the event dispatcher, `FUN_00468440`, and `FUN_0041f283`. Nothing here turns on it: the index is written between the two calls either way, so the one that installs a palette installs the one just asked for.

`FUN_004075b2(index)` reads a pointer table of `dpl\*.dpl` paths at `0046dcdc`, twenty entries long:

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

`0043b162(tab)` is the switch that picks one. Tab 3 takes 1, tabs 2 and 4-6 take 2, and tab 7 takes `stage + 4` for the briefing, `stage + 9` for the debrief and 3 or 4 for the map — `4` once `stage - 1 > 3`. Case 8 is not a tab: it hides the root, and is what `0043b0c8`'s callers pair with the refresh.

**The stage counts from one at runtime**, where the save stores it from zero ([`campaign-loop.md`](campaign-loop.md)). Three independent tables say so: the briefing and debrief runs are five long and reached by `stage + 4` and `stage + 9`, the map's Earth-to-Moon switch fires at the same stage the theater run's `luna` sits at, and `maybe_Mission_UpdateLocationTab` carries four `dba\` location names — `alph2`, `delt1`, `omic1`, `brav1` — and branches away to a cutscene entirely when `stage - 1 == 4`. The arithmetic is unguarded in all three places.

That last function also installs the theater palette directly, as `FUN_004075b2(stage + 0xe)`. Because stage 5 branches away before the call, `luna` is in the table and unreached by this path.

## Engine coverage

`Herculan.Engine.Shell` draws the shell frame: the tiled backdrop, the square button and the eight captioned tabs, hit-tested, latching on the six tabs that latch, and gated by `ShellCampaignMode`. The canvas is placed by `ShellScreenLayout`, which scales the fixed 640x480 by window height and centres it, so every rect above is used exactly as the original states it. `ShellPalette` carries the twenty-entry table and the per-tab switch. `--shell-tab-palette` follows it on a tab click, `--shell-palette <name>` pins one entry, `--shell-training` runs the gated half of the strip refresh, and `--shell-tab <n>` opens on a tab rather than on the main menu.

**The save screen is drawn**, from real files: `ShellSaveSlots` reads `sav\GAMEFILE.STR` and each `GAME_?.SAV` it marks in use, and `ShellSaveScreen` places every widget above from the same parent-relative rects and prints the detail panel from the staging record. Clicking a row moves the selection and the summary follows; `SAVE` and `RESTORE` gate as the original gates them. The slot rename, and every button's action, are not ported.

**The widget paints run in palette indices, not in quads.** `ShellSurface` is an 8-bit indexed canvas with the primitives the paints are built from, `ShellChrome` ports the five paints onto it, and the result is resolved through the palette and uploaded as one texture per repaint. That is the original's own model and two of its details depend on it: the ink remap that gives a widget its text colour cannot be done on resolved colours, and index 0 staying untouched is what lets a dithered panel body show the backdrop through it. Clipping each paint to its own widget is likewise load-bearing rather than defensive — the title bar's hatch is drawn 700 pixels wide for a 357-pixel panel.

Following the tab is off by default, which is a presentation choice and not a fidelity one: the four tabs on `arming.dpl` have no content ported, so nothing covers the shared backdrop there and switching would put a visibly wrong bay on screen and read as a palette bug. The save screen is on `palette.dpl`, the entry the shell already uses, so it is unaffected either way.

The engine reloads the whole of `ShellArt` to change palette, where the original re-installs one and lets the hardware palette do the rest — the art here is decoded to RGBA once per palette rather than kept as indices. Same result on screen, at a few milliseconds per click.

Not drawn: the other seven tabs' content, the mouse cursor (`dba\cursor.dba`), and the sounds each button plays. Nothing sets the campaign mode from a save, so the gate is driven by a command-line flag. See [`../../ROADMAP.md`](../../ROADMAP.md).

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| A button's three frame pointers are unlit, lit and disabled | `ButtonIcon_Ctor` really does take and store three, and a third face for a widget the strip refresh can gate is the natural guess. Neither paint reads `+0x59`: both branch on the lit flag between `+0x51` and `+0x55` only. A gated tab in retail looks exactly like an idle one |
| `SHELL0.VOL`'s `dba\` and `dfn\` are the 320-wide halves of a pair, as they are in the simulator archives, and shell art must be doubled to reach the canvas | There is no `hba\` or `hfn\` in `SHELL0.VOL`. The shell has one set and authors its screens at the size that set draws at — the `{0, 0, 0x27f, 0x1df}` panel is 640x480 and the button plates fill their 75x24 rects at 1:1 |
| A screen's widget rects come from its `gam\rpr_*.dat` or `gam\arm_*.dat` file, the way a cockpit widget's come from the herc's `.GAU` | Those files exist and do carry widget geometry, which makes the inference natural. They cover the per-chassis content panels only; the screen builders that place everything else read no file at all |
| `warmingi.cpp` is a warning dialog | It is `w` + `arming` + `i`, the weapon-fitting screen, in the same naming pattern as `wsrvbayi.cpp`, `wcrewi.cpp` and `warmoryi.cpp` |
| The tab screens are drawn through `dpl\bay.dpl` | `SHELL0.VOL` carries one, and the bay screen's own name makes it the obvious candidate for the palette the bay installs. The table at `0046dcdc` does not contain it: index 1 is `dpl\palette.dpl`. Nothing traced so far selects `bay.dpl` at all |
| Exactly one tab is latched at all times | Seven of the nine handlers latch their own plate and it is easy to assume the other two do too. `MAIN MENU` and `SAVE` clear all nine and write none back, so the strip is drawn with nothing lit while either is up |
