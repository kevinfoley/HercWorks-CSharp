# Terrain heightmap — `HeightGrid`, zone loading, height query, byte-verified

Reverse-engineered from `DBSIM.EXE` disassembly (Ghidra project `ES2Recon`). Covers the
heightmap/geometry side of terrain: struct layout, zone loading, height interpolation, and the ray
walk. See [`terrain-texturing.md`](terrain-texturing.md) for how cells get their texture, which is a
separate pipeline over the same grid.

Ported in `Herculan.Engine.Terrain.HeightGrid` (`HeightGrid.cs`, `HeightGrid.RayWalk.cs`).

## The `HeightGrid` struct

0x129 (297) bytes, allocated by `HeightGrid_Constructor` (`0046bdf8`), installed as
`ActiveHeightGrid` (`004a0bf8`) by `Terrain_LoadZone` (`0042789c`).

| Offset | Field | Meaning |
|---|---|---|
| `+0xec` | `int*` | Base pointer to the per-cell array (16 bytes/cell, row-major: `cellIndex = x + y*(1<<WidthShift)`) |
| `+0xf0` | `byte*` | Parallel per-cell scratch/flag byte array (`width*height` bytes), written but not traced to a consumer |
| `+0x100` | `int` | `WidthShift` — log2(grid width in cells) |
| `+0x104` | `int` | `HeightShift` — log2(grid height in cells) |
| `+0x108` | `int` | `CellShift` — log2(world-units per cell); also the shift used to convert world (x,y) → cell (x,y) |
| `+0x10c` | `int` | **View radius in cells** (10 at retail detail settings). Its derivation, writer and consumers are in [`terrain-texturing.md`](terrain-texturing.md#grid0x10c--the-lod--draw-radius-field); `Terrain_DrawCellQuad` installs `+0x10c << +0x108` as the visibility range distance fog is measured against — see [`distance-fog-and-sky.md`](distance-fog-and-sky.md) |
| `+0x110` | `int` | `HeightBase` — additive height offset (0 for real/binary zones; `MinHeight*8` for the ASCII debug format) |
| `+0x118` | `int` | `HeightScale` — multiplicative height scale applied to each cell's raw byte |
| `+0x11d` | `int` | Material/detail-type record count (from `dat\mat0`) |
| `+0x121` | `int*` | Pointer to the material/detail-type table, `count` × 8-byte records (`ZONES_MaterialTable`, from `dat\mat0`, confirmed against real `ES2/VOL/simvol0/dat/MAT0.DAT`) |

## Per-cell record (16 bytes)

- `+0x0` (byte): raw height value 0–255. World height = `rawByte * HeightScale + HeightBase`.
- `+0x1`..`+0x6` (3 shorts): the **near** face normal, scaled to length 0x800.
- `+0x7`..`+0xc` (3 shorts): the **far** face normal, same scale. Which of the two a point belongs
  to is the diagonal selector's decision, exactly as in `Terrain_HeightQuery`.
- `+0xd` (byte): the **near** triangle's baked shade byte; `+0xe` (byte): the **far** triangle's.
  Written at zone load and read straight back by `Terrain_DrawCellQuad` as the ramp row — see
  [`terrain-lighting.md`](terrain-lighting.md).
- `+0xf` (byte, bitfield): bits `[0:1]` = diagonal-split selector consumed by `Terrain_HeightQuery`'s
  barycentric interpolation (values `0`/`1`/`2` are produced; `3` is handled by the query but never
  written); bits `[2:7]` = material/detail-type index into `ZONES_MaterialTable`, assigned via a
  weighted random roll (~30.6% chance per type, first match wins) at an LOD-driven block stride so
  neighboring cells within a block share one roll.

**The selector and both normals are written by the same function**, `Terrain_BuildCellSurface`
(`0046bed8`), which `Terrain_BuildSurface` (`0046c1dc`) runs over the whole grid once at zone load
via `FUN_0046c2ec`. Choosing a cell's normals requires choosing its diagonal, so it derives the
selector from the four corner heights and stores all three together:

| Corners | Selector | Split |
|---|---|---|
| `h00 + h11 == h01 + h10` | 1 | coplanar quad — both normals identical, no triangle test |
| `h00 + h11 - (h01 + h10) < 1` | 2 | along the `(0,0)`–`(1,1)` diagonal |
| otherwise | 0 | along the `(0,1)`–`(1,0)` anti-diagonal |

Normals are built in *raw height units*, not world units: the horizontal components are plain corner
differences and the vertical one is `cellSize / HeightScale`, which is a true cross product divided
through by `HeightScale`. All six components are doubled before `FUN_0046c138` rescales them to
0x800, which changes nothing. The last row and column are skipped — no east/north neighbour to
difference against — so they keep a flat `(0, 0, 0x800)` normal and selector 0.

## Loading pipeline

Confirmed against real files in `ES2/VOL/ZONES.VOL`.

1. `Terrain_LoadZone(zoneIndex)` builds the base name `zoneNNNN` and reads a **16-byte per-zone
   header** resource at `dat\zoneNNNN` (`ZONES.VOL\DAT\ZONE*.DAT`, always exactly 16 bytes): four
   LE `int32`s — `[0] WidthShift` and `[1] HeightShift` (redundant, re-derived from the bitmap
   itself later), `[2] CellShift`, `[3] HeightScale`. E.g. `ZONE504.DAT` =
   `07 00 00 00 07 00 00 00 0E 00 00 00 95 00 00 00` → WidthShift=7, HeightShift=7 (128×128
   cells), CellShift=14, HeightScale=149.
2. `TerrainZone_LoadHeightmap` (`0046c650`) loads the shared material table from `dat\mat0`, then
   opens `dba\zoneNNNN.dba`. Every real zone resolves to `.dba` and goes through the generic
   `ClassItem_LoadResource` polymorphic loader — the same registry-dispatch architecture as
   `.DFN`/`.HFN`/`.DCI` — into `TerrainZone_PopulateFromBitmap` (`0046c3c0`). Any other extension
   falls back to a plain `fopen`/`fscanf` ASCII format (`"%d %d %d %d"` header =
   WidthShift/HeightShift/MaxHeightRaw/MinHeightRaw, then one `%d` per cell) — a level-design/debug
   path; no loose files of this kind exist in retail data.
3. **`TerrainZone_PopulateFromBitmap`: a zone's heightmap is literally an ordinary
   `DynamixBitmap` image** — the same 8-bit-indexed container used for `.DBM`/`.DBA` textures
   elsewhere (see `dfn-hfn-dci.md`). Each pixel byte (minus a small bias) becomes one cell's raw
   height byte; `WidthShift`/`HeightShift` are re-derived from the bitmap's own dimensions rather
   than trusted from the zone header. **Verified byte-exact against every real file in
   `ES2/VOL/ZONES.VOL/DBA/`:** 128×128 zones are exactly 16418 bytes (`128*128 + 34`-byte
   `DynamixBitmap` header), 256×256 zones exactly 65570 bytes (`256*256 + 34`) — the zones that
   come out 256×256 are precisely the ones whose `.DAT` header declared `WidthShift=HeightShift=8`
   (e.g. `ZONE123.DAT`).

`Terrain_HeightQuery(HeightGrid*, {x,y})` (`0046e07c`) converts a world `(x, y)` into a grid cell
via `CellShift`, fetches the enclosing cell's 4 corner texels from the 16-byte-per-cell array, and
— using each cell's `+0xf` diagonal-selector bits — does barycentric/bilinear interpolation across
whichever triangle the query point falls in. Each grid quad can independently choose which way its
diagonal split runs, chosen at terrain-authoring/compile time.

Neither loader path writes the selector — every `+0xf` write in `TerrainZone_PopulateFromBitmap` and
its ASCII counterpart masks with `& 2` and sets only the material index, via
`Math_RandomNext() & 0xfff < 0x4ce` (~30%) for material 1 vs. 0. (The bitmap path hardcodes a ceiling
of two materials, unlike the ASCII fallback which loops the whole `mat0` table. The roll is sparse:
only cells on a block boundary roll, block size `(1 << (0x15 - mat0[0].field4 - CellShift)) - 1`,
2×2 cells at retail values.) `Terrain_BuildCellSurface` runs afterwards and is what fills it in.

## Ray-versus-terrain — `Terrain_RayWalk` (`0046e87c`)

The terrain module's largest function (5129 bytes). Takes two world points and reports where the
segment between them first passes into the ground. Its last argument selects one of two bodies over
a shared walk: **mode 0** is the thin-ray query (weapon fire, via `Sim_RaycastTerrain`); **mode 1**
sweeps a volume instead, through `FUN_0046fe84`/`FUN_0046ff74`/`FUN_0046fcac`, and is the movement
collision path. Only mode 0 is described here.

Setup: halve the segment delta until every component fits ±32000 (mode 1 packs it into three
shorts), take four Q16 slopes — `dy/dx`, `dz/dx`, `dx/dy`, `dz/dy`, each falling back to 1.0 on a
zero denominator — and classify the ground-plane delta into an **octant** 0–7, which encodes the
major axis and both step signs in one value. Ties make X the major axis.

| Octant | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|---|
| Major axis | X | Y | Y | X | X | Y | Y | X |
| X step | + | + | − | − | − | − | + | + |
| Y step | + | + | + | + | − | − | − | − |

The walk is a cell DDA with a two-phase step: each iteration takes the segment's exit across the
major axis, but if a minor-axis boundary falls before it, that exit is **stashed and replayed on the
next iteration** while the minor crossing is handled first. Each iteration therefore yields one
sub-segment and one exit edge code — 0 west, 1 east, 2 north, 3 south.

`Terrain_EdgeCrossingTest` (`0047035c`) is the per-step test, and takes only the exit edge's two
corner heights: below both → clear, at or above both → hit, straddling → interpolate the edge height
at the exit point. Only once that reports a crossing does `Terrain_CellSurfaceIntersect` (`0047068c`)
solve the exact point, intersecting the sub-segment with the cell's triangle planes (built from the
`+0x1`/`+0x7` normals, through a corner each triangle contains) via
`Math_PlaneSegmentIntersect` (`0047e504`). The near triangle is tried first; a hit outside it falls
through to the far one.

Three details that look like porting slips but are the original's:

- **Six multiplies truncate.** In the octant-2 and octant-3 arms the compiler emitted 32-bit
  `imul`/`sar 16` (at `0046f077`, `0046f08d`, `0046f162`, `0046f178`, `0046f228`, `0046f23e`)
  instead of the 64-bit `Math_Q16Multiply` used everywhere else. Diverges only at grazing angles.
- **Selector 2's far plane keeps the near triangle's plane constant.** The far normal is loaded but
  `d` is not recomputed against it, so that plane is skewed by the difference in the two normals' Z.
- **Selector 0's far triangle shares no corner with its own cell** (its corners are `01`/`10`/`11`),
  so the code shifts one cell east and takes corner `10` from that record. Deliberate, and why the
  two selectors are not symmetric.

The last step — the one ending at the segment's endpoint rather than a cell boundary — has no exit
edge and falls back to `Terrain_HeightQuery` at each end instead. The **first** iteration also
queries the start point, so a ray beginning underground is a hit immediately.

Returns no-hit for a segment starting outside the grid or walking off its edge. When the plane solve
finds nothing it still returns "hit" with its output buffer left unwritten.

`Sim_RaycastTerrain` (`00428048`) is the weapon-fire caller: it builds the ray's far end as the
muzzle frame's own `(0, distance, 0)`, walks, and measures the ground hit back to the muzzle with the
fast-magnitude approximation. The ray record's `+0x08` (a literal 200) is passed through as a "walk
radius" but **mode 0 never reads it**. See
[`../simulation/weapon-firing.md`](../simulation/weapon-firing.md).

## Consumers outside the terrain system

- **Rocket ground-impact detonation** (`FUN_00409d2c`) checks altitude against
  `Terrain_HeightQuery` every tick and detonates the instant a projectile dips below ground — see
  [`../simulation/damage-system.md`](../simulation/damage-system.md#explosive-damage-blast-sweep-random-per-component-roll-distance-falloff-shield-gated).
- **Flyer ground-proximity/terrain-avoidance autopilot: `FUN_004198f4`** (likely `flyersys.cpp`,
  unconfirmed by assert string). Six direction-flag bits from the flyer's type record (a per-type
  "which sensors does this airframe have" mask) each gate a probe in a fixed direction
  (front/back/left-right/up/down offsets from static direction-vector tables); for each active
  probe, queries terrain height via `Terrain_HeightQuery` and/or raycasts via `FUN_00426528` (see
  [`../simulation/damage-system.md`](../simulation/damage-system.md#the-shared-raycast-fun_00426528)),
  and if triggered, nudges the flyer's vertical-speed field (`+0xe`) away from the obstacle and
  plays a proximity-alarm tone whose volume/pitch scales with distance via the fast-magnitude
  approximation from [`../simulation/dbsim-physics-notes.md`](../simulation/dbsim-physics-notes.md).
