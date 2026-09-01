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

`grid+0x10c` is the **view radius in cells** and `grid+0x108` the cell shift. This is one of several
readers of `+0x10c`; [`terrain-texturing.md`](terrain-texturing.md#grid0x10c--the-lod--draw-radius-field)
is the canonical account of the field, its writer and the rest of its consumers.

| Cell shift | Zones | `+0x10c` | Range (world units) | Range |
|---|---|---|---|---|
| 12 | 1 | 10 | 40960 | 246 m |
| 13 | 10 | 10 | 81920 | 492 m |
| 14 | 26 | 10 | 163840 | 983 m |
| 15 | 2 | 5 | 163840 | 983 m |

`+0x10c`'s own derivation (`10 >> (cellShift - 14)`, with 10 the retail detail-table entry) is in
[`terrain-texturing.md`](terrain-texturing.md#grid0x10c--the-lod--draw-radius-field). Its behaviour
below shift 14 is unverified — it only affects the 11 zones at shift 12 and 13.

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
`Render.SceneRenderer`. Deviations:

- **The fade is continuous per-pixel haze, not 12 per-object ramp steps.** Same start, end and
  colour; smoother between. `ShadeRamp.Lookup`'s two-argument form therefore reads slice zero and
  the haze supplies the rest; `ShadeRamp.DepthSliceFor` ports the real calculation and is unused by
  the renderer.
- **The sky is banded in screen space**, `SkyGradient.BandHeightFor` scaling the measured 6 rows at
  480 to the viewport. The horizon is projected per frame from the camera's flattened forward
  direction rather than assumed to be mid-view, because pitch moves it and so does the cockpit's
  off-centre `Camera.PrincipalPoint`.
- `SceneRenderer.SkyColor` is now only the flat fallback and the clear colour, no longer a stand-in
  for the fog colour.
