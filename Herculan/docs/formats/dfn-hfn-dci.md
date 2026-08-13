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

## `.DFN` / `.HFN` — "Panel" resource (a bitmap FONT format used by DBSIM's cockpit HUD text, NOT VSHELL's general UI-layout mechanism — see "Consumer found in DBSIM.EXE" below before assuming this answers a VSHELL-layout question)

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

### Fixed header + trailing-blob structure — confirmed via VSHELL.EXE decompilation, byte-exact against 2 real files

Traced live in Ghidra (VSHELL.EXE, `ES2Recon` project) rather than guessed from hex: found the
class's vtable at `0x0047bf68` (7 slots; slot 0 = `FUN_004542d0`, a trivial getter returning the
class id `0x280005` — the little-endian dword of the `[typeId=5][0x0028]` envelope, confirming
this vtable *is* the DFN/HFN "Panel" class). The generic loader chain, all confirmed by direct
decompilation:

- `FUN_00423d00` (`shellmap.cpp`) — VSHELL's wrapper that opens `"dfn\map.dfn"` by name; identical
  in shape to `FUN_00423c4c` which opens `.DBA` files, confirming both go through the same generic
  resource loader.
- `FUN_00403c1f` (`classio.cpp`, `ClassItem::loadItem`) — the generic loader: reads an 8-byte tag
  (`[typeId][0x0028][totalSize]` — exactly the known envelope), looks up a registered handler by
  the first 4 bytes via `FUN_00404028` (a bucket-array registry, base `DAT_00481248` / count
  `DAT_0046d1cc` — VSHELL's own copy of the same architecture found earlier in DBSIM.EXE, not
  shared code since they're separate processes), then calls the registered `loadFn(stream, mode)`.
- `FUN_00454238` — the registered factory for id `0x280005`: allocates a 0x32 (50)-byte object,
  installs the `0x0047bf68` vtable, then calls vtable slot `+0xc` (`FUN_00454474`) as
  `obj->readFrom(stream)`.
- `FUN_00454474` — **the actual field-by-field deserializer.** Reads 11 `int16` fields via
  `FUN_004646b0` (one call per field) into consecutive struct offsets `0x04`–`0x18`, then one
  `int32` field via `FUN_00454981` at `0x1a`, then conditionally reads up to 3 more variable-length
  blocks depending on those header fields' values (a 4th and 5th conditional block exist in the
  code but evaluated false/zero on every real file checked so far). `FUN_00454354` is the mirror
  "write" method (save path) — same field order, confirming the roles below are structural, not
  guessed from the reader alone.

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

Both files independently sum to exactly the declared `totalSize` once the header is subtracted —
this is real confirmation, not a coincidence of one lucky file. Notably `A = 223` in **both**
files despite them being structurally different panels (a management-shell map screen vs. a
cockpit color-scheme panel) — plausibly a fixed, engine-wide count (e.g. total named UI-element
slots in a shared enum) rather than a per-panel widget count; worth checking against more files
before assuming it varies.

### The 3 trailing blocks — semantics confirmed empirically (real-byte inspection, not decompilation) as a variable-length-record pool, NOT a string table

Traced the loaded object's only other reference in VSHELL (`FUN_00423e82`) and found it's just the
teardown/destructor call (`vtable+0x18`) — the "Panel" base class's vtable has **no rendering or
accessor methods at all** (only `classId`/2 size-calcs/`read`/`write`/destructor), and no other
code in VSHELL.EXE references the loaded `MAP.DFN` object by address. So the semantic meaning of
the trailing blocks couldn't be found via call-graph tracing (a genuine dead end, confirmed not
just unexplored — `FONT.DFN`/`FONT2.DFN`/`BLACK.DFN` aren't even referenced by a literal path
string anywhere in VSHELL.EXE, so they may be DBSIM-loaded despite living in the `SHELL0` volume;
not checked yet). Switched to directly inspecting the real bytes instead, which settled it:

- **`array2` (`A×4` bytes) is a monotonically increasing `uint32` offset table into `blob1`**,
  confirmed by direct inspection of `MAP.DFN`'s real bytes: `0, 0x18, 0x28, 0x50, 0x80, 0xa8, 0xd0,
  0xd8, 0xf8, 0x118, 0x138, ...` — strictly increasing, giving each of the `A` records a
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

### Consumer found in DBSIM.EXE — confirms this is a bitmap FONT resource, consumed as an opaque handle, not the general VSHELL UI-layout format

Checked whether DBSIM.EXE (unlike VSHELL) actually reads back what it loads: `ES2FindAddressRefs`
on the loaded color-scheme panel array (`DAT_0049b0ac`, the first of the 18 slots
`FUN_00431098` fills) found **7 real consumer functions** (`FUN_0044a7c0`, `FUN_00451e94`,
`FUN_0043a5a0`, `FUN_0043fe1c`, `FUN_0044ddec`, `FUN_0044c960`, `FUN_00450c54`) — unlike VSHELL's
`MAP.DFN`, which is genuinely dead after loading (see above). Decompiled all 7. None of them unpack
`blob1`/`array2`/`blob2` directly; instead the loaded object pointer is assigned wholesale into a
widget struct field (`**(undefined4 **)(param_1 + 0x14) = DAT_0049b0ac;`) and passed as one of
several arguments — alongside literal display strings (`_strupr(pilotName)`, a placeholder
`"XXXXXXXXX"`, etc.) — into a generic label/text constructor (`FUN_00438920`, `FUN_00438884`).
That call shape (opaque resource handle + a string, going into a "make a text widget" function) is
the signature of **a font being handed to a draw-this-string call**, not a widget-layout panel
being unpacked into buttons/rects. This independently corroborates the byte-level evidence above
(a ~223-entry glyph table, an offset table into a sparse 2-value mask pool, a per-glyph type byte)
rather than contradicting it — the trailing blocks are very likely per-glyph bitmap data for a
custom in-engine bitmap font, used by DBSIM's cockpit HUD text (pilot names, readouts), not a
button/widget layout table.

**Conclusion for this format: `.DFN`/`.HFN` "Panel" is a bitmap font resource, not VSHELL's general
UI-layout mechanism.** VSHELL references exactly one file of this format by name (`MAP.DFN`) and
never reads it back — consistent with it being a vestigial/unused load in VSHELL specifically,
while the format's real, live consumer is DBSIM's cockpit HUD font system. Finding VSHELL's actual
tab-screen layout mechanism (SAVE/WEAPONS/REPAIR/BUILD/ARMORY/CREW/MISSION) needs a different
target — the per-screen source files already identified in translation-unit recon
(`warmoryi.cpp`/`wcrewi.cpp`/`wmissini.cpp`/`wsrvbayi.cpp`/`wsquadi.cpp`, plus the underlying
`window.cpp`/`esdialog.cpp`/`wwinbase.cpp` widget machinery) are the next lead, not further work on
this format. Not pursued this session — noted here so a future session doesn't retrace this path
expecting it to answer the VSHELL-layout question.

**How to apply (if resuming pure `.DFN` glyph-format work for its own sake):** the record boundaries and per-record byte-runs from `blob1` are now extractable
mechanically (walk `array2`, slice `blob1` between consecutive offsets) — the natural next step is
dumping every record's raw bytes across several files and looking for a consistent width/height
pair per record (a mask needs dimensions from *somewhere*, plausibly encoded in the still-unmapped
header shorts `B`/`D`/`F`/`G`/`H`/`I`/`J`, or as extra bytes at the start of each `blob1` slice) —
try correlating record byte-length against candidate width×height products the way the `.GAU`
shield-display/throttle work in `project_es2_translation_status` memory did with real screenshots,
rather than guessing further from byte patterns alone. Ghidra project is `ES2Recon` at
`E:\ES2Stuff\tools\ghidra_project`; confirmed stable VSHELL.EXE addresses for resuming: vtable
`0x0047bf68`, loader chain `0x00423d00`→`0x00403c1f`→`0x00454238`→`0x00454474`. Also still open:
check whether DBSIM.EXE (not VSHELL) is what actually loads/renders `FONT.DFN`/`FONT2.DFN`/
`BLACK.DFN`, since VSHELL never references those 3 filenames by string at all despite them living
in the `SHELL0` volume alongside `MAP.DFN`.

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
