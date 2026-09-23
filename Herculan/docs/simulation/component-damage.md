# DBSIM.EXE component damage — the `.DMG` health record, cascade, and going out of the fight

Reverse-engineered from `DBSIM.EXE` disassembly (Ghidra project `ES2Recon`). All addresses are DBSIM.EXE virtual addresses. This is the shared endpoint both damage pathways in [`damage-system.md`](damage-system.md) write into — see that doc for how a shot or a blast decides how much damage arrives and at which component. [`weapon-damage-types.md`](weapon-damage-types.md) covers the separate weapon-mount destruction roll and `PROJ.DAT`'s per-weapon damage shape.

## The component damage system

**Flyers have one too.** `Flyer_Constructor` (`004215f4`) allocates the same header at `flyer+0x200` with literal counts of **1 and 1** — one main component, one dependent — which is exactly what `SKIMMER.DMG` ships. The counts are hard-coded at each constructor, not read from the file.

**`this+0x206` is a header of pointers, not inline arrays.** Allocator `Component_AllocDamageArrays` (`0040d2cc`), called as `Component_AllocDamageArrays(this+0x206, 0x1d /*29*/, 0x16 /*22*/)`:

| Offset (abs) | Field |
|---|---|
| `+0x206` | **pointer** to a 22-`short` dependent-subpiece **damage** array, zeroed = undamaged |
| `+0x20a` | **pointer** to a 29-`short` main-component **damage** array, zeroed = undamaged |
| `+0x20e` | **pointer** to a 29-`short` active/occupancy-flag array, all bytes `0x01` at init |
| `+0x21e` | `short` count = 29 |
| `+0x220` | `short` count = 22 |

Every accessor (`Component_ReadDamagePercent`/`Component_ApplyDamageAndCascade`/`Mech_ComponentDamageWrite`/`Mech_ComputeShieldCapacity`/…) treats `this+0x206` as `(int*)` and does an extra pointer dereference before indexing.

- The 22-entry array = accumulated damage on **fine sub-piece / dependent** components (see the aggregation formula below).
- The 29-entry array = accumulated damage on the **main component slots**, the same indexing space both damage pathways' component selection uses.
- The 29-entry flag array = **occupancy/active flag per component slot**, not a second depleting health pool. Zeroed for a slot when that component (typically a weapon mount) is destroyed.

`Component_LinkMaxRefData(this+0x206, mechThis, damageDataPtr, collisionRegistration)` (`0040d354`) — a second constructor call — wires up `this+0x212` (and neighboring fields) as a pointer into per-component **maximum/reference** data (sourced from the mech's own `damage.dat`-derived pointer plus its collision registration record from `Collision_RegisterObject` (`0040cd88`), tying this system to the collision bounding-sphere tree in [`dbsim-physics-notes.md`](dbsim-physics-notes.md#collision-system-collidecpp)).

**Read: `Component_ReadDamagePercent` (`0040dbc0`) — accumulated damage as Q8 (0–256), 0 = pristine, 256 = destroyed.** Note the sense: it returns damage, not health, so every caller's curve runs the opposite way to how a `…HealthPercent` name would suggest. Looks up the component's max-reference record (18 bytes, via `this+0x212`), starts with its own damage (main 29-entry array) and max values, then **aggregates in every dependent sub-component** listed in that record (walking a list, adding each dependent's damage from the 22-entry array and max from a parallel max-side array) before computing `(totalDamage << 8) / totalMax`. An entry holding `-1` (destroyed) substitutes its max, so it reads as fully damaged. A single displayed component's reading can be the aggregate of several finer sub-parts — e.g. a "leg" reading as leg proper plus whatever finer actuator/joint pieces are modeled underneath it ([Open](#open)).

**Write and cascade: `Component_ApplyDamageAndCascade` (`0040da38`)**, called from `Mech_ComponentDamageWrite` (`00417de4`, mech vtable `+0x74`) and `Flyer_ComponentDamageWrite` (`00421bb4`, the flyer's) — the shared endpoint both damage pathways call into.

`Mech_ComponentDamageWrite`'s own first line is `if (obj+0xa3 && Sim_DamageToPlayerDisabled()) return` — the mission's invulnerability setting ([`difficulty.md`](difficulty.md#the-two-sibling-cheats)), and the one thing that can stop the write and everything below it. Shields are outside it, since they are spent in the pathways above.



```
destroyed = Component_AddDamage(&mainDamage[i], piece.Armor, &damage)   // 0040d3ec
if (destroyed) {
    drained = Component_SpillIntoDependents(piece, subDamage, damage, subMax)   // 0040cf44
    if (drained && (piece.DestructionFlags & 1)) {
        Component_DestroyAndCascade(i)                     // 0040d434
        drain the pending BoneId queue through the same call
    }
}
```

- `Component_AddDamage` **adds** damage, stores `-1` rather than the max once the entry is finished, and **writes the excess back into `damage`**. An entry already at `-1` absorbs nothing, so a lost part cannot be shot again.
- `Component_SpillIntoDependents` pours that excess into the component's dependents, **one at a time, weighted and random**: each live dependent contributes its `CritChance` to a total, a draw under that total picks the one that takes the hit, and if that spill destroys it the remainder goes round again. It returns true only once no live dependents are left — which is why a component with internals still intact does not cascade even after its own armour is gone.
- `Component_DestroyAndCascade` writes `-1`, clears the active flag, finishes off everything under it with a flat 32000, and queues every live piece whose `BoneId` names this component. The original drains that queue iteratively rather than recursing.
- `Component_IsFullyDestroyed` (`0040d9f8`) ("is component *i* destroyed **and** all of its dependents too", via `Component_AllDependentsDestroyed` (`0040cf10`)) is the stricter test the mech's death gate asks of its two cockpit slots.

### The 18-byte record — `.DMG`'s `HercPiece`

`HercWorks.Core.Data.File.Dbsim.HercSimDamage.HercPiece`, loaded from `dmg\[herc].DMG` (confirmed by tracing `Damage_LoadMechDmgFile` (`0040d160`)'s caller `Mech_Constructor` (`00415bb0`), the mech constructor, which builds the filename from the mech's own name string plus extension).

The whole file, per `HercPiece_LoadTable` (`0040d09c`) — **no padding anywhere**:

```
subCount, subCount * int16 dependent max armour
pieceCount, pieceCount * 18-byte HercPiece
```

Retail: 22 dependents and 29 pieces for every HERC, 1 and 1 for `SKIMMER`. Only dependent slots 0–11 carry a nonzero maximum, and the pieces reference no index above 11.

| Offset | Field | Evidence |
|---|---|---|
| `+0x00` | `short` `Armor` (max health) | `Component_ReadDamagePercent`'s `local_10 = *psVar5` |
| `+0x02` | `signed char` — the debris group this component throws, `-1` = fall back to group 2. See [`destruction-effects.md`](destruction-effects.md#the-two-database-index-space) | `Component_DestroyAndCascade`, on destruction |
| `+0x03` | `signed char` — the `TSCellAnimPart` sequence this component drives on the machine's shape, stepped to its blank cell on destruction (`*(int*)(mechThis+0x34)+8`, `[index] = 2`), `-1` for a component with no geometry of its own. It also gates the fire — see [`../formats/mech-shape-drawing.md`](../formats/mech-shape-drawing.md) | `Component_DestroyAndCascade`, guarded by `-1 < value` |
| `+0x04` | `signed char` `BoneId` — the **index of the parent component** this one hangs off, `-1` for none. Destroying component *n* queues every still-live piece whose `BoneId` is *n*. Retail: ACHILLES' leg chain runs 7→9→11, and its two weapon brackets (4, 5) carry components 19–25. SPIDER sets `-1` throughout, so nothing on it cascades | `Component_DestroyAndCascade`'s trailing loop |
| `+0x05` | `byte` `DestructionFlags` bitfield: bit0=has dependents to cascade, bit1=alt destruction-effect mode, bit2=one-shot "major alert already fired" latch, bit3=triggers secondary effect callback | `Component_ApplyDamageAndCascade`/`Component_DestroyAndCascade` |
| `+0x06` | `short` dependent sub-component count | `HercPiece_ReadRecord` (`0040cff8`, loader), `Component_ReadDamagePercent`'s loop bound |
| `+0x08` | `int` pointer to the dependent list (4 bytes/entry: index at sub-offset `+2`) | `HercPiece_ReadRecord`/`Component_ReadDamagePercent` |
| `+0x0c` | `short` sentinel `0xffff` (runtime-only) | `HercPiece_ReadRecord` |
| `+0x0e` | `int` zero (runtime-only) | `HercPiece_ReadRecord` |

`+0x02`/`+0x03` together form the C# port's `DebrisFlags` (one `short`); `+0x05` is `DestructionFlags`.

### Component naming and index semantics

The Java author's own doc comment on `HercSimDamage.cs` lists real component names in array order: `COCKPIT/FRONT`, `COCKPIT/REAR`, `SHOULDER/LEFT`, `SHOULDER/RIGHT`, `WEPN_BRACK/LEFT`, `WEPN_BRACK/RIGHT`, `TORSO`, `LEG/LEFT/UPPER`, `LEG/RIGHT/UPPER`, ...

- **Indices 0–1 (`COCKPIT/FRONT`/`REAR`)** — individually checked (`Component_IsFullyDestroyed`) as the mech's death-trigger gate.
- **Indices 4–5 (`WEPN_BRACK/LEFT`/`RIGHT`)** — ordinary weapon-mount slots inside this same 29-entry array (see [`weapon-damage-types.md`](weapon-damage-types.md#weapon-mounts) for the separate runtime ammo/heat state).
- **Dependent-array (22-entry) slots read by literal offset in `Mech_ComponentDamageWrite`**, not by a loop. 0 and 1 are the front leg servos, joined by 10 and 11 (the rear pair) when `typeRecord+0x4a` is 4; the pair(s) are averaged before being compared against `0x8d` (crippled) and `0x50` (the milder grade), and half of them destroyed immobilises the machine. 4 is the shield generator, which `Mech_ComputeShieldCapacity` reads — so shooting it shrinks the array the machine can hold, and that recompute happens **here as well as at spawn**. 5 is the reactor, latching the two output-damage flags. 8 and 9 are life support and the pilot: either destroyed, or either cockpit slot fully gone, and the machine dies.

### What the endpoint announces

`Mech_ComponentDamageWrite` is also where the cockpit computer's damage warnings are posted, and **every one of them is gated on `obj+0xa3`** — the machine being the one the player is flying — so an AI machine losing a leg says nothing. The ids are `SYSTEM.STR`'s and the port they go to is [`../formats/cockpit-messages.md`](../formats/cockpit-messages.md#the-port)'s.

| id | line | guard |
|---|---|---|
| `0x03` | `INTERNAL DAMAGE: SHIELD GENERATOR` | dependent 4's reading was 0 before the write and is not after |
| `0x0c` | `SHIELD GENERATOR DESTROYED` | that reading was under `0x100` and is now `0x100`. Independent of `0x03`'s test rather than its other arm, so a hit that takes an untouched generator out posts both |
| `0x10` | `WEAPON DESTROYED` | a mount's own component was under `0x100` before the write and is `0x100` after. **Once per write, not once per mount** — the walk over the mounts raises a flag and the post comes after it, so a cascade that strips several hardpoints says it once |
| `0x08` | `INTERNAL DAMAGE: LEG SERVOS` | fewer than half the servos gone, both graded sides under `0x8d`, one of them over `0x50`, and `mech+0xa8` clear |
| `0x13` | `STRUCTURAL FAILURE IMMINENT` | the same with a side at or past `0x8d`, on `mech+0xa9` |
| `0x04` | `INTERNAL DAMAGE: ENGINE` | the reactor grade crossing either band. Two call sites, one per latch — `mech+0xaa` for `0x81`-`0xc0`, `mech+0xab` past `0xc0` — posting the same line; but the grade is only read while **both** latches are clear, so a machine announces its reactor once however far it goes on degrading |
| `0x2e` | `ENEMY TARGET DESTROYED` | the death gate, on the shared predicate below |
| `0x2f` | `ENEMY TARGET DISABLED` | the leg branch's immobilise, on that same predicate |

`0x15` `SHIELDS CRITICAL` belongs to the same family from one function further out: `Mech_DirectFireHitTest` posts it where it sets `mech+0xb0` (`00418dc7`), on the first shot to land on the player's own machine with under 500 points of charge left across both facings.

**Four of the five latch bytes are one-shots that are never cleared** — `+0xa8`/`+0xa9`/`+0xaa`/`+0xab`, each written `1` exactly once, in `Mech_ComponentDamageWrite`. They are why a machine that keeps taking hits in the same band does not repeat itself, and they are separate from the port's own 4.8 s repeat swallow, which would not be enough on its own. All four are load-bearing elsewhere as well: `+0xa8`/`+0xa9` are the two speed penalties ([`mech-locomotion.md`](mech-locomotion.md)) and `+0xaa`/`+0xab` the reactor's output grades.

**`+0xb0` is the exception: it re-arms.** `Mech_PerTickSystemsUpdate` clears it (`0041ab25`) on the player's own machine every tick that `front + rear` exceeds `0x5dc` (1500), so `SHIELDS CRITICAL` is hysteretic — it fires under 500 and can fire again once the array has rebuilt past 1500, with the band between the two thresholds leaving the latch as it was. The same byte is what the MFD status screen reads for its `SHIELDS DN` condition, so that indicator clears itself on the same threshold.

**`0x2e` and `0x2f` share a predicate, and it does not test sides**: the attacker is the machine the player is flying, and the victim is that machine's own selected target (`mech+0x1a4`). Nothing is asked about whose side the victim was on. `0x2e` has **three** call sites — this endpoint, the flyer's `+0x74` (`Flyer_ComponentDamageWrite`) and `Base_ApplyDamage` (`00404d70`) — so it covers a HERC, an aircraft and a building alike. What the missing side test costs is in [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

### Going out of the fight

Two independent branches, and they are **not** two readings of one condition. Losing legs disables; losing the cockpit, the pilot or life support kills.

**Disabled** — the leg branch, non-flyers only. A leg whose servos read fully destroyed has its child object deleted, so `Mech_PlaceLegsOnGround` stops placing it; that runs whatever else is true of the machine. Then, if it is not already immobilised and half or more of its legs are gone:

1. the attacker is told, through *its own* vtable `+0x60`, with "was already immobilised" clear;
2. the machine's own defeat action fires;
3. `disabled` (21) is installed;
4. `mech+0xa4` immobilised is latched and the target released.

**Dead** — the cockpit/pilot/life-support gate. `mech+0x99` is set, then, in this order:

1. **the reading of `+0xa4` is sampled**, because the next step invalidates it;
2. the recursive finish-off, a flat 30000 on component 0 with no attacker, which is why a kill leaves a machine comprehensively wrecked rather than merely stopped — and which re-enters the leg branch, harmlessly, since both branches are guarded on `+0x99` being clear;
3. the attacker is told, carrying that sampled reading;
4. **the defeat action fires only if the machine was not already immobilised**, so it goes off once per machine rather than once per way of stopping it;
5. target released, scanner forced passive;
6. the state: `in limbo` (19) when the chassis' `typeRecord+0x4c` is set, otherwise `dead` (20) for a non-flyer. **A flyer takes neither**, and keeps whatever state it was in.

`typeRecord+0x4c` means *this chassis leaves no wreck*, and the SPIDER is the only one that sets it: that branch also sinks the object to z = -100000, raises `obj+0x38` ([below](#the-no-wreck-sinks-flag-byte--obj0x38)), and hands every child part in `mech+0x238` to `ObjectPool_QueueForDelete` (`00418634`), nulling each slot and zeroing the count at `mech+0x23c`. The machine itself is not queued — the branch takes it off the screen by sinking it, not by removing it from the object list.

The three state indices and their think are [`ai-combat-states.md`](ai-combat-states.md)'s. What a machine does *after* the state is installed — the fall, and the collapse that ends it — is [`mech-locomotion.md`](mech-locomotion.md#going-down)'s.

### Spread impact damage — `Mech_SpreadImpactDamage` (`00417a04`)

One impact spread over the whole machine, rather than a shot aimed at a component. Every live component draws its own roll out of 256 against `odds`; one that is caught takes `Q8(rand(maxDamage) + maxDamage/2, totalArmor)` — so the share scales with what that component had to lose. `totalArmor` is `Component_TotalArmor` (`0040dc58`), a component's own armour plus the maximum of every internal mapped onto it, which is also the denominator the damage percentage uses. A `maxDamage` of zero returns immediately.

The damage goes in through this same `+0x74` endpoint, cascade and death gate included. Two callers: the collapse landing ([`mech-locomotion.md`](mech-locomotion.md#going-down)) and the starting condition below.

### Starting condition — `Mech_ApplyStartingCondition` (`004178e8`)

Called once from `DBSim_SpawnMissionObjects`, immediately after the machine's two mission actions are resolved, with the percentage at [`../formats/script-dat.md`](../formats/script-dat.md)'s block 7 `0x84`. The odds are always the damage figure plus 25.

| Condition | Effect |
|---|---|
| ≥ 80, or negative | untouched |
| 60–79 | 50 damage at 75 |
| 40–59 | 80 at 105 |
| 20–39 | 120 at 145, the **reactor dependent is set to its own maximum** — written off outright rather than damaged toward it — and `+0xb3` is raised |
| < 20 | a **wreck**: one of components 7/8 destroyed with 32000 (and one of 13/14 on a four-legged chassis), `+0xa4` immobilised, `+0xb3` raised and `+0xb4` collapsed, then 150 at 175 over the rest |

The order matters: writing a leg off can fire the death gate, so the defeat action has to be attached first. The wreck grade places a derelict as scenery — already down, so it never falls, and never targetable.

**`+0xb3` is *worth no salvage*** — below.

### What a wreck is worth — `Mech_SalvageValue` (`00418e60`)

What the player's side takes home. `Mission_TotalSalvage` (`00423e88`) walks the object list at the end of the run and sums this function over every machine that is on the other side from the player's and is destroyed (`+0x99`) or immobilised (`+0xa4`); the total is scaled by `Q10(2500)` and added to the campaign's salvage pool ([`../formats/save-games.md`](../formats/save-games.md)) as the mission writes its results.

Per machine, in order:

1. **`mech+0xb3` short-circuits it to zero.** A machine the mission placed already broken — the two worst starting-condition grades above — is worth nothing, so a mission cannot be farmed by authoring derelicts into it.
2. **Each surviving hardpoint is queued.** For every mount whose component is under `0x80` damage, `maybe_Salvage_QueueDestroyedWeapon` (`00426ac8`) takes `{template+0x56, (0x100 - damage) * 100 / 256}` — the weapon's catalog id and its condition as a percentage. This is the same queue the mount-destruction path appends to; see [Weapon-mount destruction](weapon-damage-types.md#weapon-mount-destruction).
3. **The chassis itself** is `Q10(Mech_WeightedArmorRemaining(mech), typeRec+0x54)`, and `typeRec+0x54` is **halved when component 0 is at full damage** — a chassis blown apart is worth half one merely stopped. `Mech_WeightedArmorRemaining` (`0041537c`) sums `(maxArmor - damage) * weight / maxArmor` over the live components, against the weight table at `00499fe0`; `maxArmor` is the component's own `.DMG` record and a component at or past 150 damage contributes nothing.

So a machine pays for what survived, not for what was wrecked, and its guns pay separately by how intact each one is.

### The no-wreck sink's flag byte — `obj+0x38`

The sink raises `obj+0x38` alongside dropping z to -100000, and **that byte has no reader**. All three classes' no-wreck branches write it — `004039a1` for a structure, `004185ec` for a machine, `00421c36` for a flyer — and it sits inside the 8 bytes `SimObjectBase_Constructor` zeroes at `00402250`, so it is a deliberately maintained flag rather than padding. A scan of the whole disassembly that resolves `LEA reg,[base + k]`, `ADD reg,k` rebasing and Borland's spill-and-reload of a rebased pointer finds no read of it on a sim object, against a control on `obj+0x39` (the shape layer's own flag beside it) that finds three — `FUN_00402400`, `SimObject_ApplyRootMotionIfEnabled` and `Sim_PollPlayerInput`.

**This is a null result and nothing more.** The byte carries a meaningful value, so the absence of a reader rests entirely on the scan being exhaustive, which it cannot be shown to be. Treat it as a reason the port leaves the byte out, not as a proven property of the original.

## Port notes

The traps, not a summary — everything else here is stated once above and does not need repeating.

1. **Component health is a dependency graph, not a flat HP list.** A component's reading aggregates its dependents, and destroying one cascades into them.

## Ported

The parts of `Mech_ComponentDamageWrite` that change behaviour — the shield-capacity recompute, leg grading, the death gate, the reactor flags, and the warnings all four of those post — are `Herculan.Engine.Sim.MechObject.Combat`'s; the whole `+0x206` header — the three arrays, the aggregate read, the spill and the cascade — is `Sim.ComponentDamage`.

The destruction path's own effects — the debris, the fire and the explosion a lost component throws — are `Sim.ComponentDamage.DestructionEffects`; see [`destruction-effects.md`](destruction-effects.md).

Both out-of-the-fight branches are ported entire, including the behaviour-state installs, the sampled-before-the-finish-off ordering the defeat action depends on, and the vtable `+0x60` kill credit (`MechObject.CreditNeutralised`) with both of its radio callouts — the scorer's `0x02` and the victim's `0x25`/`0x04`, the latter being the original's only forced post ([`../formats/cockpit-messages.md`](../formats/cockpit-messages.md#what-each-id-says)).

`Mech_CreditNeutralisedTarget` is `void __cdecl(SimObject *attacker, SimObject *victim, short victimAlreadyImmobilised)` — plain `__cdecl` on three stack arguments, whatever the decompiler's `__thiscall` rendering of the vtable slot says. All four call sites push three and clean 12 bytes. `Mech_SpreadImpactDamage` is `MechObject.SpreadImpactDamage` and `Component_TotalArmor` is `ComponentDamage.TotalArmor`; `Mech_ApplyStartingCondition` is `MechObject.ApplyStartingCondition`, called from `Scene.MissionScene` where the original calls it.

The computer's warnings are posted from the sites above through `SimWorld.Sounds.Say`, with the five latches already carried as `MechObject.LegsDamaged`, `LegsCrippled`, `Reactor` and `ShieldsDownAlert`; `SimObject.AnnounceNeutralised` is the `0x2e`/`0x2f` predicate, called from all three endpoints. The ids are `Content.SystemMessages`'.
## Open

- **Open:** the exact sub-piece breakdown per component.
- **Unported:** the salvage pass — `Mech_SalvageValue`/`Mission_TotalSalvage` ([above](#what-a-wreck-is-worth--mech_salvagevalue-00418e60)), `mech+0xb3`, and `Mech_ReportOutOfAction`'s mission-variable writes.