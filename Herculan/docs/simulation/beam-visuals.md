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

## The tracer object — `FUN_0040b804`

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

`FUN_0040b6e0` (the beam module's init, named by the `BEAM.CPP` string at `00498781`) loads two
resources once at startup.

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

## Drawing — `FUN_0040bc14` (vtable slot 0)

Per quad:

1. Both points to view space (`FUN_0048c470`), then the pair clipped against the near plane
   (`FUN_0040bb4c`); a pair wholly behind it is dropped.
2. Both projected to screen (`FUN_0048c4f0`, `FUN_0048c5c4`).
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

### Why `BEAM.DAT`'s colour index does not apply

The draw does publish it, as `{0, colourIndex}`, through the graphics context's `+0x22c` colour pair
— the same slot the HUD painters use. Mode 0 never reads it. The shade level that pair would feed
belongs to mode 1, which nothing here selects.

So every retail straight beam draws the identical orange-to-white ribbon and is told apart only by
its width. Corroborated by retail screenshots: laser and particle-beam shots are orange-white
regardless of weapon, while ELF — which takes the other branch — is yellow, matching its index 104.

## ELF and ELF2 — the jagged branch

**Geometry decoded, paint not.** `FUN_0040b804`'s branch for subtype ids 1 and 7 builds a chain
instead of a segment:

- `nodeCount = (distance >> 10) + 1`, i.e. one node per 1024 units; `pointCount = nodeCount * 2 + 2`.
- The step is the start→end delta renormalised to length `0x400` (`FUN_004926e4`).
- Node `k` is the running point, jittered on every axis by `Math_RandomNext() & 0x7f` (0-127) for all
  but the first; the last node is the exact endpoint.
- Each node writes a **pair**: `points[2k]` with `BEAM.DAT`'s width added to its z, `points[2k+1]`
  without. So the chain is a vertical ribbon, not a camera-facing one.

The paint loop runs once per node with `DAT_006c6968 = 4` and `DAT_006c696a = k << (3 - jaggedFlag)`
before calling `FUN_0048c964` / `FUN_0048ce14` / `FUN_0048d4b4`, none of which are decoded. Those
read the projection globals and the point-list globals the draw publishes up front
(`DAT_006c6970` = point array, `DAT_006c6974` = point count, `DAT_006c6976` = the 120-entry ramp
`DAT_004a9796`, built at init as `(i >> 1) + DAT_00498640[i & 3]`). Disassembly of the shared tail
shows it reading `points[0]` and `points[1]` with no loop index, which does not square with drawing a
chain — so at least one of those three helpers redirects the geometry, and the reading is incomplete.

Retail reference: `Reference/Simulator3.jpg` shows an ELF as a bright yellow zigzag, which fixes
both the shape and that the colour index reaches this path.

## Impact effects

Solved and ported — see [`impact-effects.md`](impact-effects.md), which settles the array ordering
this section previously listed as half-traced.

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
- **ELF and ELF2 draw straight**, pending the branch above.
