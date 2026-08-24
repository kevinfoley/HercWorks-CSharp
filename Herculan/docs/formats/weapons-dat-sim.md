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
+0x22 (relative) 48 raw bytes (0x30)  -- Four decoded fields (see below); the rest undecoded. Two
                             bytes at relative offset 0x26 are zeroed in memory at runtime (not
                             real file data).
```

In-memory boundary confirmed: front block 0x00-0x11, sub-sphere/sub-mesh block 0x12-0x21, tail
0x22-0x51, runtime-only pointer at 0x52, self-index at 0x56. Total 0x58 (88) bytes.

## Decoded tail fields

Offsets are absolute in-memory (tail-relative = absolute − 0x22).

| Absolute | Field | Read by |
|---|---|---|
| `0x36` | energy fire threshold, low | `FUN_0040ecdc` |
| `0x38` | energy fire threshold, high | `FUN_0040ecdc` |
| `0x3a` | magazine size | `FUN_0040e140` |
| `0x3e` | `ProjDatIndex` | `MechLoadout_ConstructWeaponMounts` |

`0x36`/`0x38` decide when an energy mount will fire: `max(0x36, mount+0x7b)` when `0x36 < 0x38`,
otherwise `0x38`. `0x3a` is both the round count an ammunition mount powers up with and its cap
(ATC20 2000 … ATC100 500, MSL6/8/10/24 6/8/10/24). Both are covered in
[`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md).

## `+0x52` and `+0x56` — runtime-only, written by the loader

Neither is file data. `Weapons_LoadResourceTables` writes the record's own table index into `+0x56`
— which is what identifies the sim table and the shell catalog as sharing one 0-32 weapon id — and a
pointer from a 33-entry string array at `00498eb0` into `+0x52`. That pointer is the name a weapon
gauge prints, and it is **not** the shell catalog's name for the same id. See
[`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md#names--fun_0040e18c).

## `ProjDatIndex` (tail-relative offset 0x1c, absolute offset 0x3e) — SOLVED

Answers how a weapon id maps to a `PROJ.DAT` record. Read via `WeaponMountTemplate_GetByWeaponId` (`0x0040fe84`) and `MechLoadout_ConstructWeaponMounts` (`0x0040fff8`). Both `simvol0/dat/WEAPONS.DAT` and `SHELL0/GAM/WEAPONS.DAT` share the same 33-entry weapon-id indexing.

- **`0x21` (33) -- no `PROJ.DAT` lookup.** Real case: `ECM` (electronic-warfare, no projectile).
- **`0x22` (34) -- resolved via `Proj_LookupRecord(category=0/*Missile*/, secondaryKey)`**, a `(category, subtypeId)` search. Real cases: `MSL6`, `MSL8`, `MSL10`, `FLYMSL` (tube/rack missile launchers). The secondary key is the hardpoint's own ammunition type out of the mission file's second loadout array, resolved 2026-08-23 — see [`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md).
- **Otherwise -- direct flat array index into `PROJ.DAT`** (`index * 0x24 + ProjDat_RecordTable`, via `Proj_LookupRecordByIndex` at `0x0040ffb0`). Confirmed for all other real weapons.
- **0 for non-firing entries** (`NONE`, `LAEW`, `MINE`, `TARG`, `SHLD`, `TURB`, `ENRG`). Field is inert for passive stat-boost systems. `LAEW` coincidentally resolves to index 0 (`ATC20`).

Full weapon-id-to-index table: see `HercWorks.Core.Data.File.Dat.Sim.ProjectileData` doc comment.

## Remaining undecoded

- `Field0` (range/tier semantics unknown)
- Reused constant fields (`DepCount`, `SubSphereFlagRaw`) from `.DMG`/`.COL`
- Firing-sequence tuple details
- The 40 bytes of the tail outside the four fields above

Implementation: see `HercWorks.Core.Data.File.Dat.Sim.Weapons` and `HercWorks.Core.Io.Transform.Dbsim.WeaponsSimTransformer`.
