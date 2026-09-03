# Effect light sources (DBSIM.EXE)

Addresses are DBSIM virtual addresses. Ported in
`Herculan.Engine.Sim.{EffectLightField, EffectLight}` and
`Herculan.Engine.Render.EffectLightSelection` — see [Engine port](#engine-port).

An impact effect can carry a dynamic light. It is not a light in the renderer's own list: it is a
slot in a separate *effect light manager*, and that manager synthesises a throwaway renderer light
per drawn object, per frame, from whichever slots are close enough to matter. The manager is the
only producer of dynamic lights in the binary — nothing else, not a muzzle flash and not a beam,
claims a slot.

The table row that starts one is `EXPLOS.DAT`'s `LightMode`, in
[`../simulation/impact-effects.md`](../simulation/impact-effects.md). The shade byte a light finally
moves is `Light_ComputeShadeForFace`, in [`dts-texture-binding.md`](dts-texture-binding.md). This doc
owns everything between the two.

## The manager — `DAT_004a968c`

One singleton, 0x404 bytes, built by `FUN_004076e4` at startup. Twenty slots of stride `0x23` from
`+0x6c`, a `Cpp_VectorNew` array:

| Offset in slot | Field |
|---|---|
| `+0x00` | free flag — 1 free, 0 in use |
| `+0x01`, `+0x05`, `+0x09` | world position, three int32 |
| `+0x0d` | intensity as int16, 0-255 |
| `+0x0f` | `A`, copied from manager `+0x10` |
| `+0x13` | `B`, copied from manager `+0x14` |
| `+0x17` | cull radius, derived — see below |
| `+0x1b` | intensity again, as int32; **the field every consumer reads** |
| `+0x1f` | the renderer light object currently standing in for this slot, or 0 |

Manager fields: `+0x00`..`+0x08` the camera position (`FUN_0040707c`, written once a frame from
`maybe_Sim_RenderFrame`), `+0x0c` a literal 10000, `+0x10`/`+0x14` the `A`/`B` above, `+0x18` the
count of live slots and `+0x1c` their pointer array, `+0x328` an embedded light object, `+0x35c` and
`+0x3b0` the free lists the two synthesised light types are recycled through.

### `A` and `B` are 0 and 62

`FUN_00406e44`, the constructor, calls `FUN_00406ee4(mgr, 2000, 3000)`; `FUN_004076e4` then
immediately calls it again with `(10, 2000)`. The setter stores both **shifted right by 5**, so the
values that survive into every calculation below are

```
A = 10   >> 5 = 0
B = 2000 >> 5 = 62
```

`A` is zero, and it is zero in the two places it is used — the denominator offset of both falloffs.
The startup call is what counts; the constructor's pair never reaches a frame.

`FUN_0040735c` recomputes the cull radius whenever position or intensity changes:

```
slot.cullRadius = (slot.intensity * B * 0x20) / 10 + A * 0x20     // = intensity * 198.4
```

At full intensity that is 50,592 world units, about 300 m.

## Claiming a slot

`Explosion_Construct` (`00407f1c`) branches on the type row's `LightMode` at `+0x06` and tests it
**only against zero**. Values 1 and 2 both take the same branch and nothing anywhere else reads the
field, so the two are indistinguishable at runtime; the split is authoring intent that the code never
honoured. Twelve of the twenty-two retail rows are nonzero.

Nonzero allocates a 0x12-byte handle from the pool at `DAT_004a9682` and runs `FUN_00407604`, which
is the whole of the attachment:

```
slotIndex   = LightManager_ClaimSlot(mgr, worldPoint)            // FUN_00406f38
handle+0x0c = slotIndex
LightManager_SetIntensity(mgr, slotIndex, FrameIntensity[0])     // FUN_00407048
```

`FUN_00406f38` seeds the slot's intensity to `0xff` and copies `A`/`B` in; the `FUN_00407048` call
right behind it overwrites the intensity with the row's first ramp entry and, critically, is the
**only** writer of `+0x1b`. `Explosion_TickUpdate` then calls `FUN_004076a0(handle, ...)` — the same
setter through the handle — with `FrameIntensity[frame] & 0xff` as each frame is stepped, and
`FUN_0040765c` releases the slot when the effect dies.

The intensity is read from the ramp at the **new** frame index, and the tick reaches that line only
when the stepped frame is nonzero, so `FrameIntensity[0]` is used exactly once, by the constructor.

### The allocator overruns when all twenty slots are busy

`FUN_00406f38` scans for a free slot and, finding none, falls out of its loop with the destination
pointer still holding **the caller's `worldPoint` argument** and the returned index at `0x14`. It
then writes the full slot record through that pointer, scribbling over the effect's own position
vector, and appends it to the live array. `FUN_00407048` compounds it: index `0x14` resolves to
`mgr + 0x6c + 20 * 0x23` = `mgr + 0x328`, the manager's embedded light object. Twenty-one
simultaneous light-bearing effects is reachable in a heavy exchange. Recorded in
[`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

## Per-object selection — `FUN_00407098`

Called from `ObjList_DrawEntryRender` (`0042876c`), **once per depth-sorted render entry, just
before that object is drawn**, with the entry's cached position and its bounding radius — the
entry's `+0x10` short, filled by `ObjList_DrawCellObjects` from `SimObject_GetShapeRadius`
(vtable `+0x10`).

For each live slot, with `dist` the distance from the object to the slot:

```
if (dist >= slot.cullRadius)  { release the slot's light; slot.light = 0; continue; }

d = dist >> 5
angle = Math_Atan2Guarded(d, radius >> 5)          // (x, y) order: atan(radius / dist)
if (d != 0 && angle < 8000)  -> DIRECTIONAL
else                         -> POINT
```

`8000` in the sim's binary-angle unit is 43.9 degrees, so the test is `radius / dist < 0.964`: the
object subtends less than that from the light, i.e. the light is more than about one bounding radius
away. **Far is directional, near is point** — the light is only made a real point light once it is
close enough that the object's own extent matters, which is the standard approximation and not the
inversion the argument order invites. `Math_Atan2Guarded` takes `(x, y)`, and reading it as `(y, x)`
mirrors the test about the 45-degree line and swaps the two branches.

Both branches recycle through the manager's free lists (`FUN_00407500`,
`Light_GetOrCreateDirectional`, `Light_GetOrCreatePoint`) and reuse the slot's existing light object
untouched whenever its type already matches, so consecutive objects mutate one light rather than
allocating.

**Directional.** Intensity is attenuated by distance at selection time, and the direction is rebuilt
to point from the light at the object:

```
atten     = min(0xff, (B << 8) / (d + A))          // = min(255, 15872 / d)
intensity = slot.intensity * atten >> 8
direction = (objectPos - slotPos) * 0x800 / dist   // length 0x800
```

**Point.** Intensity passes through unattenuated; the falloff is deferred to the shade calculation,
which is handed the same two constants:

```
intensity  = slot.intensity
light+0x34 = A << 5   = 0        // denominator offset
light+0x38 = B * 0x20 = 1984     // numerator
light+0x3c = intensity * 1984    // unread by the shade path
position   = slotPos
```

The synthesised light is registered into the ordinary ten-slot active list (`DAT_006c6130`) beside
the mission sun, so `Light_Register`'s cap means a busy frame silently drops the ninth and later
lights. `maybe_Raster_SetModelTransform` re-transforms every registered light into model space
(`light+0x22` position, `light+0x2e` direction) for each node composed, gated on `DAT_006cbc88` —
which `FUN_0048dbfc`, the per-mission light reset, sets unconditionally, so it is always on in a
mission.

## What a light contributes

The directional term is the sun's own curve, in
[`dts-texture-binding.md`](dts-texture-binding.md); the only difference is that the direction vector
is built at length `0x800` rather than the sun's `0x1000`, which halves the raw dot and makes the
term peak at `intensity` instead of `2 * intensity`.

The point term is `Light_ComputeShadeForFace`'s type-2 branch, and is a different shape:

```
disp = lightPosModel - faceCentre
dot  = disp . normalAsInt                       // normal length 0x800
if (dot > 0) shade += intensity * light+0x38 * (dot / (|disp| + 1)) / (light+0x34 + |disp|) >> 11
```

With `A = 0` and `B * 0x20 = 1984` that reduces to

```
shade += intensity * 1984 * cos / dist
```

**The two branches carry the same falloff.** Substituting `d = dist >> 5` and `A = 0` into the
directional attenuation gives `slot.intensity * 1984 / dist` as its peak too, so the branch boundary
is smooth in magnitude and differs only in the angular term — half-lambert `(1 - cos) / 2` for the
directional approximation against `cos` for the point light. That agreement is what confirms the
`A`/`B` reading; getting either constant wrong makes the two branches disagree by a factor at the
crossover.

So a full-intensity frame adds 255 to a face turned squarely at it within about 12 m, and roughly
100 at 30 m.

## Why retail reads unlit

The intensities are real and large, and the effect is still hard to see. Four structural reasons,
none of them a brightness of zero:

- **Terrain cannot respond.** `Terrain_BuildCellSurfaceAndShade` bakes a cell triangle's shade byte
  once at zone load ([`terrain-lighting.md`](terrain-lighting.md)). The ground — the largest surface
  near any impact — never flashes, whatever is registered when it draws.
- **Only depth-sorted entries are lit at all.** `FUN_00407098` runs from
  `ObjList_DrawEntryRender`. `ObjList_DrawCellObjects` draws class-tag-9 objects immediately and
  fullbright, bypassing the light pass entirely.
- **About two thirds of a second.** Every retail row holds a frame for one tick and the ramps run
  ten frames.
- **The sun has already saturated most of what is visible.** The shape curve is
  `128 + 256 * facing`, so every face within 60 degrees of the sun is pinned at 255 before an effect
  light adds anything. Only surfaces turned away from the sun have headroom, and the billboard
  flipbook is drawn over the part of the object nearest the light.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `LightMode` 1 and 2 select directional versus point | Nothing reads the field but `Explosion_Construct`, which tests it against zero. The type is chosen per drawn object by `FUN_00407098`'s angular test, and both values reach the same code. |
| `A` and `B` are 2000 and 3000 | Those are `FUN_00406e44`'s constructor defaults, overwritten by `FUN_004076e4` before any frame runs. Both calls also shift right by 5, which the raw literals do not show. |
| `Math_Atan2Guarded(d, radius)` makes near lights directional | The helper takes `(x, y)`, so this is `atan(radius / dist)` — the object's angular size. Small angle means far, and far is the directional branch. |
| `Light_ComputeShadeForFace` reads the light's world position | It reads `+0x22`/`+0x2e`, the model-space copies `maybe_Raster_SetModelTransform` rebuilds per node. `+0x04`/`+0x10` are the world-space fields `FUN_00407098` writes. |
| The mission sun is the only entry in the active light list | It is the only *persistent* one, and the only one a mission starts with. Types 1 and 2 are both created dynamically here, into the same ten-slot list. Type 0, ambient, is genuinely never created anywhere in the binary. |

## Engine port

`EffectLightField` is the twenty-slot manager on `SimWorld.EffectLights`; `ImpactEffect` claims a
slot when its row's `LightMode` is nonzero, drives the intensity from the row's ramp on each frame
step, and releases it when the flipbook wraps. `EffectLight.CullRadius` carries `FUN_0040735c`.
`EffectLightSelection` is `FUN_00407098`, run from `SceneRenderer`'s draw loop over each
`SceneItem` whose `LightSubject` names the object it belongs to, and what it picks is uploaded to
`Scene.glsl` as a nine-entry uniform array beside the sun. It stays in world units throughout —
the distance is the sim's own `ApproxDistanceTo` and the branch test its own arctangent — and only
the vectors that leave convert to render space.

The shade sum in the vertex shader is `Light_ComputeShadeForFace`'s: each light's term is added and
the total is clamped at 255 once, per corner, so the sun's own term is floored at 0 but not capped
and a `TSGouraudPoly` still interpolates clamped corner bytes.

Deviations:

- **The allocator refuses instead of overrunning.** A claim with all twenty slots busy returns -1
  and the effect plays without a light. The original's overrun is in
  [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).
- **A ramp read past the row's twelve entries yields 0.** The original runs off the end of the row
  into `ProximityRadius`. No retail shape has a flipbook long enough to reach it.
- **A point light is measured to the corner, not to the face centre.** The original hands
  `Light_ComputeShadeForFace` a poly's stored centre point; the shader has the corner it is
  already lighting. The sun's own term is per corner for the same reason, and the difference is
  bounded by the poly's own size.
- **The arithmetic is float from the selection outward.** The original truncates twice inside the
  point term — `dot / (|disp| + 1)` and the divide by the range sum — where the shader does not.
- The manager's camera position (`mgr+0x00`) is not modelled — its only consumer has no callers.

Terrain is unlit by construction and must stay that way: `TerrainMeshBuilder` bakes
`MissionSun.ShadeFor` into its vertices, which is what the original does, and the shader skips the
whole accumulate for a vertex carrying a baked shade byte. A `SceneItem` with no `LightSubject` —
the terrain, and a projectile, which is drawn fullbright — is lit by the sun alone.

`Herculan.Engine.Host` takes `--impact`, which holds a `--screenshot` capture until a slot is lit:
a light lasts about two thirds of a second, so a fixed frame count is as likely to photograph the
gap between two effects as one of them.
