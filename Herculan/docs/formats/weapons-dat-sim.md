# simvol0/dat/WEAPONS.DAT (sim-side weapon mount template table)

Distinct from `SHELL0/GAM/WEAPONS.DAT` (the UI-facing weapon catalog, see `docs/formats/weapons-dat.md`).
This is DBSIM.EXE's own runtime weapon-mount table, loaded by `Weapons_LoadResourceTables`
(`0x0040fc8c`) via a resource literally named `"weapons"`. Previously flagged "investigated but not
implemented" in project memory (`project_es2_translation_status`) after hex-only inspection stalled.
Cracked 2026-08-11 by finding and decompiling the real loader in Ghidra (DBSIM.EXE), the same
technique that worked for `.GAU`'s widget offsets — **not** by further hex-guessing.

## Structure — confirmed byte-exact against the real retail file

Verified with a throwaway console probe that walks the real file
(`ES2/VOL/simvol0/dat/WEAPONS.DAT`, 3790 content bytes after stripping the 9-byte VOL prefix and
1-byte trailing marker) using exactly the read sequence found in the disassembly below: **the
parse consumes all 3790 content bytes across the file's 33 records with zero remainder.** That
exact-consumption result is the strongest evidence this project uses for "structure confirmed."

```
0x00  UINT16  Total          -- 33 in the real file, matches SHELL0/GAM/WEAPONS.DAT's catalog count
0x02  WeaponMountTemplate[Total]   -- variable-length records, back to back (NOT a fixed stride --
                                       see below)
```

### `WeaponMountTemplate` record (variable length)

Found by decompiling `WeaponMountTemplate_ReadRecord` (`0x0040f8bc`), the per-record reader
`Weapons_LoadResourceTables` calls in its load loop. It turns out to be built entirely out of
**reused calls into the exact same low-level record readers `.DMG`/`.COL` already use**
(`HercPiece_ReadRecord`, `Collision_LoadSubSphereFlag`, `Collision_LoadSubMeshIndices` — see
`docs/simulation/dbsim-physics-notes.md`), which is why the shape looks unrelated to a "weapon"
record at first glance. The in-memory struct is a fixed 88 bytes (`0x58`, matching the doc
comment's "88-byte per-hardpoint mount-template record" that named this resource before it was
traced), but the **on-disk** record is shorter and variable-length — the extra in-memory bytes are
runtime-only (a pointer + self-index the loader fills in after reading, not present in the file).

Read order (all fields little-endian):

```
+0   short Field0        -- 0 for id0/NONE; one of {1500, 2000, 2500, 15000} for every real weapon
                             seen -- too few distinct values to be a per-weapon-unique stat, plausibly
                             a range/tier bucket. NOT decoded further.
+2   short Field1        -- 0 for NONE; exactly -1 (0xFFFF) for every real weapon seen.
+4   short Field2        -- 0 for NONE; exactly 0x01FF (511) for every real weapon seen.
+6   short DepCount       -- 0 for NONE, 1 for every real weapon seen.
     DepCount*4 bytes     -- DepCount raw 16-bit pairs, present only if DepCount != 0. Always
                             exactly (20, 12) in every real weapon record seen. Semantics unknown --
                             this is HercPiece_ReadRecord's "dependent sub-component list" mechanism
                             reused generically; for weapons it never varies, so it isn't obviously
                             a real per-weapon list despite the mechanism supporting one.
     short SubSphereFlagRaw   -- read via Collision_LoadSubSphereFlag; constant 0x13 (19) in EVERY
                                  real record seen, including id0/NONE. Not the boolean "flag" the
                                  function's own name suggests when reused here -- semantics unknown.
     short SubMeshCountRaw    -- read via Collision_LoadSubMeshIndices; real count is this value
                                  masked with 0x1FFF (top 3 bits are reserved for flags in the
                                  original collision-record format; never observed set here). 0 for
                                  NONE.
     (SubMeshCountRaw & 0x1FFF) * 8 bytes  -- present only if the masked count != 0. Each 8-byte
                                  entry is 4 raw int16s. Real values look like (offsetish, offsetish,
                                  0-or-small, rate-ish) tuples with a strong resemblance to the
                                  original Java doc comment's guessed "SEQ" firing-sequence array
                                  (per-shot muzzle offset + fire-rate data for multi-shot/chain
                                  weapons) -- plausible but NOT confirmed field-by-field.
+0x22 (relative to the block start after the above) 48 raw bytes (0x30) -- read as one flat block.
                             Mostly undecoded, EXCEPT tail-relative offset 0x1c -- see "ProjDatIndex,
                             solved" below. Two bytes at relative offset 0x26 within this block get
                             force-zeroed in memory immediately after the read (a runtime-only reset,
                             not real file data at that position -- preserved as read here since this
                             port doesn't replicate DBSIM's in-memory-only side effect).
```

Confirmed directly from `WeaponMountTemplate_ReadRecord`'s own disassembly (`0x0040f8bc`): the tail
is read straight into **absolute in-memory offset 0x22** of the 88-byte struct (not some separately
computed relative offset as the original prose above implied) -- `(**(code**)(*param_2+0x18))
(param_2, 0x30, param_1+0x22)`. That pins every absolute in-memory offset exactly: front block
0x00-0x11 (`HercPiece_ReadRecord`), sub-sphere/sub-mesh block 0x12-0x21 (`Collision_LoadSubSphereFlag`),
tail 0x22-0x51, runtime-only pointer at 0x52, runtime-only self-index at 0x56 -- total 0x58 (88)
bytes, matching the confirmed stride exactly.

## `ProjDatIndex` (tail-relative offset 0x1c, absolute in-memory offset 0x3e) -- SOLVED 2026-08-11

This is the field that answers the project's long-open "how does a real weapon id map to a
`PROJ.DAT` record" question -- found by tracing `WeaponMountTemplate_GetByWeaponId`'s (`0x0040fe84`)
one real caller, `MechLoadout_ConstructWeaponMounts` (`0x0040fff8`, itself called from
`Mech_ConfigureLoadout`/`0x004175dc`, DBSIM's mech-loadout-(re)configuration entry point).

`MechLoadout_ConstructWeaponMounts` walks a mech's hardpoint-slot table (stride `0x1a`/26 bytes);
for each occupied slot it looks up the slot's real catalog weapon id, fetches that weapon's
`WeaponMountTemplate` via a **direct flat array index** (`weaponId * 0x58 + base`, confirmed via
`WeaponMountTemplate_GetByWeaponId`'s disassembly -- this also confirms, for the first time, that
`simvol0/dat/WEAPONS.DAT` and `SHELL0/GAM/WEAPONS.DAT` share one 33-entry order/indexing scheme by
weapon id, not just an equal count), reads that template's tail-relative-0x1c field into a local,
and branches on it:

- **`0x21` (33) -- no `PROJ.DAT` lookup at all.** Confirmed real-world case: only `ECM`'s template
  carries this literal sentinel (an electronic-warfare mount with no projectile of its own).
- **`0x22` (34) -- resolved via `Proj_LookupRecord(category=0/*Missile*/, secondaryKey)`**, a
  `(category, subtypeId)` search rather than a direct index. The `secondaryKey` comes from a
  *different* per-hardpoint-slot parallel table (`MechLoadout_ConstructWeaponMounts`'s own
  `param_6`), not from anything in `WEAPONS.DAT` itself -- so which of `PROJ.DAT`'s remaining
  Missile/Rocket entries a given hardpoint resolves to isn't fully traceable from this file alone.
  Confirmed real-world case: exactly `MSL6`/`MSL8`/`MSL10`/`FLYMSL` (the 4 tube/rack-style missile
  launchers), consistent with a single catalog "launcher" entry being able to carry different
  submunition records depending on mount/loadout variant.
- **Otherwise -- a direct flat array index into `PROJ.DAT`** (`Proj_LookupRecordByIndex`,
  `0x0040ffb0`: `index * 0x24 + ProjDat_RecordTable`). Confirmed byte-exact for every other real
  catalog weapon -- see `HercWorks.Core.Data.File.Dat.Sim.ProjectileData`'s doc comment for the
  full resulting index-to-weapon table, cross-checked with a throwaway `dotnet run` probe that
  joins the real retail `WEAPONS.DAT` (sim), `WEAPONS.DAT` (SHELL0 catalog, for real names), and
  `PROJ.DAT`.
- A handful of non-firing catalog entries (`NONE`, `LAEW`, `MINE`, `TARG`, `SHLD`, `TURB`, `ENRG`)
  carry an **all-zero placeholder template** (byte-identical trailing tail across all of them) whose
  field here reads `0` -- a coincidentally "valid" index, but confirmed (by reading
  `MechLoadout_ConstructWeaponMounts`'s own per-weapon-id switch, which selects the actual mount C++
  class/vtable) that the constructors used for `TARG`/`SHLD`/`TURB`/`ENRG` never even receive the
  resolved `PROJ.DAT` pointer as an argument -- these are passive stat-boost systems, not firing
  weapons, so the field is simply inert for them. `LAEW`'s constructor *does* receive it, so it
  genuinely (if perhaps unintentionally) resolves to `PROJ.DAT` index 0 (`ATC20`'s record).

Observed (not yet decoded) patterns worth chasing if this is picked up again:
- The 48-byte tail's first 8 bytes are frequently four *equal* 16-bit values (e.g. `4,4,4,4` or
  `12,12,12,12`) for many records, but for others (e.g. records 8-12 in the real file) they're four
  *distinct* small values that look like a `(n, n+2, category, n+1)` pattern — plausibly
  cross-references to other `WeaponMountTemplate` records (multi-barrel/linked-mount weapons) or to
  `PROJ.DAT` indices. Not traced to a consumer function yet.
- A `0x01F4` (500) UINT32 shows up constantly at relative offset 8 within the 48-byte tail for most
  records; the 3 records whose `Field0` is 15000 instead show that same 15000 value there — i.e.
  this may be `Field0`'s value re-encoded as a 32-bit field, with 500 as an unrelated coincidentally
  common separate value, not confirmed.

## How to apply

The structural/boundary decode is done and verified byte-exact — safe to build a transformer
directly from the read order above (see `HercWorks.Core.Data.File.Dat.Sim.Weapons` /
`HercWorks.Core.Io.Transform.Dbsim.WeaponsSimTransformer`). **`ProjDatIndex` (tail-relative 0x1c) is
now SOLVED** — see the dedicated section above; it's exposed as `WeaponMountTemplate.ProjDatIndex`
in the C# model and is the mechanism behind `ProjectileData`'s confirmed weapon-to-record mapping.
The remaining gap is the rest of the semantics: `Field0` (range/tier?), the constant
`DepCount`/`SubSphereFlagRaw` fields (mechanism reused from `.DMG`/`.COL` but not obviously
meaningful for a weapon), the firing-sequence tuples, and the other 46 bytes of the tail (including
the `0x01F4`/`Field0`-echo pattern at tail-relative offset 8). If resumed, the same "find the real
consumer function via `ES2FindAddressRefs` on the record array, decompile it, don't guess from
shape" technique that cracked `ProjDatIndex` and the boundary itself is the natural next
step — that consumer function (`MechLoadout_ConstructWeaponMounts`) is now found and traced for the
`ProjDatIndex` field specifically, but its other arguments (the full per-hardpoint slot record
shape, and the `param_6` secondary-key table the `0x22` case reads) weren't fully mapped this
session and are the most direct route to the remaining tail bytes.
