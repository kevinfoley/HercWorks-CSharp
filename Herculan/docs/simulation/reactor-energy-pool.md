# DBSIM.EXE reactor and Master Energy Pool

Solved 2026-08-23. Ported in `Herculan.Engine.Sim.MechObject.Power.cs`, `ShieldCharge.cs`,
`MechPods.cs`.

The reactor and the pool are separate things: the reactor is a **rate** (`mech+0x256`), the pool is
a **capacitor** (`mech+0x292`). Consumers draw on the pool, never on the reactor.

## The per-tick cycle — `Mech_PerTickSystemsUpdate` (`0041aa5c`)

Called once per live mech per tick from `Sim_MainTick` (`0045f464`). Its first five statements are
the whole power model:

```
pool += IntegrateRateOverTick(reactorRate)      // ADD word ptr [EBX+0x292],AX
budget = pool - 500                             // ADD DX,0xfe0c
budget = weaponMountManager->vtable[0](budget, mech)
budget = Shield_RechargeTick(mech+0x222, budget)
pool   = budget + 500                           // overwrite, not subtract
pool   = clamp(pool, 0, 10000)
```

Consumption is not a subtraction: the pool is **rebuilt** each tick from whatever survives the pass.

- **Reserve = 500.** Held out of every arbitration, so a machine under sustained load settles at 500
  rather than 0. Below it the budget goes negative, which energy mounts read as "give charge back".
- **Ceiling = 10000**, also the value `Mech_Constructor` writes at spawn — a HERC powers up full.
- **Order is weapons, then shields.** The manual's "movement, shields, and weapons, in that order" is
  wrong on both count and order; locomotion never reads the pool. The consequence it describes is
  real: shields only ever get what the weapons leave.

## Reactor output rate — `FUN_00417d08`

```
rate = 20                                  // MOV ESI,0x14 — a literal
if   (mech+0xab) rate = Q10(200, 20)  = 3  // reactor critical
elif (mech+0xaa) rate = Q10(600, 20)  = 11 // reactor degraded
if (EnergyPod && podDamage < 225)
    rate += Q10(1024 - 204*(podDamage/51), 20)   // up to +20
```

**Base rate is uniform across the fleet.** The function never reads the mech type record. At the
25 Hz tick `IntegrateRateOverTick(20)` yields 6 pool units/tick, i.e. 150/s.

**Computed once, at spawn.** `FUN_00417d08` has exactly one reference in the binary — the tail of
`Mech_ConfigureLoadout` (`004175dc`), itself only reached on spawn. Damage taken mid-mission never
changes the rate; the damage terms still matter because a machine can spawn already damaged.

### Reactor damage flags

`Mech_ComponentDamageWrite` (`00417de4`) latches them off dependent-subpiece **5**'s damage:

| Subpiece 5 damage | Flag | Reactor output | Also |
|---|---|---|---|
| ≤ 50% (`0x80`) | — | 20 | — |
| > 50%, < 75% (`0xc1`) | `mech+0xaa` | 11 (~59%) | movement penalty; alert sound for the player |
| ≥ 75% | `mech+0xab` | 3 (~20%) | movement penalty; alert sound |

Identified as the reactor by effect: the same pair cuts power and mobility together. Both latch and
are never cleared, and the check is gated on **both** being clear — so once `+0xaa` sets, `+0xab` is
only reachable by a single hit crossing both thresholds at once.

## Equipment pods — `mech+0x307`, filled by `FUN_0040fb2c`

Pods are ordinary weapon mounts on ordinary hardpoints. At the end of `Mech_ConfigureLoadout`,
`FUN_0040fb2c` walks the finished mount list and files five weapon ids into a five-pointer array.
The switch keys on the mount template's `+0x56`, which `Weapons_LoadResourceTables` (`0040fc8c`)
writes as the record's own table index — so it is the `SHELL0.VOL` `gam\WEAPONS.DAT` catalog id.

| Slot | Offset | Id | Name | Effect |
|---|---|---|---|---|
| 0 | `+0x307` | 18 | ECM | not traced |
| 1 | `+0x30b` | 29 | TARG | targeting; not traced |
| 2 | `+0x30f` | 30 | SHLD | shield capacity, `FUN_00417bec` |
| 3 | `+0x313` | 32 | ENRG | reactor rate, `FUN_00417d08` |
| 4 | `+0x317` | 31 | TURB | speed, see [mech-locomotion.md](mech-locomotion.md) |

Slot order is not id order (`0x1f`→[4], `0x20`→[3]). The switch assigns rather than accumulates, so a
second copy of a pod fills the same slot and contributes nothing.

**Both pod bonuses share one curve**, gated off entirely at 225/256 damage:
`scale = 1024 - 204 * (damage / 51)`, Q10 — five steps from 1024 (pristine) down to 208, then
nothing. A pristine pod is worth `Q10(1024, base) = base`: it **doubles** the stat.

> The manual says the Energy Pod doubles the pool's *capacity*. It does not — 10000 is a literal in
> both the constructor and the clamp. It doubles the recharge *rate*, which is the manual's own
> afterthought ("a modest increase in the Pool recharge rate").

## Weapon energy arbitration — `FUN_004107e4`

Vtable slot 0 of the mount-manager object at `mech+0x202`, for both the local (`00499238`) and
remote (`00499338`) manager classes. Not ported — weapon systems are a later milestone.

- Mounts are served one at a time, highest priority first. Priority is the mount's `+0x7b`, except a
  mount already mid-charge (`+0x43`) reports 10000 and jumps the queue.
- The player's selected mount (`manager+0x1d`) is served before the ranking is consulted; the AI
  passes `-1` and goes straight to the ranking.
- Per mount (`FUN_0040f00c`): takes `min(chargeRate +0x7f, budget, capacitor deficit)` into `+0x7d`
  and passes the remainder on.
- Once any mount reports itself mid-charge, every mount after it targets zero instead and **bleeds
  its capacitor back into the pool** at 5/tick. One energy weapon charges at a time.
- Ammunition mounts consume nothing — their slot-`0x34` override returns the budget untouched.
- PLAS (id 25) is half-efficiency: its deficit counts double and only half of what it draws is
  stored.
- An infinite-energy debug path exists (`DAT_004a9ed6 == 0 && DAT_004a9edc == 1`, player only):
  consumption is refunded at the end of the pass.

## Cockpit readouts

- **Energy meter.** `Player_PerFrameCockpitUpdate` (`0041b130`) computes `(pool << 10) / 10000` and
  pushes it to the LED bar at UI slot `+0x1e5`, whose range is `0x400`.
- **Shield rings** show charge; **the shield numbers show balance.** See
  [damage-system.md](damage-system.md#the-shield-system).

## Verified against retail

Refilling a damaged shield array takes ~30 s, matching 3500 ÷ 5 per tick = 700 ticks at the hard
25 Hz cap (28 s). The recharge cap is per *tick*, not per unit time, but the tick is capped by a
`GetTickCount` spin (`FUN_004677bc`), so this does not vary with hardware.

> A cosmetic mismatch is open: in retail the shield rings fade black→green over ~10 s at mission
> start. Both facings are full from `Shield_Init` onward and nothing writes zero to the struct
> (every reference enumerated, including a raw scan for the `0x222` displacement outside decompiled
> code), so this is a HUD animation, not charge. Not chased; tracked in KNOWN_ISSUES.md.
