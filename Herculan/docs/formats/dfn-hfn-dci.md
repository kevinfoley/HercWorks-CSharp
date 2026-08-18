# .DFN / .HFN / .DCI — bitmap fonts and cursor images

NOTE TO CLAUDE: This should be a reference document, not a personal journal.

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
| `0x0B002800` | `.DCI` — cursor image | decoded below |
| `0x05002800` | `.DFN`/`.HFN` — bitmap font | decoded below |

`.DFN`/`.HFN`/`.DCI` are dispatched by a generic class-registry loader in `DBSIM.EXE` (FUN_0047a5a8 → FUN_0047a394). Specific loaders: `FUN_00430f58` (fonts), `FUN_00430fb0` (cursors).

## `.DCI` — cursor image

7 real files in `ES2/VOL/simvol0/dci/`: `{CURSOR,ECURSOR,MCURSOR,NCURSOR,PCURSOR,SCURSOR,WCURSOR}.DCI`.

Unlike `DynamixBitmapArray`, `.DCI` is a single embedded `DynamixBitmap` with an extra **hotspot** field spliced between the outer envelope and the sub-header.

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

**Hotspot field (click-point coordinates), verified against all 7 files by their directional prefix:**

| File | width×height | hotspot (x,y) | Interpretation |
|---|---|---|---|
| CURSOR.DCI | 7×8 | (3,3) | default/center |
| MCURSOR.DCI | 7×8 | (3,3) | move — also center |
| ECURSOR.DCI | 7×8 | (7,3) | east — right edge, vertically centered |
| WCURSOR.DCI | 7×8 | (0,3) | west — left edge, vertically centered |
| NCURSOR.DCI | 8×8 | (3,0) | north — top edge |
| SCURSOR.DCI | 8×8 | (3,7) | south — bottom edge |
| PCURSOR.DCI | 9×16 | (4,4) | pointer/pick — tip-ish, not center (see caveat below) |

**Caveat:** `PCURSOR.DCI` has ~101 undecoded trailing bytes; other 6 files end with 5 zero-padding bytes. The trailing data is mostly zero with scattered `0x38` and `0x3C` values — possibly a second image layer (AND-mask, outline) specific to this cursor, but not confirmed. Preserve as raw when parsing.

## `.DFN` / `.HFN` — bitmap font

DBSIM's only HUD text mechanism, and VSHELL's. Not a widget-layout resource: the seven consumer
functions in `DBSIM.EXE` pass the loaded object as an opaque handle to the generic label
constructors (`FUN_004387ac`/`FUN_00438884`/`FUN_00438920`) alongside a display string.

Two sets: `simvol0/dfn/*.DFN` and `simvol0/hfn/*.HFN` (26 and 25 files — the 18
`ColorSchemePanels` fonts plus spares), and `SHELL0/DFN/*.DFN` (`FONT`, `FONT2`, `MAP`, `BLACK`).
Same format throughout. `.HFN` is the 640-wide video mode's set and `.DFN` the 320-wide one's,
selected by `VideoMode_UseHiResPanels == 3`; they are separate art, not a 2x scale of each other
(cell heights 13 and 10, glyph counts 217 and 223).

### Layout

Offsets relative to content start, i.e. after the 9-byte VOL prefix.

```
0x00  uint16 typeId     = 0x0005      (BE dword 0x05002800)
0x02  uint16            = 0x0028
0x04  uint32 totalSize  -- content size below this field
0x08  int16  glyphCount
0x0a  int16             -- 0 in every retail file
0x0c  int16  firstCharCode           -- 32 in every retail file
0x0e  int16  cellHeight
0x10  int16             -- -1 in every retail file
0x12  int16  cellHeight              -- repeated
0x14  int16  baseline                -- 8 (.DFN) / 9 (.HFN)
0x16  int16             -- 8 in every retail file
0x18  int16             -- 0 in every retail file
0x1a  int16             -- 8 / 11 / 7, varies by file, meaning unmapped
0x1c  int16  arrayCount              -- 0 in every retail file; when non-zero, arrayCount x 4 bytes
                                        precede the glyph pool
0x1e  uint32 poolLength
0x22  [poolLength bytes]              glyph pool
      [glyphCount x uint32]           each glyph's start offset into the pool
      [glyphCount x uint8]            each glyph's width
```

A glyph is `width * cellHeight` bytes, row-major, one palette index per pixel. **Verified across all
54 retail font files: every glyph's pool slice is exactly `width * cellHeight` bytes, no
exceptions** — so the width byte and the gap between consecutive offsets state the same fact twice.

The declared width is the advance, art included: cells carry their own right-hand spacing column, so
a run is laid out by summing widths with no extra tracking. Glyph art is proportional — in
`ACTIVE.HFN`, `1` is 3 wide, `S` 6, `0` and `A` 8.

Index 0 is transparent and **every retail file uses exactly one other value as its ink**. That is
what makes the 18 colour-scheme fonts copies of one typeface: a widget picks its text colour by
picking which font to hand the label constructor, never by passing a colour.

| `.HFN` | ink | `.HFN` | ink |
|---|---|---|---|
| `WHITE` | 30 | `HUD1` | 72 |
| `GRAY` | 25 | `HUD2` | 73 |
| `GREEN` | 14 | `HUD3` | 74 |
| `DARK` | 19 | `CPGREEN` | 15 |
| `RED` | 10 | `ACTIVE` | 24 |

`ColorSchemePanels` (`0049b0ac`) is the 18-entry loaded-font array; see cockpit-hud.md for the load
order and which widget takes which entry.

Engine implementation: `Herculan.Engine.Content.HudFont`, packed into the shared HUD atlas by
`HudSpriteSheet`.

### Label background

A label paints its rect before its text, in the colour id at the label object's field `0x1d` — `0x2e`
for a weapon row, `DAT_004d3c26` (`COLORS.DAT` id 19, palette 16, black) for the shield readouts.
That is why retail's shield "100" sits on solid black rather than on the bezel art under it.

### Consumers

`FUN_0044a7c0`, `FUN_00451e94`, `FUN_0043a5a0`, `FUN_0043fe1c`, `FUN_0044ddec`, `FUN_0044c960`,
`FUN_00450c54`. VSHELL loads `MAP.DFN` (`ShellMap_DfnPanelPtr`, `00471ca8`) but never reads it back —
that load is vestigial.

## Ruled out: `.BND` and `.SNC`

Real files checked (`ACTOR.BND`, `MECH.BND`, `CAM.BND`, `PA_01000.SNC`, `PA_02000.SNC`) do NOT start with `[typeId][0x0028]` after the VOL prefix. Both remain separate, still-undecoded formats.

## Open questions

- `.DFN`/`.HFN`: the header shorts at `0x0a`, `0x16`, `0x18` and `0x1a` are constant or near-constant
  across every retail file and have no observed consumer.
- `.DCI`: `PCURSOR.DCI`'s trailing 101 bytes (likely an AND-mask or outline layer, unconfirmed).
- Whether DBSIM.EXE (not VSHELL) loads the SHELL0 fonts (`FONT.DFN`, `FONT2.DFN`, `BLACK.DFN`).
