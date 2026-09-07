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

- **Mission deployment.** A group waiting on a mission action is correctly held out of the world,
  but no trigger ever fires, so drop pods (the falling `METEOR` that delivers Cybrid reinforcements)
  and walk-on arrivals never happen and those units never appear.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md)
- **CD music and squad speech.** The effects half of the catalog is ported, and so is the cockpit
  computer's channel entire — the message port's queue, timings, repeat suppression and preemption,
  the scrolling ticker, and `SYSTEM.STR`'s lines read from their `CVM` clips. Red Book music through
  MCI is not, so a mission runs without its track. Neither is the port's second instance, the pilot
  and squad channel: squadmate and commander lines with their `.SNC` portrait lip-sync scripts are
  unported, so the comm box never speaks or animates and nothing posts to that channel.
  → [`docs/formats/audio.md`](docs/formats/audio.md)
- **Combat gaps.** Hit detection, weapon-mount destruction and the explosive blast sweep are
  complete for all three shootable classes. Two of the sweep's three call sites are still unreachable
  because the functions that own them are unported: the drop pod's landing detonation (`Meteor_Tick`,
  part of the mission-deployment entry above) and the AI ramming attack (`FUN_0041e488`, part of the
  AI entry below).
  → [`docs/simulation/damage-system.md`](docs/simulation/damage-system.md)
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
- **The object's own mission action.** `Ai_ChooseWeapon` fires `mech+0x1b6` when a machine runs out
  of weapons, and so does the damage path; the engine has no per-object action to fire.
  → [`docs/simulation/ai-weapons.md`](docs/simulation/ai-weapons.md)
- **Flyer control bindings.** The flight model is ported and the axis roles are known, but key
bindings are hardcoded placeholders.
  → [`docs/simulation/razor-flight.md`](docs/simulation/razor-flight.md)

## Reverse-engineering still open

The engine cannot be faithful here until the original is understood.

- **What the combat behaviour states do.** The dispatch spine, targeting, the mission-group order
  layer and the five navigation states are decoded and ported: a machine takes a state from its
  group's current order, walks its route or holds formation on its leader, steers round what is in
  its way, acquires and abandons targets, and answers incoming fire. What is still undecoded is the
  other half of the roster — `attacking`, `flanking`, `facing off`, `attacking base`,
  `attacking flyer`, `skirting`, `driving off en`, `fleeing` — so a machine walks to its objective
  and then stands still the moment it finds something to fight. Its weapons are decoded and ported
  and reach it through `travelling` and `following` alone; the combat states have no think to call
  them from.
  → [`docs/simulation/ai-dispatch.md`](docs/simulation/ai-dispatch.md),
  [`docs/simulation/ai-targeting.md`](docs/simulation/ai-targeting.md),
  [`docs/simulation/ai-goals.md`](docs/simulation/ai-goals.md),
  [`docs/simulation/ai-navigation.md`](docs/simulation/ai-navigation.md),
  [`docs/simulation/ai-weapons.md`](docs/simulation/ai-weapons.md)
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
- **Flyer formation spread.** `FUN_00421ee8` untraced; no multi-flyer groups observed in retail
  missions so far.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md)
- **External view (`[V]` chase camera) is entirely engine-invented.** DBSIM's own external view
  placement, transitions, terrain handling and overlay chrome are unrecovered.
  `Render/ExternalCamera.cs` is the single place a real rule would replace the guess.
- **Pause (`[P]`) is a placeholder** that just stops the fixed-timestep tick loop. Retail DBSIM's own
  pause has not been traced.
