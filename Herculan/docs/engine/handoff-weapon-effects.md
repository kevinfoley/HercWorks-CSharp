# Handoff — after structure hit detection

Rewritten 2026-08-27. Addresses are starting points to decompile, not settled findings — check
anything load-bearing. What *is* settled is in
[`../simulation/projectiles.md`](../simulation/projectiles.md),
[`../simulation/rockets.md`](../simulation/rockets.md),
[`../simulation/weapon-firing.md`](../simulation/weapon-firing.md),
[`../simulation/beam-visuals.md`](../simulation/beam-visuals.md),
[`../simulation/impact-effects.md`](../simulation/impact-effects.md),
[`../simulation/structure-hit-detection.md`](../simulation/structure-hit-detection.md),
[`../formats/dts-billboards.md`](../formats/dts-billboards.md),
[`../formats/dts-texture-binding.md`](../formats/dts-texture-binding.md) and
[`../formats/distance-fog-and-sky.md`](../formats/distance-fog-and-sky.md).

## Where this left off

Built and shipped: travelling `Bullet` projectiles and launcher rounds, all three fire-dispatch
branches, beam tracers and their visuals, the billboard path, impact effects, the zone's distance
fog and banded sky, and the `TSSolidPoly` outline pass.

Since: **structures are shootable.** Both of `Base_DirectFireHitTest`'s hit paths are ported — the
`dat\BASECOL.DAT` sphere model and the `.DGS` record's collision height field — along with the
per-component damage model and its random early-destruction roll. Closed with it: that the `.DGS`
tail this project called "an opaque block plus sub-records" is that height field, that `shape+8` is
a bounding radius rather than an id, and that `Mech_SelectStruckComponent` is shared by all three
object classes.

Not built: **anything you can hear**, target selection, flyer and mech component selection, and the
light sources effects are supposed to cast.

## Next

Pick one; they are independent.

### Flyer and mech component selection

Now cheap, and it closes two gaps at once. Flyers have no vtable `+0x20`, so nothing can shoot an
aircraft; mechs have one but stop at `MechObject.PenetratingHits` because nothing picks the
component struck. Both want the same missing piece: the per-type hit-sphere models in
`col\<NAME>.COL` (22 retail files, one per HERC plus `SKIMMER`), which use the reader already
ported for `dat\BASECOL.DAT` — see
[`../simulation/structure-hit-detection.md`](../simulation/structure-hit-detection.md). Past the
loader, the flyer needs `FUN_00421c8c` ported and the mech needs the 29-slot component health array
(`Mech_ApplyDirectFireDamage`, `004188c8`) that
[`../simulation/damage-system.md`](../simulation/damage-system.md) documents.

### Target selection

Now the single biggest unlock. Nothing homes — not the plasma round, not a missile — because both
read `mech+0x1a4` and nothing ever sets it. It would also make the ECM weave, the anti-radiation
missile's emission gate and the HUD lead indicator reachable at once. Start from
`Mech_PerTickSystemsUpdate` (`0041aa5c`), which reads `mech+0x1a4` throughout and writes the
lock-related flags around it.

`manager+0x0a`, the per-ammunition-type counters, still blocks part of it: `Rocket_Fire` will not
attach a target without one, and `FUN_00410970` gates a missile row's ready box on the same array.
Readers found (`Mech_MissileAmmoCount`, `FUN_0041f358`), no writer traced.

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
- **Effect light sources.** An `EXPLOS.DAT` row's `LightMode` and its twelve-frame intensity ramp
  drive a light object the engine does not have. Wants whatever lighting model replaces the
  `TSShadedPoly` stand-in, not a bolt-on. Effect light sources are not obviously visible in retail,
  so it's possible that this logic was misunderstood, or that the light sources have a brightness of
  0, or are only used in certain zones.
- **Mech component damage.** Shield absorption is real; past it, `MechObject.PenetratingHits` counts
  hits a 29-slot component health array would have applied (`Mech_ApplyDirectFireDamage`
  `004188c8`). Promoted to "Next" above, since the component-selection half is now a loader away.
- **Plasma does not splash and does not home.** Its branch zeroes the shot's damage and calls
  `Damage_ExplosiveBlastSweep` with a 4000-unit radius; the engine has no blast sweep, so the round
  keeps its direct-fire damage instead. Zeroing the record is also why a structure hit recovers the
  damage from a global — see
  [`../simulation/structure-hit-detection.md`](../simulation/structure-hit-detection.md). Homing is
  ported but reads a selected target, and there is no target selection.
- **Structures clip.** Projectiles and impact effects visibly sink into buildings, which retail does
  not do. Uninvestigated; candidates are the collision bound running slightly small or a render
  layering problem. See `KNOWN_ISSUES.md`.
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
