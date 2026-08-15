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

`.DFN`/`.HFN`/`.DCI` are dispatched by a generic class-registry loader in `DBSIM.EXE` (FUN_0047a5a8 → FUN_0047a394). Specific loaders: `FUN_00430f58` (panels), `FUN_00430fb0` (cursors).

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

## `.DFN` / `.HFN` — Panel resource (bitmap FONT for DBSIM's cockpit HUD, not VSHELL's UI layout)

Two file sets:
- `ES2/VOL/simvol0/dfn/*.DFN` — 18 color-scheme variants (ACTIVE, CPBLACK, CPBLUE, etc., loaded by FUN_00431098). `.HFN` and `.DFN` are the same format, differing only by extension based on runtime mode (DAT_004d25bb).
- `ES2/VOL/SHELL0/DFN/*.DFN` — 4 files (FONT.DFN, FONT2.DFN, MAP.DFN, BLACK.DFN) referenced directly in VSHELL.EXE. Same outer magic as simvol0.

Confirmed layout (after 9-byte VOL prefix):

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

### Fixed header + trailing-blob structure

Panel class vtable at `0x0047bf68` (slot 0: `FUN_004542d0`). Load path: VSHELL `FUN_00423d00` → generic loader `FUN_00403c1f` → factory `FUN_00454238` → deserializer `FUN_00454474`. Deserializer reads 11 int16 fields at offsets `0x04`–`0x18`, one int32 at `0x1a`, then conditionally-parsed variable-length blocks.

**Confirmed on-disk layout** (offsets relative to content start, i.e. after the 9-byte VOL prefix;
letters are this doc's own labels, not the original source's field names, which weren't recovered):

```
0x00  uint16 typeId    = 0x0005        (BE dword 0x05002800)
0x02  uint16            = 0x0028
0x04  uint32 totalSize -- content size below this field
0x08  int16  A          -- a count, reused twice below (array-of-4-byte-entries count AND, when
                           E==-1, a second raw-byte-blob length)
0x0a  int16  B
0x0c  int16  C          -- sentinel: when == -1, a 5th conditional block (count=B, 2 bytes/entry)
                           is present (not observed set in any file checked yet)
0x0e  int16  D
0x10  int16  E          -- sentinel: -1 in every real file checked so far (MAP.DFN, ACTIVE.DFN);
                           when -1, triggers the length-A raw blob at the end
0x12  int16  F
0x14  int16  G
0x16  int16  H
0x18  int16  I
0x1a  int16  J
0x1c  int16  K          -- count for the first conditional array (K×4 bytes); 0 in every file
                           checked so far, so that array has never actually been observed non-empty
0x1e  int32  L          -- byte length of the first conditional blob
      -- (26-byte header ends here; all of the below are conditional on the header fields)
      [K×4 bytes]        -- array1, present only if K != 0 (not yet observed non-empty)
      [L bytes]          -- blob1, raw bytes, present only if L != 0 (present in every file checked)
      [A×4 bytes]        -- array2, present only if A != 0 (present in every file checked)
      [A bytes]          -- blob2, raw bytes, present only if E == -1 (present in every file checked;
                            length reuses field A, NOT a separate on-disk length)
      [B×2 bytes]        -- array3 (2 bytes/entry), present only if C == -1 (not yet observed)
```

**Byte-exact validated against 2 real files, arithmetic not guesswork:**

| File | totalSize | A | E | K | L (blob1) | A×4 (array2) | A (blob2) | sum vs. totalSize−26 |
|---|---|---|---|---|---|---|---|---|
| `SHELL0/DFN/MAP.DFN` | 5909 | 223 | −1 | 0 | 4768 | 892 | 223 | 4768+892+223 = 5883 = 5909−26 ✓ |
| `simvol0/dfn/ACTIVE.DFN` | 7741 | 223 | −1 | 0 | 6600 | 892 | 223 | 6600+892+223 = 7715 = 7741−26 ✓ |

Both files sum exactly to declared `totalSize`. Field `A = 223` in both files despite structural differences (map vs. cockpit panel) — plausibly a fixed engine-wide count.

### The 3 trailing blocks — variable-length-record pool (NOT a string table)

- **`array2` (`A×4` bytes) is a monotonically increasing `uint32` offset table into `blob1`**,
  confirmed by direct inspection of `MAP.DFN`'s real bytes: `0, 0x18, 0x28, 0x50, 0x80, ...` — 
  strictly increasing, giving each of the `A` records a
  variable-length slice of `blob1` (record *i*'s data runs from `array2[i]` to `array2[i+1]`,
  lengths seen: 24, 16, 40, 48, 40, 40, 8, 32, 32, 32, 48, ...). This is the same
  `[count][pool][count×offset]` shape already confirmed for `weapons.bin`
  (`docs/formats/weapons-dat.md`), just with 4-byte offsets instead of 2-byte.
- **`blob1` is NOT a string pool — ruled out by direct inspection.** Real bytes are overwhelmingly
  `0x00` with a narrow band of other values, never printable ASCII text: `MAP.DFN`'s 4768 bytes are
  3891× `0x00`, 789× `0x3b`, 88× `0x3a` (only 3 distinct byte values in the entire blob);
  `ACTIVE.DFN`'s 6600 bytes are **exactly 2 distinct values**, 5380× `0x00` and 1220× `0x1f`. This
  is the signature of a sparse raster mask/stencil (1 byte per pixel, "off"/"on" plus one
  anti-aliased edge value for `MAP.DFN`), not text — each of the `A` variable-length slices from
  `array2` is very likely a small per-record bitmap fragment (a glyph, an icon silhouette, or a
  highlight/glow mask), matching the same "sparse mask, narrow value band" signature already seen
  in `PCURSOR.DCI`'s undecoded trailing bytes and the confirmed `.EDG` scanline clip masks
  elsewhere in this project — worth checking against those known-decoded formats for a shared
  encoding scheme rather than treating this as fully novel.
- **`blob2` (`A` bytes, one per record) is a small enum, not a flags byte.** `MAP.DFN`'s real
  bytes are dominated by `0x04` with scattered `0x01`–`0x06` for roughly the first 180 records,
  then a **long uniform run of `0x01`** for the remaining ~40 — matching the "unused/null slot"
  sentinel pattern already confirmed elsewhere in this project (e.g. `.GAU`'s null-weapon
  sentinel). Best current guess: a per-record type/category tag (widget kind, icon kind, etc.),
  with `0x01` doubling as the empty-slot value.
- The 11 small header shorts (`B`,`C`,`D`,`F`,`G`,`H`,`I`,`J`) are still not mapped to individual
  meaning. `A` (the record count) is 223 in **both** `MAP.DFN` and `ACTIVE.DFN` but only 217 in
  `FONT2.DFN`/`BLACK.DFN` (SHELL0) — so it does vary per-file, not a fixed engine-wide constant as
  first guessed, but 217–223 is a suspiciously tight band (printable-ASCII-range-sized) worth
  testing against more of the 18 `simvol0/dfn` color variants before concluding anything.

### Consumer found in DBSIM.EXE

7 real consumer functions in DBSIM.EXE (`FUN_0044a7c0`, `FUN_00451e94`, `FUN_0043a5a0`, `FUN_0043fe1c`, `FUN_0044ddec`, `FUN_0044c960`, `FUN_00450c54`) pass the loaded panel object as an opaque handle to generic label/text constructors (`FUN_00438920`, `FUN_00438884`), alongside literal strings — the signature of **a font being handed to a draw-this-string call**, not a widget-layout unpacking.

**Conclusion: `.DFN`/`.HFN` is a bitmap font resource used by DBSIM's cockpit HUD (pilot names, readouts), not VSHELL's general UI-layout mechanism.** VSHELL's `MAP.DFN` is loaded but never used in VSHELL.EXE itself.

**Open question:** Whether DBSIM.EXE loads the SHELL0 DFN files (FONT.DFN, FONT2.DFN, BLACK.DFN), since VSHELL never references them by name.

## Ruled out: `.BND` and `.SNC`

Real files checked (`ACTOR.BND`, `MECH.BND`, `CAM.BND`, `PA_01000.SNC`, `PA_02000.SNC`) do NOT start with `[typeId][0x0028]` after the VOL prefix. Both remain separate, still-undecoded formats.

## Open questions

- `.DFN`/`.HFN`: record dimensions (width/height) within each `blob1` slice — still unmapped header shorts `B`/`D`/`F`/`G`/`H`/`I`/`J` may encode this.
- `.DCI`: `PCURSOR.DCI`'s trailing 101 bytes (likely AND-mask or outline layer, but not confirmed).
- Whether DBSIM.EXE (not VSHELL) loads the SHELL0 DFN files (FONT.DFN, FONT2.DFN, BLACK.DFN).
