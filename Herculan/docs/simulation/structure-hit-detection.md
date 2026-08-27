# Structure hit detection and damage

How a shot hits a building. Reverse-engineered from `DBSIM.EXE` (Ghidra project `ES2Recon`);
addresses are DBSIM virtual addresses. Companion to
[`damage-system.md`](damage-system.md) (the mech's own path through the same raycast) and
[`impact-effects.md`](impact-effects.md) (what a hit spawns).

Structures are the only class other than the mech whose vtable `+0x20` is ported. **Flyers still
have none** — see "Not ported" below.

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

## The sphere model — `dat\BASECOL.DAT`

One hand-authored hit-sphere model per structure type, in `BASES.DAT` type order, read as one
continuous stream at the tail of `Bases_LoadTypeTable` (`0043a2e0`). Every field is `int16`:

```
per type:   nodeCount
              per node: nodeIndex, clusterCount
                per cluster: componentIndex, sphereCount, sphereCount * { x, y, z, radius }
```

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
- `nodeIndex < 0` means the object's own frame. A non-negative one resolves through the shape
  instance's node-transform array (`inst+0x16 + node[+4] * 0x20`) so a moving part carries its hit
  volume; only the eight animated structure types have any.
- Bound first (`FUN_0040c4c4`), spheres only if it passes (`FUN_0040c524` → `FUN_0040c428`).
- **The ray shortens as the test runs** (global `DAT_004a9894`): each struck sphere clips the
  working distance to its own entry point, `alongAxis - (radius + clearance - offAxis)` floored at
  zero, and later spheres are tested against the clipped ray. The result kept is the *last* cluster
  that hit, which is therefore the nearest. Returned distance is that clipped distance `+ 1`;
  `00405038` adds another `+ 1`.

`FUN_0040c428`'s off-axis test is written in 16-bit arithmetic — `(ushort)(offAxis + reach) <
(ushort)(reach * 2)` — because the doubled radius can overflow a signed short.

### Verified against retail data

`BASECOL.DAT` is 4,938 content bytes; the walk lands exactly on the end after 65 types. Every
cluster's `componentIndex` is inside its type's component array. The geometry reads as deliberate
hitboxes — a three-section bunker with a cluster per section, a gun tower with a cluster per barrel.
One type (3) carries a full model that its `+0x30` flag leaves switched off.

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

## Ported

`Herculan.Engine.Sim.BaseObject` (both paths, damage), `Sim.ShapeVolume` (the grid queries),
`Sim.CollisionModel` (the sphere test), `World.BaseCollisionTable` (`BASECOL.DAT`),
`World.BaseTypeTable` (the combat fields), and the corrected volume read in
`HercWorks.Core.Io.Transform.Dbsim.BasesDgsTransformer`.

## Not ported

- **Flyers.** `FUN_00421c8c` (flyer vtable `+0x20`) runs the same sphere test against
  `flyerTypeRec+0x32`. That model comes from `col\<NAME>.COL` via `Collision_RegisterObject`
  (`0040cd88`), loaded per type by the flyer type loader (`FUN_00422ed0`) and by `Mech_Constructor`
  (`00415bb0`, into `mech+0x1f6`) — the same reader as `BASECOL.DAT`, a different source. Retail
  ships 22 `.COL` files, one per HERC plus `SKIMMER`. **Loading them is also what would let
  `Mech_SelectStruckComponent` name a struck HERC component**, which
  [`damage-system.md`](damage-system.md) lists as outstanding.
- **Node-placed sphere clusters** are skipped rather than tested in the wrong frame — the engine has
  no node transforms for structures. Only the eight animated types carry any, and each keeps its
  body cluster (`nodeIndex == -1`).
- The destruction effect table (`0049741c`), the kill credit (`attacker+0x60`), the mission action a
  destroyed structure fires, the hidden sub-shape, and the 25%-chance secondary effect on every hit.
- Spawn-time component health from the mission record.
- Terrain flattening under a placed structure (`FUN_00470dc8`).

## Known issue

Impact effects and projectiles visibly clip into buildings, which retail does not do. Cause not
investigated — candidates are the collision bound being slightly small (`Collision_ComputeBoundingSphere`'s
fast-magnitude radius runs low, and the volume's cells are 512 units across) or a render-ordering
problem independent of the hit geometry. Recorded in `KNOWN_ISSUES.md`.
