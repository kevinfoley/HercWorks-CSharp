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
[`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md),
[`../simulation/beam-visuals.md`](../simulation/beam-visuals.md),
[`../simulation/impact-effects.md`](../simulation/impact-effects.md),
[`../simulation/hit-detection.md`](../simulation/hit-detection.md),
[`../simulation/damage-system.md`](../simulation/damage-system.md),
[`../formats/hud-target-indicator.md`](../formats/hud-target-indicator.md),
[`../formats/mfd-scanner.md`](../formats/mfd-scanner.md),
[`../formats/dts-billboards.md`](../formats/dts-billboards.md),
[`../formats/dts-texture-binding.md`](../formats/dts-texture-binding.md),
[`../formats/mech-shape-drawing.md`](../formats/mech-shape-drawing.md) and
[`../formats/distance-fog-and-sky.md`](../formats/distance-fog-and-sky.md).

## Not built

_ These are not organized in any particular order; this may not be the best order to complete these tasks in._

- AI machines never select anything, so they never fire and never switch their radar on — which is
  also why a hostile is only targetable at long range once the player goes ACTIVE. The setter is
  `FUN_0041c0f4`; the state functions that call it are `FUN_0041c418`, `FUN_0041cf18`, `FUN_0041d60c`,
  `FUN_0041d7d0`, `FUN_0041d9cc`, `FUN_0041daac` and `FUN_0041e224`, which also drive `mech+0x96`
  from a per-state flag table at `mech+0x92`.

- **Structures clip.** Projectiles and impact effects visibly sink into buildings, which retail does
  not do. **Hit geometry is ruled out** — measured, see
  [`../simulation/hit-detection.md`](../simulation/hit-detection.md), "Measured: hit geometry versus
  the drawn mesh". Remaining suspect is render layering.
- **A machine's LOD roots are not selected.** The original picks one of a `.DTS`'s roots per frame
  from projected size and a detail bias; the engine hard-codes root 0. See
  [`../formats/mech-shape-drawing.md`](../formats/mech-shape-drawing.md).
- **A destroyed component's geometry is not hidden.** Every body part is a three-cell flipbook and
  the original steps a lost component's to its blank cell; the engine builds cell 0 always. Same doc.
- **No debris objects.** Both a destroyed component and a destroyed weapon mount throw shapes in the
  original, and there is nothing here to spawn into.
- **The 3D view is not clipped to the `.VUE` viewport rect.** Equivalent while the canopy is opaque;
  not for RAZOR's non-stub heads-down view.
- In retail, when a target is selected, chain-firing skips weapons for which the selected target is
  out of range.