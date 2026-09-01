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
`Collision_ReadCluster`, `Collision_ReadSphereArray` (see `docs/simulation/hit-detection.md`). In-memory struct is 88 bytes (`0x58`), but on-disk record is variable-length; extra in-memory bytes are runtime-only (a pointer + self-index the loader fills in after reading).

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
     short SubSphereFlagRaw   -- read via Collision_ReadCluster; constant 0x13 (19) in EVERY
                                  real record seen, including id0/NONE. In a real collision model
                                  this field is the component index; here it never varies, so its
                                  meaning for a weapon is unknown.
     short SubMeshCountRaw    -- read via Collision_ReadSphereArray; real count is this value
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
| `0x30` | **range**, int32, in world units | `WeaponMount_FireDispatch_GunBeam` |
| `0x36` | energy fire threshold, low | `WeaponMount_EnergyCanFire` |
| `0x38` | energy fire threshold, high, **and the per-shot cost** — for an ammunition mount, rounds per shot | `WeaponMount_EnergyCanFire`, both fire dispatchers |
| `0x3a` | magazine size | `FUN_0040e140` |
| `0x3c` | barrel count; `3` fires three shots spread along the muzzle offset's own X | `WeaponMount_FireDispatch_GunBeam` |
| `0x3e` | `ProjDatIndex` | `MechLoadout_ConstructWeaponMounts` |
| `0x40`–`0x44` | muzzle offset, three int16, in the firing bone's space | `WeaponMount_PrepareShot` |
| `0x46` | lateral muzzle offset, for a side-mounted hardpoint | `WeaponMountTemplate_SideMuzzleOffset` |
| `0x4a` | vertical muzzle offset, for a top- or bottom-mounted one | `WeaponMountTemplate_SideMuzzleOffset` |
| `0x4c` | refire delay, in sim timer units | `WeaponMount_PrepareShot` |

`0x30` is the ray length the beam dispatch hands `Bullet_FireBurst`, which is what identifies it; it
was previously known only as the value `FUN_004110ac` requires to be positive before it will put a
hardpoint into a fire chain, and every pod carries zero, so that gate still works. Retail values run
75000 (ATC20) down to 15000 (ELF2) — 450 m to 90 m at the simulation's own scale, which does *not*
match the manual's 20 m figure for the ELF.

`0x36`/`0x38` decide when an energy mount will fire: `max(0x36, mount+0x7b)` when `0x36 < 0x38`,
otherwise `0x38`. `0x38` is also what a shot costs, so the two shapes real data takes — equal pair
(LAS100 80/80) versus small low against a 10000 high (PBEAM 300/10000) — are a fixed-cost weapon and
a charge-up one. `0x3a` is both the round count an ammunition mount powers up with and its cap
(ATC20 2000 … ATC100 500, MSL6/8/10/24 6/8/10/24), and the ammunition dispatch spends `0x38` rounds
per shot.

`0x4c` is 1200 on most weapons — about 15 sim ticks — and **zero on `ELF` and `ELF2`**, which is what
makes those two continuous beams. The mount scales it by its own `+0x63`, a constant `0x400`.

`0x3c` is 1 everywhere except catalog id 19 (the big EMP), where it is 3. `0x3e == 0x13` is true for
exactly one weapon too — id 23, `EMP2` — because the value is that weapon's own `PROJ.DAT` row; the
gun dispatch reads it as a burst flag. An earlier note attributed both to `EMP2`; they are different
weapons. See [`../simulation/weapon-firing.md`](../simulation/weapon-firing.md#the-gun-branches).

See [`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md) for the mount fields and
[`../simulation/weapon-firing.md`](../simulation/weapon-firing.md) for the fire path.

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

- `Field0` (tier semantics unknown — **not** the range, which is `0x30`)
- Reused constant fields (`DepCount`, `SubSphereFlagRaw`) from `.DMG`/`.COL`
- Firing-sequence tuple details
- `0x4e` (200 for LAS100 rising to 800 for the big launchers) and `0x50` (a small per-family code:
  1 laser, 2 autocannon, 3 EMP, 4 particle beam, 5 missile)
- The rest of the tail outside the fields above

Implementation: see `HercWorks.Core.Data.File.Dat.Sim.Weapons` and `HercWorks.Core.Io.Transform.Dbsim.WeaponsSimTransformer`.
