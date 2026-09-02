# DBSIM.EXE beam visuals: the tracer object and how it is drawn

What a fired beam looks like. The firing side — trigger, dispatch, shot record, hit resolution — is
in [`weapon-firing.md`](weapon-firing.md).

## Chain

`Bullet_FireBurst` (`0040bf74`) resolves the hit, then builds the visual from the already-shortened
distance:

1. `Sound_PlayAt(0x0b, muzzlePoint)` — `FUN_004627dc`, untraced beyond the call.
2. The far end is rebuilt from the shot's own frame as `transform(0, travelled, 0)`, where
   `travelled` is the raycast's distance or the weapon's full range when it struck nothing.
3. One tracer object per **5000-unit** span, allocated from the pool at `DAT_004a9746`, plus a final
   one for the remainder. The loop advances the shot transform's translation by a 5000-unit step each
   iteration and writes it back, so each tracer spans start→start+step.
4. Subtype ids **1 and 7** (ELF, ELF2) skip the span loop entirely and spawn one object of a
   different shape — see [ELF](#elf-and-elf2--the-jagged-branch).

The `local_20` values written before each allocation (`0x14`, `0x2c`, `0x20`, `0x44`) are Watcom
exception-frame state, not data.

## The tracer object — `BeamTracer_Ctor` (`0040b804`)

Constructed as `(obj, subtypeId, startPoint, endPoint, owner)`; vtable `PTR_FUN_004987c4`, type 3.

| Field | Meaning |
|---|---|
| `+0x41`, `+0x52` | subtype id (byte and short copies) |
| `+0x4a` | owner |
| `+0x54` | quad count — 1 for a straight beam |
| `+0x55` | jagged flag — 0 straight, 1 ELF |
| `+0x56` | point count — 2 for a straight beam |
| `+0x58` | point array, 12 bytes per point |
| `+0x5d` | life timer |

A straight beam stores exactly two points: the muzzle and the hit.

**Lifetime is one tick.** The timer arms at `0x38` = 56, in the same Q8-of-125 ms unit as every other
simulation timer, so 27 ms. Vtable `+0x14` (`FUN_0040c2a0`) is one `Math_CountdownTimerTick` and
nothing else, and `Sim_MainTick` frees the object the tick it returns zero. Since 56 is less than one
`SimTickDelta` (81 at the 40 ms frame cap), a tracer never survives a second tick however fast the
machine runs — which is what makes a held trigger read as separate flashes rather than a continuous
beam.

`Sim_MainTick` walks this pool **before** the machine list, so a tracer spawned during a machine's
update is not counted down until the tick after.

## Appearance data

`Beam_LoadResourceTables` (`0040b6e0`, the beam module's init, named by the `BEAM.CPP` string at
`00498781`) loads two resources once at startup.

**`dat\BEAM.DAT`** → `DAT_004a9888`. `int16 count`, then `count x` 3 `int16`s. Indexed by the
`PROJ.DAT` record's **subtype id**, not by weapon id. C# port:
`HercWorks.Core.Data.File.Dat.Sim.BeamData`; engine wrapper `Content.BeamAppearance`.

| Field | Meaning |
|---|---|
| 0 | half-width, world units |
| 1 | palette index — **unused by the straight path**, see below |
| 2 | `BEAMTEX.DBA` frame |

Retail (10 records, frame 0 throughout):

| id | Weapon | Half-width | Colour |
|---|---|---|---|
| 0 | PBW | 60 | 10 |
| 1 | ELFW | 30 | 104 |
| 2 | BPBW | 120 | 10 |
| 3 | L100 | 20 | 88 |
| 4 | L200 / L400 | 25 | 88 |
| 5 | L300 / L500 | 30 | 88 |
| 6 | PBW2 | 75 | 1 |
| 7 | ELF2 | 45 | 99 |
| 8-9 | unused | 35, 40 | 88 |

**`dba\BEAMTEX.DBA`** → `DAT_004a988c` via `FUN_00469f38`, which packs each frame into a 256x256
atlas page and writes a 20-byte descriptor `{x0, y0, x1, y1, pageIndex}` — hence the draw's
`(short)entry[4]`. The `+0x12` short flags a frame containing palette index 0.

Retail ships **one** frame, 128x25, every row a single repeated index: 11 at both edges, then the
ramp 84..95 in to the middle and back out. Nothing varies along the beam's length, so the frame is a
pure cross-section. In a `WORLD<n>.DPL` that ramp is the fire ramp — dark orange (184, 92, 20)
climbing to near-white (252, 248, 228).

## Drawing — `BeamTracer_Draw` (`0040bc14`, vtable slot 0)

Per quad:

1. Both points to view space (`FUN_0048c470`), then the pair clipped against the near plane
   (`FUN_0040bb4c`); a pair wholly behind it is dropped.
2. Both projected to screen (`Raster_PerspectiveDivide`, `FUN_0048c5c4`).
3. Half-width in pixels at each end: `FUN_0048c4c0(width, viewZ)` = `(width << shift) / z`, then
   `if (< 2) = 2`. This floors the **half**-width, so a beam is never narrower than four pixels.
4. Four vertices: each screen point stepped ±(half-width) along the segment's 2D perpendicular,
   normalised in Q11.
5. UVs from the frame descriptor: u runs along the beam's **length**, v across its width.
6. `FUN_00468310(4, verts, 0, page, NULL, 0)`.

No z is written — the vertex struct's `+8` is left untouched.

### The fill is a plain texture copy

`FUN_00468310`'s third argument selects the span routine, and its last selects transparency:

| mode | span routine | interpolants |
|---|---|---|
| 0 | `FUN_0046ab10` | u, v |
| 1 | `FUN_0046ac48` | u, v + a shade level from `param_5` |
| 2 | `FUN_0046adad` | u, v + a third at vertex `+0x14` |

A beam uses **mode 0 with the transparency argument zero**, which is `FUN_0046ab10`'s opaque half:
fetch `atlasPage[v][u]`, store that palette byte to the framebuffer, step the fixed-point u/v,
repeat. The non-zero form is a colour-key skip of index 0 — not blending. **There is no alpha, no
shade level and no colour lookup anywhere in this path.**

### `BEAM.DAT`'s colour index is the fill brush, and only the jagged path uses it

Before either branch runs, the draw installs `{0, colourIndex}` at the graphics context's `+0x22c`.
That field is **the rasterizer's fill brush**: `Raster_InstallRenderContext` (`00480c38`) sets the
clip block to `ctx + 4`, so `ctx+0x22c` is the `clipBlock+0x228` that `Raster_DrawPolygonDispatch`
reads and dispatches on. A brush is `{mode, colour}`; mode 0 with a colour whose top byte is zero is
a flat fill of that palette index.

The straight path installs it and then never uses it — it submits through `FUN_00468310`, whose
mode-0 span routine has no colour lookup. The [jagged path](#elf-and-elf2--the-jagged-branch) goes
through the polygon dispatch and does.

So every retail straight beam draws the identical orange-to-white ribbon and is told apart only by
its width, while ELF and ELF2 are the flat colour their record names. Corroborated by retail
screenshots: laser and particle-beam shots are orange-white regardless of weapon, while `ELF` is
yellow, matching its index 104.

## ELF and ELF2 — the jagged branch

`BeamTracer_Ctor`'s branch for subtype ids 1 and 7 builds a chain instead of a segment:

- `nodeCount = (char)(distance >> 10) + 1` at `+0x54`, i.e. one node per 1024 units;
  `pointCount = nodeCount * 2 + 2`, so the loop writes `nodeCount + 1` node pairs.
- The step is the start→end delta rescaled to length `0x400` (`Math_NormalizeVec3ToLength`, `004926e4`).
- Node `k` is the running point; the **last** node restarts from the exact endpoint instead. Every
  node but the first — the last one included — is then jittered on each axis by
  `Math_RandomNext() & 0x7f`. The mask leaves that one-sided, 0 to 127, so the chain bows off the
  straight line rather than wandering either side of it, and the far end does not sit on the impact
  point.
- Each node writes a **pair**: `points[2k]` with `BEAM.DAT`'s width added to its z, `points[2k+1]`
  without. So the chain is a ribbon standing vertically **in the world**, not a camera-facing one:
  seen from directly above an ELF is edge-on.

### The paint is the shape renderer's point-list path

The three functions the paint loop calls are not beam code — they are the generic path
`TSSolidPoly_Render` and the rest of the `.DTS` renderers use, driven by globals the beam draw
publishes up front: `DAT_006c6970` = point array, `DAT_006c6974` = point count,
`DAT_006c6976` = the vertex-index list, and per quad `DAT_006c6968` = 4 vertices with
`DAT_006c696a` = `k << (3 - jaggedFlag)` as the offset into that list. `jaggedFlag` is 1 on every
object that reaches here, so the shift is always 2 and the other value is unreachable.

| | |
|---|---|
| `Poly_ProjectIndexedVertices` (`0048c964`) | Projects the four named vertices, memoising each in a per-point state byte at `DAT_006c697e` — 0 untouched, 1 behind the near plane, 2 projected — and writing screen points to `DAT_006cbb86` / count `DAT_006cbc86`. Returns non-zero when any vertex fell behind the near plane |
| `Poly_ClipRingToNearPlane` (`0048ce14`) | Only then: clips the vertex ring against the near plane, rebuilding the same screen-point list |
| `PolyFill_Fill` (`0048d4b4`) | Fills the screen polygon. Sibling of `PolyFill_FillThenOutline` (`0048d518`) without its mode-5 guard |

The fill is winding-agnostic — `FUN_004841af` measures the signed area and picks `FUN_00484116` for
the other winding — so the ribbon draws from either side. The index list is the 120-entry table at
`DAT_004a9796`, built by `Beam_LoadResourceTables` as `(i >> 1) + {1, 0, 1, 2}[i & 3]` over the
**`int16`** table at `00498640`. Read four entries from `4k`, that is `points[2k+1]`, `points[2k]`,
`points[2k+2]`, `points[2k+3]` — a wound quad spanning nodes `k` and `k+1`.

120 entries is 30 quads. Retail never approaches it: the longer-ranged of the two is `ELF` at 20000
units (see [`weapons-dat-sim.md`](../formats/weapons-dat-sim.md)), which is 20.

`PolyFill_Fill`'s second pass — re-fill in the line colour when `DAT_006c60d4 != DAT_006c60dc` — is a
no-op here. Those globals are the *default* brush; the beam installed its own on the context, so the
redraw is an identical flat fill.

### The muzzle stub is a retail fall-through

The jagged branch does not return. Control drops into the straight-beam code below it, which draws
`points[0]`→`points[1]` — for a chain, node zero with and without the width, a stub one half-width
long standing at the muzzle — and the enclosing loop runs once per quad, so the identical stub is
redrawn `nodeCount` times. It takes the straight path's `BEAMTEX` frame, not the chain's flat
colour, and the half-width pixel floor makes it wider than it is long at any real range: a ~4 px
orange-white dash at the muzzle.

Nothing in the fire path spawns a muzzle visual for this to be part of — `Bullet_FireBurst` does one
thing at the muzzle point, the sound. Logged in [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).

Retail reference: `Reference/Simulator3.jpg` shows an ELF as a thin bright yellow zigzag.

## Impact effects

Ported — [`impact-effects.md`](impact-effects.md) carries the array ordering.

## Engine port

`Sim.BeamTracer`, `Content.BeamAppearance`, `Render.BeamRenderer`. Deliberate deviations:

- **One quad per shot, not one per 5000 units.** The split exists because the original's rasterizer
  interpolates a poly's screen-space width linearly between its two ends. World-space geometry gets
  the exact perspective from the projection, so the split buys nothing.
- **The quad faces the viewer in three dimensions** — the perpendicular is
  `cross(axis, toCamera)` in a vertex shader — rather than being expanded in 2D after projection. The
  half-width floor is kept literally, measured in the framebuffer.
- **Depth test on, depth write off.** The original writes no z at all; a beam already stops at
  whatever it hit, so testing costs nothing visible and keeps a shot fired past a ridge from painting
  over it.
- **The chain's quads are built in the simulation, not the renderer.** `BEAM.DAT`'s half-width is
  baked into the geometry at fire time and the jitter is rolled off the sim generator, exactly as
  `BeamTracer_Ctor` does it, so `SimWorld` carries the appearance table.
- **The muzzle stub is drawn once, not once per quad.** The fill is opaque and the geometry
  identical, so the repeats are not observable.
- **The node count is clamped to the 30 quads the index table holds.** The original's count is a
  signed byte it never bounds, so a long enough chain would read past the table; no retail weapon
  gets near it, and the clamp is a guard rather than a behaviour difference.

## Rejected readings

Readings a fresh pass could plausibly land on. Each is disproven; do not reintroduce.

| Reading | Why it is wrong |
|---|---|
| `BEAM.DAT`'s colour index is unused — the `+0x22c` pair is a HUD colour pair mode 0 never reads | `ctx+0x22c` is `clipBlock+0x228`, the fill brush, because the clip block is `ctx + 4`. The jagged path dispatches on it |
| One of `0048c964`/`0048ce14`/`0048d4b4` redirects the geometry, since the tail reads `points[0]` and `points[1]` with no loop index | That tail is the straight-beam code the jagged branch falls through into — a separate draw, not part of the chain's |
| The chain's last node is the exact endpoint | It is the endpoint **plus** the same jitter every other node gets |
| The index table at `00498640` is bytes | `word ptr [ECX*2 + 0x498640]`; as bytes the quads collapse |
