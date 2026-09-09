# The HERC and armory catalogs — `SHELL0/GAM`

The forty files in `SHELL0.VOL` under `GAM\` are the shell's static data: the HERC chassis catalog, the armory's stock and price tables, and the screen layouts for the construction, armory and repair-bay screens. **Every address in this doc is in `VSHELL.EXE`**, and the shell's source module is named in the assertion strings for each function cited.

Every format here is verified byte-exact against the retail files — each parse consumes its file to the last byte.

For the campaign table `gam\career.dat` see [`../shell/campaign-loop.md`](../shell/campaign-loop.md); for the weapon catalog `gam\weapons.dat` see [`weapons-dat.md`](weapons-dat.md); for what the economy does with the numbers here see [`../shell/armory.md`](../shell/armory.md).

## The chassis type space

Nine chassis, addressed by a `0`–`8` type id that every file in this cluster and the save share. The display name is `estext.bin` string `0x6e + type` (`HercRecord_ResolveName`, `00410e79`'s tail via `00410e31`), which fixes the order:

| Type | Name | `INI_`/`ARM_`/`RPR_` file stem | Hardpoints |
|---|---|---|---|
| 0 | Outlaw | `OUTL` | 3 |
| 1 | Raptor II | `RAPT` | 5 |
| 2 | Tomahawk | `TOMA` | 5 |
| 3 | Samson | `SAMS` | 8 |
| 4 | Colossus | `COLO` | 9 |
| 5 | Apocalypse | `APOC` | 9 |
| 6 | Ogre | `OGRE` | 10 |
| 7 | Maverick | `MAVR` | 4 |
| 8 | Razor | `RAZR` | 7 |

The three per-chassis file families are each reached through a nine-entry pointer table of literal filenames, all three beginning at `OUTL` — `0046ff2c` (`arm_`), `0046fe94` (`rpr_`) and `004706fc` (`ini_`) — which is the same ordering read a second way.

**Hardpoint capacity comes from the code, not the data.** `Herc_CapacityForType` (`00410d54`) maps the type through `00410d30` — an identity search over the nine-entry table at `0046f728` — and returns `0046f73a[type]`, the column above. `herc_inf.dat` carries its own hardpoint field and the two disagree for the Raptor II; see [Rejected readings](#rejected-readings).

## `gam\hercs.dat` — the starting hangar

`LoadHercsDat` (`004104ed`, `herclist.cpp`) fills the hangar object at `00482ac3` at new-career time. The hangar is eight pointer slots at `+0x00`, an occupied count at `+0x20` and a scratch index at `+0x22` (`00482ae3` and `00482ae5` reached directly).

```
int16   count
per entry:
  int16   hangar slot, 0-7
          one HERC catalog record (below)
```

Retail's 148 bytes are four Outlaws in slots 0–3 and a Razor in slot 4:

| Slot | Chassis | Build % | Missions left | Hardpoint fits (id, ammo type) |
|---|---|---|---|---|
| 0 | Outlaw | 100 | 0 | `ATC50`, `ATC50`, `EMPC` |
| 1 | Outlaw | 100 | 0 | `L300`, `L300`, `MSL8` ammo 1 |
| 2 | Outlaw | 100 | 0 | `L300`, `L300`, `ELFW` |
| 3 | Outlaw | 100 | 0 | `ATC50`, `L300`, `ECM` |
| 4 | Razor | 0 | 3 | none |

Slot 4 is a chassis **under construction**, not a damaged one: 0% built with three missions still to run is exactly the state `Herc_Order` (`00411019`) leaves behind when the player buys one. A new career therefore opens with a Razor already on the slipway, three missions out.

### The HERC catalog record

`FUN_00410e79` (`herc.cpp`) reads the `gam\*.dat` form. It is the same 122-byte in-memory record the save serializes, but the file supplies only four of its fields:

```
int16   +0x00   chassis type, 0-8
int16   +0x4a   build progress, percent complete
int16   +0x78   build time remaining, in missions
int16   +0x4e   hardpoints occupied
        that many { int16 hardpoint index; weapon unit, 6 bytes }
```

Everything else comes from the constructor (`00410d7a`) or is derived after the read: `+0x4c` from `Herc_CapacityForType`, `+0x02` from `00410d30`, the name pointer at `+0x04` from `estext.bin`, and the whole 66-byte status block left at the constructor's uniform 100. A chassis loaded from one of these files is therefore always at full condition.

For the full 122-byte record and the status block's three condition arrays see [`save-games.md`](save-games.md#herc-record--122-bytes-0x7a-in-memory).

**Hardpoints serialize sparsely**, occupied entries only, each preceded by its index — so a reader must use the count and cannot assume dense packing. Retail's `INI_APOC.DAT` exercises this: it fits hardpoints 0–5 and 7, leaving 6 empty.

### The weapon unit record

One mounted or stocked weapon. Ten bytes in memory, five `int16`, constructed by `FUN_004119d8` as `{ -1, -1, 100, 100, 5 }`:

| Offset | Field |
|---|---|
| `+0x00` | weapon catalog id |
| `+0x02` | armory class index, derived by `FUN_004119b4` and never stored in any file |
| `+0x04` | 100, from the constructor |
| `+0x06` | condition |
| `+0x08` | ammo type |

Two file forms share it. The `gam\*.dat` form is six bytes — `+0x00`, `+0x06`, `+0x08` (`FUN_00411a36`) — and the save form is all ten (`FUN_00411aa6` / `FUN_00411aff`).

`FUN_004119b4` searches the thirty-entry table at `0046f868` for the id and returns its position. That table holds ids `0`–`18` and `22`–`32`: **every id except the three Bull weapons**, which therefore resolve to `-1`. It is an independent statement of the same exclusion `arm_weap.dat` makes below.

Ammo type `0`–`3` are the guidance kinds `ARM`, `ARH`, `SARH`, `EO` (`estext.bin` `0xa1`–`0xa4`); `5` means the hardpoint carries nothing guided. Retail data holds `5` everywhere except the missile racks, and `Armory_DeliverQueue` (`00412428`) writes `1` for ids 13–16 and `5` for everything else. The `5` that [`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md) observes in every non-launcher slot originates here.

## `gam\ini_*.dat` — the stock fit per chassis

Nine files, one per type, each a **bare HERC catalog record** with no leading hangar slot. This is the fit a newly built chassis is delivered with. All nine consume exactly, and each file's leading type field matches its filename stem.

## `gam\trn_herc.dat` — a second stock-fit set

The same record again, nine of them for types 0–8, with **no count prefix** — the file is a bare sequence terminated by EOF, and 432 bytes consume exactly. Every record reads 100% built with 0 missions remaining, and the hardpoint counts match the nine `ini_*.dat` files one for one.

Four of the nine fits are byte-identical to the matching `ini_*.dat`. The other five differ in exactly one hardpoint, and always by upgrading it:

| Chassis | `ini_*.dat` | `trn_herc.dat` |
|---|---|---|
| Outlaw | `PBW` | `PLAS` |
| Tomahawk | `PBW` | `PLAS` |
| Samson | `PBW` | `MFAC` |
| Apocalypse | `PBW` | `MFAC` |
| Ogre | `EMP2` | `MFAC` |

**This is the only place in retail data that fits `MFAC` to a player machine.** The weapon has no armory panel and ships locked, so nothing in the purchase path can reach it ([`../cut-content.md`](../cut-content.md)) — but three chassis here carry it.

That does not make it reachable, because **no reader for this file has been traced**. The literal `trn_herc.dat` appears in neither `VSHELL.EXE`'s nor `DBSIM.EXE`'s data segment, and no reference to it appears in the VSHELL decompile, where the sibling `arm_`, `rpr_` and `ini_` families are each reached through a nine-entry table of literal filenames. A path assembled at runtime cannot be ruled out from string evidence alone, so treat this as "no load path found", not as proof there is none.

## `gam\herc_inf.dat` — the chassis stat table

`LoadHercInfDat` (`0041181c`, `hercinfo.cpp`) reads a count and then bulk-reads `count << 4` bytes into `00483b54` — a flat sixteen-byte stride, nine records, 146 bytes exactly.

Both the stride and the price field's offset are confirmed in raw disassembly rather than from decompiler pointer math: `Herc_ScrapValue` addresses the table as `shl edx, 4` followed by `movsx ecx, word [edx + 0x00483b5c]` (`00413b93`), which is a 16-byte stride reaching `0x00483b54 + 8`.

```
int16   count -- 9
per record, 16 bytes:
  int16   +0x00   chassis type, 0-8
  int16   +0x02   mass, tons
  int16   +0x04   top speed, kph
  int16   +0x06   hardpoints
  int16   +0x08   price, in tons -- x1000 gives the salvage cost
  int16   +0x0a   no reader traced
  int16   +0x0c   build time, in missions
  int16   +0x0e   availability flag
```

The first four stats are exactly what the Herc Construction screen prints, which is what names them: `Herc_BuildScreenRefresh` (`00446cfa`) formats `+0x02` as `"%d TONS"`, `+0x04` as `"%d KPH"`, `+0x06` bare, and `+0x08` as `"%d TONS"` against the labels `Mass`, `Speed`, `Hardpoints` and `Salvage Reqd` (`estext.bin` `0xbb`, `0xbe`, `0xbf`, `0xc0`).

| Chassis | Mass | Speed | Hardpoints | Price | `+0x0a` | Build | Available |
|---|---|---|---|---|---|---|---|
| Outlaw | 27 | 80 | 3 | 60 | 40 | 1 | yes |
| Raptor II | 40 | 74 | 4 | 90 | 50 | 2 | no |
| Tomahawk | 45 | 71 | 5 | 100 | 60 | 2 | yes |
| Samson | 63 | 52 | 8 | 170 | 95 | 2 | yes |
| Colossus | 77 | 46 | 9 | 200 | 110 | 3 | yes |
| Apocalypse | 70 | 60 | 9 | 190 | 100 | 3 | yes |
| Ogre | 80 | 50 | 10 | 230 | 125 | 3 | no |
| Maverick | 25 | 85 | 4 | 55 | 30 | 1 | no |
| Razor | 50 | 200 | 7 | 120 | 70 | 3 | no |

`+0x08` is the record's load-bearing field: `Herc_Order` (`00411019`) returns `herc_inf[type].+0x08 * 1000` as the price to charge, and the purchase screen tests the salvage pool against the same product.

`+0x0c` is copied into the new chassis's `+0x78` by `Herc_Order`, and `Herc_BuildTick` (`00411086`) decrements it and recomputes the percentage:

```
+0x78 -= 1;
+0x4a = ((herc_inf[type].+0x0c - +0x78) * 100) / herc_inf[type].+0x0c;
```

So `+0x78` counts missions down to delivery and `+0x4a` is the `% Complete` figure (`estext.bin` `0x6c`) the hangar shows against it.

`+0x0e` is the **chassis availability flag**, and it is campaign state rather than a catalog constant: the four chassis that ship as `0` are exactly the four `Herc_GrantUnlocks` (`004118c5`) can turn on. It is the array the save persists — see [`save-games.md`](save-games.md#savgame_sav--block-order) — and the purchase screen refuses a chassis whose flag is clear.

### Chassis unlocks — `Herc_GrantUnlocks` (`004118c5`)

Walks the nine records and, for a chassis still locked, tests one campaign-flag slot against one expected value; on a match the flag is set, and either way the slot is cleared. This mirrors the weapon-unlock path in [`weapons-dat.md`](weapons-dat.md#0x16-is-the-weapon-unlock-flag) exactly.

| Chassis | Flag slot | Expected value |
|---|---|---|
| Raptor II | `0x3c` | 2 |
| Maverick | `0x3d` | 2 |
| Ogre | `0x3e` | 2 |
| Razor | `0x3f` | 1 |

The expected value is a loop-carried local that starts at 2 and is set to 1 in the Razor's own branch. Because the Razor is the last record in the table, nothing after it reads the changed value; a table reordered to put type 8 earlier would silently drop every later chassis's threshold to 1.

## `gam\damage.dat` — the component and weapon value table

One 102-byte file, read whole at load and immediately turned into the tables the repair bay and the scrap screen price against.

```
int16       chassis scale -- 800
6 x int16   external component group percentages -- 100 50 50 50 100 100
9 x int16   internal component percentages -- 100 100 50 50 25 100 25 25 25
int16       weapon scale -- 1000
int16       count -- 33
count x int16   per-weapon scrap value
```

The loader (`hercdisp.cpp`, the function opening `s_gam_damage_dat_0046fdb4`) expands it against `herc_inf.dat`'s prices through `FUN_00450a84`, a Q10 fixed-point multiply — `(a * b) >> 10`:

```
for each chassis type t, price = herc_inf[t].+0x08 * 1000:
  DAT_004840cc[t][g] = (800 * ((external[g] * price) >> 10)) >> 10    for g in 0..5
  DAT_004840e6[t][i] = (800 * ((internal[i] * price) >> 10)) >> 10    for i in 0..8
for each weapon id w:
  DAT_0048431e[w] = (1000 * weaponValue[w]) >> 10
```

`DAT_004840cc` is a 9 × 33 `int16` table and `DAT_004840e6` is its column 13; the two together end exactly where the 33-entry weapon table `DAT_0048431e` begins. The six external and nine internal figures are the units the whole cost model works in — see [`../shell/armory.md`](../shell/armory.md#repairing-and-scrapping).

The per-weapon column is **the weapon's `weapons.dat` price times 100**, holding for all thirty non-Bull ids, with the three Bull weapons deliberately zeroed where `weapons.dat` prices them at 50. Since the catalog price times 1000 is the cost in kg, a weapon scraps for a tenth of what it cost.

## The screen-layout families

`gam\arm_weap.dat`, and the nine-file `gam\arm_*.dat` and `gam\rpr_*.dat` families, are widget geometry: each record positions one panel against a frame of the matching `DBA\` sprite sheet. They carry no simulation quantities. What makes `arm_weap.dat` worth reading anyway is its record *list*, which is the authoritative set of weapons the armory offers.

### `gam\arm_weap.dat`

Read by `Arming_BuildScreen` (`0043e52c`, `warmingi.cpp` — the weapon-fitting screen) alongside `dba\arm_weap.dba`:

```
int16   count -- 26
count x { int16 weaponId; int32 x; int32 y; int16 dbaFrame }
int16   count -- 4
count x { int16 ammoType; int32 x; int32 y; int16 dbaFrame }
```

The loader derives each panel's rect as `{ x, y, x + frameWidth, y + frameHeight }` from the named DBA frame, which is what identifies the two `int32` as a top-left corner.

**Twenty-six weapons have a panel.** The seven ids with none are `NONE` (0), the three Bull weapons (19–21), and `LAEW` (26), `MINE` (27) and `MFAC` (28). This settles player availability outright, where the `.MSN` roster scan cannot: see [`weapons-dat.md`](weapons-dat.md#the-rank-byte-and-what-retail-actually-fits) and [`../cut-content.md`](../cut-content.md).

The four trailing panels are the guidance kinds `ARM`, `ARH`, `SARH`, `EO`, ids `0`–`3`.

`WPN_INFO.BIN` is indexed by this file's panel order rather than by weapon id, five strings per panel — see [`weapons-dat.md`](weapons-dat.md#the-bin-string-tables).

### `gam\rpr_*.dat` — repair-bay layout

Nine files, loaded together by `FUN_00413d1c` (`hercdisp.cpp`) into a per-chassis structure of 33 counts followed by 33 record pointers, `0xc6` bytes per chassis.

```
int16   count
count x layout record            -- the chassis's own components
1 x     layout record            -- one further record per chassis, at 00484534
int16   groupCount
groupCount x { int16 weaponId; int16 count; count x layout record }

layout record, 14 bytes on disk into a 26-byte struct:
  int16   +0x00
  8 B     +0x02
  int16   +0x12
  2 B     +0x16
```

Retail's component counts are 4, 6 or 12 — the Razor's twelve against the walkers' four or six, which is the flyer's own component set.

### `gam\arm_*.dat` — armory layout

The same shape with two leading records instead of a counted list, and two 8-byte blocks per record instead of one:

```
2 x     layout record
int16   groupCount
groupCount x { int16 weaponId; int16 count; count x layout record }

layout record, 22 bytes on disk into a 26-byte struct:
  int16   +0x00
  8 B     +0x02
  8 B     +0x0a
  int16   +0x12
  2 B     +0x16
```

The group records' first two `int32` are each incremented by one as they are read — a one-pixel inset applied at load.

`gam\arm_hots.dat` and `gam\rpr_hots.dat` are the two chassis-independent companions, opened by `warmoryi.cpp` and `wsrvbayi.cpp` (`00447e34`, `0043a944`).

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `herc_inf.dat` `+0x06` gives a chassis's hardpoint count | It is what the Herc Construction screen *prints*, and for the Raptor II it prints 4 where the machine the player receives has 5. `Herc_CapacityForType` reads the in-code table at `0046f73a`, and that is the figure `+0x4c` and every hardpoint loop use. The other eight chassis agree, so a reader checking one file will not notice |
| The 26 bytes at HERC status block `+0x00` are opaque | They are 13 `int16` component conditions, initialized to 100 alongside the other two arrays by `FUN_00411b88` and averaged with them by `FUN_00411bd4`, whose divisor is `13 + 9 + hardpoints`. See [`save-games.md`](save-games.md#the-66-byte-status-block) |
| `hercs.dat`'s fifth entry is a wrecked Razor | Its `+0x4a` of 0 is build progress, not condition. The status block a `gam\*.dat` chassis carries is never read from the file and stays at the constructor's uniform 100 |
| The `+0x4a`/`+0x78` pair is repair state | `Herc_Order` sets them when a chassis is *bought*, from `herc_inf.dat` `+0x0c`, and `Herc_BuildTick` drives them one mission at a time until delivery. Repair works on the status block instead |
| `+0x4a` is a health or condition ratio | It is the build percentage. The data will not separate the two readings: `+0x4a` is 100 in all nine `ini_*.dat` and in `trn_herc.dat`, which is equally consistent with an undamaged machine. Only `hercs.dat`'s part-built Razor reads anything else, and it reads 0 while that machine is at full condition — so a condition reading has it dead on arrival |
| `+0x78` picks a frame of `[chassis]_bod.dba` | It is the mission countdown to delivery. Both readings fit the shipped files, where the field is 0 everywhere except that same Razor's 3; what settles it is `Herc_BuildTick`, which decrements it and recomputes `+0x4a` from it against `herc_inf.dat`'s build time. A damage-model frame index would not be arithmetic on a purchase countdown |
