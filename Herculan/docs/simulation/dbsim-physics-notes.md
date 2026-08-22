# DBSIM.EXE simulation physics — fixed-point core, collision, rocket flight

NOTE TO CLAUDE: This should be a reference document, not a personal journal.

Reverse-engineered from `DBSIM.EXE` disassembly (Ghidra project `ES2Recon`, `-cspec windows`
reimport + `ES2CommitAllParams.java` applied — see `project_es2_exe_recon` memory for setup). All
addresses are DBSIM.EXE virtual addresses. Several numeric claims below (fast-magnitude
coefficients, fixed-point shift amounts) were checked against raw disassembly, not just
decompiler output.

Scope is movement/collision/projectile-flight math only. Combat damage resolution (shields,
components, weapon effectiveness) is in [`damage-system.md`](damage-system.md). Terrain heightmap
format and query is in [`../formats/terrain-heightmap.md`](../formats/terrain-heightmap.md).

## Fixed-point math toolkit

Shared helper functions used throughout the sim, not tied to any one subsystem — the primitives
every other section below builds on.

**`DAT_004d3be8` — the global simulation timestep**, read by both helpers below. Everything
scaled by it is a "per this tick" quantity, not "per second" — DBSIM runs a discrete
fixed/semi-fixed timestep sim, not a continuous-time integrator.

**`FUN_0047df94(a, b)` — Q8 fixed-point multiply.** `(int64)a * b`, right-shifted 32 bits via
`SHRD EAX,EDX,0x8` (i.e. `>> 8`, scale factor 256). Confirmed via raw disassembly
(`FUN_0047df94_asm.txt`). Two adjacent sibling functions share the same `IMUL`+`SHRD` shape at
different shift amounts (`0xa` = Q10, `0xe` = Q14 with a 16-bit signed operand) — Q8 is used for
position/rate math below; Q14's range fits a normalized `-1.0..1.0` value like a sin/cos table
output, though no caller confirms that.

**`FUN_00467820(rate)` — "integrate this rate over one tick."** `Q8mul(DAT_004d3be8, rate)`,
clamped to signed 16-bit range (`[-0x7fff, 0x7fff]`). The core "apply a per-unit-time rate as this
tick's delta" primitive — called on velocity/acceleration-like type-table fields to get a position
delta, and on trig-adjacent values (rocket homing below).

**`FUN_00467944(timerPtr)` — countdown timer tick.** `*timerPtr -= DAT_004d3be8`, clamped to 0,
returns the new value. Used for cooldowns (e.g. the rocket seeker's target-reacquire timer).

**`FUN_004679d8(current*, target, step)` — rate-limited "move toward."** If `current < target`,
adds `step` (clamped so it doesn't overshoot `target`); symmetric for `current > target`. Returns
the remaining error (0 once `current == target`). A generic per-tick turn/slew-rate limiter — used
by the rocket's guidance to cap heading-correction rate (below), and reused for other rate-capped
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
- **Rocket target-proximity check** (`FUN_0040a538`, below) — `if (dist_approx < 40000) { ...proximity warning... }`.

## Collision system — hierarchical bounding-sphere construction (`collide.cpp`)

Address cluster `0x0040cc14`–`0x0040cd88`. This is the collision-model **load-time setup**, not
per-tick narrow-phase collision detection.

- **`FUN_0040cd88(name)`** — top-level entry point: allocates the next slot in a fixed collision-
  object table (`_DAT_004a98a8`, 6 bytes/entry, index counter `DAT_004987de`), then calls
  `FUN_0040ccf8` to populate it.
- **`FUN_0040ccf8(countOut, stream)`** — reads a record count, allocates `count × 10` bytes, and
  for each record: `FUN_0040cc50` (load), then `FUN_0040ccc8` (post-process).
- **`FUN_0040cc50(record, stream)`** — reads a per-record sub-sphere count, then for each
  sub-sphere calls `FUN_0040cc14`, which reads a flag byte and, if set, calls `FUN_0040c7c4` —
  which reads a 13-bit-masked count (`value & 0x1fff`, top 3 bits reserved for flags — a packed
  count+flags field) and, if nonzero, loads that many 8-byte index/sub-mesh records.
- **`FUN_0040c5d0(record, boundsOut)` — the actual math.** Given a list of child spheres (each
  `{x, y, z, radius}` as four `short`s), computes the record's own bounding volume:
  1. AABB from all children, each inflated by its own radius (`min(x-r)`, `max(x+r)`, etc. per
     axis) — a proper "union of spheres" bound, not just a union of center points.
  2. Center = midpoint of that AABB (`(max+min) >> 1` per axis).
  3. Radius = `FUN_0047dd66(halfExtentX, halfExtentY, halfExtentZ)` — the fast-magnitude
     approximation above, applied to the half-extents from center to the AABB corner.

  So the per-object collision bound is a **sphere approximating the AABB's circumscribing
  sphere**, built bottom-up from named sub-part spheres (a hierarchical hitbox/hardpoint tree —
  see [`damage-system.md`](damage-system.md#the-componenthealth-system) for the runtime
  counterpart of this tree).

## Rocket physics (`rocket.cpp`, cluster `0x0040a120`–`0x0040ac3c`)

**Type table** — loaded by `FUN_0040a818` (`DAT_004a9754`, 14-byte/`0xe` stride, count
`DAT_004a9758`; confirmed stride via the `IMUL`-style `*0xe` indexing seen directly in
`FUN_0040a234`, not just decompiler pointer arithmetic). Per-rocket-instance type lookup is
`FUN_0040a234(instance) = DAT_004a9754 + instance.typeIndex(byte @ +0x41) * 0xe` — a plain
`RocketType* getType()` accessor. Confirmed type-record field usage (not yet a full byte map):
- offset `+2`: max lifetime/tick-count — compared against the instance's running tick counter
  (`+0x54`) to trigger burnout.
- offset `+4`: a rate value fed through `FUN_00467820` (this-tick delta) — speed or acceleration.
- offset `+6`, `+8`, `+0xa`: used in ammo bookkeeping and a targeting-solver call
  (`FUN_00426528`, see [`damage-system.md`](damage-system.md#the-shared-raycast-fun_00426528)).

**Spawn ("fire a rocket"): `FUN_0040a9c4`** → constructs via `FUN_0040a948` (unguided/type-0
variant) or `FUN_0040ac3c` (type-3 variant, different type array `DAT_004a9768`/`DAT_004a9770`).
Sets launch position/velocity from caller args, stores the owner pointer (`+0x4a`), and — if the
owner currently has a valid weapon lock — copies the locked target pointer into `+0x56` and asks
the target for a tracking handle (vtable call `target[+0x54]`) stored at `+0x5a`. Lock-on state is
captured **once, at launch**, not re-acquired continuously (though see the reacquire-cooldown path
below, which does allow re-targeting later in flight).

**Per-tick update: `FUN_0040a538`.** Called once per rocket per tick. In order:
1. **Seeker reacquire.** If a per-instance cooldown timer (`+0x5c`) has just expired
   (`FUN_00467944` hits 0), attempts to claim/refresh a tracking slot on the current target via
   the type record's `+8`/`+0xa` fields (modular slot-index arithmetic against a per-target
   tracking-slot pool) — rockets share a limited number of "lock" slots per target and
   periodically re-roll for one rather than holding it exclusively for the whole flight.
2. **Burnout check.** `age (+0x54) += 1`; if `age > type.maxLifetime (+2)`, sets the "detonate/
   remove" flag (`local_6 = 1`) and skips straight to the fuel-outcome branch — a rocket that
   outlives its motor just expires.
3. **Otherwise, integrate one tick:** advances `+0x52` (current speed) by
   `FUN_00467820(type.rate @ +4)`, averages it with the previous value (`(new+old)>>1` — a simple
   damped/smoothed speed update, not instantaneous), clamps to the launcher's max speed
   (`launcher[+10]`).
4. **Homing vs. ballistic:** if the owner is a "network remote" actor (`owner[+0xa3] != 0`) and
   this rocket is guided (`type == 3`), takes a stripped-down unguided path (`FUN_0040a488`,
   ballistic — resets position derived purely from stored velocity, no steering). Otherwise calls
   `FUN_0040a254` — the actual homing/steering logic.
5. **Proximity fuze.** For locally-simulated (non-remote) rockets: computes
   `FUN_0047dd66(pos - target.leadPos)` (the fast-magnitude helper); if `< 40000` (game units,
   unconverted), sets a proximity-warning flag once and calls `FUN_0046272c(0x32)` — most likely a
   proximity alarm sound/HUD cue, not a fuze detonation itself; the flag is only cleared, not
   itself checked for detonation, within this function. The actual ground-impact detonation is a
   separate function — `FUN_00409d2c`, see
   [`damage-system.md`](damage-system.md#explosive-damage-blast-sweep-random-per-component-roll-distance-falloff-shield-gated).

**Homing/steering: `FUN_0040a254`.** Only runs when the rocket has a live target and (a) the
target hasn't switched controllers mid-flight in a way that invalidates the lock, and (b) the
target isn't "gone" (destroyed/stealthed — checked via two target-state bytes). Computes the
line-of-sight delta to a **lead-predicted target position** (`FUN_00492884`, a
position-extrapolation helper, not traced) rather than the target's raw current position —
rockets lead their target, they don't just chase the instantaneous position. That delta is then,
for non-network non-type-2 rockets, given a **deadband**: if the heading error on an axis falls
within `±0x1800` (6144) of zero but outside `±0xc00` (3072), it's pushed out to exactly `±0xc00` —
avoids twitchy micro-corrections in the visual flight path without fully ignoring the axis. The
(possibly deadbanded) delta then goes through `FUN_004679d8` twice — the rate-limited "move-toward"
helper — applied with a fixed max-turn-rate constant `0x500` (1280) per axis per tick. This **is**
the missile's turn-rate cap: `0x500` game-units of heading change per tick, independent of rocket
type. After steering, the (still fixed-point) result is passed back through `FUN_00467820` and
added into the stored position — the "steering command" and "this tick's positional delta" share
the same Q8/timestep-scaled pipeline as straight-line motion.

**Non-homing variant: `FUN_0040a488`.** Simpler — takes the launcher's aim-point delta directly
(no deadband, no rate limiting), scales through `FUN_0047df94`(Q8)/`FUN_00467820`, and clears the
launcher's stored aim-point fields afterward (one-shot consumption: a ballistic rocket uses the
aim solution present at the moment it fires and then forgets it, vs. the homing rocket's
continuous re-targeting).

**`fire.cpp` ruled out as a projectile-math source.** Only one function (`FUN_0046b0a4`) carries a
`fire.cpp` assert string, and it's a muzzle-flash/fire-effect resource loader (builds filenames by
appending an index to a base string, loads two effect variants, wires results into per-hardpoint
pointers). Projectile spawn/hit-resolution logic lives in `rocket.cpp`/`bullet.cpp` and
[`damage-system.md`](damage-system.md), not `fire.cpp`.

## Port notes

1. **DBSIM is a fixed-timestep, fixed-point simulation** — `DAT_004d3be8` is the tick length and
   essentially all motion math is `rate × tick` in Q8, not continuous float integration; a naive
   float-based reimplementation will drift from the original unless the same quantization/clamping
   is preserved.
2. **Collision bounds are spheres built from AABBs of named sub-part spheres**, using the
   ~3.4%-low-biased fast-magnitude approximation rather than true Euclidean distance —
   reproducing hit detection faithfully means reproducing that approximation, not substituting a
   real `sqrt`.
3. **Guided rockets lead-predict, deadband, and rate-limit their turn** — the `0x500`/tick turn
   cap and the `0xc00`/`0x1800` deadband thresholds most directly control how "floaty" vs.
   "locked-on" homing feels.
