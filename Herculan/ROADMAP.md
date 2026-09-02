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
- **CD music, squad speech and the message port.** The effects half of the catalog is ported, and so
  is the cockpit computer's voice — `SYSTEM.STR`'s messages read from their `CVM` clips. Red Book
  music through MCI is not, so a mission runs without its track. Neither are squadmate and commander
  lines with their `.SNC` portrait lip-sync scripts, so the comm box never speaks or animates. And
  only the audio half of the message port exists: a posted message is spoken but never written on
  screen, held for its display time or preempted.
  → [`docs/formats/audio.md`](docs/formats/audio.md)
- **Group orders.** `Group_OrderTick` (`00423a74`) advances a group through its row-15 orders. The
  layer is decoded; nothing in the engine runs it.
  → [`docs/formats/script-dat.md`](docs/formats/script-dat.md)
- **Combat gaps.** A struck weapon mount is never destroyed; there is no explosive blast sweep, so a
  shot's `SplashFactor` share is dropped. Hit detection itself is complete for all three classes.
- **The ELF spin-up.** `ElfMount_TriggerHeld` swallows the first trigger press and returns no shot
  until the muzzle-flash flipbook has played once, one cell per tick (`ElfMount_SpinUpAndChargeTick`);
  ELF2 skips it. The engine fires on the press instead. Blocked on the muzzle flash itself, which is
  what defines the delay's length — the engine draws none.
  → [`docs/simulation/weapon-mounts.md`](docs/simulation/weapon-mounts.md)
  → [`docs/simulation/weapon-firing.md`](docs/simulation/weapon-firing.md),
  [`docs/engine/handoff-weapon-effects.md`](docs/engine/handoff-weapon-effects.md)
- **Weapon input divergences.** A right press dragged off its widget before release fires nothing
  here, where the original re-hits on release; TRACK latches but nothing reads it; clicking a pod's
  row does nothing, where the original toggles the pod.
  → [`docs/simulation/weapon-mounts.md`](docs/simulation/weapon-mounts.md)
- **GAU widgets are not interactive** outside the weapon panel and console buttons — no input wiring.
  → [`docs/formats/cockpit-input.md`](docs/formats/cockpit-input.md)
- **Terrain raycast, swept-volume mode.** Only thin-ray mode is ported; the swept-volume mode
  (movement collision) is not, because nothing in the engine needs it yet.
  → [`docs/formats/terrain-heightmap.md`](docs/formats/terrain-heightmap.md)
- **Automatic turret tracking (`[T]`) and AI turret aiming.**
  → [`docs/simulation/torso-aim.md`](docs/simulation/torso-aim.md)

## Reverse-engineering still open

The engine cannot be faithful here until the original is understood.

- **AI / behaviour trees barely understood.** Blocks enemy mech behaviour and patrol movement, and
  is why AI machines never select a target and so never fire.
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
