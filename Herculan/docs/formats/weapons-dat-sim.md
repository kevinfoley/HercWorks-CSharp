# simvol0/dat/WEAPONS.DAT (sim-side weapon mount template table)

Distinct from `SHELL0/GAM/WEAPONS.DAT` (the UI-facing weapon catalog, see `docs/formats/weapons-dat.md`).
This is DBSIM.EXE's runtime weapon-mount table, loaded by `Weapons_LoadResourceTables`
(`0x0040fc8c`) via a resource literally named `"weapons"`. **Fully SOLVED and verified byte-exact against the real retail file.**

## Structure

```
0x00  UINT16  Total          -- 33 in the real file, matches SHELL0/GAM/WEAPONS.DAT's catalog count
0x02  WeaponMountTemplate[Total]   -- variable-length records, back to back (NOT a fixed stride --
                                       see below)
```

### `WeaponMountTemplate` record (variable length)

Built entirely from **reused low-level record readers**:
`HercPiece_ReadRecord` (see `docs/simulation/damage-system.md`, "The component damage system"),
`Collision_LoadSubSphereFlag`, `Collision_LoadSubMeshIndices` (see `docs/simulation/dbsim-physics-notes.md`, "Collision system"). In-memory struct is 88 bytes (`0x58`), but on-disk record is variable-length; extra in-memory bytes are runtime-only (a pointer + self-index the loader fills in after reading).

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
                                  entry is 4 int16s. Pattern suggests (offsetish, offsetish,
                                  0-or-small, rate-ish) tuples, plausibly muzzle offset + fire-rate
                                  for multi-shot weapons (not confirmed field-by-field).
+0x22 (relative) 48 raw bytes (0x30)  -- Mostly undecoded, EXCEPT ProjDatIndex at tail-relative
                             offset 0x1c (see below). Two bytes at relative offset 0x26 are
                             zeroed in memory at runtime (not real file data).
```

In-memory boundary confirmed: front block 0x00-0x11, sub-sphere/sub-mesh block 0x12-0x21, tail
0x22-0x51, runtime-only pointer at 0x52, self-index at 0x56. Total 0x58 (88) bytes.

## `ProjDatIndex` (tail-relative offset 0x1c, absolute offset 0x3e) — SOLVED

Answers how a weapon id maps to a `PROJ.DAT` record. Read via `WeaponMountTemplate_GetByWeaponId` (`0x0040fe84`) and `MechLoadout_ConstructWeaponMounts` (`0x0040fff8`). Both `simvol0/dat/WEAPONS.DAT` and `SHELL0/GAM/WEAPONS.DAT` share the same 33-entry weapon-id indexing.

- **`0x21` (33) -- no `PROJ.DAT` lookup.** Real case: `ECM` (electronic-warfare, no projectile).
- **`0x22` (34) -- resolved via `Proj_LookupRecord(category=0/*Missile*/, secondaryKey)`**, a `(category, subtypeId)` search. Real cases: `MSL6`, `MSL8`, `MSL10`, `FLYMSL` (tube/rack missile launchers).
- **Otherwise -- direct flat array index into `PROJ.DAT`** (`index * 0x24 + ProjDat_RecordTable`, via `Proj_LookupRecordByIndex` at `0x0040ffb0`). Confirmed for all other real weapons.
- **0 for non-firing entries** (`NONE`, `LAEW`, `MINE`, `TARG`, `SHLD`, `TURB`, `ENRG`). Field is inert for passive stat-boost systems. `LAEW` coincidentally resolves to index 0 (`ATC20`).

Full weapon-id-to-index table: see `HercWorks.Core.Data.File.Dat.Sim.ProjectileData` doc comment.

## Remaining undecoded

- `Field0` (range/tier semantics unknown)
- Reused constant fields (`DepCount`, `SubSphereFlagRaw`) from `.DMG`/`.COL`
- Firing-sequence tuple details
- Other 46 bytes of the 48-byte tail

Implementation: see `HercWorks.Core.Data.File.Dat.Sim.Weapons` and `HercWorks.Core.Io.Transform.Dbsim.WeaponsSimTransformer`.
