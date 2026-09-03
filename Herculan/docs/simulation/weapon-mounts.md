# DBSIM.EXE weapon mounts: loadout, naming, magazines, capacitors, selection

Reverse-engineered from `DBSIM.EXE` in the `ES2Recon` Ghidra project; all addresses are DBSIM
virtual addresses. Ported
in `Herculan.Engine.Sim.{WeaponCatalog, WeaponMount, WeaponMounts}` and
`Herculan.Engine.Content.WeaponRowState`.

Covers how a fit becomes mounts, what those mounts hold, and how the player arms them. What happens
when the trigger is pulled is in [`weapon-firing.md`](weapon-firing.md); the pool arbitration they
feed is in [`reactor-energy-pool.md`](reactor-energy-pool.md); the widgets they drive are in
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

The factory's switch on the weapon id picks one of four live classes. Nothing else decides what a
mount is:

| Class | Ctor | Weapon ids | Carries | Gauge |
|---|---|---|---|---|
| Ammunition | `WeaponMount_CtorAmmunition` | 1–5, 13–16, 21, 26 | rounds | numeric (`FUN_00432124` → `FUN_00440f78`) |
| Energy | `WeaponMount_CtorEnergy` | 7–12, 17, 19, 20, 23–25, 28 | a capacitor | LED bar (`FUN_00432074` → `FUN_00440a68`) |
| ELF | `WeaponMount_CtorEnergy`, then vtable `ElfMountVtable` | 6, 22 | a capacitor | LED bar, as Energy |
| Pod | `FUN_0040e274`/`e308`/`e344`/`e2bc`/`e380` | 18, 29–32 | nothing | name only (`FUN_004321d4` → `FUN_00441524`) |

The ELF case is the only one that is not just a constructor call: the factory runs the energy
constructor and then **overwrites the object's vtable pointer** with `ElfMountVtable` (`004992c0`).
The two classes therefore share every field and differ only in the five slots that table replaces —
see [ELF and ELF2](#elf-and-elf2).

Shared mount fields mean different things per class:

| Offset | Ammunition | Energy |
|---|---|---|
| `+0x10`, `+0x14` | the weapon model and its private cell-frame array — see [The muzzle flash](#the-muzzle-flash) | as ammunition |
| `+0x44` | muzzle flash playing | as ammunition |
| `+0x7b` | rounds remaining | charge target, **and** the pool-arbitration priority |
| `+0x7d` | rounds in 256ths | capacitor level |
| `+0x7f` | — | charge rate, a flat 20 |
| `+0x31` | refire countdown | refire countdown |
| `+0x33`, `+0x3b` | fired-recently blocks | fired-recently blocks (the ELF sustain reads `+0x33`) |
| `+0x43` | — | mid-charge flag |
| `+0x47`, `+0x48`, `+0x84` | — | ELF spin-up running / latched / cell timer |
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

`+0x7b` is a *request*, not a capacity, and it is the manual's **power level**. `FUN_0040f4d8` drops
it to 820 when the mount goes idle, and `WeaponMount_AdjustPowerLevel` (`0040f48c`) is what the pilot
moves it with, ±80 a press over 0..1200 — see
[`weapon-firing.md`](weapon-firing.md#power-level--weaponmount_adjustpowerlevel-0040f48c). The charge
bar's denominator is the fixed 1200, so a mount at its spawn charge fills 960/1200 = four-fifths of
its bar and only a mount turned up ever fills it.

> `WeaponMount_DemandFullCharge` (`0040f4f0`) sets 1200 in one step and looks like the natural
> mechanism, but its only caller has no reference of any kind in the image; neither is reachable in
> the retail build.

Readiness (`WeaponMount_EnergyCanFire`) is `!destroyed && refireTimer == 0 && charge >= threshold`,
where the threshold comes from the template's `+0x36`/`+0x38` pair: `max(+0x36, +0x7b)` when
`+0x36 < +0x38`, otherwise `+0x38` outright. Real templates carry both shapes — `EMP` reads
(350, 10000), `LAS100` (80, 80). The ammunition equivalent (`WeaponMount_AmmoCanFire`) is
`!destroyed && refireTimer == 0 && rounds != 0`.

## ELF and ELF2

`ElfMountVtable` (`004992c0`) replaces five of the energy class's slots. The charge, power-level,
wake, priority and gauge slots are **not** among them, so an ELF carries and charges an ordinary
capacitor and prints an ordinary bar.

| Slot | Energy | ELF |
|---|---|---|
| `+0x28` fire | `WeaponMount_FireDispatch_GunBeam` | `ElfMount_FireDispatch` |
| `+0x2c` ready | `WeaponMount_EnergyCanFire` | `ElfMount_CanFire` |
| `+0x30` trigger | `WeaponMount_TriggerHeld` | `ElfMount_TriggerHeld` |
| `+0x34` pool turn | `WeaponMount_ChargeCapacitor` | `ElfMount_SpinUpAndChargeTick` |
| `+0x5c` | `FUN_004111e9` | `FUN_0040ed34` (returns the destroyed byte) |

**`ElfMount_CanFire` is why an ELF cannot be re-triggered until its capacitor is full.** It reads the
same two template fields as the energy test and drops the branch between them, so the threshold is
always `max(+0x36, +0x7b)`:

```
!destroyed && ( charge >= max(template+0x36, chargeTarget)
                || (mount+0x33 && charge >= template+0x38) )
```

Both ELFs read `+0x36` = 400 against a charge target of 960, so a fresh trigger pull needs the full
capacitor. The second clause is the sustain: `+0x33` means the mount fired on the previous tick, and
while it is set the bar drops to one shot's 70, so the weapon empties itself over successive ticks
and can only start again once it has climbed all the way back. There is no refire-timer term —
which is consistent with both ELFs carrying a `+0x4c` of zero.

`+0x33` and `+0x3b` are two 8-byte blocks. `WeaponMount_PrepareShot` sets both on firing;
`WeaponMount_RefireTick` **ands** `+0x33` with `+0x3b` (`FUN_0040f881`) and then clears `+0x3b`. So
`+0x33` survives only while the mount fires on every tick.

**`ElfMount_FireDispatch`** subtracts `template+0x38` unconditionally rather than capping it at the
charge — the last partial shot takes the capacitor slightly negative, which is what ends the burst —
and passes `Bullet_FireBurst` a **fixed 1200** as the shot power instead of the charge spent. Every
shot of a burst therefore lands at full strength however far the capacitor has drained, which is
what makes the ELF the damage outlier the manual describes from small `PROJ.DAT` figures.

**`ElfMount_TriggerHeld` is a spin-up.** A press sets `+0x47` and returns 0 — no shot that tick.
`ElfMount_SpinUpAndChargeTick` then advances the muzzle-flash flipbook one cell per tick until the
last, at which point it latches `+0x48` and clears `+0x47`; from then on the slot returns the trigger
byte and the weapon fires every tick until release, which clears `+0x48` and rewinds the flipbook to
cell zero. **ELF2 skips it**: the slot opens by forcing both flags to 1 whenever `template+0x56`
(the record's self-index) is 22.

**The flipbook is what sets the delay's length**, so it is data, not a constant: the spin-up runs
for as many ticks as the weapon model has cells. Both ELFs carry `MECHWPNS.DTS` shape 4, seven
cells, so both take seven ticks — about 0.28 s at 25 Hz.

The spin-up only runs while the mount is **ready**: `WeaponMounts_FireTrigger` tests vtable `+0x2c`
before it reaches `+0x30` at all. So an ELF whose capacitor is still filling does not spin up, and
one that empties mid-burst keeps its latch until the trigger is released after it has recharged.

## The muzzle flash

**It is the weapon's own model playing its cell animation.** Nothing spawns an effect for it, and
there is no muzzle-flash resource of any kind — `dts\FIRE.DTS` is the burning-object effect, not
this (see [`dbsim-physics-notes.md`](dbsim-physics-notes.md#rocket-physics)).

Every hardpoint whose mounting code is visible (`.GL +6 < 4`) gets its own copy of the weapon model
when the mount is built:

1. `FUN_0040df30` calls `FUN_0040fab0`, which instantiates `MECHWPNS.DTS`'s shape
   `template[0x22 + code * 2]` from the raw chunk `FUN_0040f998` cached, and binds it the `wpntex`
   atlas at `shape+0x26`.
2. `FUN_00402fc0` stores the shape at `mount+0x10` and allocates `mount+0x14` as a **private copy**
   of its per-sequence frame array, `shape+0x24` entries long.
3. `FUN_0040dd4c` translates the shape's own point lists by `WeaponMount_MuzzleOffset` — the mount
   point, not the muzzle. That private copy is why every mount can carry the same weapon and still
   sit and flash independently.
4. `Mech_ConfigureLoadout` reads the hardpoint's bone from `FUN_0040e61c` (the `.GL` record's `+0`,
   the same bone the shot leaves from) and `FUN_00417530` stamps that transform id onto every part
   of the shape, so it rides the machine's own skeleton.

The animation is `WeaponMount_RefireTick`'s tail, and it is the whole of it:

```
if (mount+0x44) {
    cell = (cell + 1) % *shape+0x20      // cell is element 0 of the private array
    if (cell == 0) mount+0x44 = 0
}
```

`mount+0x44` is raised by both fire dispatches whenever the hardpoint is visible — the ammunition
class on its `Bullet` branch only, since a rocket comes off a rail rather than out of a barrel and
lights nothing. `*shape+0x20` is the shape's `SequenceList[0]`; retail weapons carry two to seven
cells, so a flash lasts two to seven ticks. Cell zero is the gun at rest. Nothing restarts a flash
already running, so a weapon firing every tick shows a cycling book rather than one stuck on its
first cell.

**The ELF's dispatch does not raise `+0x44`.** Its flipbook is driven by the spin-up instead, which
leaves it parked on the last cell for as long as the burst lasts.

## Losing a mount

Two independent paths take a hardpoint out, and both end at `WeaponMount_Destroy` (`0040f57c`),
which is idempotent — its whole body sits under a test of the destroyed byte:

```c
mount+0x10 = 0;      // drop the weapon model: the gun stops being drawn on the chassis
mount+0x49 = 1;      // destroyed: charges nothing, fires nothing, cannot be armed, prints OFFLINE
```

A visibly-mounted hardpoint then throws the template's `mechwpn2` shape (`template+0x26`) off the
mount point as debris, on a `Math_EulerToward` bearing away from the machine, with a longer lifetime
pair when the caller passes a nonzero third argument.

### The certain path — the condition notification

`Mech_ComponentDamageWrite` snapshots **every** mount's component reading before its write and hands
both readings to every mount afterwards, through the mount's vtable `+0x68`. The snapshot has to
cover all of them because the write cascades: a hit on a shoulder can move a mount several components
away. The component a mount reads is `.GL +0x17` + 19 — see
[`damage-system.md`](damage-system.md#weapon-mount-destruction).

`WeaponMount_ConditionChangedBase` (`0040ee0c`), the base class' whole slot: a component reading 256
destroys the mount, with no roll.

`WeaponMount_ConditionChanged` (`0040ee90`), the two classes that carry a weapon: the base first,
then, only while the mount is not empty (`+0x7b != 0`) and the reading is past `0x80`:

| `PROJ.DAT` type | Effect per 25 points of damage past `0x80` |
|---|---|
| `Missile` | Rolls `rand & 0x3ff < 300` — a shade under 30% — and the first success destroys the mount. One hit crossing several steps rolls several times |
| `Bullet` | Sets the refire scale `mount+0x63 = 0x400 - steps * 0x66` |
| `Beam` | Neither |

**A damaged gun fires faster, not slower.** `WeaponMount_PrepareShot` arms
`Q10Multiply(mount+0x63, template+0x4c)`, so halving the scale halves the delay. It reads backwards
for damage and it is what the original does; see [`KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

### The chance path — the destruction roll

A band change on a mount component rolls once to take that mount out, inside
`Mech_ApplyDirectFireDamage`. It is decoded in
[`damage-system.md`](damage-system.md#weapon-mount-destruction), which owns the damage side;
`WeaponMounts_MountForHardpointSlot` (`00410670`) is the component-to-mount lookup it uses, matching
on `.GL +0x17` rather than on a position in the mount array.

## Names — `FUN_0040e18c`

**The simulator does not use the shell catalog's names.** `Weapons_LoadResourceTables` (`0040fc8c`)
walks a 33-entry string-pointer array at `00498eb0` as it reads the template table and stores one
pointer into each record's `+0x52`; that is what a gauge prints. The two spellings are
tabulated per id in [`../formats/weapons-dat.md`](../formats/weapons-dat.md#the-weapon-id-space--three-spellings-per-weapon)
alongside each weapon's full name; the array itself is ported as `WeaponCatalog.MountNames`.

The name is chosen off the **resolved projectile**, not the weapon id: when the mount's `PROJ.DAT`
record is a `Missile`, the gauge prints that record's own subtype from a four-entry table at
`004989c8` — `SARH`, `ARH`, `ARM`, `EO` — so a launcher is named by what is loaded in it. This is
why the retail player's `MSL10` hardpoint reads `ARH`. Ids 13–16's own names are bare round counts
(`"6"`, `"8"`, `"10"`, `"24"`) precisely because a launcher never prints them.

> The subtype index is unbounded in the original. `MISSL` (id 21) points straight at the `BMSL`
> record, subtype 4, and reads one past the four-entry table. The engine falls back to the id's own
> name rather than reproducing a read off the end of a table. `BMSL` is Bull armament and no
> player HERC can mount it.

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
| `+0x0a` | per-subtype missile-lock flags, written every tick by `Mech_PerTickSystemsUpdate` and read by the launcher fire path — see [`missile-lock.md`](missile-lock.md) |
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
anything. `0x30` is the weapon's **range**, so this gate amounts to "is a weapon at all" — every real
firing weapon carries a large positive value and every pod carries zero.

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

- **The missile row's state box.** `FUN_00410970` colours it from the per-subtype lock flags at
  `manager+0x0a`; the engine does not colour that box yet.
- Template fields other than those named here — see
  [`../formats/weapons-dat-sim.md`](../formats/weapons-dat-sim.md).
- **Firing** is in [`weapon-firing.md`](weapon-firing.md). All three dispatch branches are ported;
  auto-fire is not.
- **Auto turret tracking.** `manager+0x14` is latched by the TRACK button and read by nothing in
  Herculan; the tracking itself is unported.
- **A pod's on/off toggle.** Clicking a pod's row in the original flips `gauge+0xc2`
  (`FUN_004419fc`), which re-fonts its name. No pod carries an on/off state here.
- **The debris a destroyed mount throws.** The engine has no debris objects at all, so there is
  nothing for `WeaponMount_Destroy`'s `mechwpn2` spawn to go into.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| The ELF's cell timer at `+0x84` is a per-cell interval, so the spin-up's length is a rate rather than a cell count | `ElfMount_TriggerHeld` zeroes it on the press and `ElfMount_SpinUpAndChargeTick` zeroes it again after every advance, and `Math_CountdownTimerTick` clamps at zero, so it expires on every tick it is asked. Nothing in the retail build ever gives it a non-zero value |
