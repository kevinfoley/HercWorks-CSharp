# Handoff — after beam visuals

Rewritten 2026-08-25, at the end of the beam-visuals milestone. Addresses are starting points to
decompile, not settled findings — check anything load-bearing. What *is* settled is in
[`../simulation/beam-visuals.md`](../simulation/beam-visuals.md) and
[`../simulation/weapon-firing.md`](../simulation/weapon-firing.md).

## Where this left off

Built and shipped: the tracer object and its one-tick life, `BEAM.DAT` and `BEAMTEX.DBA`, the quad
and its half-width floor, the near-plane clip, and the `.VUE` projection centre. Holding `[Space]`
draws a beam from the muzzle to whatever it hit, in the retail orange-to-white ribbon, converging on
the gunsight.

Not built: **anything you can hear**, anything a non-beam weapon fires, and anything that happens
where a beam lands.

## Next, in the order I would take them

### 1. Projectiles — bullets first

The largest remaining gap in the weapon milestone, and the one that unblocks most weapons: EMP,
autocannon and plasma are all `PROJ.DAT` type `Bullet`, which means a real travelling object rather
than a hitscan.

- The gun dispatch's bullet branch is the other half of `WeaponMount_FireDispatch_GunBeam`
  (`0040ea58`); the ammunition dispatch (`0040e964`) has its own bullet fallback.
- The object's class has vtable `PTR_FUN_00498628` and a per-tick method that moves it and re-tests
  — see [`../simulation/damage-system.md`](../simulation/damage-system.md), which already sketches
  the three rocket/bullet families and their type tables.
- `PROJ.DAT`'s `Speed` is the mover (fixed point, 5000 → 500.0). Beams are the `Speed == 0` case that
  made hitscan possible.
- **The ammunition dispatch's round cost is deliberately still not taken** — a magazine that empties
  with nothing leaving the barrel is worse than one that does not move. Take it with this.
- Visuals reuse most of this milestone: `Bullet_FireBurst`'s tracer path is shared, and `BULLETS.DAT`
  sits beside `BEAM.DAT` in the same folder with a parser already in `HercWorks.Core`.
- Plasma is a homing projectile. Homing functionality can be stubbed for now, or the Plasma can be
  left for a future session.

### 2. ELF and ELF2 — the jagged branch

Small, self-contained, and now well specified. Geometry is fully decoded (see the beam-visuals doc);
what is missing is the paint half — `FUN_0048c964`, `FUN_0048ce14`, `FUN_0048d4b4`, called per node
with `DAT_006c6968`/`DAT_006c696a` set. Expect this to be a dig through the software rasterizer's
span/setup helpers rather than a single function.

Two things make it tractable now that were not before: `Screenshots/Simulator3.jpg` shows exactly
what the answer looks like (a bright yellow zigzag), and `BEAM.DAT`'s colour index for ELF is 104,
yellow — so this branch is where that field is consumed, which the straight path proved it is not.

The disassembly puzzle to resolve first: the shared tail reads `points[0]` and `points[1]` with no
loop index, which cannot draw a chain. One of those three helpers must redirect the geometry.

### 3. Structure hit detection

Beams currently pass through buildings. `BaseObject` has no `DirectFireHitTest`; the original's base 
class has its own vtable `+0x20`, findable from the class's constructor (`FUN_00405314`). Port it 
the way `MechObject`'s was. 

`FlyerObject` is the same gap, but nothing in a mission shoots at aircraft yet.

### 4. Impact effects

`FUN_00407f1c`, allocated from `DAT_004a96a2`. Resolves a DTS shape through a 0x28-byte type table
and drives an animation, so it needs a sprite/shape effect system the engine does not have — its own
milestone, not a tail end of this one. Which of the `PROJ.DAT` record's three `ImpactFX` arrays a
shot uses is half-traced; see the beam-visuals doc.

## Also outstanding, lower priority

- **Sound.** `Bullet_FireBurst` opens with `FUN_004627dc(0x0b, muzzlePoint)`. Untraced entirely.
- **Component damage.** Shield absorption is real; past it, `MechObject.PenetratingHits` counts hits
  a 29-slot component health array would have applied. That array is its own milestone
  (`Mech_SelectStruckComponent` `0040c9d4`, `Mech_ApplyDirectFireDamage` `004188c8`).
- **Field of view.** Still a guess. The original's focal length is the per-view shift at `view+0x1a`
  that `Raster_InstallViewProjection` installs; its writer has not been traced. Worth doing while
  near the view code — it changes every panel, so it wants its own verification pass.
- **The 3D view is not clipped to the `.VUE` viewport rect.** The engine renders the world into the
  whole panel and lets the canopy art mask it. Equivalent while the canopy is opaque; not equivalent
  for RAZOR's non-stub heads-down view.
- **`manager+0x0a`**, the per-ammunition-type counters `FUN_00410970` gates missile readiness on.
  Readers found (`FUN_004155ac`, `FUN_0041f358`), no writer traced. Only matters for missiles.
