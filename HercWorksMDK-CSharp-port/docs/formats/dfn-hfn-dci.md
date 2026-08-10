# .DFN / .HFN / .DCI — Panel definitions and cursor images

Reverse-engineered from `VSHELL.EXE`/`DBSIM.EXE` disassembly (Ghidra, `E:\ES2Stuff\tools\`), not
from the Java source (`ES2TransferApi`/etc. never covered these). Cross-checked against real
retail files (`ES2/VOL/simvol0/dfn/`, `ES2/VOL/simvol0/dci/`, `ES2/VOL/SHELL0/DFN/`). This doc
records what's confirmed and what's still open — don't treat the open parts as settled.

## The shared "Dynamix resource" envelope

All of `.DFN`, `.HFN`, `.DCI`, `.DBA`/`.HBA`/`.HB0-2`/`.DB0-2` (already-ported
`DynamixBitmapArray`), and the embedded per-image `DynamixBitmap` sub-header share one 4-byte
envelope shape, immediately after the standard 9-byte VOL-entry prefix
(`HercWorks.Vol.VolEntryPrefixCodec`):

```
[0..1] uint16 typeId   -- distinguishes the specific resource kind
[2..3] uint16 0x0028   -- constant across the whole family
```

Confirmed `typeId` values (all read as **big-endian** 4-byte magic, matching the existing
`DynamixBitmapArray.HeaderMagic = 0x01002800` convention already in the codebase):

| typeId (BE dword) | Kind | Status |
|---|---|---|
| `0x01002800` | `.DBA`/`.HBA`/`.HB0-2`/`.DB0-2` — bitmap array | already ported (`DynamixBitmapArrayTransformer`) |
| `0x0E002800` | Embedded single-image sub-header inside the above | already ported |
| `0x0B002800` | `.DCI` — cursor image | **new, this doc** |
| `0x05002800` | `.DFN`/`.HFN` — "Panel" resource | **new, this doc** |

This lines up with a real, generic **class-registry loader found in `DBSIM.EXE`**
(`FUN_0047a5a8` → `FUN_0047a394` → handler's `vtable+4` load function, at addresses current as
of the 2026-08-08 Ghidra session — see the `project_es2_exe_recon` memory for the full call
chain): the loader reads this envelope, looks up a registered handler by `typeId`, and dispatches
to it. `.DFN`/`.HFN`/`.DCI` all go through this same generic loader (`FUN_00430f58` for
panels, `FUN_00430fb0` for cursors — both trivial wrappers calling the shared loader with
different filenames). **Practical implication for a C# port: any future undecoded Dynamix-family
file is worth checking against this same `[typeId][0x0028]` shape before assuming it's unrelated.**
Ruled out for `.BND` and `.SNC` — neither matches this envelope (see bottom of this doc).

## `.DCI` — cursor image (structure confirmed and verified against all 7 real files)

Real files: `ES2/VOL/simvol0/dci/{CURSOR,ECURSOR,MCURSOR,NCURSOR,PCURSOR,SCURSOR,WCURSOR}.DCI` — 7
files total, matching a loop-count of 7 found in `DBSIM.EXE`'s panel/cursor init function
(`FUN_00431098`).

This is why plugging `.DCI` directly into the existing `DynamixBitmapArrayTransformer` throws
`ArgumentException` on every real file (noted in `project_es2_translation_status` memory): DCI is
**not** a bitmap array with a different magic, it's a single embedded `DynamixBitmap` with an
extra **hotspot** field spliced in between the outer envelope and the embedded sub-header —
something `DynamixBitmapArrayTransformer` has no slot for.

Confirmed layout (offsets relative to the start of file content, i.e. after the 9-byte VOL
prefix):

```
0x00  uint16 typeId       = 0x000B   (BE dword 0x0B002800)
0x02  uint16              = 0x0028   (constant marker)
0x04  uint32 totalSize    -- content size below this field
0x08  int32  hotspotX     -- cursor click-point X, CONFIRMED (see below)
0x0C  int32  hotspotY     -- cursor click-point Y, CONFIRMED (see below)
0x10  --- embedded DynamixBitmap sub-header starts here (typeId 0x0E002800) ---
0x10  uint16 typeId       = 0x000E
0x12  uint16              = 0x0028
0x14  uint32 subSize
0x18  uint16 width
0x1A  uint16 height
0x1C  uint16 bitsPerPixel  -- CONFIRMED = 8 (indexed color) in all 7 files, constant regardless of width/height
0x1E  uint32 pixelDataLen  -- CONFIRMED = width*height in all 7 files (1 byte/pixel)
0x22  [pixelDataLen bytes] pixel data (0x00 = background, one non-zero indexed color = the cursor's "ink")
```

**Hotspot field confirmed by cross-checking all 7 files against their directional letter prefix**
— this was the deciding evidence, not just a plausible guess:

| File | width×height | hotspot (x,y) | Interpretation |
|---|---|---|---|
| CURSOR.DCI | 7×8 | (3,3) | default/center |
| MCURSOR.DCI | 7×8 | (3,3) | move — also center |
| ECURSOR.DCI | 7×8 | (7,3) | east — right edge, vertically centered |
| WCURSOR.DCI | 7×8 | (0,3) | west — left edge, vertically centered |
| NCURSOR.DCI | 8×8 | (3,0) | north — top edge |
| SCURSOR.DCI | 8×8 | (3,7) | south — bottom edge |
| PCURSOR.DCI | 9×16 | (4,4) | pointer/pick — tip-ish, not center (see caveat below) |

**Caveat — `PCURSOR.DCI` has ~101 bytes of undecoded trailing data the other 6 files don't
have.** The other 6 files' content ends exactly 5 bytes after the last pixel byte (those 5
trailing bytes are consistently `00 00 00 00 00` in every file checked — likely padding/alignment,
not a real field). `PCURSOR.DCI` is bigger (288 bytes total vs. 104–112 for the rest) and after
its one confirmed embedded bitmap (144 = 9×16 pixel bytes) there's ~101 more bytes that are
**not** a second `DynamixBitmap` sub-header (checked — no `0E 00 28 00` signature there) and
don't match the "5 trailing zero bytes" pattern either. The extra bytes are mostly zero with
scattered `0x38` values in the first ~90 bytes and scattered `0x3C` (the same "ink" value used in
every other cursor's pixel data) in the last ~15 — plausibly a second, differently-encoded image
(e.g. a packed 1-bit AND-mask, or an outline/glow layer) specific to this one cursor, but not
confirmed. Don't assume the simple 6-file structure covers `PCURSOR.DCI` fully; a transformer
should either special-case it or explicitly flag/preserve the trailing bytes as raw, unparsed data
the way `.STR`'s per-file-variable trailer or `.RMP`'s body were handled elsewhere in this
project.

## `.DFN` / `.HFN` — "Panel" resource (color-scheme cockpit UI definitions; magic + high-level role confirmed, byte layout beyond the envelope NOT decoded)

Two file sets share this format:
- `ES2/VOL/simvol0/dfn/*.DFN` — 18 files, all per-**color-scheme** variants of the same panel
  (`ACTIVE`, `CPBLACK`, `CPBLUE`, `CPDARK`, `CPGREEN`, `CPGREY`, `CPOFF`, `CPON`, `CPORANGE`, ...).
  Confirmed by `DBSIM.EXE`'s `FUN_00431098`: it loads exactly 18 of these by name from a
  hardcoded name table (`cpblue` etc. is the first one), keyed by `DAT_004d25bb` selecting
  `.hfn` vs `.dfn` extension for the *same* name (mode/team-flag `== 3` picks `.hfn`, otherwise
  `.dfn`) — i.e. **`.HFN` and `.DFN` are the same format with the same filename stem**, just an
  alternate-extension variant selected by some runtime mode, not two different formats.
- `ES2/VOL/SHELL0/DFN/*.DFN` — a different, smaller set (`FONT.DFN`, `FONT2.DFN`, `MAP.DFN`,
  `BLACK.DFN`) referenced directly by name in `VSHELL.EXE` (e.g. the literal string
  `"dfn\map.dfn"`). Confirmed same outer magic (`0x05002800`) as the simvol0 color-scheme set, so
  it's the same container format reused for non-color-scheme panels (a font layout, a map panel,
  etc.) — **not** a separate format needing its own doc.

Confirmed from real-file inspection (`ACTIVE.DFN`/`CPBLACK.DFN`/`MAP.DFN`/`FONT.DFN`, all past
the 9-byte VOL prefix):

```
0x00  uint16 typeId    = 0x0005   (BE dword 0x05002800)
0x02  uint16           = 0x0028
0x04  uint32 totalSize -- content size below this field (e.g. 0x1e3d for ACTIVE.DFN/CPBLACK.DFN)
0x08  ...              -- NOT decoded past this point
```

`ACTIVE.DFN` and `CPBLACK.DFN` (both 7759 bytes) have **byte-identical content from offset 0x08
through at least offset 0x27** in a spot-check — strong evidence the two colors share one panel
*layout* and differ only in some later portion (very likely color/bitmap-reference fields), but
that later portion wasn't located. `MAP.DFN`/`FONT.DFN` differ from the color-scheme files in
several of the early count/size-looking fields (as expected, since they're structurally different
panels), but share the same envelope.

**Not decoded at all:** the actual widget/button table implied by the `ClassItem`-style
architecture and the `title`/`active`/`pushed`/`inactive`/`cpylw` strings found sitting near
`ALERT.CPP`'s `hfn`/`dfn` extension-token pair in `DBSIM.EXE` — these look like generic
UI-widget *state names* (a button has an active/pushed/inactive bitmap or color each), suggesting
each panel is a list of named widgets each with several state-specific sub-values, but no record
boundaries were found in the real file bytes past the envelope. This is a **much bigger, less
constrained format than DCI** (7759 bytes of real content vs. DCI's ~95) — expect this to need a
dedicated session with real byte-diffing across several of the 18 color variants (not just the
two spot-checked here) to find where they actually diverge, which is the strongest lead for
finding the first real per-widget record boundary.

## Ruled out: `.BND` and `.SNC` do NOT use this envelope

Checked real files (`ACTOR.BND`, `MECH.BND`, `CAM.BND`, `PA_01000.SNC`, `PA_02000.SNC`) — none
start with a `[[typeId][0x0028]]` shape after the VOL prefix. Both remain genuinely separate,
still-undecoded formats (see `project_es2_translation_status` memory for prior findings: `.BND`
is ~83 files across ~20+ "flavors" of mostly-unlabeled tuning constants; `.SNC` is 556 files,
likely per-mission audio-sync/cue data, first content field looks like a count followed by small
byte-pair entries — not investigated further yet).

## How to apply

If picking this up again: `.DCI` is the fast finish (7 files, ~95 bytes each, structure is
essentially fully known — just verify all 7 and nail the trailing 5 bytes). `.DFN`/`.HFN` needs a
proper byte-diff pass across the 18 color-scheme files (and ideally the class-registration
disassembly, to find the actual widget-record loader function referenced through the
`ClassItem`-style dispatch) before a transformer can be written with real confidence — the
envelope alone isn't enough to parse the widget table. `.BND`/`.SNC` need their own investigation
from scratch; this session only ruled out that they share the Dynamix envelope, nothing more.
