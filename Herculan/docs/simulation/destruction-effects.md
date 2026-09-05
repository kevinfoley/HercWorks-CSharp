# Destruction effects — wreckage, fire, and a structure coming down

What a destroyed thing puts on screen: the shapes it throws (`debris.cpp`), the flames left burning
on it (`fire.cpp`), and the multi-stage collapse a building runs before it becomes a wreck
(`base.cpp`). All three are driven by the damage model but are separate from it — the arithmetic of
losing a part is in [`damage-system.md`](damage-system.md) and
[`hit-detection.md`](hit-detection.md).

Impact effects — the puff where a shot lands — are a fourth, different subsystem:
[`impact-effects.md`](impact-effects.md). Everything here spawns them, and nothing here is one.

## Debris

### The two-database index space

Every spawn site names a debris group by a single index, and `Debris_Resolve` (`004089bc`) splits it
against the **default** database's group count:

```c
if (index >= g_DebrisDefault.groupCount) { index -= g_DebrisDefault.groupCount; return g_DebrisAlternate; }
return &g_DebrisDefault;
```

`g_DebrisDefault` (`004a96d4`) is `DEF_DEB`, loaded once at startup. `g_DebrisAlternate`
(`004a96d0`) is a *currently installed* pointer each spawn site writes immediately before it spawns:

| Site | Installs |
|---|---|
| `Mech_ComponentDamageWrite` (`00417de4`) | the machine's own chassis table, `typeRecord+0x212` |
| `Base_ThrowDebris` (`0040379c`) | `BASE_DEB` (`004a9644`) |
| `Debris_TickUpdate` (`00408bd8`), for a bursting piece | whatever table that piece was thrown out of, captured at `obj+0x5d` |

So a low index always means the same thing — group 2 is `DEF_DEB`'s third group whoever throws it —
and a high index means whatever was installed a heartbeat earlier. Nothing bounds the alternate
half; a high index with nothing installed dereferences null in the original and has never been
observed to happen.

Retail has 6 groups in `DEF_DEB`, so indices 0-5 are shared and 6 upward reach the installed table.

### `dat\<name>_DEB.DAT`

A database is a `.DAT` and a `.DTS` under one base name, loaded together by `Debris_LoadDatabase`
(`0040874c`) into a 12-byte struct: `{ group* groups; int16 groupCount; TSShape** shapes; int16
shapeCount }`. Its third argument, when non-zero, is written into every loaded shape's bound-bank
pointer (`shape+0x26`), which is how a chassis' wreckage ends up painted in the chassis' colours.

```
int16 groupCount
groupCount × {
    int16 throwCount
    int16 pieceCount
    pieceCount × 14-byte piece
}
```

| Offset | Field | Meaning |
|---|---|---|
| `+0x00` | `ShapeIndex` | root of the matching `.DTS` |
| `+0x02` | `Weight` | share of the group's weighted draw; retail states 10 or 20 |
| `+0x04` | `ChildGroup` | group this piece bursts into where it ends, `-1` for none |
| `+0x06` | `DestroyEffect` | `EXPLOS.DAT` type that goes off there, `-1` for none |
| `+0x08` | `OrientationYaw` | **degrees**, `-1` = leave the spawn frame alone |
| `+0x0a` | `ThrowYaw` | **degrees** relative to the above, `-1` = throw on a random bearing |
| `+0x0c` | `Mass` | divides the throw speed; retail 800-4000 |

`Debris_LoadPieceList` (`004083f8`) multiplies both angles by 182 as it reads them
(`65536 / 360 ≈ 182.04`, degrees to BAM) unless the raw value is the `-1` sentinel, and accumulates
the group's total weight, which the file does not store.

Walking this shape consumes all 24 retail files exactly, with nothing left over in any of them. The
three kinds are `DEF_DEB`, `BASE_DEB`, and one per HERC chassis named by that chassis'
`HercSimDat.DebrisFile` (record offset 204, a 12-byte NUL-padded string).

### Throwing a group

`Debris_ThrowGroup` (`004086bc`) reads a group two ways:

- **`throwCount == 0`** — throw every piece it holds, once each.
- **otherwise** — throw exactly `throwCount` pieces drawn from it at random, weighted: a draw under
  the group's total, then each piece's weight subtracted off it in file order until one covers what
  is left. The same piece can therefore be thrown twice and another skipped.

`Debris_ThrowGroupAt` (`00408530`) is the same call from a *point* rather than a frame — it builds
an identity rotation with the point in the translation. Every site but a machine's own component
destruction uses it.

`Debris_Throw` (`00408588`) places one piece. A piece stating either angle has its own yaw composed
onto the spawn frame and the composed attitude read back out as Euler angles; only a piece stating
`OrientationYaw` keeps that attitude, so one stating only `ThrowYaw` does the matrix work and
discards it. **The position is the spawn frame's own either way** — the composed transform is a
temporary nothing is placed at.

`Debris_Launch` (`004089e4`) does the launch:

```c
speed      = (g_DebrisSpeedScale << 10) / mass
horizontal = Q14Multiply(speed, Cos(pitch))
vx = Q14Multiply(horizontal, Cos(yaw + 0x4000))
vy = Q14Multiply(horizontal, Cos(yaw))
vz = Q14Multiply(speed,      Cos(pitch - 0x4000))
spin = Q10Multiply(-1700, rand & 0x3ff) - 800      // always negative: 800-2500 BAM/s
```

The pitch is drawn between two globals and the speed scaled by a third. All three are rewritten
around a burst and restored after it, which is the whole of what makes second-generation debris
scatter closer:

| | `004a96e8` pitch min | `004a96ea` pitch max | `004a96ec` speed scale |
|---|---|---|---|
| First throw | 6000 (≈33°) | 16000 (≈88°) | 420 |
| A piece bursting | 3000 | 8000 | 320 |

`Debris_ThrowAtBearing` (`00408b74`) draws the pitch and forwards; `Debris_ThrowRandomBearing`
(`00408bb4`) draws the bearing too. A carrier velocity at `004a96e4` is added to the result, and
only `Flyer_ComponentDamageWrite` (`00421bb4`) ever sets it — a shot-down aircraft's wreckage keeps
the aircraft's motion, and nothing else's does.

### The piece — `Debris_TickUpdate` (`00408bd8`)

Object fields, 0x61 bytes from a 150-entry pool at `004a96c6`:

| Offset | Meaning |
|---|---|
| `+0x0c` | euler triple; only X moves |
| `+0x26` | position, three `int32` |
| `+0x41` | `signed char` child group, `-1` for none — **the gate on the whole death branch** |
| `+0x4a` | velocity, three `int16` |
| `+0x55` | burst countdown, `RandomBelow(30000) + 2000` |
| `+0x59` | `int16` `EXPLOS.DAT` type on death, `-1` for none |
| `+0x5b` | spin rate |
| `+0x5d` | the database this piece was thrown out of |

Per tick, in order:

1. Spin integrated into euler X; gravity into vertical speed.
2. Horizontal drag on X and Y only, `Q10Multiply(30, v)` integrated. Nothing slows the fall.
3. The move, by the **average of the speed before and after** this tick's changes. Verified against
   the raw disassembly at `00408ca1`: it is `ADD dword [pos], movsx word [avg]` — the average is
   taken and shifted in 16 bits (`SAR word`), and it is added **un-integrated**, so a debris
   velocity is per-tick where a flyer's is per-second.
4. Ground: below `terrainHeight + Q10Multiply(500, shapeRadius)` the piece is snapped up to it and
   bounces at `-Q10Multiply(450, vz)`, its countdown cleared. A rebound under `0x2d` has stopped.
5. For a piece with a child group: the countdown is ticked only while it is still flying, so ground
   contact bursts it on that tick either way. The burst spawns its effect, re-installs `+0x5d`, and
   throws the child group at the tightened window above.

Gravity is `-0x20` at every detail level but 4, where it is `-10` and wreckage hangs noticeably
longer. A piece with no child group has no death branch at all and simply lives until it settles.

### Spawn sites

| Site | Group | When |
|---|---|---|
| `Component_DestroyAndCascade` (`0040d434`) | the `.DMG` record's `+0x02`, or 2 when it states `-1` | a machine's component comes apart; thrown from the component's own composed frame |
| `Mech_ApplyDirectFireDamage` (`004188c8`) | 2 | a hit moved a component into a new damage band without finishing it |
| `WeaponMount_Destroy` (`0040f57c`) | see below | a visibly-mounted hardpoint is lost |
| `Base_ThrowDebris` (`0040379c`) | the component record's `+8`, plus 10 and 12 when that is above 5 | a structure's part collapses |
| `Base_DirectFireHitTest` (`00405038`) | 1, on a `rand & 0xfff < 0x401` roll | any hit on a structure |
| `Flyer_DirectFireHitTest` (`00421c8c`) | 1, or 3 for the hit that brings it down | any hit on an aircraft |
| `Razor_MovementTick` (`004198f4`) | 3 | a cockpit or fuselage contact that destroys its component |

`WeaponMount_Destroy` is the one site that throws a piece it built itself rather than anything a
table names: the gun's own model, the same shape index out of `dts\MECHWPN2.DTS` rather than the
`MECHWPNS.DTS` it was drawn from, off the mount point, on a `Math_EulerToward` bearing away from the
machine's aim point, at the hardpoint's `.GL +0x18` pitch and a flat mass of `0x4b0`. It keeps the
muzzle frame's attitude. **The third argument picks the pair the piece is built with**: the certain
path through the mount's `+0x68` notification passes 0 and gets a piece that bursts (child group 2,
effect `0x14`); the destruction roll in `Mech_ApplyDirectFireDamage` passes 1 and gets a plain piece
that just falls. See [`weapon-mounts.md`](weapon-mounts.md#losing-a-mount).

## Fire

`fire.cpp`'s burning-object effect — **not** the muzzle flash, which is the weapon model's own
flipbook ([`weapon-mounts.md`](weapon-mounts.md#the-muzzle-flash)).

`FireEffect_LoadResources` (`0046b0a4`) reads `dts\FIRE.DTS` (`fire2.dts` under the low-memory art
setting), allocates a **ten**-entry pool at `006b4fb4`, loads banks `fire0`/`fire1`, and reads
`dat\FIRE.DAT` — a 4-byte count header and then one byte per shape naming its bank. Retail: four
shapes, banks `[0, 0, 0, 1]`, of 24, 24, 24 and 27 billboard frames.

`FireEffect_AcquireSlot` (`0046b32c`) takes a free entry, or when none is free returns the
live one with the **fewest loops left** (`+0x59`). A full pool never refuses a new fire; the one
nearest going out pays for it.

`FireEffect_Ctor` (`0046b388`) fields:

| Offset | Meaning |
|---|---|
| `+0x41` | set when no attach point was given: follow the owner's origin |
| `+0x4a` | owner |
| `+0x4e` | attach cluster id; `< 0` uses the raw local point, `0xffff` is the structures' "no cluster" |
| `+0x50` | attach point, three `int16` |
| `+0x57` | frame timer, reloaded to `0x40` |
| `+0x59` | loops remaining: 30, or 5 at detail level 4 |

At detail level 4 only shapes 1 and 3 are built at all; the others go straight back on the free list.
The first live instance starts sound `0x33` and `FireEffect_Dtor` stops it on the last — one loop for
every fire in the mission at once, kept positioned on whichever is nearest the camera.

`FireEffect_TickUpdate` is the tick: count the frame timer down, step the shape's cell animation, decrement
the loop count each time the frame wraps to zero, then re-place the effect from wherever the owner
has carried it to. It ends when the loop count reaches zero. So a fire **loops** where an impact
effect plays once.

`FireEffect_ReleaseForOwner` (`0046b528`) returns every entry whose owner matches — called from
exactly one place, the whole-object destruction branch below.

### Who catches fire

Two sites, and they light different shapes:

- **`Component_DestroyAndCascade`**, for a component whose `.DMG` `+0x03` byte is not `-1` (the same
  byte that drives its shape sequence — see
  [`../formats/mech-shape-drawing.md`](../formats/mech-shape-drawing.md)). `DestructionFlags` bit 1
  releases every fire already on the machine and lights **shape 0** in their place — the machine
  going up as a whole; bit 3 adds **shape 2** to whatever is already alight.
- **`Base_DeathSequenceTick`**, at the last stage of a collapsing part — see below.

## A structure coming down

A structure's part is not deleted when its health runs out. `Base_ApplyDamage` (`00404d70`) gives it
a **stage countdown**, and `Base_DeathSequenceTick` (`00403914`) steps every falling part through it
one stage at a time. That is why a building takes several seconds to collapse and its parts collapse
in the order they were shot.

The sequence is picked by the component record's `+4`, indexing five parallel four-entry tables in
the executable's statics:

| Sequence | `004973fc` collapse explosion | `00497404` hold after it | `0049740c` explode at object origin | `00497414` smoke explosion | `0049741c` stage count |
|---|---|---|---|---|---|
| 0 | 16 | 2000 | yes | 15 | 8 |
| 1 | 21 | 0 | no | 15 | 6 |
| 2 | 15 | 0 | no | 15 | 6 |
| 3 | 15 | 0 | no | 15 | 2 |

`Base_ApplyDamage` sets `state[+5] = stageCount` and `state[+3] = 300`; each expiry of that timer
takes one off the stage, and:

- **Stage > 1** — one smoke explosion at a random point inside the part's own spread box (component
  record `+0x16`, half-extents) around its position, and the timer reloaded to 300. **Stage exactly
  4** also runs `Base_FinishDependents`.
- **Stage 1** — the collapse. `Base_CollapseExplosion` (`004036c0`) sets off the sequence's
  explosion, at the structure's origin or at the part's emission point on `0049740c`, and reloads
  the timer with the sequence's hold. Then `Base_ThrowDebris`, then the shape change: a type that
  leaves a wreck and has just lost its last part switches its model instance's shape pointer to
  `hulkShapes[typeRec+0x04]` — the **hulk swap** — and anything else steps the sequence the component
  record's `+2` names to cell 1 (`shapeInstance[+8][rec+2] = 1`, the structure-scale counterpart of a
  machine's `= 2`) and finishes its dependents. A structure's parts are two-cell `TSCellAnimPart`s
  whose **second cell is that part's own rubble**, so a collapsing part is replaced by its wreckage
  rather than removed — unlike a machine's, whose third cell is a bare `TSPoly` and draws nothing.
- **Stage 0** — the fire. A type stating a whole-structure fire (`typeRec+0x08`) lights it at
  `typeRec+0x0a`, but only once `Base_EveryPartGone` reports every part either has no fire of its own
  or is fully damaged; otherwise the part lights its own at its emission point. A structure with
  neither, one part, and no cell sequence of its own is instead **dropped through the floor** — `-100000`
  written straight onto its Z, which is how a small object disappears.

`Base_FinishDependents` (`00403890`) finishes every part naming this one at component record
`+0x1c`, recursively and through the ordinary damage path, so each starts its own sequence. That is
the structure equivalent of a machine's bone group.

`Base_EveryPartGone` (`00403668`) is the "the structure, not just this part, has gone" test:
`typeRec+0x06`-guarded loops elsewhere aside, it walks the parts and passes one whose fire shape is
`-1` **or** whose damage has reached its maximum.

`dgs\BHULKS.DGS` is the wreck library, loaded by `Base_LoadResources` (`00405fac`) into `004a9608`,
sized by `max(typeRec+0x04) + 1` over the whole type table, and bound to `BASETEX` whatever bank the
standing building used. Retail ships 16 wrecks.

The `BASES.DAT` record fields this section reads are tabulated in
[`hit-detection.md`](hit-detection.md#datbasesdat-runtime-record).

## HERCULAN Engine

| Mechanism | Where |
|---|---|
| The file, round-tripped | `HercWorks.Core.Data.File.Dat.Sim.DebrisHerc` + `DebrisHercTransformer` |
| The index space and the databases | `Sim.DebrisCatalog`, `Sim.DebrisDatabase`, `Sim.DebrisGroup` |
| A thrown piece | `Sim.DebrisObject`; `SimWorld.SpawnDebris` is the group throw, `SpawnDebrisPiece` the mount's |
| Burning objects | `Sim.FireEffect`; `SimWorld.SpawnFire` and `ReleaseFires` |
| The structure sequence | `Sim.StructureDeathSequence` and `Sim.BaseObject.DeathSequenceTick` |
| Drawing | `Program.RefreshDebrisItems` (meshes), `RefreshSpriteBatches` (fires), `RefreshWreckItems` (the hulk swap) |
| The sub-shape step, both scales | `Sim.ShapeCellFrames` is the port of `shapeInstance+8`; `ComponentDamage.CellFrames` and `BaseObject.CellFrames` are the per-object arrays. `DtsMeshBuilder` builds every cell into its own gated piece (`BuildSegments` for a machine, `BuildCells` for a structure or a flyer) and `Program`'s per-frame gate pass draws the one each sequence stands on |

Every spawn site in the table above is ported. Both pools are capped at the original's sizes and
both drop a spawn when full, as the original's allocator does.

**Not ported:** the detail-level branches (this engine has no detail setting and always takes the
full-detail figure) and the flyer carrier velocity at `004a96e4` (`FlyerObject` holds no velocity to
add).

**The arcs are large at this world scale.** A `DEF_DEB` group-2 throw peaks around 48 m and lands
about 137 m out over 5 seconds. That follows from constants none of which are this engine's — the
33-88° pitch window, `420 << 10 / mass`, gravity `-0x20`, and the un-integrated position add
confirmed in the disassembly above.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `dat\<name>_DEB.DAT` is a flat table of 9 `int16` entries behind a count | The shape HercWorks' Java original assumed. The file is nested — a group count, then per group a throw count, a piece count and that many **14**-byte pieces — and only that shape consumes all 24 retail files exactly |
| The `.DMG` record's `+0x03` byte is a HUD slot | It is the index of the `TSCellAnimPart` sequence this component drives, which the destruction path steps to its blank cell. The `= 2` write is a cell frame, not a damage state. See [`../formats/mech-shape-drawing.md`](../formats/mech-shape-drawing.md) |
| `typeRec+0x04` indexes the base shape table, so a wreck is another building's model | It indexes `dgs\BHULKS.DGS`, a separate library `Base_LoadResources` sizes from the largest value any type states |
| `WeaponMount_Destroy`'s third argument selects a debris *lifetime*, shorter for the local player | It selects a `(childGroup, deathEffect)` pair, and it is the *path* that picks it: the certain notification passes 0 and the destruction roll passes 1. Neither call site tests who is flying |
| A debris piece's `+0x59` is a lifetime or an eviction priority, as it is on a fire | Different classes at the same offset. On a piece it is the `EXPLOS.DAT` type that goes off where the piece ends |
