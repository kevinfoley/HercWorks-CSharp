# DTS/DBA texture binding (VSHELL.EXE)

**The `.DTS` file format carries no reference to any specific texture file.** Which `.DBA` gets bound
to a given model is a runtime/application-level decision (see "DBA filename selection" below), never
recorded in the `.DTS` bytes. **`TSBitmapPart` and `TSTexture4Poly` — the file format's two
texture-bearing poly types — have distinct rendering mechanisms.** `TSBitmapPart` is a fixed 2D billboard quad
(see "TSBitmapPart's mechanism" below); `TSTexture4Poly` is a real scanline/perspective-correct textured-polygon
rasterizer (see "TSTexture4Poly's real mechanism" below).

All findings are from Ghidra 12.1.2 disassembly of `VSHELL.EXE` (project
`E:\ES2Stuff\tools\ghidra_project\ES2Recon`).

## TSBitmapPart's mechanism (confirmed, simple)

`TSBitmapPart` really does work exactly as originally described — a plain frame-index lookup into
whichever DBA is currently "active," rendered as a fixed 2D billboard quad:

1. **`TSShapeInstance` carries its own bound DBA pointer as a struct field, offset `+0x26`.**
   Confirmed by `TSShapeInstance_GetBoundBitmapArray` (`FUN_0046296d`):
   `return *(undefined4 *)(param_1 + 0x26);`.
2. **A process-wide global, `g_ActiveBitmapArray` (`DAT_005d8010`), holds "whichever DBA is
   currently active."** Only two accessors in the whole binary: `TSBase_GetActiveBitmapArray`/
   `TSBase_SetActiveBitmapArray` (confirmed via `ES2FindAddressRefs` on the global's address).
3. **`TSShapeInstance_Render`/`TSShapeInstance_RenderFromRef`** (`FUN_00462730`/`FUN_00462894`) do a
   save/swap/restore around the poly walk: save the current global, swap in this instance's own
   `+0x26` DBA, call `TSShapeInstance_RenderPolys` (`FUN_0042203a`, which iterates the shape's poly
   list and calls each poly's own vtable slot `+0x1c`), then restore the previous global.
4. **`TSBitmapPart_Render`** (`FUN_00421db2` — the function holding the `"TSBase::getBitmapPtr() :
   index out of range"` assert) reads `poly+0x10` (== `TSBitmapPart.BmpTag`) as a plain frame index,
   bounds-checks it against `g_ActiveBitmapArray`'s count, and looks up `array[frameIndex]` — a
   direct pointer-table indexed lookup (`*(int*)(*g_ActiveBitmapArray+8) + frameIndex*4`). If found,
   draws a fixed quad at the poly's position using `OfsX`/`OfsY` (`poly+0x12`/`+0x13`, single bytes —
   exact match for `TSBitmapPart.OfsX`/`OfsY`) and the bitmap as its texture.

**This part is genuinely simple: for `TSBitmapPart`, texture resolution really is just
`activeDba.Frames[BmpTag]`, rendered as a billboard quad, no UV interpolation needed.**

## TSTexture4Poly's front/back stride

`ColorIndexId` is pre-scaled by 4 (i.e., by `surfaceIndex * 4`, not a plain surface index). This means
the `+2` int32 offset lands on the same surface's back color rather than a different surface. Confirmed
by two independent sources:

1. **Fresh raw disassembly** of `TSTexture4Poly_Render` (`00422af5`, re-dumped via `ES2DumpAsm`, not
   just re-read from the old decompiler output): `MOVZX ESI,word ptr [EBX+0xc]` (ColorIndexId, as
   unsigned short) → `SHL ESI,0x2` (×4) → added to `g_ActiveSurfaceRecords` (`DAT_005d88a2`) as a byte
   offset. Front = `*(int32*)(base+offset)`; back = `*(int32*)(base+offset+8)` (exactly 2 int32 slots
   later).
2. **Independent cross-check from the file-format parser**, unrelated to any exe RE:
   `DTSModelTransformer.cs`'s `ReadTSGroup`/`WriteTSGroup` already read/write the on-disk group's
   surface-count field as `colorCount / 4` (read) / `Surfaces.Length * 4` (write) — i.e. the file
   format itself has always encoded this count in 4-int32-slot units, matching `TSSurfaceEntry`'s
   true 4-slot (Front/FrontLine/Back/BackLine) layout. This was sitting in already-shipped, working
   code the whole time and nobody had connected it to the render-side stride question before.

**Both sources agree: `ColorIndexId` on disk is `surfaceIndex * 4`, not a plain surface index.**
`surfaceIndex = ColorIndexId / 4`. Front value = `group.Surfaces[surfaceIndex].FrontColor` (packed
with `FrontFlag`); back value = `group.Surfaces[surfaceIndex].BackColor` (2 slots after Front, matching
the `+2` observed in both the disassembly and the file layout).

New symbols: `TSGroup_RenderPolys`/`TSGroup_RenderPolysFromRef` (`0042349d`/`00423709`) and
`g_ActiveSurfaceRecords` (`005d88a2`). See `known_symbols.json` for full descriptions.

## TSTexture4Poly's UV-generation formula

The flag at `DAT_00471890` controls rendering mode:

- **`DAT_00471890 == 0` (`g_UseFlatPolyFallback`): the textured path.** Builds a per-vertex 3D position array from
  the group's own points, builds a 4-entry UV-corner array, and calls
  `TSTexture4Poly_RasterizeA`/`RasterizeB`. This is the only branch that reaches the rasterizer.
- **`DAT_00471890 != 0`: a flat/solid 2D polygon-fill fallback**, previously misread as "the normal
  textured case." It projects the poly's vertices to screen space and edge-clips them
  (`FUN_0045e694`/`FUN_0045ee2c`, newly decompiled this pass), then fills via `FUN_0045f364` →
  `FUN_0045f8e7`/`FUN_0045f8d2` — **no texture sampling anywhere in this path.** It does still resolve
  a DBA frame index via the same pointer-table lookup `TSBitmapPart_Render` uses
  (`*(int*)(*g_ActiveBitmapArray+8) + frameIndex*4`), which is what the earlier pass saw and
  misattributed as "the" texture-resolution mechanism — but that resolved value is never used to
  sample a pixel in this branch, only for a bounds-check assert.

**The UV-corner formula itself, from the real (`==0`) branch:** the front/back value indexes not
`activeDba.Frames[]` but a **separate 20-byte-stride per-frame descriptor table**, reached via
`g_ActiveBitmapArray[1]` — which turns out to be a *different* field than `*g_ActiveBitmapArray`
(one extra pointer dereference apart; see `g_ActiveBitmapArray`'s corrected `known_symbols.json`
entry). Each descriptor's first 16 bytes are four `int32` fields `F0, F1, F2, F3` (a 5th field `F4` at
byte 16 is passed straight through to the rasterizer as a texture-data pointer/handle). The UV array
built for the poly's 4 vertices is, in order:

```
V0 = (F0, F1)
V1 = (F2, F1)
V2 = (F2, F3)
V3 = (F0, F3)
```

confirmed directly from the decompiled assignment sequence (`_DAT_0048a190` through `_DAT_0048a1ac` =
`F0,F1,F2,F1,F2,F3,F0,F3`). This matches the UV-corner order `DtsGeometryBuilder.cs` uses
((0,0)/(1,0)/(1,1)/(0,1) for vertices 0-3). Assumes each DBA frame is independently-cropped (not a
shared atlas with nonzero offsets), consistent with `HercWorks.Core`'s `DynamixBitmap` parsing.

## TSTexture4Poly's real mechanism

`TSTexture4Poly` is a *mesh* poly (lives in a `TSGroup`, references real 3D vertices via
`VertexList`/`VertexCount`, same as `TSSolidPoly`) — structurally different from `TSBitmapPart`'s
fixed 2D quad. It has **its own distinct vtable**, found by locating its **real constructor:**

1. **`g_TSObjectTypeRegistry`** (`0047f258`, VSHELL) — an 18-entry, 12-byte-stride table of
   `{tag:uint32, constructorFnPtr, nameStringPtr}`, where `tag` matches
   `TSObjectHeader`'s on-disk `[subtype:u16][supertype:u16]` bytes exactly (e.g. `0x0014000f` =
   `TSTexture4Poly`'s `{0x0f,0x00,0x14,0x00}`). This is the actual DTS-tree node factory the shared
   ThreeSpace engine code uses to construct the right C++ class per on-disk chunk tag — found by
   scanning memory near `TSBitmapPart`'s own (successfully-found) vtable slot for a repeating
   pattern, after the RTTI-string approach came up empty (a second confirmed dead end for that
   technique in this binary, see `project_es2_exe_recon`'s `.BND`/`MECH` notes for the first).
2. **`TSTexture4Poly_Construct`** (`FUN_0045ffe0`, the registry's tag-`0x0014000f` entry) — installs
   the object's vtable pointer several times during construction (standard Watcom multi-base-class
   pattern), finishing with **`g_TSTexture4PolyVtable`** (`0047ee0c`) as the actually-used vtable.
3. **`g_TSTexture4PolyVtable`, slot `+0x1c`, is `TSTexture4Poly_Render`** (`FUN_00422af5`) —
   confirmed **different from `TSBitmapPart_Render`**, disproving the earlier "same mechanism"
   claim. Its body:
   - Reads `poly+0xc` (== `TSSolidPoly.ColorIndexId`, inherited by `TSTexture4Poly`) as an index
     into a **per-surface runtime record array** (`DAT_005d88a2`, 4-byte stride) — a different,
     more indirect lookup than `TSBitmapPart`'s direct pointer-table index.
   - Runs a visibility test (`maybe_TSPoly_FrontBackVisibilityTest`, `FUN_0045e480`, medium
     confidence) using `poly+4`/`poly+6` — confirming these are `TSPoly.Normal`/`Center`, and that
     they're **auxiliary vertex indices used to pick front- vs. back-facing color**, not literal
     surface-normal or UV data. This resolves which of a front/back bitmap-record pair to use.
   - Resolves that record to a bitmap descriptor (`DAT_005d8010[1] + idx*0x14` — a 20-byte-stride
     struct array, again distinct from `TSBitmapPart`'s pointer table).
   - **Builds real 3D world-space positions for all of the poly's `VertexCount` vertices** (not
     just 3) from the group's own point/index arrays.
   - Builds texture-space "corners" from the resolved bitmap descriptor's own declared
     width/height fields — **not from any per-vertex file data** (`TSPoly` genuinely carries no
     on-disk UV fields — that part of the earlier analysis was correct — but the engine doesn't
     need them: it derives a UV rectangle procedurally from the bitmap's own dimensions).
   - Calls **`TSTexture4Poly_RasterizeA`/`RasterizeB`** (`FUN_004202dd`/`FUN_00420900`, dispatched
     by a flag) with the vertex positions, UV corners, and bitmap data pointer.
4. **`TSTexture4Poly_RasterizeA`/`RasterizeB` are genuine scanline/perspective-correct textured-fill
   rasterizers** — per-edge polygon clipping, fixed-point interpolation of screen position plus a
   4th interpolated value across spans, and per-pixel inner-loop draw calls.

**Result:** `TSTexture4Poly` uses real textured-polygon rasterization. The UV mapping is procedural
(derived from the bound bitmap's own dimensions at render time), not stored per-vertex.

**Open:** The precise semantics of `TSTexture4Poly_RasterizeA`/`RasterizeB`'s 4th interpolant
(lighting? a second UV axis? both, via the two variants?).

## DBA filename selection (unchanged from before — still an application-level decision)

VSHELL contains texture references for 2D Herc-display widgets in `dba\rpr_<code>.dba`, `dba\<code>_int.dba`,
`dba\<code>_bod.dba`/`_wep.dba`/`_out.dba` — but these are UI graphics, not 3D mesh textures.

The real in-game mech-body texture source (from DBSIM, below) is a set of shared atlases in `simvol0/dba/`:
- `LIGHT.DBA`, `MEDIUM.DBA`, `HEAVY.DBA` — keyed by weight class.
- `ENEMY.DBA` — for enemy-faction mechs.
- `NEWHERCS.DBA` — for certain mechs (see below).
- `APOCATEX.DBA`, `RAZORTEX.DBA` — one each for Apocalypse and Razor.

## DBSIM.EXE's mech-to-texture mechanism

Which `.DBA` DBSIM binds to a given mech at spawn time is encoded in game data (not a guess).
From DBSIM.EXE RE:

- **`MechType_InitOne`** (`004201a8`) — DBSIM's per-mech-type init function. For every mesh sub-component,
  it sets `TSShapeInstance+0x26` (the bound-active-DBA field) to `&g_MechTextureGroupSlots + typeRecord[0x96]*8`.
- **`g_MechTextureGroupSlots`** (`004a9df6`) — an 8-byte-stride array, one runtime slot per texture group.
- **`typeRecord+0x96` is the group-index field**, read from the per-mech file at **record offset 148**.
  This is exactly `HercSimDat.ModelSkinId` in `HercWorks.Core`'s `HercSimDat` class.

**Byte-verified against every real `simvol0/dat/*.DAT` file** (all 226 bytes: 9-byte VOL prefix +
216-byte content + 1 trailer, matching `MechType_InitOne`'s 0xd8=216-byte read exactly):

| ModelSkinId | Group name | Confirmed mechs |
|---|---|---|
| 0 | light | OUTLAW |
| 1 | medium | TOMAHAWK |
| 2 | heavy | SAMSON, COLOSSUS |
| 3 | enemy | DIABLO, CERBERUS, HYPERION, MIRIMAC, MONGOOSE, HEADHUNT, PITBULL, ACHILLES, RAMSES, SCARAB, STINGRAY, SPIDER (every enemy-only mech checked) |
| 4 | apocatex | APOCA |
| 5 | razortex | RAZOR |
| 6 | newhercs | OGRE, MAVERICK, RAPTOR2 |

This confirms `NEWHERCS.DBA` is used by OGRE/MAVERICK/RAPTOR2, and APOCATEX/RAZORTEX are 1:1 with APOCA/RAZOR.

**Implemented:** `Model3DViewerForm.TryLoadDefaultTextureBank()` — when a `.dts` is loaded from a VOL,
looks for a same-basename `dat\<mech>.DAT`, reads `ModelSkinId`, maps it to a group name, and auto-loads
that group's `.DBA`. Silent best-effort; "Load Texture Bank" remains available as a manual override.

## HERCULAN Engine implementation

The engine renders textured mechs on the GPU: `HercSimDat.ModelSkinId` → bank name → `.DBA` →
`Surfaces[ColorIndexId / 4].FrontColor` → frame → confirmed corner order. `Herculan.Engine.Render.TextureAtlas`
decodes and packs a whole bank; `Gl.GpuTexture` uploads it; `DtsMeshBuilder` emits UVs;
`Scene.SceneModelLibrary` picks the bank from the mech's `.DAT` and caches one atlas per bank across
every unit in a mission.

**Two engine-side departures from the original, both deliberate and both documented in code:**

- **Frames are packed into one atlas.** The original ships no atlas — a `.DBA` is an array of
  independently-sized frames, and a software rasterizer pays nothing to switch between them. A GPU
  pays per bind, so packing lets a mech draw in one call. Packing is a pure relocation, so the
  confirmed corner order is unaffected. Retail banks are small: the largest of the seven is 53
  frames into a 256x512 atlas.
- **Nearest-neighbour sampling, no mipmaps.** A fidelity call, not a shortcut — the original point
  samples, so filtering would look softer than the game ever did. Filtering belongs in the opt-in
  bucket per the planning doc's "vanilla by default" principle.

**Fleet-wide audit** (every `dts\*.DTS` with a matching `dat\*.DAT`, 22 mechs):

- **21 of 22 mechs: zero unresolved texture polys.** Every `TSTexture4Poly`'s
  `Surfaces[ColorIndexId/4].FrontColor` lands inside its selected bank's frame count. This is the
  strongest evidence yet that both the `/4` stride and the `ModelSkinId`→bank mapping are right —
  a wrong bank or a wrong stride would produce out-of-range indices somewhere across ~2000 polys.
- **TOMAHAWK has exactly 4 anomalous polys**, all identical: `ColorIndexId = 0` into a `Surfaces`
  array of length **1**, with `FrontColor == BackColor == 3084` (`0xC0C`) against a 36-frame bank.
  Front and back agreeing on the same nonsense value, in a one-entry surface table, reads as a
  degenerate group in the source art rather than a decoding error — nothing else in the fleet looks
  like it. They fall back to the placeholder colour.
- **13 `TSTexture4Poly`s across 6 mechs have 3 vertices, not 4** (SAMSON, COLOSSUS, CERBERUS,
  HYPERION, OUTLAW, TOMAHAWK). The engine falls these back to flat shading rather than mapping three
  of the four corners: the exe's decompiled UV builder populates a 4-entry array, and what it does
  for a 3-vertex poly was never traced. Mapping the first three corners is the natural guess and
  would probably look fine — but this document's own history (the flat-average-colour episode) is a
  standing argument against shipping plausible guesses, and 13 polys fleet-wide is not worth the
  risk. Concrete follow-up if anyone wants it: decompile the vertex-count branch of
  `TSTexture4Poly_Render`'s UV setup.

**The coincident-twin rule had to invert, and this is easy to miss.** Real DTS meshes stack a
textured poly exactly on top of a flat-shaded twin (186 such pairs in SAMSON's first root). While
texturing was unimplemented, `DtsMeshBuilder` deliberately kept the *untextured* twin — the only one
that could look right. With texturing live, the textured twin is the one the original draws, so the
preference had to flip; leaving it would have loaded and packed every texture and then hidden it
behind the flat twin. Implemented as a three-way rank rather than a flipped boolean, so a texture
poly that fails to resolve still loses to its flat twin and the no-bank path behaves exactly as
before. Verified by building each mech both ways: identical total triangle counts, textured
triangles 0 → 142 (SAMSON), 0 → 198 (DIABLO), 0 → 232 (APOCA).

**Note:** A prior audit reported parse failures in debris/effect `.DTS` files — this was a false alarm
(all 55 retail `.DTS` files parse byte-complete). The audit probe called two parsers in a single try
block, which cannot distinguish which parser threw.

## Implementation status

- **`TSTexture4Poly` (4-vertex quads only):** Resolves to decoded DBA frame (`group.Surfaces[ColorIndexId/4].FrontColor`)
  and renders perspective-correct UV-mapped in `Model3DViewerControl`'s rasterizer once a texture bank is loaded.
  Without a bank, falls back to flat placeholder color.
- **`TSBitmapPart`:** Not implemented (architecture change needed for per-frame billboard generation).
- **Front/back visibility test:** Not implemented (`Model3DViewerControl` uses `FrontColor` unconditionally).
- **Mech-to-`.DBA` binding:** Automated via `HercSimDat.ModelSkinId` from the mech's `dat\<name>.DAT`.

## Open follow-ups

- Trace the function that populates `g_ActiveBitmapArray[1]`'s 20-byte-stride descriptor table, to
  confirm `F0/F1` (frame UV top-left) are always `(0,0)` or can be nonzero (atlas sub-rects).
- `TSTexture4Poly_RasterizeA`/`RasterizeB`'s internal fixed-point interpolation math and 4th
  interpolant semantics.
- Confirm the registry-table/constructor/vtable/rasterizer chain in DBSIM.EXE (expected, not yet independently verified).
- `.DBA`'s on-disk frame layout (assumed covered by `HercWorks.Core`'s `DynamixBitmap` parsing).
- The 20-byte runtime bitmap-descriptor struct's 5th field `F4` (byte 16) — pixel-data pointer/handle,
  not yet independently confirmed.
