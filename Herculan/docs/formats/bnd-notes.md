# .BND — per-subsystem tuning/config source files (SHELVED; CAM.BND confirmed build-time-only, never read by DBSIM.EXE at runtime)

83 entries in `SIMVOL0.VOL`'s `bnd\` folder, one per DBSIM subsystem; filenames map to `DBSIM.EXE` translation units (`ACTOR`, `ALERT`, `BULLET`, `CAM`, `DEBRIS`, `FIRE`, `MECH`, `MECHSYS`, `OBJLIST`, `ROCKET`, `TERRAIN`, `TS_PART`, `PWEAPONS`, etc.). Contents are 6–394 bytes; small per-module tuning/config records, not per-entity arrays.

All offsets below are content-relative — the start of what `VolEntry.RawBytes` holds. A copy unpacked by `ES2/VOL/extractVol.py` carries a further nine leading bytes belonging to the archive, not to this format; see [vol-archive.md](vol-archive.md).

**Status:** Shelved deliberately; not needed at runtime.

## No shared header

There is no envelope, no format marker and no record tag: the first byte of a `.BND` file's content
is already the first byte of its per-subsystem record, and its value differs between files (67
distinct values across the 83).

## CAM.BND's full 24-byte record

The Java source (`herc-works-mdk-main/ES2Core/.../data/file/bnd/{Cam,Mech,MechSys,AppInput,MechView}.java`) has **sample-value-annotated byte layouts** for 5 of the 83 files; for `CAM.BND` specifically it accounts for **every byte of the record**:

| Content offset | Field | Real value | C# property |
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

All 22 numeric fields but one match the Java author's sample values exactly. Offset 14 (`Unknown7`): author's notes say "50" but retail is `0x50` = 80 (likely hex transcription).

Implemented as `HercWorks.Core.Data.File.Bnd.Cam` + `HercWorks.Core.Io.Transform.Bnd.CamTransformer`, registered in `TransformerRegistry` by exact file name (`CAM.BND` — every other `.BND` file has an unrelated record shape). Round-trips byte-exact against real retail `CAM.BND`.

Field *meanings* are unconfirmed — `Distance1`/`Distance2`/`Value3`/`Value4` (2500, 30000, 500, 8000) are plausibly camera near/far or zoom-range values, but unverified. `Unknown3` (49 = ASCII `'1'`) appears at the same offset in `CAM`/`MECH`/`MECHSYS` — plausibly a shared format sub-version byte.

**Other Java-annotated files** (`MECH.BND`, `MECHSYS.BND`, `AppInput.BND`, `MechView.BND`):
- `MECH.BND`: first 8 bytes match Java notes exactly (242, 164, 51, 49, 12, 0, 42, 0); bytes 8+ diverge, likely per-mech-type array starting ~offset 8. Record 394 bytes total; only first 16 documented.
- `MECHSYS.BND`: 38-byte record; after first 5 bytes (241, 184, 35, 49, 75), stride `[UINT8 value][3×0x00]` at offsets 4,8,12,16,20,24,28 with values **75, 60, 45, 25, 18, 12, 6** (decreasing, distance/LOD tier?).
- `AppInput.BND`: offset 0 documented (=84); other 22 bytes unmapped.
- `MechView.BND`: offsets 0-1 documented only; body untouched.

## Build-time-only source format — values compiled into DBSIM.EXE, never read at runtime

Hardcoded instruction immediates in `dbsim-physics-notes.md` (rocket steering) and disassembly-found weapon range breakpoints (not yet written up in `damage-system.md`) match byte-exact values in their corresponding `.BND` files:
- `ROCKET.BND` at content offsets 6-7, 8-9, 14-15: `1280`, `3072`, `40000`
- `PWEAPONS.BND` at content offsets 58-65: `120, 360, 180, 1800` (contiguous)

**Conclusion:** `.BND` files are human/build-tool source format (likely compiled by `ES2/BATCH.EXE`) whose values are baked directly into `DBSIM.EXE`'s code at build time. The retail game never opens `.bnd` files; there is no runtime loader.

## Not applicable to runtime

- **Not part of the "Dynamix resource" envelope** (`dfn-hfn-dci.md`). `ACTOR.BND`, `MECH.BND`, `CAM.BND` do not start with `[typeId:uint16][0x0028:uint16]`.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| A universal 9-byte `.BND` envelope — `[0]=0x02`, `[1..2]` payload length, `[3..4]=0x0000`, `[5..8]` build stamp — followed by a 1-byte record tag | Those nine bytes are the VOL entry prefix, present on every entry of every type, and are absent from the content the game reads. The reading is convincing on an extracted `.BND` alone: the flag really is 0x02, the size field really does hold `fileSize - 10`, and `[3..4]` really is zero — because no `.BND` reaches 64 KB, so the size field's high half is always empty. The "build stamp" is the source file's MS-DOS date and time, which is why files built in the same batch share it. The "record tag" is just the record's first byte. See [vol-archive.md](vol-archive.md). |
| `CAM.BND`'s record is 25 bytes — one more than the Java notes account for | The 25th byte is the archive's per-entry trailer, which repeats the content's last byte. The record is 24 bytes. |

## Notes for future work

**Work is shelved.** If resumed: 

- Only 5 of 83 files have Java source doc comments (`Cam`, `Mech`, `MechSys`, `AppInput`, `MechView`). Check `herc-works-mdk-main/ES2Core/.../data/file/bnd/*.java` before hex-diffing.
- `CAM.BND` is fully decoded and implemented: `HercWorks.Core.Data.File.Bnd.Cam` + `Io.Transform.Bnd.CamTransformer` (registered in `TransformerRegistry`, round-trips its 24 content bytes byte-exact). Use as template.
- For other files: group by same payload-length, diff within family (e.g., `P*.BND` cockpit panels, `*_ALRT.BND` alert configs) — the approach that cracked `.DCI`.
- Cross-reference unknown fields against `dbsim-physics-notes.md`'s and `damage-system.md`'s per-subsystem constants (the technique that confirmed build-time-only).
- No runtime loader exists; don't search for one.
