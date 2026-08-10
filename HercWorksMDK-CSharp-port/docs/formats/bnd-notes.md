# .BND — per-subsystem tuning constants (investigated, NOT solved)

83 real files in `ES2/VOL/simvol0/bnd/`, one per major DBSIM subsystem — filenames map directly
onto `DBSIM.EXE`'s own translation-unit names found via disassembly (`ACTOR`, `ALERT`, `BULLET`,
`CAM`, `DEBRIS`, `FIRE`, `MECH`, `MECHSYS`, `OBJLIST`, `ROCKET`, `TERRAIN`, `TS_PART`, `PWEAPONS`,
etc. — see `project_es2_exe_recon` memory's DBSIM source-file list). Roughly 20+ distinct content
"flavors" are expected (previously judged, correctly, too large for one sitting in the
data-only investigation phase — see `project_es2_translation_status` memory).

## Ruled out

- **Not part of the "Dynamix resource" envelope** documented in `dfn-hfn-dci.md` — checked
  `ACTOR.BND`, `MECH.BND`, `CAM.BND` directly against real bytes; none start with
  `[typeId:uint16][0x0028:uint16]` after the 9-byte VOL prefix. Whatever generic mechanism loads
  `.BND` files, it isn't the same `ClassItem` registry used for `.DFN`/`.HFN`/`.DCI`.
- `MECH.BND`'s real content is 394 bytes — the ported Java/C# `Mech.cs` doc comment's "16
  bytes" claim is stale/wrong for real data (already flagged in `project_es2_translation_status`
  memory; repeating here since it's directly relevant).

## Suggestive but unconfirmed

- Each subsystem name (`MECH`, `ROCKET`, `ACTOR`, `BULLET`, `CAM`, `FIRE`, `DEBRIS`, etc.) exists
  as a short, standalone string literal in `DBSIM.EXE`, each one physically near that subsystem's
  own code (e.g. `"ROCKET"` sits ~340 bytes after `rocket.cpp`'s main function) rather than
  clustered together in one shared table — consistent with each subsystem registering itself by
  name at static-init time (the same architectural pattern as the `.DFN`/`.DCI` `ClassItem`
  registry, just evidently a *different* registry/mechanism specific to tuning data). A direct
  address-reference search on these class-name strings (`ES2FindAddressRefs.java`) came back
  **empty** for `"MECH"`. **The CODE-section file-offset→VA conversion was independently
  re-verified and confirmed correct** (cross-checked raw file bytes at offset `0x600` against
  Ghidra's disassembly at VA `0x401000` — byte-for-byte identical instructions), so this is a
  genuine empty result, not a formula bug: the `"MECH"` string really isn't referenced by any
  direct instruction operand anywhere in the binary. Same for `"MECH_TYPE_DATA[]"` (also checked,
  also empty). Most likely explanation: these are debug/RTTI-style metadata strings (e.g. from a
  `DECLARE_CLASS(name)`-style macro) reached only through a pointer-arithmetic/table lookup that
  Ghidra's static reference analysis can't resolve, or genuinely unused dead metadata — not a
  useful anchor for finding the actual `.BND` loader via this method.
- The string content itself is still a hint even though it's unreferenced: `"MECH_TYPE_DATA[]"`
  (and `"MECH_TYPE_DATA ( *)[]"`, `"MECH_TYPE_DATA"`) — the `[]` suffix suggests `MECH.BND`'s
  content might be an **array** of a `MECH_TYPE_DATA` struct (one entry per mech/herc type?)
  rather than one flat tuning blob, which would explain why 394 bytes didn't look like a single
  record. 394 doesn't divide evenly by 21 (the real herc/mech count from `MECHS.NAM`), so if this
  is right the per-entry size or entry count isn't simply "21 mechs" — not resolved, and now that
  the string itself is confirmed a dead end for finding the *loader*, this is just a naming hint,
  not an anchor.

## How to apply

Don't assume the `.DFN`/`.DCI` `ClassItem` technique transfers directly — it doesn't, based on
what's checked so far. Don't chase the class-name strings (`MECH`, `MECH_TYPE_DATA[]`, etc.) as a
way to *find* the loader function either — confirmed dead end, not just unlucky. The actual next
step would be finding the loader a different way: e.g. search for functions that read a `.bnd`
file by constructing `"<name>.bnd"` at runtime (the same technique that worked for
`gam\weapons.dat` etc. — check what calls the shared extension-lookup table entry for `"bnd"`
found in `project_es2_exe_recon` memory), or just start from a real file's bytes and look for
plausible record boundaries the way earlier data-only sessions did for `.DGS`. Given the size of
this format family (83 files, ~20+ flavors), plan for a dedicated session per a small batch of
related flavors rather than trying to crack all 83 files at once.
