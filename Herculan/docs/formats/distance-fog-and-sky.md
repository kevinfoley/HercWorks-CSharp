# Distance fog and sky (DBSIM.EXE)

Addresses are DBSIM virtual addresses. Ported in
`Herculan.Engine.Content.{ShadeRamp, SkyGradient}`, `Scene.Atmosphere` and `Render.SceneRenderer`.

Two mechanisms that share one palette: everything fades toward the colour the sky already is just
above the horizon.

## Visibility range

`Terrain_DrawCellQuad` installs it per cell:

```
FUN_00467fdc(grid[+0x10c] << grid[+0x108])      // -> DAT_004a08c4
```

`grid+0x10c` is the **view radius in cells** and `grid+0x108` the cell shift.
[`terrain-texturing.md`](terrain-texturing.md#grid0x10c--the-lod--draw-radius-field) is the canonical
account of the field, its writer and the rest of its consumers; the radius comes from the player's
terrain-detail setting, so every range below is per setting, not per zone.

| Cell shift | Zones | Range at detail 0 / 1 / 2 |
|---|---|---|
| 12 | 1 | 147 m / 246 m / 344 m |
| 13 | 10 | 295 m / 492 m / 688 m |
| 14 | 26 | 590 m / 983 m / 1376 m |
| 15 | 2 | 590 m / 983 m / 1376 m |

Shift 15 is the only case the `>>` correction touches. All three table entries are even, so it halves
them exactly and those two zones land on the same ranges as shift 14 — the correction exists to stop
the largest cells reaching further, not to normalise the smaller ones, which it leaves short.

**The same radius is the far clip.** `Terrain_BuildDrawRegionQuad` (`0046d220`) builds the terrain
draw region as a square of that half-width around the viewer, so the world ends exactly where the
fade saturates — which is why the edge does not read as a clip.

## The fade — `Raster_SetDepthFadeFromDistance` (`00467fec`)

```
if (d >= range)   d = range;
if (d <= range/2) bias = 0;
else {
  t    = min(Q16Divide((d - range/2) * 2, range), 1.0)
  bias = Q16Multiply(t, depthSlices - 1) * shadeLevels * 256
}
```

`bias` is a whole number of 8192-byte slices, added by `Raster_ShadeRampRow` (`00468054`) to the row
offset it reads `world<N>.rmp` at. So nothing inside **half** the range is fogged at all, the fade
runs over the outer half only, and it is quantised to the ramp's 12 slices. This is what the file's
11 "unused" height slices are for — see [`dts-texture-binding.md`](dts-texture-binding.md).

**Fog is a ramp lookup, not a blend.** Every fogged pixel is still `world<N>.rmp[row + bias][index]`,
so the fade is whatever that table does. Its slices average out close to a linear fade toward the fog
colour, but they are not one: each palette index fogs at its own rate, and distinct colours stay
distinct almost to the last slice.

`Raster_ShadeRampRow` is not the only reader of the bias. `Raster_DrawPolygon` (`00468310`, mode 1)
and `Raster_SetupTexturedSpan` (`00468078`, mode 2) each compute `shade * (shadeLevels - 1) + bias`
inline for their per-pixel and per-vertex fills, so the Gouraud and textured paths fog by exactly the
same rule without either renderer calling `Raster_ShadeRampRow`.

### The distance measured

The original's view space is **(across, depth, up)**: `Raster_PerspectiveDivide` (`0048c4f0`) divides
components 0 and 2 by component **1** to project. Fog is measured against that depth, not against
distance from the eye — radial distance is larger everywhere off the view axis, by `1/cos` of the
angle off it, which reaches 18% at the corner of the view.

`Terrain_DrawCellQuad` passes the **minimum** of its four corners' depth, once for the whole cell. So
a cell is fogged as if it were all at its leading edge, and the ground is systematically less fogged
than its own depth says — by half the cell's depth extent, averaged over the cell. That is not a
rounding detail: a shift-13 cell is 49 m across against a fade covering 344 m in twelve slices, so
ignoring it costs a whole slice through the middle distance.

## What gets faded

`Raster_SetDepthFadeFromDistance` has exactly three callers:

| Caller | Argument |
|---|---|
| `Terrain_DrawCellQuad` (`0046d344`) | the cell's own distance |
| `FUN_0042876c` | the drawn object's own distance, from its render entry `+0x12` |
| `maybe_TSShapeInstance_PrepareRenderContext` (`0042fa18`) | `0` |

The third does **not** reset anything drawn through `TSSolidPoly_Render`. It belongs to DBSIM's
other, parallel render implementation (the `0042xxxx` family); the poly renderers the DTS type
registry points at are the `00474xxx`/`00475xxx` family, whose group-level setup is
`TSGroup_RenderPolys` (`004758c8`) / `FUN_00475af8` — neither of which touches the bias.

### A projectile is faded like anything else

`FUN_0042876c` is the render entry's vtable slot 0 (`FUN_00428e10` stamps `PTR_FUN_0049ac38`), and it
sets the fade on the line before it calls the object's own slot 0. A bullet reaches it:

1. `maybe_Scene_SubmitFrameObjects` (`0042841c`) walks the bullet pool `DAT_004a9746` →
   `FUN_004282d8` → `FUN_004282f8`, which buckets the round into `ObjList::drawTable` by terrain cell.
2. The per-cell hook `FUN_00428c60` branches on the object's type tag at `+4`. `Bullet_Construct`
   writes **3**, so it takes the deferred branch and gets a 0x36-byte render entry carrying its
   distance at `+0x12`. (Tag 9 is the immediate branch, drawn on the spot with no fade.)
3. `FUN_00429620` → `FUN_004295f0` walks those entries in sorted order and calls each entry's slot 0.

So **a flat solid face is not pinned to ramp row 15 at distance**; it fades from its own range like
anything else drawn.

## The sky — palette entries 208-223

Sixteen horizontal bands, entry 208 at the zenith and 223 at the horizon, with flat 208 above the
gradient. Measured, not RE'd: the draw routine registers itself into a frame-callback table
(`FUN_00401d94`'s table at `004a80d0`) that lives in uninitialised memory, so static analysis cannot
reach it.

- `Reference/Apocalypse_Cockpit.png` is lossless and shows the zone `DATA\script.dat` points at
  (zone 888, theater 1, `WORLD2`). Every band is an exact match for a consecutive `WORLD2.DPL` entry
  — `#D4D0D4`, `#D4D0D8`, `#D8D0D8`, … — changing every 6 rows at y = 107, 113, 119, 125 … in a
  480-row view, with flat 208 above.
- `Reference/Simulator5_Preferences.jpg` shows a `WORLD0` zone, orange `#985C20` at the top to olive
  `#747060` at the horizon. Its ground is flat rather than a ridge, so the run is visible there all
  the way to entry 223, which is what fixes the end of the range.

Band height differs slightly between the two captures (6 px vs ~5.5 px at the same resolution), so
the bands may be angular rather than screen-space.

## Where the two meet

A theater's fog colour — the commonest output of `world<N>.rmp`'s last depth slice at the unlit shade
— lands on entry **222 or 223**, the last colours of that same sky run:

| | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Fog | `#747060` | `#1C1818` | `#F4D4BC` | `#242828` | `#D80400` | `#101010` | `#ECF0FC` | `#0C3058` | `#040404` | `#040404` |

Two independent derivations — the ramp's far slice, and a palette run measured off screenshots —
landing on adjacent entries. Distant terrain therefore fogs to very nearly the colour of the sky
immediately above it, which is why retail's horizon reads as continuous rather than as a seam.

## Engine port

`Scene.Atmosphere` reads all of it off the loaded zone and theater and applies it to
`Render.SceneRenderer` and to `Camera.FarPlane`. The fade is a ramp slice, as above:
`PaletteRampTable` and `SurfaceRampTable` carry every slice and `Scene.glsl` picks one per fragment
with `ShadeRamp.DepthSliceFor`'s formula, truncation included, so a fogged pixel is the byte the
original would have written. Deviations:

- **The per-cell rule is spent as its mean, not as its staircase.** Over a cell's four corners the
  minimum of `i*a + j*b` is `min(0,a) + min(0,b)` and the centre is `(a+b)/2`, so centre-to-nearest
  corner is exactly `(|a| + |b|)/2` — with `a` and `b` the depth a cell step along each grid axis
  covers. `SceneRenderer.FogCellSize` works that out per frame from the camera's forward direction
  and subtracts it from the terrain's own depth. Same fog on the same ground, without a per-vertex
  attribute carrying the four corners; what is missing is the flat step across each cell.
- **A `TSGouraudPoly` fades by an RGB blend** toward `ShadeRamp.FogColor` over the same interval,
  because the engine's Gouraud chain resolves through the palette with no `.RMP` row for a slice to
  bias (see [`dts-texture-binding.md`](dts-texture-binding.md)). The same fallback covers a theater
  whose ramp did not load.
- **The far plane is the range itself, not its diagonal.** The original's draw region is a
  world-axis-aligned square, so along its diagonals it reaches 41% further than a uniform far plane
  does. Invisible, because everything in the gap is already saturated in the fog colour — which is
  the colour the sky's bottom band paints where the terrain stops.
- **The sky is banded in screen space**, `SkyGradient.BandHeightFor` scaling the measured 6 rows at
  480 to the viewport. The horizon is projected per frame from the camera's flattened forward
  direction rather than assumed to be mid-view, because pitch moves it and so does the cockpit's
  off-centre `Camera.PrincipalPoint`.
- `SceneRenderer.SkyColor` is the flat fallback and the clear colour. `ShadeRamp.FogColor` is what
  distant surfaces fade into; the two are separate, as they are in the original.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| Fog is a blend toward the fog colour, so the ramp slices can be replaced by a linear fade of the same mean strength | The slices fog each palette index at its own rate and keep distinct colours apart almost to the last one. A uniform blend fades a whole surface evenly and washes distant terrain into featureless pastel. Only a surface with no `.RMP` row of its own — a `TSGouraudPoly` — legitimately blends |
| Fog is measured against distance from the eye | It is measured against view-space **depth**, component 1 of `Raster_PerspectiveDivide`'s input. Radial distance is larger everywhere off the view axis, by 18% at the corner of the view |
| The engine draws past the visibility range, so the far clip is a separate free parameter | `Terrain_BuildDrawRegionQuad` (`0046d220`) makes the draw region that same radius: the world ends exactly where the fade saturates |
