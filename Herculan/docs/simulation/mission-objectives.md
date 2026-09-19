# Mission objectives (DBSIM.EXE)

What the mission wants done, what loses it, and the one number the whole layer produces. The records are `script.dat` block 12 and their text is block 13 plus `data\mission.str`; see [`../formats/script-dat.md`](../formats/script-dat.md#block-12-in-memory--76-bytes-0x4c) for the layout and [`../formats/msn-mission-file.md`](../formats/msn-mission-file.md#row-17-field-decode--the-objective-record-dat_0047064a-58-bytesrecord) for where an author writes them.

This is a separate mechanism from the mission **actions** in [`mission-deployment.md`](mission-deployment.md). An action is a latch that makes something happen; an objective is a question that is asked over and over and never makes anything happen. They share only the mission-counter array.

## The record

An objective is **one condition asked of one subject**, plus what satisfying it does to the mission counters. `Mission_EvaluateObjectives` (`00413280`) walks the array each poll.

| field | meaning |
|---|---|
| `+0x00` | **required.** `== 1` means the objective must be satisfied; **anything else means the opposite** — the record is a failure condition and the mission is lost the moment it comes true |
| `+0x02` | which condition is asked |
| `+0x04` | subject kind: 0 group, 1 mech, 2 flyer, 3 base |
| `+0x06` | the resolved subject |
| `+0x0a` | a block-1 point. No condition reads it, and it is `-1` in all 62 retail missions |
| `+0x0e` | a block-3 waypoint group — which of the subject group's ten orders condition 0 is about |
| `+0x12` | the resolved failure text, four `char*` |
| `+0x24`/`+0x38` | ten mission-counter refs and ten operations |
| `+0x22` | runtime: the counters have been applied |

**The counter operation codes are not the action layer's.** Here 4 sets, 5 clears, 6 increments and 7 decrements; `Action_Activate` knows only 5 and 6 and reads them as clear and increment, so the two layers disagree on 6 with nothing to warn a reader. A slot is skipped unless **both** its ref and its operation are non-negative. The array is [`mission-deployment.md`](mission-deployment.md#the-mission-counters--dat_004a9ef4)'s.

The counters are applied the first time the condition holds and never again; the latch is not "the objective is met", which is re-read every poll.

## What each condition asks

The original writes the eleven cases out twice, once for a group subject and once for an object one. They are the same eleven questions.

| code | of a group | of an object |
|---|---|---|
| 0 | the order that runs the record's waypoint group is flagged complete (`Mission_GroupOrderCompleteOnRoute`, `0041324c`) | the same, asked of the object's own group |
| 1 | written off — `Group_ConditionTier` at or past the side's threshold | destroyed or immobilised (`+0x99 \|\| +0xa4`) |
| 2 | not written off, and every living member is clear of threats | clear of threats |
| 3, 4 | the player has completed a data link (`player+0xa0`) — the subject is not looked at | as for a group |
| 6 | any member has been engaged (`+0x9e`) | engaged |
| 7 | **every** member is disarmed (`+0xa5`) | disarmed |
| 8 | condition 6 negated | condition 6 negated |
| 9, 10 | the player has *not* completed a data link | as for a group |

**Code 5 has no case in either switch**, and neither does a subject kind above 3. The original leaves its working register untouched, so such a record silently answers whatever the record before it answered. No retail mission reaches either: across the 127 objective records in the 62 `.MSN` files the codes used are 1 (67), 2 (27), 0 (19), 3 (5), 6 (5), 4 (3) and 7 (1), and the subject is a group 92 times, a mech 22 and a structure 13 — never a flyer. 91 records are mandatory and 36 are failure conditions. **Codes 8, 9 and 10 are unused as well**, so the two negations and one of the two data-link readings are exercised by nothing that ships.

**A group's write-off threshold is not the same for both sides** (`FUN_00413920`): a human group is written off at condition tier 3, a Cybrid one only at 4. So "wipe out this Cybrid group" means every machine, and "this convoy did not make it" is answered a tier earlier. The tiers are [`ai-goals.md`](ai-goals.md)'s.

### Clear of threats — `Mission_IsClearOfThreats` (`004137b4`)

The escort objective's whole test, and the gate on every conclusive mission status. It walks the live object list, skipping anything undeployed, on the subject's own side, or already out of the fight, and the subject is **not** clear when a survivor either:

- knows about it (`Ai_KnowsObject`, [`ai-targeting.md`](ai-targeting.md)) and is within 80000 units — 100000 if the subject is that machine's own selected target; or
- **for a subject not on the player's group**, belongs to a group whose current order names the subject, and is either still on its way (its route has somewhere left to go) or already knows where the subject is. An assigned hunter counts at any range, which is what stops an escort being called safe while something walks towards it.

## The status — `Mission_Status` (`004135e8`)

The judgement, in the original's order: the player's own condition first, then the mission box, then the objectives.

```
if (player destroyed)                      2
else if (player immobilised)               3
else if (!quiet && outside box + 110000)   8
else if (!quiet && outside box)            7
else                                       EvaluateObjectives()
```

The box is block 1's own extent, accumulated as the coordinates are read (`FUN_0041373c`); the Heads-Down Display's map is framed by the same one ([`../formats/heads-down-display.md`](../formats/heads-down-display.md)).

**`quiet` is the third argument, and it means "just answer the question".** Set, the function skips the 500 ms hold, both box arms and the `SYSTEM.STR` post, and only computes. The poll clears it; the player's own [Q] clears nothing else and sets it — so **a [Q] can never answer 7 or 8**, and a player standing outside the mission box is told how the objectives stand as though they were inside it.

`EvaluateObjectives` reduces the array to three outcomes and then splits each by whether the player is clear:

| | player clear | player still in contact |
|---|---|---|
| a failure condition holds | **6** mission failed | 10 |
| every required record holds | **9** mission successful | 10 |
| neither | 5 | 4 |

**The mission does not conclude while the player is still in a fight.** All three outcomes collapse to 10 there, and 4, 5 and 10 announce nothing. `FUN_0042412c` writes `(status == 9)` into `results.dat` as the mission ends, so 9 is the only success.

The first required record that is *not* satisfied is published at `DAT_004d1f1c` as the failure text the alert panel prints — four `char*`, three from the file and an empty fourth.

## What the computer says

Four statuses carry a `SYSTEM.STR` line, posted on the **change** rather than each poll:

| status | line |
|---|---|
| 6 | `0x16` MISSION FAILED |
| 7 | `0x1e` APPROACHING MISSION ZONE BOUNDARY |
| 8 | `0x20` RULES OF ENGAGEMENT VIOLATED. MISSION ABORTED. |
| 9 | `0x17` MISSION SUCCESSFUL |

After a post the answer is held still for 500 ms so the caller's next poll cannot queue the line twice; the running baseline catches up on the first evaluation after that, which is what sequences the spoken line ahead of the alert panel. `MISSION OBJECTIVES COMPLETE`, `PRIMARY OBJECTIVE COMPLETE` and `SECONDARY OBJECTIVE COMPLETE` are recorded but posted by nothing — see [`../formats/cockpit-messages.md`](../formats/cockpit-messages.md#posters).

## The poll — `Mission_PollStatus` (`004131ac`)

`Sim_MainTick`'s last act, run with the player's machine and only while it is not destroyed. Two countdowns shape it, and between them they are why a finished mission takes tens of seconds to say so:

- **The poll interval** (`DAT_004a9ee6`) is re-armed to 10 s every time the answer is not worth raising, so the objectives are read about once every ten seconds. A player **outside the mission box** skips the interval and is read every tick, which is what makes the boundary warning prompt.
- **The alert delay** (`DAT_004a9ee9`) is armed once, the first time an alert-worthy status appears, and the status is not handed up until its 10 s runs out. A destroyed player skips it.

A status is worth raising when `DAT_0049935c[status]` is set — 2, 3, 6, 7, 8 and 9. The caller builds the [status alert](#the-status-alert--gnl_alrt-00455934) for it. `DAT_004a9ed0` is the status already raised, which is what stops the same one being raised twice; `Mission_StatusForAlert` (`00413180`) is the wrapper both this and [Q] go through, and **the [Q] path writes that baseline as well** — reading the status yourself is enough to stop the poll announcing it.

`Sim_MainTick` also rewrites one answer before it builds the panel: a status 3 whose `FUN_00423f08` says the machine went down in Cybrid-held ground becomes **18**, which is the same "disabled" alert with a worse ending.

## The status alert — `gnl_alrt` (`00455934`)

The panel that says how the mission stands, and the only thing in the simulator that ends one. Two ways in, and they build the same panel from the same status:

- **[Q]**, scancode `0x10` in `Sim_DispatchCommand`. Asks `Mission_StatusForAlert(player, publish)` with the publish flag set — a quiet evaluation, so never 7 or 8 — and raises the panel for the answer.
- **the poll**, once the answer is worth raising and its ten-second delay has run out.

Either way the caller then compares the button the player pressed against `DAT_0049f5d8[status]`, and **that comparison is the whole of what ends a mission**. It propagates out of `Sim_DispatchCommand` through `Sim_PollPlayerInput` and `Sim_MainTick` as the tick's own return.

| status | panel | ends on |
|---|---|---|
| 2, 3, 8, 18 | one button, `CONTINUE` | that button — the mission is already over |
| 7 | one button, `CONTINUE` | **nothing.** Its entry is 1 and it has no button 1, so the boundary warning can only be acknowledged |
| 4, 6, 9, 10 | two, `CONTINUE` + `ABORT MISSION`/`RETURN TO BASE` | the second |
| 5 | two, `CONTINUE` + `QUIT ANYWAY` | the second |
| 17 | one button | that button, but its caller is not this panel's |

### Its text

`str\GNL_ALRT.STR`, three groups read in file order and indexed by the status: 20 titles, 40 button captions (two per status) and 80 body lines (four per status). Rows **0** (`PAUSE`) and **1** (`EXIT EARTHSIEGE?`) are not this panel's — they belong to the [pause panel](#the-pause-panel--004561c0), built from the same table at a different size.

**Status 5's body is not from the table.** The constructor replaces it with `DAT_004d1f1c`, the four `char*` the first unsatisfied mandatory objective carries — the mission author's own `mission.str` lines. So the table's `YOUR MISSION IS NOT COMPLETE.` is what a status 5 would read with no objective outstanding, and what the player actually sees is whatever that mission's author wrote.

Rows **11-16 and 19** are a canned set of the same idea, one line per objective condition (waypoints, detect, protect a base, protect a squad, data link, find and destroy, find and protect). **No writer reaches them.** The raw-opcode scan for calls to this constructor finds four sites and no more; two pass a constant, one patches 3 to 18, and the fourth passes `Mission_StatusForAlert`'s answer, which `Mission_Status` and `EvaluateObjectives` between them confine to {2,3,4,5,6,7,8,9,10}. Row 17 (`INSUFFICIENT MEMORY FOR MAXIMUM DETAIL`) has no writer either. The status-5 substitution is the mechanism that replaced them.

### Geometry

Same arrangement as the objectives panel: written to `.bss` once as `value << VideoMode_?CoordShift`, panel-local rects, centred by the declared size.

| Global | Device | Is |
|---|---|---|
| — | 444 x 218 | the panel's declared size, centred on 640x480 at origin (98, 131). The plate is 444x**214** |
| `004d1f22`/`24` | y 0, height 16 | the title bar |
| `004d1f28` | 160 | the one button's x, when the status has one |
| `004d1f2a`/`2c` | 82, 240 | the two buttons' x, when it has two |
| `004d1f30`, `32`, `36` | y 160, 122 x 18 | every button's y and size. The `ALERT` plate is 124x22 and overhangs |
| `004d1f3a`/`3e` | x 100, width 244 | the body block |
| `004d1f3c`, `40`/`42` | y 60, height and pitch 20 | its four rows |
| `004d1f20`, `26`, `38`, `2e`, `34` | 0, 0, 0, 186, 18 | label margins; the last two are written and never read |

The title is `title` and the buttons `active`/`pushed`, as everywhere in this family. **The body is `green6x8`** (`DAT_004d1eb0`), which is where this panel's green comes from — the objectives panel's yellow is `cpylw`.

### Paint — `00456068`

Plate, title, body, then each button. The body count stops at the **first empty line** rather than skipping it, and then:

**A one-line body is drawn on row 1, not row 0.** `lineCount == 1` offsets the whole run by one row so a short message sits nearer the middle of the plate than the top of the block. Two lines or more start on row 0.

A line holding a single space is not empty and does not stop the count, which is how a mission's own third `mission.str` line — `" "` in the shipped training mission — reaches it and draws nothing.

### Closing it

`AlertPanel_HandleEvent` again, and the same modal loop with one addition: `DAT_004d25b6`, the abort flag the input poll sets, closes the panel from under it. Its `OnChildClick` (`00456160`) writes 2 for button 0 and 3 for button 1, and the loop returns that `& 1` — so the caller reads 0 for the left button and 1 for the right. [Return] presses the focused button and [Esc] the cancel widget, both of which the panel sets to button 0, so **neither key can ever be the answer that ends the mission**; only the pointer can reach button 1.

## The pause panel — `004561c0`

The status alert's small sibling, and the same behaviour: the same base, the same modal loop, the same `GNL_ALRT.STR` read the same three ways, and **a vtable whose six entries are byte-for-byte the other's**. `FUN_00455908` is the intermediate constructor both go through, which chains `AlertPanel_CtorBase` and installs that vtable; `PausePanel_Ctor` then overwrites the vtable pointer with its own duplicate.

What differs is size and arrangement. Two statuses reach it, both as constants from `Sim_DispatchCommand`:

| Command | Key | Status | Panel |
|---|---|---|---|
| `0x19` | `P` | 0 | `PAUSE`, one button: `CONTINUE` |
| `0x410` | `Ctrl+Q` | 1 | `EXIT EARTHSIEGE?`, two: `CONTINUE` and `QUIT` |

The manual agrees with both — "Pause the mission at any time by pressing [P]; resume by clicking Continue or pressing [Enter]", and "You can exit the game at any time by pressing [Ctrl]+[Q]" — and it is what fixes `0x400` as the `[Ctrl]` bank ([`../formats/cockpit-input.md`](../formats/cockpit-input.md#keyboard-commands-are-scancodes)).

**Neither answer ends a mission**, so neither goes through `DAT_0049f5d8`. `[P]`'s one button returns 0 and the dispatcher passes that straight out, which is simply "carry on"; `[Ctrl+Q]`'s second button sets `DAT_004d2582`, the global quit flag — the same one `AlertPanel_Present` watches each pass to tear down any panel still up.

### Geometry

| Global | Device | Is |
|---|---|---|
| — | 178 x 68 | the declared size, centred on 640x480 at origin (231, 206). The plate is `GNL_ALRT.HBA` **frame 1**, 181x70 — *larger* than the declared size, where the other two panels' plates are smaller |
| `004d1f4a`/`4c` | y 0, height 16 | the title bar |
| `004d1f50`/`52` | (28, 24) | the one button, when the status has one |
| `004d1f54`/`56`, `58`/`5a` | (28, 16), (28, 42) | the two buttons, when it has two. **They share an x and stack**, where the status alert's pair sits side by side |
| `004d1f5c`/`5e` | 122 x 18 | every button's size. The `ALERT` plate is 124x22 and overhangs, as everywhere in this family |
| `004d1f48`, `4e`, `60` | 0 | label margins |

**The three button y-origins are scaled by the horizontal shift**, not the vertical one — the constructor's own slip, and the only place in the family where an axis is crossed. It costs nothing: both of DBSIM's coordinate shifts are equal in both video modes, so the numbers come out the same.

### It has no body labels

The constructor builds a title and its buttons and stops — it never creates the four body labels its shared paint writes to. That paint runs anyway, and would index an array the constructor never filled. **It is saved by its own data**: `GNL_ALRT.STR` group 2 is empty for statuses 0 and 1, so the paint's count loop stops on the first line and the write loop never runs.

## The objectives panel — `obj_alrt` (`0045751c`)

What [F11] puts up: a plate over the frozen cockpit listing block 13, with one button. `[F11]` is scancode `0x57`, which `CockpitWidgets_HandleCommand` answers by constructing the panel, running its modal loop and destroying it. **Nothing in that loop answers `0x57` again**, so a second [F11] does not take the panel back down.

Its resources are SIMALERT.VOL's, alongside the [status alert](#the-status-alert--gnl_alrt-00455934)'s and the other two panels' (`prf_alrt` and `ctl_alrt`, both in [`preferences.md`](preferences.md)):

| Resource | Holds |
|---|---|
| `hba\OBJ_ALRT.HBA` | one frame, 630x230 — the whole plate. Index 0 appears four times in it, the rounded corners |
| `hba\ALERT.HBA` | frames 0 and 1, 124x22 — the button at rest and held. They differ only in the border's palette index |
| `str\OBJ_ALRT.STR` | two groups of one: `OBJECTIVES` and `RETURN`. (`stf\` and `stg\` are the French and German twins) |

### Geometry

The constructor writes the whole block into `.bss` once, as `value << VideoMode_?CoordShift`, so every number below is an authored 320-wide coordinate doubled. Rects are panel-local — (0, 0) is where the plate is blitted.

| Global | Device | Is |
|---|---|---|
| `004d1f84`/`86` | 630 x 278 | the panel's declared size, which is what `FUN_00454f34` centres on the 640x480 screen: origin (5, 101) |
| `004d1f8a`/`8c` | y 0, height 16 | the title bar the title is centred in |
| `004d1f90`..`96` | 254, 186, 120 x 20 | the button. Its plate art is 2px larger both ways and is blitted at the rect's origin, so it overhangs |
| `004d1fa0`, `004d1f9c`/`a4` | y 34, pitch and height 20 | the seven objective lines |
| `004d1f88`, `8e`, `98` | 0 | title and button label margins |
| `004d1f9a`, `9e`, `a2` | 40, 40, 360 | written and never read |

**The plate is 230 rows, not the declared 278.** The panel is centred by the declared height, so the art sits 24 rows above the middle of the screen and the bottom 48 rows of the panel's rect are empty.

**The objective lines are not centred on the panel.** Their rect takes x from `panel+0x04` and `panel+0x0c` — the *absolute* screen pair — where every other rect the constructor builds uses the panel-local one at `+0x1c`/`+0x24`. The labels are centre-aligned, so the text lands `(screenWidth - 630) / 2` pixels right of the panel's centre line while the title and the button sit on it: five pixels at 640x480, and visible against the title in any retail capture.

### Paint — `FUN_00457b58`

Plate at the panel origin, then the title, then the lines, then each widget's own paint. The line loop **skips an empty string rather than leaving its row blank**, and counts only the lines it filled: an eighth non-empty entry is dropped, because the constructor builds seven labels. Block 13 has ten slots, but row #4's sub-array A fills one to four of them across the 62 `.MSN` files ([`../formats/msn-mission-file.md`](../formats/msn-mission-file.md#row-4-field-decode--the-missions-text-package-dat_00470668-144-bytesrecord)), so nothing authored reaches the cap.

Every label is centre-aligned and placed by `Label_SetRect`/`Label_SetText` ([`../formats/mfd.md`](../formats/mfd.md#label-placement)). The title draws in `title`, the lines in `cpylw`. The button's caption label is constructed in `cpylw` too and never drawn in it: a button's paint (`FUN_00454ff8`) overwrites the label's font from its own four-entry table every time, so the caption is `active` at rest and `pushed` while held — which is why RETURN reads grey against yellow objective text.

### The loop — `FUN_00457ae4`

Poll input, hand the event to the panel's key handler, repaint the widgets, present; repeat until the close flag is set. **It never calls the sim tick**, and entering (`FUN_00454630`) pauses both message ports and saves the framebuffer — so the cockpit behind the panel is frozen, not merely undrawn. Input the loop polls is still dispatched to the cockpit's own widget tree and to the player's machine by `Input_BuildPlayerDevice`, so piloting keys are not swallowed; with the tick stopped they just have nothing to act on.

What closes it, from `FUN_00454e10`:

| | |
|---|---|
| [Return], or joystick button 1 | presses the focused widget, which the loop set to widget 0 before its first pass |
| [Esc] | presses the panel's cancel widget, `+0x2fb`, which the constructor also sets to widget 0 |
| a click on RETURN | `FUN_00455080` forwards to the panel's `+0x0c` slot (`FUN_00457c30`), which sets the flag when the clicked child is child 0 |
| [Tab] / [Shift+Tab], or joystick button 2 | walk the focus. With one widget they land back on it |

## The player think's objective arms

`Mech_BehaviourPlayerThink` (`0041c194`) carries the waypoint arm ([`player-waypoints.md`](player-waypoints.md#the-players-think--mech_behaviourplayerthink-0041c194)) and then exactly one objective arm, chosen by the mission's own selector — `script.dat`'s header at `+0x06`. **They are the only writers of `+0x9f` and `+0xa0` in the image**, and those two flags are what conditions 3, 4, 9 and 10 read back.

| selector | arm |
|---|---|
| 0 | **the mission target is picked up.** One-shot for the whole run: the first time the player's own selected target (`mech+0x1a4`) is what their group's current order names, post `0x19` MISSION TARGET DETECTED and raise `+0x9f`. It fires on the pilot selecting the thing, not on the sensors finding it |
| 5 | **the goal is reached.** Within 40000 ground units of `Mech_AiGoalPosition` raises `+0x9f`. Says nothing, and four times the waypoint arm's range |
| 3, 7 | **the data link**, below |
| other | nothing |

All ten retail `script.dat` files carry selector 0; the other arms are reached from the campaign's own missions.

Selector **3** also reaches into the AI: `Ai_IsTargetable` refuses the current order's target to a group led by the player's machine, so the squad does not shoot the thing the player came to read. Selector 7 does not get that shield. See [`ai-targeting.md`](ai-targeting.md#is-it-a-target-at-all--ai_istargetable-00411e80).

### The data link

The player parks in front of what their order names and holds position. Holding needs four things at once: the subject alive, its group in the mission, the player within 10000 units in three dimensions, and the player's **aim** — body heading plus turret twist — within 45° of it. Nothing tests speed, so the link can be held while walking past. Breaking off posts `0x38` DATA TRANSFER ABORTED and puts the sequence back to the start.

Four lines are spoken, `0x34` to `0x37`, each after the delay the table at `0049a318` gives for the step before it: 5000, 5000, `0xffff9c40`, 0. **The link therefore takes ten seconds of holding station**; the third entry is negative, `Timer_CountDown` clamps at zero, and `DATA TRANSFER COMPLETE` is queued the tick after `TRANSFERRING DATA`.

That is not what the player sees. The two are queued a tick apart but shown ten seconds apart, because `TRANSFERRING DATA` is the one `SYSTEM.STR` entry whose display timings are 10 s and 20 s rather than 3 s and 6 s and the port will not let a message yield before its minimum ([`../formats/cockpit-messages.md`](../formats/cockpit-messages.md#the-port)). So the transfer reads on screen as a long operation while the simulation has already finished it: `+0xa0` goes up when the last line is *queued*.

## The group report, and why nothing shows it

_NOTE: Claude often incorrectly decides that code is unused, when in fact Claude just hasn't yet found the mechanism that calls the code. Treat this section with skepticism._

Eight functions sit among the ones above, read the same order records and the same per-machine flags, and produce a small integer that is plainly a line index. **None of them is reachable.** `Group_StatusLineIndex` (`00412f90`) is the head of the set, and it has no caller: no relative call anywhere in the code section, and the little-endian dword `90 2f 41 00` occurs nowhere in `DBSIM.EXE`, so no vtable, table or callback holds it either. Everything it calls is called by it alone.

```
Group_StatusLineIndex(group, verb):
    writtenOff = Group_ConditionTier(group) == 4
    tier       = min(Group_ConditionTier(group), 3)
    switch (verb) {
        0  writtenOff || !anyMember(+0x9f) ? tier+6 : anyMember(+0x9e) ? tier+2 : 1
        1  !routeExhausted ? tier+6 : tier == 0 && anyMember(+0xa6)     ? 1 : tier+2
        2  !routeExhausted ? tier+6 : tier == 0 && subjectCondition == 4 ? 1 : tier+2
        3  member[0] destroyed || !anyMember(+0xa0) ? tier+5 : tier+1
        4  !subjectArrivedAndClear || subjectCondition > 2 ? tier+5
           : subjectCondition == 0 && tier < 2 ? 1 : max(tier, 1) + 1
        5  subjectArrivedAndClear && anyMember(+0x9f) && subjectCondition != 4 ? tier+1 : tier+5
        6  subjectCondition != 4 ? tier+6 : tier == 0 && !anyMember(+0x9e) ? 1 : tier+2
    }
    return result - 1
```

`routeExhausted` is the **group's own** route cursor at `+0x04`, not the subject's. So each verb answers in one of three bands — 0 for done cleanly, `tier+1`/`tier+2` for done, `tier+5`/`tier+6` for still running — with the group's damage tier sliding the answer inside its band. It is a per-group "how is this squad doing" line, one the mission never asks for.

The six helpers it owns:

| | Asks |
|---|---|
| `Group_AnyMemberEngaged` (`00412d90`) | any member's `+0x9e` |
| `Group_AnyMemberObjectiveSighted` (`00412ef4`) | any member's `+0x9f` |
| `Group_AnyMemberDataLinked` (`00412f28`) | any member's `+0xa0` |
| `Group_AnyMemberScoredAKill` (`00412f5c`) | any member's `+0xa6` |
| `Group_OrderSubjectEngaged` (`00412d4c`) | the current order's subject — the group form for kind 0, the object's own `+0x9e` otherwise |
| `Group_OrderSubjectArrivedAndClear` (`00413a08`) | the current order's subject is deployed and clear of threats |

`Group_AnyMemberEngaged` is the exception: `Mission_EvaluateObjectives` calls it too, which is what makes condition 6 work. The other five are dead with their caller.

`Group_OrderSubjectRouteExhausted` (`004139a0`) sits in the middle of the set and is one step further out still — nothing calls it, the chooser included.

**`mech+0xa6` therefore has no live reader.** `Mech_CreditNeutralisedTarget` latches it on a machine's first cross-side kill ([`damage-system.md`](damage-system.md#what-a-wreck-is-worth--mech_salvagevalue-00418e60)) and only `Group_AnyMemberScoredAKill` ever asks. The same goes for `+0x9f` and `+0xa0` *in their group form* — the objective conditions read the player's own copies directly rather than through these helpers.

## Engine port

`World.MissionObjective` is the record and `Sim.MissionObjectiveState` its runtime half; `Sim.MissionObjectives` is all four of the original's functions — `Poll`, `Evaluate`, `EvaluateObjectives` and `QueryForPlayer`, the last being the [Q] wrapper — and `Sim.MissionStatus` names the values. `MissionScene` resolves each subject the way it resolves an order's; `SimWorld.MissionBounds` is the box and `SimWorld` polls at the end of its tick. `MechObject.PlayerThink` holds the three arms, `SimObject.MissionGoalReached` and `SimObject.DataLinkComplete` are `+0x9f` and `+0xa0`, and `MissionGroup.OrderCompletedForRoute` is condition 0.

Both panels are ported. `Content.ObjectivesPanel` and `Content.StatusAlertPanel` hold their text, their state and the ways they close; `Content.ObjectivesPanelLayout` and `Content.StatusAlertPanelLayout` hold the two geometry tables above, over a shared `Content.AlertPanelLayout` that owns the 640x480 screen, the rect type, the button bank, the three shared fonts and the window transform. `Render.Overlay2DRenderer.DrawAlertPanel` paints either. The plates, the button bank and the four fonts are packed into the cockpit's own sprite atlas (`Content.CockpitArt`), so a panel costs one bind; SIMALERT.VOL is mounted with the rest (`Content.GameContent.SimulatorArchives`). The host owns the keys, the pointer and the stopped tick.

Divergences:

- **`DAT_004a9d7c`, the MISSION TARGET DETECTED latch, has no reset** — two references, both in the player think, in `.bss`. The port holds it per machine, so it resets with the mission rather than with the process.
- **Each panel is built once per mission**, where the original constructs and destroys one per press. Nothing in either changes during a mission except the status alert's status.
- **The panels are placed against the window, not a 640x480 screen**: each is scaled by the same art-pixels-to-window factor the cockpit is and centred horizontally, which is how the three-panel composite is anchored. Their internal geometry, the objectives panel's off-centre lines included, is the tables above unchanged.
- **The variant is chosen by the status, not by the call site.** The original picks a constructor and its statuses happen to fall out disjoint; the port reads the split off the status instead. Every reachable call site in the original agrees with it.
- **Every answer that leaves the simulator closes the window.** The original has two endings here — `[Ctrl+Q]`'s QUIT sets a global flag the whole program watches, and a mission-ending answer returns up through `Sim_MainTick` to the shell, which writes `results.dat` and advances the campaign. With no shell ported, both exit.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| Every objective the player is shown is an objective the simulation tests | Block 13 and block 12 are separate data and nothing reconciles them. An author writes the lines the player reads and the conditions the code tests independently |
| Reaching the last waypoint completes a "travel there" objective | Condition 0 reads the group's order-completed flag at `+0x70`, which `Group_OrderTick` sets — so the objective is about the **order** finishing, not about arrival |
| Meeting every objective ends the mission | It yields status 9 only while `Mission_IsClearOfThreats` also holds for the player. With a live hostile aware and near, the status is 10 and nothing is announced |
| `+0x00` is a priority, with higher meaning more important | The evaluator's only test is `== 1`. One is mandatory; every other value makes the record a failure condition, which is the opposite meaning rather than a weaker one |
| The data link finishes instantly because its third delay clamps to zero | The delay is written before the line it precedes, so the link still needs ten seconds of holding; the clamp only removes a wait after the last line is decided |
| The status alert's body text is a `GNL_ALRT.STR` row chosen by the status | For every status but 5, yes. Status 5 — the one [Q] usually answers — has its body replaced with the outstanding objective's own `mission.str` lines, so the row in the table is not what a player ever reads there |
| `DAT_0049f5d8` is a per-status button count | It is which button index ends the mission. Status 7's entry is 1 against a one-button panel, which is how its warning is made unanswerable rather than a count being wrong |
