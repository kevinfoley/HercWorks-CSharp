# DBSIM.EXE weapon damage types — `PROJ.DAT`, the structural/internal/weaponry split, and weapon mounts

Reverse-engineered from `DBSIM.EXE` disassembly (Ghidra project `ES2Recon`). All addresses are DBSIM.EXE virtual addresses. Confirmed against the official *Earthsiege 2 - On-Line Manual.pdf* where noted. See [`damage-system.md`](damage-system.md) for the direct-fire and explosive pathways that read the figures below, and [`component-damage.md`](component-damage.md) for the `.DMG` health record and cascade a weapon-mount hit writes into.

## Structural / Internal / Weaponry

The manual describes a HUD/HDD display split into "structural, internal, and weaponry" categories. This is **three different mechanisms**, not a 3-way partition of one index space:

- **Structural** = most of the 29-slot `HercPiece` array — named body pieces (torso, legs, feet, shoulders).
- **Weaponry** = a *subset of that same array*, distinguished only by name/position (`WEPN_BRACK/LEFT`/`RIGHT`). Weapon-specific runtime state (ammo, heat) lives elsewhere, in the weapon-mount-manager object (`this+0x202`), not in this health record.
- **Internal** = a *wholly separate*, smaller table, `HercInternals` (Left/Right Leg Servos, Sensor Array, Targeting Computer, Shield Generator, Engine, Hydraulics, Stabilizers, Life Support, Pilot) — reached *probabilistically* through a struck structural/weaponry piece's own `MappedInternals`/`CritChance` list, not directly targetable. An Internal system has no health slot of its own in the 29-component array; damaging it is a chance-based side effect of hitting whichever structural piece maps to it.

"Armor" in the manual's "where shields leave off, armor takes over... duranium plates" sense maps to the per-component `Armor` field on `HercPiece` (`this+0x20a`/`this+0x206`) — not a separate third depleting pool distinct from "structure." Genuinely still open: whether shields differentiate by weapon type anywhere (checked, not found in the shield-absorption functions themselves — see "Weapon-type effectiveness" below).

## Weapon-type effectiveness

The manual: "Two weapons are effective against shields: EMP cannons disrupt the shield matrix, and the ELF is so incredibly powerful that it punches through shields as if they are not even there," "energy weapons... are effective against shields... Projectile weapons have longer range and do more damage to enemy armor, but have little effect on a target with shields," Lasers have "limited effectiveness against shields," ATCs are "fast and hard enough to penetrate most armor plating."

**Not found in code.** The one candidate — `Mech_ApplyDirectFireDamage` (`004188c8`)'s `Math_Q10Multiply(shotData[+8], armorDamage)` — is `SplashFactor`, the secondary-explosion split documented below, not a per-weapon-type effectiveness scale. The whole of the manual's claim that lives in code is the two separate `DamageShield`/`DamageArmor` figures each `PROJ.DAT` record carries; nothing scales either by the *target's* defence type. The shield absorption functions were also checked and carry no weapon-type term.

The shot descriptor (`shotData`, the same struct `Mech_DirectFireHitTest` (`00418ba8`) and `Mech_ApplyDirectFireDamage` consume) is built in `Bullet_FireBurst` right before the `Sim_RaycastObjectList` (`00426528`) raycast call from the firing weapon's `PROJ.DAT` record — layout in [`weapon-firing.md`](weapon-firing.md#the-shot-record); `+0x04` is the figure `Mech_ApplyDirectFireDamage` applies to structure and armor, `+0x06` the one `Mech_DirectFireHitTest` feeds into shields.

The record table is `PROJ.DAT` (`HercWorks.Core.Data.File.Dat.Sim.ProjectileData`). Cross-checked against the real retail `ES2\VOL\simvol0\dat\PROJ.DAT` (984 bytes: 9-byte VOL prefix + `[Total:u16=27][27×36-byte records]` + 1 trailing marker byte): values line up with the manual —
- Entries with `DamageShield ≫ DamageArmor` (e.g. 2000/400, 8000/2000) — EMP-shaped.
- Entries with `DamageArmor ≫ DamageShield` (e.g. 400/1600, 3000/7200) — ordinary Autocannon-shaped.
- Several `DamageShield ≥ DamageArmor` entries with `Speed=0` (no travel time) — beam-shaped.
- The first 3 entries (60/360, 120/480, 180/600, `Speed=5000`) match ATC20/35/50.

**`DamageShield`/`DamageArmor` are the weapon's own base damage stats against each defense type** — the value scaled against them (`shotPower`) is not a "raw damage" the file further adjusts.

`shotPower` is the capacitor charge the shot was fired at, `min(template+0x38, mount+0x7d)`, and the scale is **Q10** — against a capacitor scaled to 1200, so a mount holding more than 1024 makes a shot worth slightly more than the record's face value. `SplashFactor`'s own multiply below is Q10 as well (`Math_Q10Multiply`, `0047dfa4`).

**`SplashFactor` (`Unk2_val`, short-index 4, `shotData+8`) — a per-weapon splash/secondary- explosion trigger, not a third damage-type multiplier.** Consumer, `Mech_ApplyDirectFireDamage`:
```c
uVar1 = Q10mul(shotData+8 /*SplashFactor*/, shotData+4 /*armor-scaled damage*/);
call obj[+0x74](obj, part, armorDamage - uVar1, ...);      // general component health takes the REMAINDER
if (uVar1 != 0) call obj[+0x70](obj, uVar1, ..., blastRadius=500, ...);  // secondary explosion, same formula explosive weapons use
```
A Q10 **fraction of the already shield-absorbed armor damage** diverted into a small (500-unit-radius) secondary explosion instead of applying straight to the struck component's health. Zero means no secondary explosion — the guard (`if (uVar1 != 0)`) skips it and the full armor-damage amount goes straight to health.

Real nonzero values (`500` or `1000`) appear scattered across several weapons, most consistently for one whole weapon family (uniform `DamageShield==DamageArmor`) — a plausible match for Electron Flux, not proven.

**It is a direct call on the struck object, not a sweep**, so the blast stays inside the machine that was hit and cannot reach anything standing next to it. It also runs the share through `Mech_ShieldAbsorb_Explosive` a second time — the original does not exempt one that has already been through `Mech_ShieldAbsorb_DirectFire` — so both that absorption and its 4× apply on top of what the shot already lost to shields.

**The loader:** `Weapons_LoadResourceTables` (`0040fc8c`) opens `"wpntex"`, `"mechwpn2"`, `"weapons"` (count + 88-byte records — plausibly a per-hardpoint mount-template table, not traced further), then `"proj"` and reads its count + 36-byte records in one flat read into `DAT_004a9980`, linear-searched by `Proj_LookupRecord(category, subtypeId)` (`0040ffc8`) — `PROJ.DAT`'s in-memory copy is keyed by `(category, id)` (matching `Projectile.Type`/`MissileId`), not by flat array index.

### `Type` — a firing-mechanism selector

Traced all 5 callers of `Proj_LookupRecord` and all 3 of `Bullet_FireBurst` — the two weapon-mount fire dispatches (`WeaponMount_FireDispatch_GunBeam` `0040eae0`, `ElfMount_FireDispatch` `0040ecc5`) and `maybe_Base_TripleTurretThinkTick` (`00404a65`), so a **structure's turret fires beams through the same path a HERC does**. Each caller hardcodes a literal category constant, and each corresponds to a genuinely different projectile *class* (different vtable, different construction):

| `Type` | Constructor | Object kind | Real `PROJ.DAT` shape |
|---|---|---|---|
| `0` | `Missile_Construct` (`0040a948`) | the launcher round (14-byte type table `ROCKETS.DAT`, vtable `RocketVtable` (`00498448`)) — see [`rockets.md`](rockets.md) | 5 entries, `SplashFactor=500` uniformly, real `Speed`, armor≫shield |
| `2` | `Bullet_Construct` (`0040af6c`) | the travelling gun round (own 14-byte type table `BULLETS.DAT`, own vtable `BulletVtable` (`00498628`)) — see [`projectiles.md`](projectiles.md) | mixed: ATC20/35/50-shaped progression *and* EMP-shaped high-shield entries — `SplashFactor=0` for all but `MissileId=9` (Plasma cannon, below) |
| `3` | `Grenade_Construct` (`0040ac3c`) | **dead code** — a cut `Grenade` class, see below | 3 entries, shield==armor exactly, `SplashFactor` 1000/500/500, all unreachable |
| `4` | `Bullet_FireBurst` (`0040bf74`) | **no persistent simulated object at all** — resolves its raycast hit synchronously inside the call itself, then spawns pure-visual tracer segments | every `Type=4` record has `Speed=0`, no exceptions |

`Type=4`'s "no persistent object, resolves at the call site, always `Speed=0`" combination is the concrete mechanical definition of a beam/hitscan weapon. Only `0` and `2` are live classes: the ammunition dispatch (`WeaponMount_FireDispatch_Missile`) tests for `Type == 0` and sends everything else to `Bullet_Fire`, and `Rocket_Fire` always builds the `Type 0` class.

**`Type 3` is unreachable, and this one is settled rather than inferred from a caller sweep.** A scan of the whole image for the constructor's address — every section, as a bare little-endian dword as well as an `E8`/`E9` rel32 branch target, so a factory table or a jump thunk would show — finds the address nowhere but in its own prologue. Everything downstream follows: `Grenade_Construct` is the only caller that hands `Proj_LookupRecord` a category of 3, so the three `Type 3` records are never looked up; `GrenadeVtable` (`004984fc`) is installed only by it; and that vtable's per-tick slot is `FUN_0040acb4`, a bare `return 0`, so an instance would never move and never die.

**The class is `GRENADE`, and the binary says so itself**: its Borland class record is at `0040acdc` ([`../formats/borland-rtti.md`](../formats/borland-rtti.md)), and `GrenadeVtable` points back to it from `-0x0c`. The record's destructor (`0040ad2c`) is the one in the vtable's `+0x08` slot. Its one base is `PROJECTILE` (`0040c2d0`), the same as for `ROCKET` (`0040ab3d`) and `BULLET` (`0040b5fd`). That makes it their sibling, which the vtables confirm: all three install `ProjectileBaseVtable` before their own. `ROCKET` and `BULLET` each also have a pointer-type record (`"ROCKET *"`, `"BULLET *"`), but `GRENADE` has none. That fits a class that no reachable code handles through a pointer.

So `Type 3` is a cut **grenade** weapon class, not an unnamed stub, and its three `PROJ.DAT` records are that weapon's data left in the shipped file — see [`../cut-content.md`](../cut-content.md#projectiles).

Mapping onto the weapon taxonomy — flagged as a reasoned hypothesis from mechanism + shape except where noted confirmed:
- **`Type 4` (beam) → Lasers + PBW.** Two unusually low-damage `Type 4` entries (150/200, 200/300, both far below the others' 1000+ values) plausibly fit Electron Flux, not confirmed.
- **`Type 2` (real flight time, no splash) → Autocannons + EMP** — accounts for every `Type 2` entry except the one Plasma outlier.
- **`Type 0` (5 entries) → the game's Missile weapons**, confirmed: its five subtype ids are the five `ROCKETS.DAT` records, and the four the `MSL` launchers reach are `SARH`/`ARH`/`ARM`/`EO` while `BMSL` takes the fifth. `Type 3`'s three entries are data for a class that never runs.

**Plasma cannon — confirmed.** The one `Type 2` outlier (`DamageShield==DamageArmor==3000`, `SplashFactor=1000`) is `MissileId 9`. The `Bullet` class's vtable (`BulletVtable` (`00498628`)) per-tick slot (`+0x14`) is `Bullet_TickUpdate` (`0040b124`), whose `type == 9` branch (checked via `*(char*)(this+0x41) == '\t'`) calls the explosion formula directly instead of the ordinary single-target hit path — `this+0x41` is exactly where every projectile constructor (`Missile_Construct`/`Bullet_Construct`/`Grenade_Construct`) stores its own `MissileId` argument, so this is checking `MissileId==9` on a live `Bullet` instance. `(Type=2, MissileId=9)` is mechanically a `Bullet` (real flight time, unlike true `Beam`s) that explodes with splash on impact (unlike every other `Bullet`), matching the manual's Plasma description exactly.

A weapon's `(Type, MissileId)` pair is set upstream, in the mount template table ([`../formats/weapons-dat-sim.md`](../formats/weapons-dat-sim.md)) via each template's `ProjDatIndex` — the engine looks records up by key, never by array position. `MissileId` also indexes `BULLETS.DAT`/`ROCKETS.DAT` for model data.

### Beam-weapon dispatch

Beam weapons need no special hit-test call: they go through the same `Sim_RaycastObjectList` raycast every other weapon uses, just synchronously, once, at fire time, with no persisting object afterward. The dispatch itself is in [`weapon-firing.md`](weapon-firing.md#the-fire-dispatch--vtable-0x28).

**Do not conflate two similar-looking fields.** The shot-record field `shotData+0x12` (hardcoded `5` for `Bullet_FireBurst`'s bullets) is a different numbering scheme from `PROJ.DAT`'s own `Type` field. `shotData+0x12` gates an unrelated target-side alert/timer effect; `PROJ.DAT`'s `Type` is what determines beam-vs-projectile behaviour, at the mount's fire-dispatch decision, not in the shot record's own flag byte.

## Weapon mounts

`this+0x202` is a **pointer to a separately-allocated weapon-mount-manager object** (own vtable; size `0x14` or `0x35` bytes depending on the `this+0xa3` "locally-simulated" flag), allocated in `Mech_ConfigureLoadout` (`004175dc`, the mech loadout-(re)configuration function, called on spawn/equip changes): `*(int**)(this+0x202) = malloc(...)`, thereafter accessed via `(**(vtable)(*(this+0x202)))` virtual calls. It's referenced throughout `Mech_ComponentDamageWrite` (`00417de4`) for computing ammo/heat-style ratios, by `FUN_00415558` (a "find the next occupied weapon slot" iterator, walking a 7-entry table `DAT_0049a060`), and is where the shield-recharge tick's energy-arbitration vtable call goes (see [`damage-system.md`](damage-system.md#the-shield-system)). `this+0x20e`'s per-slot indices, the weapon mount active flags, are a *different* array from `this+0x202` itself — see [`component-damage.md`](component-damage.md#the-component-damage-system). Losing a mount matches the manual's "Weaponry" HDD damage category and is the section below.

## Weapon-mount destruction

Components **19-28** are the machine's weapon mounts. The component a mount occupies is its `.GL` record's `+0x17` plus 19, which is also how `Mech_ConfigureLoadout` registers each mount's collision and damage records; `WeaponMounts_MountForHardpointSlot` (`00410670`) is the lookup back.

`Mech_ApplyDirectFireDamage` rolls once for a hit that moved a mount component into a new damage band:

```c
if (after != 0x100 && typeRec+0x56 != 0 && component > 0x12) {
    odds = (obj[+0x45][+0x12] == 0) ? 3 : 10;            // the mission group's side byte
    if ((rand & 0xfff) < odds * 0x29) {                  // 3/4096-per-41 vs 10, ~3% vs ~10%
        WeaponMount_Destroy(mountFor(component), mech, 1);
        mech+0x20e[component] = 0;                       // clear the active flag FIRST
        if (side == 1) queueSalvage(template+0x56, condition);
        Component_ApplyDamageAndCascade(component, 10000);
    }
}
```

Four things a port has to keep:

- **The chassis gates it.** `typeRec+0x56` is record offset 84 (the record sits at `MECH_TYPE_DATA[i]+2`), and the PITBULL alone states zero — its mounts are immune to the roll, though not to the certain path. See [`mech-locomotion.md`](mech-locomotion.md#mech-type-record).
- **The odds depend on whose machine it is**: about 3% for the player's side, about 10% for the Cybrids.
- **The order of the three writes.** Clearing the active flag before the flat 10000 is what stops the component cascading, so losing a gun does not take the shoulder it hangs off with it. `Component_ApplyDamageAndCascade` does **not** test the active flag — the flag gates `Mech_ComponentDamageWrite` at its entry and `Component_DestroyAndCascade`, and neither of those is reached here.
- **The Cybrid branch queues salvage.** `maybe_Salvage_QueueDestroyedWeapon` (`00426ac8`) appends the destroyed weapon's catalog id (`template+0x56`) and its remaining condition to a global list.

The mount side of all this — what `WeaponMount_Destroy` writes, and the second, certain path through each mount's vtable `+0x68` — is in [`weapon-mounts.md`](weapon-mounts.md#losing-a-mount).

## Ported

Weapon-mount destruction is ported on both paths — `Sim.WeaponMount.Destroy` and `ConditionChanged`, and `MechObject.RollWeaponMountDestruction`.
