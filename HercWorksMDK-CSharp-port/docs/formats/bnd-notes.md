# .BND — per-subsystem tuning/config source files (envelope + CAM.BND fully solved; confirmed build-time-only, never read by DBSIM.EXE at runtime)

83 real files in `ES2/VOL/simvol0/bnd/`, one per major DBSIM subsystem — filenames map directly
onto `DBSIM.EXE`'s own translation-unit/class names (`ACTOR`, `ALERT`, `BULLET`, `CAM`, `DEBRIS`,
`FIRE`, `MECH`, `MECHSYS`, `OBJLIST`, `ROCKET`, `TERRAIN`, `TS_PART`, `PWEAPONS`, etc. — see
`project_es2_exe_recon` memory's DBSIM source-file list). Files are **tiny** — 16 to 404 bytes,
median well under 100 — ruling out the earlier working theory that these are per-mech-type arrays
like `WEAPONS.DAT`; they read as small per-module tuning/config records instead.

## Solved: a universal 9-byte envelope + 1-byte record tag, confirmed byte-exact across all 83 files

```
offset 0x00        byte    0x02              constant format/record-type marker (100% of files)
offset 0x01-0x02   uint16  payloadLen = fileSize - 10, little-endian, verified with ZERO exceptions
offset 0x03-0x04   uint16  0x0000            reserved/padding, constant 0 in all 83 files
offset 0x05-0x08   4 bytes                   build/batch stamp (see below) — not yet fully decoded
offset 0x09        byte    recordTag         first byte of the per-subsystem record (see below)
offset 0x0a..end   payloadLen bytes          per-subsystem record body, decoded for CAM.BND only
```

Verified programmatically against every file in `ES2/VOL/simvol0/bnd/` (a small shell loop
checking `byte[0]==0x02`, `byte[3..4]==0000`, and `payloadLen==fileSize-10`) — **no exceptions
across all 83 files**, from the smallest (`FLAT.BND`/`GNDTEX.BND`/`LIGHTS.BND`/`TS_PART.BND`, 16
bytes) to the largest (`MECH.BND`, 404 bytes — this is the "394 bytes" figure already flagged as
stale-vs-the-Java-doc-comment in `project_es2_translation_status`; that 394 is actually
`payloadLen`, i.e. the file is `9-byte envelope + 1-byte recordTag + 394-byte payload`).

**The offset-0x09 byte is not part of the build stamp — it's the first byte of the real
per-subsystem record**, confirmed by cross-checking against `org.hercworks.core.data.file.bnd`'s
Java doc comments (see "CAM.BND fully solved" below): the original Java author's own "offset 0"
notes for `Cam.java`/`Mech.java`/`MechSys.java`/`AppInput.java` all line up exactly with this file
byte, not with the byte after it. This was a real correction mid-session — the byte was initially
lumped in with the 5-byte stamp before the Java source was checked.

**Bytes `0x05`-`0x08` (4-byte "build stamp"):** not a plain Unix timestamp (checked both byte
orders against a 1996-97 ship-date window, neither produces a plausible date). Files cluster into
tight groups that share these 4 bytes exactly, with the following recordTag byte (0x09) varying by
a small increment within a group — e.g. `ROCKET.BND` stamp=`3b20ef7a` tag=`52`, `PSTATUS.BND`
stamp=`3b20ef7a` tag=`53`, `APPINPUT.BND`/`PHDDDAMG.BND` stamp=`3b20ef7a` tag=`54`,
`PMISSILE.BND` stamp=`3b20ef7a` tag=`55`. This is the signature of an **offline build tool
stamping a batch of outputs compiled in the same run** (consistent with a `BATCH.EXE`-style
compiler — `ES2/BATCH.EXE` exists and is a real candidate, though it does not itself contain any
`"bnd"` text — see below), not something the game itself needs to interpret. Grouping isn't
alphabetical or size-correlated, so it likely reflects the original source/build-script ordering,
not recoverable from the shipped files alone.

## Solved: CAM.BND's full 25-byte record (envelope's recordTag + 24-byte payload)

The Java source (`herc-works-mdk-main/ES2Core/.../data/file/bnd/{Cam,Mech,MechSys,AppInput,
MechView}.java`) turned out to already have **sample-value-annotated byte layouts** for 5 of the 83
`.BND` files, ported into the C# tree as empty placeholder classes
(`HercWorks.Core/Data/File/Bnd/*.cs`) that nobody had cross-checked against real files or the
corrected envelope model yet. Doing that cross-check this session is what caught the
offset-0x09/recordTag correction above, and for `CAM.BND` specifically it accounts for **every
byte in the file**:

| Offset (from recordTag=0) | Field | Real value | C# property |
|---|---|---|---|
| 0 | UINT8 | 54 | `RecordTag` |
| 1 | UINT8 | 208 | `Unknown1` |
| 2 | UINT8 | 52 | `Unknown2` |
| 3 | UINT8 | 49 | `Unknown3` |
| 4-5 | UINT16 LE | 2500 | `Distance1` |
| 6-7 | UINT16 LE | 30000 | `Distance2` |
| 8 | UINT8 | 0 | `Blank1` |
| 9 | UINT8 | 8 | `Unknown4` |
| 10 | UINT8 | 192 | `Unknown5` |
| 11 | UINT8 | 0 | `Blank2` |
| 12 | UINT8 | 0 | `Blank3` |
| 13 | UINT8 | 4 | `Unknown6` |
| 14 | UINT8 | 80 | `Unknown7` |
| 15 | UINT8 | 0 | `Blank4` |
| 16 | UINT8 | 0 | `Blank5` |
| 17 | UINT8 | 48 | `Unknown8` |
| 18 | UINT8 | 38 | `Unknown9` |
| 19 | UINT8 | 2 | `Unknown10` |
| 20-21 | UINT16 LE | 500 | `Value3` |
| 22-23 | UINT16 LE | 8000 | `Value4` |
| 24 | UINT8 | 31 | `TrailingByte` (not in the Java author's notes — their list ends one byte early) |

21 of 22 numeric fields match the Java author's own sample values exactly; the one exception
(offset 14, `Unknown7`) is their notes saying "50" against the real retail file's `0x50` = 80 —
almost certainly the author writing down a hex digit string without converting it, not a real
data difference (everything else, including two full 16-bit values, matches exactly).

Implemented as `HercWorks.Core.Data.File.Bnd.Cam` + `HercWorks.Core.Io.Transform.Bnd.CamTransformer`,
registered in `TransformerRegistry` by exact file name (`CAM.BND` — every other `.BND` file has an
unrelated record shape, there is no shared per-extension parser). Round-trips byte-exact against
the real retail `CAM.BND` (verified via a throwaway probe project, same pattern used for other
format verifications in this codebase).

Field *meanings* are still unconfirmed — `Distance1`/`Distance2`/`Value3`/`Value4` (2500, 30000,
500, 8000) are plausibly camera near/far or zoom-range values given the file name, but this is a
guess, not verified against any code path (the loader was never found — see below). `Unknown3`
(49 = ASCII `'1'`) is shared at the same relative offset by `CAM`/`MECH`/`MECHSYS` — plausibly a
shared format sub-version byte, also unconfirmed.

**Partial coverage for the other 4 Java-annotated files** (`MECH.BND`, `MECHSYS.BND`,
`AppInput.BND`, `MechView.BND`) — no transformer written yet (data too incomplete), but their doc
comments now carry the corrected offset-0x09 alignment and, cross-checked against the real files:
- `MECH.BND`: first 8 record bytes match the Java author's notes exactly (242, 164, 51, 49, 12, 0,
  42, 0); bytes 8+ diverge from their all-zero notes (retail has 48, 117, 0, 0, 100, 0, 100, 0, ...)
  — most likely because the author's build had fewer defined entries in what looks like a
  per-mech-type array starting around record offset 8, matching the long-standing
  `MECH_TYPE_DATA[]` string hint in `project_es2_exe_recon` memory. Not fully mapped — MECH.BND's
  record is 395 bytes total, only the first 16 have any notes at all.
- `MECHSYS.BND`: the Java notes stop at a `TODO — finish` after offset 20. Extending them against
  the real 39-byte record found a clean repeating structure: after the first 5 bytes (241, 184, 35,
  49, 75), a `[UINT8 value][3 zero bytes]` stride runs at offsets 4, 8, 12, 16, 20, 24, 28 holding
  **75, 60, 45, 25, 18, 12, 6 — a monotonically decreasing sequence**, strongly suggestive of a
  distance/LOD tier or priority-falloff table, before a differently-shaped 4-byte trailer.
- `AppInput.BND`: only offset 0 (=84) was ever documented, confirmed exact; the other 22 record
  bytes are completely unmapped.
- `MechView.BND`: Java notes list offsets 0-1 with no sample values, so nothing to cross-check;
  envelope alignment applied but body untouched this session.

**Body-level echo of the same "batch family" pattern seen in the envelope stamp:** for several
small, same-shape files, the payload also opens with 2-3 bytes shared verbatim across "family"
files before diverging into what look like real per-instance values — e.g. `CTL_MGR.BND` payload =
`57 64 30 64 00 0a 00 00`, `PNAVMAP.BND` payload = `57 64 30 40 0d 03 00 00` (identical `57 64 30`
prefix). Not pursued into a confirmed field-by-field layout for these two — flagged as the
strongest lead for decoding the remaining ~78 files without Java notes (diff same-payload-length
files in like-named groups, e.g. all the `P*.BND` cockpit panels or `*_ALRT.BND` alert configs, the
same way `.DCI` got cracked by diffing 7 real cursor files).

## Solved: .BND is a build-time-only source format — its values are compiled directly into DBSIM.EXE, never read at runtime

The loader search below (kept for the record — it's real, careful work and explains *how* this was
established, not just the conclusion) found zero trace of any runtime `.BND` loader anywhere in
`DBSIM.EXE`. Rather than leaving that as an open question, this session cross-checked it against a
different, independent source: `docs/simulation/dbsim-physics-notes.md` already had several DBSIM
tuning values confirmed via raw disassembly as **literal instruction immediates** — i.e.
hardcoded into the compiled machine code itself, not read from any file at runtime:
- Rocket steering (`FUN_0040a254`): turn-rate cap `0x500` (1280), heading-error deadband `0xc00`
  (3072)/`0x1800` (6144); rocket proximity-fuze distance threshold `40000` (`FUN_0040a538`).
- Weapon default range-falloff breakpoints (`FUN_0044080c`): `0x78` (120), `0x168` (360), `0xb4`
  (180), `0x708` (1800) — the doc's own wording already called these "literal constants... default
  min/short/medium/long weapon range tiers... independent of whatever WEAPONS.DAT specifies."

Checked whether these exact numbers appear inside the matching real `.BND` files:

- **`ROCKET.BND`** contains `1280`, `3072`, and `40000` as exact little-endian UINT16 values, at
  file offsets 15-16, 17-18, and 23-24 respectively.
- **`PWEAPONS.BND`** contains `120, 360, 180, 1800` **contiguously, in that exact order**, 8
  straight bytes at file offset 67-74: `78 00 68 01 b4 00 08 07`.

Four specific values landing back-to-back in the right order in `PWEAPONS.BND` is not a coincidence
— for reference, `DEBRIS.BND` does *not* contain `182` (`0xb6`, the degrees→BAM angle-scale factor
also documented as a hardcoded multiplier applied by the loader *code*, not read from the file) —
consistent with the pattern, not a counter-example: values the code treats as genuinely fixed don't
show up in any `.BND`, values the code treats as per-subsystem tunables do.

**This resolves the original question cleanly: `.BND` files are a human/build-tool-facing source
format (very plausibly compiled by `ES2/BATCH.EXE`, matching the build-batch stamp evidence in the
envelope above) whose values get baked directly into `DBSIM.EXE`'s code as literal constants at
build time.** The retail game never opens a `.bnd` file — there is no runtime loader to find, and
the string/address-xref search below correctly found nothing because there was nothing to find, not
because the technique failed. This also means decoding `.BND` is *not* pointless for this project:
it's the authoritative source for constants whose in-game effect is already understood from the
opposite direction (code disassembly) — matching a `.BND` file's still-unknown fields against more
of `dbsim-physics-notes.md`'s documented constants (per subsystem) is now the most promising way to
give the remaining "Unknown"/"?" fields real names, without needing any further Ghidra loader work.

## Loader search (kept for the record — explains how "build-time-only" was established)

DBSIM.EXE's shared extension-name table does NOT reference "bnd"

Found (this session) a previously-unknown **18-entry literal string-pointer array** in `DBSIM.EXE`'s
DATA section, file offset `0x9F9FC` (VA `0x004A09FC`), containing the game's known file-open modes
and extensions in order: `wb, rb, nam, dat, bnd, dts, dpl, gl, dmg, col, edg, dba, dci, dfn, gau,
hfn, hdg, hba` — one `char*` per entry, 4 bytes each, each pointing at a short null-terminated
string immediately following the array. This is the **only** occurrence of the substring `"bnd"`
anywhere in `DBSIM.EXE` (confirmed via an exhaustive case-insensitive scan of every printable
ASCII run in the whole 724 KB file) — and `"bnd"` does not appear anywhere at all in `VSHELL.EXE`,
`ES.EXE`, or `BATCH.EXE` either (also fully scanned).

Already-confirmed real usage of this same array: `maybe_DatFolderPrefixPtr` (`docs/simulation/
dbsim-physics-notes.md`'s terrain section) is literally this array's `"dat"` slot (VA `0x004A0A08`
— independently confirmed, both derivations land on the same address). So this table is real and is
genuinely used as a shared folder/extension name pool by many subsystems.

**But the `"bnd"` slot itself (VA `0x004A0A0C`) has zero references anywhere in the binary** —
checked two independent ways: (1) a full raw-byte scan of the entire `DBSIM.EXE` file for the
slot's own address as a little-endian 4-byte immediate (the pattern that *does* find real usages,
e.g. it's how the array itself was discovered), and (2) `ES2FindAddressRefs.java` (Ghidra's formal
reference manager) against the same address. Both come back empty. As a control, the immediately
adjacent slot, `"dts"` at VA `0x004A0A10`, returns **11 real call-site references** via the exact
same technique — proving the method works and that `"bnd"`'s zero-result is a genuine finding, not
a tooling gap (the same false-negative trap flagged for the `"MECH"` string in an earlier session,
now positively ruled out here by the working control).

**Conclusion: `.BND` files are not opened by `DBSIM.EXE` through the shared extension-table +
generic path-builder mechanism (`FUN_00492ae0`, confirmed used by ~85 call sites across ~60
different functions for the other 17 extensions in this same table) — and, per the section above,
not through any other runtime mechanism either, since their values are confirmed baked directly
into the compiled code instead.** This was the right technique, applied thoroughly (positive
control included), and it correctly returned nothing.

## Ruled out (carried over from the previous investigation, still holds)

- **Not part of the "Dynamix resource" envelope** documented in `dfn-hfn-dci.md` — checked
  `ACTOR.BND`, `MECH.BND`, `CAM.BND` directly; none start with `[typeId:uint16][0x0028:uint16]`
  after a VOL prefix (moot for these specific files anyway, since they're loose files on disk, not
  VOL-archived — but the new 10-byte header above is the real envelope, and it does not match the
  Dynamix `ClassItem` shape either).

## How to apply

**Check the Java source first, every time, before hex-diffing a new `.BND` file** — this session's
single biggest time-saver was finally reading `herc-works-mdk-main/ES2Core/.../data/file/bnd/*.java`
instead of only the empty C# placeholders that were ported from them; the sample-value comments
there directly cracked `CAM.BND` and partially cracked 3 more files in minutes, after a loader-
search dead end had consumed most of the session. Only 5 of the 83 files have any Java notes
(`Cam`, `Mech`, `MechSys`, `AppInput`, `MechView`) — for the other ~78, the same-shape-family
diffing approach (group same-payload-length files by likely-related name, e.g. `P*.BND` cockpit
panels or `*_ALRT.BND` alert configs, diff byte-by-byte the way `.DCI` got cracked) is still the
best untried lead.

`CAM.BND` is fully solved and implemented — `HercWorks.Core.Data.File.Bnd.Cam` +
`Io.Transform.Bnd.CamTransformer`, registered in `TransformerRegistry`, round-trips byte-exact. Use
this as the template for finishing `Mech`/`MechSys`/`AppInput`/`MechView` once their remaining
bytes are mapped, and for any new file cracked via the family-diffing approach.

**Don't look for a `.BND` runtime loader — there isn't one, confirmed.** Any future `.BND` session
should instead cross-reference a file's still-unknown fields against `dbsim-physics-notes.md`'s
per-subsystem documented constants (the same technique that cracked the "is it even used" question
above) before falling back to blind same-shape-family diffing — a field's real meaning may already
be sitting in that doc from the code-disassembly side, just not yet connected to its `.BND` source.
