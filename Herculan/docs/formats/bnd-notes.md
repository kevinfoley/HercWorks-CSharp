# .BND — per-subsystem tuning/config source files (SHELVED; envelope + CAM.BND confirmed build-time-only, never read by DBSIM.EXE at runtime)

83 files in `ES2/VOL/simvol0/bnd/`, one per DBSIM subsystem; filenames map to `DBSIM.EXE` translation units (`ACTOR`, `ALERT`, `BULLET`, `CAM`, `DEBRIS`, `FIRE`, `MECH`, `MECHSYS`, `OBJLIST`, `ROCKET`, `TERRAIN`, `TS_PART`, `PWEAPONS`, etc.). Files are 16–404 bytes; small per-module tuning/config records, not per-entity arrays.

**Status:** This work was deliberately shelved per user request (2026-08-11). The core finding stands: `.BND` is a human/build-tool source format whose values are compiled directly into `DBSIM.EXE`'s code at build time (`ES2/BATCH.EXE` is the likely compiler); the retail game never opens `.bnd` files at runtime.

## Solved: universal 9-byte envelope + 1-byte record tag (byte-exact across all 83 files)

```
offset 0x00        byte    0x02              constant format/record-type marker (100% of files)
offset 0x01-0x02   uint16  payloadLen = fileSize - 10, little-endian, verified with ZERO exceptions
offset 0x03-0x04   uint16  0x0000            reserved/padding, constant 0 in all 83 files
offset 0x05-0x08   4 bytes                   build/batch stamp (see below) — not yet fully decoded
offset 0x09        byte    recordTag         first byte of the per-subsystem record (see below)
offset 0x0a..end   payloadLen bytes          per-subsystem record body, decoded for CAM.BND only
```

**Verified:** `byte[0]==0x02`, `byte[3..4]==0x0000`, and `payloadLen==fileSize-10` hold across all 83 files. Smallest files (`FLAT.BND`/`GNDTEX.BND`/`LIGHTS.BND`/`TS_PART.BND`) 16 bytes; largest (`MECH.BND`) 404 bytes. The "394 bytes" in earlier notes is `payloadLen` (file = 9-byte envelope + 1-byte recordTag + 394-byte payload).

**The offset-0x09 byte is the first byte of the per-subsystem record**, not part of the build stamp. Confirmed by Java source `org.hercworks.core.data.file.bnd`'s doc comments: offsets in `Cam.java`/`Mech.java`/`MechSys.java`/`AppInput.java` all line up here, not one byte further.

**Bytes `0x05`-`0x08` (4-byte "build stamp"):** not a Unix timestamp. Files cluster into groups sharing the same stamp with sequential recordTag values — e.g. `ROCKET.BND` stamp=`3b20ef7a` tag=`52`, `PSTATUS.BND` stamp=`3b20ef7a` tag=`53`, `APPINPUT.BND`/`PHDDDAMG.BND` stamp=`3b20ef7a` tag=`54`, `PMISSILE.BND` stamp=`3b20ef7a` tag=`55`. Signature of an **offline batch build tool** — consistent with `ES2/BATCH.EXE`, though no `"bnd"` text found in that binary. Grouping reflects original source/build-script ordering, not recoverable from shipped files alone.

## Solved: CAM.BND's full 25-byte record (envelope's recordTag + 24-byte payload)

The Java source (`herc-works-mdk-main/ES2Core/.../data/file/bnd/{Cam,Mech,MechSys,AppInput,MechView}.java`) has **sample-value-annotated byte layouts** for 5 of the 83 files; for `CAM.BND` specifically it accounts for **every byte in the file**:

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

21 of 22 numeric fields match the Java author's sample values exactly. Offset 14 (`Unknown7`): author's notes say "50" but retail is `0x50` = 80 (likely hex transcription).

Implemented as `HercWorks.Core.Data.File.Bnd.Cam` + `HercWorks.Core.Io.Transform.Bnd.CamTransformer`, registered in `TransformerRegistry` by exact file name (`CAM.BND` — every other `.BND` file has an unrelated record shape). Round-trips byte-exact against real retail `CAM.BND`.

Field *meanings* are unconfirmed — `Distance1`/`Distance2`/`Value3`/`Value4` (2500, 30000, 500, 8000) are plausibly camera near/far or zoom-range values, but unverified. `Unknown3` (49 = ASCII `'1'`) appears at the same offset in `CAM`/`MECH`/`MECHSYS` — plausibly a shared format sub-version byte.

**Other Java-annotated files** (`MECH.BND`, `MECHSYS.BND`, `AppInput.BND`, `MechView.BND`):
- `MECH.BND`: first 8 bytes match Java notes exactly (242, 164, 51, 49, 12, 0, 42, 0); bytes 8+ diverge, likely per-mech-type array starting ~offset 8. Record 395 bytes total; only first 16 documented.
- `MECHSYS.BND`: 39-byte record; after first 5 bytes (241, 184, 35, 49, 75), stride `[UINT8 value][3×0x00]` at offsets 4,8,12,16,20,24,28 with values **75, 60, 45, 25, 18, 12, 6** (decreasing, distance/LOD tier?).
- `AppInput.BND`: offset 0 documented (=84); other 22 bytes unmapped.
- `MechView.BND`: offsets 0-1 documented only; body untouched.

## Solved: .BND is a build-time-only source format — values compiled into DBSIM.EXE, never read at runtime

Hardcoded instruction immediates in `dbsim-physics-notes.md` (rocket steering) and disassembly-found weapon range breakpoints (not yet written up in `damage-system.md`) match byte-exact values in their corresponding `.BND` files:
- `ROCKET.BND` at offsets 15-16, 17-18, 23-24: `1280`, `3072`, `40000`
- `PWEAPONS.BND` at offset 67-74: `120, 360, 180, 1800` (contiguous)

**Conclusion:** `.BND` files are human/build-tool source format (likely compiled by `ES2/BATCH.EXE`, consistent with the build-batch stamp in the envelope above) whose values are baked directly into `DBSIM.EXE`'s code at build time. The retail game never opens `.bnd` files; there is no runtime loader.

## Not applicable to runtime

- **Not part of the "Dynamix resource" envelope** (`dfn-hfn-dci.md`). `ACTOR.BND`, `MECH.BND`, `CAM.BND` do not start with `[typeId:uint16][0x0028:uint16]`. The 10-byte envelope documented here does not match `ClassItem` shape.

## Notes for future work

**Work is shelved.** If resumed: 

- Only 5 of 83 files have Java source doc comments (`Cam`, `Mech`, `MechSys`, `AppInput`, `MechView`). Check `herc-works-mdk-main/ES2Core/.../data/file/bnd/*.java` before hex-diffing.
- `CAM.BND` is fully decoded and implemented: `HercWorks.Core.Data.File.Bnd.Cam` + `Io.Transform.Bnd.CamTransformer` (registered in `TransformerRegistry`, round-trips byte-exact). Use as template.
- For other files: group by same payload-length, diff within family (e.g., `P*.BND` cockpit panels, `*_ALRT.BND` alert configs) — the approach that cracked `.DCI`.
- Cross-reference unknown fields against `dbsim-physics-notes.md`'s and `damage-system.md`'s per-subsystem constants (the technique that confirmed build-time-only).
- No runtime loader exists; don't search for one.
