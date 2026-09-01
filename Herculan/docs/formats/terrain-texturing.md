# Terrain texturing (DBSIM.EXE)

Terrain *lighting* is a separate subject with its own file: [`terrain-lighting.md`](terrain-lighting.md).

See `dts-texture-binding.md` for the mech-side texturing chain — terrain and mechs share a data structure.

## The answer, end to end

```
world<N> descriptor file  ──(a string field in the data)──▶  dba\<name>.DBA
                                                                    │
                            Terrain_BindTextureBank (0046bc98)      │
                                                                    ▼
                                         _DAT_006b4fc4 = 20-byte-per-frame descriptor table
                                                                    ▲
   cell[0xf] >> 2  =  material index ──▶ mat0[i] (8 bytes) ─ field 0 = FRAME INDEX
                                                           └ field 1 = BlockShift (tiling)
                                              Terrain_ResolveCellTexture (0046bcf4)
```

**Terrain bank selection:** `Terrain_BindTextureBank` loads a DBA by name read from the `world<N>` descriptor file. Bank names (`ice`, `bsnow`, `volcan`, `moon`, `urban`) are data-driven, not hardcoded.

**Descriptor table:** 20-byte stride (frame descriptors); first 16 bytes are the `int32` UV-rect corners `F0..F3` (documented in `dts-texture-binding.md`). Terrain and mechs share one texturing substrate.

### The `world<N>` descriptor — layout

`wld\WORLD0.WLD` … `WORLD9.WLD`, 310–313 bytes each. `maybe_World_LoadTheater` reads them field-by-field in this order:

| | |
|---|---|
| 8 x `int16` | dispatched into subsystem setup (`0042ebbc`, sky/haze globals), not stored as a struct |
| 6 x `int16` | ditto; two land in `DAT_004cfd76`/`DAT_004cfd78` |
| `int32` count + count x `int32` | 16 entries in every retail file, ascending in even steps |
| `int32` count + count x `int32` | 16 again, identical to the first array |
| `int16` rows, `int16` cols | sizes the pair of ramp tables that follow |
| cols x `int32`, `int16`, cols x `int32` | expanded by `FUN_00430d08` into `_DAT_004cfd7c` |
| 4 bytes, 4 bytes | a second, 1-wide ramp through the same expander |
| `int16`, `int16`, `int32`, `int32` | |
| 5 NUL-terminated strings | `world24`, `clouds2`, `impact<N>`, **terrain bank**, `tex` |

| descriptor | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| bank | urban | urban | bsnow | bsnow | volcan | volcan | ice | ice | moon | moon |

Five theaters, two variants each. Retail missions use variant 0; variant 1 purpose (weather/time-of-day) unknown. Which theater, variant and zone a mission runs is the `script.dat` header's — see
[`script-dat.md`](script-dat.md#header-format).

Alongside the terrain bank, `maybe_World_LoadTheater` loads the theater palette `dpl\world<N>.dpl`,
one per theater, which mech and structure shading resolves through too.

### `mat0`'s two fields, both now resolved

`TerrainMaterial.Index` (field 0) had been recorded in the engine as a self-index with *no known
consumer*. **It is a DBA frame index** — `_DAT_006b4fc4[Index * 0x14]`.

`TerrainMaterial.BlockShift` (field 1) selects between two placement modes:

- **`BlockShift == 0`** — the frame's own `F0..F3` corners are used verbatim: the whole frame is
  stretched across the quad.
- **`BlockShift != 0`** — tiled:

  ```
  shift = cellShift + BlockShift - 13
  u0    = (cellX << shift) & 0xff        u1 = u0 + (1 << shift)
  v0    = (cellY << shift) & 0xff        v1 = v0 + (1 << shift)      (V is negated)
  ```

  **The `& 0xff` is the tiling wrap** — UV space is 256 texels, which is why every terrain bank
  ships as 256x256 frames and why they must be edge-tileable. Each cell covers `2^shift` texels.

`Terrain_ResolveCellTexture` returns descriptor field 4 (bytes 16–19) as a short — the bitmap
handle passed to the polygon draw.

### Retail numbers

`MAT0.DAT` holds 13 records: `{0,6}`, `{1,6}`, `{2,5}`, … — field 0 ascending (frame index), field 1 per-material tiling shift. At `cellShift = 14`, materials 0 and 1 give `shift = 14 + 6 - 13 = 7` → **128 texels per cell, repeating every 2 cells.**

The retail bitmap loader rolls only material **0 or 1**, so shipped zones use frames 0–1 of the theater bank. Frame 0 is the tiling base; frames 1–12 are variants with roads, pads, and building footprints.

**World scale:** `Hud_WorldUnitsToMetres` (`00434228`) defines 166.667 world units = 1 metre (recovered from the HUD's distance conversion in `docs/engine/planning.md`). A retail cell at 128 texels is ~0.77 m/texel.

## `grid+0x10c` — the LOD / draw-radius field

This section is the canonical account of `+0x10c`; [`terrain-heightmap.md`](terrain-heightmap.md) and
[`distance-fog-and-sky.md`](distance-fog-and-sky.md) reference it rather than re-deriving it.

One function **writes** the field; four read it:

- `Terrain_SetupVisibleRegion` (`0046ca98`) sets `grid[+0x10c] = DAT_004a0bcc[DAT_004d1fc3]` — a
  per-detail-setting LOD table — then `>>= (cellShift - 14)` when `cellShift > 14`. The engine's
  ported `detailLod = cellShift > 14 ? 10 >> (cellShift - 14) : 10` **is this formula**, with 10
  being the retail default table entry rather than a constant. The engine currently derives it once
  at load; the original re-reads it every frame from the detail setting.
- `Terrain_BuildDrawRegionQuad` (`0046d220`) builds the draw region as a square of radius
  `grid[+0x10c] << cellShift` world units around the viewer, clamped to the grid extent. So the LOD
  field is literally **a terrain draw radius in cells**.
- `maybe_Terrain_SetDistanceBands` (`00428bc0`) turns that same distance into five scaled values via
  a 5-entry table at `DAT_0049abb0` — LOD thresholds or similar, consumer not traced. **Not** the
  distance fog, which is 12-slice and computed per drawn thing.
- `Terrain_DrawCellQuad` (`0046d344`) installs `grid[+0x10c] << grid[+0x108]` per cell as the
  visibility range the distance fade is measured against —
  see [`distance-fog-and-sky.md`](distance-fog-and-sky.md), which tabulates the resulting range per
  cell shift.

`maybe_Terrain_ComputeViewDistance` (`00470910`) reads the same field per frame for the view setup;
its two outputs remain undecoded.

## The diagonal selector — read-only in the render path

`cell[+0xf]`'s low two bits are the diagonal-split selector; bits `[2:7]` are the material index this
document's texture lookup uses. The selector is written once at zone load by `Terrain_BuildCellSurface`
(`0046bed8`), alongside the two face normals it has to choose a diagonal to build — see
[`terrain-heightmap.md`](terrain-heightmap.md), which carries the four-corner rule and the selector
table.

Every reference to `cell[+0xf]` in the render path is a read — `>> 2` for the material index, `& 3`
for the selector, the latter tested in four places in `FUN_0046ff74`, all against `== 0`. Neither
`Terrain_DrawCellQuad` nor `FUN_0046ff74` writes the byte.

## The render path, for whoever picks this up

```
maybe_Sim_RenderFrame (0045fb9c)          ← the frame root
 ├─ Terrain_SetupVisibleRegion (0046ca98)  ← takes ActiveHeightGrid AS A PARAMETER
 │   ├─ Terrain_BuildDrawRegionQuad (0046d220)
 │   └─ maybe_Terrain_SetDistanceBands (00428bc0)
 ├─ maybe_Scene_SubmitFrameObjects (0042841c)
 └─ maybe_Terrain_ComputeViewDistance (00470910), via 0042e700

Terrain_DrawCellQuad (0046d344)            ← per cell
 ├─ 4x corner projection via 00495240 against grid+0xd0
 ├─ Terrain_ResolveCellTexture (0046bcf4)  ← the texture + tiling answer
 └─ 0046865c / 00468078                    ← the two triangles
```

## Engine implementation

Data-driven, theater-indexed via mission. Core components:

- **`World/TheaterDescriptor`** — parses `wld\WORLD<n>.WLD` and exposes terrain bank, theater palette, impact palette names.
- **`World/ScriptDatHeader`** — reads the three header fields above.
- **`Render/TerrainTextureBank`** — loads and packs named `.DBA` via theater `.DPL`; implements `Terrain_ResolveCellTexture`'s rect selection (material index → frame; tiling or whole-frame UV).
- **`Render/TerrainMeshBuilder`** — per-corner UVs from rect; **`Gl/MeshVertex.Textured`** flag allows cells that fail texture lookup to keep height/slope ramp colour.

Known constraints: corner-to-quad assignment not resolved from disassembly (monotone choice flagged at call site); shelf-packed atlas uses 4 MB/theater.
