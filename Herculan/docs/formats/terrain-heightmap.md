# Terrain heightmap — `HeightGrid`, zone loading, height query — SOLVED, byte-verified

Reverse-engineered from `DBSIM.EXE` disassembly (Ghidra project `ES2Recon`). Covers the
heightmap/geometry side of terrain (struct layout, zone loading, height interpolation). See
[`terrain-texturing.md`](terrain-texturing.md) for how cells get their texture, which is a
separate pipeline over the same grid.

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
| `+0x10c` | `int` | LOD value, `10 >> (CellShift-14)` (clamped, default 10) at load time; also a **terrain draw radius in cells**, re-scaled to world units by `maybe_Terrain_ComputeViewDistance` (`00470910`) once/frame — see `terrain-texturing.md` Question 2 |
| `+0x110` | `int` | `HeightBase` — additive height offset (0 for real/binary zones; `MinHeight*8` for the ASCII debug format) |
| `+0x118` | `int` | `HeightScale` — multiplicative height scale applied to each cell's raw byte |
| `+0x11d` | `int` | Material/detail-type record count (from `dat\mat0`) |
| `+0x121` | `int*` | Pointer to the material/detail-type table, `count` × 8-byte records (`ZONES_MaterialTable`, from `dat\mat0`, confirmed against real `ES2/VOL/simvol0/dat/MAT0.DAT`) |

## Per-cell record (16 bytes)

- `+0x0` (byte): raw height value 0–255. World height = `rawByte * HeightScale + HeightBase`.
- `+0x1`..`+0xe` (14 bytes): not decoded (neither loader path writes anything here).
- `+0xf` (byte, bitfield): bits `[0:1]` = diagonal-split selector consumed by `Terrain_HeightQuery`'s
  barycentric interpolation (values `0`/`2` confirmed produced by the loaders; `1`/`3` are handled
  by the query but never observed written — see "Open: diagonal selector" below); bits `[2:7]` =
  material/detail-type index into `ZONES_MaterialTable`, assigned via a weighted random roll
  (~30.6% chance per type, first match wins) at an LOD-driven block stride so neighboring cells
  within a block share one roll.

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

## Open: diagonal-selector bit 1 writer

Nothing in either loader path writes bit 1. Every write to the `+0xf` byte in
`TerrainZone_PopulateFromBitmap` and its ASCII counterpart masks with `& 2` (preserves bit 1,
clears bit 0), and the cell array arrives zeroed from `Cpp_VectorNew` — so both bits stay 0 through
loading for every retail-loaded cell. (The material index, bits `[2:7]`, is what those writes
actually set, via `Math_RandomNext() & 0xfff < 0x4ce` — ~30% — for material 1 vs. 0; the bitmap
path hardcodes a ceiling of two materials, unlike the ASCII fallback which loops the whole `mat0`
table. The roll is sparse — only cells on a block boundary roll, block size
`(1 << (0x15 - mat0[0].field4 - CellShift)) - 1`, 2×2 cells at retail values.)

Since `Terrain_HeightQuery` handles selector value 2 and that value has been observed in practice,
**some not-yet-located code must set bit 1** — not in either loader.

Terrain-renderer theory disproved (2026-08-13): `Terrain_DrawCellQuad` and `FUN_0046ff74` — the
consumers of `cell[+0xf]` on the render side — only ever *read* the byte (`>> 2` for material,
`& 3` for selector); neither writes it. The remaining candidate is whatever else can touch a live
grid at runtime — base/structure placement is the obvious one, since it's the other thing known to
be stamped onto terrain post-load.

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
