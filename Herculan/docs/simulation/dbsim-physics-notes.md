# DBSIM.EXE simulation physics — fixed-point core and collision

NOTE TO CLAUDE: This should be a reference document, not a personal journal.

Reverse-engineered from `DBSIM.EXE` disassembly (Ghidra project `ES2Recon`, `-cspec windows`
reimport + `ES2CommitAllParams.java` applied — see `project_es2_exe_recon` memory for setup). All
addresses are DBSIM.EXE virtual addresses. Several numeric claims below (fast-magnitude
coefficients, fixed-point shift amounts) were checked against raw disassembly, not just
decompiler output.

Scope is the shared fixed-point primitives and the collision-bound build. Projectile flight is in
[`projectiles.md`](projectiles.md) and [`rockets.md`](rockets.md); combat damage resolution
(shields, components, weapon effectiveness) is in [`damage-system.md`](damage-system.md); the
terrain heightmap format and query is in
[`../formats/terrain-heightmap.md`](../formats/terrain-heightmap.md).

## Fixed-point math toolkit

Shared helper functions used throughout the sim, not tied to any one subsystem — the primitives
every other section below builds on.

**`DAT_004d3be8` — the global simulation timestep (`SimTickDelta`)**, read by both helpers below
and computed once per tick by `FUN_004677bc`:

```
spin until GetTickCount() >= last + 40             // 25 Hz frame cap
SimTickDelta = clamp((elapsedMs << 8) / 125, 0x40, 0x1c2)
```

Q8, where `1.0` (`0x100`) = 125 ms — helper "rates" below are per-125ms quantities, not
per-second or per-tick-count. Everything scaled by it is a "per this tick" quantity — DBSIM runs
a discrete fixed/semi-fixed timestep sim, not a continuous-time integrator. At the vanilla 40
ms/25 Hz tick this evaluates to **81** (`40×256/125`, floored) — the constant the engine's
`SimWorld.TickDelta` is pinned to, running a fixed 25 Hz tick decoupled from rendering rather than
reproducing the spin-wait.

Not every per-tick quantity is scaled by this timestep: locomotion's `SpeedAccelDecel`/
`DecelTurning` accel-step fields are raw per-tick steps with no `FUN_00467820` integration,
making the original's control law frame-rate dependent — see
[`mech-locomotion.md`](mech-locomotion.md#timing) for the consequence and the engine's
`SimMath.ScalePerTickStep` port deviation.

**`FUN_0047df94(a, b)` — Q8 fixed-point multiply.** `(int64)a * b`, right-shifted 32 bits via
`SHRD EAX,EDX,0x8` (i.e. `>> 8`, scale factor 256). Confirmed via raw disassembly
(`FUN_0047df94_asm.txt`). Two adjacent sibling functions share the same `IMUL`+`SHRD` shape at
different shift amounts (`0xa` = Q10, `0xe` = Q14 with a 16-bit signed operand) — Q8 is used for
position/rate math below; Q14's range fits a normalized `-1.0..1.0` value like a sin/cos table
output, though no caller confirms that.

**`FUN_00467820(rate)` — "integrate this rate over one tick."** `Q8mul(DAT_004d3be8, rate)`,
clamped to signed 16-bit range (`[-0x7fff, 0x7fff]`). The core "apply a per-unit-time rate as this
tick's delta" primitive — called on velocity/acceleration-like type-table fields to get a position
delta, and on trig-adjacent values (missile guidance, see `rockets.md`).

**`FUN_00467944(timerPtr)` — countdown timer tick.** `*timerPtr -= DAT_004d3be8`, clamped to 0,
returns the new value. Used for cooldowns (e.g. a projectile shape's animation-frame interval).

**`FUN_004679d8(current*, target, step)` — rate-limited "move toward."** If `current < target`,
adds `step` (clamped so it doesn't overshoot `target`); symmetric for `current > target`. Returns
the remaining error (0 once `current == target`). A generic per-tick turn/slew-rate limiter — used
by missile guidance to cap heading-correction rate, and reused for other rate-capped
values.

**`FUN_0047dd66(dx, dy, dz)` — fast (sqrt-free) 3D magnitude approximation.** Takes `|dx|,|dy|,|dz|`,
sorts into `L ≥ M ≥ S`, returns:

```
L + M×0.34375 + S×0.25          (M×(1/4 + 1/16 + 1/32), S×(1/4))
```

Classic alpha-max-plus-beta-min-style 3D distance approximation (avoids a real `sqrt`). **Verified
against raw disassembly** (`FUN_0047dd66_asm.txt`) — the sort is three `CMP`/`XCHG` pairs, the
coefficient computation is `SAR`+`ADD` chains, matching the decompiled formula exactly. Reused for
two unrelated purposes, confirming it's a general math-library utility:
- **Collision bounding-sphere radius** (`FUN_0040c5d0`, below).
- **Missile target-proximity check** (`Rocket_TickUpdate`, `0040a538`) — `if (dist_approx < 40000) { ...proximity warning... }`.

## Collision system (`collide.cpp`)

Address cluster `0x0040c428`–`0x0040cd88`: the per-object hit-sphere model, its load-time setup and
its ray test. **Fully decoded and ported for structures** —
[`hit-detection.md`](hit-detection.md) carries the file format, the readers, the
ray test and the retail verification. Three points that belong with the rest of the fixed-point math:

- The model is a tree of `{x, y, z, radius}` `int16` spheres grouped into clusters, one cluster per
  destructible component of the object.
- Each cluster's bound (`Collision_ComputeBoundingSphere`, `0040c5d0`) is the AABB of its children
  each inflated by its own radius, centred on that box's midpoint, radius =
  `Math_FastMagnitude3D(halfExtents)`. Reproducing hit detection faithfully means reproducing that
  ~3.4%-low bias, not substituting a real `sqrt`.
- `Collision_RegisterObject` (`0040cd88`) loads one model by name into a fixed table
  (`_DAT_004a98a8`, 6 bytes/entry, counter `DAT_004987de`). Its two callers are `Mech_Constructor`
  (`00415bb0` → `mech+0x1f6`) and the flyer type loader (`FUN_00422ed0` → `+0x32`), both from
  `col\<NAME>.COL`. Structures use the same reader against `dat\BASECOL.DAT` instead. **The `.COL`
  files are unported**, which is why mech and flyer component selection is still missing.

## Rocket physics

Moved to [`rockets.md`](rockets.md), which supersedes what was here: the earlier reading of
`ROCKETS.DAT`'s fields `+6`/`+8`/`+0xa`, of the per-tick "seeker reacquire" step (it is the exhaust
flame's animation counter), and of `Rocket_BallisticSteer` (renamed `Rocket_PlayerSteer` — it is the player flying an
electro-optical missile, not a ballistic variant) were all wrong.

**`fire.cpp` ruled out as a projectile-math source.** Only one function (`FUN_0046b0a4`) carries a
`fire.cpp` assert string, and it's a muzzle-flash/fire-effect resource loader (builds filenames by
appending an index to a base string, loads two effect variants, wires results into per-hardpoint
pointers). Projectile spawn/hit-resolution logic lives in `rocket.cpp`/`bullet.cpp` and
[`damage-system.md`](damage-system.md), not `fire.cpp`.

## Port notes

1. **DBSIM ticks at a fixed 25 Hz** (`SimTickDelta`/`DAT_004d3be8` = 81 in Q8/125ms units at that
   rate) and essentially all motion math is `rate × tick` in Q8, not continuous float integration;
   a naive float-based reimplementation will drift from the original unless the same
   quantization/clamping is preserved. Not universal — some per-tick fields (locomotion's
   accel/decel steps) are unscaled and frame-rate dependent in the original; see
   [`mech-locomotion.md`](mech-locomotion.md#timing).
2. **Every range and radius comparison in the simulation uses the ~3.4%-low fast-magnitude
   approximation**, collision bounds included — reproducing hit detection faithfully means
   reproducing that bias, not substituting a real `sqrt`.
3. **Missile guidance leads its target and rate-limits its turn at `0x500`/tick, and weaves by
   `0xc00` while the target is jamming** — see [`rockets.md`](rockets.md).
