# DBSIM.EXE simulation math — fixed-point core, collision, rocket physics, damage system

Reverse-engineered from `DBSIM.EXE` disassembly (Ghidra, `E:\ES2Stuff\tools\`), continuing from
the file-format-focused recon documented in `docs/formats/`. This doc is about **game-logic
math** (how combat actually computes), not file layouts — see `docs/formats/` for those. Several
numeric claims below (the fast-magnitude coefficients, the fixed-point shift) were checked
against raw disassembly, not just decompiler output, following the same discipline that caught
the earlier WEAPONS.DAT stride error.

All addresses are DBSIM.EXE virtual addresses in the current Ghidra project (`ES2Recon`,
`-cspec windows` reimport + `ES2CommitAllParams.java` already applied, decompiler output is
trustworthy — see `project_es2_exe_recon` memory for the setup).

**A note on how the damage-system section below was produced, since it went through several
false starts before settling:** the first pass mislabeled a value as "armor" that turned out to
be shields; a second pass, after being corrected, produced an incomplete two-layer model; both
mistakes traced back to describing what a function does from its *call shape* — argument count,
who calls it, what a sibling function looks like — instead of actually decompiling and reading
it. The user then added the official `Earthsiege 2 - On-Line Manual.pdf` to the repo root, and a
third pass, decompiling every remaining function in the chain before writing anything down and
checking each conclusion against the manual's actual wording, produced the account below. It's
presented as one coherent result; the false starts are preserved in `project_es2_exe_recon`
memory as a standing reminder of the failure mode, not repeated here.

## Fixed-point math toolkit

A small family of shared helper functions used throughout the sim, not tied to any one
subsystem. These are the primitives every other section below builds on.

**`DAT_004d3be8` — the global simulation timestep**, read (never written, within this analysis)
by both helpers below. Everything scaled by it is a "per this tick" quantity, not "per second" —
confirms DBSIM runs a discrete fixed/semi-fixed timestep sim, not a continuous-time integrator.

**`FUN_0047df94(a, b)` — Q8 fixed-point multiply.** `(int64)a * b`, right-shifted 32 bits via
`SHRD EAX,EDX,0x8` (i.e. `>> 8`, scale factor 256). Confirmed via raw disassembly
(`FUN_0047df94_asm.txt`) — the decompiler's `>>8` matched the actual `SHRD ...,0x8` instruction
exactly. Immediately adjacent in the binary are two sibling functions with the same `IMUL`+`SHRD`
shape but different shift amounts (`0xa` = Q10, `0xe` = Q14 with a 16-bit signed operand) — a
small family of fixed-point multiply helpers at different scales, likely for different unit
domains (Q8 for position/rate math below; Q14's range fits a normalized `-1.0..1.0` value like a
sin/cos table output, though that's not yet confirmed by a caller).

**`FUN_00467820(rate)` — "integrate this rate over one tick."** `Q8mul(DAT_004d3be8, rate)`,
clamped to signed 16-bit range (`[-0x7fff, 0x7fff]`). This is the core "apply a per-unit-time rate
as this tick's delta" primitive — called on velocity/acceleration-like type-table fields to get a
position delta, and on trig-adjacent values (see rocket homing below).

**`FUN_00467944(timerPtr)` — countdown timer tick.** `*timerPtr -= DAT_004d3be8`, clamped to 0,
returns the new value. Used for cooldowns (e.g. the rocket seeker's target-reacquire timer,
below).

**`FUN_004679d8(current*, target, step)` — rate-limited "move toward."** If `current < target`,
adds `step` (clamped so it doesn't overshoot `target`); symmetric for `current > target`. Returns
the remaining error (0 once `current == target`). This is a generic per-tick turn/slew-rate
limiter — used by the rocket's guidance to cap how fast it can correct its heading (see below),
and plausibly reused elsewhere for any other rate-capped value.

**`FUN_0047dd66(dx, dy, dz)` — fast (sqrt-free) 3D magnitude approximation.** Takes `|dx|,|dy|,|dz|`,
sorts them into `L ≥ M ≥ S` (largest/mid/smallest), returns:

```
L + M×0.34375 + S×0.25          (M×(1/4 + 1/16 + 1/32), S×(1/4))
```

This is a classic alpha-max-plus-beta-min-style 3D distance approximation (avoids a real
`sqrt`, consistent with a 1997-era title targeting CPUs where FPU `sqrt` was expensive or a
software fallback existed for non-Pentium machines). **Verified against raw disassembly**
(`FUN_0047dd66_asm.txt`) — the sort is three `CMP`/`XCHG` pairs, the coefficient computation is
`SAR`+`ADD` chains (`>>2`, `>>2` again for `>>4`, `>>1` more for `>>5`), matching the decompiled
formula exactly. Reused for two unrelated purposes, confirming it's a general math-library
utility rather than something purpose-built for one subsystem:
- **Collision bounding-sphere radius** (`FUN_0040c5d0`, below).
- **Rocket target-proximity check** (`FUN_0040a538`, below) — `if (dist_approx < 40000) { ...proximity warning... }`.

## Collision system — hierarchical bounding-sphere construction (`collide.cpp`)

Address cluster `0x0040cc14`–`0x0040cd88`. This is the collision-model **load-time setup**, not
per-tick narrow-phase collision detection (that logic wasn't located this session — see Open
items below).

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
  sphere**, built bottom-up from named sub-part spheres (consistent with a hierarchical
  hitbox/hardpoint tree — see the damage system below for the runtime counterpart of this tree).

## Rocket physics (`rocket.cpp`, cluster `0x0040a120`–`0x0040ac3c`)

**Type table** — loaded by `FUN_0040a818` (`DAT_004a9754`, 14-byte/`0xe` stride, count
`DAT_004a9758`; confirmed stride via the `IMUL`-style `*0xe` indexing seen directly in
`FUN_0040a234`, not just decompiler pointer arithmetic). Per-rocket-instance type lookup is
`FUN_0040a234(instance) = DAT_004a9754 + instance.typeIndex(byte @ +0x41) * 0xe` — i.e. a plain
`RocketType* getType()` accessor. Confirmed type-record field usage (not yet a full byte map):
- offset `+2`: max lifetime/tick-count — compared against the instance's running tick counter
  (`+0x54`) to trigger burnout.
- offset `+4`: a rate value fed through `FUN_00467820` (this-tick delta) — speed or acceleration.
- offset `+6`, `+8`, `+0xa`: used in ammo bookkeeping and a targeting-solver call
  (`FUN_00426528`, see the damage system below).

**Spawn ("fire a rocket"): `FUN_0040a9c4`** → constructs via `FUN_0040a948` (unguided/type-0
variant) or `FUN_0040ac3c` (type-3 variant, different type array `DAT_004a9768`/`DAT_004a9770`).
Sets launch position/velocity from caller args, stores the owner pointer (`+0x4a`), and — if the
owner currently has a valid weapon lock — copies the locked target pointer into `+0x56` and asks
the target for a tracking handle (vtable call `target[+0x54]`) stored at `+0x5a`. Confirms
lock-on state is captured **once, at launch**, not re-acquired continuously (though see the
reacquire-cooldown path below, which does allow re-targeting later in flight).

**Per-tick update: `FUN_0040a538`.** Called once per rocket per tick. In order:
1. **Seeker reacquire.** If a per-instance cooldown timer (`+0x5c`) has just expired
   (`FUN_00467944` hits 0), attempts to claim/refresh a tracking slot on the current target via
   the type record's `+8`/`+0xa` fields (modular slot-index arithmetic against a per-target
   tracking-slot pool) — i.e. rockets share a limited number of "lock" slots per target and
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
   unconverted), sets a proximity-warning flag once and calls `FUN_0046272c(0x32)` — most likely
   a proximity alarm sound/HUD cue (id `0x32`), not a fuze detonation itself; the flag is only
   cleared, not itself checked for detonation, within this function. (The actual ground-impact
   detonation is a separate function — see "Explosive damage" below.)

**Homing/steering: `FUN_0040a254`.** Only runs when the rocket has a live target and (a) the
target hasn't switched controllers mid-flight in a way that invalidates the lock, and (b) the
target isn't "gone" (destroyed/stealthed — checked via two target-state bytes). Computes the
line-of-sight delta to a **lead-predicted target position** (`FUN_00492884`, a
position-extrapolation helper — not traced this session) rather than the target's raw current
position — i.e. rockets lead their target, they don't just chase the instantaneous position.
That delta is then, for non-network non-type-2 rockets, given a **deadband**: if the heading
error on an axis falls within `±0x1800` (6144) of zero but outside `±0xc00` (3072), it's pushed
out to exactly `±0xc00` — a "don't bother making tiny corrections, but also don't fully ignore
this axis" clamp, likely to avoid twitchy micro-corrections in the visual flight path. The
(possibly deadbanded) delta then goes through `FUN_004679d8` twice — the rate-limited
"move-toward" helper from the toolkit above — applied with a fixed max-turn-rate constant
`0x500` (1280) per axis per tick. This **is** the missile's turn-rate cap: `0x500` game-units of
heading change per tick, independent of rocket type. After steering, the (still fixed-point)
result is passed back through `FUN_00467820` and added into the stored position — i.e. the
"steering command" and "this tick's positional delta" share the same Q8/timestep-scaled pipeline
as straight-line motion.

**Non-homing variant: `FUN_0040a488`.** Simpler — takes the launcher's aim-point delta directly
(no deadband, no rate limiting), scales through `FUN_0047df94`(Q8)/`FUN_00467820`, and clears the
launcher's stored aim-point fields afterward (one-shot consumption, matching "ballistic rocket
uses the aim solution present at the moment it fires and then forgets it," vs. the homing
rocket's continuous re-targeting).

## `fire.cpp` — confirmed, but it's visual-effects setup, not projectile math

Only one function (`FUN_0046b0a4`, `0x0046b0a4`) carries a `fire.cpp` assert string, found via a
dedicated `ES2FindStringRefs` search this session (last session's keyword list never included
`"fire.cpp"`). Decompiling it shows it's a **muzzle-flash/fire-effect resource loader** — builds
filenames by appending an index (`0`, `1`, ...) to a base string, loads two effect
variants via `FUN_0045cdd8`, and wires the results into per-hardpoint pointers on every object in
a list (`DAT_006b4fac`, offset `+0x26`). This is asset/resource setup for the visual firing
effect, not the projectile-spawn or hit-resolution logic — those live in `rocket.cpp`/`bullet.cpp`
and the damage system below, not `fire.cpp` itself. Worth knowing so a future session doesn't
re-search `fire.cpp` expecting to find damage/hit math there.

## Terrain system: `HeightGrid`, `Terrain_LoadZone`, `Terrain_HeightQuery` — fully solved, byte-verified

**The `HeightGrid` struct (0x129 = 297 bytes, allocated by `HeightGrid_Constructor` @ `0046bdf8`,
installed as `ActiveHeightGrid` @ `004a0bf8` by `Terrain_LoadZone` @ `0042789c`):**

| Offset | Field | Meaning |
|---|---|---|
| `+0xec` | `int*` | Base pointer to the per-cell array (16 bytes/cell, row-major: `cellIndex = x + y*(1<<WidthShift)`) |
| `+0xf0` | `byte*` | Parallel per-cell scratch/flag byte array (`width*height` bytes), written but not yet traced to a consumer |
| `+0x100` | `int` | `WidthShift` — log2(grid width in cells) |
| `+0x104` | `int` | `HeightShift` — log2(grid height in cells) |
| `+0x108` | `int` | `CellShift` — log2(world-units per cell); also the shift used to convert world (x,y) → cell (x,y) |
| `+0x10c` | `int` | An LOD value derived at load time as `10 >> (CellShift-14)` (clamped, default 10) — not read by `Terrain_HeightQuery` itself, presumably a renderer/chunking parameter |
| `+0x110` | `int` | `HeightBase` — additive height offset (0 for real/binary zones; `MinHeight*8` for the ASCII debug format) |
| `+0x118` | `int` | `HeightScale` — multiplicative height scale applied to each cell's raw byte |
| `+0x11d` | `int` | Material/detail-type record count (from `dat\mat0`) |
| `+0x121` | `int*` | Pointer to the material/detail-type table, `count` × 8-byte records (`ZONES_MaterialTable`, from `Mat0ResourceName`/`dat\mat0`, confirmed against the real `ES2/VOL/simvol0/dat/MAT0.DAT`) |

**Per-cell record (16 bytes):**
- `+0x0` (byte): raw height value 0–255. World height = `rawByte * HeightScale + HeightBase`.
- `+0x1`..`+0xe` (14 bytes): not yet decoded (neither loader path examined writes anything here).
- `+0xf` (byte, bitfield): bits `[0:1]` = diagonal-split selector consumed by `Terrain_HeightQuery`'s barycentric interpolation (values `0`/`2` confirmed produced by the loaders; `1`/`3` are handled by the query but never observed being written); bits `[2:7]` = material/detail-type index into `ZONES_MaterialTable`, assigned via a weighted random roll (~30.6% chance per type, first match wins) at an LOD-driven block stride so neighboring cells within a block share one roll rather than each rolling independently.

**Loading pipeline, confirmed against real files in `ES2/VOL/ZONES.VOL`:**
1. `Terrain_LoadZone(zoneIndex)` builds the base name `zoneNNNN` (`ZoneFilenameTemplate`,
   `_itoa`-substituted) and reads a **16-byte per-zone header** resource at `dat\zoneNNNN`
   (`ZONES.VOL\DAT\ZONE*.DAT`, confirmed real, always exactly 16 bytes): four LE `int32`s —
   `[0] WidthShift` and `[1] HeightShift` (redundant pre-declarations, re-derived and overwritten
   from the bitmap itself later), `[2] CellShift`, `[3] HeightScale`. Verified byte-exact, e.g.
   `ZONE504.DAT` = `07 00 00 00 07 00 00 00 0E 00 00 00 95 00 00 00` → WidthShift=7, HeightShift=7
   (128×128 cells), CellShift=14, HeightScale=149.
2. `TerrainZone_LoadHeightmap` (`0046c650`) also loads the shared (not per-zone) material table
   from `dat\mat0` (confirmed = real retail `MAT0.DAT`), then opens `dba\zoneNNNN.dba`. If the
   resolved extension is `.dba` (`DbaExtensionLiteral`, every real zone), it goes through the
   generic `ClassItem_LoadResource` polymorphic loader — the same registry-dispatch architecture
   already confirmed for `.DFN`/`.HFN`/`.DCI` — into `TerrainZone_PopulateFromBitmap` (`0046c3c0`).
   Any other extension falls back to a plain `fopen`/`fscanf` ASCII format (`"%d %d %d %d"` header
   = WidthShift/HeightShift/MaxHeightRaw/MinHeightRaw, then one `%d` per cell) — almost certainly a
   level-design/debug-only path; no such loose files exist in retail data.
3. **`TerrainZone_PopulateFromBitmap` reveals that a zone's heightmap is literally an ordinary
   `DynamixBitmap` image** — the exact same 8-bit-indexed container format used for regular
   `.DBM`/`.DBA` textures elsewhere in this codebase (see `docs/formats/dfn-hfn-dci.md`). Each
   pixel byte (minus a small bias parameter) becomes one cell's raw height byte; `WidthShift`/
   `HeightShift` are re-derived from the bitmap's own width/height fields rather than trusting the
   zone header's copies. **Verified byte-exact against every real file in
   `ES2/VOL/ZONES.VOL/DBA/`:** 128×128 zones are exactly 16418 bytes (`128*128 + 34`-byte
   `DynamixBitmap` header) and 256×256 zones are exactly 65570 bytes (`256*256 + 34`) — both an
   exact match, and the zones that come out 256×256 are precisely the ones whose `.DAT` header
   declared `WidthShift=HeightShift=8` (e.g. `ZONE123.DAT`), confirming the redundant header fields
   really do track the bitmap dimensions.

`Terrain_HeightQuery(HeightGrid*, {x,y})` (`0046e07c`) converts a world `(x, y)` into a grid cell
via `CellShift`, fetches the enclosing cell's 4 corner texels from the 16-byte-per-cell array, and
— using each cell's `+0xf` diagonal-selector bits — does a proper barycentric/bilinear
interpolation across whichever triangle the query point falls in. A materially more sophisticated
terrain representation than a naive fixed-diagonal heightmap: each grid quad can independently
choose which way its diagonal split runs, chosen at terrain-authoring/compile time. Used by the
flyer terrain-avoidance autopilot below and by a rocket's ground-impact detonation check.

**Open items:** the 14 undecoded per-cell bytes (`+0x1`..`+0xe`); the parallel `+0xf0` scratch
array's consumer; the `+0x10c` LOD value's consumer (presumably the terrain renderer, not yet
located); and the exact path-join semantics of `FUN_00492ae0`/`FUN_00492a84` (medium-confidence
`maybe_` names — the resulting paths were confirmed against real files, but the string-concatenation
order wasn't independently proven byte-for-byte).

**Flyer ground-proximity/terrain-avoidance autopilot: `FUN_004198f4`.** Initially suspected (from
its 5 calls to the weapon-fire raycast, below) to be a generic weapon-fire dispatcher; decompiling
it shows otherwise. Six direction-flag bits from the flyer's type record (`local_c[0]`/`[4]`/`[5]`/
`[6]`/`[7]`/`[8]`, a per-type "which sensors does this airframe have" mask) each gate a probe in a
fixed direction (front/back/left-right/up/down offsets from static direction-vector tables), and
for each active probe: query terrain height via `FUN_0046e07c`, and/or raycast (below), and if the
probe is triggered, nudge the flyer's vertical-speed field (`+0xe`) away from the obstacle and
play a proximity-alarm tone (`FUN_00463010`/`FUN_00462878`/`FUN_004629c0`, sound id `0x2d`) whose
volume/pitch scales with distance via — once again — the fast-magnitude approximation
(`FUN_0047dd66`) from the shared math toolkit. Very likely `flyersys.cpp` code despite that file's
string having zero direct references (see "Open items" below) — the function exists and does real
work, it just doesn't happen to carry an assert tied to `__FILE__` in this build.

# The damage system

This is the combat-relevant heart of DBSIM: how a weapon's fire actually turns into a mech taking
damage. It has two genuinely different pathways — **direct fire** (deterministic, single
component, gated by shields) and **explosive/area-of-effect** (random, multi-component, distance
falloff, also gated by shields via a separate but parallel implementation) — that share a common
raycast entry point and converge on the same final health-writing primitive. Both are confirmed
against the official `Earthsiege 2 - On-Line Manual.pdf` (added to the repo root 2026-08-09; see
`reference_es2_manual` memory for how to read it — `pdftotext` via Git for Windows, poppler isn't
installed).

## The shared raycast: `FUN_00426528`

A generic ray-vs-live-object-list query, not weapon-specific — found to have exactly 5 call
sites: rocket per-tick homing (`FUN_0040a538`, above), bullet per-tick and burst-fire
(`FUN_0040b124`/`FUN_0040bf74`, below), and — initially mistaken for a weapon-fire dispatcher
because it's called from there 5 times — the flyer terrain-avoidance autopilot above
(`FUN_004198f4`). A single raycast primitive reused for weapon hit-scan **and** obstacle sensing
confirms it's a shared low-level utility rather than something owned by any one weapon-type
source file — most likely `objlist.cpp` (its use of the same global live-object list,
`DAT_004a9b7c`/`DAT_004a9b82`, as the confirmed-`objlist.cpp` functions at `0x004281b0`/
`0x004282f8` is the strongest available evidence; not confirmed by a direct assert-string tie,
since this function itself carries no embedded string). Confirmed **not** `fire.cpp` (checked
directly, only one function ties to that string, see above).

Walks the live-object list; for each candidate that passes team/state filtering, calls that
object's vtable method at `+0x20` — which, for a mech, is `FUN_00418ba8`, the direct-fire
hit-test-and-damage function described below. **The hit test and the damage application are the
same call** — there is no separate "apply damage" step visible from the caller's side, which is
why an earlier pass through this investigation couldn't find one: it was one level deeper than it
was looking. `FUN_00426528` also makes a second, unrelated vtable call per candidate (`+0x50`,
`FUN_0041f7b8`) which turned out to be AI threat-tracking ("this object just took fire, update who
it thinks is attacking it"), not damage — a dead end worth remembering so it isn't re-checked.

`bullet.cpp`'s per-tick and burst-fire functions, found by walking `FUN_00426528`'s other callers:
- **`FUN_0040b124`(instance) — per-bullet-instance tick.** Structurally parallel to the rocket's
  `FUN_0040a538` (periodic seeker-slot reacquire, age counter, lifetime-expiry check) but bullets
  age at a **fixed baked-in rate, `FUN_00467820(0x200)`**, not a per-type-record rate field like
  rockets. For bullet **type 9 specifically** (a distinct type index, not bullets as a class),
  there's a near-miss short-circuit before the raycast, and on a confirmed hit it calls the
  explosion function directly with a `4000`-unit blast radius — see "Explosive damage" below.
  Every other bullet type just calls the raycast and, if it returns a hit, marks itself for
  removal — the direct-fire damage already happened inside that raycast call.
- **`FUN_0040bf74`(type, origin/dir, distance, owner, angle) — fire-burst / tracer spawner.**
  Calls the raycast once up front to get the actual (possibly shortened) travel distance, then —
  if that distance exceeds 5000 game units — splits the visual tracer into multiple 5000-unit
  segments (`FUN_0040b804` spawns each), otherwise spawns one tracer for the whole distance. Pure
  rendering; the hit-distance math is already resolved by the raycast call at the top.

## Direct-fire damage: armor-then-part, deterministic, shield-gated

**`FUN_00418ba8` (mech vtable `+0x20`), called by `FUN_00426528` on every raycast candidate.** In
order:

1. **Range/geometry check.** Transforms the shot into the mech's local space and rejects it if
   outside the mech's hit-cylinder (a per-type radius, `typeRecord+0x1a`).
2. **Shield absorption — `FUN_00413cc4`.** Picks the front or rear shield zone by which side of
   the mech was hit, then: `absorbed = min(incomingDamage, remainingShieldInZone)`; both the
   incoming damage and the zone's remaining charge are reduced by that amount. This is a **hard
   cap, not an all-or-nothing threshold** — a hit whose damage exceeds what's left in that zone
   drains it to zero and carries its excess straight through in the same hit; the zone doesn't
   need to already be empty beforehand. If the shot is fully absorbed, the function returns "no
   penetration" and the caller only spawns a visual hit-spark effect. See "The shield system"
   below for full details, confirmed against the manual.
3. **Component selection — `FUN_0040c9d4` → `FUN_0040c8fc` (per candidate) → `FUN_0040c8c8`
   (fine geometry test).** Only reached if some damage penetrated shields. Iterates the mech's up
   to 29 component slots (see "The component/health system" below), and for each occupied one,
   does a proper geometric ray-vs-subshape test (coarse part, then fine sub-piece within it) to
   find the ONE specific component actually struck — **not** a random roll, unlike the explosion
   path. Tracks a "best match so far" and returns that single component's index.
4. **Damage application — `FUN_004188c8`.** Applies a **per-weapon-type damage multiplier**
   (`FUN_0047dfa4(shotData[+8], remainingDamage)`, Q8) — direct code evidence of the
   weapon-vs-target effectiveness the manual describes (see "Weapon-type effectiveness" below).
   **Splits** the (multiplier-scaled) damage: a portion goes toward destroying the specific weapon
   mount at that location if one is present (which, if it fails, can trigger a secondary
   small-radius explosion via the mech's own `+0x70` vtable slot — the same function the AoE path
   uses, i.e. a destroyed weapon mount can itself explode and splash nearby components), and the
   remainder goes to that component's general health via the mech's `+0x74` vtable slot
   (`FUN_00417de4`, described below). Health is bucketed into 8 levels (`>>5` of the 0–256 Q8
   percentage) for state-transition/alert purposes, plausibly matching the manual's 5-color status
   system (Green/Yellow/Orange/Red/Gray).

**This is fundamentally different in shape from the explosion path**: one deterministic component
per hit vs. many randomly-rolled ones; no distance-falloff curve (a shot either penetrates shields
and hits its one component, or it doesn't — there's no "closer components take more damage").
This matches expectations well: precisely-aimed weapons hit what you aimed at; explosions spray
damage around imprecisely.

**Not yet confirmed as the beam-weapon (Laser/PBW/ELF) path specifically** — only as the general
path anything routed through `FUN_00426528`'s raycast uses. `FUN_00418ba8` has no direct literal
callers besides its own vtable-slot data reference (checked via `ES2FindAddressRefs` on its entry
address) — it's exclusively invoked polymorphically as `obj[+0x20](...)`, so a beam weapon calling
this same virtual method directly on its locked target (skipping the whole-object-list scan, since
it already knows its target) wouldn't show up as a distinct literal caller. Confirming that would
need a search for indirect calls through vtable offset `+0x20` specifically, regardless of which
object's vtable — no existing script supports that (everything so far searches for calls to named
functions or literal addresses, not calls through a specific *offset*). Still, the mechanism
itself — test hit and apply damage as one call, on a single known target — is exactly the shape a
beam weapon would want, so this is a strong candidate, not just a guess.

## Explosive damage: blast sweep, random per-component roll, distance falloff, shield-gated

**`FUN_00426a20` — the area-of-effect sweep.** Walks the live-object list, skips inactive objects
and the excluded object (`param_5`), and for every other live object: computes its hit-radius via
a vtable call (`obj+0x5c`), computes distance from the impact point, and if
`distance − hitRadius < blastRadiusParam`, calls that object's `+0x70` vtable method — for a mech,
`FUN_004187d0` — with `(weaponType, impactPos, blastRadius, extra)`.

This function has exactly **3 call sites in all of DBSIM.EXE**, all genuinely explosive/terminal
events, not routine weapon fire:
1. **`FUN_00409d2c`** — a projectile's ground-impact handler: checks altitude against terrain
   height every tick via `FUN_0046e07c`, and the instant it dips below ground, detonates —
   `FUN_00426a20(pos, 3000, 10000, 0, null)`. A missile exploding on ground impact.
2. **`FUN_0040b124`, only inside the bullet `type == 9` branch** — a distinct bullet subtype, not
   bullets as a class, calling `FUN_00426a20(pos, 4000, ..., owner, null)` on a hit. Per the
   user's weapon-taxonomy notes (energy weapons: Lasers/PBW/ELF beam instantly; EMP/Plasma/
   Missiles/Autocannons are projectiles; only Missiles and the Plasma cannon splash), **type 9 is
   very plausibly the Plasma cannon** — a slow-moving energy projectile that (per the manual)
   "fire tracks to nearby targets" and does splash damage, unlike ordinary bullets. Not proven by
   a direct string tie, but the taxonomy match is strong.
3. **`FUN_0041e48e`** — a mech's own death/destruction handler: once confirmed dead, it drops to
   the ground, triggers one `FUN_00426a20(pos, 3000, 2000, 0, self)` (a death explosion that can
   splash nearby objects), then unconditionally slams **every one of its own remaining
   components with a flat 32000 damage** via direct `+0x74` calls — no random roll, no falloff;
   this part is guaranteed-destruction cleanup, not the live-combat formula.

**`FUN_004187d0(this, weaponType, hitPos, blastRadius, extra)` — the per-mech AoE damage
formula.** In order:
1. Computes the angular difference between the mech's facing and the direction to the hit point,
   classifying it front (`< 0x4000`, ±90° in BAM units) or rear.
2. **Shield absorption — `FUN_00413c68`, the explosion path's own separate implementation of
   the same concept `FUN_00413cc4` implements for direct fire.** Picks the front or rear shield
   value by the classification above, computes `scaledDamage = (weaponDamage × 1000) >> 8` (a
   different scaling constant than the direct-fire path — a genuinely separate piece of code, not
   a shared subroutine), subtracts it from that shield zone, and if the zone goes negative,
   clamps it to 0 and returns a scaled overflow amount (`overflow × 0x400 / 1000`); otherwise
   returns 0 (fully absorbed, no structural damage). **This was originally misread as a "hit-zone
   damage-budget lookup table"** — it isn't a table at all, it's shield math, and confirms shields
   gate both damage pathways, matching the manual: "shields cause missiles to explode on contact,
   preventing most of their blast power from reaching the HERC's armor."
3. If shield overflow is positive, **iterates up to 29 component slots** (the same indexing space
   the direct-fire path's component selection uses — see below), and for each occupied slot: rolls
   a per-component random chance (`FUN_00492dd4(...) & 0xfff < 0x802`, ≈51%) to even be considered
   for this hit — splash damage doesn't hit every component in range, each independently rolls.
   For components that pass, gets that component's world position (`this+0x58` vtable call),
   computes distance from the hit point, and if within `blastRadius`, applies damage via the
   mech's `+0x74` vtable slot (the same `FUN_00417de4` the direct-fire path's final step uses)
   with:
   ```
   amount = shieldOverflow × (blastRadius − distance) / blastRadius
   ```
   **Linear distance falloff** — full overflow amount at zero distance, scaling to zero exactly at
   `blastRadius`.

**Both pathways converge on the same final health-writing function, `FUN_00417de4`** — they
differ in shield-absorption implementation (parallel but separate code), in how many/which
components get selected (one deterministic vs. many random), and in whether there's a
distance-falloff curve. A faithful port needs both kept as genuinely separate systems, not one
generalized formula with different parameters — using the AoE formula for a laser would make it
behave like a mini-explosion (random near-misses on adjacent components) instead of a precise
hit, and using the direct-fire formula for a missile would make its splash radius meaningless.

## The shield system

Confirmed against the manual's own words: "Your HERC is protected by front and rear shields...
Shield power is depleted by enemy fire, and replenishes at a steady rate from the Master Energy
Pool... Shield power is normally distributed evenly front and back, but you can change this
balance manually... To redistribute shield power, click the respective shield symbol, or press
`[` (for rear) or `]` (for forward)." Also: "Shields are powered from the onboard fusion
generator... The bigger the HERC's generator, the stronger the shield," and "shields cause
missiles to explode on contact, preventing most of their blast power from reaching the HERC's
armor" — matching the two separate shield-absorption implementations found in both damage
pathways above.

**Struct layout, confirmed from the mech constructor (`FUN_00415bb0`) and the shield initializer
it calls (`FUN_00413a90`):** five consecutive `short` fields starting at `this+0x222`:

| Offset  | Field                          |
|---------|--------------------------------|
| `+0x222`| current front shield charge    |
| `+0x224`| current rear shield charge     |
| `+0x226`| balance setting (`0x200` = default center) |
| `+0x228`| front max capacity (= per-side potential if fully allocated) |
| `+0x22a`| rear max capacity (same)       |

`FUN_00413a90(shieldFields, baseValue)` — called at construction with `baseValue =
typeRecord[0xc0]` (confirming max shield capacity is a **per-mech-type stat**, matching "the
bigger the generator, the stronger the shield" — different HERC classes have different base
values in their static type data) — initializes `+0x228`/`+0x22a` to `baseValue` each (each side's
max *if fully allocated to it*) and `+0x222`/`+0x224` to `baseValue >> 1` each (the default 50/50
split of one pool of size `baseValue`). This reconciles with the manual's "100/200" display
numbers exactly: if the displayed percentage is `currentSideCharge / (totalCapacity/2) × 100`,
the default split (`baseValue/2` per side, half-of-total also `baseValue/2`) reads as 100%/100%,
and fully shifting to front (`front = baseValue`, still dividing by the same `baseValue/2`
reference) reads as 200%/0% — matching the manual's own example precisely. `+0x226` (`0x200` at
init) is very likely the adjustable balance value itself, though the code that reads player
balance-adjustment input and redistributes `+0x222`/`+0x224` accordingly wasn't located this
session.

**Getter:** `FUN_004154d0` (mech vtable `+0x34`) — given a heading angle, returns `+0x222` if
within ±90° of front, else `+0x224`. Confirms the same front/rear split used by both damage
pathways' absorption steps.

**Confirmed answer to a question the user raised, about whether damage bleeds through a depleted
shield only at exactly zero charge or partially at low charge:** it's the latter. Both absorption
implementations (`FUN_00413cc4` for direct fire, `FUN_00413c68` for explosions) are a hard cap —
`min(damage, remainingCharge)` — not a threshold gate. A single hit whose damage exceeds what's
left in a shield zone drains it to zero and its excess carries through in that same hit, even if
the zone wasn't already empty before the shot landed.

**Solved (2026-08-09, continuation session): the recharge tick and the balance-adjustment input
handler are both found, and they're two separate code paths, not one.** The earlier vtable-slot
guessing (both ruled-out candidates above) was the wrong approach — neither is a method the mech
calls on itself. The real answer was found by scanning every DBSIM function's decompiled text for
the literal struct-offset patterns `0x222`/`0x224`/`0x226` (a new headless script,
`ES2FindImmediateRefs.java`, decompiles every function and greps the output — necessary because
these fields are accessed through a passed-in pointer, not a fixed data address, so the existing
address-xref script doesn't apply). That surfaced a small, previously-unexamined cluster of
functions at `0x004139xx`–`0x0041aaxx` that all take the shield-struct pointer (`this+0x222`)
directly as an argument.

**The recharge tick — runs every tick, for every mech, from the main simulation loop:**
- **`FUN_0045f464`** is DBSIM's per-frame simulation tick — it updates the global timestep
  (`DAT_004d3be8`) from a hardware/timer source, walks every global object list (rockets, bullets,
  debris, etc.) via repeated `FUN_00471b64` iteration, and — the relevant part — walks the global
  mech list (`DAT_004a9bfe`) calling **`FUN_0041aa5c(mech)` once per live mech**, skipping any
  mech with a "removed" flag (`+0x14`) or a "already dead" state byte (`+0x99`). This confirms
  `FUN_0041aa5c` is a genuine per-mech-per-tick systems update, not a UI or AI-only thing.
- **`FUN_0041aa5c`** does reactor/energy bookkeeping first: `FUN_00467820(*(short*)(this+0x256))`
  integrates a per-mech-type reactor output *rate* over one tick and accumulates it into an energy
  pool tracker at `this+0x292` (a strong match for the manual's "Master Energy Pool"). It then
  calls a vtable method on the mech's own weapon-mount-manager object (`*(this+0x202)` —
  see the correction below) passing `(energyPool − 500, this)`, which is presumably weapon-energy
  arbitration (how much of the pool weapons claim first); the *returned* value is the amount left
  over for shields, and is fed straight into:
- **`FUN_00413b38(shieldStructPtr, requestedAmount)`** — the actual recharge primitive. Clamps the
  request to **at most 5 units per tick**, then further clamps it to `frontMax − (frontCurrent +
  rearCurrent)` (the total deficit, i.e. never overcharges past full). Computes the new total
  charge, then re-derives the front share via `Q8mul(balance, newTotal)` and slews the front value
  toward that target using the shared rate-limiter `FUN_004679d8` (max step `0x41`/tick normally,
  `10000`/tick — effectively instant — if the deficit is negative, i.e. snapping back down from an
  over-full state). The rear value is set to make up the remainder. Returns the *unclaimed* portion
  of the request, which `FUN_0041aa5c` folds back into the energy pool. This is a clean, complete
  account of "shields recharge steadily from the reactor, redistributed by balance" — the 5-unit/
  tick cap is the concrete recharge-rate constant.
- **Correction while tracing this: `this+0x202` is a pointer to a separately-allocated
  weapon-mount-manager object with its own vtable — not inline weapon-mount data as previously
  assumed.** Seen directly in `FUN_004175dc` (the mech loadout-(re)configuration function, called
  on spawn/equip changes): `*(int**)(this+0x202) = malloc(0x14 or 0x35 bytes, depending on the
  `this+0xa3` "locally-simulated" flag)`, and thereafter accessed via `(**(vtable)(*(this+0x202)))`
  virtual calls, e.g. the energy-arbitration call above. `this+0x20e`'s per-slot indices (weapon
  mount active flags) are a *different*, already-correctly-documented array — this correction is
  specifically about `+0x202` itself being a pointer-to-object, not a raw array.

**Balance-adjustment input — runs every tick, only for the player's own mech, from a separate
per-frame UI/cockpit update:**
- **`FUN_0041b130(DAT_004d256a)`** (`DAT_004d256a` is the confirmed global "this instance's own
  locally-piloted mech" pointer — set in the level-init function `FUN_004614fc` by scanning the
  mech list for the one with `this+0xa3` set) is called once per frame from the main-loop function
  `FUN_0045fb9c`, and does a large amount of cockpit/HUD-adjacent bookkeeping (target tracking,
  network-state syncing, HUD field updates). Among other things it calls:
- **`FUN_00413bc8(shieldStructPtr)`** — reads a UI/input widget's state via
  `FUN_004438e0(*(int*)(DAT_0049b088+0x1e9))` (a singleton UI-context lookup, `0x1e9` slot — very
  likely the shield-balance HUD gauge widget itself, one of the `*Gauge` classes catalogued in
  [[project-es2-exe-recon]]'s class-name recon), copies out two flag bytes, and if either is set
  calls `FUN_00413af8(shieldStructPtr, direction)` — clearing the flag after handling it (so it
  fires once per press/hold-tick, not continuously). It finishes by computing front/rear display
  percentages (`(current<<10)/max`) and pushing them back to the same widget via `FUN_00443858` —
  i.e. this one function both reads the balance-adjust input *and* drives the gauge's own display,
  matching a single interactive HUD gauge object that owns both roles.
- **`FUN_00413af8(shieldStructPtr, direction)`** — the actual balance nudge: adds `±0x66` (102) to
  the balance field (`shieldStructPtr+4`, i.e. `this+0x226`), clamped to `[0, 0x400]` (0–1024).
  This confirms the balance field's real range is 0–1024 with `0x200` (512) as the documented
  default center — matches the ctor's `0x200` init value exactly (half of `0x400`) — and gives the
  concrete per-tick adjustment rate (102/1024 ≈ 10% of the full range per tick while a balance key
  is held).

**Net picture:** shield recharge is a background per-tick trickle shared by every mech (AI and
player alike) driven off the reactor's energy pool; balance adjustment is purely a player-input
concern layered on top, only ever touching the `balance` field itself, which the recharge tick
then reads back via `FUN_0047dfa4(balance, newTotal)` on the very next tick. The two systems don't
call each other directly — they're connected only through the shared struct fields.

## The component/health system

**Correction (2026-08-09, continuation session): `this+0x206` is a *header* of pointers, not
inline arrays as previously documented — found by finally decompiling the allocator itself,
`FUN_0040d2cc`, instead of inferring its layout from call shape.** Its real body:
```c
*param_1     = malloc(param_3*2); memset(*param_1, 0, param_3*2);   // pointer, zeroed
param_1[1]   = malloc(param_2*2); memset(param_1[1], 0, param_2*2); // pointer, zeroed
param_1[2]   = malloc(param_2*2); memset(param_1[2], 1, param_2*2); // pointer, filled with 0x01
*(short*)(param_1+6)         = param_2;  // count = 29
*(short*)((int)param_1+0x1a) = param_3;  // count = 22
```
called as `FUN_0040d2cc(this+0x206, 0x1d /*29*/, 0x16 /*22*/)`. So the header at `this+0x206`
actually holds:

| Offset (abs) | Field |
|---|---|
| `+0x206` | **pointer** to a 22-`short` dependent/sub-piece health array, zeroed |
| `+0x20a` | **pointer** to a 29-`short` main-component health array, zeroed |
| `+0x20e` | **pointer** to a 29-`short` active/occupancy-flag array, all bytes `0x01` at init |
| `+0x21e` | `short` count = 29 |
| `+0x220` | `short` count = 22 |

This explains (rather than merely describes) why every accessor
(`FUN_0040dbc0`/`FUN_0040da38`/`FUN_00417de4`/`FUN_00417bec`/…) treats `this+0x206` as `(int*)`
and does an *extra* pointer dereference before indexing — they were always reading a pointer
field correctly, the earlier doc pass just mis-described what was being pointed to. Semantics are
otherwise unchanged from the original writeup:
- The 22-entry array = current health for **fine sub-piece / dependent** components (see the
  aggregation formula below).
- The 29-entry array = current health for the **main component slots**, the same indexing space
  both damage pathways' component selection uses (direct-fire's deterministic pick, and the AoE
  formula's iteration bound — the "up to 29 parts" in both places is the same array).
- The 29-entry flag array = **occupancy/active flag per component slot**, not a second depleting
  health pool. Zeroed for a specific slot when that component (typically a weapon mount) is
  destroyed.

**New this session: two of the 22 dependent-array slots are individually named as the mech's
front leg pair.** `FUN_00417de4` (the shared health-write/cascade endpoint, `this+0x74]`) reads
dependent-array indices **0 and 1** by literal offset (not a loop) into `local_48[0]`/`[1]`, and —
only for mech types whose leg count (`typeRecord+0x4a`) is `4` — *also* reads indices **10 and
11** (byte offsets `0x14`/`0x16`) into `local_48[2]`/`[3]`, averaging the two pairs together
before comparing against thresholds that gate a "walk" vs. "crippled" vs. "destroyed-legs" status
and (for the player's own mech) an alert sound/HUD flag. This is a concrete, disassembly-grounded
index mapping: **dependent-array slots {0,1} are the front leg pair, {10,11} are the rear leg pair
on four-legged mech types.** A `local_3e >> 1 <= (count of slots reading exactly 0x100/full-health
loss)` check triggers the mech's "can't stand" state, distinct from the final all-parts-destroyed
kill check further down the same function. This doesn't resolve the full 29-slot
Structural/Internal/Weaponry mapping (see the dedicated section below), but it's the first
confirmed per-index semantic label found in this array.

`FUN_0040d354(this+0x206, mechThis, damageDataPtr, collisionRegistration)` — a second constructor
call — wires up `this+0x212` (and neighboring fields) as a pointer into per-component **maximum/
reference** data (sourced from the mech's own `damage.dat`-derived pointer plus its collision
registration record from `FUN_0040cd88`, tying this system directly to the collision bounding-
sphere tree documented above and to last session's `damage.dat` hit-zone table finding).

**Read: `FUN_0040dbc0(this+0x206, componentIndex)` — current/max percentage, in Q8 (0–256).**
Looks up the component's max-reference record (18 bytes, via `this+0x212`), starts with its own
current (main 29-entry array, reached via the `this+0x206` header's `+4` pointer, index) and max
values, then **aggregates in every dependent sub-component** listed in that record (walking a
list, adding each dependent's current value from the 22-entry dependent array — reached via the
header's `+0` pointer — and max value from a parallel max-side array) before computing
`(totalCurrent << 8) / totalMax`. So a single displayed component's health can be the aggregate of
several finer sub-parts — e.g. plausibly a "leg" reading as the combination of the leg proper plus
whatever finer actuator/joint pieces are modeled underneath it, though the exact sub-piece
breakdown per component wasn't traced this session.

**Write and cascade: `FUN_0040da38(this+0x206, componentIndex, damage)`**, called from
`FUN_00417de4` (mech vtable `+0x74`, the shared endpoint both damage pathways call into).
Subtracts damage from that component's current value (via `FUN_0040d3ec`), and if the component's
state crosses into "destroyed" (a flag bit in its record), cascades: calls `FUN_0040d434` on that
component, then walks a **dependency list** (`DAT_00498864`) calling `FUN_0040d434` on every
dependent — i.e. destroying one component can automatically destroy things that depend on it
(plausibly a leg's foot, or a weapon mounted on a destroyed limb).

**The 18-byte record's previously-unread bytes decoded (2026-08-09, continuation session,
targeting the Structural/Internal/Weaponry mapping) — full layout, from `FUN_0040dbc0` (read),
`FUN_0040da38`/`FUN_0040d434` (write/destroy), and the original loader `FUN_0040cff8`:**

| Offset | Field | Evidence |
|---|---|---|
| `+0x00` | `short` max health | `FUN_0040dbc0`'s `local_10 = *psVar5` — corrects an earlier guess ("minAngle") that was written before this record's actual consumer had been decompiled |
| `+0x02` | `signed char` — **an index into a lookup table (`FUN_004089bc`), sentinel `-1` = none** | `FUN_0040d434`: `if (*(char*)(iVar5+2) == -1) {...default...} else {local_8e[0] = *(char*)(iVar5+2); FUN_004089bc(local_8e); ...}` on destruction, selecting a specific debris/effect variant |
| `+0x03` | `signed char` — **an index into a slot array on the mech itself** (`*(int*)(mechThis+0x34)+8`, byte `[index]` written `=2` — a state-transition write, matching the same "2" state code seen elsewhere for a damaged/destroyed HUD slot) | `FUN_0040d434`, guarded by `-1 < *(char*)(iVar5+3)` (i.e. `-1` = "no HUD slot") |
| `+0x04` | `char` — read as a plain equality compare (`(char)(iVar2) == param_2`, i.e. "which higher-level group index does this component belong to") — used to find *all* components sharing a group when cascading destruction | `FUN_0040d434`'s trailing loop: `if (... (char)(*(int*)param_1[3] + iVar2*0x12 + 4) == param_2) ...` |
| `+0x05` | `byte` bitfield: bit0=has-dependents-to-cascade (already documented), bit1=alt-effect-mode, bit2=one-shot "major alert already fired" flag, bit3=trigger-secondary-effect | `FUN_0040da38`/`FUN_0040d434`, multiple `& 1`/`& 2`/`& 4`/`& 8` checks |
| `+0x06` | `short` dependent sub-component count | `FUN_0040cff8` (loader), `FUN_0040dbc0`'s loop bound |
| `+0x08` | `int` pointer to the dependent list (4 bytes/entry: index at sub-offset `+2`) | `FUN_0040cff8`/`FUN_0040dbc0` |
| `+0x0c` | `short` sentinel `0xffff` (runtime-only, not file-loaded) | `FUN_0040cff8` |
| `+0x0e` | `int` zero (runtime-only) | `FUN_0040cff8` |

**Fully resolved — this record is `.DMG`'s `HercPiece`, already independently decoded by the
original Java author, and the Structural/Internal/Weaponry question turned out not to need a
category tag at all.** Checking the C# port before chasing `+0x03`/`+0x04` further paid off
exactly like `PROJ.DAT` did: `HercWorks.Core.Data.File.Dbsim.HercSimDamage.HercPiece` is this
exact 18-byte record (`Armor`=offset 0, `DebrisFlags`=offsets 2-3 as one `short`, `BoneId`=offset
4, an until-now-unresolved `Unk_val`=offset 5, `MappedInternals`=the offset-6/8 dependent list),
loaded from `dmg\[herc].DMG` — confirmed as the same file DBSIM opens at runtime by tracing
`FUN_0040d160`'s caller (`FUN_00415bb0`, the mech constructor): it builds the filename from the
mech's own name string plus an extension, matching `dmg\[herc].DMG`'s naming exactly. The Java
author's own doc comment on `HercSimDamage.cs` already lists real component names in array order —
`COCKPIT/FRONT`, `COCKPIT/REAR`, `SHOULDER/LEFT`, `SHOULDER/RIGHT`, `WEPN_BRACK/LEFT`,
`WEPN_BRACK/RIGHT`, `TORSO`, `LEG/LEFT/UPPER`, `LEG/RIGHT/UPPER`, ... — which **directly confirms
two things found independently this session**: `WEPN_BRACK/LEFT`/`RIGHT` (indices 4-5) are
genuine weapon-mount slots inside this same 29-entry array, and `COCKPIT/FRONT`/`REAR` (indices
0-1) are exactly the two components this session found individually checked (`FUN_0040d9f8`) as
the mech's death-trigger gate.

**This settles the Structural/Internal/Weaponry question — it isn't a 3-way partition of one
index space, it's three different mechanisms:**
- **Structural** = most of the 29-slot `HercPiece` array — the named body pieces (torso, legs,
  feet, shoulders).
- **Weaponry** = a *subset of that same array*, distinguished only by name/position
  (`WEPN_BRACK/LEFT`/`RIGHT`), not a separate array or index range. Weapon-specific runtime state
  (ammo, heat) lives elsewhere, in DBSIM's weapon-mount-manager object (`this+0x202`), not in this
  health record.
- **Internal** = a *wholly separate*, smaller table, `HercInternals` (Left/Right Leg Servos,
  Sensor Array, Targeting Computer, Shield Generator, Engine, Hydraulics, Stabilizers, Life
  Support, Pilot) — reached *probabilistically* through a struck structural piece's own
  `MappedInternals`/`CritChance` list, not directly targetable. An Internal system has no health
  slot of its own in the 29-component array; damaging it is a chance-based side effect of hitting
  whichever structural piece maps to it.

The offset `+0x05` byte (`Unk_val`, previously unresolved in the C# port) is now also resolved via
this session's tracing of `FUN_0040da38`/`FUN_0040d434` — a bitfield: bit 0 = has dependents to
cascade-destroy, bit 1 = selects an alternate destruction-effect mode, bit 2 = a one-shot "major
alert already fired" latch, bit 3 = triggers a secondary effect callback. Renamed to
`DestructionFlags` in the C# port with a doc comment recording this. `+0x02` (an index into a
debris/effect lookup, `FUN_004089bc`, sentinel `-1`=none) is very likely `DebrisFlags`'s low byte;
`+0x03` (an index into a slot array on the mech itself, guarded the same way) is very likely
`DebrisFlags`'s high byte — both already captured as one `short` field in the existing model, just
not previously broken down bit-by-bit; not re-split into two properties this session since the
existing `DebrisFlags` grouping is still accurate to what the file contains. `+0x04` (`BoneId`) is
confirmed by this session's disassembly to double as a cascade-destruction group key (destroying
one piece finds and destroys every other piece sharing the same `BoneId`), matching its name — a
piece's mounting bone and its destruction-dependency group are the same concept.

**`FUN_00417de4` itself**, beyond wrapping the write above, does per-subsystem percentage
tracking with 8-level bucketing and fires distinct alert sounds at multiple thresholds (~55%,
~31%, fully destroyed). It specifically tracks a **leg/limb subset** (count read from the mech's
own type record) in pairs, and — the mech-death trigger — **if enough limbs are fully destroyed,
kills the mech outright**: clears its target, calls a destruction handler, sets flags, and
finishes off remaining components via a recursive self-call with a flat 30000 damage. It also
processes weapon-mount ratios (from `this+0x202`, a pointer to the weapon-mount-manager object —
see "Weapon mounts" below and the correction in "The shield system" above) and a distinct
"torso"-like aggregate with its own thresholds (75%/50%). **New this session: two of the leg-pair
indices in this check are now confirmed by literal offset — dependent-array slots `{0,1}` (front
legs) and, for four-legged mech types only, `{10,11}` (rear legs) — see the correction block above
under "The component/health system."**

## Weapon-type effectiveness

The manual describes clearly differentiated weapon-vs-defense effectiveness: "Two weapons are
effective against shields: EMP cannons disrupt the shield matrix, and the ELF is so incredibly
powerful that it punches through shields as if they are not even there," "energy weapons... are
effective against shields... Projectile weapons have longer range and do more damage to enemy
armor, but have little effect on a target with shields," Lasers have "limited effectiveness
against shields," ATCs are "fast and hard enough to penetrate most armor plating."

**Confirmed in code:** `FUN_004188c8` (part of the direct-fire chain, step 4 above) applies
`FUN_0047dfa4(shotData[+8], remainingDamage)` — a Q8 multiply by a value carried in the shot's own
data — before splitting/applying damage to the selected component. **Not found in the
shield-absorption step itself** (`FUN_00413cc4` does a flat, unscaled subtraction with no
weapon-type field referenced) — resolved this session, see below: the differentiation happens
*before* the shot record is built, not inside either absorption/apply function.

**Fully solved (2026-08-09, continuation, second pass): identified the exact on-disk file, and
verified the field semantics against real weapon numbers — this is the mechanism behind the
manual's shields/armor effectiveness differences.** Traced by reading `FUN_0040bf74` (bullet
burst/tracer spawn) in full, where the shot descriptor (`shotData`, the same struct
`FUN_00418ba8`/`FUN_004188c8` consume) is built right before the `FUN_00426528` raycast call:
```c
psVar1 = FUN_0040ffc8(4, param_1);                   // look up this bullet's weapon-type record
shotData.field_0x04 = Q8mul(shotPower, psVar1[3]);   // -> shotData+4, read by FUN_004188c8 (structure/armor damage)
shotData.field_0x06 = Q8mul(shotPower, psVar1[2]);   // -> shotData+6, read by FUN_00418ba8 (fed into shields)
shotData.field_0x08 = psVar1[4];                     // -> shotData+8, a further per-hit scaling factor, see below
shotData.field_0x0a = psVar1 + 6;                    // pointer into the record's effect/sound data
shotData.field_0x12 = 5;                             // a "weapon category" tag, see beam-weapon section below
```
`psVar1` is **`PROJ.DAT`, the already-known, already-partially-ported sim-side weapon-damage
table** (`HercWorks.Core.Data.File.Dat.Sim.ProjectileData`) — not a new, unidentified file as
first thought. Confirmed two ways: (1) the loader (`FUN_0040fc8c`, below) opens a resource by the
literal name `"proj"`, matching the file's own real name exactly; (2) `psVar1[2]`/`psVar1[3]` land
on **exactly** the byte offsets the Java-ported `ProjectileData.Projectile.DamageShield`/
`DamageArmor` fields already used — two completely independent reverse-engineering efforts (the
original Java author's, and this session's from-scratch DBSIM disassembly) arrived at the same
36-byte record layout and the same two field positions without either one referencing the other.
Cross-checked against the real retail `ES2\VOL\simvol0\dat\PROJ.DAT` (984 bytes: 9-byte VOL
prefix + `[Total:u16=27][27×36-byte records]` + 1 trailing marker byte, decoded directly): the
values line up cleanly with the manual's fiction —
- Entries with `DamageShield ≫ DamageArmor` (e.g. 2000/400, 8000/2000) — EMP-shaped, matching
  "EMP cannons disrupt the shield matrix."
- Entries with `DamageArmor ≫ DamageShield` (e.g. 400/1600, 3000/7200) — ordinary
  Autocannon-shaped, matching "projectile weapons... do more damage to enemy armor, but have
  little effect on a target with shields."
- Several `DamageShield ≥ DamageArmor` entries with `Speed=0` (no projectile travel time) —
  beam-shaped, matching energy weapons' shield effectiveness.
- The first 3 entries (steadily increasing 60/360, 120/480, 180/600, `Speed=5000`) match the
  doc comment's existing ATC20/35/50 progression exactly.

This resolves the multiplier semantics too: **`DamageShield`/`DamageArmor` are the weapon's own
base damage stats against each defense type, not abstract multipliers** — the value scaled
against them (`shotPower` above, `param_5` in the raw disassembly) is the *shot's* own power/
charge level (Q8), not a "raw damage" the file further adjusts. This is a cleaner, better-founded
reading than this session's first pass at this section, which described it backwards (as if a
single raw damage number were being split by the file's fields).

**A second real finding this session: `Unk2_val` (short-index 4, `shotData+8`) is now resolved —
a per-weapon splash/secondary-explosion trigger, not a third damage-type multiplier.** Traced its
consumer, `FUN_004188c8`, precisely:
```c
uVar1 = Q8mul(shotData+8 /*SplashFactor*/, shotData+4 /*armor-scaled damage*/);
call obj[+0x74](obj, part, armorDamage - uVar1, ...);      // general component health takes the REMAINDER
if (uVar1 != 0) call obj[+0x70](obj, uVar1, ..., blastRadius=500, ...);  // secondary explosion, same formula explosive weapons use
```
So this field is a Q8 **fraction of the already shield-absorbed armor damage** diverted into a
small (`500`-unit-radius) secondary explosion — reusing the exact same blast-sweep formula
documented under "Explosive damage" above — instead of applying straight to the struck
component's health. Zero (the value for most real weapons) means no secondary explosion at all,
the guard (`if (uVar1 != 0)`) skips it entirely and the full armor-damage amount goes straight to
health. Real nonzero values (`500` or `1000`) appear scattered across several weapons, most
consistently for one whole weapon family (uniform `DamageShield==DamageArmor`, unlike every other
family) — a plausible match for the manual's Electron Flux description, though not proven to be
ELF specifically. Renamed in the C# port (`ProjectileData.Projectile.SplashFactor`, was
`Unk2_val`) with a doc comment recording this finding — a previously-unknown field in the
Java-ported model, closed by this session's DBSIM disassembly.

**The loader, for completeness:** `FUN_0040fc8c` — a function an earlier session flagged as a
"zero-callers dead end" near the `"weapons"` string and never actually decompiled on that basis —
opens a resource named `"wpntex"` (textures), then `"mechwpn2"`, then `"weapons"` (reads a count +
array of **88-byte** records into a separate, not-yet-traced structure — plausibly a per-hardpoint
mount-template table, given the name), then opens **`"proj"`** and reads its count + 36-byte
records in one flat read into `DAT_004a9980`, later linear-searched by `FUN_0040ffc8(category,
subtypeId)` — i.e. `PROJ.DAT`'s in-memory copy is keyed by a `(category, id)` pair matching this
session's `Projectile.Type`/`MissileId` fields, not by flat array index.

**Third pass (2026-08-09, same continuation session): `Type` is a firing-mechanism selector, not a
raw weapon-flavor tag — traced all 5 callers of `FUN_0040ffc8` and both callers of `FUN_0040bf74`,
which fully resolves the beam-weapon question (see the dedicated section below) and substantially
sharpens the `Type`-to-weapon mapping.** Each caller hardcodes a *literal* category constant, and
each corresponds to a genuinely different projectile *class* (different vtable, different
construction), not just a cosmetic label:

| `Type` | Constructor | Object kind | Real `PROJ.DAT` shape |
|---|---|---|---|
| `0` | `FUN_0040a948` | real rocket-family object (14-byte type table `DAT_004a9754`, "unguided" per last session's rocket.cpp work) | 5 entries, `SplashFactor=500` uniformly, real `Speed`, armor≫shield |
| `2` | `FUN_0040af6c` | a **third**, previously-unexamined rocket-family object (own 14-byte type table `DAT_004a9784`, own vtable `PTR_FUN_00498628`) | mixed: both the low-value ATC20/35/50-shaped progression *and* the EMP-shaped high-shield entries — `SplashFactor=0` for all but `MissileId=9` (the confirmed Plasma cannon, see below) |
| `3` | `FUN_0040ac3c` | the confirmed guided/homing rocket variant from last session's rocket.cpp work (own type table `DAT_004a9768`/`DAT_004a9770`) | 3 entries, shield==armor exactly, `SplashFactor` 1000/500/500 |
| `4` | `FUN_0040bf74` | **no persistent simulated object at all** — resolves its raycast hit synchronously inside the call itself, then spawns pure-visual tracer segments | every single `Type=4` record has `Speed=0`, no exceptions |

`Type=4`'s "no persistent object, resolves at the call site, always `Speed=0`" combination is the
concrete mechanical definition of a beam/hitscan weapon in this engine — there's no separate
travelling instance because none is needed. `Type=0`/`2`/`3` are genuinely different rocket-family
C++ classes (three distinct vtables), not the same class with three data variants — matching the
Java doc comment's original "ID (BULLETS or ROCKETS)" description more precisely than previously
understood: "BULLETS" = `Type 4` only, "ROCKETS" = three separate sub-classes (`0`/`2`/`3`)
differing in guidance and splash, not one family.

Best-effort mapping onto the user's original weapon taxonomy ([[project-es2-game-domain]]),
**flagged as a reasoned hypothesis from mechanism + shape, not proven by name — except one entry,
which is now confirmed by a concrete code path, not just shape (see below)**:
- **`Type 4` (beam, no travel time) → Lasers + PBW.** The two unusually low-damage `Type 4`
  entries (150/200 and 200/300, both far below the others' 1000+ values) are a plausible fit for
  Electron Flux specifically — the manual's "short-range lightning gun" framing predicts a
  weaker/shorter-range profile than dedicated Lasers — but this is not confirmed, only consistent.
- **`Type 2` (real flight time, no splash) → Autocannons + EMP**, both single-target per the
  user's taxonomy — this accounts for every `Type 2` entry except one.
- **`Type 0`/`Missile` (5 entries) and `Type 3`/`Rocket` (3 entries) → the game's ordinary
  Missile weapons, not Plasma cannon.** Corrected from an earlier guess in this section
  (`Type 0` = Plasma cannon) after finding where Plasma actually lives (below) — with Plasma
  accounted for elsewhere, there's no more reason to doubt the original Java author's own
  `ProjectileType` names (`Missile`=0, `Rocket`=3) in favor of a shape-based guess. Both remain the
  only two wholesale splash-capable `Type`s, consistent with two Missile sub-variants
  (guided/unguided) rather than one being a disguised Plasma cannon.

**Plasma cannon — found concretely, not just guessed from shape (2026-08-09, same continuation
session).** The one `Type 2` outlier flagged above (`DamageShield==DamageArmor==3000`,
`SplashFactor=1000` — anomalous for a family that's otherwise single-target with unequal
shield/armor) is `MissileId 9`. Dumping the `Bullet` class's own vtable (`PTR_FUN_00498628`)
shows its per-tick slot (`+0x14`) is `FUN_0040b124` — already documented, two sessions ago, as
having a special `type == 9` branch (checked via `*(char*)(this+0x41) == '\t'`) that calls the
explosion formula directly instead of the ordinary single-target hit path, and flagged then as
"very plausibly the Plasma cannon" from taxonomy alone. Confirmed this session: `this+0x41` is
exactly where *every* rocket-family constructor (`FUN_0040a948`/`FUN_0040af6c`/`FUN_0040ac3c`)
stores its own `MissileId` argument — so `FUN_0040b124`'s "type==9" branch is checking
`MissileId==9` on a live `Bullet`-class instance, not some other field. **This one entry —
`(Type=2, MissileId=9)` — is the Plasma cannon: mechanically a `Bullet` (real flight time, unlike
the true `Beam`s) that explodes with splash on impact (unlike every other `Bullet`), matching the
user's taxonomy's "an energy weapon that fires a slow-moving projectile, does splash" description
exactly, and now anchored to a specific `PROJ.DAT` record rather than a whole `Type`.**

**The full `Type`-to-name mapping (not just the mechanism) is still open** — `MissileId` (the
second key field) is, per the existing doc comment, *also* used to index into `BULLETS.DAT`/
`ROCKETS.DAT` for model data, meaning a weapon's `(Type, MissileId)` pair is set somewhere upstream
(most likely the still-untraced 88-byte `"weapons"` record table found alongside `PROJ.DAT`'s own
loader this session) rather than being implied by `PROJ.DAT`'s own array position — the "entries
are in Weapon ID order" assumption in the existing doc comment is best treated as an unproven
coincidence of how the file happens to be laid out, not something the engine relies on (it looks
records up by key, never by index). Tracing that 88-byte table is the natural next step to get a
real name for every index, not further pattern-matching from shape.

## Weapon mounts

`this+0x202` is a **pointer to a separately-allocated weapon-mount-manager object** (own vtable;
size `0x14` or `0x35` bytes depending on the `this+0xa3` flag) — corrected this session from an
earlier "separate array" description, see "The shield system" above for the allocation site
(`FUN_004175dc`). It's referenced throughout `FUN_00417de4` for computing ammo/heat-style ratios
and by `FUN_00415558` (a "find the next occupied weapon slot" iterator, walking a 7-entry table
`DAT_0049a060`), and is where the per-tick energy-arbitration vtable call in the shield-recharge
tick goes. When a weapon mount is destroyed (inside `FUN_004188c8`'s damage-split logic), its
`this+0x20e` active-flag is cleared and its owning object is released. This is consistent with the
manual's separate "Weaponry" HDD damage category.

## Reconciling with the manual's shields/armor/structure and Structural/Internal/Weaponry terminology

The manual describes combat in terms of two related but distinct three-part framings: "shields...
armor... [a sustained attack gets through]" (a penetration-order description), and a HUD/HDD
display split into "structural, internal, and weaponry" categories (a component-type
description, each shown with its own percentage). Best current synthesis, stated with the
confidence level it deserves:

- **Shields** = confirmed, the `+0x222` struct above. Global, front/rear, matches the manual
  precisely.
- **"Armor"**, in the manual's "where shields leave off, armor takes over... duranium plates...
  layered over the HERC's surface" sense, best maps to the **per-component health values**
  (`this+0x20a`/`this+0x206`) found above — i.e. not a separate third depleting pool distinct from
  "structure," but the protective material each individual component (leg, torso panel, weapon
  mount, internal system alike) inherently has as its own health value. This reading is a
  synthesis, not a literal quote — the manual's own "ARMOR" paragraph doesn't explicitly name a
  further, deeper layer beyond armor in that specific passage, and the HDD's Structural/Internal/
  Weaponry split reads as three *parallel categories of components* (each independently
  percentage-tracked) rather than three *sequential layers of one HP value* — matching a single
  per-component health array covering all three categories uniformly, which is what the code
  shows.
- **Structural / Internal / Weaponry — SOLVED (2026-08-09, continuation session).** Not a 3-way
  partition of one index space, as this section originally guessed — three genuinely different
  mechanisms. Structural and Weaponry are both slots in the *same* 29-entry component array
  (`HercWorks.Core.Data.File.Dbsim.HercSimDamage.HercPiece`, i.e. `.DMG`'s per-mech component
  table — confirmed as this session's `this+0x206`/`this+0x212` array by tracing DBSIM's own
  `dmg\[herc].DMG` loader), distinguished only by name/position — the Java author's own doc
  comment lists real names in array order (`COCKPIT/FRONT`, `COCKPIT/REAR`, `SHOULDER/LEFT`/
  `RIGHT`, `WEPN_BRACK/LEFT`/`RIGHT`, `TORSO`, `LEG/.../UPPER`/`LOWER`, ...), confirming both that
  `WEPN_BRACK/LEFT`/`RIGHT` (indices 4-5) are ordinary slots in this array (not a separate
  structure) and that `COCKPIT/FRONT`/`REAR` (indices 0-1) are exactly the two components this
  session found individually gated (`FUN_0040d9f8`) before the mech-death determination. Internal
  is a wholly *separate*, smaller table (`HercInternals` — Leg Servos, Sensor Array, Targeting
  Computer, Shield Generator, Engine, Hydraulics, Stabilizers, Life Support, Pilot), reached only
  *probabilistically* through a struck structural/weaponry piece's own `MappedInternals`/
  `CritChance` list — an Internal system has no health slot of its own to be directly targeted.
  Full byte-level record layout (including the previously-unresolved `Unk_val`, now
  `DestructionFlags`, a cascade/effect bitfield) is in "The component/health system" above.
- **"Armor" reconciliation, now better-grounded but still not fully closed:** the per-component
  `Armor` field on `HercPiece` (this session confirmed it's literally named that in the
  already-existing C# port, independently of this doc's own "armor = per-component health"
  synthesis reached from disassembly alone) supports the reading above — a component's own health
  value *is* called "Armor" in the data itself, not merely inferred to represent it. Genuinely
  still open: whether shields differentiate by weapon type anywhere (checked, not found in the
  shield-absorption functions themselves — see "Weapon-type effectiveness" above), and the
  remaining `+0x02`/`+0x03` sub-byte semantics within `DebrisFlags`.

## Open items

**Shield recharge and balance-adjustment input handling — SOLVED**, see "The shield system"
above (`FUN_0041aa5c`/`FUN_00413b38` for recharge, `FUN_00413bc8`/`FUN_00413af8` for balance
input).

**Weapon-type effectiveness multiplier values — SOLVED, including concrete per-weapon numbers.**
The per-weapon-type record table (`DAT_004a9980`, 36 bytes/record, keyed by `(category,
subtypeId)` via `FUN_0040ffc8`, loaded by `FUN_0040fc8c` from a resource literally named `"proj"`)
is `PROJ.DAT`'s in-memory copy — the same file already known to and partially parsed by the C#
port (`ProjectileData.cs`). Cross-checked against the real retail file byte-for-byte; all 27 real
weapon records decoded and spot-checked against the manual's fiction. The third field
(`Unk2_val`, now renamed `SplashFactor`) is also resolved: a per-weapon fraction of armor damage
diverted into a secondary explosion, not a third damage-type multiplier. See "Weapon-type
effectiveness" above for the full account.

**Beam-weapon (Laser/PBW/ELF) confirmation — SOLVED, and the earlier "exhaustive dead end" framing
was chasing the wrong question.** The first attempt this session scanned every DBSIM function for
a direct call through vtable slot `+0x20` (the mech's hit-test slot), reasoning that a beam weapon
must call it directly on a locked target since it doesn't spawn a travelling projectile. That
search came back empty (18 hits, all wrong argument shape or unrelated classes on checking) —
correctly executed, but built on a false premise: **beam weapons don't need a special call to the
hit-test slot at all, because they still go through the exact same `FUN_00426528` raycast every
other weapon uses — they just do it synchronously, once, inside their own fire function, with no
persisting object afterward.** This fell out for free while tracing `PROJ.DAT`'s `Type` field for
the weapon-mapping question above: `FUN_0040bf74` (long assumed to be "the ordinary bullet
function," one of only 5 known `FUN_00426528` callers) calls the raycast **immediately, at fire
time**, resolving the hit before any tracer visual is even spawned — and its own hardcoded
`Type=4` lookup category corresponds, in every single real `PROJ.DAT` record, to `Speed=0`. A
beam is just a `Type=4` shot: the same object, the same raycast, the same hit-test — the only
thing that changes is the caller passes a subtype whose `PROJ.DAT` record says "no travel time."

Confirmed the dispatch structure end-to-end via `FUN_0040ea58`/`FUN_0040ec64` (the generic
weapon-mount fire handlers, found by decompiling `FUN_0040bf74`'s own 2 callers): each checks the
mount's cached `PROJ.DAT` record's `Type` field (`**(short**)(mount+0x20) == 4`) and branches —
`Type==4` → `FUN_0040bf74` (instant hitscan, the beam path); anything else → `FUN_0040b5a0`/
`FUN_0040b43c`, which **unconditionally constructs a `Type=2` object via `FUN_0040af6c`** (real
flight-time, no persisting-instance ambiguity — this is a genuine travelling projectile, unlike
the `Type=4` path). So the *entire* generic gun/beam hardpoint mechanism is a two-way branch on
one field, and beams are simply the branch that happens not to build a travelling object.

**The sibling missile-launcher dispatcher — found (2026-08-09, same continuation session,
following up on a flagged gap).** `FUN_0040e964` is structurally parallel to `FUN_0040ea58`
(same `FUN_0040e788` setup call, same shared scratch globals `DAT_004a98d8`/`DAT_004a98e4`),
found by tracing callers of `FUN_0040a9c4` (the already-known-from-two-sessions-ago "fire a
rocket" entry point) up one more level — its only non-AI-scripted caller. Its branch: reads the
mount's cached `PROJ.DAT` record via `psVar1 = *(short**)(mount+0x20)` (same cache field
`FUN_0040ea58` uses) and checks `*psVar1 == 0` (`Type == Missile`) — if so, fires via
`FUN_0040a9c4(psVar1[1] /*MissileId*/, ...)`, the confirmed rocket/missile spawn entry point that
itself already handles lock-on target capture and chooses unguided (`FUN_0040a948`) vs. guided
(`FUN_0040ac3c`) physics internally (see "Rocket physics" above); any other `Type` falls through to
the *same* `FUN_0040b43c`/`Bullet` fallback `FUN_0040ea58`'s else-branch uses. So `FUN_0040e964` is
specifically the fire-dispatch for missile-*capable* hardpoints (branching Missile-vs-everything-
else), complementing `FUN_0040ea58`'s gun/beam-capable hardpoints (branching Beam-vs-everything-
else) — two mount categories, each defaulting to the shared ballistic `Bullet` path for whatever
they don't specifically handle. `Type 3`/`Rocket`'s own dedicated dispatch trigger (as opposed to
its constructor, `FUN_0040ac3c`, already confirmed) wasn't separately traced — plausible it's
selected *within* `FUN_0040a9c4` by a guidance-capability flag rather than by a distinct top-level
dispatcher, matching how that function already internally picks between the two rocket
constructors.

**How this reconciles with the earlier "exhaustive dead end":** the shot-record field
`shotData+0x12` (hardcoded `5` for `FUN_0040bf74`'s bullets) is a *completely different* numbering
scheme from `PROJ.DAT`'s own `Type` field — conflating the two during the first pass is what made
the search for a "separate beam mechanism" look necessary when it wasn't. `shotData+0x12` gates an
unrelated target-side alert/timer effect (see the direct-fire section above); `PROJ.DAT`'s `Type`
is what actually determines beam-vs-projectile behavior, and it lives one level up, at the
mount's fire-dispatch decision, not in the shot record's own miscellaneous flag byte.

**Structural/Internal/Weaponry index mapping — SOLVED, and reframed.** It isn't a 3-way partition
of one index space: Structural and Weaponry are both slots in the same 29-entry `HercPiece`
(`.DMG`) array, distinguished only by name/position (`WEPN_BRACK/LEFT`/`RIGHT`); Internal is a
wholly separate, smaller table (`HercInternals`) reached probabilistically through a struck
piece's own `MappedInternals`/`CritChance` list, not directly targetable. See "The component/
health system" above for the full account, including the resolved `HercPiece.DestructionFlags`
bitfield (was `Unk_val`).

**`FUN_00426528`'s and `FUN_004198f4`'s exact source translation unit** remain unconfirmed by a
direct assert string — the `objlist.cpp`/`flyersys.cpp` attributions are architecturally
well-supported (shared object-list usage, sensor/autopilot semantics) but not proven the way
rocket.cpp/collide.cpp were. If a future session wants certainty, the next lever to pull is a
relative-address diff against a symbol table or map file if one can be found for this build,
rather than more string searching (string search has now been exhausted for this address region).

**`debris.cpp`'s `FUN_0040874c`** was decompiled but is, like the rocket/bullet loaders, a
load-time setup function (loads a debris-piece list, reads per-piece records via
`FUN_004083f8`). One real numeric finding inside `FUN_004083f8`: two angle-like `short` fields are
each multiplied by `0xb6` (182) after loading, unconditionally unless the raw value is the
sentinel `-1`. `65536 / 360 ≈ 182.04`, so **`×182` is very likely a degrees→BAM (16-bit binary
angle measurement, 0–65535 representing 0–360°) conversion** applied to debris piece orientation
data at load time — consistent with the rest of the engine's fixed-point-over-floating-point
design philosophy. Not yet cross-checked against a second `×182`/`×0xb6` site to confirm the BAM
theory beyond "the constant matches almost exactly."

**`mechsys.cpp` / `flyersys.cpp` / `grid.dat` — reconfirmed genuine dead ends, via a second,
independent method.** Last session found zero code cross-references to these three strings via
`ES2FindStringRefs` (a decompiler/symbol-based search) and flagged that result as needing
independent confirmation. This session re-checked all three directly against Ghidra's low-level
reference-manager database (`ReferenceManager.getReferencesTo`, via `ES2FindAddressRefs` — a
different code path than the string-xref script, not just a rerun of the same check) and got
empty results again for all three. Two independently-implemented lookups agreeing is strong
evidence these are genuinely unreferenced debug/RTTI-adjacent strings in DBSIM.EXE specifically
(matches the `MECH`/`MECH_TYPE_DATA[]` pattern already documented as a confirmed dead end in
`docs/formats/bnd-notes.md` for `VSHELL.EXE`'s `.BND` investigation) — stop looking for their
referencing function; if `mechsys.cpp`/`flyersys.cpp` logic is needed, it has to be found by
following code rather than by string xref.

## How to apply

For porting combat feel to a modern engine, the load-bearing facts are:

1. **DBSIM is a fixed-timestep, fixed-point simulation** — `DAT_004d3be8` is the tick length and
   essentially all motion math is `rate × tick` in Q8, not continuous float integration; a naive
   float-based reimplementation will drift from the original unless the same quantization/
   clamping is preserved.
2. **Collision bounds are spheres built from AABBs of named sub-part spheres**, using a specific
   ~3.4%-low-biased fast-magnitude approximation rather than true Euclidean distance —
   reproducing hit detection faithfully means reproducing that approximation, not substituting a
   real `sqrt`.
3. **Guided rockets lead-predict, deadband, and rate-limit their turn** — the `0x500`/tick turn
   cap and the `0xc00`/`0x1800` deadband thresholds most directly control how "floaty" vs.
   "locked-on" homing feels.
4. **Shields are a single pool per side (front/rear), redistributable by balance, that hard-caps
   how much damage of any kind gets through** — `absorbed = min(damage, remainingCharge)`, so
   damage bleeds through the instant a hit exceeds what's left in that zone, not only once the
   zone is already empty. Both damage pathways (direct-fire and explosive) implement this
   separately but with the same concept; a port needs shields to gate both.
5. **There are two structurally different post-shield damage models, not one.** Direct fire hits
   exactly one deterministically-selected component (found by real hit geometry, not randomness),
   applies a per-weapon-type multiplier, and splits damage between destroying a weapon mount (with
   a possible secondary explosion) and general component health — no distance falloff. Explosive
   weapons (confirmed: missile ground-impact, the Plasma cannon's bullet-type-9, mech death) sweep
   the whole object list by blast radius, then independently roll each of up to 29 components at
   ~51% odds and apply linear distance falloff to the ones that pass. Using one model for both
   weapon categories would break the feel of both — precise weapons would spray damage like a
   mini-explosion, and explosives would lose their splash radius entirely.
6. **Both pathways converge on one shared health-writing/cascading-destruction primitive** — a
   component's health is the aggregate of itself plus dependent sub-parts, and destroying one
   component can cascade to destroy its dependents. Enough destroyed limbs kill the mech outright.
   A port needs this dependency graph, not just a flat per-part HP list.
7. **Terrain height is bilinearly interpolated across a per-cell-selectable diagonal**, not a
   naive fixed-diagonal heightmap — matters for exact collision/ballistic-impact-point parity with
   the original on sloped terrain.

8. **Shields recharge from the reactor at a flat 5-units/tick cap, redistributed by a
   player-adjustable balance value (range 0–1024, default 512, ±102/tick while held)** — a
   separate per-mech-per-tick system (`FUN_0041aa5c`/`FUN_00413b38`) from the player-only
   balance-input handler (`FUN_0041b130`/`FUN_00413bc8`/`FUN_00413af8`). A port needs both: the
   background regen for every mech (AI included), and the player-specific input path layered on
   top, connected only through the shared balance/charge fields.
9. **Per-weapon-type effectiveness is `PROJ.DAT`'s own `DamageShield`/`DamageArmor` fields,
   applied once, upstream, when the shot record is built** — not a branch inside the shield or
   structure damage functions. The shot's own power/charge level (Q8) is independently scaled
   against each of those two per-weapon stats before shields and structure ever see it; a third
   field (`SplashFactor`) diverts a fraction of the armor-damage portion into a secondary
   explosion. A port's "energy weapons hit shields hard, projectiles hit armor hard" behavior
   should read directly from `PROJ.DAT`'s existing fields — the data needed is already parsed.

**New technique this session, worth reusing:** when a struct offset is accessed only through a
passed-in pointer (not a fixed data address), `ES2FindAddressRefs` can't find its accessors —
there's no fixed address to search for. `E:\ES2Stuff\tools\ghidra_scripts\ES2FindImmediateRefs.java`
fills that gap: it decompiles every function in the program and greps the decompiled *text* for
literal substrings (hex offsets like `"0x222"`, or call-shape fragments like `"+ 0x20))("` for an
indirect call through a specific vtable slot regardless of whose vtable it is). This is how both
the shield-function cluster and the beam-weapon vtable-offset scan were done this session — prefer
it over guessing at vtable slots by size/position when a fixed-address search doesn't apply.

As of this continuation session, all four items this session set out to resolve (shield recharge/
balance input, weapon-multiplier values, beam-weapon confirmation, Structural/Internal/Weaponry
mapping) are closed — two of them (`PROJ.DAT`, `.DMG`'s `HercPiece`) turned out to already have
independent, high-quality documentation in the C# port from the original Java author, and checking
that *before* re-deriving structure from scratch was the single highest-leverage move both times.
**Before starting deep DBSIM disassembly on any new question, check whether the C# port already
has a parser with a matching record size/field count and an "unknown field" TODO first** — this is
now a proven pattern, not a one-off.

**Follow-up in the same continuation session — the missile-launcher dispatcher was found
(`FUN_0040e964`, structurally parallel to `FUN_0040ea58`, branching `Type==Missile` vs. the shared
`Bullet` fallback) and the Plasma cannon was pinned to a specific record** (`Type=2`/`Bullet`,
`MissileId=9` — found via the `Bullet` class's own vtable, whose per-tick slot has a dedicated
explosion branch for exactly that `MissileId`, resolving a `PROJ.DAT` outlier flagged earlier in
this same session and a "very plausibly the Plasma cannon" hunch from two sessions ago). The
88-byte per-weapon-mount template's remaining 48 raw bytes (attempted as part of that same
follow-up) did **not** yield the `(Type, MissileId)`-to-weapon-name mapping — its only other
consumer turned out to be a debris-visual spawner, not a damage/reference lookup — see "Weapon
mounts" above for the full account of what is and isn't decoded in that record.

Remaining leads, roughly in priority order: (a) ~~the `(Type, MissileId)`-to-weapon-name mapping for
`PROJ.DAT`'s other 26 entries is still open~~ **SOLVED 2026-08-11, continuation session** — see "The
real weapon-id-to-`PROJ.DAT` mapping" below, found not via `WEAPONS.DAT(sim)`'s SEQ/48-byte tail
guesswork but by tracing the mech-loadout weapon-mount factory that actually consumes one specific
field within that tail; (b) if pinning down `FUN_00426528`'s/`FUN_004198f4`'s exact source
translation unit still matters, a symbol/map-file search is a better lever than further string
xrefs, which are exhausted for this address region; (c) the `Missile` and `Rocket` constructor
classes (`FUN_0040a948`/`FUN_0040ac3c`) still have unexplored per-class fields beyond their
`PROJ.DAT` category literal and confirmed guidance behavior — `Bullet`'s class (`FUN_0040af6c`) is
now better understood via its vtable and `MissileId==9` special case, but a full field map for all
three, the way rocket.cpp's was rounded out two sessions ago, remains undone.

## The real weapon-id-to-`PROJ.DAT` mapping — SOLVED (2026-08-11, continuation session)

Closes the single item this doc's "Remaining leads" had flagged as still open after the previous
session's `PROJ.DAT`/beam-weapon/`.DMG` work. Found by picking a different thread than the one the
previous session tried (`GUNLIST.CPP`'s loader, a confirmed dead end for this specific question):
tracing every caller of `Proj_LookupRecord` (`0x0040ffc8`) turned up a 5th caller beyond the 4
already-known projectile constructors — `FUN_0040fff8`, called from `Mech_ConfigureLoadout`
(`0x004175dc`, DBSIM's mech-loadout-(re)configuration entry point, already known from the shield
system's `+0x202` correction two sessions ago). Renamed `MechLoadout_ConstructWeaponMounts`.

`MechLoadout_ConstructWeaponMounts` is the mech-loadout-time factory that turns a real catalog
weapon id (0-32, `SHELL0/GAM/WEAPONS.DAT`'s own id space) into a live weapon-mount C++ object for
each of a mech's hardpoint slots. For each occupied slot it: reads the slot's weapon id, fetches
that weapon's `simvol0/dat/WEAPONS.DAT` template via a **direct flat array index** (`weaponId *
0x58 + base`, `WeaponMountTemplate_GetByWeaponId`/`0x0040fe84` — this is the first confirmation
that the sim and shell weapon catalogs share one 33-entry indexing scheme by weapon id, not merely
the same entry count), reads a specific 2-byte field within that template's previously-undecoded
48-byte tail (tail-relative offset `0x1c`, now named `ProjDatIndex` — see
`docs/formats/weapons-dat-sim.md` for the full byte-offset derivation), and resolves the weapon's
`PROJ.DAT` shot-data record from it before constructing the mount object with that record attached.

`ProjDatIndex` has three cases, confirmed against a byte-exact cross-join of the real retail
`simvol0/dat/WEAPONS.DAT`, `SHELL0/GAM/WEAPONS.DAT` (for real weapon names), and `simvol0/dat/PROJ.DAT`
(a throwaway `dotnet run` console probe against `HercWorks.Core`'s own transformers — deliberately
reusing the already-shipped, already-verified byte layouts rather than re-parsing by hand):

1. **`0x21` (33) — no `PROJ.DAT` lookup at all.** Real-world case: only `ECM`.
2. **`0x22` (34) — resolved via `Proj_LookupRecord(category=0/*Missile*/, secondaryKey)`**, a
   `(category, subtypeId)` search rather than a direct index; `secondaryKey` comes from a
   *different* per-hardpoint-slot table (`MechLoadout_ConstructWeaponMounts`'s own `param_6`), not
   from the template itself, so it isn't resolvable from `WEAPONS.DAT` alone. Real-world case:
   exactly `MSL6`/`MSL8`/`MSL10`/`FLYMSL` — the four tube/rack-style missile launchers, consistent
   with one catalog "launcher" entry being able to carry different submunition records by loadout
   variant.
3. **Otherwise — a direct flat array index into `PROJ.DAT`** (`Proj_LookupRecordByIndex`,
   `0x0040ffb0`: `index * 0x24 + ProjDat_RecordTable`). Confirmed for 21 of the 32 real catalog
   weapons (2 of those 21, `PLAS` and `MFAC`, happen to share one index). 6 more catalog ids
   (`NONE`, `LAEW`, `MINE`, `TARG`, `SHLD`, `TURB`, `ENRG`) carry a byte-identical all-zero
   placeholder template whose `ProjDatIndex` reads `0` — a coincidentally "valid" index that
   `MechLoadout_ConstructWeaponMounts`'s own per-weapon-id switch statement confirms
   `TARG`/`SHLD`/`TURB`/`ENRG`'s mount constructors never even receive as an argument (they're
   passive stat-boost systems, not firing weapons — the field is simply inert for them); `LAEW`'s
   constructor *does* receive it, so `LAEW` genuinely resolves to `PROJ.DAT` index 0 (`ATC20`'s
   record), whether or not that was the original data author's intent. This corrects the earlier
   session's guess (`32 - 5 = 27` skipped ids) — the real skipped set is 6 ids wide, not 5, and the
   count-27 match was coincidental, not causal: `PROJ.DAT`'s remaining 7 entries (indices 7-13,
   exactly the 3 `Rocket` + the other 4 `Missile` entries not already claimed by `BMSL`) are reached
   only through the `MSL6`/`MSL8`/`MSL10`/`FLYMSL` secondary-key path above, not by direct id order.

**Retroactively confirms several manual-fiction matches by real weapon name, not just numeric
shape:** `EMPC` (index 6, 2000 shield/400 armor) and `BEMP` (index 16, 8000/2000 — the exact
"8000/2000" pair this doc's own weapon-effectiveness section already flagged as EMP-shaped from
numbers alone) really are the EMP cannons ("EMP cannons disrupt the shield matrix"); `PLAS` (index
22, `MissileId==9`) really is the Plasma cannon, confirming by name a mechanism two sessions ago
only confirmed by behavior; `ELFW` (index 15, 150/200 — the exact "unusually low-damage `Beam`
entry" flagged as a plausible ELF candidate one session ago) really is Electron Flux. The full
index table (all 27 `PROJ.DAT` entries, weapon names, `Type`/`MissileId`/damage fields) is recorded
in `HercWorks.Core.Data.File.Dat.Sim.ProjectileData`'s doc comment rather than duplicated here — see
that file for the exact numbers.
