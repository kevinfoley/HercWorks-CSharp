# Terrain texturing (DBSIM.EXE) — SOLVED 2026-08-13

Ghidra session opened to answer three questions before implementing terrain texturing in the
HERCULAN Engine:

1. Which texture resource does a cell's material index select, and how is it tiled across a cell?
2. Who consumes the `HeightGrid` LOD field (`+0x10c`)?
3. What writes the diagonal-selector's bit 1?

**Questions 1 and 2 are answered.** Question 3 is still open. Read alongside
`dts-texture-binding.md` (the mech-side chain) — the two share a data structure.

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

**The terrain bank is named in data, not in code.** `Terrain_BindTextureBank` takes a bare base
name, joins it with the `dba` folder, and stores the loaded bank's frame-descriptor table. It is
called from `maybe_World_LoadTheater` with a null-terminated string read field-by-field out of the
`world<N>` descriptor file. That is why neither DBSIM.EXE nor VSHELL.EXE contains the strings
`ice` / `bsnow` / `volcan` / `moon` despite those `.DBA` files shipping — a fact that badly misled
the first pass of this investigation.

**The descriptor table is the same structure the mech path uses** — 20-byte stride, first 16 bytes
being the four `int32` UV-rect corners `F0..F3`, documented in `dts-texture-binding.md`'s
"UV-generation formula — FOUND". Terrain and mechs share one texturing substrate.

### The `world<N>` descriptor — layout decoded 2026-08-13

`wld\WORLD0.WLD` … `WORLD9.WLD`, 310–313 bytes each. `maybe_World_LoadTheater` reads them
field-by-field off a stream, in this order:

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
| 5 NUL-terminated strings | see below |

**Reading it this way consumes every one of the ten retail files to its exact last byte** — the
structural check that matters, since a wrong field width anywhere would leave the strings misaligned
or overrun.

The five strings, in file order, are `world24`, `clouds2`, `impact<N>`, **the terrain bank**, `tex`.
The original reads the first three into one scratch buffer (keeping only the third, an impact
palette, as it goes), then reads the fourth into that same buffer — and it is that buffer the
function hands to `Terrain_BindTextureBank` at the end. Two independent checks confirm the ordering
rather than trusting the decompiler's stack-slot reuse: string 4 takes exactly five values across the
ten files, and all five exist as `.DBA`s; string 3 is `impact0`..`impact9`, and all ten exist as
`.DPL`s in the folder the original loads it from.

| descriptor | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| bank | urban | urban | bsnow | bsnow | volcan | volcan | ice | ice | moon | moon |

So there are **five theaters with two variants each**, matching the original's own
`world<theaterIndex * 2 + variant>` name construction. Every retail `script.dat` carries variant 0,
so what the odd-numbered descriptors are for (weather? time of day?) is not established.

### Which theater, and which zone: `script.dat`'s header

Both come from the mission handoff, and both are now decoded. `DBSim_LoadScriptDat` reads
`data\script.dat`'s 20-byte header into one global and uses it twice: it passes the whole thing to
`maybe_World_LoadTheater` (which takes the `int16` at 0 as the theater and the `int16` at 18 as the
variant) and the `int16` at **offset 2 straight to `Terrain_LoadZone`**. Checked against the ten real
files in the retail install: every offset-2 value (555, 123, 22, 234, 3333) is a zone that ships, and
every offset-0 value is 0, 1 or 2. This closes `script-dat.md`'s open question about that field,
which it had guessed might be a mission id or a checksum.

The theater's palette is its own name: `maybe_World_LoadTheater`'s first act is to load
`dpl\world<N>.dpl`, which is what the retail `WORLD0.DPL`–`WORLD9.DPL` family is — one active palette
per theater, for everything the theater draws, mechs included.

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

`MAT0.DAT` holds 13 records: `{0,6}`, `{1,6}`, `{2,5}`, … — field 0 ascending (frame index), field 1
the per-material tiling shift. At `cellShift = 14`, materials 0 and 1 give `shift = 14 + 6 - 13 = 7`
→ **128 texels per cell, the 256-texel texture repeating every 2 cells.**

The retail bitmap loader only ever rolls material **0 or 1**, so shipped zones use frames 0 and 1 of
the theater bank. That fits the bank contents exactly: rendering `ICE.DBA` shows frame 0 as plain
tiling ground and frames 1–12 as the same tileable base with roads, pads and building footprints
painted over — i.e. a tiling base plus detail variants, which is what the higher material indices
(reachable through the ASCII loader, and possibly through base placement) would select. Different
materials carry different `BlockShift` values, so variants tile at different scales.

### One number to check against the game — SETTLED 2026-08-13

At the time this was written the scale constant was an estimate (200 world units/metre), making a
cell 81.9 m and a texel **0.64 m**, finer than the "several metres per texel" a gameplay screenshot
suggested. Two explanations were on the table: screen pixels rather than texels, or a wrong scale
constant.

**The scale constant was indeed wrong — and correcting it moves the number the other way.** DBSIM
states its own scale in `Hud_WorldUnitsToMetres` (`00434228`): `metres = (worldUnits / 1000) * 6`,
i.e. **166.667 units per metre**, not 200 (see `docs/engine/planning.md`, "World scale —
recovered"). A retail cell is therefore 98.3 m, and 128 texels across it is **0.77 m per texel** —
*finer* than the old figure, not coarser. That eliminates the second explanation and leaves the
first: the blockiness is 320x200 screen pixels. The formula was never in doubt; the metres-per-texel
figure it inherits is now on a recovered constant rather than an estimated one.

## Question 2 — ANSWERED: the LOD field

Two functions, and one of them **writes** the field:

- `Terrain_SetupVisibleRegion` (`0046ca98`) sets `grid[+0x10c] = DAT_004a0bcc[DAT_004d1fc3]` — a
  per-detail-setting LOD table — then `>>= (cellShift - 14)` when `cellShift > 14`. The engine's
  ported `detailLod = cellShift > 14 ? 10 >> (cellShift - 14) : 10` **is this formula**, with 10
  being the retail default table entry rather than a constant. The engine currently derives it once
  at load; the original re-reads it every frame from the detail setting.
- `Terrain_BuildDrawRegionQuad` (`0046d220`) builds the draw region as a square of radius
  `grid[+0x10c] << cellShift` world units around the viewer, clamped to the grid extent. So the LOD
  field is literally **a terrain draw radius in cells**.
- `maybe_Terrain_SetDistanceBands` (`00428bc0`) turns that same distance into five scaled values via
  a 5-entry table at `DAT_0049abb0` — the shape of fog/haze bands or LOD thresholds, consumer not
  traced.

`maybe_Terrain_ComputeViewDistance` (`00470910`) reads the same field per frame for the view setup;
its two outputs remain undecoded.

## Question 3 — still open, but the renderer is now ruled out

No writer of the diagonal-selector's bit 1 was found. `FUN_0046ff74` *reads* `cell[+0xf] & 3` in four
places, all testing `== 0`. `Terrain_DrawCellQuad` splits each quad into two triangles but the
selector's role in that split was not traced.

**The standing theory that the terrain renderer sets the bit is now disproved** (2026-08-13):
decompiling both `Terrain_DrawCellQuad` and `FUN_0046ff74` — the pair the frame path reaches once the
render path above was located — shows every reference to `cell[+0xf]` in them is a read
(`>> 2` for the material index, `& 3` for the selector). Neither writes the byte. So the writer, if
one exists in DBSIM at all, is somewhere else again; the remaining candidates are whatever else can
touch a live grid (base/structure placement is the obvious one, since it is the other thing known to
be stamped onto terrain).

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

**Why the first pass missed all of this:** it enumerated functions that read the `ActiveHeightGrid`
*global* (`004a0bf8`, 56 references). Every function above takes the grid **as a parameter**, so
none of them referenced the global and none appeared in that list. Searching by global reference
cannot find a subsystem whose entry point is called with a pointer.

## Process notes worth keeping

**An earlier draft of this document concluded terrain was probably not texture-mapped at all**, and
proposed palette/ramp shading instead. A gameplay screenshot disproved it immediately, and the
disassembly above now settles it outright. The failure was reasoning from file bytes and
disassembly to a conclusion about *what the renderer draws* without checking it against visible game
output — the second time in this project (see `dts-texture-binding.md`'s flat-average-colour
episode). Screenshots are cheap and are ground truth.

Two specific inference errors from that draft, both worth recognising as patterns:

- *"Neither exe names the terrain banks, so they must not be terrain textures."* The names come from
  a data file. Absence of a string in an executable says nothing when the engine is data-driven.
- *"13 frames vs 13 `mat0` records is probably coincidence."* It is not a coincidence: `mat0` field 0
  indexes the frames directly. The correspondence was the answer, dismissed because it arrived
  before the mechanism did.

Also retracted from that draft: a claimed `DTSModelTransformer` crash on debris/effect models. The
audit probe had two different parsers in one try block and blamed the wrong one; no such bug exists.
**One try block per claim.**

## Status for the engine — IMPLEMENTED 2026-08-13

The whole chain is live and data-driven, with nothing hardcoded but the theater index (which comes
from a mission, and the engine does not load missions yet):

- **`World/TheaterDescriptor`** parses `wld\WORLD<n>.WLD` per the layout above and exposes the
  terrain bank name, the theater palette name and the impact palette name.
- **`World/ScriptDatHeader`** reads the three header fields decoded above, so a host with a real
  `script.dat` gets its zone and theater from the mission rather than from arguments.
- **`Render/TerrainTextureBank`** loads and packs the named `.DBA` through the theater's own
  `.DPL` and implements `Terrain_ResolveCellTexture`'s rect: material index from the cell, frame from
  `mat0[material].Index`, and either the whole frame (`BlockShift == 0`) or the tiled sub-rect.
- **`Render/TerrainMeshBuilder`** emits per-corner UVs from that rect; **`Gl/MeshVertex`** gained a
  per-vertex `Textured` flag so a cell that fails to resolve keeps the old height/slope ramp colour
  while its neighbours sample the atlas.

**Verified headlessly against the real install.** All ten descriptors parse to their exact length and
name a bank that exists; all five banks decode and pack (13 frames each, 256x256, into a 1024x2048
atlas); building zone 504 against each of the five theaters textures 100% of terrain vertices with
UVs inside the atlas. The per-cell rects are right by inspection at the documented values: with
`CellShift` 14 and `BlockShift` 6 the shift is 7, so a cell spans 128 of the 256 texels and cell
(2, 0) gets the same rect as cell (0, 0) — the texture repeating every two cells, exactly as the
formula says. Zone 504 rolls only materials 0 and 1, matching the retail loader's known behaviour.
Both decoded banks were rendered and eyeballed: `urban`'s frame 0 is plain tiling dirt and its later
frames are that same base with roads, building pads and craters painted over, and `ice` is the same
structure in snow — which is what this document predicted from the frame contents before any of it
was drawn.

**Two things deliberately left as-is.** The corner-to-quad assignment (which of the rect's corners
attaches to which corner of the cell) is not stated by the disassembly read here, so the monotone
choice is used and flagged at the call site — on a tiling base texture a wrong choice mirrors or
rotates the ground rather than tearing it. And the shelf packer leaves a 1024x2048 atlas half empty
for 13 same-sized frames, because one pixel of padding pushes four 256-wide frames past a 1024-wide
shelf; it costs 4 MB of VRAM per theater and nothing else.
