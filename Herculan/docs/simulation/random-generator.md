# DBSIM.EXE pseudo-random generator — `Math_RandomNext` (`00492dd4`)

Reverse-engineered from `DBSIM.EXE` disassembly. All addresses are DBSIM.EXE virtual addresses.

A general math-library utility rather than a simulation subsystem: every randomised decision in the
game comes out of here, from the load-time terrain material pass to the explosion's per-component
roll. Its determinism is what makes replay parity and a differential harness possible at all, so
that property gets as much space below as the algorithm.

## The algorithm

An additive lagged Fibonacci generator over a 56-entry table of `short`s with two rotating byte
cursors. One step is `table[i] += table[j]`, returning the new `table[i]`, then both cursors advance
and wrap at 56 — the original's `== '8'` test. `i` is the state block's `+0x71` and `j` its `+0x70`.

`Math_RandomBelow` (`00492e18`) wraps it as `(next & 0x7fff) % bound` — the mask **drops the sign
bit rather than taking an absolute value**, so a bounded draw is over the low fifteen bits, not the
full sixteen, and is not quite uniform for a bound that does not divide `0x8000`.

Callers pass the state block's address and mask the result: `& 0xfff` for the terrain material roll
and for the explosion's per-component roll. The simulation's shared block is `0x4d261d`; a second
adjacent block at `0x4d268f` is seeded beside it, and what reads that one is not traced.

## Seeding — `FUN_00492d7c`

**The generator has no entropy input of any kind.** The state lives in BSS and is seeded only here,
which sets the two cursors to the literals `0x37` and `0x18` and `memmove`s 112 bytes from the
static table at `004a6958`. The two functions it calls either side — `00492e3c` and `00492e41` — are
both `push ebp; pop ebp; ret`. There is no `srand`, and the image imports no clock function that
reaches it.

So **DBSIM replays identically on every run**, up to the one wall-clock path into simulation state:
`SimTickDelta`, whose measured 40 ms comes back as 41 or 42 under load and rescales every rate that
tick (see
[`dbsim-physics-notes.md`](dbsim-physics-notes.md)). Pin that and the whole simulation is a pure
function of the mission file and the input — the premise
[`../engine/plan-differential-harness.md`](../engine/plan-differential-harness.md) rests on.

## Ported

`Numerics.SimRandom`. Its default constructor is that starting state exactly — the same table, the
same two cursors — so it and the original step in lockstep from there. `MissionScene` builds one
instance and hands it to both the terrain pass and `SimWorld`, because DBSIM has a single shared
generator that the zone load draws from before anything else does; two instances would produce the
same stream twice rather than one continuing stream.

The `SimRandom(int)` constructor is this engine's own device, for a test that wants a pinned stream
independent of the retail table.

**What is still open is not the generator but the call history.** A roll's result depends on how
many draws preceded it, so matching a specific retail roll means matching tick order, not just the
seed. Until that holds, treat any particular roll as statistically faithful rather than
replay-faithful — see [`../../ROADMAP.md`](../../ROADMAP.md). The terrain scatter is the visible
case: whether DBSIM has already drawn from the generator by the time a zone populates is not
established, and if it has, the scatter lands on different cells
([`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md)).
