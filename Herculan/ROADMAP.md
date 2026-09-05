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
- **Group orders.** `Group_OrderTick` (`00423a74`) advances a group through its row-15 orders. The
  layer is decoded; nothing in the engine runs it.
  → [`docs/formats/script-dat.md`](docs/formats/script-dat.md)
- **Combat gaps.** Hit detection, weapon-mount destruction and the explosive blast sweep are
  complete for all three shootable classes. Two of the sweep's three call sites are still unreachable
  because the functions that own them are unported: the drop pod's landing detonation (`Meteor_Tick`,
  part of the mission-deployment entry above) and the AI ramming attack (`FUN_0041e488`, part of the
  behaviour-tree entry below).
  → [`docs/simulation/damage-system.md`](docs/simulation/damage-system.md)
- **Nothing draws a destroyed part's blank cell.** Every body part of a machine, and every
  destructible section of a structure, is a flipbook whose last cell draws nothing, and the original
  steps a lost part to it. The engine records which sequences should have been blanked and draws none
  of them: `DtsMeshBuilder` takes one cell index for a whole root, so this needs a per-part one.
  → [`docs/formats/mech-shape-drawing.md`](docs/formats/mech-shape-drawing.md)
- **A machine's LOD roots are not selected.** Root 0 is hard-coded where the original picks one per
  frame from projected size and a detail bias.
  → [`docs/formats/mech-shape-drawing.md`](docs/formats/mech-shape-drawing.md)
- **Weapon input divergences.** A right press dragged off its widget before release fires nothing
  here, where the original re-hits on release; TRACK latches but nothing reads it; clicking a pod's
  row does nothing, where the original toggles the pod.
  → [`docs/simulation/weapon-mounts.md`](docs/simulation/weapon-mounts.md)
- **Terrain raycast, swept-volume mode.** Only thin-ray mode is ported; the swept-volume mode
  (movement collision) is not, because nothing in the engine needs it yet.
  → [`docs/formats/terrain-heightmap.md`](docs/formats/terrain-heightmap.md)
- **Automatic turret tracking (`[T]`) and AI turret aiming.**
  → [`docs/simulation/torso-aim.md`](docs/simulation/torso-aim.md)
- **Flyer control bindings.** The flight model is ported and the axis roles are known, but key
bindings are hardcoded placeholders.
  → [`docs/simulation/razor-flight.md`](docs/simulation/razor-flight.md)

## Reverse-engineering still open

The engine cannot be faithful here until the original is understood.

- **AI / behaviour trees barely understood.** Blocks enemy mech behaviour and patrol movement, and
  is why AI machines never select a target and so never fire. Each machine gets a behaviour class at
  construction and a state within it; the state blocks (`0049991c` onward, stride `0x24`) are three
  pointer-to-member triples each — a think slot, a per-tick move slot and an empty one — and their
  class descriptors are filled at startup, so the image alone does not say which state is which. One
  state is decoded end to end, the ramming attack at `00499b5c`; see
  [`damage-system.md`](docs/simulation/damage-system.md#the-sweep--damage_explosiveblastsweep-00426a20).
  → [`docs/simulation/target-selection.md`](docs/simulation/target-selection.md)
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
