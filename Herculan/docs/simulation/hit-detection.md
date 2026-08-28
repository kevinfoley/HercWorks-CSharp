# Hit detection

How a shot decides what it struck, for all three shootable classes. Reverse-engineered from
`DBSIM.EXE` (Ghidra project `ES2Recon`); addresses are DBSIM virtual addresses. Companion to
[`damage-system.md`](damage-system.md) (what the hit then does to a mech) and
[`impact-effects.md`](impact-effects.md) (what a hit spawns).

All three vtable `+0x20` implementations are ported: `Base_DirectFireHitTest` (`00405038`),
`Mech_DirectFireHitTest` (`00418ba8`, in [`damage-system.md`](damage-system.md)) and
`Flyer_DirectFireHitTest` (`00421c8c`). All three reach the same sphere test; only the model source
and what surrounds it differ.

## The sweep — `Sim_RaycastObjectList` (`00426528`)

Full treatment in [`damage-system.md`](damage-system.md). One candidate filter is easy to miss and
matters: **an object whose mission group still carries an action is skipped before any geometry is
touched** (`*(int*)(obj[+0x45] + 0x14) != 0` — the group record's action slot, the same test
[`mission-deployment.md`](mission-deployment.md) covers). Retail missions place undeployed groups by
the ordinary rules, so several routinely sit stacked on a shared waypoint; the first stock mission
parks seven objects in three overlapping pairs. Without the skip they are invisible and unticked but
perfectly solid, and shots stop on nothing.

## `Base_DirectFireHitTest` — `00405038`

Vtable `+0x20` for every structure class: all five type-switched vtables `FUN_00405314` installs
(`00497784`, `00497818`, `004978ac`, `00497940`, `004979d4`) point slot `+0x20` at this one
function. Like the mech's, it is the hit test and the damage application in one call.

```
wreck = typeRec[+4] != -1 && componentState[0][+5] <= 1 && obj[+0x99] != 0
if (wreck || typeRec[+0x38] == 0)  -> collision-volume path, component 0
else                               -> sphere-model path, names the component
if (hit) {
    if (!destroyed) vtable+0x74(componentIndex, shot.DamageArmor, shot.owner)
    spawn ImpactFXArmor effect at rayTransform.TransformPoint(0, hitDistance, 0)
    if ((rand & 0xfff) < 0x401) spawn a second effect from a different pool   // 25%, unported
}
```

`typeRec[+0x38]` is non-null exactly when `BASES.DAT`'s `+0x30` is non-zero, so the branch is a
per-type flag. Retail split: **25 of 65 types use the sphere model, 40 use the volume**. A
*destroyed* type that leaves a wreck (`typeRec[+4] != -1`) switches to the volume whichever it used
standing — the wreck is a different shape out of `BHULKS.DGS`.

Both paths open with the same coarse reject as every other hit test in the simulation:
`shapeRadius + shot.clearance + rayLength < |muzzle - object|`, where `shapeRadius` is vtable
`+0x10` (`FUN_0046b80c`), i.e. `shape+8`.

**Damage recovery for plasma.** `Bullet_TickUpdate`'s subtype-9 branch stashes the round's two
damage figures in globals (`FUN_0040501c` → `DAT_004a9676`) and then **zeroes them in the shot
record** before the raycast. A volume-path hit whose shot carries zero on both counts reads the
armour figure back out of that global. Unreachable in the engine, which does not port the plasma
branch (see [`../engine/handoff-weapon-effects.md`](../engine/handoff-weapon-effects.md)).

## The sphere model — `dat\BASECOL.DAT` and `col\<NAME>.COL`

One format, two sources. Every field is `int16`:

```
nodeCount
  per node: nodeIndex, clusterCount
    per cluster: componentIndex, sphereCount, sphereCount * { x, y, z, radius }
```

- **Structures** read 65 of these back to back out of `dat\BASECOL.DAT`, in `BASES.DAT` type order,
  as one continuous stream at the tail of `Bases_LoadTypeTable` (`0043a2e0`). `componentIndex`
  indexes the type's `BASES.DAT` component array.
- **Mechs and flyers** each read one whole file, `col\<NAME>.COL`, through
  `Collision_RegisterObject` (`0040cd88`) — the mech from `Mech_Constructor` (`00415bb0`, into
  `mech+0x1f6`), the flyer from its type loader (`FUN_00422ed0`, into `flyerTypeRec+0x32`).
  `componentIndex` indexes the `.DMG` file's 29-slot component array instead.

Readers: `Collision_LoadRecordArray` (`0040ccf8`) → `Collision_ReadNode` (`0040cc50`) →
`Collision_ReadCluster` (`0040cc14`) → `Collision_ReadSphereArray` (`0040c7c4`). The last three were
named `Collision_LoadSubSpheres` / `Collision_LoadSubSphereFlag` / `Collision_LoadSubMeshIndices`;
the "flag" is the component index and the "8-byte index/sub-mesh records" are the spheres. The field
order inside a cluster is not the struct order: `componentIndex` lands at the record's `+6` and is
read first, `sphereCount` lands at `+0` and is read second, because the two live in different
functions. `sphereCount` is tested as `value & 0x1fff` but allocated and read unmasked — the mask is
only a zero-test, and no retail record sets the top bits.

**Not on disk:** each cluster's bounding sphere, built at load by `Collision_ComputeBoundingSphere`
(`0040c5d0`) as the AABB of the children each inflated by its own radius, centred on that box's
midpoint, radius = `Math_FastMagnitude3D` of the half-extents. That approximation runs a few percent
under a true Euclidean radius, so the bound is slightly tight; it is behaviour, not an artefact.

### The test — `Mech_SelectStruckComponent` (`0040c9d4`)

Despite the name it is shared: structures reach it from `00405038`, flyers from `FUN_00421c8c`,
mechs from `Mech_DirectFireHitTest`. Only the model source differs.

Per cluster (`Mech_ComponentGeometryTest_Candidate`, `0040c8fc`):

- A cluster whose component is already destroyed (`obj+0x201` alive flags) is **skipped entirely**,
  so a building stops blocking shots through the sections it has lost.
- `nodeIndex < 0` means the object's own frame. A non-negative one is a shape **part id**, resolved
  through the shape (`shape->vtable+0x20`) to that part's transform slot and then through the
  instance's node-transform array (`inst+0x16 + part[+4] * 0x20`), so a moving part carries its hit
  volume. A part the shape does not have falls back to an identity transform rather than being
  skipped. **This is the whole of a HERC's hit geometry** — every mech `.COL` cluster is node-placed,
  so the volume walks with the legs and swings with the torso. Structures are the opposite: only the
  eight animated types have any, and each keeps its body cluster in the object frame.
- Bound first (`FUN_0040c4c4`), spheres only if it passes (`FUN_0040c524` → `FUN_0040c428`).
- **The ray shortens as the test runs** (global `DAT_004a9894`): each struck sphere clips the
  working distance to its own entry point, `alongAxis - (radius + clearance - offAxis)` floored at
  zero, and later spheres are tested against the clipped ray. The result kept is the *last* cluster
  that hit, which is therefore the nearest. Returned distance is that clipped distance `+ 1`;
  `00405038` and `00421c8c` each add another `+ 1`, `00418ba8` does not.

`FUN_0040c428`'s off-axis test is written in 16-bit arithmetic — `(ushort)(offAxis + reach) <
(ushort)(reach * 2)` — because the doubled radius can overflow a signed short.

The two transform helpers under it differ only in input width: `FUN_004800c8` takes a `short`
point (structure/sphere centres), `FUN_00480330` an `int` one. Both branch on the transform's rank
byte at `+0x12` — translation only, Z-rotation only, or full 3×3 — which is an optimisation, not a
behavioural difference.

### Verified against retail data

`BASECOL.DAT` is 4,938 content bytes; the walk lands exactly on the end after 65 types. Every
cluster's `componentIndex` is inside its type's component array. The geometry reads as deliberate
hitboxes — a three-section bunker with a cluster per section, a gun tower with a cluster per barrel.
One type (3) carries a full model that its `+0x30` flag leaves switched off.

All **22 `.COL` files** likewise walk exactly to their own end and **round-trip byte-exact** through
`HercColliderTransformer`. Every `componentIndex` is inside the 29-slot array (max 28); every node
id resolves to a real shape part except RAZOR's single node 5, which takes the identity fallback
above. Node counts run 1 (RAZOR, SKIMMER) to 13 (SPIDER); sphere radii 40–600 world units.
`SKIMMER` is the only file with an object-frame cluster.

ACHILLES' first cluster cross-checks against its `.DMG`: it places spheres for components 7, 9 and
11 on nodes 3 and 1, and 7→9→11 is exactly the `BoneId` chain that file states for the left leg.
Component 7's two spheres are `(-20, 0, -100) r=200` and `(-20, 0, -400) r=180` — a thigh as two
stacked balls.

**This corrects `HercWorks.Core`'s `HercCollider`**, which described a 10-byte header followed by
data it called undecoded. There is no header: those five shorts are the walk's first five fields,
which is why that reading's own observations lined up as they did — "always 6" is ACHILLES' node
count, "always 3 for hercs / FFFF for skimmer" the first node's index, the "collider type" that
crashes above 1 the first node's cluster count reading past the end of the file, "hercs have 7" the
first cluster's component index, and the last field its sphere count.

## The collision volume — the `.DGS` record's height field

**This corrects [`../formats/dgs-hd0-notes.md`](../formats/dgs-hd0-notes.md).** That doc's steps 5–6
("5 `int16` scalars + a fixed 1024-byte block", then "sub-record count × sub-record size raw
records") are one structure: the shape's **collision volume**. The old walk consumed exactly the
same byte count, so every retail record parsed correctly while all of it was named wrongly.

Read by `BaseShape_ReadFromStream` (`0042762c`):

| Offset | Type | Meaning |
|---|---|---|
| `+0x2a` | `int16` | columns (grid X extent) |
| `+0x2c` | `int16` | rows (grid Y extent) |
| `+0x2e` | `int16` | origin column |
| `+0x30` | `int16` | origin row |
| `+0x32` | `int16` | log2 of the cell size in world units |
| `+0x34` | 256 × `int32` | height table, indexed by a cell's byte code |
| `+0x430` | — | the table's last entry, addressed directly as the grid's ceiling |
| `+0x434` | rows × columns bytes | height codes, row-major with Y outermost |

**A building is a height field, not a mesh.** Nothing tests a shot against structure polygons; the
ray is stepped in shape space and each step asks the grid how tall the column under it is.

- `FUN_00427238` — the height under a point. When `1 << (shift-1) < radius` it takes the tallest of
  every cell within `radius`; below that it samples the single cell. Off-grid returns 0, which is
  what makes the volume end at its own edges.
- `FUN_004273c8` — the march. Rejects an empty grid, and a ray with *both* ends above the ceiling.
  A point is shifted by `origin << shift` (world units, not cells) before it is divided down, so the
  footprint straddles the model. Step length is `(1 << shift) + clearance`; the direction is rescaled
  to it by a Q16 divide whose result is **truncated to 16 bits** before it multiplies. Hits when the
  sampled height is non-zero and above the ray's current Z.
- `FUN_00427da8` — the object test around it. After the coarse reject, brings the object's centre
  into muzzle space and tests a box on **X and Y only** (`|x| < reach`, `-reach < y < rayLength +
  reach`) — Z is not tested. Then transforms the ray into shape space and marches. Also called by
  `FUN_00404bc0`, a bulk line-of-sight query over the structure list.

### Verified against retail data

All 45 `BASES.DGS` records: cell shift is 9 (512 world units, ~3 m) in every one, the origin is the
grid centre, and the height table is **ascending in all 45** — which is what makes `+0x430` the true
ceiling rather than just the last entry. Footprints run 2560×4096 to 19456×19456 world units
(15 m to 117 m), and ceilings 511 to 29537.

Firing at all 65 types from 16 bearings × 15 muzzle heights (200 to 6000 world units) strikes every
type on its correct path, at distances matching where the surface is. Short structures stop being
hit above their own roof line: type 51's shape has a 899-unit ceiling and it is struck only from the
two sample heights below that.

### `shape+8` is the bounding radius, not an id

The third of the three `int16` head fields every `ClassItem` record carries (`ClassItemTree_ReadBaseHeader`,
`0048f894`). Two unrelated consumers identify it: the LOD selector (`FUN_004033e4`) divides it by
viewing distance to estimate on-screen size, and vtable `+0x10` (`FUN_0046b80c`) hands it to every
coarse hit reject. It tracks `BASES.DAT`'s own `+0x2a` radius within about a fifth across all 45
records (6334/5600, 10325/9600, 3577/3600). `BasesDgsTransformer` called it `Id`, which was a
placeholder rather than a finding.

## Component damage — `Base_ApplyDamage` (`00404d70`)

Vtable `+0x74`. Structures have a per-component health model, much simpler than the mech's:

```
if (typeRec[+0x1e] != 0) return                       // invulnerable
if (componentIndex == -1) componentIndex = 0
if (!alive[componentIndex]) return
taken = damage[i] + incoming
destroyed = component.maxDamage <= taken
if (!destroyed && component.maxDamage / 2 < taken) {
    tenth = Q16Divide(10, maxDamage)
    for (a = Q16Multiply(damage[i], tenth); Q16Multiply(taken, tenth) > a; a++)
        if ((rand & 0xfff) <= 0x199) { destroyed = true; break }    // ~10% per step
}
if (!destroyed) { damage[i] = taken; return }
damage[i] = maxDamage; alive[i] = false; attacker recorded at state+7
if (vtable+0x40 == 0x100) {                            // FUN_004052b4, the Q8 damage fraction
    obj[+0x99] = 1; obj[+0x96] = 0; fire the object's mission action (obj+0x1b6)
    attacker->vtable+0x60 credits the kill
}
if (component[+4] != -1) { state[+5] = DAT_0049741c[component[+4]]; state[+3] = 300 }
```

**A component can die early, at random.** Past half its maximum, one ~10% roll fires per tenth of
the component's health the shot moved it through, so a heavy hit on a half-wrecked section usually
finishes it before its stated hit points run out, and the same hit twice does not do the same thing.

`FUN_004052b4` (vtable `+0x40`) is a **ratio of sums**, not a count of destroyed components:
`(Σ damage << 8) / Σ maxDamage`. A type with one 30000-point core and six 2000–8000-point parts is
effectively destroyed by killing the core alone, which is how both seven-component retail types are
authored.

Spawn-time health comes from the block-9 record's `param_1[0x19]`: `<0` or `100` = undamaged,
`0` = spawned destroyed (and the component's sub-shape is hidden), anything else scales
`(100 - pct) * maxDamage / 100`. **Not read by the engine** — structures always spawn intact.

## `dat\BASES.DAT` runtime record

`Bases_LoadTypeTable` reads straight into a 60-byte struct offset by offset, so **the file's field
order is the runtime record's field order**. The only divergence is the component array, inline on
disk and a pointer at `+0x14`.

| Offset | Type | Meaning |
|---|---|---|
| `+0x02` | `int16` | shape index into the selected library |
| `+0x04` | `int16` | wreck type index, `-1` for none |
| `+0x06` | `int16` | 0 selects `dgs\BASES.DGS`, else `dts\BASES_AN.DTS` |
| `+0x12` | `int16` | component count |
| `+0x14` | array | components, 30 bytes each |
| `+0x1e` | `int16` | non-zero = invulnerable (types 21, 22, 23) |
| `+0x2a` | `int16` | hit radius, vtable `+0x5c` (`FUN_004035a4`); four types state 0 |
| `+0x30` | `int16` | non-zero installs `BASECOL.DAT`'s model at runtime `+0x38` |
| `+0x32` | `int16` | texture bank selector |

Unread: `+0x00`, `+0x08`, `+0x0a` (6 bytes), `+0x10`, `+0x18` (6 bytes), `+0x20` (4 bytes),
`+0x24`–`+0x28`, `+0x2c`, `+0x2e`.

Component record, 30 bytes, three fields read:

| Offset | Meaning |
|---|---|
| `+0` | max damage (retail 1000–30000) |
| `+2` | sub-shape hidden when destroyed, `-1` for none |
| `+4` | index into DBSIM's fixed destruction-effect table (`0049741c`), `-1` for none |

Runtime object fields the hit path uses: `+0x0c` euler triple, `+0x12` transform (translation at
`+0x26`, valid flag at `+0x32`), `+0x34` shape instance (`+4` = shape), `+0x99` destroyed,
`+0x1f2` type record, `+0x201` per-component alive flags, `+0x205` per-component state (11 bytes:
`+0` damage, `+3` effect timer, `+5` effect id, `+7` attacker).

## `Flyer_DirectFireHitTest` — `00421c8c`

The shortest of the three, and the only one with no second piece of geometry behind it: the coarse
reject on `SimObject_GetShapeRadius` (`shape+8`), then straight to the sphere test against
`flyerTypeRec+0x32` with the alive flags at `flyer+0x208`. No shields, no collision volume.

```
if (shapeRadius + shot.clearance + rayLength < |muzzle - obj|) -> miss
hit = Mech_SelectStruckComponent(typeRec[+0x32], obj, ray, objToMuzzle, obj[+0x208])
if (hit) {
    fx = shot.ImpactFXArmor[rand & 3]
    vtable+0x74(hit.component, shot.DamageArmor, shot.owner)
    if (obj[+0x99]) fx = 10                       // already a wreck: a fixed effect id, not the shot's
    spawn fx at rayTransform.TransformPoint(0, hit.distance + 1, 0)
    spawn a second effect from a different pool   // unconditional here, 25% for a structure
}
```

A flyer's health record is **one component with one dependent** — `FUN_004215f4` allocates the
arrays with literal counts of 1, which is exactly what `SKIMMER.DMG` ships. Its vtable `+0x74`
(`FUN_00421bb4`) is a thin wrapper: destroy component 0 and the aircraft is lost (`obj+0x99`), it
fires its mission action, credits the kill, and is given a large negative rate at `obj+0x2e` to
fall.

Retail ships a `.COL` and a `.DMG` for `SKIMMER` only, so `HOVTANK` and `DROPSHIP` cannot be shot
at all — in the original as much as here.

## Ported

`Herculan.Engine.Sim.BaseObject` (both paths, damage), `Sim.FlyerObject`, `Sim.MechObject.Combat`,
`Sim.ComponentDamage` (the mech/flyer health record), `Sim.ShapeVolume` (the grid queries),
`Sim.CollisionModel` (the sphere test, with the node resolver),
`World.CollisionModelReader` (the shared format), `World.BaseCollisionTable`,
`World.BaseTypeTable` (the combat fields), and — on the tool side — the corrected volume read in
`HercWorks.Core.Io.Transform.Dbsim.BasesDgsTransformer` and the corrected, now round-trippable
`HercColliderTransformer`.

## Not ported

- **Node-placed clusters on structures** are tested in the object frame rather than the node's — the
  engine has no posed node transforms for structures. Only the eight animated types carry any.
- The destruction effect table (`0049741c`), the kill credit (`attacker+0x60`), the mission action a
  destroyed structure fires, the hidden sub-shape, and the secondary effect every hit rolls.
- Spawn-time component health from the mission record.
- Terrain flattening under a placed structure (`FUN_00470dc8`).
- The second exclusion `Sim_RaycastObjectList` tests at the shot record's `+0x14`. The beam path
  never writes that field — it is stack garbage there, so the comparison excludes nothing.
- `FUN_00404bc0`, the bulk line-of-sight query over the structure list.

## Measured: hit geometry versus the drawn mesh

Prompted by projectiles appearing to sink into buildings. Across every static structure type, the
`.DGS` grid's world footprint is **larger** than the mesh it is drawn with, by 200–900 units a side,
because it rounds out to whole 512-unit cells — so the volume never runs small horizontally and a
shot should if anything stop slightly early. `Sim_RaycastShapeVolume`, `ShapeVolume_Raycast`,
`ShapeVolume_HeightAround`, `Collision_RaySphereTest` and `Collision_ClusterBoundTest` all re-check
as faithful ports, including the 712-unit march step (`(1 << 9) + clearance`) that retail shares.
The remaining suspect is render layering, not hit geometry.

The same pass found a real data quirk: several types' collision **ceiling** sits well below their
roof line — type 3 stops at 2225 against a 6756 mesh, type 22 at 9400 against 18300 — so shots at
the upper part of those buildings pass clean through. That is the retail data's own authoring.
