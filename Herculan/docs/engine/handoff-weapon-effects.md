# Handoff — after mech and flyer hit detection

Rewritten 2026-08-28. Addresses are starting points to decompile, not settled findings — check
anything load-bearing. What *is* settled is in
[`../simulation/projectiles.md`](../simulation/projectiles.md),
[`../simulation/rockets.md`](../simulation/rockets.md),
[`../simulation/weapon-firing.md`](../simulation/weapon-firing.md),
[`../simulation/beam-visuals.md`](../simulation/beam-visuals.md),
[`../simulation/impact-effects.md`](../simulation/impact-effects.md),
[`../simulation/hit-detection.md`](../simulation/hit-detection.md),
[`../simulation/damage-system.md`](../simulation/damage-system.md),
[`../formats/dts-billboards.md`](../formats/dts-billboards.md),
[`../formats/dts-texture-binding.md`](../formats/dts-texture-binding.md) and
[`../formats/distance-fog-and-sky.md`](../formats/distance-fog-and-sky.md).

## Where this left off

Built and shipped: travelling `Bullet` projectiles and launcher rounds, all three fire-dispatch
branches, beam tracers and their visuals, the billboard path, impact effects, the zone's distance
fog and banded sky, the `TSSolidPoly` outline pass, and structures as shootable objects.

Since: **everything in a mission is shootable, and hits land on named components.** All three
vtable `+0x20` implementations are ported, the `col\<NAME>.COL` reader is shared with
`BASECOL.DAT`, and the mech/flyer component health model (`Component_*`, the `+0x206` header) is
ported whole — the weighted overflow spill into a component's internals and the `BoneId` cascade
included. Closed with it: that a mech `.COL` is entirely node-placed (the hit volume walks with the
legs), that `typeRecord+0x18`/`+0x1a`/`+0x4a` are `.DAT` offsets 22/24/72, that `HercPiece.BoneId`
is a signed parent-component index, that the direct-fire multiplier is `SplashFactor` at Q10 and
not a weapon-type effectiveness scale, and that `Sim_RaycastObjectList` skips objects awaiting
deployment.

Since: **target selection, the sensor model and missile lock**, with homing reachable at last — see
[`../simulation/target-selection.md`](../simulation/target-selection.md) and
[`../simulation/missile-lock.md`](../simulation/missile-lock.md). Closed with them: that
`manager+0x0a` is the per-subtype lock state and not an ammunition count, that `mech+0x96` is the
PASSIVE/ACTIVE radar mode a HERC powers up without, and that everything aims at an object's shape
centre rather than its origin. Corrected on the way: `SimTrig.EulerToward` had its atan2 arguments
swapped and `Atan2Guarded` guarded the wrong operand, which nobody had noticed because no shot had
ever had a target to steer at.

Not built: **anything you can hear**, AI target acquisition, weapon-mount destruction, and the light
sources effects are supposed to cast.

## Next

### The HUD target box

A target can be selected but nothing draws it. `Gunsight_SetValues` pushes the target and a flag
into the gunsight widget, and `FUN_00434a24` parks the target and its world aim point at
`CockpitView+0x26c`..`+0x27e`. The reader is one of gunsight children 0, 4, 5, 6 or 8 — all
constructed, none traced. Without it the only evidence of a selection is the debug panel.

### AI target acquisition

AI machines never select anything, so they never fire and never switch their radar on — which is
also why a hostile is only targetable at long range once the player goes ACTIVE. The setter is
`FUN_0041c0f4`; the state functions that call it are `FUN_0041c418`, `FUN_0041cf18`, `FUN_0041d60c`,
`FUN_0041d7d0`, `FUN_0041d9cc`, `FUN_0041daac` and `FUN_0041e224`, which also drive `mech+0x96`
from a per-state flag table at `mech+0x92`.

## Also outstanding, lower priority

- **`TSShadedPoly` still uses an averaged atlas colour**, which is a stand-in and not the mechanism.
  The real one is `Palette_ShadeRampLookup` (`00430e34`) against the active palette's own shade
  ramps, which sit unparsed in the tail of every `.DPL` after the 256 colour entries.
  `DynamixPaletteTransformer` stops at the colours. Decoding the tail yields clean monotone ramps
  (`{?, ?, int16 count, int16 indices[count]}`-ish — the first two fields are not pinned down, and
  the framing desyncs after the eighth ramp), so RE the DPL loader's writer of
  `ActivePaletteObject+0x0c`/`+0x10` rather than guessing the header. **This changes how every mech
  and building looks**, so it wants its own pass and its own verification.
- **Sound.** `Bullet_Fire` plays `record[+8] + 10`; `Bullet_FireBurst` opens with
  `FUN_004627dc(0x0b, muzzlePoint)`; an impact effect plays its type row's `SoundId + 10` through the
  same call. Untraced past it. One entry point covers all three.
- **Weapon-mount destruction.** Components 19–28 are the machine's mounts, indexed
  `component - 19` into the mount manager. A health-band change on one rolls (`FUN_00410670` →
  `FUN_0040f57c`) to knock it out and then finishes the component with a flat 10000. The roll and
  the component side are decoded; the mount manager's own destroy path is not.
- **The explosive blast sweep** (`FUN_00426a20`, mech vtable `+0x70`). Its absence is why
  `SplashFactor`'s share of a direct-fire hit is dropped rather than diverted, why plasma does not
  splash, and why a destroyed weapon mount cannot explode. Fully decoded in
  [`../simulation/damage-system.md`](../simulation/damage-system.md).
- **Effect light sources.** An `EXPLOS.DAT` row's `LightMode` and its twelve-frame intensity ramp
  drive a light object the engine does not have. Wants whatever lighting model replaces the
  `TSShadedPoly` stand-in, not a bolt-on. Effect light sources are not obviously visible in retail,
  so it's possible that this logic was misunderstood, or that the light sources have a brightness of
  0, or are only used in certain zones.
- **Structures clip.** Projectiles and impact effects visibly sink into buildings, which retail does
  not do. **Hit geometry is ruled out** — measured, see
  [`../simulation/hit-detection.md`](../simulation/hit-detection.md), "Measured: hit geometry versus
  the drawn mesh". Remaining suspect is render layering.
- **ELF and ELF2 draw straight** — the jagged branch's paint half (`FUN_0048c964`, `FUN_0048ce14`,
  `FUN_0048d4b4`) is undecoded. See the beam-visuals doc.
- **Field of view.** Still a guess. The original's focal length is the per-view shift at `view+0x1a`
  that `Raster_InstallViewProjection` installs; its writer has not been traced. It also affects the
  sky, whose band height is measured in screen rows while the two retail captures disagree slightly.
  Note it does **not** affect billboard size — the shift cancels out of that formula, see
  [`../formats/dts-billboards.md`](../formats/dts-billboards.md).
- **The 3D view is not clipped to the `.VUE` viewport rect.** Equivalent while the canopy is opaque;
  not for RAZOR's non-stub heads-down view.
- **Far clip does not follow the visibility range.** The engine fogs to the zone's range but still
  draws past it; retail's terrain draw region is that same radius
  (`Terrain_BuildDrawRegionQuad`, `0046d220`). Cheap to try, changes what is on screen at the edges.
