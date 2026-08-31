# DTS/DBA texture binding and poly shading

NOTE TO CLAUDE: This should be a reference document, not a personal journal.

Covers how a `.DTS` poly gets a colour: which `.DBA` is bound to a model, how a textured poly maps
its UVs, and how the three untextured poly types resolve their surface value. VSHELL findings are
from Ghidra 12.1.2 disassembly of `VSHELL.EXE` (project
`E:\ES2Stuff\tools\ghidra_project\ES2Recon`); the shading sections are from `DBSIM.EXE` and are
marked as such.

**The `.DTS` format carries no reference to any texture file.** Which `.DBA` is bound to a model is
an application-level decision — see "DBA binding" below.

## TSBitmapPart's texture lookup (VSHELL)

Placement, sizing and rotation are in [`dts-billboards.md`](dts-billboards.md). The lookup is a
plain frame index into whichever DBA is currently active:

1. `TSShapeInstance` carries its bound DBA pointer at `+0x26`
   (`TSShapeInstance_GetBoundBitmapArray`, `FUN_0046296d`).
2. `g_ActiveBitmapArray` (`DAT_005d8010`) is a process-wide "currently active DBA" global, with two
   accessors: `TSBase_GetActiveBitmapArray` / `TSBase_SetActiveBitmapArray`.
3. `TSShapeInstance_Render` / `TSShapeInstance_RenderFromRef` (`FUN_00462730` / `FUN_00462894`)
   save the global, swap in the instance's `+0x26`, call `TSShapeInstance_RenderPolys`
   (`FUN_0042203a`, which dispatches each poly's vtable `+0x1c`), then restore.
4. `TSBitmapPart_Render` (`FUN_00421db2`) reads `poly+0x10` (`TSBitmapPart.BmpTag`) as a frame
   index, bounds-checks it against `g_ActiveBitmapArray`'s count, and looks up
   `*(int*)(*g_ActiveBitmapArray+8) + frameIndex*4`. `OfsX`/`OfsY` (`poly+0x12`/`+0x13`, single
   bytes) place the result.

Resolution is `activeDba.Frames[BmpTag]`, with no UV interpolation.

## TSTexture4Poly (VSHELL)

### Surface stride

`ColorIndexId` on disk is `surfaceIndex * 4`, so `surfaceIndex = ColorIndexId / 4`. Front value is
`group.Surfaces[surfaceIndex].FrontColor`; back is `.BackColor`, 2 int32 slots (8 bytes) later.

Two independent sources agree:

- Raw disassembly of `TSTexture4Poly_Render` (`00422af5`):
  `MOVZX ESI,word ptr [EBX+0xc]` → `SHL ESI,0x2` → added to `g_ActiveSurfaceRecords`
  (`DAT_005d88a2`) as a byte offset; front = `*(int32*)(base+offset)`, back = `+8`.
- The file format itself: `DTSModelTransformer`'s `ReadTSGroup`/`WriteTSGroup` read and write the
  on-disk surface count as `colorCount / 4`, matching `TSSurfaceEntry`'s 4-slot
  Front/FrontLine/Back/BackLine layout.

Related symbols: `TSGroup_RenderPolys` / `TSGroup_RenderPolysFromRef` (`0042349d` / `00423709`),
`g_ActiveSurfaceRecords` (`005d88a2`).

### Render path and UV generation

`DAT_00471890` (`g_UseFlatPolyFallback`) selects the mode:

- `== 0` — the textured path, and the only branch that reaches a rasterizer. Builds a per-vertex 3D
  position array from the group's points and a 4-entry UV-corner array, then calls
  `TSTexture4Poly_RasterizeA` / `RasterizeB` (`FUN_004202dd` / `FUN_00420900`).
- `!= 0` — a flat 2D polygon fill. Projects and edge-clips the vertices
  (`FUN_0045e694` / `FUN_0045ee2c`), fills via `FUN_0045f364` → `FUN_0045f8e7` / `FUN_0045f8d2`.
  **No texture sampling.** It still resolves a DBA frame index through the same pointer-table lookup
  `TSBitmapPart_Render` uses, but only for a bounds-check assert.

The front/back value indexes a **20-byte-stride per-frame descriptor table** reached via
`g_ActiveBitmapArray[1]` — one extra pointer dereference from `*g_ActiveBitmapArray`. The first 16
bytes are four `int32` fields `F0..F3`; a 5th field at byte 16 is passed to the rasterizer as a
texture-data handle. UV corners, in vertex order:

```
V0 = (F0, F1)
V1 = (F2, F1)
V2 = (F2, F3)
V3 = (F0, F3)
```

from the decompiled assignment sequence `_DAT_0048a190`..`_DAT_0048a1ac` =
`F0,F1,F2,F1,F2,F3,F0,F3`. Matches the corner order `DtsGeometryBuilder` uses. Assumes each DBA
frame is independently cropped rather than a shared atlas with nonzero offsets, consistent with
`HercWorks.Core`'s `DynamixBitmap` parsing.

### Type identification

`TSTexture4Poly` is a mesh poly: it lives in a `TSGroup` and references real 3D vertices via
`VertexList`/`VertexCount`, structurally unlike `TSBitmapPart`'s 2D quad. It has its own vtable.

- `g_TSObjectTypeRegistry` (`0047f258`, VSHELL) — 18 entries, 12-byte stride, each
  `{tag:uint32, constructorFnPtr, nameStringPtr}`. `tag` matches `TSObjectHeader`'s on-disk
  `[subtype:u16][supertype:u16]` bytes (e.g. `0x0014000f` = `TSTexture4Poly`).
- `TSTexture4Poly_Construct` (`FUN_0045ffe0`) installs its vtable several times during construction
  (Watcom multi-base-class pattern), finishing with `g_TSTexture4PolyVtable` (`0047ee0c`).
- Slot `+0x1c` of that vtable is `TSTexture4Poly_Render` (`FUN_00422af5`), which:
  - reads `poly+0xc` (`ColorIndexId`) as an index into the per-surface runtime record array
    (`DAT_005d88a2`, 4-byte stride);
  - runs `maybe_TSPoly_FrontBackVisibilityTest` (`FUN_0045e480`) on `poly+4`/`poly+6` — confirming
    `TSPoly.Normal`/`Center` are auxiliary point indices used to pick the front or back colour pair;
  - resolves the descriptor at `DAT_005d8010[1] + idx*0x14`;
  - builds world-space positions for all `VertexCount` vertices;
  - derives UV corners from the descriptor's own fields, not from per-vertex file data
    (`TSPoly` carries no on-disk UV fields);
  - calls the rasterizers, which do per-edge clipping, fixed-point interpolation of screen position
    plus a 4th interpolant across spans, and per-pixel inner-loop draws.

RTTI type-name strings are unreferenced in this binary; the type registry is the reliable route to a
constructor. See `project_es2_exe_recon` for the same dead end on `.BND`/`MECH`.

## DBA binding

Which `.DBA` a model gets is not in the `.DTS`.

VSHELL's `dba\rpr_<code>.dba`, `dba\<code>_int.dba`, `_bod`/`_wep`/`_out` are 2D Herc-display UI
graphics, not mesh textures. In-game mech body textures are shared atlases in `simvol0/dba/`:
`LIGHT`, `MEDIUM`, `HEAVY` (weight class), `ENEMY`, `NEWHERCS`, `APOCATEX`, `RAZORTEX`.

### DBSIM's mech-to-texture mapping

`MechType_InitOne` (`004201a8`) sets each mesh sub-component's `TSShapeInstance+0x26` to
`&g_MechTextureGroupSlots + typeRecord[0x96]*8`. `g_MechTextureGroupSlots` (`004a9df6`) is an
8-byte-stride array, one slot per texture group. `typeRecord+0x96` is file record offset 148 —
`HercSimDat.ModelSkinId`.

Byte-verified against every `simvol0/dat/*.DAT` (226 bytes each: 9-byte VOL prefix + 216-byte
content + 1 trailer, matching the function's `0xd8` = 216-byte read):

| ModelSkinId | Group | Mechs |
|---|---|---|
| 0 | light | OUTLAW |
| 1 | medium | TOMAHAWK |
| 2 | heavy | SAMSON, COLOSSUS |
| 3 | enemy | DIABLO, CERBERUS, HYPERION, MIRIMAC, MONGOOSE, HEADHUNT, PITBULL, ACHILLES, RAMSES, SCARAB, STINGRAY, SPIDER |
| 4 | apocatex | APOCA |
| 5 | razortex | RAZOR |
| 6 | newhercs | OGRE, MAVERICK, RAPTOR2 |

Consumed by `Model3DViewerForm.TryLoadDefaultTextureBank()` (best-effort, with "Load Texture Bank"
as a manual override) and by `Scene.SceneModelLibrary`.

## Engine texturing

`HercSimDat.ModelSkinId` → bank name → `.DBA` → `Surfaces[ColorIndexId / 4].FrontColor` → frame →
corner order above. `Render.TextureAtlas` decodes and packs a bank, `Gl.GpuTexture` uploads it,
`DtsMeshBuilder` emits UVs, `Scene.SceneModelLibrary` selects the bank and caches one atlas per bank
per mission.

Two deliberate departures from the original:

- **Frames are packed into one atlas.** The original ships none — a `.DBA` is independently-sized
  frames, and a software rasterizer pays nothing to switch between them. A GPU pays per bind.
  Packing is a pure relocation, so the corner order is unaffected. The largest retail bank is 53
  frames into 256x512.
- **Nearest-neighbour sampling, no mipmaps.** The original point-samples; filtering would look
  softer than the game did. Filtering belongs in the opt-in bucket per planning.md's "vanilla by
  default" principle.

### Fleet audit

Every `dts\*.DTS` with a matching `dat\*.DAT`, 22 mechs:

- **21 of 22 have zero unresolved texture polys** — every `Surfaces[ColorIndexId/4].FrontColor`
  lands inside the selected bank's frame count. Across ~2000 polys, a wrong stride or a wrong bank
  mapping would produce out-of-range indices.
- **TOMAHAWK has 4 anomalous polys**, all identical: `ColorIndexId = 0` into a 1-entry `Surfaces`
  array with `FrontColor == BackColor == 3084` (`0xC0C`) against a 36-frame bank. Reads as a
  degenerate group in the source art. They fall back to the placeholder colour.
- **13 `TSTexture4Poly`s across 6 mechs have 3 vertices, not 4** (SAMSON, COLOSSUS, CERBERUS,
  HYPERION, OUTLAW, TOMAHAWK). The engine flat-shades these. The exe's UV builder populates a
  4-entry array and its 3-vertex branch has not been traced. Follow-up: decompile the vertex-count
  branch of `TSTexture4Poly_Render`'s UV setup.

### Coincident twins

Real DTS meshes stack a textured poly exactly on top of a flat-shaded twin — 186 such pairs in
SAMSON's first root. `DtsMeshBuilder.DropCoincidentTwins` keeps one per group, ranked: a resolved
texture poly beats a flat poly, which beats an unresolved texture poly. The three-way rank (rather
than a boolean) keeps the no-bank path drawing the flat twin. Textured triangle counts with the rank
in place: SAMSON 142, DIABLO 198, APOCA 232, with total triangle counts unchanged.
## Poly types and their colour mechanisms (DBSIM.EXE)

DBSIM's DTS type registry (`g_TSObjectTypeRegistry`, `004a63c8` — 12-byte `{tag, ctor, name}`
entries keyed by the on-disk chunk marker) identifies a `TSObject` subclass by construction. Use it,
not structural resemblance to VSHELL's renderers.

| Tag | Type | Vtable | Render (`+0x1c`) | Surface value means |
|---|---|---|---|---|
| `0x00140002` | `TSSolidPoly` | `004a5ef8` | `00474db4` | palette index |
| `0x00140003` | `TSShadedPoly` | `004a6000` | `0047542c` | shade-ramp number |
| `0x00140009` | `TSGouraudPoly` | `004a5fd4` | `004755c8` | shade-ramp number |
| `0x0014000f` | `TSTexture4Poly` | `004a5f24` | `00474e9c` | `.DBA` frame index |

The four are different mechanisms. Only `TSTexture4Poly` samples a bitmap.

The group's surface array is read raw (`TSGroup_ReadFromFile`, `0048e8e4`), so a renderer's surface
value is the file's own `{int16 colour, int16 flag}` pair packed into one int32, flag in the high
half. A pair with `0x14` in the top byte means "do not draw this face"; retail uses it on back pairs
only (flag 5120, against 1024 on the front).

Retail usage counts: `TSSolidPoly` is rare — 12 polys in `BULLETS.DTS`, 57 in `ROCKETS.DTS`, 73
across the whole mech and building fleet. `TSShadedPoly` is nearly everything else: 1227 of APOCA's
1368 polys, 2049 of `BASES_AN`'s.

### `TSSolidPoly` — palette index, unlit, fill plus outline

`TSSolidPoly_Render` (`00474db4`) computes no light term and resolves **two** colours per face:

```
pick front pair (surface[0]=Front, surface[1]=FrontLine) or back pair ([2]/[3]) by visibility
skip if both have 0x14 in the top byte
row  = Raster_ShadeRampRow(0x80)        // the fixed unlit row; no light term is ever computed
fill = row[Front];  line = row[FrontLine]
FUN_0048d518 -> fill the polygon, then when line != fill re-draw it in `line`
```

The second pass is the rasterizer's **mode 4**, which `FUN_00483dac`'s `iVar11 == 4` branch walks as
a line loop over the poly's own vertex list, closing back to the first vertex — an outline, not a
second fill. The `line != fill` test is on the **ramped** bytes, so two surface values that resolve
to the same ramp output draw no outline.

Across all 55 retail `.DTS`, 11 roots carry a surface whose line colour differs from its fill:
`BULLETS.DTS` root 4 (ATC35), five weapon-model roots in `MECHWPNS`/`MECHWPN2`, and 3-edge slivers on
two `HYPERION` LODs and one `MIRIMAC` root. ATC35's three quads are gold `#D0CC3C` with no outline,
`#ECCCAC` outlined `#E4E4E4`, and `#DCCCA0` outlined `#D8D4D4`.

### `TSShadedPoly` — shade-ramp number, per-face light, fixed `.RMP` row

`TSShadedPoly_Render` (`0047542c`) resolves a face's colour in two lookups, and the same pair again
for the line colour:

```
shade        = Light_ComputeShadeForFace(normal, center)     // 0048bedc, 0..255
paletteIndex = Palette_ShadeRampLookup(surface.Front, shade) // 00430e34
byte         = Raster_ShadeRampRow(0x80)[paletteIndex]       // 00468054, the FIXED unlit row
```

All of a shaded face's lighting is in the first lookup; the `.RMP` row is the same literal `0x80` the
unlit solid renderer passes and never varies with light.

`Palette_ShadeRampLookup` (`00430e34`):

```
idx = value & 0xff;  if (idx >= ActivePalette.rampCount) idx = 0;
pos = Q8Multiply(shade, ramp[idx].length);
if (pos == ramp[idx].length) pos = ramp[idx].length - 1;
return ramp[idx].indices[pos];
```

The surface value names a *material*; the light level picks a step along that material's brightness
sequence.

### `TSGouraudPoly` — same ramp number, per-vertex light, no `.RMP` row

`TSGouraudPoly_Render` (`004755c8`; its tag function `0048e450` returns `0x140009`) shares the
surface-pair selection and the light function, and differs in both of the things that decide a pixel:

```
for each vertex i:
    shades[i] = Light_ComputeShadeForFace(points[normalList[i]], points[vertexList[i]])
DAT_006c60e4 = shades;  DAT_006c60d8 = 1;      // fill mode 1: interpolate the shade
PolyFill_FillThenOutline(...)
```

- The shade is computed **per vertex**, walking `NormalList` and `VertexList` in step, and
  interpolated by the span routine.
- **`Raster_ShadeRampRow` is never called.** The ramp lookup moves into the span so it can vary per
  pixel; the fixed `.RMP` row is not part of this path. A Gouraud surface's colour is the material
  ramp's entry straight through the palette.

Distinguishing evidence: the `.RMP` row shifts every ramp entry down one step and collapses two
pairs, so for `WORLD2`'s ramp 8 the shaded chain can never emit palette 178 (`#68687c`). A retail
capture of the ramp-8 cylindrical structure (`Reference/Gouraud_shading_comparison.png`) shows
`#9090a4 #848498 #7c7c90 #707084 #68687c #606074 #545468 #4c4c60 #444454 #3c3c4c #343444` — a
consecutive run of the **raw** ramp entries, `#68687c` included. Neither the fixed-row chain nor a
per-pixel row selected by the interpolated shade contains all eleven.

### The `.DPL` shade-ramp table

Immediately after the `colourCount * 4` colour entries. Read byte-complete on all four
`WORLD<n>.DPL`:

```
int32  rampCount              // 256 in every retail file
rampCount x {
  int16  length               // retail: 1, 4, 7, 8, 13 or 16
  int16  paletteIndex[length] // darkest to brightest
}
```

Only the low ~19 slots carry real ramps; the rest is the degenerate `[255]`. `WORLD2`: ramp 0 is
`196..203` (greys `#484848`..`#d4d4d4`), ramp 8 is `172..187` (blue-greys `#343444`..`#c4c4d4`),
ramp 12 is `192..198` (near-black to `#707070`).

Corroboration that the surface value is a ramp number and not a frame index: across the retail fleet
shaded-poly surface values cluster on **0-15**, overwhelmingly on the even (multi-step) slots.
`APOCA.DTS` uses exactly four values over 1227 polys (12, 2, 0, 8); `SAMSON.DTS` five; `BASES_AN.DTS`
nine, topped by 14, 8, 4, 12. A frame index into a 24-to-66-frame bank does not concentrate like
that. Colour check: every distinct tone the tall chimney (structure type 14) shows in
`Reference/Scramble_Training_Base_2.png` is in the set `WORLD2`'s ramp 8 produces, and its surfaces
name ramp 8.

### Two shade calculations — terrain and shapes use different ones

Both walk the active light list and reduce, for the single directional sun, to a function of
`facing = -cos` between the surface normal and the direction the light travels (positive = lit).
Normals carry length `0x800` and the sun's direction `0x1000`, so the raw dot is `0x800000 * cos`.

| | `FUN_0048c060` (terrain) | `Light_ComputeShadeForFace` `0048bedc` (shapes) |
|---|---|---|
| gate | `t = dot` | `t = (dot - 0x400000) >> 1` |
| accumulate | `if (t < 0) shade -= (intensity * t) >> 22` | same |
| reduces to | `512 * facing` | `128 + 256 * facing` |
| edge-on (facing 0) | 0 | 128 |
| reaches 0 at | 90 degrees off the light | 120 degrees off |
| saturates at | facing 0.5 | facing 0.496 |

`FUN_0048c060` is what `Terrain_BuildSurface` bakes a cell with; see
[`terrain-lighting.md`](terrain-lighting.md). Every poly renderer of a *shape* calls
`Light_ComputeShadeForFace`. For a shape the falloff is half as steep, so a curved surface spends its
gradient over twice the angular range, and a shadowed side is a mid tone that keeps falling rather
than a floor of black.

Intensity scales both terms together (`shade = I * (0.5 + facing)` for the shape curve), so it does
not move the zero crossing.

### The sun

One hardcoded directional light per mission, created unconditionally by `Light_CreateMissionSun`
(`00461240`). No ambient light is created anywhere in the binary.

```
angles = (-6000, 0, 21000)                     // Vec3Short, 0x10000 per full circle
BuildEulerRotationMatrixQ14(angles, m)         // 0047eaac
RotateVectorByMatrixQ14((0, 0x1000, 0), m, d)  // 0047ffb4
intensity = 0x100
```

`BuildEulerRotationMatrixQ14` reads a 1024-entry quarter-wave cosine table at `DAT_004a25dc` in Q14,
indexed `round(angle / 16)` with the usual quadrant reflection. Because the rotated vector is
`(0, 0x1000, 0)`, only the matrix's middle column matters: `m[2] = ±cosX·sinZ`, `m[3] = cosX·cosZ`,
`m[5] = sinX`. With X = `-6000` (-32.96 degrees) and Z = `21000` (115.34 degrees) the direction is
**(±0.758, -0.359, -0.544)** at length `0x1000` — horizontal component 0.839, vertical 0.544.

### Normals live in the point list

A poly's normal is not a vector stored on the poly. It is a **point index**, and the shape's normals
are extra entries in the same `TSGroup.Points` array its corners come from. All flat renderers
dereference `TSPoly.Normal` (`poly + 4`) with the 6-byte `Vec3Short` stride against the group's point
base — `*(ushort *)(poly + 4) * 6 + DAT_006c696c` — and hand it, with the same treatment of
`TSPoly.Center` (`poly + 6`), to `TSPoly_FrontBackVisibilityTest`.

`TSGouraudPoly.NormalList` is the per-vertex form: an offset into the group's **index** array running
parallel to `TSPoly.VertexList`, whose entries are point indices of normals rather than of corners.
`TSGouraudPoly_Render` walks the two lists in step, one light call per vertex.

Verified on `BASES.DGS` shape 11 (structure type 15): every entry the list reaches has length exactly
**2048** — the `0x800` the shade calculation is scaled around — and adjacent side panels share the
normal at the edge between them.

**Stored normals oppose the corner winding.** For every poly in `BASES.DGS`, `BASES_AN.DTS` and
`APOCA.DTS` — 12,656 of them, no exceptions and no intermediate values —

```
dot(normalize(cross(p1 - p0, p2 - p0)), storedNormal) == -1.000
```

so a normal derived from the corner order is the negation of the one the file carries. A renderer
must use the stored normal, not the winding: the front/back sign is derived from the poly's normal
and then applied to the **corner** normals, which come from this same point list. Mixing the two
conventions inverts the light term. It is invisible on a flat poly, where the face and corner normals
are the same vector and the sign cancels, and shows up only once per-vertex normals are in use.

### `TSPoly_FrontBackVisibilityTest`

Per **poly**, not per pixel. Takes the poly's own stored normal and centre points; when it answers
"back", the renderer negates *all* of that poly's normals before lighting them and takes the back
surface pair instead of the front.

## `TSDetailPart` level selection and STRUCTURE DETAIL

`TSDetailPart_Render` (`004768bc`, vtable installed by `FUN_00476834`):

```
size = (radius << DAT_006c60ac) / max(FastMagnitude3D(viewOffset) - radius, 1)   // projected size
if (size == 0) size = 1
t    = Q10Multiply(DAT_004a1034, size)          // global detail scale, Q10
i    = DAT_004a1038                             // global detail BIAS -- the STRUCTURE DETAIL setting
while (i < count - 1 && details[i] < t) i++
render(parts[min(i - DAT_004a1038, count - 1)])
```

`radius` is the part's own `ClassItem` bounding radius (`part+8`). Thresholds are walked in file
order and the part index is `i - bias`, so:

- `details[]` is ascending and index-aligned with `Parts[]`: **part 0 is the coarsest**, the last is
  the finest. Retail structure shapes end at 255 (`BASES.DGS` shape 5: `[5, 15, 35, 255]`).
- A **larger** `DAT_004a1038` shifts the whole scale down, so `LOW`/`MED-HIGH`/`MAXIMUM` is a bias of
  2/1/0 in some order with **0 = MAXIMUM**. At bias 0 a close object reaches `count - 1`.

Levels are not always the same shape at different densities. `BASES.DGS` shape 10 (structure type 14,
the tall chimney) is a 4-sided box with its corners on the world axes at level 0, and an octagon with
its *vertices* on the axes at level 1 — a 45-degree difference in cross-section.

## Implementation status

`Herculan.Engine` unless noted. `Model3DViewerControl` (HercWorks.UI) implements only the
`TSTexture4Poly` and averaged-colour paths and none of the shading work below.

| Mechanism | Status |
|---|---|
| `TSTexture4Poly` UV mapping | Exact, 4-vertex quads only; falls back to a placeholder colour with no bank |
| `TSSolidPoly` fill + outline | Exact; outline is a second primitive range in the same buffer (`MeshBuild.TriangleVertexCount`) |
| `TSShadedPoly` / `TSGouraudPoly` colour | Exact, via `SurfaceRampTable` |
| Per-face / per-vertex shade | Exact, `MissionSun.ShadeForFace` in the vertex shader |
| Lit textured texel | Exact, via `TextureAtlas.IndexPixels` + `PaletteRampTable` |
| Terrain shade | Exact, `MissionSun.ShadeFor` baked per triangle into `MeshVertex.Shade` |
| `TSBitmapPart` | Implemented as a view-space billboard quad — see [`dts-billboards.md`](dts-billboards.md) |
| Cutout texture frames | Structure banks decoded index-0-transparent; shader discards |
| `TSDetailPart` | Maximum detail only (`Parts[^1]`); distance selection and the STRUCTURE DETAIL setting not implemented |
| Front/back visibility test | Normal flip implemented; **back surface pair not selected** — `FrontColor` is used unconditionally |
| Per-poly stored normals | Exact; `DtsMeshBuilder.ResolveFaceNormal` reads `TSPoly.Normal` as a point index. All triangles fanned from one poly share it. The winding survives only as a fallback for an unresolvable normal index, negated to match |
| Distance fog | Continuous per-pixel haze, not the original's 12 quantised `.RMP` depth slices |

How the shading paths map onto engine types:

- **`SurfaceRampTable`** — 256 shade columns x 512 rows. Rows `0..255` are the `TSShadedPoly` chain
  (ramp entry through `.RMP` row `0x80`), rows `256..511` the `TSGouraudPoly` one (raw ramp entry).
  A vertex's `MeshVertex.ShadeRamp` is a row of this table: the surface's ramp number, biased by
  `SurfaceRampTable.GouraudRowOffset` for a Gouraud poly.
- **`PaletteRampTable`** — 256 palette-index columns x 32 `.RMP` rows, expanded through the palette.
  This is `rampRow(shade)[texelPaletteIndex]`, the original's per-texel operation for a lit textured
  surface. Requires the atlas to be bound from `TextureAtlas.IndexPixels` (palette index in red)
  rather than `Pixels`. Anything that blits a frame unlit — HUD sprite sheets, the billboard
  renderer — keeps using `Pixels`.
- **Shade byte** — computed per vertex in the vertex shader and interpolated, which is what makes a
  `TSGouraudPoly` Gouraud. The shade clamps at 0, so clamping per corner before interpolating (the
  original) and clamping after are not the same picture. `MeshVertex.FaceNormal` carries the poly's
  stored normal alongside the possibly-smoothed `Normal`, so the front/back flip is one decision per
  poly and both vectors share the point list's convention.
- **Fallbacks, reached only by a theater with no ramp table or no palette** (no retail theater):
  `TextureAtlas.AverageColor` for a shaded surface, and an unshaded texel for a textured one.

Cutout frame inventory: `BASETEX` frames 11, 36, 38, 39, 52, 53, 60, 61, 63-65 are 20-73% palette
index 0 each. Mech skins are excluded — they carry a handful of stray index-0 texels that are paint,
not cutouts (9 of 44376 in `LIGHT`, 7 of 68464 in `MEDIUM`).

## Rejected readings

Each of these was implemented or documented at some point and is disproven. Do not reintroduce.

| Reading | Why it is wrong |
|---|---|
| `FUN_00474e9c` is `TSSolidPoly_Render` | It is `TSTexture4Poly_Render`; the type registry settles it. Assigned by resemblance to VSHELL's renderer |
| A flat poly's `FrontColor` is a `.DBA` frame index sampled as a dither swatch | Only `TSTexture4Poly`'s is a frame index |
| A flat/shaded surface renders as the frame's **average colour** | The value is a palette index (`TSSolidPoly`) or a ramp number (`TSShadedPoly`/`TSGouraudPoly`). Averaging `BASETEX` frames 0/8/12 gives browns and greens where ramps 0/8/12 are greys and blue-greys |
| `DefaultShapeColors`, a 13-entry guess table | Mostly clamps to cyan |
| A direct `.DPL[FrontColor]` palette index for the lit types | Right idea, wrong table — it indexes the ramp table, not the colour table |
| `abs()` on the light term (to keep winding-flipped triangles from going black) | Gives a surface pointing away from the sun the same light as one facing it. The original flips the normal toward the **eye**, then lights it signed |
| The terrain shade curve (`512 * facing`) applied to shapes | Shapes use `Light_ComputeShadeForFace`; see the table above |
| The fixed `.RMP` row applied to `TSGouraudPoly` | That path never calls `Raster_ShadeRampRow` |
| A brightness multiplier over an expanded RGB texel, in place of the indexed lookup | The `.RMP` is a per-colour remap that preserves hue and compresses unevenly; no scalar reproduces it |
| Interpolating the normal and computing the shade per fragment (Phong) | The original interpolates the shade computed per vertex; the two differ wherever the 0 clamp bites |
| Normals are not reachable, so Gouraud cannot be implemented | Normals are extra entries in the point list; `NormalList` indexes them per vertex |
| A winding-derived face normal stands in for the stored one, the eye-facing flip cancelling the sign | It cancels only while the corner normals are that same vector. Once they come from the point list the sign is derived from one convention and applied to the other, and every Gouraud poly lights inside out — dark toward the sun. The two conventions are exactly opposed; see "Normals live in the point list" |

## Unresolved: type-15 band widths

Retail's scanline across the type-15 octagon (`Reference/Gouraud_shading_comparison_2.png`, and the
same structure in `Reference/Scramble_Training_Base_4.png`) is six narrow bands of ~4 px (ramp-8
entries 9 down to 4) followed by four wide ones of 28, 29, 29 and 59 px (entries 3 down to 0). The
wide bands are not reproducible under the mechanism above: for the facet whose normal faces away from
the sun, `128 + 256 * facing` is negative at both corners, so it must be a single flat entry-0 band,
yet retail grades it.

Checked and excluded as the cause:

- The sun direction, re-derived from `BuildEulerRotationMatrixQ14`'s own arithmetic (see above) and
  independently corroborated by flat terrain being pinned at full brightness.
- Intensity, which scales both terms and cannot move the zero crossing.
- The ramp entry sequence, which matches retail exactly and in order.

A 2D scanline simulation over shape 11's real plan octagon, sweeping camera azimuth, sun azimuth and
sun elevation, reproduces the band *structure* only near a sun horizontal component of ~0.50 against
the derived 0.839. That simulation approximates the projection and knows neither screenshot's camera,
so it bounds the problem rather than locating it.

Tracked in `KNOWN_ISSUES.md`.

## Open follow-ups

- Trace the function that populates `g_ActiveBitmapArray[1]`'s 20-byte-stride descriptor table, to
  confirm `F0/F1` (frame UV top-left) are always `(0,0)` or can be nonzero (atlas sub-rects).
- `TSTexture4Poly_RasterizeA`/`RasterizeB`'s internal fixed-point interpolation math and 4th
  interpolant semantics.
- `.DBA`'s on-disk frame layout (assumed covered by `HercWorks.Core`'s `DynamixBitmap` parsing).
- Where the runtime frame descriptor's **transparency flag** comes from. `TSTexture4Poly_Render`
  passes `*(int16*)(frameDescriptor + 0x12)` as `Raster_DrawPolygon`'s last argument, which selects
  the span routine's transparent half (`DAT_004a09ac`). Nothing in the `.DBM`/`.DBA` headers carries
  it — every retail frame's two spare header fields are zero — so it is derived or set at load, and
  the function that builds the descriptor table has not been traced. The engine decodes whole
  structure banks index-0-transparent instead, which is equivalent on retail data because a frame
  with no index 0 draws the same either way; see `SceneModelLibrary.LoadAtlas`.
- The back surface pair (`BackColor`/`BackLineColor`) is never selected — see the front/back note
  above.
