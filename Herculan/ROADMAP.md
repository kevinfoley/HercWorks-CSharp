# Roadmap — work not yet done

Everything the HERCULAN Engine does not implement yet, in one place. This is the counterpart of
[`KNOWN_ISSUES.md`](KNOWN_ISSUES.md), which records things that *are* implemented but behave
differently from retail. If a feature is missing entirely it belongs here; if it is present and
wrong it belongs there.

Each entry names the doc that owns the subject. **That doc is authoritative** for how much is
reverse-engineered and how much is still unknown — this file only tracks that the porting work is
outstanding.

## Reverse-engineered, not ported

The mechanism is understood; what is left is engine work.

- **CD music and squad speech.** The effects half of the catalog is ported, and so is the cockpit
  computer's channel entire — the message port's queue, timings, repeat suppression and preemption,
  the scrolling ticker, and `SYSTEM.STR`'s lines read from their `CVM` clips. Red Book music through
  MCI is not, so a mission runs without its track. Neither is the port's second instance, the pilot
  and squad channel, where messages wrap several lines instead of scrolling one line: squadmate and
  commander lines with their `.SNC` portrait lip-sync scripts are unported, so the comm box never 
  speaks or animates and nothing posts to that channel.
  → [`docs/formats/audio.md`](docs/formats/audio.md)
- **Combat gaps.** Hit detection, weapon-mount destruction and the explosive blast sweep are
  complete for all three shootable classes. One of the sweep's three call sites is still unreachable
  because the function that owns it is unported: the AI ramming attack (`FUN_0041e488`, part of the
  `ramming` entry below).
  → [`docs/simulation/damage-system.md`](docs/simulation/damage-system.md)
- **The `ramming` behaviour state.** Every other behaviour state's think is ported; state 17's pair
  (`Mech_BehaviourRamThink` `0041e570`, `Mech_BehaviourRamTick` `0041e488`) is not, so a group given
  order verb 1 takes the state and stands still in it.
  → [`docs/simulation/ai-combat-states.md`](docs/simulation/ai-combat-states.md),
  [`docs/simulation/damage-system.md`](docs/simulation/damage-system.md)
- **A machine's LOD roots are not selected.** Root 0 is hard-coded where the original picks one per
  frame from projected size and a detail bias.
  → [`docs/formats/mech-shape-drawing.md`](docs/formats/mech-shape-drawing.md)
- **Weapon input divergences.** A right press dragged off its widget before release fires nothing
  here, where the original re-hits on release; clicking a pod's row does nothing, where the original
  toggles the pod.
  → [`docs/simulation/weapon-mounts.md`](docs/simulation/weapon-mounts.md)
- **Terrain raycast, swept-volume mode.** Only thin-ray mode is ported; the swept-volume mode
  (movement collision) is not, because nothing in the engine needs it yet.
  → [`docs/formats/terrain-heightmap.md`](docs/formats/terrain-heightmap.md)
- **The HUD's "ATT" legend.** Automatic Turret Tracking itself works, but the manual's upper-left
  "ATT" readout has not been located in the cockpit widget set, so nothing on screen says the tracker
  is on but the TRACK button's own lamp and the computer's spoken confirmation.
  → [`docs/simulation/torso-aim.md`](docs/simulation/torso-aim.md)
- **Mission difficulty.** Nothing sets it, so the two systems that index it — the AI's aim scatter
  and the explosive damage scale — run on entry 0.
  → [`docs/simulation/ai-weapons.md`](docs/simulation/ai-weapons.md),
  [`docs/simulation/projectiles.md`](docs/simulation/projectiles.md)
- **An unpiloted flyer's own tick.** `FlyerObject.Tick` is empty, so a mission's flyers hold station
  where they spawn. They are a live group like any other, so this also costs a mission any trigger a
  flying group would have crossed — which can make an action fire later here than in retail.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md)
- **Flyer control bindings.** The flight model is ported and the axis roles are known, but key
bindings are hardcoded placeholders.
  → [`docs/simulation/razor-flight.md`](docs/simulation/razor-flight.md)

## Reverse-engineering still open

The engine cannot be faithful here until the original is understood.

- **Squad orders.** The standing orders the player gives their own squad (`mech+0x23e`), which
  `Mech_AiSelectBehaviour`'s second path reads. Undecoded, so that path installs nothing.
  → [`docs/simulation/ai-dispatch.md`](docs/simulation/ai-dispatch.md)
- **SimRandom's 56-entry seed table isn't extracted** from DBSIM's data section. The algorithm is a
  literal port; the seeding is not, and a roll's result also depends on generator-advance count —
  treat as statistically faithful, not replay faithful.
  → [`docs/simulation/dbsim-physics-notes.md`](docs/simulation/dbsim-physics-notes.md)
- **Flyer texture banks.** Which `.DBA` DBSIM binds for a flyer is untraced, so flyers draw
  flat-shaded.
  → [`docs/formats/dts-texture-binding.md`](docs/formats/dts-texture-binding.md)
- **The mission message an action queues.** `Action_Fire` queues the line named at action `+0x34`;
  the id is decoded and carried but `data\mission.str` is not loaded, so nothing is posted.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md)
- **The mission counters' reader.** `DAT_004a9ef4` is written by a firing action and dumped to
  `mission_var` at mission end; the campaign layer that reads it back is not ported.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md)
- **The drop pod's ground mark.** The leftover effect a landed pod spawns comes from the theater's
  `flat`/`flat2` shape pool, which is not ported.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md)
- **Flyer formation spread.** `FUN_00421ee8` untraced; no multi-flyer groups observed in retail
  missions so far.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md)
- **External view (`[V]` chase camera) is entirely engine-invented.** DBSIM's own external view
  placement, transitions, terrain handling and overlay chrome are unrecovered.
  `Render/ExternalCamera.cs` is the single place a real rule would replace the guess.
- **Pause (`[P]`) is a placeholder** that just stops the fixed-timestep tick loop. Retail DBSIM's own
  pause has not been traced.

## Other unported features
- Currently missing is a quirk from retail where the player's shield meter fills in over ~10 seconds at the start of a mission. Claude says there's no explanation for this in the shield code, where the shields start out at full charge, and would take ~30 seconds to fully charge from empty. The fade-in-over-10-seconds may be a HUD animation that hasn't been discovered during RE yet.
- Similarly to the previous, currently missing is an animation where weapon buttons wink on one-at-a-time when the simulation first starts.
- The Preferences screen is not implemented.
