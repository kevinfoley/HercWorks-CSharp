# .DGS and .HD0 formats

Partial investigation of `.DGS` and `.HD0` formats. Companion: [`weapons-dat-sim.md`](weapons-dat-sim.md).

## `.DGS` — two real files, genuinely different formats

Loader code in DBSIM.EXE: `FUN_00405ebc` (model-instance lookup/cache) and `FUN_00405fac`
(resource-load sequence: `"bforms"`, `"lc_wpns"`, `"bases"`, `"basecol"`, `"bhulks"`, `"basetex"`, `"vehtex"`).

**`BHULKS.DGS`:** Likely DTS-model-family; opened via `FUN_00474bcc` (same "load 3D model" call as
confirmed DTS `"mechwpn2"`). Header bytes share `FF-FF-00-00` sentinel at byte 8 but differ in leading fields:
- `BHULKS.DGS`: `01-00-BC-02-68-0E-00-00-FF-FF-00-00-93-26-CB-FC`
- `SAMSON.DTS`: `03-00-1E-00-FE-46-00-00-FF-FF-00-00-05-08-D4-FF`

Sentinel position (byte 8) likely non-coincidental. Not byte-identical; DTS-*family* plausible.

**`BASES.DGS`** (565,882 bytes): Decompiled `FUN_00405fac` hypothesizes `[uint16 count] → 60-byte
records with nested 30-byte sub-records`. Parsing yields `count=1` — impossible for 565KB file.
**Hypothesis disproven.** Real file shows `FF-FF-00-00` sentinel at record-relative offset 6–9
near start (~78-byte span), consistent with (unconfirmed) 22-byte-record hypothesis.

**Next steps if resumed:** For `BASES.DGS`, trace `FUN_00405ebc`'s model-cache arrays
(`DAT_004a9600`/`DAT_004a95f8`). For `BHULKS.DGS`, find caller of `FUN_00474bcc("bhulks", ...)`
to extract mode/version flags; compare against `mechwpn2` and `.DTS` header field meanings.

## `.HD0`/`.HD1`/`.HD2`/`.HD3` — no loader found

No literal `"hd0"` / `"hd1"` / `".hd"` string in DBSIM.EXE. `.HD0` paired 1:1 with `.HB0`
cockpit-textures (`SAMSON.HD0`/`SAMSON.HB0`, etc.). Likely approach: trace `.HB0`'s confirmed
loader (herc-name + extension, like `.GAU`/`.DMG`) to find `.HD0` sibling.

Previous finding (hex-only, pre-Ghidra): long run of `[UINT16][UINT16]` pairs (one counts up,
one counts down) — suggestive of gradient/remap table; only extant evidence.
