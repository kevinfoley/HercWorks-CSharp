# Target selection and the sensor model

Solved and shipped 2026-08-29. `Sim.TargetSelection`, `Sim.Detection`, `MechObject.Target`.

## Where the selection lives

`mech+0x1a4` is the selected target every homing weapon and most of the AI reads. **For the player's
machine nothing in the simulation writes it.** The selection is made in the cockpit widget tree at
`CockpitViewInstance+0x210` and copied onto the machine once a frame by
`Player_PerFrameCockpitUpdate` (`0041b130`). AI machines get theirs from a separate family
(`FUN_0041c0f4` and the state functions around `0041c418`-`0041e224`), none of it ported.

Every writer of `mech+0x1a4` also maintains `target+0x1a2`, a count of how many machines hold that
object, and raises `mech+0x9d` ("target changed"), which suppresses lock for one tick.

| Key | Scancode | Function | What it does |
|---|---|---|---|
| `Enter` | `0x1c` | `FUN_0043349c` | Cycle — rebuild the shortlist, take its head, or step |
| `'` | `0x28` | `FUN_004333c8` | Nearest HERC or flyer, ignoring facing |
| `;` | `0x27` | `FUN_004332dc(view, 0)` | Clear. Undocumented in the manual |

`FUN_004332dc(view, obj)` also serves the F4 scanner's TARGET button and a gunsight click. It
*walks* `+0x210` through the object list from a stored cursor until it lands on the object asked for,
so a request for something unselectable ends with the selection back where it started.

### Cycle's shortlist — `FUN_0043349c`

Everything selectable and inside the ±8999 cone is filed into one of four buckets by bearing error
(`|err| >> 10`, clamped to 3), sorted by range within its bucket, keeping four. Flattening the
buckets in order gives the shortlist: **nearest the crosshair wins, range only breaks ties inside a
band**. A repeat press whose rebuilt head is unchanged steps to the shortlist entry after the
current selection.

The angular-size correction the function computes from the target's range and shape radius is
multiplied by a literal `PUSH 0x0` (`004335d0`) and is therefore always zero. Not ported, by
omission.

### Can this be targeted — `FUN_00433174`

Alive (`obj+0x99`/`+0xa4` both clear), on the other side, and **known** by either sensor route:

- radar-visible (`obj+0x95`) within `DAT_004d1cfc` = **200000**, the last of the scanner's three
  ranges (`MfdDisplay_Ctor` writes 50000/100000/200000) — read directly, not the current setting;
- or a held contact within `FUN_00426aec` = **30000** on the short scan setting, **60000** otherwise.

`FUN_00433250` is the cone test: bearing less heading **plus** turret twist, within ±8999. The twist
sign is the original's and is transcribed rather than corrected — `Mech_PerTickSystemsUpdate` and the
sensor sweep fold it the same way.

## The sensor model — `FUN_004123ac`

Runs once per tick from `Sim_MainTick`, **after** every object update and the input poll, immediately
before the per-mech systems pass. Two distinct notions:

- **Radar visibility** (`obj+0x95`) is a property of one object: something with an active scanner
  painted it. Set by the sweep, cleared wholesale by decay.
- **A contact** (`obj+0xc2 + otherListIndex`) is a property of a *pair*, made by looking and shared
  sideways to the spotter's own side.

`obj+0x4b` is the object's slot in the single live-object list (`ObjectList_Add`, `FUN_00411dd4`) and
is what indexes both the contact table and the line-of-sight cache at `obj+0x132`.

### Passes

1. **Timers.** `obj+0x1e2` (LOS cache) and `obj+0x1e5` (contact decay) tick; an expired decay runs
   `FUN_0041251c` on the spot, reloading at `10000 + rand(1000)`.
2. **Sweeps.** `FUN_004128f8` for each live human-side object, over Cybrid objects only — but it
   writes *both* objects' tables, so a Cybrid learns without sweeping. The locally-piloted machine
   (`obj+0xa3`) is held back and swept **last**, so squadmates' contacts have already been shared to
   it.
3. Clears `obj+0xa2`, a per-tick latch for engagement actions.

### `FUN_004128f8` ranges

| Range | Effect |
|---|---|
| < 200000 | Scanner paints, if either object emits and not both are already painted, with LOS |
| < 140000 | A scanner paints an object that is *not* emitting back |
| < 80000 | The looking half runs at all |
| < 50000 | Sets `obj+0x9e` and fires the engagement action |

Looking: bearing plus aim twist against the ±`0x3800` sensor arc (`FUN_00411acc`, vtable `+0x44`),
then LOS. An AI machine's contact goes to `FUN_00412704`, which shares it to everything on its side
within 100000; the player's machine keeps it to itself. The reciprocal bearing is tested from the
other object's arc in the same pass.

Decay (`FUN_0041251c`) drops a contact past **100001** measured **on the ground plane only**
(`FastMagnitude2D`, where every other range here is the 3D approximation) or with no LOS, mutually.

### Line of sight — `FUN_00412608`

A terrain ray between the two objects' shape-box centres (`+0x1c` of the vtable `+0x24` box, 500 with
no shape), cached per pair on the observer. **The cache gate tests the *other* object's countdown and
reloads the observer's** — verified at `00412617`/`0041263f` (`CMP word ptr [ESI+0x1e3]` against
`MOV word ptr [EBX+0x1e3]`), so it is not a decompiler slip. Since only human-side objects sweep,
Cybrid timers stay at zero and pairs are re-walked whenever asked.

## Radar mode

`mech+0x96` is PASSIVE/ACTIVE, toggled by `FUN_0041b468` — the manual's [R] and the F4 scanner's
PASS/ACTIVE buttons, gated on `obj+0xa3` so only the player's machine flips. **A HERC powers up
passive**: nothing writes the field at construction and that toggle is its only caller.
`Base_Construct` latches it on for structure types 5, 6, `0x1d`, `0x1e` — the radar masts.

This matters for what the player can target. Passive, targeting depends on visual contacts and
reaches about 350 m; active, it reaches as far as terrain gives line of sight — measured at 831 m
against the stock mission's nearest hostile. In the original a distant enemy is usually targetable
because *its own* radar is on, set by the unported AI state functions, so the player-side toggle is
what substitutes for that here.

## Object classification

`obj+0x1a8`, written by each constructor: `Mech_Constructor` 0, `Flyer_Constructor` 2,
`Base_Construct` 1 for every structure except types `0x2d`-`0x34`/`0x37`-`0x3d`, which get 3. All
three write `0xffff` first. Six type indices (`0x0a`, `0x35`, `0x36`, `0x3e`-`0x40`) match no case
and leave the original's object pointer uninitialised; the port takes them as ordinary structures.

Only classes 0 and 2 are candidates for the nearest-target key.

`script.dat` block 11's `0x6e` (`ScriptEntity164Export.TriStateFlag`) is the side, copied to the
group record's `+0x12` by `DBSim_BuildGroupRecord`; 0 is human, 1 Cybrid. The stock mission has 2
human groups and 7 Cybrid.

## Aim point — vtable `+0x24`

`Rocket_HomingSteer` (`0040a254`), `Bullet_HomingSteer` (`0040aff0`) and the HUD target indicator
(`FUN_0041b728`) share one branch verbatim: call the target's vtable `+0x24`, and if it returns a
record, transform that record's `+0x14` by the target's own rotation; otherwise use the raw origin.

**The record is a shape node transform, not a bounding box.** It is
`shapeInstance+0x16 + index*0x20`, the same 0x20-byte per-node array
`Mech_ComponentGeometryTest_Candidate` indexes — 9 matrix shorts then a 3-int translation at
`+0x14`..`+0x1f`. So `+0x14` is the node's model-space position and `+0x1c` is its Z, which is what
the line-of-sight test adds to the object's own.

**Which node is per class:**

- **Mech** — `FUN_00417b98` pushes the type record's `+0x0c` (`.DAT` file offset 10,
  `HercSimDat.CameraBoneId`) as the part id, so a HERC is aimed at **through its cockpit node**, the
  same one the pilot's eye rides. It walks and leans with the machine. Retail rises are 7.2 m
  (HEADHUNT) to 10.4 m (ACHILLES) above the model origin, which sits on the ground.
- **Flyer and structure** — both install `FUN_00411a9c`, which is `return 0`, so both aim at the raw
  origin and both sight from the literal 500.

`SimObject.AimPoint` / `SimObject.SightHeight`, overridden only on `MechObject`.

## Engine port

`SimObject` carries `ListIndex`, `Side`, `TargetClass`, `Neutralised`, `RadarVisible`,
`ScannerActive`, `JammerActive`, `AimOffset`/`AimPoint`/`SightHeight`, `TargetedBy` and the two
per-object tables. `MissionScene.Targeting` holds the selection; the host drives it from
[Enter]/[']/[;] and pushes it to the machine once a frame.

Deviations:

- **The observer camera is excluded** from the sensor model by target class. DBSIM's live-object list
  only ever holds the three combat classes; `SimWorld`'s also holds the camera, which would otherwise
  spot for the player's side.
- **`TargetSelection.DropIfInvalid`** is not the original's. Its owners there are the AI
  target-abandon check (`FUN_0041c4a8`) and the death path (`FUN_0041eb34`), neither ported, so
  without it a destroyed target stays locked.
- Not ported: the "enemy detected" callout (vtable `+0x48`, `FUN_00412800`), `obj+0x9e` and its
  engagement action (no mission actions exist), the second viewing object `DAT_004d2708` selects when
  watching another machine, and the HUD target box — `Gunsight_SetValues` pushes the target into a
  gunsight child that has never been traced, though `CockpitView+0x26c`..`+0x27e` is known to hold the
  target and its world aim point.
