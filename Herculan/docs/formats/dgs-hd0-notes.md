# .DGS and .HD0 — investigation notes (2026-08-11)

Companion to `docs/formats/weapons-dat-sim.md` (the sim-side `WEAPONS.DAT`, fully solved this
session). `.DGS` and `.HD0` were investigated with the same "find the real loader in Ghidra,
verify byte-exact against real files" technique, following the earlier `.GAU` playbook. Neither
reached a full crack — recorded here so a future session has the real findings instead of
restarting from the old byte-pattern-only guesses in `project_es2_translation_status` memory.

## `.DGS` — two real files, genuinely different formats, only one lead partially chased

String search on `"bases"`/`"bases_an"`/`"bhulks"` (the two real filenames, `BASES.DGS` and
`BHULKS.DGS`) found real loader code in DBSIM.EXE's `base.cpp` module: `FUN_00405ebc` (per-object
model-instance lookup/cache) and `FUN_00405fac` (the module's big resource-loading routine, opens
`"bforms"`, `"lc_wpns"`, `"bases"`, `"basecol"`, `"bhulks"`, `"basetex"`, `"vehtex"` in sequence,
each its own separate file/stream).

**`BHULKS.DGS` is very likely a DTS-model-family container, not a flat record table** — its
resource is opened via `FUN_00474bcc`, the exact same "load a 3D model resource" call
`Weapons_LoadResourceTables` uses for `"mechwpn2"` (a confirmed DTS-format resource). Tried parsing
the real file directly with the existing `DTSModelTransformer` — it threw
(`ArgumentOutOfRangeException`), so it is **not** byte-identical to plain `.DTS`. But the header
bytes are suggestive: `BHULKS.DGS` content starts `01-00-BC-02-68-0E-00-00-FF-FF-00-00-93-26-CB-FC`
versus a real `.DTS` (`SAMSON.DTS`) starting `03-00-1E-00-FE-46-00-00-FF-FF-00-00-05-08-D4-FF` — the
`FF-FF-00-00` sentinel lands at the exact same byte offset (8) in both, which is unlikely to be
coincidental, but the two leading fields differ in both position and value, so this is DTS-*family*
at best, not a drop-in match. Not pursued further this session.

**`BASES.DGS` does NOT match the `FUN_00405fac`-decompiled `"bases"` stream-read block** — that
block reads a UINT16 count then a loop of nominally-60-byte (`0x3c`) records, each with an embedded
variable-length sub-list (a count at record-relative offset 18, `subCount` sub-records of 30 bytes
each). Simulating that exact read sequence against the real `BASES.DGS` file (565882 content bytes)
gives `count=1`, which is obviously wrong for a 565KB file — the hypothesis fails real-data
verification immediately, so this is a genuine negative result, not just "unverified." Two
explanations seem plausible, neither chased down: (1) the `"bases"` string in `FUN_00405fac` opens
a *different*, smaller file than `BASES.DGS` itself (this project's resource-name-to-filename
mapping isn't always 1:1 — `weapons.bin` vs `WEAPONS.DAT` is a precedent for two files sharing a
"weapon"-ish name but being genuinely different), or (2) the decompiled read sequence itself is
subtly wrong (the source had several places where Ghidra reused one variable name, `piVar6`, across
what look like two unrelated local variables in the original C++ — very likely just stack-slot
reuse across non-overlapping lifetimes, a normal compiler artifact, but not independently confirmed
here). Real `BASES.DGS` bytes *do* still show the `FF-FF-00-00` sentinel pattern the previous
(pre-Ghidra) hex-only investigation noted, at record-relative offset 6-9 of an ~78-byte span near
the start of the file — consistent with, but not proof of, that older 22-byte-record hypothesis
being right at a different granularity than first assumed.

**How to apply:** don't re-attempt the flat 60-byte-record reading of `BASES.DGS` as-is — it's
disproven, not just unconfirmed. If resumed, the highest-value next step is finding what function
actually *reads* real `BASES.DGS` bytes by tracing forward from `FUN_00405ebc`'s
`DAT_004a9600`/`DAT_004a95f8` model-cache arrays (the ones indexed by the per-object id in the
`.DGS`-named placement data) rather than re-trusting the `"bases"`-string-adjacent block in
`FUN_00405fac`, since that block's own count field already fails the real-file check. For
`BHULKS.DGS`, the next step would be finding the real caller of `FUN_00474bcc("bhulks", ...)` to
see what mode/version flags it passes, and comparing those against `mechwpn2`'s and `.DTS`'s own
confirmed header field meanings — the shared `FF-FF-00-00` sentinel offset is a real, non-coincidental
lead worth following from the field-meaning side rather than more raw hex diffing.

## `.HD0`/`.HD1`/`.HD2`/`.HD3` — no loader found this session

A string search for `"hd0"`, `"hd1"`, `".hd"` in DBSIM.EXE found **zero hits** — unlike `.DGS`,
there is no literal filename/extension string to pivot from directly. `.HD0` is paired 1:1 with the
already-solved `.HB0` cockpit-texture files (`SAMSON.HD0`/`SAMSON.HB0` etc.), so the likely next
approach is tracing forward from `.HB0`'s own confirmed loader (opened via a herc-name + extension
pattern, same as `.GAU`/`.DMG`) to find whatever sibling call opens the same herc's `.HD0` — not
attempted this session. The previous (pre-Ghidra) hex-only finding — a long run of `[UINT16][UINT16]`
pairs where one value counts up while the paired value counts down, suggestive of a gradient/remap
table — is still the only evidence on record; nothing here changes or corroborates it.
