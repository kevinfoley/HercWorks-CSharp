# Terrain lighting (DBSIM.EXE) — SOLVED

Terrain is lit **once, at zone load**, and the result is stored in the height grid. Nothing about it
is recomputed per frame.

```
Terrain_BuildSurface (0046c1dc)              once per zone load
 └─ Terrain_BuildCellSurfaceAndShade (0046c2ec)   per cell
     ├─ Terrain_BuildCellSurface (0046bed8)       diagonal selector + both face normals
     ├─ Math_NormalizeVec3ShortToLength (0046c138)  both normals -> length 0x800
     ├─ cell[+0xd] = Light_ComputeShadeForNormal(cell+0x1)    near triangle
     └─ cell[+0xe] = Light_ComputeShadeForNormal(cell+0x7)    far triangle

Terrain_DrawCellQuad (0046d344)              per cell, per frame
 ├─ shades[0] = cell[+0xd]  (near tri)  /  cell[+0xe]  (far tri)
 └─ Raster_DrawTexturedPolyNear (0046865c) / FUN_00468078
     └─ mode 1: DAT_004a09c4 = ((shades[0] * (shadeLevels-1)) + depthBias) << 8
```

That last line is `Raster_ShadeRampRow` inlined — the shade byte is a row of the theater's
`world<n>.rmp`, the same table a flat solid poly resolves through. See
[`distance-fog-and-sky.md`](distance-fog-and-sky.md) for `depthBias`.

## The shade calculation

`Light_ComputeShadeForNormal` (`0048c060`) is the per-normal sibling of `Light_ComputeShadeForFace`.
It walks the active light list; the mission sun is the only entry a retail mission populates, and it
is type 1 (directional):

```
dot = normal . lightDirection             // plain integer dot, no normalisation
if (dot < 0) shade -= (intensity * dot) >> 22
shade = min(shade, 255)
```

**The magnitudes are the whole story.** Normals arrive at length `0x800`, the sun's direction vector
is length `0x1000`, intensity is `0x100`, so `|dot| = 0x800 * 0x1000 * cos` and the expression
collapses to:

```
shade = min(255, 512 * cos)
```

It **saturates at cos 0.5** — every surface within 60° of facing the sun draws at the ramp's top row,
not on a falloff. Flat ground sits at cos 0.544 and is therefore pinned at 255. That is why retail
terrain reads as evenly lit with shading confined to the steeper slopes, and it is not a curve any
Lambert term reproduces.

The contribution is gated on `dot < 0`, so the light's direction field points the way the light
travels, into the surfaces it lights. There is no ambient light anywhere in the binary, so a face
turned away gets shade 0 — ramp row 0, not black (see below).

## The sun

`Light_CreateMissionSun`, once per mission, from constants compiled into DBSIM: direction
`rotate((0,0x1000,0), eulerMatrix(-6000,0,21000))` in Z-up world space, intensity `0x100`. No mission
or theater file contributes.

The 3-axis rotation order was not read out of the exe's fixed-point trig, but only one order is
consistent with the game rendering at all: composing Z after X puts the direction's world Z at
**-0.544**, lighting upward-facing ground. The other order gives +0.233, which leaves every flat cell
facing away from the sun and the whole zone at shade 0.

## The ramp rows are not a 0..1 fade

Measured on `WORLD0.RMP` against `WORLD0.DPL` (luminance out over luminance in, summed across the
palette):

| row | 0 | 8 | 15 | 23 | 30 | 31 |
|---|---|---|---|---|---|---|
| multiplier | 0.36 | 0.62 | 0.79 | 1.00 | 1.15 | 1.16 |

The ramp **brightens as well as darkens** and passes through unity around row 23. An earlier reading
of the file recorded row 0 as near black and row 31 as full brightness; both halves are wrong. This
matters wherever an RGB renderer substitutes a multiply for the indexed lookup — the neutral row is
not the top one.

Flat ground's saturated shade of 255 selects row 30 (`255 * 31 / 256`), i.e. **1.15x** the texture's
own palette colour.

## Shading mode

`DAT_004aab30` selects the span routine's shading mode. Mode 2 is Gouraud, taking a shade per vertex
out of the shades array; mode 1 takes `shades[0]` alone as one flat row for the whole polygon.
`Terrain_DrawCellQuad` only ever fills `shades[0]`, leaving the other three entries of the array
untouched — so retail terrain is flat-shaded per triangle, mode 1, and mode 2 is not something the
terrain path is set up to use.

## Engine implementation

- **`Render/MissionSun`** — the sun's direction and `ShadeFor(normal)`, the saturating 512*cos above.
- **`Render/ShadeBrightness`** — measures the row multiplier table from the theater's own `.RMP` and
  `.DPL` at load. Summed luminances rather than a mean of per-index ratios: a handful of near-black
  palette entries map onto much brighter bytes and produce ratios above 5, which drag a plain mean
  around. The two metrics agree to about 5% regardless (row 30: 1.209 unweighted, 1.149 weighted).
- **`Render/TerrainMeshBuilder`** — bakes one shade per triangle into `MeshVertex.Shade` and marks
  terrain `Unlit`, so the renderer applies no light term of its own over it.

Known divergences:

- The multiplier is per row, not per palette index; the original picks a specific palette entry per
  texel. Faithful in shape, not byte-exact, and it can clip already-bright texels where the row
  multiplier exceeds 1.
- Triangle normals come from the cross product of the triangle actually drawn, where
  `Terrain_BuildCellSurface` differences neighbouring cell heights. Same surface, different
  derivation.
- Distance fog stays continuous per-pixel haze in the engine rather than the original's twelve
  quantised ramp slices.

**What this replaced:** the engine had been running its own directional Lambert
(`0.35 + 0.65 * lambert`) over terrain, which put flat ground at **0.70x** where the original puts it
at **1.15x** — terrain rendered about 1.6x too dark against retail.
