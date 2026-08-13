# DTS/DBA texture binding (VSHELL.EXE, confirmed 2026-08-11, corrected same day)

## Summary

**The `.DTS` file format carries no reference to any specific texture file.** Which `.DBA` gets bound
to a given model is a runtime/application-level decision (see "DBA filename selection" below), never
recorded in the `.DTS` bytes. **But `TSBitmapPart` and `TSTexture4Poly` — the file format's two
texture-bearing poly types — do NOT share one texture-rendering mechanism.** An earlier version of
this doc claimed they did (based only on decompiling `TSBitmapPart`'s render method and assuming
`TSTexture4Poly` worked the same way from a doc-comment description alone, without ever decompiling
`TSTexture4Poly`'s own code) and was wrong — corrected the same day after the user pointed out that a
flat-per-triangle-average-color implementation built on that assumption didn't match a real gameplay
screenshot's visibly detailed, per-surface texture. `TSTexture4Poly` in fact drives a genuine
scanline/perspective-correct textured-polygon software rasterizer, confirmed via direct disassembly —
see "TSTexture4Poly's real mechanism" below, which supersedes anything this doc said about it
previously.

All findings are from Ghidra 12.1.2 headless passes on `VSHELL.EXE` (project
`E:\ES2Stuff\tools\ghidra_project\ES2Recon`, already `-cspec windows` + `ES2CommitAllParams`-cleaned
per `project_es2_exe_recon` memory).

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

## TSTexture4Poly's front/back stride question — SETTLED (2026-08-11 follow-up)

The previous version of this doc left one thing unresolved: whether `ColorIndexId` is pre-scaled
before `TSTexture4Poly_Render` uses it, so that `+2` int32 slots lands on the *same* surface's back
color rather than a different surface entirely. This is now settled with two independent sources:

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

New symbols from this pass: `TSGroup_RenderPolys`/`TSGroup_RenderPolysFromRef` (`0042349d`/`00423709`
— confirmed, via field-order cross-check against `DTSModelTransformer.cs`'s `ReadTSGroup`, to be a
`TSGroup`-like render-time object's own render-setup wrappers, not just "some context object") and
`g_ActiveSurfaceRecords` (`005d88a2`). See `known_symbols.json` for full descriptions.

**A same-day "bonus finding" in the previous version of this section was WRONG and is retracted —
see "TSTexture4Poly's UV-generation formula — FOUND" below.** It claimed `DAT_00471890 != 0` was "the
normal case for a bound DBA" and that the resolved front/back value always became a literal
`activeDba.Frames[]` index. Decompiling the full `TSTexture4Poly_Render` body (not just the front/back
snippet) shows that flag actually picks between two **entirely different code paths**, and the
`activeDba.Frames[]` lookup only happens in the branch that does *not* reach the real textured
rasterizer.

## TSTexture4Poly's UV-generation formula — FOUND (2026-08-11 second follow-up)

Decompiling `TSTexture4Poly_Render`'s **full body** (not just the front/back snippet examined for the
stride question) turned up something the earlier pass missed entirely: the flag gating the two
branches after front/back resolution is not "which lookup style" — it's "which renderer runs at all."

- **`DAT_00471890 == 0` (renamed `g_UseFlatPolyFallback` — name describes the *other* branch; this is
  the one that's actually interesting): the real path.** Builds a per-vertex 3D position array from
  the group's own points, builds a 4-entry UV-corner array, and calls
  `TSTexture4Poly_RasterizeA`/`RasterizeB`. **This is the only branch that ever reaches the rasterizer
  — confirmed by reading the full decompiled function, not assumed.**
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
`F0,F1,F2,F1,F2,F3,F0,F3`). This is a standard axis-aligned rectangle-corner mapping — (left,top),
(right,top), (right,bottom), (left,bottom) — **if** `F0`/`F1` are that frame's own left/top and
`F2`/`F3` its right/bottom. That assumption was not independently verified this session (the function
that *builds* the descriptor table, populating `g_ActiveBitmapArray[1]`, was not traced — bounded
follow-up below), but it's the natural reading and it happens to exactly match — in topology, vertex
for vertex — the UV-corner order `DtsGeometryBuilder.cs` already used
((0,0)/(1,0)/(1,1)/(0,1) for vertices 0-3), which was previously only a labeled guess. That guess is
now RE-confirmed correct in *order*, under the (reasonable, but not exe-confirmed) assumption that
each DBA frame is its own independently-cropped image rather than a shared atlas with a nonzero
top-left offset — which matches how `HercWorks.Core`'s existing `DynamixBitmap` parsing already treats
DBA frames (each with its own `Cols`/`Rows`, not atlas sub-rects).

**Not traced this session (bounded follow-up, not chased further per this project's "ship something
visible, don't over-RE" lesson):** the function that populates `g_ActiveBitmapArray[1]`'s descriptor
table. Finding it would settle whether `F0/F1` are always `(0,0)` (confirming the no-atlas assumption
with certainty) or can be nonzero (meaning some DBAs really are shared atlases, which the current C#
implementation doesn't yet handle). Also still undecoded: `TSTexture4Poly_RasterizeA`/`RasterizeB`'s
own internal fixed-point interpolation math and the exact semantics of their 4th interpolant — not
needed to know *which* UV a vertex gets (now answered), only to bit-exactly reproduce the rasterizer's
own per-pixel fill algorithm.

## TSTexture4Poly's real mechanism (the part this doc got wrong before)

`TSTexture4Poly` is a *mesh* poly (lives in a `TSGroup`, references real 3D vertices via
`VertexList`/`VertexCount`, same as `TSSolidPoly`) — structurally nothing like `TSBitmapPart`'s
fixed 2D quad. It has **its own distinct vtable**, found by locating its **real constructor** rather
than trusting the (dead, zero-reference) RTTI type-name string:

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
   4th interpolated value across spans, and per-pixel inner-loop draw calls. This is real per-pixel
   (or at minimum true per-span, not per-triangle-flat) texture mapping, confirmed by direct
   disassembly of the actual draw path, not inferred from a doc comment.

**Net result: `TSTexture4Poly` texturing is real, not flat-shaded — the earlier "DTS polys carry no
UV data so only a flat average color is possible" conclusion in this doc was wrong.** The UV mapping
is procedural (derived from the bound bitmap's own dimensions at render time), not stored per-vertex,
but the actual fill is a proper textured-polygon rasterizer.

**Not fully decoded this session:** the exact UV-corner-generation formula and the precise semantics
of `TSTexture4Poly_RasterizeA`/`RasterizeB`'s 4th interpolant (lighting? a second UV axis? both, via
the two variants?) — see "Open follow-ups."

## DBA filename selection (unchanged from before — still an application-level decision)

`ES2FindStringRefs` on keywords `.dts`/`.dba`/`texture`/`bitmap` turned up an explicit, literal table
of per-Herc DBA filenames used by **VSHELL's own 2D Herc-display widgets specifically** (an
`ESGrid`-based gadget, confirmed via `ESGrid_SetPart`'s/`FUN_0040b8cf`'s `"ESGrid::setPart out of
range"` assert) — `dba\rpr_<code>.dba`/`<code>_int.dba` for a small icon-list context, and
`dba\<code>_bod.dba`/`_wep.dba`/`_out.dba` for a full-portrait context, both keyed by the known
9-mech-code order.

**This table turned out to be the wrong file family for the sim's own in-game mech-body rendering.**
Per user domain knowledge (2026-08-11, not derivable from the files themselves, and correcting a
second wrong guess this doc made from a same-folder `_FILE TYPE NOTES.txt` claiming `APOCATEX.DBA`
covered "most or all human hercs"): the real in-game mech-body texture source is a small, partially
irregular set of shared atlases in `simvol0/dba/`:
- `LIGHT.DBA`, `MEDIUM.DBA`, `HEAVY.DBA` — reusable atlases keyed by weight class; most mechs of a
  given weight class share one, referencing different frames of it per part via their own
  `ColorIndexId`→surface-record→frame chain (see above).
- `ENEMY.DBA` — a separate atlas/variant for enemy-faction mechs.
- `NEWHERCS.DBA` — used by "certain mechs" instead (which ones, not yet determined).
- `APOCATEX.DBA`/`RAZORTEX.DBA` — the Apocalypse and Razor each have their own dedicated atlas
  rather than sharing a weight-class one.

Same-basename DBAs like `SAMSON.DBA`/`OUTLAW.DBA` are a red herring — 2D UI graphics used in damage
readouts, not 3D mesh textures.

**Scope caveat, unresolved:** none of the mechanism-tracing above (registry table, real constructor,
real vtable, real rasterizer) was independently re-derived inside **DBSIM.EXE** (the actual 3D
combat/cockpit renderer that produced the reference screenshot) — it was all found in VSHELL.EXE,
which the user confirmed does not display 3D combat but does (per this session's findings) contain
real 3D poly rendering of its own for Herc-display widgets, using the same shared ThreeSpace engine
object code (confirmed via identical `TSShapeInstance`/`TSTexture4Poly`/`TSBitmapPart`/
`GLBitmapArray` RTTI class-name strings existing in both binaries). DBSIM is a stripped release build
(debug assert strings compiled out), so the string-anchored techniques that worked in VSHELL don't
directly transfer — but the registry-table technique (which doesn't depend on strings at all, just
matching known `TSObjectHeader` tag values against a data pattern) should.

## DBSIM.EXE's own mech-to-texture mechanism — CONFIRMED (2026-08-12 follow-up)

The scope caveat above (mechanism only confirmed in VSHELL, not independently re-derived in
DBSIM) is now closed for the specific question that actually matters for the 3D viewer: **which
`.DBA` DBSIM binds to a given mech at spawn time is no longer a guess — it's a literal field in
game data, confirmed via Ghidra RE of DBSIM.EXE directly.**

Investigation started from a user-supplied Ghidra address hint (interpreted as a hex file-offset
range, `0x99436`-`0x99469`, into `DBSIM.EXE`) which landed on a genuine data table:
`g_MechTextureGroupNames` (`0049a360`) — 7 pointers to the literal strings `"light"`/`"medium"`/
`"heavy"`/`"enemy"`/`"apocatex"`/`"razortex"`/`"newhercs"`, an exact byte-for-byte match of the
7-name set this doc's "DBA filename selection" section above already had from VSHELL research +
user domain knowledge. **This particular table turned out to be dead data** — zero code references
anywhere in DBSIM.EXE (checked both via `ReferenceManager` and a full-binary decompiled-text scan)
— but it was a strong enough landmark to lead to the real mechanism nearby:

- **`MechType_InitOne`** (`004201a8`, renamed from `FUN_004201a8`) — DBSIM's real per-mech-type
  init function, called once per valid mech-type slot by `Mechs_InitAllTypes` (`0042049c`). It reads
  a 0xd8-byte record from `simvol0/dat/<MECHNAME>.DAT` into the mech type's `MECH_TYPE_DATA` entry
  (0x21e bytes each), and — the actual answer — for every mesh sub-component of that mech type, sets
  `TSShapeInstance+0x26` (the *same* bound-active-DBA field confirmed in VSHELL's RE above) to
  `&g_MechTextureGroupSlots + typeRecord[0x96]*8`.
- **`g_MechTextureGroupSlots`** (`004a9df6`) — an 8-byte-stride array, one runtime slot per texture
  group. The loader that lazily fills each slot with a real parsed `.DBA` (presumably by building
  `"simvol0\dba\<name>.dba"` from `g_MechTextureGroupNames`' *live* twin, if one exists elsewhere, or
  by some other still-unfound path) was not traced — not needed for this session's actual goal.
- **`typeRecord+0x96` is the group-index field**, and it's not exe-internal: it's read straight from
  the per-mech file at **record offset 0x96 = file content offset 148**. Cross-checked against
  `HercWorks.Core`'s own `HercSimDat` class (`Data/File/Dat/Sim/HercSimDat.cs`, ported from the Java
  source but never wired to any texture logic) — offset 148 is exactly `HercSimDat.ModelSkinId`,
  which the *original Java port's author* had already named correctly (evidently from context/naming
  convention) without any of this RE trail existing to explain *why*.

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

This resolves the "**DBA filename selection**" section's two open questions from user domain
knowledge above: `NEWHERCS.DBA`'s "certain mechs" are OGRE/MAVERICK/RAPTOR2, and APOCATEX/RAZORTEX
are confirmed exactly 1:1 with APOCA/RAZOR as stated (no other mechs use them).

New `known_symbols.json` entries (2 renamed functions, 1 renamed + 1 unrelated-but-adjacent labeled
data symbol, applied and spot-check-verified): `MechType_InitOne`, `Mechs_InitAllTypes`,
`g_MechTextureGroupNames`, `g_MechTextureGroupSlots`, plus `maybe_`-prefixed medium-confidence
`MechTypeSlot_GetIndex`/`MechTypeSlot_Allocate`.

**Implemented same session:** `Model3DViewerForm.TryLoadDefaultTextureBank()` — when a `.dts` is
loaded from a VOL, looks for a same-basename `dat\<mech>.DAT` in that VOL, parses it via the
now-used `HercSimDataTransformer`, reads `ModelSkinId`, maps it to a group name via the new
`HercSimDat.TextureGroupDbaBaseName`, and auto-loads that group's `.DBA` as the texture bank —
mirroring the existing `TryLoadDefaultPalette` (WORLD0) convention: silent best-effort, only runs
when no bank is already loaded, "Load Texture Bank" remains available as a manual override. The
manual texture-bank/palette selectors in the UI are unchanged (still there for loose files, files
opened outside a VOL, or overriding the auto pick).

**Not pursued further this session** (bounded, not needed for the viewer): the loader that actually
populates `g_MechTextureGroupSlots[i]` with a parsed DBA the first time group `i` is needed — since
the group→filename mapping is now known from data (not from tracing that loader), reproducing its
exact lazy-load mechanics wasn't necessary to implement auto-selection in the C# viewer.

## HERCULAN Engine implementation — shipped 2026-08-13, with a fleet-wide audit

The engine (not the WinForms viewer) now renders textured mechs on the GPU, using this document's
chain end to end: `HercSimDat.ModelSkinId` → bank name → `.DBA` → `Surfaces[ColorIndexId / 4]
.FrontColor` → frame → the RE-confirmed corner order. `Herculan.Engine.Render.TextureAtlas` decodes
and packs a whole bank, `Gl.GpuTexture` uploads it, `DtsMeshBuilder` emits UVs, `Scene.ZoneScene`
picks the bank from the mech's own `.DAT`.

**Two engine-side departures from the original, both deliberate and both documented in code:**

- **Frames are packed into one atlas.** The original ships no atlas — a `.DBA` is an array of
  independently-sized frames, and a software rasterizer pays nothing to switch between them. A GPU
  pays per bind, so packing lets a mech draw in one call. Packing is a pure relocation, so the
  confirmed corner order is unaffected. Retail banks are small: the largest of the seven is 53
  frames into a 256x512 atlas.
- **Nearest-neighbour sampling, no mipmaps.** A fidelity call, not a shortcut — the original point
  samples, so filtering would look softer than the game ever did. Filtering belongs in the opt-in
  bucket per the planning doc's "vanilla by default" principle.

**Fleet-wide audit (every `dts\*.DTS` with a matching `dat\*.DAT`, 22 mechs).** Ran to check whether
the frame-index chain actually resolves on real data rather than just on SAMSON:

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

**A parser bug reported by this audit was MISATTRIBUTED — retracted.** The audit originally reported
that `DTSModelTransformer` throws `IndexOutOfRangeException` on every `*_DEB.DTS` (debris) model and
on `BULLETS`/`ROCKETS`/`FIRE`/`SKIMMER`. A follow-up investigation found no such failure: all 55
retail `.DTS` files parse byte-complete and no unhandled subtype exists.

The cause was a flaw in the audit probe, not in the reader: it called `HercSimDataTransformer` on
`dat\<name>.DAT` and `DTSModelTransformer` on `dts\<name>.DTS` **inside a single try block**, then
reported any exception as a DTS parse failure. Debris and effect `.DAT` files are not mech sim data,
so the thrower was almost certainly the *sim-data* parser being handed a file it was never meant to
read. **Lesson: one try block per claim.** A probe that cannot say which of two operations threw
cannot support a statement about either, and this one sent a separate investigation after a
non-existent bug.

## How to apply

**Implemented 2026-08-11 (follow-up session):**

1. `TSTexture4Poly` (4-vertex quads only) now resolves to a real decoded DBA frame
   (`group.Surfaces[ColorIndexId/4].FrontColor`, see the settled section above) and renders
   perspective-correct UV-mapped in `Model3DViewerControl`'s rasterizer, once a texture bank is
   loaded via `Model3DViewerForm`'s "Load Texture Bank (.DBA)"/"Load Palette (.DPL)" (now wired to a
   real rebuild, not just status-bar info). The vertex-to-UV-corner *order* is now RE-confirmed (see
   "UV-generation formula — FOUND" above) rather than a guess; the one remaining unconfirmed
   assumption is that DBA frames are never shared-atlas sub-rects (not traceable without finding the
   descriptor-table builder — see open follow-ups). Without a bank loaded, `TSTexture4Poly` still
   falls back to the original flat placeholder color.
2. `TSBitmapPart` is still **not implemented** (geometry-building is skipped entirely, same as
   before) — its own mechanism is fully confirmed and simple (`activeDba.Frames[BmpTag]`, billboard
   quad at `OfsX`/`OfsY`), but a camera-facing billboard needs per-frame geometry generation, a
   bigger architecture change than this pass tackled (`DtsGeometryBuilder` builds a static triangle
   list once per model load, not per rendered frame).
3. The real front/back visibility test (choosing `BackColor` instead of `FrontColor` depending on
   view angle) is **not implemented** — `Model3DViewerControl` never backface-culls, so `FrontColor`
   is used unconditionally regardless of viewing side.
4. ~~Picking which `.DBA` to bind for a given mech~~ — **automated 2026-08-12**, see "DBSIM.EXE's own
   mech-to-texture mechanism — CONFIRMED" above. `Model3DViewerForm` auto-selects the right `.DBA`
   from a same-basename `dat\<mech>.DAT`'s `ModelSkinId` field when loading from a VOL; "Load Texture
   Bank" remains available to override or for loose files with no matching `.DAT`.

**Open follow-ups:**
- ~~Front/back stride (`ColorIndexId` pre-scaling)~~ — settled, see the dedicated section above.
- ~~UV-corner-generation formula (which vertex gets which corner)~~ — found, see "UV-generation
  formula — FOUND" above.
- Trace the function that populates `g_ActiveBitmapArray[1]`'s 20-byte-stride descriptor table, to
  confirm `F0/F1` (that frame's UV top-left) are always `(0,0)` — i.e. that DBA frames are never
  shared-atlas sub-rects with a nonzero offset. Not chased this session; the current C# renderer
  assumes `(0,0)`, matching how `HercWorks.Core` already parses each DBA frame as independently
  sized/cropped.
- `TSTexture4Poly_RasterizeA`/`RasterizeB`'s own internal fixed-point interpolation math and 4th
  interpolant semantics are still undecoded — not needed to know which UV a vertex gets (now
  answered), only to bit-exactly reproduce the rasterizer's per-pixel fill algorithm itself.
- Confirm the same registry-table/constructor/vtable/rasterizer chain exists in DBSIM.EXE (expected,
  given the shared RTTI strings, but not independently re-derived) — use the registry-table
  technique above, which doesn't need debug strings.
- ~~Pin down the full mech→DBA lookup table~~ — **found, see "DBSIM.EXE's own mech-to-texture
  mechanism — CONFIRMED" above.** Turned out to be `HercSimDat.ModelSkinId` (`simvol0/dat/<mech>.DAT`
  offset 148), not `HercInfEntry.Weight` as guessed here — a full per-mech table, not just the
  weight-class case.
- The per-poly static override table found incidentally in VSHELL's `FUN_004140a9`/`FUN_00414e5b`
  (`DAT_00484d28`, stride `0x1a`) is VSHELL's own paint-job/customization overlay data, unrelated to
  base texture rendering — left alone.
- `.DBA`'s own on-disk frame layout is assumed already covered by existing `Dyn`/`DynamixBitmap`
  parsing in `HercWorks.Core` — not re-derived here, only the binding/rendering side. The 20-byte
  runtime bitmap-descriptor struct `TSTexture4Poly_Render` reads is now partially decoded: 4 leading
  `int32` fields `F0,F1,F2,F3` (bytes 0-15, the UV-rect corners — see "UV-generation formula" above)
  plus a 5th field `F4` at byte 16 (passed to the rasterizer as a pixel-data pointer/handle, not yet
  independently confirmed). It's a *runtime* structure built from the parsed DBA by an as-yet-untraced
  builder function, not necessarily the on-disk layout — the builder is the concrete next step if
  this is resumed (see the descriptor-table follow-up above).
