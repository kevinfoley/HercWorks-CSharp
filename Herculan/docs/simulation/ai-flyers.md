# Flyer AI — the `Flyer` class' own behaviour layer

The Cybrid aircraft. `Flyer` is a class of its own, not a HERC: it has its own behaviour state table,
its own thinks, its own control law, and its own move. What it shares with a machine is the
*dispatch* — see [`ai-dispatch.md`](ai-dispatch.md), whose model applies unchanged — and the flight
model, which is the player RAZOR's ([`razor-flight.md`](razor-flight.md)).

Retail ships one flyer chassis with data: `SKIMMER` ("Landskimmer"). `nam\FLYERS.NAM` also lists
`HOVTANK` and `DROPSHIP`, and neither has a `.DAT`, `.COL`, `.DMG` or `.FM`, so neither can fly or
be shot.

Ported as `Sim.FlyerObject` (`.Ai.cs`, `.Flight.cs`), `Sim.Ai.FlyerBehaviourState` and
`World.FlyerFormationTable`.

## The seven states

`FlyerBehaviourStateTable` (`00499cf8`) holds seven `0x3c`-byte descriptors, built at startup by
`Flyer_BuildStateTable` (`00414c65`) from `FlyerBehaviourSlotBlocks` (`00499e9c`) and
`FlyerBehaviourStateNames` (`00499f98`). The descriptor layout is the mech one minus its trailing
`+0x3c` string index — an aircraft never appears on the [F7] comm page — which is what makes the
stride `0x3c` where the mech table's is `0x3e`.

| # | Name | Think | Move | Dwell | Flags |
|---|---|---|---|---|---|
| 0 | `deciding` | — | — | 0 | `0x00` |
| 1 | `attacking` | `00422ca8` | `004218c4` | 50000 | `0x01` |
| 2 | `patrolling` | `00422b34` | `004218c4` | 5000 | `0x00` |
| 3 | `search and destroy` | `00422a80` | `004218c4` | 5000 | `0x00` |
| 4 | `sleeping` | — | — | 500 | `0x01` |
| 5 | `scouting` | `00422bdc` | `004218c4` | 5000 | `0x00` |
| 6 | `dead` | — | — | 500 | `0x01` |

Only flag bit 0 is ever set, and it means what it means for a machine: `Mech_AiTick` skips the dwell
countdown. So the three states with no think and no move are also the three that never time out —
`deciding` is the exception only because its dwell is already zero.

**Every state's reassess slot is the same function.** There is no combat form: an aircraft picks its
target inside its think and changes state from there.

### Dispatch

The flyer's vtable `+0x14`/`+0x18`/`+0x1c` are three descriptor dispatchers — `Flyer_DispatchMove`
(`004217fc`), `Flyer_DispatchThink` (`0042184c`), `Flyer_DispatchReassess` (`00421888`) — reading the
same `obj+0x4d` behaviour block a machine has. `Mech_AiTick` (`00411cec`) therefore drives an
aircraft exactly as it drives a HERC, through `Group_OrderTick`, and a flyer that is not a live
member of a mission group never thinks.

## Orders — `Flyer_AiSelectBehaviour` (`00422d00`)

The reassess maps the group's current order verb onto a state. The verb set is
[`ai-goals.md`](ai-goals.md)'s.

| Verb | State | Also |
|---|---|---|
| 0 `search/destroy` | `search and destroy` | |
| 3 `patrol` | `patrolling` | |
| 4 `sleep` | `sleeping` | latches `flyer+0xa5` |
| 5 `travel` | `scouting` | latches `flyer+0xa5` |

**Verbs 1, 2 and 6 install nothing, and neither does an empty slot.** The switch has no default and
the descriptor it is about to install lives in `ECX`, which nothing on that path writes — the same
shape of defect `Mech_AiSelectBehaviour` has for an empty order slot, though **not demonstrably the
same consequence**: there the register is provably zero and the dereference is a null one, and no
equivalent trace has been done here. See
[`ai-goals.md`](ai-goals.md#a-group-with-no-order-at-all); the engine leaves the aircraft in the
state it has.

`flyer+0xa5` is the third byte of the out-of-the-fight triple
([`sim-object-layout.md`](sim-object-layout.md#the-out-of-the-fight-triple--0x99-0xa4-0xa5)), and
nothing clears it. A flight ordered to travel or to sleep stops counting as something the other side
has to contest, which is the point.

## The thinks

Three of the four open with the same movement step, `Flyer_FollowStep` (`00422a50`): the group's
**first member** flies the route and everybody else keeps station on it. It also zeroes `flyer+0x96`,
so a flight not fighting holds its radar passive.

- **`scouting`** (`00422bdc`) is that step and nothing else.
- **`patrolling`** (`00422b34`) adds a target sweep, for the leader alone and only when the
  `flyer+0x5b` countdown expires (reloaded with 10000 ms): `Ai_SelectTarget` with mask `0x10`
  (reject own class), so a flight on patrol takes any machine or structure but not another aircraft.
- **`search and destroy`** (`00422a80`) is the same with mask `0x11`, and what it finds must also pass
  `Group_IsOrderTarget` — a flight under this order engages only what the mission named.
- **`attacking`** (`00422ca8`) reports **finished** as soon as the target is immobilised or
  destroyed, which zeroes the dwell and sends the aircraft back through the reassess. Otherwise the
  leader flies the attack run and the rest hold station.

`Flyer_EngageWithFlight` (`00422bf0`) is what a successful sweep calls: the leader enters
`attacking`, then hands every surviving member the same target and the same state. **A flight commits
as one.** The function is recursive and terminates because only the leader takes the loop.

## Station keeping

`Flyer_FormationStep` (`00422598`) puts a wingman at a point `0x2000` ahead of its leader along the
leader's heading, plus its own `FFORMS.DAT` slot. The anchor is therefore a spot the leader is flying
*into*, not the leader itself, which is what stops a wingman chasing a machine that keeps turning
under it.

A wingman that is **ahead** of its station — the station bearing outside ±90° — while still aligned
with the leader and inside 10000 units has its steering command *mirrored* (`-0x8000 - err`) rather
than reversed, so it eases back from in front instead of hauling round through a half turn.

### `dat\FFORMS.DAT`

Loaded by `Flyer_LoadResources` (`00422d8f`): a 2-byte record count, then that many `0x12`-byte
records. `FlyerFormation_GetSlotOffset` (`00423044`) resolves one as
`base + formationId * 0x12 + slot * 6 - 6`, so a record is **three** slots of three `int16` and the
slot index is **one-based** — the flight leader takes no offset at all.

**The offset carries a Z**, where a mech's `MFORMS.DAT` and a structure's `BFORMS.DAT` entries do
not. `Flyer_ApplyFormationOffset` (`00421e98`, vtable `+0x78`) hands all three components to the
shared `Formation_RotateAndAddOffset` (`00411d64`), which rotates by the **group leader's** heading
and adds the Z unrotated. Retail's five formations are trailing echelons: 2500, 5000 and 7500 units
aft, stepped 400 units up per slot.

## The attack run — `Flyer_AttackRun` (`004226a0`)

The only place a Cybrid flyer shoots, and a **two-phase circuit rather than a pursuit**. The phase is
`flyer+0x1fe`.

- **Phase 0, extending.** The steering command is reversed — the aircraft flies *away* — while it
  holds the target's altitude plus 10000. Past 90000 units of range it turns in.
- **Phase 1, running in.** Outside 65000 it is still closing at that same height; inside it, the nose
  goes onto the target. The run ends and the phase returns to 0 the moment the bearing leaves ±45°,
  the height over the target drops under 3000, or the range closes inside 10000.

So a flight keeps coming round rather than trying to stay on something that can turn inside it.

**The aim is led.** The target's position is offset along its own heading by
`(range>>3) * (targetSpeed*8) / shotSpeed`, with the range capped at 200000; `shotSpeed` is the
aircraft's own travel speed until the run is close enough to shoot, and `PROJ.DAT` row 2's speed from
then on.

Firing needs bearing and pitch both within ±1000 and the `flyer+0x21e` countdown expired. **The first
shot of each pass is a missile** — a flag at `flyer+0x5a`, cleared every time the phase returns to 0,
lets one `Rocket_Fire(0)` off the `(500, 200, -100)` muzzle inside 30000 units — and every shot after
it on that pass is a pair of `Bullet_Fire(2)` rounds from that point and its mirror. The countdown
reloads with 1500 ms.

A flyer's missiles always have a lock: the gate is the launcher's vtable `+0x6c`, and the `Flyer`
class' slot is a `return 1` stub. See [`rockets.md`](rockets.md).

## The control law

Every think ends in `Flyer_SteerAndFly` (`004222fc`), which turns a heading error into a bank and
runs the flight model. The bank command is a 16-bit accumulator:

```
bank = Q16(-turn, 2500) + Q16(bankTurnRate, 32000) + Q16(rollRate, -5000)
```

where `bankTurnRate` is `flyer+0x287` — the heading rate the current bank is already producing, which
is the term that stops a turn once it is actually coming round. Three arms follow:

- Past the chassis' `MaxBankAngle` (type record `+0x0e`, 14000 on `SKIMMER`) less a 1500 hysteresis
  band, and only in the direction that would take it further over, the aircraft is **held at the
  limit**.
- A steering command **under 2000** is answered with rudder (`Q16(-turn, 4000)`) and level wings
  rather than a bank at all — and the rudder is refused while the roll is still over 800, so the
  wings come level first.
- Anything larger is flown as a bank.

The pitch channel is `Flyer_PitchToAltitude` (`00422108`) into `Flyer_PitchCommand` (`00422098`): a
height error becomes a pitch demand against a **fixed 10000-unit horizontal run**, so the angle asked
for depends on the error alone; the demand is then scaled, resolved through the current bank, and
damped by the pitch rate. Cruise altitude is a flat 30000 world units.

`Flyer_ApplyFlightCommand` (`004221a8`) hands the result to `FlightModel_Step` (`00466a54`) — see
[`razor-flight.md`](razor-flight.md#control-law-flightmodel_step) for the model itself. Two things it
does on the way:

- **The two stick axes are squared**, sign kept (`(v*v)>>8`), before the ±0x100 clamp. Small commands
  are softened quadratically and only a large one reaches full deflection, which is what keeps an AI
  aircraft from sawing its controls.
- **The wing-damage argument is a stack array of four zeros.** A `Flyer` has one component, not a
  RAZOR's wings and nacelles, so it never takes the model's lost-wing or lost-nacelle arms.

### A flyer cannot change speed

The command array's throttle element is **never written** — `Flyer_SteerAndFly` zeroes it at every
site — so the flight model's rate branch never steps the setting and it stays at
`Flyer_Constructor`'s `0x200`, half. A `SKIMMER` therefore cruises at the airspeed half throttle asks
for, 875 of its 500–1000 range, biased only by its own pitch attitude.

`Flyer_FormationThrottle` (`00422260`) does work a throttle figure out of the station error and the
leader's speed, clamped to `[0x8c, 0x100]`, and writes it to `flyer+0x21c` — **a field with no reader
anywhere in the image**. Not ported.

## The move — `Flyer_MovementTick` (`004218c4`)

The `+0x24` slot of the four states that have one.

**The world velocity is added to the position raw**, three plain `ADD`s at `0042190a`, with no
`Math_IntegrateRateOverTick`. That is the one place a flyer's move differs in kind from
`Razor_MovementTick`'s, and it is what makes a flyer fast: its world velocity is a **per-tick step**
where the RAZOR's is a rate. Integrating it leaves a `SKIMMER` moving at a fraction of its speed.

Then four terrain probes, in the airframe's own frame:

| Probe | Point | Reaction |
| --- | --- | --- |
| Right wingtip | `(1000, -420, -300)` | Roll away, `Q10(4000, depth)` |
| Left wingtip | `(-1000, -420, -300)` | as above, opposite sign |
| Nose | `(0, 1000, 0)` | Pitch up, `Q10(2000, depth)` |
| Look-ahead | `(0, 15000, -1500)` | Pitch up, `Q10(20, depth)` |

Each kicks the rate and applies it to the attitude in the same tick. **None of them does any damage
and none sweeps against objects** — a Cybrid flyer that scrapes a hillside is rolled or pitched off
it and flies on, where a RAZOR loses the wing. The two pitch probes clear a negative pitch rate
first, so a climb the terrain orders is not fighting a dive the control law asked for.

The frame the probes are placed through is captured after the position moves and is **not** rebuilt
as they go, even though each contact marks the attitude stale: the original holds one matrix pointer
across all four.

Finally the origin is clamped to `terrainHeight + 500`, and the flyby loop (sound `0x31`) is started
or released at 30000 units from the view object.

## Death

`Flyer_ComponentDamageWrite` (`00421bb4`) loses the aircraft on component 0 — see
[`hit-detection.md`](hit-detection.md#flyer_directfirehittest--00421c8c) for the health record. Past
the flag it installs the `dead` descriptor, which has neither think nor move, and **writes -100000
into `flyer+0x2e`**, the object's world Z. The wreck drops straight out of the world; nothing moves
it afterwards because the state it is now in has no move slot to clamp it back to the ground.

For the length of that call the debris carrier global `004a96e4` points at the aircraft's own world
velocity, so wreckage the component cascade sheds keeps the speed it was doing — see
[`destruction-effects.md`](destruction-effects.md). The hit test's own throw happens after the clear
and gets nothing.

## Drawing

A flyer's shape is textured from **`ENEMY.DBA`**. Where a HERC picks its bank per chassis,
`maybe_FlyerType_LoadResources` (`00422ed0`) writes a *literal* slot address, `0x004a9e0e`, into the
shape's `+0x26` — that is `g_MechTextureGroupSlots` (`004a9df6`) plus `3 * 8`, texture group 3, the
Cybrid mechs' own bank. One bank for every flyer type, which fits a roster that is entirely Cybrid.
See [`../formats/dts-texture-binding.md`](../formats/dts-texture-binding.md#dba-binding).

An aircraft is drawn by **cell** rather than by node — it loses components like a machine but has
nothing that animates — and with its full attitude, bank and pitch included, where a structure has
only a heading.

## Rejected readings

| Reading | Why it is wrong |
| --- | --- |
| A flyer is driven by its own tick, separate from the mech AI | It is the same `Mech_AiTick`. Only the three dispatchers and the descriptor table differ |
| The flyer behaviour table is the mech's, indexed differently | Two tables, different addresses, different strides, different names. `00499cf8` and `004993a4` share only their shape |
| `Flyer_MovementTick` integrates its velocity like `Razor_MovementTick` | It adds it raw. The two functions look alike and this is the one difference that matters |
| `flyer+0x2e`'s `-100000` on death is a fall rate | It is the Z position. There is no fall: the aircraft is simply put below the world |
| `Flyer_FormationThrottle`'s output controls the flight | `flyer+0x21c` has no reader, and the model's throttle input is zero at every call site |
| A flyer's own radar is what paints it | It zeroes `flyer+0x96` every non-combat tick. A flight is painted by the other side's scanner or not at all |

## Open questions

- **What the three unhandled verbs actually do.** `Flyer_AiSelectBehaviour` leaves `ECX` unwritten
  for verbs 1, 2 and 6; what the dispatcher leaves in that register is not traced, and whether any
  retail `.MSN` gives a flyer group one of them has not been checked — the question is per group
  kind, and `ai-goals.md`'s verb-range census is not broken down that way.
- **`flyer+0x1f4`** gates firing (`|x| < 10`) and **`flyer+0x1f8`** scales the leader term in the
  bank command. Both are read exactly once each and written nowhere in the image, so both are
  identically zero: the gate always passes and the term contributes nothing. Neither is ported.
- **`Flyer_PitchToAltitude`'s other arm**, gated on `flyer+0xae` and reading `flyer+0x23c`. Both
  fields belong to the walking machine's obstacle avoidance and leg placement
  ([`ai-navigation.md`](ai-navigation.md), [`mech-locomotion.md`](mech-locomotion.md)) and no flyer
  path writes either, so the substitution cannot fire on an aircraft.
- **`FUN_00422a3c`**, which zeroes `flyer+0x21c` and returns 0. No caller traced; it is not in the
  flyer vtable and not in any descriptor triple.
