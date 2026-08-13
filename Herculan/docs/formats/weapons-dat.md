# WEAPONS.DAT (SHELL0/GAM) and its companion WEAPONS.BIN

Reverse-engineered from `VSHELL.EXE` disassembly and verified against real retail files. This is
the SHELL0/GAM weapon **catalog** (33 entries: id 0 = NONE + 32 real weapons) — distinct from the
simulator's `simvol0/dat/WEAPONS.DAT` (see [`weapons-dat-sim.md`](weapons-dat-sim.md)).

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

Confirmed names (English, first several; full list in `WEAPONS_ENG.BIN`): `None`, `Autocannon
20mm`, `Autocannon 35mm`, `Autocannon 50mm`, `Autocannon 75mm`, `Autocannon 100mm`, `Electron
Flux`, `Elec.Mag. Pulse`, `Laser 100` (etc.; walk the offset table for all entries).

## WEAPONS.DAT catalog record (29 bytes; partially confirmed)

Loaded by `VSHELL.EXE`'s `FUN_00411fc4` (file-level) → `FUN_00411d57` (per-record). Stored
in-memory as a flat array at `DAT_00483be4`, 29-byte (`0x1d`) stride per weapon id (confirmed via
`IMUL EDX,EDX,0x1d` in `FUN_0041266a`).

```
0x00–0x13  (20 bytes) raw "blockLen"-prefixed front block, NOT decoded field-by-field yet
0x14–0x15  uint16, scaled ×1000 at load time — plausibly price/cost in tons of salvage. Used in
           cost calculations (`FUN_00412428`, `FUN_0041266a`) as (price/1000) × field_0x17 / 10.
0x16       byte, read raw at load, usage not yet found
0x17–0x18  short, used in FUN_0041266a as a multiplier against the price field — plausibly an
           ammo/quantity count, not confirmed by a load-time read (may be inside the 0x00-0x13
           raw block, or set some other way)
0x19–0x1c  (4 bytes) not yet observed
```

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
