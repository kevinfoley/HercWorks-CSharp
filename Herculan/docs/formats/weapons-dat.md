# WEAPONS.DAT (SHELL0/GAM) and its companion WEAPONS.BIN

Reverse-engineered from `VSHELL.EXE` disassembly (Ghidra, `E:\ES2Stuff\tools\`) plus direct
verification against real retail files. This is the SHELL0/GAM weapon **catalog** (33 entries:
id 0 = NONE + 32 real weapons) — distinct from `simvol0/dat/WEAPONS.DAT`, a different file with
its own unresolved structure (see `project_es2_translation_status` memory).

## WEAPONS.BIN — weapon name strings (fully confirmed, byte-exact)

Lives in `ES2/VOL/LANG0.VOL` (one copy per language folder: `ENG`, `FRE`, `GER`) — **not** in
`SHELL0.VOL` alongside `WEAPONS.DAT` itself, which is why it's easy to miss when only looking at
the loose-extracted `SHELL0/GAM/` tree. Extract via `HercWorks.Vol.Io.VolFileReader.ParseVolFile`
(no existing loose copy in this install as of 2026-08-08).

Confirmed structure (verified byte-exact against all 3 language copies, which turned out to be
**identical to each other** — weapon names apparently aren't localized in retail, unlike other
`LANG0.VOL` content):

```
0x00  uint32 count          -- 33, matches the WEAPONS.DAT catalog's real entry count exactly
0x04  uint32 stringDataSize -- size in bytes of the string-pool region below (505 for the real file:
                               579 total - 4 - 4 - 33*2 = 505, confirms this field's meaning exactly)
0x08  uint16[count] offsets -- relative offsets (from the start of the string pool, i.e. from
                               byte 0x08 + count*2) to each entry's null-terminated name string
...   string pool           -- packed null-terminated ASCII strings, one per weapon, in catalog
                               id order (offsets[0] = "None", offsets[1] = "Autocannon 20mm", etc.)
```

This is the file DBSIM.EXE's `FUN_00408240` reads from at runtime (found via disassembly): given a
weapon id, it does a bounds-checked lookup `*(ushort*)(base + id*2) + baseOffset` — exactly the
`offsets[id] + stringPoolStart` pattern above. `FUN_00408605` opens the file by the literal name
`"weapons.bin"` found in `VSHELL.EXE`'s strings.

Real confirmed names (first several, English — see `WEAPONS_ENG.BIN` extraction for the full
list): `None`, `Autocannon 20mm`, `Autocannon 35mm`, `Autocannon 50mm`, `Autocannon 75mm`,
`Autocannon 100mm`, `Electron Flux`, `Elec.Mag. Pulse`, `Laser 100...` (truncated in the initial
check, full list is straightforward to dump — just walk the offset table).

## WEAPONS.DAT catalog record (29 bytes; partially confirmed)

Loaded by `VSHELL.EXE`'s `FUN_00411fc4` (file-level) → `FUN_00411d57` (per-record) →
`FUN_00408240` (the `weapons.bin` name lookup above). Stored in-memory as a flat array at
`DAT_00483be4`, one 29-byte (`0x1d`) record per weapon id — **verified stride is 29 bytes via raw
disassembly** (`IMUL EDX,EDX,0x1d` used directly as a byte offset in `FUN_0041266a`), not 116 as
an earlier pass through this investigation initially mis-read from the decompiler's symbolic
pointer arithmetic — don't trust that decompiler notation for stride without checking the actual
multiply instruction.

```
0x00–0x13  (20 bytes) raw "blockLen"-prefixed front block, NOT decoded field-by-field yet
0x14–0x15  uint16, read raw then ×1000 scaled at load time — best current guess: price/cost in
           tons of salvage (the game's currency), stored ×1000 for fractional-ton precision.
           Used later (armory total-cost calculation, `FUN_00412428`) as a direct per-item cost
           contribution, and in `FUN_0041266a` as (price/1000) * field_0x17 / 10 — consistent
           with a "price × quantity" cost calc, not consistent with any "range" usage despite an
           initial (wrong) guess to that effect.
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
