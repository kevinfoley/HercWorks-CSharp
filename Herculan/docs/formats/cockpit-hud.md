# Cockpit canopy art (`.HB0`/`.HB1`/`.HB2`) and HUD compositing

Milestone 8 Phase 0 findings. All disassembly against `DBSIM.EXE` in the `ES2Recon` Ghidra project
unless noted; all pixel/coordinate findings independently verified against real retail data
(`ES2/VOL/simvol0/{hb0,hb1,hb2,gau,dpl}/`).

## Q1 — Palette: `COCKPIT.DPL`, needs a lighting-state index offset

**Update 2026-08-15 (follow-up session):** the resource name and load path below are still correct,
but "no offset" was wrong for the actual on-screen result — a real screenshot comparison showed the
naive decode is visibly wrong (purple/lavender-tinted, not the retail neutral gray/white). The offset
just isn't in the *load* path this section traced; see "The real fix — found empirically" below.

`maybe_MechInspectView_Ctor` (`00429660`) loads a singleton palette via the literal resource name 
`"cockpit"` joined to folder `"dpl"`(`ResourcePath_BuildFolderName` + `ClassItem`-tag load), guarded 
by `CockpitPaletteSingleton == 0` — i.e. loaded once, lazily, as `dpl\cockpit`. 
No index-remap, bank-select bit, or `+offset` was found anywhere between the raw pixel byte and the 
palette lookup *in this load path*, but I couldn't find any references to it or figure out how it is
used.

**Second finding:** a smaller companion palette, `IMPACTCP.DPL` (also a real retail file, same
1050-byte size as `COCKPIT.DPL`), gets loaded the same way and its colors copied into a 24-entry
sub-range (base index `0x2a`) of the shared 256-slot active display palette via `Palette_InstallRange`
(`004303c4`) — a damage/impact flash effect that temporarily recolors part of the palette, not a
static per-pixel transform. Doesn't affect steady-state decoding.

**Every caller of `Palette_InstallRange` was traced (8 call sites, 6 functions)** looking for where
`COCKPIT.DPL` itself gets installed into the shared hardware palette at some base index. None of them
is it: two are the `IMPACTCP.DPL` flash effect above, one is a 5-slot animated-palette-cycling system
(blinking lights, keyed off `Time_GetCoarseTicks`), one computes a 6-color lighting-ramp gradient for
a small indicator, one installs two 16-color ranges for an unidentified small effect, and one
(`FUN_0045dc34`) loads `"death1"`/`"death2"`/`"world0"` DPLs and installs the first at base 0 — this
is the mech-death-explosion screen-flash/fade sequence (matches the already-known
`Mech_DeathExplosion`), not cockpit rendering. **None of DBSIM's `Palette_InstallRange` call sites
install `COCKPIT.DPL` for normal gameplay rendering.**

### The workaround — found empirically, not via disassembly

`COCKPIT.DPL` is not one flat 256-color space. Dumping all 256 entries shows it's a sequence of ~15
short brightness **ramps**, each 6-16 entries, each a different hue/material. A full per-pixel 
histogram of a real `.HB0` (`COLOSSUS.HB0`) shows it only ever uses index 0 (viewport hole) plus a 
narrow band, almost entirely 42-71 — one ramp and its immediate neighbors. This looks like a classic
8-bit VGA-era lighting technique: each surface's shading is baked as a short palette-index ramp, and
a lighting/brightness state is applied by shifting the pixel's index within (or near) its own ramp.

**Confirmed empirically against a real reference screenshot:** shifting every *nonzero* palette index
by a constant offset (circularly within 1..255 — index 0 is a fixed sentinel, the viewport-hole
marker) reproduces the retail look. Offset **14** matches a neutral/daylight look for both COLOSSUS
(but couldn't find a real screenshot to compare against) and APOCA. Different offsets appear to
select different in-game lighting states — offset 246 gives APOCA a darker, redder look matching real
retail gameplay screenshots, but the same offset looks somewhat garbled for the Colossus cockpit.
Implemented as `Herculan.Engine.Content.CockpitArt.PaletteIndexOffset` (currently a static constant,
14).

**Still open:** the real selection mechanism (which offset applies when) was not found — plausibly
tied to an ambient-lighting system, given the terrain/mech renderer already has one
(`SceneRenderer.LightDirection`/`HazeColor`), but that system doesn't touch 2D/HUD rendering
currently. `CockpitViewInstance` (`0049b088`) is the next lead: it owns the live widget tree (see Q2)
but its background-art/palette field was not identified in this or the follow-up session.

**Empirically verified**: decoding a real `.HB0` through `COCKPIT.DPL` with a plain, unmodified
index→RGB lookup (`DynamixBitmapArrayTransformer` + `DynamixPaletteTransformer`, zero offset)
produces a correct, recognizable cockpit image — see the render checked against `APOCA.HB0` and
`SAMSON.HB0`. **`TextureViewerForm.PreferredPaletteFor`'s existing `"COCKPIT"` heuristic is exactly
right**, not merely a reasonable guess; no changes needed there.

Palette index 0 is pure black `(0,0,0)` in `COCKPIT.DPL` — relevant to Q3 below.

## Q2 — View compositing and `.HB1`'s role: empirically resolved, load site not found

**`.HB0`/`.HB1`/`.HB2`'s own load site was not located in DBSIM.EXE.** No literal `"hb0"`, `"hb1"`,
`"hb2"`, `"hba"`-as-cockpit, or `".hb"` substring exists anywhere in either `DBSIM.EXE` or
`VSHELL.EXE`'s static data (verified by raw byte scan, not just XREF search — the same technique that
worked for `MFORMS.DAT`'s hidden load site in Milestone 7 found nothing here). A promising lead
(`maybe_MechInspectView_Ctor`/`maybe_MechInspectView_LoadDamageFrames`, loading `vue\<herc>` +
per-index `hd0-3`/`ed0-3` frames) turned out to be a **different, smaller object**
(`maybe_MechInspectViewInstance`, `DAT_004d2544`) unrelated to the player's own cockpit background —
see the correction in `dgs-hd0-notes.md`. `.HBA` (e.g. `APOCA.HBA`, 35KB) and `.VUE` (142 bytes) are
both confirmed real but distinct, smaller formats, not related to the 640×480 `.HB0` family (sizes
alone rule out any envelope/container relationship: `.HB0`/`.HB1`/`.HB2` are each independently
~307KB, exactly `640*480 + header`).

**Resolved empirically instead, by decoding and visually inspecting the real assets** (all three
`.HBx` files decode cleanly through `COCKPIT.DPL` per Q1):

- **`.HB0` = front/center view.** Console with MFD bezel, shield gauge, throttle slider, weapon
  buttons — matches every already-decoded `GAUFile` widget.
- **`.HB2` = a genuine, distinct side view** — not a duplicate of `.HB0`. Shows a canopy strut close
  in the frame and one corner of the console (the energy-meter's diagonal stripe graphic) from a
  turned angle. Confirms the plan's "mirrored left / normal right" framing: one real side asset,
  horizontally flipped in the renderer for the opposite side, matching Phase 2's planned
  UV-flip-only approach (no separate mirrored asset needed).
- **`.HB1` is NOT a third front-facing angle.** It is the "Heads-down-display" (see manual).
  **Not used by this milestone**.
- `maybe_Sim_RenderFrame` (`0045fb9c`, already known) confirms per-frame ordering: `CockpitViewInstance`
  widget-paint dispatch happens **before** the 3D scene submit each frame, then `Player_PerFrameCockpitUpdate`
  and further widget paints happen **after**. `CockpitViewInstance` (`0049b088`, newly named) embeds
  each GAU widget as a live sub-object at a fixed offset matching `GAUFile.cs`'s own field order (a
  10-slot weapon-button array, then chain/link/autotrack/shield/etc.) — structural confirmation that
  `GAUFile`'s parsed layout matches DBSIM's own in-memory widget tree, independent of the file-format
  work already shipped.

**Open item:** the specific function that blits the `.HB0`/`.HB2` background and mirrors `.HB2` for
the opposite panel was not pinned to an address. Not needed for implementation — the mirror mechanism
(horizontal UV flip, same asset) is already visually confirmed correct by content inspection above.

## Q3 — 3D-viewport cutout and GAU↔pixel coordinate mapping: empirically solved

**GAU coordinates map to `.HB0`/`.HB2` pixel space via a uniform 2× scale on both axes.** `HudScreenSize`
(320,400) × 2 = (640,800) exceeds the image's 480px height, which is expected and fine — GAU's own 
addressable Y-range tops out around 220-240 (console widgets) to 400 (theoretical max). Additional
height might belong to the heads-down display or may be unused.

**3D-viewport cutout mechanism: solid black, palette index 0.** Every pixel in the "sky" region above
the console — where the live 3D view shows through — decodes to palette index 0 = RGB `(0,0,0)`,
confirmed by direct pixel sampling on real `APOCA.HB0`. **Not a clean, isolated color-key region**
though: index 0 also appears scattered through small shadow/gap details elsewhere in the console art
(a global bbox-by-index scan spans nearly the whole image, x:[0..639] y:[0..371] on `APOCA`). Recommend
implementing the cutout as a **flood-fill from a known-interior seed point** (e.g. the GAU reticle
position, always inside the viewport) rather than a naive "any black pixel is transparent" global
color-key, so scattered black console-detail pixels elsewhere aren't accidentally punched through to
the 3D view. Functionally this matches option 2 in the original Phase 0 question (opaque cockpit art
with a hole) more than a true render-order color-key — for an OpenGL implementation, either produces
the same visual result: draw the cockpit-art quad with alpha 0 over the flood-filled region.

## Side finding: `.HD0`-family loader

Not part of Q1-Q3, but found en route. See the correction in `dgs-hd0-notes.md` — the loader for
`.HD0`/`.HD1`/`.HD2`/`.HD3` (and a parallel `.ED0`-`.ED3` family) is now traced, but it belongs to a
separate, smaller target-inspection/mech-scan object, not the player's own cockpit background.

## New symbols

Applied to `known_symbols.json` / the live Ghidra project (`ES2ApplySymbolNames`, DBSIM.EXE):
`ResourcePath_BuildFolderName` (`00492ae0`), `CockpitDpl_ResourceFolderNames` (`004a0a14`),
`CockpitPaletteSingleton` (`0049ac48`), `Palette_InstallRange` (`004303c4`), `CockpitViewInstance`
(`0049b088`), `maybe_MechInspectView_Ctor` (`00429660`), `maybe_MechInspectView_LoadDamageFrames`
(`00429834`), `maybe_MechInspectViewInstance` (`004d2544`). `maybe_Sim_RenderFrame`'s existing entry
description extended with the widget-paint-before-3D-submit ordering.
