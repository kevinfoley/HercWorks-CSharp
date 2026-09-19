# DBSIM.EXE equipment pods

Ported in `Herculan.Engine.Sim.MechPods`.

The five non-firing pods a HERC can carry — ECM, TARG, SHLD, TURB, ENRG. This doc owns the pod as an
**object**: how the five classes are built, what each one overrides, how their cockpit rows behave,
when they tick, and the damage curve they share. What each pod's contribution then *does* belongs to
the system it feeds, and stays there:

| Pod | Effect owned by |
|---|---|
| ECM | the spoof roll in [`missile-lock.md`](missile-lock.md#ecm); the jammer flag is derived here |
| TARG | [`target-selection.md`](target-selection.md#component-targeting--the-targeting-pod) |
| SHLD | [`damage-system.md`](damage-system.md#the-shield-system) |
| TURB | [`mech-locomotion.md`](mech-locomotion.md) |
| ENRG | [`reactor-energy-pool.md`](reactor-energy-pool.md#reactor-output-rate--mech_computereactorrate-00417d08) |

Pods are ordinary weapon mounts on ordinary hardpoints. At the end of `Mech_ConfigureLoadout`,
`MechLoadout_FileEquipmentPods` (`0040fb2c`) walks the finished mount list and files five weapon
ids into a five-pointer array. The switch keys on the mount template's `+0x56`, which
`Weapons_LoadResourceTables` (`0040fc8c`) writes as the record's own table index — so it is the
`SHELL0.VOL` `gam\WEAPONS.DAT` catalog id.

| Slot | Offset | Id | Name | Effect |
|---|---|---|---|---|
| 0 | `+0x307` | 18 | ECM | on/off from its own gauge, `EcmPod_Tick` (`0040f184`), which posts the jamming messages `0x2a`/`0x2b` |
| 1 | `+0x30b` | 29 | TARG | component targeting, [`target-selection.md`](target-selection.md#component-targeting--the-targeting-pod) |
| 2 | `+0x30f` | 30 | SHLD | shield capacity, `Mech_ComputeShieldCapacity` (`00417bec`) |
| 3 | `+0x313` | 32 | ENRG | reactor rate, `Mech_ComputeReactorRate` (`00417d08`) |
| 4 | `+0x317` | 31 | TURB | speed, `TurboPod_Tick` (`0040f1f0`) and [mech-locomotion.md](mech-locomotion.md) |

Slot order is not id order (`0x1f`→[4], `0x20`→[3]). The switch assigns rather than accumulates, so a
second copy of a pod fills the same slot and contributes nothing — the last mount in hardpoint order
wins. Ported in `Sim.MechPods`.

Each pod is its own class: `Pod_CtorBase` (`0040e234`) installs the shared table `00498cdc` after
`WeaponMount_CtorBase`, and one of `EcmPod_Ctor`, `TargetingPod_Ctor`, `ShieldPod_Ctor`,
`EnergyPod_Ctor` and `TurboPod_Ctor` (`0040e274`, `e308`, `e344`, `e380`, `e2bc`) then replaces it
with its own and writes its catalog id to `+0x77`. **A pod's layout diverges from a weapon mount's at
that offset**: `+0x77` is the catalog id and `+0x79` the cockpit gauge handle, where an ammunition
mount keeps the gauge handle at `+0x77` itself; and what `+0x7d` and `+0x7f` hold differs again per
pod, so an offset read off one of these classes says nothing about the others.

## What each class actually overrides

Against the shared table `00498cdc`, the five reach for only four slots between them — `+0x18` and
`+0x6c` aside, which every class differs in because they are its destructor and its class-descriptor
pointer rather than behaviour. A dash is the base row inherited unchanged:

| Pod | `+0x34` pool turn | `+0x50` tick | `+0x64` gauge | `+0x68` condition changed |
|---|---|---|---|---|
| base | `WeaponMount_RefireTick` (`0040ef94`) | `Pod_TickBase` (`0040f144`) | stub `00496684` | `WeaponMount_ConditionChangedBase` (`0040ee0c`) |
| ECM | — | `EcmPod_Tick` (`0040f184`) | `EcmPod_CreateGauge` | — |
| TARG | — | — | `TargetingPod_CreateGauge` | `TargetingPod_ConditionChanged` (`0040ef6c`) |
| SHLD | — | — | `ShieldPod_CreateGauge` | — |
| ENRG | — | — | `EnergyPod_CreateGauge` | — |
| TURB | `TurboPod_ChargeTick` (`0040f0d0`) | `TurboPod_Tick` (`0040f1f0`) | `TurboPod_CreateGauge` | — |

Inheriting `WeaponMount_RefireTick` in the pool slot is what makes a pod free to run: it counts the
mount's refire timer down and returns the budget untouched. **The Turbo Pod is the one pod that
draws on the pool** — `TurboPod_ChargeTick` spends 35 of its own charge per tick while engaged and
buys it back at up to 20/tick of the weapons' leftover budget, toward the 2000 its constructor
starts it at.

**The Shield and Energy pods are inert objects.** Neither overrides anything but its own gauge
constructor: no tick of its own, no notification, no state. That is the pod *object*, not the number
it feeds: each is reached by name from the one routine that wants it, and `Mech_ComputeShieldCapacity`
re-runs on every `Mech_ComponentDamageWrite`, so a Shield Pod's contribution really does fall away as
the pod is shot. `Mech_ComputeReactorRate` has no second caller, so an Energy Pod's does not.
`es2_fieldscan.py` over `0x307`–`0x317` finds nothing else —
`Mech_ComputeShieldCapacity` is the only access to `+0x30f` it reports and `Mech_ComputeReactorRate`
the only access to `+0x313`. That scan resolves the rebase idiom (it recovers `AlertPanel_Enter`'s
writes through an `EBX+0x300` alias, on an unrelated object), so a slot reached as
`equipmentPods[n]` off the array base would show; and no caller indexes the array in any case, since
the two `+0x307` readers at `0041aa10` and `0041aa44` dereference slot 0 directly. It remains a null
result.

**The `+0x50` tick runs on the player's machine only.** Its one call site is
`WeaponMounts_PerFrameUpdate` (`00410b40`), whose one caller is `Player_PerFrameCockpitUpdate` — so
an AI machine's pods are never ticked at all. Two consequences follow, both checkable in play:

- **An AI machine never engages its Turbo Pod.** `TurboPod_Ctor` clears the engaged flag at `+0x81`
  and `TurboPod_Tick` is its only writer, while `Mech_LocomotionTick` gates the speed bonus on that
  flag. A squadmate fitted with one loses the hardpoint and gets nothing.
- **An AI machine's ECM pod never sets `+0x7f`**, which `EcmPod_Ctor` clears and `EcmPod_Tick` alone
  writes. That is the field `Mech_PerTickSystemsUpdate` uses to raise the per-tick action latch on
  the jamming machine's own target, so that half of ECM is the player's alone. The other half — the
  jammer flag at `+0xa1` — does work for an AI machine, because `FUN_0041aa10` takes its answer from
  the radar mode at `+0x96` rather than from the pod: an AI jams exactly while its radar is ACTIVE,
  where the player's follows the pod row's button and ignores their radar entirely. In other words,
  the AI activates the ECM through a different code path than the player.

It also explains why `EcmPod_Ctor`'s null gauge handle at `+0x79` is not a crash: `EcmPod_Tick`
dereferences it unconditionally, and only ever sees the real handle `EcmPod_CreateGauge` gave the
player's own pod.

Two consequences for whose ECM is actually on, both following from it being the radar mode:

- **A squadmate's jammer is off by default, and the player owns the switch.** `Mech_AiCombatReassess`
  puts a machine in the player's own group back to PASSIVE — the squad does not paint the sky — so
  in practice the AI branch means *Cybrids*. `SCAN FOR HOSTILES` sets the squad radar order at
  `mech+0xb2` and lights a squadmate up, ECM with it; `EMCON` clears both. There is no way to order
  a squadmate's pod on its own. The two verb pairs are in [`ai-squadmates.md`](ai-squadmates.md).
- **An anti-radiation hit suppresses the jamming it homed on.** A round of class 2 landing on any
  machine but the player's clears its scanner and holds it dark for 6000 — long enough for the
  seeker to lose it, and long enough that the machine stops spoofing the player's missile locks for
  the same window. The weapon that finds a target by its emissions silences that target's ECM when
  it lands.

`Pod_TickBase` is a display refresh and nothing else: it takes the gauge's 8-byte state block
through `PodGauge_GetStateBlock`, replaces byte 1 with the mount's destroyed flag at `pod+0x49`,
leaves byte 0 — the button — and the other six bytes as they were, and pushes the block back through
`PodGauge_SetStateBlock`. It touches no pod field. `EcmPod_Tick` and `TurboPod_Tick` end in the same
two calls; what makes them ticks is the button handling in front.

## Only two pods have a button

`CockpitView_CreatePodGauge` (`004321d4`) switches on the catalog id at `pod+0x77` and builds one of
three gauge classes: `TogglePodGauge_Ctor` (`00441998`, vtable `0049c8a4`) for ECM,
`TurboPodGauge_Ctor` (`00441a34`, vtable `0049c878`, which derives from it and adds an LED bar over
the pod's charge — [`../formats/cockpit-hud.md`](../formats/cockpit-hud.md))
for TURB, and the plain `PodGauge_Ctor` (`00441524`, vtable `0049c8d0`) for TARG, SHLD and ENRG.
**The split is the tick table again** — the two classes with a tick of their own are the two with a
gauge class of their own.

Every pod row is clickable and sounds on a click: `PodGauge_Ctor` registers the row's
`WeaponSelectGadget` child with `Widget_RegisterClickable`, and that child sounds `Widget_ClickSound`
and forwards to its owner's slot 0 ([`../formats/cockpit-input.md`](../formats/cockpit-input.md)).
What the slot does is where the classes part:

- `TogglePodGauge_OnClick` (`004419fc`) XORs the button byte at `gauge+0xc2` — structurally the same
  child-table search as `ShieldsGauge_OnClick`. `EcmPod_Tick` and `TurboPod_Tick` read that byte back
  out of the state block on the next tick. This is the whole of a pod's on/off toggle.
- `PodGauge_OnClick` (`00441988`) sets the repaint byte and returns.

So a Shield, Targeting or Energy pod row clicks and sounds but has no on/off state to change, and
`Pod_TickBase` would ignore it if it did. Clicking is not how a Targeting Pod is used either — that
is `[Tab]`, through `CockpitWidgets_HandleCommand`
([`target-selection.md`](target-selection.md#component-targeting--the-targeting-pod)).

**The Shield and Energy pods read their damage live** — `Component_ReadDamagePercent` against the
mount's own component, `.GL +0x17` + 19 — where the Targeting Pod caches its reading in `+0x7f` from
its vtable `+0x68`. Same quantity, two mechanisms.

## The damage curve both bonuses share

Gated off entirely at 225/256 damage: `scale = 1024 - 204 * (damage / 51)`, Q10 — five steps from
1024 (pristine) down to 208, then nothing. A pristine pod is worth `Q10(1024, base) = base`: it
**doubles** the stat it feeds.

Only the Shield and Energy pods use it, and each applies it to its own base — see
[`damage-system.md`](damage-system.md#the-shield-system) and
[`reactor-energy-pool.md`](reactor-energy-pool.md#reactor-output-rate--mech_computereactorrate-00417d08)
for what "doubles" amounts to in each case, and for the manual's claim about the Energy Pod that
the pool's own literals disprove.
