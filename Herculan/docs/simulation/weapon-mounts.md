# DBSIM.EXE weapon mounts: loadout, naming, magazines, capacitors, selection

Loadout and naming solved 2026-08-23; selection, chaining and linking 2026-08-24. Reverse-engineered
from `DBSIM.EXE` in the `ES2Recon` Ghidra project; all addresses are DBSIM virtual addresses. Ported
in `Herculan.Engine.Sim.{WeaponCatalog, WeaponMount, WeaponMounts}` and
`Herculan.Engine.Content.WeaponRowState`.

Covers how a fit becomes mounts, what those mounts hold, and how the player arms them. The pool
arbitration they feed is in
[`reactor-energy-pool.md`](reactor-energy-pool.md); the widgets they drive are in
[`../formats/cockpit-hud.md`](../formats/cockpit-hud.md#weapon-hardpoint-rows); the template table
itself is in [`../formats/weapons-dat-sim.md`](../formats/weapons-dat-sim.md).

## The join — `MechLoadout_ConstructWeaponMounts` (`0040fff8`)

**The chassis drives the join, not the fit.** `Mech_ConfigureLoadout` (`004175dc`) passes the mech
type's hardpoint list (`FUN_00420634` → the `gl\<HERC>.GL` record array) plus the mission's two
parallel per-slot arrays. The factory walks the hardpoint records **in file order**, and each
record's byte at `+0x17` indexes the fit arrays:

```
weaponId     = weaponRefs[record[0x17]]      // negative clamps to 0
secondaryKey = ammoTypes [record[0x17]]      // 5 is rewritten to 0
```

Consequences: the fit array's slot positions are load-bearing (compacting it fits the wrong weapon
to the wrong hardpoint), a slot no hardpoint addresses contributes nothing, and the same
`player.mec` entry produces a different panel on two different HERCs. Weapon id 0 and id 27
(`MINE`, which has no case in the factory's switch) build no mount; the array keeps the hole.

### `gl\<HERC>.GL` — the hardpoint list

`FUN_0040fee8` (asserts in `GUNLIST.CPP`) reads `short count` then `count` 26-byte records.
Modelled by `HercWorks.Core.Data.File.Dbsim.GunLayout`. The fields the loadout path reads:

| Offset | Field | Role |
|---|---|---|
| `+0x07` | fire-chain number | **the cockpit weapon row this mount owns** — handed to the gauge factory as a `.GAU` weapon-slot index, so the panel prints it as `n+1` |
| `+0x16` | link partner offset | signed; how far away in the mount array this hardpoint's LINK partner sits. Retail chassis pair mirrored left/right hardpoints with ±1 |
| `+0x17` | fit slot | index into the mission's two loadout arrays |

The rest of the record (bone id, orientation, mount-point offset) is the placement data `GunLayout`
already models and this system does not touch.

Row order and mount order are different orderings of the same list. SAMSON.GL, against the retail
`player.mec`, yields rows `EMP, ATC35, ATC35, ARH, EMP, ATC35, SHIELD POD, ATC35` from a fit array
reading `ATC35, SHIELD, ATC35, ATC35, ATC35, EMP, EMP, MSL10`.

### The second loadout array is the ammunition type

Both mission formats carry it beside the weapon ids, and it is what a missile launcher is loaded
with — not a round count:

| Source | Offset |
|---|---|
| `player.mec` entry | after the weapon-id array (`MecEntry.WeaponAmmoTypes`) |
| `script.dat` block 7 record | source `0x72`, i.e. 64 bytes past the weapon-id array at `0x32` |

Retail data puts a filler `5` in every non-launcher slot. Verified against the retail mission: every
slot whose weapon id is `MSL10` reads 1 here and every other slot reads 5.

## Mount classes

The factory's switch on the weapon id picks one of three live constructors. Nothing else decides
what a mount is:

| Class | Ctor | Weapon ids | Carries | Gauge |
|---|---|---|---|---|
| Ammunition | `FUN_0040e140` | 1–5, 13–16, 21, 26 | rounds | numeric (`FUN_00432124` → `FUN_00440f78`) |
| Energy | `FUN_0040e074` | 6–12, 17, 19, 20, 22–25, 28 | a capacitor | LED bar (`FUN_00432074` → `FUN_00440a68`) |
| Pod | `FUN_0040e274`/`e308`/`e344`/`e2bc`/`e380` | 18, 29–32 | nothing | name only (`FUN_004321d4` → `FUN_00441524`) |

Shared mount fields mean different things per class:

| Offset | Ammunition | Energy |
|---|---|---|
| `+0x7b` | rounds remaining | charge target, **and** the pool-arbitration priority |
| `+0x7d` | rounds in 256ths | capacitor level |
| `+0x7f` | — | charge rate, a flat 20 |
| `+0x31` | refire countdown | refire countdown |
| `+0x43` | — | mid-charge flag |
| `+0x49` | destroyed | destroyed |
| `+0x4b` | LINK engaged | LINK engaged |

### Ammunition

`FUN_0040e140` reads the magazine size from the template's field at `+0x3a` and powers the mount up
holding a full one: `+0x7b = size`, `+0x7d = size << 8`. The gauge prints `+0x7d >> 8`.

Retail magazines: ATC20 2000, ATC35 1500, ATC50 1000, ATC75 750, ATC100 500, MSL6/8/10/24 6/8/10/24,
MISSL 36, PLAS 20, LAEW 0.

### Energy

`FUN_0040e074` writes `Q10Multiply(820, 1200) = 960` into **both** `+0x7b` and `+0x7d` and `20` into
`+0x7f` — literals, identical for every energy weapon. A HERC powers up with its capacitors full.

`+0x7b` is a *request*, not a capacity: `FUN_0040f4d8` drops it to 820 when the mount goes idle and
`FUN_0040f4f0` raises it to 1200 (and sets the mid-charge flag) when a shot is demanded. The charge
bar's denominator is the fixed 1200, so a mount at its spawn charge fills 960/1200 = four-fifths of
its bar, and only a mount charging for a shot ever fills it.

Readiness (`FUN_0040ecdc`) is `!destroyed && refireTimer == 0 && charge >= threshold`, where the
threshold comes from the template's `+0x36`/`+0x38` pair: `max(+0x36, +0x7b)` when `+0x36 < +0x38`,
otherwise `+0x38` outright. Real templates carry both shapes — `EMP` reads (350, 10000), `ELF`
reads (400, 70). The ammunition equivalent (`FUN_0040ed6c`) is `!destroyed && refireTimer == 0 &&
rounds != 0`.

## Names — `FUN_0040e18c`

**The simulator does not use the shell catalog's names.** `Weapons_LoadResourceTables` (`0040fc8c`)
walks a 33-entry string-pointer array at `00498eb0` as it reads the template table and stores one
pointer into each record's `+0x52`; that is what a gauge prints. The two disagree:

| Id | `SHELL0/GAM/WEAPONS.DAT` | `DBSIM.EXE` |
|---|---|---|
| 7 | `EMPC` | `EMP` |
| 30 | `SHLD` | `SHIELD` |
| 31 | `TURB` | `TURBO` |
| 32 | `ENRG` | `ENERGY` |

Full table in `WeaponCatalog.MountNames`.

The name is chosen off the **resolved projectile**, not the weapon id: when the mount's `PROJ.DAT`
record is a `Missile`, the gauge prints that record's own subtype from a four-entry table at
`004989c8` — `SARH`, `ARH`, `ARM`, `EO` — so a launcher is named by what is loaded in it. This is
why the retail player's `MSL10` hardpoint reads `ARH`. Ids 13–16's own names are bare round counts
(`"6"`, `"8"`, `"10"`, `"24"`) precisely because a launcher never prints them.

> The subtype index is unbounded in the original. `MISSL` (id 21) points straight at the `BMSL`
> record, subtype 4, and reads one past the four-entry table. The engine falls back to the id's own
> name rather than reproducing a read off the end of a table; no player machine mounts that weapon.

A pod row is the one place the name is decorated. `FUN_00441524` seeds its 11-char buffer with a
literal space, appends the mount name, then appends `STRINGS0.STR` group 3 (`" POD"`) into whatever
room is left — `" SHIELD POD"` exactly fills it. The Heads-Down Display's weapon list
(`FUN_00450c54`) takes the undecorated name, so the same pod reads `SHIELD` there.

## The manager — `mech+0x202`

`MechLoadout_ConstructWeaponMounts` builds the base object (the mount array and its count);
`FUN_004104ec` extends it for a locally-simulated machine with the selection and the fire groups.
A remote machine gets the base class and never has either read.

| Offset | Field |
|---|---|
| `+0x04` | mount array (holes preserved) |
| `+0x08` | mount count |
| `+0x0a` | per-ammunition-type counters, read by the readiness test — see Open |
| `+0x1c` | current fire group, 0–2 |
| `+0x1d` | armed mount index, `0xff` for none |
| `+0x1f`, `+0x25`, `+0x2b` | the three fire-group arrays, one `short` per mount |
| `+0x31` | target range, gating the readiness test |

| `+0x14` | TRACK's latch — automatic turret tracking |
| `+0x18` | single-fire flag, below |

**Every non-pod mount starts in group I and groups II and III start empty.** The constructor writes
`group == wanted` into all three arrays with `wanted` fixed at 0 for a weapon and −1 for a pod. The
initial selection is the first non-pod mount in *mount* order.

`FUN_00410b40`, the per-frame pass, reads the console panel's three-field input block, applies the
chain and LINK commands from it, and then pushes three flags to each row's gauge: armed, in the
current group, and ready (`FUN_00410970`).

## Arming, chaining and linking

**Arming a weapon and putting it in a fire chain are different actions**, and the cockpit offers
each of them two ways:

| Action | Key | Mouse | Reaches |
|---|---|---|---|
| Arm a row | `[1]`–`[0]` | left-click the row | `FUN_004106ac` |
| Add/remove a row from the current chain | `[Alt]`+`[1]`–`[0]` | right-click the row | `FUN_004110ac` |
| Step the armed weapon | `[W]` / `[Alt]`+`[W]` | — | `FUN_0041074c` |
| Link the armed weapon | `[L]` | the LINK button | `FUN_00410f14` |
| Next fire chain | `` [`] `` | the chain button | `FUN_00410ae4` |

The two routes converge rather than duplicating: a number key indexes the cockpit's own ten-gauge
array at `CockpitViewInstance+0x70` and presses that gauge's select gadget, which is the same gadget
the mouse hits. The mouse's own split is not a modifier but the **button**: a row gadget's click
handler (`FUN_00440ef0` for an energy row, `FUN_004414b4` for an ammunition one) branches on bit 1
of the value it is handed, and that value is the mouse-button word `0049db6c` — bit 0 left, bit 1
right.

> Command codes are **PC set-1 scancodes**, with `0x200` added for `[Alt]`. `0x26` is `L` and
> `0x29` is `` ` ``, which is how `FUN_004421a0` binds them to the console panel's LINK and chain
> children; `0x11`/`0x211` are `W`/`Alt+W`; `0x02`–`0x0b` and `0x202`–`0x20b` are the two number-key
> banks. `FUN_0045fdac` is the dispatcher every code passes through.

### Arming — `FUN_004106ac` and `FUN_00410708`

`FUN_004106ac` finds the mount whose own gauge pointer (`+0x77`) matches, requires `+0x4c`, calls
`FUN_00410708` and sets the single-fire flag. **A pod fails this twice over**: its `+0x4c` is
cleared by `FUN_0040e234`, and its gauge-match slot (`FUN_0040df00`) returns 0 unconditionally.

`FUN_00410708` is the only writer of `+0x1d`, and it normalises a linked pair onto its first half —
arming the right-hand weapon of a linked pair arms the left-hand one instead, since the second
half's partner offset is negative. That is what keeps a pair's two rows agreeing.

**Single fire (`+0x18`).** Set by arming a weapon by hand, cleared by `[W]`/`[Alt]`+`[W]` and by
firing. While it is set, the per-frame pass leaves the selection alone however unready the weapon
is; while it is clear, `FUN_00410a3c` hands the selection to the next mount in the chain that could
fire. That is the whole of the manual's "select a weapon to single-fire … once you fire, the current
firing chain will resume". The flag is cleared at the key handler, not inside `FUN_0041074c`, so the
chain switch and the per-frame advance both step the selection without clearing it.

`FUN_0041074c` steps to the next mount that is selectable, in the current chain, and either unlinked
or the *first* half of a linked pair.

### Chaining — `FUN_004110ac`

Finds the mount by its `.GL` fire-chain byte rather than by gauge pointer, requires the template's
int32 at `0x30` to be positive, and **XORs** its bit in the current chain's array. It does not arm
anything. The `0x30` gate is what keeps a pod off a chain — every real firing weapon carries a large
positive value there and every pod carries zero. (The field descends with calibre across the
autocannon and laser families, which looks like a range, but the manual's own 20 m figure for the
ELF does not fit; it is left undecoded.)

### Linking — `FUN_00410f14`

Two conditions, both from the chassis: the armed mount's `.GL` record names a partner (`+0x16`
non-zero), and that partner carries the **same weapon id**. Both halves' `+0x4b` flip together. The
manual states the same rule from the other side — "any two identical weapons mounted symmetrically
on the HERC (on opposite hard points)".

Linking is visible because `FUN_00410b40` lights a linked mount's row when its *partner* is the
armed one, so both rows of a pair draw armed together. Readiness is joined too: `FUN_00410970`
recurses into the partner, so a pair is ready only when both halves are. A destroyed or empty half
unlinks the pair and hands the selection to the survivor.

> One LINK press runs the toggle **three** times in the original: the button's own click handler
> (`FUN_0044202c`), the manager's next per-frame pass reading the button's latch byte, and that
> pass writing the byte back so the widget sees it change and calls the handler again. Three flips
> of one bit is one flip. Herculan reproduces the net effect, not the round trip.

### Console buttons

`FUN_0044212c` switches on the child index: 0 advances the chain group and wraps at 3, 1 toggles
link, 2 toggles auto-track. `ConsoleButton_Paint` then takes each one's frame from a different
field — CHAIN and LINK from the shared press byte `+0x1b`, so they light only while held, and TRACK
from its own `+0x40` latch. **LINK never stays lit**; the link state lives on the mounts.

## Open

- **`manager+0x0a`.** `FUN_00410970` gates a missile mount's readiness on `manager[0x0a + type*2]`,
  and the manager block is `malloc`'d rather than zeroed. Readers were found (`FUN_004155ac`,
  `FUN_0041f358`); no writer was found along the paths traced, which is not the
  same as none existing. The engine leaves this gate out; it affects only a missile row's state-box
  colour.
- Template fields other than those named here, including the `0x30` chain gate — see
  [`../formats/weapons-dat-sim.md`](../formats/weapons-dat-sim.md).
- **Firing.** `WeaponMount_FireDispatch_GunBeam` (`0040ea58`), `WeaponMount_FireDispatch_Missile`
  (`0040e964`) and auto-fire (`FUN_0040ede8`) are unported. See
  [`../engine/handoff-beam-firing.md`](../engine/handoff-beam-firing.md).
- **Auto turret tracking.** `manager+0x14` is latched by the TRACK button and read by nothing in
  Herculan; the tracking itself is unported.
- **A pod's on/off toggle.** Clicking a pod's row in the original flips `gauge+0xc2`
  (`FUN_004419fc`), which re-fonts its name. No pod carries an on/off state here.
