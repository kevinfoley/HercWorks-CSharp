# WEAPONS.DAT (SHELL0/GAM) and its companion WEAPONS.BIN

Reverse-engineered from `VSHELL.EXE` disassembly and verified against real retail files. This is
the SHELL0/GAM weapon **catalog** (33 entries: id 0 = NONE + 32 real weapons) — distinct from the
simulator's `simvol0/dat/WEAPONS.DAT` (see [`weapons-dat-sim.md`](weapons-dat-sim.md)). Both share
the same 0-32 weapon id.

> **The simulator does not read this file.** DBSIM carries its own 33-entry name table and its names
> differ (`EMPC`/`SHLD` here, `EMP`/`SHIELD` there), so a cockpit fed from this catalog prints the
> wrong words. Both spellings, and the full names, are in the table below.

## The weapon id space — three spellings per weapon

One 0-32 id addresses the same weapon in this catalog, in `simvol0/dat/WEAPONS.DAT` and in
`player.mec`, but **three different name strings describe it** and no two agree throughout:

- the **catalog code** — a 4-6 character string stored in this file, one per record, and what the
  `player.mec` editor shows;
- the **simulator code** — DBSIM's own 33-entry string array at `00498eb0`, what a cockpit weapon
  gauge prints. See [`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md#names--fun_0040e18c)
  for why the simulator ignores the catalog's spelling and when a gauge prints neither;
- the **full name** — `WEAPONS.BIN`, the only player-facing prose form.

| id | Catalog | Simulator | Full name (`WEAPONS.BIN`) | |
|---|---|---|---|---|
| 0 | `NONE` | *(empty)* | None | empty hardpoint |
| 1 | `ATC20` | `ATC20` | Autocannon 20mm | |
| 2 | `ATC35` | `ATC35` | Autocannon 35mm | |
| 3 | `ATC50` | `ATC50` | Autocannon 50mm | |
| 4 | `ATC75` | `ATC75` | Autocannon 75mm | |
| 5 | `ATC100` | `ATC100` | Autocannon 100mm | |
| 6 | `ELFW` | `ELF` | Electron Flux | |
| 7 | `EMPC` | `EMP` | Elec.Mag. Pulse | |
| 8 | `L100` | `LAS100` | Laser 100 GW | |
| 9 | `L200` | `LAS200` | Laser 200 GW | |
| 10 | `L300` | `LAS300` | Laser 300 GW | |
| 11 | `L400` | `LAS400` | Laser 400 GW | |
| 12 | `L500` | `LAS500` | Laser 500 GW | |
| 13 | `MSL6` | `6` | Missile Rack 6X | gauge prints the loaded round, not this |
| 14 | `MSL8` | `8` | Missile Rack 8X | gauge prints the loaded round, not this |
| 15 | `MSL10` | `10` | Missile Rack 10X | gauge prints the loaded round, not this |
| 16 | `FLYMSL` | `24` | Razor Missile Rack | gauge prints the loaded round, not this |
| 17 | `PBW` | `PBEAM` | Particle Beam | |
| 18 | `ECM` | `ECM` | Radar Jammer | no projectile |
| 19 | `BEMP` | `EMP` | Bull Elec. Mag. Pulse | Bull only |
| 20 | `BPBW` | `PBEAM` | Bull Particle Beam | Bull only |
| 21 | `BMSL` | `MISSL` | Bull Missile Rack | Bull only |
| 22 | `ELF2` | `ELF2` | Electron Flux II | |
| 23 | `EMP2` | `EMP2` | Elec.Mag. Pulse II | |
| 24 | `PBW2` | `PBW2` | Particle Beam II | |
| 25 | `PLAS` | `PLAS` | Plasma Cannon | |
| 26 | `LAEW` | `LAEW` | Locust Launcher | cut — see [`../cut-content.md`](../cut-content.md) |
| 27 | `MINE` | `MINE` | Mine Launcher. | cut — see [`../cut-content.md`](../cut-content.md) |
| 28 | `MFAC` | `MAGN` | MagnetoFusion Cannon. | |
| 29 | `TARG` | `TARG` | Targeting Pod. | pod, no projectile |
| 30 | `SHLD` | `SHIELD` | Shield Pod. | pod, no projectile |
| 31 | `TURB` | `TURBO` | Turbo Pod. | pod, no projectile |
| 32 | `ENRG` | `ENERGY` | Energy Pod. | pod, no projectile |

Full names are quoted verbatim: ids 27-32 really do carry a trailing full stop in `WEAPONS.BIN`,
and the lasers really are `GW` there where the codes say plain `L100`.

The three **Bull** weapons are the Cybrid four-legged HERC's own armament and can never be equipped
to a player HERC in retail. They are oversized versions of the player equivalents — `BEMP` is the
only weapon in the game with a barrel count of 3, and `BMSL` carries 36 rounds — so reading a
`BEMP` figure as an EMP-family stat overstates the family by 4x on shields. Their sim-side templates
are also the only ones carrying `Field0 = 15000` where every other weapon carries 1500-2500.

### The rank byte, and what retail actually fits

The fourth byte of each record's nine-byte trailer sorts the catalog, and it separates the three
groups cleanly. Twenty-eight weapons hold **ranks 1-28**, a permutation with no gap or repeat —
`PLAS` is 1 and `MSL6` is 28, so a low rank is an advanced weapon. `NONE` and the three Bull
weapons hold **99**. `MFAC` alone holds **0**, outside the sequence in both directions.

Parsing the mech roster (row #12's ten-slot fit at `0x32`) of all 62 retail `.MSN` files — 1683
records — gives the complementary view. Eight ids are fitted by nothing:

| Never fitted | Rank | Reading |
|---|---|---|
| `ATC75` (4), `L400` (11), `FLYMSL` (16), `ECM` (18) | 12, 13, 24, 25 | ordinary player weapons; the roster is the **AI** side only, and the player's own fit comes from `player.mec` |
| `BPBW` (20) | 99 | Bull armament that no retail mission ever spawns, unlike `BEMP` (12 missions) and `BMSL` (2) |
| `LAEW` (26), `MINE` (27) | 8, 15 | inert templates — nothing could fire them anyway |
| `MFAC` (28) | 0 | a complete, working weapon that nothing in retail fits |

**The roster scan cannot settle player availability by itself** — `ATC75` and `L400` are plainly
buildable and appear in it exactly as rarely as `MFAC` does. The rank byte is what separates them,
and rank 0 is unexplained: it may mean "not offered" or it may mean "sorts above `PLAS`". What is
certain is that no AI carries `MFAC`, the manual never names it, and it is the only weapon whose
rank falls outside the ordering every other non-Bull weapon takes part in.

`LAEW`, `MINE` and `MFAC` are indexed in [`../cut-content.md`](../cut-content.md).

## WEAPONS.BIN — weapon name strings (fully confirmed, byte-exact)

Lives in `ES2/VOL/LANG0.VOL` (one copy per language folder: `ENG`, `FRE`, `GER`) — **not** in
`SHELL0.VOL` alongside `WEAPONS.DAT` itself, which is why it's easy to miss when only looking at
the loose-extracted `SHELL0/GAM/` tree. Extract via `HercWorks.Vol.Io.VolFileReader.ParseVolFile`.

Verified byte-exact against all 3 language copies (identical; weapon names not localized):

```
0x00  uint32 count          -- 33, matches the WEAPONS.DAT catalog's real entry count exactly
0x04  uint32 stringDataSize -- size in bytes of the string-pool region below (505 for the real file:
                               579 total - 4 - 4 - 33*2 = 505, confirms this field's meaning exactly)
0x08  uint16[count] offsets -- relative offsets (from the start of the string pool, i.e. from
                               byte 0x08 + count*2) to each entry's null-terminated name string
...   string pool           -- packed null-terminated ASCII strings, one per weapon, in catalog
                               id order (offsets[0] = "None", offsets[1] = "Autocannon 20mm", etc.)
```

Read by DBSIM.EXE: `FUN_00408240` (weapon-id lookup via `offsets[id] + stringPoolStart`),
called from `FUN_00408605` which opens `"weapons.bin"` from `VSHELL.EXE`'s string table.

All 33 names are in the id-space table above, transcribed from `WEAPONS_ENG.BIN`.

## WEAPONS.DAT catalog record (29 bytes; partially confirmed)

Loaded by `VSHELL.EXE`'s `FUN_00411fc4` (file-level) → `FUN_00411d57` (per-record). Stored
in-memory as a flat array at `DAT_00483be4`, 29-byte (`0x1d`) stride per weapon id (confirmed via
`IMUL EDX,EDX,0x1d` in `FUN_0041266a`).

```
0x00–0x13  (20 bytes) raw "blockLen"-prefixed front block, NOT decoded field-by-field yet;
           the catalog code is the string inside it, see below
0x14–0x15  uint16, scaled ×1000 at load time — plausibly price/cost in tons of salvage. Used in
           cost calculations (`FUN_00412428`, `FUN_0041266a`) as (price/1000) × field_0x17 / 10.
0x16       byte, read raw at load, usage not yet found
0x17–0x18  short, used in FUN_0041266a as a multiplier against the price field — plausibly an
           ammo/quantity count, not confirmed by a load-time read (may be inside the 0x00-0x13
           raw block, or set some other way)
0x19–0x1c  (4 bytes) not yet observed
```

**The catalog codes are in the file.** Each record carries its own code as a NUL-terminated
ASCII string followed by nine bytes, so walking name-then-9 from the first record recovers all
33 in id order and lands at content offset 475 of 708. That walk produced the id-space table
above; the codes never need hand-transcribing.

File-level format: 2-byte record count, then per record: 2-byte id, the 29-byte body above
(`FUN_00411d57`), then a 2-byte value stored in a *separate* parallel array
(`DAT_00483fa2[id]`, not part of the 29-byte struct) — meaning not confirmed.

## How to apply

`WEAPONS.BIN` is done — safe to write a transformer directly from the structure above, and it's
a clean, small win (579 bytes, trivial indexed-string-table format, same shape independent of
language). The catalog record's `0x00–0x13` front block is the significant remaining gap — that's
most of the record and likely contains the numeric stats (damage, heat, rate of fire, etc.) that
actually matter for a simulation port; finding what reads those offsets back (the same
"decompile the accessor" technique used for `0x14`/`0x17` above) is the natural next step, not
further hex-guessing.
