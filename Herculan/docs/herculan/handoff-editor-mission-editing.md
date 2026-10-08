# Handoff — mission editing in the HERCULAN Editor

Feasibility and scope for three features in `Herculan.Engine.Host.Editor`:

1. Showing and selecting the mission's non-object features — trigger areas, routes, groups, objectives and the action wiring between them.
2. Moving a herc to a new starting position.
3. Saving the edited mission back to `script.dat`.

## Headline

None of this is a from-scratch build. `MissionLoader` resolves almost every mission feature the editor could show into `Mission`, and the editor already shows and selects trigger areas, routes, the mission box, groups and the action wiring ([In place](#in-place)); what is left of the first feature is the same kind of drawing and UI work. The `script.dat` writer exists and is complete: `ScriptDatTransformer.Write` covers all 13 blocks, and `HercWorks.UI.MissionScriptForm` already drives it through an open/edit/Save As UI. What the write path lacks is that the 3D editor discards the parsed document.

For the move, the design work concentrates in how a per-object position is represented; the question of whether DBSIM can read a grown file is settled (it can — [gap 3](#3-three-consequences-of-appending)).

## What already exists

- `Mission` carries `Actions` (each `MissionAction` with its resolved `MissionTriggerArea` list), `ActionTimers`, `GroupOrders` (each order's `Route` resolved to points), `PlayerRoute`, `Objectives`, `GroupDeploymentActions`, `GroupKinds`, `GroupSides`, `GroupOutOfActionReports`, `Coordinates` and `Text`. Each `MissionPlacement` carries its `GroupIndex`, `SlotIndex`, `EngagementActionRef`, `DefeatActionRef`, `StartingCondition` and `OutOfActionReport`.
- `SceneItem.Visible`, for hiding waiting groups; the editor's own `GroundPick`, which a drag gizmo needs too.
- `MissionLoader.AddRoster` — `Coordinate(script, record.PositionRef) ?? OffsetFromGroup(...)`. The engine already honours a roster record's own position override, matching `Mech_AttachToGroup` (`00417aa8`). The move mechanism is implemented; nothing writes to it.
- `MissionPlacement.Kind` + `SlotIndex` address a block-7/8/9 record exactly, and `GroupIndex` addresses block 11, so the editor's selection already identifies the record to edit.
- `HercWorks.UI.MissionScriptForm.OnSaveAs` — a working save path, with VOL entry-prefix preservation and an advisory cross-block ref-range validator.
- `MissionGenerator` (Core) builds a `script.dat` from an `.MSN` as VSHELL does, and `MissionFileTransformer` round-trips `.MSN` files.
- `HercWorks.Query actions|orders|objectives` — the corpus cross-check for anything the editor shows.

## What a saved `script.dat` reaches

VSHELL writes `data\script.dat` afresh from the `.MSN` every time it loads a campaign or practice mission, and so does this engine's own campaign launch (`ShellCampaignLaunch`, through `MissionGenerator`). An edited `script.dat` is therefore played by: the editor and `Herculan.Engine.Host` opening that file directly; DBSIM run directly over `data\`; and a save slot, since `Career_LoadSlot` (`00412bbf`) copies `sav\script%d.dat` into `data\` and the replay flies it as-is ([`../retail/formats/save-games.md`](../retail/formats/save-games.md#the-slot-handoff)). It does not change a campaign mission. That needs `.MSN` editing — GUID-keyed rows, conditions and variants, the `.ENG` text — a separate and larger project this plan does not cover.

## Mission features to show and select

Counts below are over the 62 retail `.MSN` files with campaign conditions not applied (`HercWorks.Query`).

### In place

- **Selection and panels.** `EditorSelection` is an object, group, trigger area, action or route (with an optional waypoint), held in one `SelectionState` that the viewport click, the `MissionOutliner` (left: groups with members, areas, actions, routes) and the `PropertiesPanel` (right, one view per kind, every mentioned record a link) all set.
- **Picking.** `ScenePicker` tries a waypoint in screen space, then an object's bounding sphere, then `GroundPick` (a ray march against `HeightAtWorld`) and the smallest trigger area containing that ground point.
- **Overlays.** `MissionOverlay` draws, through `DrapedLines`, every trigger area (coloured by who trips it), every route (the player's, the walked ones and the never-walked ones in three colours, loops closed, waypoints numbered when selected) and the mission box with its HDD-map and abort margins, each toggled from the View menu. A line hidden behind terrain shows dimmed.
- **Wiring.** `MissionIndex` cross-references, once, which actions test each area, which groups walk or merely name each route, and for each action the timers, engaged and defeated objects that fire it and the waiting groups, ended orders, started timers, counters and message (from `COMMAND<n>.STR`) that follow. The action view shows all of it, and an area's view shows it for every action testing that area. Selecting a group boxes all its members; its view lists members, deployment action and its ten orders with subject, route and ending action.

Counts for the retail missions: 58 of 62 use trigger areas, 257 distinct areas named 143 times as boxes and 132 as circles, 16 shared by more than one action. Subjects are the player in 142 of 338 actions and the player's group in 170. `C2_01`'s boundary strips and `TRAIN8.MSN`'s three defeat-chained reinforcement waves ([`../retail/simulation/mission-deployment.md`](../retail/simulation/mission-deployment.md#train8msn-end-to-end)) are good test cases.

### Groups and orders in the scene (blocks 10, 11)

What the group view does not yet show: the group's own point and heading (the loader's `Group` record is private, so it needs exposing on `Mission`), ghosts of the formation slots it leaves empty, its formation, paints-ground flag and out-of-action counter writes, and a line from the group to each order's subject — what a guard, follow or search-and-destroy order is aimed at.

An order's own point (`+0x04`, set in 47 orders) is read only by the briefing map, and only off the squad's first order ([`../retail/simulation/ai-goals.md`](../retail/simulation/ai-goals.md#the-order-record)); mark it as a distinct briefing-only marker or leave it out.

Size: small–medium.

### Groups waiting to deploy

The editor currently draws every placed object, so groups held back on an action stand at their placeholder points — routinely stacked on the player's spawn. Their position is meaningless except for the in-place verb ([`../retail/simulation/mission-deployment.md`](../retail/simulation/mission-deployment.md#arrival--group_deploymentcheck-004236c4)). Add a toggle to hide or ghost them, and label each with its gating action and arrival (drop pod, on foot, in place). Held-back structures (54 records in five missions) are the exception worth showing in place: they stand solid but undrawn from the start.

Size: small to hide (`SceneItem.Visible`); small–medium to ghost, which needs a tint or alpha path in `SceneRenderer`.

### Objectives (block 12)

An outliner list: mandatory or failure condition, the condition asked, the subject (selecting the objective highlights it), condition 0's route, the failure text from `mission.str`, and the counter writes. 127 records across the corpus; the subject is a group 92 times, a mech 22, a structure 13.

Size: small–medium.

### Action graph

The action view walks the wiring one link at a time; a node graph of the mission's actions, timers and the objects and groups between them would show it at once. Timers have no outliner entry or view of their own yet, only their mention in the actions they touch.

Size: small for timers; medium–large for the graph.

### Base pads

A paints-ground base group's point picks a terrain tile only; the formation fixes where in the tile the group stands ([`../retail/formats/script-dat.md`](../retail/formats/script-dat.md#the-anchor)). Outlining the tile explains why a base does not sit on its mission point, and is what a move of a base group has to show.

Size: small.

### Mission lint

One check pass whose findings link to the selection: dangling refs (`MissionScriptForm`'s validator, moved into Core so both apps share it); an action area ref behind its first `-1`, which is never tested; an objective with condition 5 or a subject kind above 3; a group with neither a heading nor a slot-0 order, which faults DBSIM at load; a mech group with no order; rosters over DBSIM's slot caps ([gap 3](#3-three-consequences-of-appending)). No retail mission trips the four DBSIM-side checks, so they matter only once editing exists.

Size: small–medium.

### Left out

- **Drop-pod and on-foot arrival points.** Picked at run time relative to the player's position and heading when the action fires; nothing static to draw.
- **Detection and engagement ranges** (50,000 and 100,000 units). Simulation constants, not mission data; a per-object debug ring at most.
- **Variants and conditions.** `script.dat` is already resolved; these exist only in the `.MSN`.

## Gaps on the write path

### 1. The editor drops the source document

`MissionLoader.Load` parses a `ScriptDat`, builds a `Mission`, and lets the `ScriptDat` fall out of scope. The read-only features do not need it — `Mission` carries what they show — but nothing can write back. Carrying it on `Mission` (or returning a pair) is small work, but touches `Mission`'s 18-parameter constructor and `MissionScene`.

### 2. Positions are indirect

An object does not hold a position; it holds a `PositionRef` index into block 1. Every roster record across all 10 retail samples carries `-1` there, so every object inherits its group's point plus a formation offset. Moving one herc means giving it a coordinate of its own. Two routes:

- **Append** a block-1 entry and point the record at it. Index-safe: every other block indexes block 1 by array position, so appending at the end shifts nothing. This is why `MissionScriptForm` refuses add/remove elsewhere.
- **Mutate** the coordinate the object already resolves to. Only correct when nothing else references it, which needs a refcount across blocks 1/3/4/7/8/9/11 and 12.

Recommended: reuse an identical existing point, otherwise append; refcount before ever mutating in place. Moving a waypoint or an area corner is the same problem.

### 3. Three consequences of appending

- **Block 1's extent frames the heads-down map** (see [`../retail/formats/heads-down-display.md`](../retail/formats/heads-down-display.md#the-maps-frame-of-reference), and `MissionBox`), and the mission box. A point outside the current box silently rescales the player's in-game map and moves the boundary warning and abort lines. Warn on it; the mission box overlay already shows the lines it moves.
- **File length is not a constraint.** `DBSim_LoadScriptDat` (`00424308`) reads through a `FileRStream` block by block and allocates each of blocks 1-6 and the group array from its own count, so a file longer than the retail 13,520 bytes reads correctly. Its fixed arrays are per-roster slot flags and type lists: block 7's count plus the squad size is capped at 100, block 8 at 50 and block 9 at 140, with no bounds check — past them it writes over neighbouring globals. Appending block-1 points is unaffected; adding roster records is not. VSHELL's own reader, `ShellMap_LoadScriptDat` (`004243d7`), has not been checked for caps, and it reads a save slot's file for the briefing map.
- **The override path has never been observed live.** It is RE-derived and engine-implemented, but no retail file exercises it. Confirming it means writing one and running retail DBSIM.

### 4. Moved is not the same as stays there

- `MissionPlacement` carries `FormationOffset` even when the record supplies its own position, and AI followers hold formation on their leader every tick. For a non-leader group member the edit sets a spawn position the AI may walk out of. Correct behaviour, but it reads as wrong if the UI does not say so.
- A group with no point of its own stands on its route's first waypoint. Moving the group means either giving it a point or moving that waypoint, which moves the start of the route too.
- A paints-ground base group snaps to a fixed spot in its tile, so a small move does nothing and a larger one jumps a whole tile ([Base pads](#base-pads)).
- A group waiting to deploy is placed at a placeholder; moving it changes nothing unless it arrives in place ([Groups waiting to deploy](#groups-waiting-to-deploy)).

### 5. Live scene refresh

`items[]` in the editor's `Program.cs` is a flat array with transforms baked once at `window.Load`, with the pick spheres alongside it. Moving an object needs both updated plus a terrain-height query for the new ground level. Cheap, but needs a `SceneObject` to item-slot index that does not exist yet.

### 6. Round-trip coverage

`TrainingLaunchTests.LoadsEveryRetailMission` builds a `script.dat` from each of the 62 `.MSN` files and checks `ScriptDatTransformer` parses and rewrites it byte-exact. The 10 real `script.dat` files, which carry stale tails past block 13, are not under test: their byte-exact claim lives only in the format doc. A test over them, comparing through block 13's declared end, is the remaining piece. Both skip silently without an install.

## Scope

| Slice | Size |
|---|---|
| Groups and orders in the scene | small–medium |
| Base pads | small |
| Waiting groups: hide / ghost | small / small–medium |
| Objectives | small–medium |
| Timers / action graph | small / medium–large |
| Mission lint | small–medium |
| `script.dat` round-trip test over the 10 real files | small |
| Carry `ScriptDat` through to the editor | small |
| Save button, backup/temp-file write | small |
| Position write-back: reuse/append, refcount, box warning | medium — the bulk of the edit path |
| Numeric X/Y/Z entry in the Properties panel | small |
| Drag gizmo (ground pick, handles) | medium |
| Live scene and pick-sphere refresh | small–medium |

## Suggested sequencing

1. Waiting groups, groups and orders in the scene, objectives, timers.
2. The round-trip test, carrying the document through, then numeric entry plus save — which proves the write chain end to end with almost no UI.
3. The drag gizmo on top, reusing the ground pick; then waypoint and area-corner moves through the same point-ref machinery; then lint.
