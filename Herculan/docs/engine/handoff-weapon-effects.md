# Handoff — outstanding combat and effects work

> **This file is a scratchpad, not a status record.** It is ephemeral: it may hold the newest lead
> before that lead reaches a topic doc, but it is never the authority on what is or is not done.
> For that, read the topic doc that owns the subsystem, or `KNOWN_ISSUES.md` for behavioural
> divergences. Anything here that becomes settled should move out into a topic doc and be deleted
> from this file.

Addresses below are starting points to decompile, not settled findings — check anything
load-bearing. What *is* settled is in
[`../simulation/projectiles.md`](../simulation/projectiles.md),
[`../simulation/rockets.md`](../simulation/rockets.md),
[`../simulation/weapon-firing.md`](../simulation/weapon-firing.md),
[`../simulation/beam-visuals.md`](../simulation/beam-visuals.md),
[`../simulation/impact-effects.md`](../simulation/impact-effects.md),
[`../simulation/hit-detection.md`](../simulation/hit-detection.md),
[`../simulation/damage-system.md`](../simulation/damage-system.md),
[`../formats/hud-target-indicator.md`](../formats/hud-target-indicator.md),
[`../formats/mfd-scanner.md`](../formats/mfd-scanner.md),
[`../formats/dts-billboards.md`](../formats/dts-billboards.md),
[`../formats/dts-texture-binding.md`](../formats/dts-texture-binding.md) and
[`../formats/distance-fog-and-sky.md`](../formats/distance-fog-and-sky.md).

## Not built

_ These are not organized in any particular order; this may not be the best order to complete these tasks in._

- AI machines never select anything, so they never fire and never switch their radar on — which is
  also why a hostile is only targetable at long range once the player goes ACTIVE. The setter is
  `FUN_0041c0f4`; the state functions that call it are `FUN_0041c418`, `FUN_0041cf18`, `FUN_0041d60c`,
  `FUN_0041d7d0`, `FUN_0041d9cc`, `FUN_0041daac` and `FUN_0041e224`, which also drive `mech+0x96`
  from a per-state flag table at `mech+0x92`.

- **Weapon-mount destruction.** Components 19–28 are the machine's mounts, indexed
  `component - 19` into the mount manager. A health-band change on one rolls (`FUN_00410670` →
  `FUN_0040f57c`) to knock it out and then finishes the component with a flat 10000. The roll and
  the component side are decoded; the mount manager's own destroy path is not.
- **The explosive blast sweep** (`FUN_00426a20`, mech vtable `+0x70`). Its absence is why
  `SplashFactor`'s share of a direct-fire hit is dropped rather than diverted, why plasma does not
  splash, and why a destroyed weapon mount cannot explode. Fully decoded in
  [`../simulation/damage-system.md`](../simulation/damage-system.md).
- **Effect light sources.** An `EXPLOS.DAT` row's `LightMode` and its twelve-frame intensity ramp
  drive a light object the engine does not have. The engine's light list holds only the mission sun
  (`Render.MissionSun`); a second entry would have to sum into the same shade byte, so this wants a
  pass over the shading path rather than a bolt-on. Effect light sources are not obviously visible in retail,
  so it's possible that this logic was misunderstood, or that the light sources have a brightness of
  0, or are only used in certain zones.
- **Structures clip.** Projectiles and impact effects visibly sink into buildings, which retail does
  not do. **Hit geometry is ruled out** — measured, see
  [`../simulation/hit-detection.md`](../simulation/hit-detection.md), "Measured: hit geometry versus
  the drawn mesh". Remaining suspect is render layering.
- **The 3D view is not clipped to the `.VUE` viewport rect.** Equivalent while the canopy is opaque;
  not for RAZOR's non-stub heads-down view.
- In retail, when a target is selected, chain-firing skips weapons for which the selected target is
  out of range.