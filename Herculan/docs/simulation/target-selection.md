# Target selection and the sensor model

Ported in `Sim.TargetSelection`, `Sim.Detection`, `SimObject.Target`.

## Where the selection lives

`+0x1a4` is the selected target every homing weapon and most of the AI reads. It is a field of the **shared base**, not of the HERC class: an aircraft and an armed structure keep theirs at the same offset, and the two places that read another object's selection — `Ai_SelectTarget`'s scoring and `Mission_IsClearOfThreats` — read it off whatever object they are holding without asking what class it is. **For the player's machine nothing in the simulation writes it.** The selection is made in the cockpit widget tree at `CockpitViewInstance+0x210` and copied onto the machine once a frame by `Player_PerFrameCockpitUpdate` (`0041b130`). AI machines get theirs from a separate family, decoded in [`ai-targeting.md`](ai-targeting.md) and ported in `Sim.Ai.AiTargeting`.

Every writer of `+0x1a4` also maintains `target+0x1a2`, a count of how many objects hold that one, and raises `+0x9d` ("target changed"), which suppresses lock for one tick. The armed and triple-turret structure ticks raise `+0x9d` too, as shared boilerplate; **nothing reads a structure's copy** — the two readers (`Mech_PerTickSystemsUpdate` and `Mech_LockTonePlay`) are both mech-only.

| Key | Scancode | Function | What it does |
|---|---|---|---|
| `Enter` | `0x1c` | `TargetSelect_Cycle` (`0043349c`) | Cycle — rebuild the shortlist, take its head, or step |
| `'` | `0x28` | `TargetSelect_Nearest` (`004333c8`) | Nearest HERC or flyer, ignoring facing |
| `;` | `0x27` | `TargetSelect_SetObject(view, 0)` (`004332dc`) | Clear. Undocumented in the manual |
| `Tab` | `0x0f` | `TargetingPod_CycleComponent` | Step the Targeting Pod's component lock, if one is fitted — [below](#component-targeting--the-targeting-pod) |

`TargetSelect_SetObject(view, obj)` also serves the F4 scanner's TARGET button and a gunsight click. It *walks* `+0x210` through the object list from a stored cursor until it lands on the object asked for, so a request for something unselectable ends with the selection back where it started.

### Cycle's shortlist — `TargetSelect_Cycle` (`0043349c`)

Everything selectable and inside the ±8999 cone is filed into one of four buckets by bearing error (`|err| >> 10`, clamped to 3), sorted by range within its bucket, keeping four. Flattening the buckets in order gives the shortlist: **nearest the crosshair wins, range only breaks ties inside a band**. A repeat press whose rebuilt head is unchanged steps to the shortlist entry after the current selection.

The angular-size correction the function computes from the target's range and shape radius is multiplied by a literal `PUSH 0x0` (`004335d0`) and is therefore always zero; the engine omits the same dead term.

### Can this be targeted — `TargetSelect_CanTarget` (`00433174`)

Alive (`obj+0x99`/`+0xa4` both clear), on the other side, and **known** by either sensor route:

- radar-visible (`obj+0x95`) within `DAT_004d1cfc` = **200000**, the last of the scanner's three ranges (`MfdDisplay_Ctor` writes 50000/100000/200000) — read directly, not the current setting;
- or a held contact within `FUN_00426aec` = **30000** on the short scan setting, **60000** otherwise.

### Losing the selection — `FUN_004327ac`

The cockpit's per-frame update, run from `maybe_Sim_RenderFrame` just before `Player_PerFrameCockpitUpdate` copies the selection onto the machine, ends by re-running `TargetSelect_CanTarget` on `view+0x210` and clearing it when that fails. A selection is therefore dropped the frame its target dies **or stops being known** — no longer radar-visible within 200000 and not a held contact within the scan range. The whole widget pass, this check included, is skipped while `view+0x20f` is set, which `CockpitView_ApplyViewState` does for view 4, the external view; a selection survives there until the cockpit returns.

Radar visibility does not blink the selection off: decay clears `obj+0x95` and the sweep repaints it inside the same `Sim_DetectionTick` (`004123ac`), and the check runs outside it.

`TargetSelect_InForwardCone` (`00433250`) is the cone test: bearing less heading **plus** turret twist, within ±8999. The twist sign is the original's and is transcribed rather than corrected — `Mech_PerTickSystemsUpdate` and the sensor sweep fold it the same way.

## The sensor model — `Sim_DetectionTick` (`004123ac`)

Runs once per tick from `Sim_MainTick`, **after** every object update and the input poll, immediately before the per-mech systems pass. Two distinct notions:

- **Radar visibility** (`obj+0x95`) is a property of one object: something with an active scanner painted it. Set by the sweep, cleared wholesale by decay.
- **A contact** (`obj+0xc2 + otherListIndex`) is a property of a *pair*, made by looking and shared sideways to the spotter's own side.

`obj+0x4b` is the object's slot in the single live-object list (`ObjectList_Add`, `00411dd4`) and is what indexes both the contact table and the line-of-sight cache at `obj+0x132`.

### Passes

1. **Timers.** `obj+0x1e2` (LOS cache) and `obj+0x1e5` (contact decay) tick; an expired decay runs `Detection_DecayContacts` (`0041251c`) on the spot, reloading at `10000 + rand(1000)`.
2. **Sweeps.** `Detection_Sweep` (`004128f8`) for each live human-side object, over Cybrid objects only — but it writes *both* objects' tables, so a Cybrid learns without sweeping. The locally-piloted machine (`obj+0xa3`) is held back and swept **last**, so squadmates' contacts have already been shared to it.
3. Clears `obj+0xa2`, a per-tick latch for engagement actions.

### `Detection_Sweep` (`004128f8`) ranges

| Range | Effect |
|---|---|
| < 200000 | Scanner paints, if either object emits and not both are already painted, with LOS |
| < 140000 | A scanner paints an object that is *not* emitting back |
| < 80000 | The looking half runs at all |
| < 50000 | Sets `obj+0x9e` and fires the engagement action |

Looking: bearing plus aim twist against the ±`0x3800` sensor arc (`SimObject_BearingInSensorArc` (`00411acc`), vtable `+0x44`), then LOS. An AI machine's contact goes to `Detection_ShareContact` (`00412704`), which shares it to everything on its side within 100000; the player's machine keeps it to itself. The reciprocal bearing is tested from the other object's arc in the same pass.

Decay (`Detection_DecayContacts`, `0041251c`) drops a contact past **100001** measured **on the ground plane only** (`FastMagnitude2D`, where every other range here is the 3D approximation) or with no LOS, mutually.

### Line of sight — `Detection_LineOfSight` (`00412608`)

A terrain ray between the two objects' aim nodes (`+0x1c` of the vtable `+0x24` record — see [Aim point](#aim-point--vtable-0x24) — or 500 when that slot returns nothing), cached per pair on the observer. **The cache gate tests the *other* object's countdown and reloads the observer's** — verified at `00412617`/`0041263f` (`CMP word ptr [ESI+0x1e3]` against `MOV word ptr [EBX+0x1e3]`), so it is not a decompiler slip. Since only human-side objects sweep, Cybrid timers stay at zero and pairs are re-walked whenever asked.

## Radar mode

`mech+0x96` is PASSIVE/ACTIVE, toggled by `Mech_ToggleRadarMode` (`0041b468`) — the manual's [R] and the F4 scanner's PASS/ACTIVE buttons, gated on `obj+0xa3` so only the player's machine flips. **A HERC powers up passive**: nothing writes the field at construction and that toggle is its only caller. `Base_Construct` latches it on for structure types 5, 6, `0x1d`, `0x1e` — the radar masts.

This matters for what the player can target. Passive, targeting depends on visual contacts and reaches about 350 m; active, it reaches as far as terrain gives line of sight — measured at 831 m against the stock mission's nearest hostile. A distant enemy is usually targetable because *its own* radar is on: `Mech_AiCombatReassess` switches an AI machine to ACTIVE the moment it enters a fight and a squadmate of the player's back to PASSIVE (see [`ai-targeting.md`](ai-targeting.md#the-combat-reassess--mech_aicombatreassess-0041cf18)), ported as `MechObject.CombatReassess`.

Radar mode is also what an **AI machine's ECM pod** follows, so a Cybrid that lights its radar up on entering a fight starts jamming at the same moment — see [`equipment-pods.md`](equipment-pods.md).

## Object classification

`obj+0x1a8`, written by each constructor: `Mech_Constructor` 0, `Flyer_Constructor` 2, `Base_Construct` 1 for every structure except types `0x2d`-`0x34`/`0x37`-`0x3d`, which get 3. All three write `0xffff` first. Six type indices (`0x0a`, `0x35`, `0x36`, `0x3e`-`0x40`) match no case and leave the original's object pointer uninitialised; the port takes them as ordinary structures.

Only classes 0 and 2 are candidates for the nearest-target key.

`script.dat` block 11's `0x6e` (`ScriptEntity164Export.TriStateFlag`) is the side, copied to the group record's `+0x12` by `DBSim_BuildGroupRecord`; 0 is human, 1 Cybrid. The stock mission has 2 human groups and 7 Cybrid.

## Aim point — vtable `+0x24`

`Rocket_HomingSteer` (`0040a254`), `Bullet_HomingSteer` (`0040aff0`) and the HUD target indicator (`Player_ResolveTargetAimPoint`, `0041b728`) share one branch verbatim: call the target's vtable `+0x24`, and if it returns a record, transform that record's `+0x14` by the target's own rotation; otherwise use the raw origin.

**The record is a shape node transform, not a bounding box.** It is `shapeInstance+0x16 + index*0x20`, the same 0x20-byte per-node array `Mech_ComponentGeometryTest_Candidate` indexes — 9 matrix shorts then a 3-int translation at `+0x14`..`+0x1f`. So `+0x14` is the node's model-space position and `+0x1c` is its Z, which is what the line-of-sight test adds to the object's own.

**Which node is per class:**

- **Mech** — `Mech_GetAimNodeTransform` (`00417b98`) pushes the type record's `+0x0c` (`.DAT` file offset 10, `HercSimDat.CameraBoneId`) as the part id, so a HERC is aimed at **through its cockpit node**, the same one the pilot's eye rides. It walks and leans with the machine. Retail rises are 7.2 m (HEADHUNT) to 10.4 m (ACHILLES) above the model origin, which sits on the ground.
- **Structure** — all five structure vtables install `Base_GetAimNodeTransform` (`00403548`), which fills a static record with a fixed matrix and the translation `(0, 0, BASES.DAT +0x2c)`, the type's aim-point height (1000 to 2000). A structure is aimed at that far up its side, the same point its own `+0x30` (`Base_GetAimPoint`, `0040351c`) gives, and sights from that height. A turret standing just behind a rise stays in line of sight over the rise's edge because of it; at 500 it drops out.
- **Flyer** — installs `SimObject_GetAimNodeTransform_None` (`00411a9c`), which is `return 0`, so a flyer is aimed at its raw origin and sights from the literal 500.

`SimObject.AimPoint` / `SimObject.SightHeight`, overridden on `MechObject` and `BaseObject`.

## Component targeting — the Targeting Pod

A **Targeting Pod** (catalog id 29, `mech+0x30b` — [`equipment-pods.md`](equipment-pods.md)) lets the player aim at one part of the selected machine instead of at its aim node. The manual: select with `Enter`, then "use [Tab] to cycle through that target's components", and Automatic Turret Tracking follows the part rather than centre mass — which is how a pilot cripples a Cybrid's legs and salvages the rest of it.

Three functions drive it, each a plain direct call rather than a slot:

| | Called by | |
|---|---|---|
| `TargetingPod_ResetComponentLock` (`0040e484`) | `Player_PerFrameCockpitUpdate+0x26b` | The selection changed: restart the rotation, or switch component targeting off |
| `TargetingPod_CycleComponent` (`0040e4ac`) | `CockpitWidgets_HandleCommand+0x321`, and the resolver below | `Tab` — step to the next component the target still has |
| `TargetingPod_ResolveAimPoint` (`0040e4dc`) | `Player_ResolveTargetAimPoint+0x51` | Where to aim, and which component that is |

`Tab` is scancode `0x0f`, dispatched like every other cockpit command — see [`../formats/cockpit-input.md`](../formats/cockpit-input.md#keyboard-commands-are-scancodes). It only reaches the pod while the heads-down display is down: in view mode 1 the same case hands `0x0f` to the HDD's own command slot instead, which is the manual's `Zoom Map In/Out`.

### The lock is two fields, and they are not interchangeable

| Field | |
|---|---|
| `+0x7d` | `short` — the **cursor**, a position in the seven-entry rotation `TargetingPodComponentRotation` (`0049a060`). `-1` is no component lock |
| `+0x86` | `int` — the **component id** that cursor last resolved to. This, and never the cursor, is what the target is asked about |
| `+0x81` | `LongCountdownTimer` — the decay a damaged pod runs, counter at `+0x82` |
| `+0x7f` | `short` — the pod's own component damage, 0–256, cached by `TargetingPod_ConditionChanged` (`0040ef6c`) |

**A reset moves the cursor and not the id.** `TargetingPod_ResetComponentLock` writes `+0x7d` alone, so restarting the rotation on a new selection leaves `+0x86` holding the component the *previous* target was locked to, and the resolver goes on aiming at it — the machine's own part of that name, if it still has one. Selecting a new machine therefore does not put the aim back on centre mass; only pressing `Tab` moves the id. On the first selection of a mission the id is the zero the mount's block was allocated with, which names component 0.

Otherwise the two move together and only together, because the vtable slot that advances the lock writes both: it returns the new cursor and writes the component id through an out-parameter, and answers `-1` in both when the rotation has nothing left. `+0x86` is the pod's last field — `TargetingPod_Ctor` asks for `0x8a` bytes against `0x7d`–`0x85` for the other four.

### The rotation belongs to the target — vtable `+0x80` and `+0x84`

Two `SimObjectVtable` slots exist for this and nothing else; the pod is the only caller of either.

- **`+0x80` `(this, cursor, int *outComponentId)` — advance.** `Mech_NextTargetableComponent` (`00415558`) steps the cursor over `TargetingPodComponentRotation` = `{0, 4, 5, 7, 8, 9, 10}`, skipping any slot the machine has lost (its occupancy array at `+0x20e`) and wrapping at 7. `Base_NextTargetableComponent` (`00403624`) is the structure's, over its type's whole component list. `SimObject_NextTargetableComponent_None` (`00411b1c`) answers `-1`, so a flyer has no parts to single out.

**The walk steps before it looks, and the slot it started on is never tested.** A machine whose only remaining rotation slot is the one the cursor already sits on answers `-1` to both, and a cursor of `-1` is clamped to position 0 before the first step — so the first `Tab` after no lock gives rotation entry 1, component 4, and never component 0.
- **`+0x84` `(this, componentId)` — still there?** `Mech_ComponentPresent` (`00415540`) reads that one occupancy entry. The resolver asks before using a lock, so a component shot off between presses moves the lock on rather than aiming at nothing.

Seven of a machine's twenty-nine slots, straddling both the chassis band (0, 4, 5) and the systems band (7–10) of [`ai-targeting.md`](ai-targeting.md#which-component-the-shot-is-aimed-at--mech_aiselectaimcomponent-0041ce08)'s table — the manual's "target areas".

Only a HERC has them: `TargetingPod_ResetComponentLock` writes `-1` for every target whose `TargetClass` is not 0, which is the same fence that keeps a structure's and a flyer's `+0x58` out of the aim-point path ([`damage-system.md`](damage-system.md#where-a-component-stands--the-0x58-slot)). A structure's `+0x80` and `+0x84` are therefore installed but never reached.

### A damaged pod degrades in four steps

Every reader of the pod is a threshold on the cached reading at `+0x7f`, and the four do not agree on a cutoff:

| `+0x7f` | What stops | Where |
|---|---|---|
| `≥ 0x33` | The odds that the target's ECM spoofs the player's missile lock go back up: `Mech_PerTickSystemsUpdate`'s re-roll weight returns to `0x14` from the `5` a healthy pod buys — [`missile-lock.md`](missile-lock.md#ecm) | `Mech_PerTickSystemsUpdate` |
| `> 0x68` | The lock stops holding: the resolver runs the countdown at `+0x81` on every frame it answers, and each expiry reloads 5000 — about 2.4 s in that counter's unit — and drops the cursor back to `-1`. So past 40% damage the player keeps the component for a couple of seconds at a time and has to press `Tab` again. The counter is never initialised, so the first frame a damaged pod resolves on expires at once | `TargetingPod_ResolveAimPoint` |
| `≥ 0x9b` | Component targeting stops entirely — the resolver takes the target's vtable `+0x24` aim node and reports no component, exactly as a machine with no pod does | `TargetingPod_ResolveAimPoint` |
| `> 0xa9` | An **AI** machine carrying one stops preferring the systems band when it picks a component to shoot at | [`ai-targeting.md`](ai-targeting.md#which-component-the-shot-is-aimed-at--mech_aiselectaimcomponent-0041ce08) |

**The Targeting Pod is the only pod that caches its damage.** Its vtable `+0x68` override is what fills `+0x7f`, from the reading `Mech_ComponentDamageWrite` hands every mount after a write; the Shield and Energy pods instead read theirs live through `Component_ReadDamagePercent` each time their bonus is recomputed. Same quantity, two mechanisms — a port that models one will not find the other by grepping for the offset.

**A pristine pod's cache reads zero**, so the two mechanisms agree at spawn. Neither `TargetingPod_Ctor` nor `Mech_ConfigureLoadout` writes `+0x7f` — the constructor writes the gauge handle and the catalog id and stops, and the loadout pass ends at `MechLoadout_FileEquipmentPods`, `Mech_ComputeShieldCapacity`, `Shield_RefillToBalance` and `Mech_ComputeReactorRate` without touching any mount's condition slot. What settles it is the allocation: `MechLoadout_ConstructWeaponMounts` (`0040fff8`) opens by pushing a 200000-byte arena that `Arena_Push` (`00474ab0`) has just `calloc`'d, and bump-allocates every mount out of it through `Arena_Alloc` (`0047a1bc`) — which does no zeroing of its own but never needs to, and whose fallback for an absent or full arena, `Mem_AllocZeroedTagged`, zeroes anyway. So a pod block is zero on every path, and so are the lock's other three fields: cursor 0, component 0, expired decay.

Because `Mech_ComponentDamageWrite` then hands **every** mount its component's reading on every write anywhere on the machine, the cache tracks the live figure from there on. The two are still not interchangeable — the cache is only as current as the last write, and a thing that changed a component reading without going through that write would part them — but no such path exists in the simulation.

`TargetingPod_ResolveAimPoint`'s fourth parameter is dead. `Player_ResolveTargetAimPoint` passes the target's occupancy-array pointer `mech+0x20e`, and `[EBP+0x14]` is untouched in the whole body — while the two out-parameters either side of it, `[EBP+0x18]` and `[EBP+0x1c]`, are read. The pod reaches the same array through the target's own slots instead.

## Engine port

`SimObject` carries `ListIndex`, `Side`, `TargetClass`, `Neutralised`, `RadarVisible`, `ScannerActive`, `JammerActive`, `AimOffset`/`AimPoint`/`SightHeight`, `TargetedBy` and the two per-object tables. `MissionScene.Targeting` holds the selection; the host drives it from [Enter]/[']/[;] and pushes it to the machine once a frame.

The pod is `Sim.TargetingPodLock`, hung off `WeaponMount.ComponentLock` for the one mount whose catalog id is 29 and null on every other — the engine has a single mount class where the original has a subclass per kind, and the four fields belong to the mount that has them. `MechObject` supplies the callers: the reset from its `OnTargetChanged`, `CycleTargetComponent` for `[Tab]`, and `ResolveTargetAimPoint` for `Player_ResolveTargetAimPoint`, whose result the host resolves **once a frame** and hands to both consumers — asking twice would run the decay countdown twice. The target's two slots are `SimObject.NextTargetableComponent` / `ComponentPresent`, overridden on `MechObject`; `Base_NextTargetableComponent` has no engine counterpart, since it sits unreachable behind the `TargetClass` fence in retail too.

All three entry points also set the gunsight's "indicator armed" byte (`TargetSelection.IndicatorArmed`, state-block offset 36) on a successful press, which the target box's paint requires — see [`../formats/hud-target-indicator.md`](../formats/hud-target-indicator.md). Nothing ever clears it.

Deviations:

- **The observer camera is excluded** from the sensor model by target class. DBSIM's live-object list only ever holds the three combat classes; `SimWorld`'s also holds the camera, which would otherwise spot for the player's side.
- `TargetSelection.DropIfInvalid` is [the cockpit update's drop](#losing-the-selection--fun_004327ac), plus a removed-object test of the engine's own that applies in every view — DBSIM's object list has no removed-but-listed state.
- `obj+0x9e` and the engagement action it fires at 50000 units are `SimObject.Engaged` and `SimObject.EngagementAction` — [`mission-deployment.md`](mission-deployment.md).

## Rejected readings

| Obvious reading | Actually |
|---|---|
| The player's selection is never dropped: the death path `Mech_AiSelectBehaviour` (`0041eb34`) and `Ai_ShouldAbandonTarget` (`0041c4a8`, see [`ai-targeting.md`](ai-targeting.md#abandoning-a-target--ai_shouldabandontarget-0041c4a8)) both run only for AI machines, and a text search for writes to `+ 0x210)` finds only the three selection commands | `FUN_004327ac` clears it, written by the decompiler as `param_1[0x84] = 0` — `0x84 * 4 = 0x210` — so an offset search misses it. See [Losing the selection](#losing-the-selection--fun_004327ac) |
| Structures sight from the literal 500 because they install the `return 0` stub at vtable `+0x24` | That stub (`00411a9c`) is the flyer's and the base class's; all five structure vtables install `Base_GetAimNodeTransform` (`00403548`). See [Aim point](#aim-point--vtable-0x24) |

## Open

- **Unported:** the "enemy detected" callout (vtable `+0x48`, `Mech_AiEnemySighted` — see [`ai-targeting.md`](ai-targeting.md#radio-callouts)).
- **Unported:** the second viewing object `DAT_004d2708` selects when watching another machine.
