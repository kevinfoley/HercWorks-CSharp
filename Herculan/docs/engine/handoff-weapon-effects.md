# Handoff — after projectiles

Rewritten 2026-08-26. Addresses are starting points to decompile, not settled findings — check
anything load-bearing. What *is* settled is in
[`../simulation/projectiles.md`](../simulation/projectiles.md),
[`../simulation/weapon-firing.md`](../simulation/weapon-firing.md),
[`../simulation/beam-visuals.md`](../simulation/beam-visuals.md),
[`../formats/dts-texture-binding.md`](../formats/dts-texture-binding.md) and
[`../formats/distance-fog-and-sky.md`](../formats/distance-fog-and-sky.md).

## Where this left off

Built and shipped: travelling `Bullet` projectiles — spawn, scatter, inherited launcher speed,
per-tick segment sweep, lifetime, and the damage they do; both gun-dispatch branches including the
big EMP's three barrels and `EMP2`'s two-volley burst; the autocannon's round cost and the rolling
cockpit counter.

Since: the **`TSSolidPoly` outline pass**, which is what an ATC35 round needed to read gold-edged and
white-centred; the zone's real **distance fog** (start, end and colour all off the zone and its
theater instead of hand-picked); and the theater's **banded sky**. The two previously-open questions
from this file — whether a projectile is pinned to ramp row 15, and where the sky colour comes from —
are both closed in `distance-fog-and-sky.md`.

Not built: **anything you can hear**, rockets and missiles, anything that happens where a shot lands,
and the three EMP rounds have no visual at all.

## Next: EMP rounds, with impact effects behind them

`BULLETS.DTS` roots 2 and 3 are a `TSCellAnimPart` of five `TSBitmapPart`s — a flipbook of billboard
sprites, not geometry — so `DtsMeshBuilder` builds nothing and all three EMP cannons fire an
invisible round. They simulate and do damage.

This needs a world-space sprite path, which is the same thing impact effects need, so the two belong
in one pass. `TSBitmapPart_Render` indexes the *active* DBA context by `BmpTag` rather than anything
the poly's own shape owns (see `dts-texture-binding.md`); `Bullet_TickUpdate` already keeps the
per-round animation countdown from `BULLETS.DAT+0x06`, and nothing steps a frame index yet.

No stronger candidate was found. The one thing that would change more on screen is `TSShadedPoly`
(below) — it is most of every mech and building — but it is a bigger, riskier pass and does not
unblock anything else.

## Rockets and missiles

The last unported fire branch. `Rocket_Fire`, `Rocket_ConstructGuided` (`0040ac3c`),
`Rocket_TickUpdate` (`0040a538`), `Rocket_HomingSteer` (`0040a254`), `Rocket_BallisticSteer`
(`0040a488`) are all already named and sketched in
[`../simulation/damage-system.md`](../simulation/damage-system.md); `ROCKETS.DAT` shares
`BULLETS.DAT`'s record layout. Note there is no `ROCKETS.DBA` — how a rocket's shape is textured is
an open question. **A launcher deliberately does not spend its round today**; take the spend with
this.

## Structure hit detection

Beams and bullets pass through buildings. `BaseObject` has no `DirectFireHitTest`; the original's
base class has its own vtable `+0x20`, findable from the class's constructor (`FUN_00405314`). Port
it the way `MechObject`'s was. `FlyerObject` is the same gap.

## Impact effects

`FUN_00407f1c`, allocated from `DAT_004a96a2`. Resolves a DTS shape through a 0x28-byte type table
and drives an animation. Which of the `PROJ.DAT` record's three `ImpactFX` arrays a shot uses is
half-traced; see the beam-visuals doc. Its pool is walked by `maybe_Scene_SubmitFrameObjects`
alongside the bullet pool, so it is drawn through the same per-cell path
(`distance-fog-and-sky.md`).

## Also outstanding, lower priority

- **`TSShadedPoly` still uses an averaged atlas colour**, which is a stand-in and not the mechanism.
  The real one is `Palette_ShadeRampLookup` (`00430e34`) against the active palette's own shade
  ramps, which sit unparsed in the tail of every `.DPL` after the 256 colour entries.
  `DynamixPaletteTransformer` stops at the colours. Decoding the tail yields clean monotone ramps
  (`{?, ?, int16 count, int16 indices[count]}`-ish — the first two fields are not pinned down, and
  the framing desyncs after the eighth ramp), so RE the DPL loader's writer of
  `ActivePaletteObject+0x0c`/`+0x10` rather than guessing the header. **This changes how every mech
  and building looks**, so it wants its own pass and its own verification. It is also what
  `KNOWN_ISSUES.md`'s "terrain lighting doesn't match retail" is.
- **Sound.** `Bullet_Fire` plays `record[+8] + 10`; `Bullet_FireBurst` opens with
  `FUN_004627dc(0x0b, muzzlePoint)`. Untraced past the call.
- **Component damage.** Shield absorption is real; past it, `MechObject.PenetratingHits` counts hits
  a 29-slot component health array would have applied (`Mech_SelectStruckComponent` `0040c9d4`,
  `Mech_ApplyDirectFireDamage` `004188c8`).
- **Plasma does not splash and does not home.** Its branch zeroes the shot's damage and calls
  `Damage_ExplosiveBlastSweep` with a 4000-unit radius; the engine has no blast sweep, so the round
  keeps its direct-fire damage instead. Homing is ported but reads a selected target, and there is no
  target selection.
- **ELF and ELF2 draw straight** — the jagged branch's paint half (`FUN_0048c964`, `FUN_0048ce14`,
  `FUN_0048d4b4`) is undecoded. See the beam-visuals doc.
- **Field of view.** Still a guess. The original's focal length is the per-view shift at `view+0x1a`
  that `Raster_InstallViewProjection` installs; its writer has not been traced. Now also affects the
  sky: `distance-fog-and-sky.md`'s band height is measured in screen rows, and the two retail
  captures disagree slightly, which would be explained by the bands being angular.
- **The 3D view is not clipped to the `.VUE` viewport rect.** Equivalent while the canopy is opaque;
  not for RAZOR's non-stub heads-down view.
- **`manager+0x0a`**, the per-ammunition-type counters `FUN_00410970` gates missile readiness on.
  Readers found (`FUN_004155ac`, `FUN_0041f358`), no writer traced. Only matters for missiles.
- **Far clip does not follow the visibility range.** The engine fogs to the zone's range but still
  draws past it; retail's terrain draw region is that same radius
  (`Terrain_BuildDrawRegionQuad`, `0046d220`). Cheap to try, changes what is on screen at the edges.
