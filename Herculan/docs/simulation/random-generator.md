# DBSIM.EXE pseudo-random generator — `Math_RandomNext` (`00492dd4`)

Reverse-engineered from `DBSIM.EXE` disassembly. All addresses are DBSIM.EXE virtual addresses.

A general math-library utility rather than a simulation subsystem: every randomised decision in the game comes out of here, from the load-time terrain material pass to the explosion's per-component roll. Its determinism is what makes replay parity and a differential harness possible at all, so that property gets as much space below as the algorithm.

## The algorithm

An additive lagged Fibonacci generator over a 56-entry table of `short`s with two rotating byte cursors. One step is `table[i] += table[j]`, returning the new `table[i]`, then both cursors advance and wrap at 56 — the original's `== '8'` test. `i` is the state block's `+0x71` and `j` its `+0x70`.

`Math_RandomBelow` (`00492e18`) wraps it as `(next & 0x7fff) % bound` — the mask **drops the sign bit rather than taking an absolute value**, so a bounded draw is over the low fifteen bits, not the full sixteen, and is not quite uniform for a bound that does not divide `0x8000`.

Callers pass the state block's address and mask the result: `& 0xfff` for the terrain material roll and for the explosion's per-component roll. The simulation's shared block is `0x4d261d`; sounds, messages and the cockpit's own effects draw on a [second generator](#the-presentation-generator) instead.

## Seeding — `FUN_00492d7c`

**The generator has no entropy input of any kind.** The state lives in BSS and is seeded only here, which sets the two cursors to the literals `0x37` and `0x18` and `memmove`s 112 bytes from the static table at `004a6958`. The two functions it calls either side — `00492e3c` and `00492e41` — are both `push ebp; pop ebp; ret`. There is no `srand`, and the image imports no clock function that reaches it.

So **DBSIM replays identically on every run**, up to the one wall-clock path into simulation state: `SimTickDelta`, whose measured 40 ms comes back as 41 or 42 under load and rescales every rate that tick (see [`dbsim-physics-notes.md`](dbsim-physics-notes.md)). Pin that and the whole simulation is a pure function of the mission file and the input — the premise [`../engine/plan-differential-harness.md`](../engine/plan-differential-harness.md) rests on.

## The presentation generator

`0x4d268f` is the block directly after the simulation's — `0x4d261d + 0x72`, one 56-entry table and its two cursors — and a generator in its own right. The static initialiser `FUN_0045cad8` seeds the two back to back through `FUN_00492d7c` (`0045cbc8`–`0045cbd8`), so both start in the same state and diverge only through their own draws. Every consumer read so far is a sound, a message, a portrait or a cockpit shake, so none of those moves a simulation roll ([Open](#open)).

Its draw sites are the sixteen `PUSH 0x4d268f` in the image besides that seeding one:

| Site | Function | Draw |
|---|---|---|
| `004080ca` | `Explosion_Construct` | `Math_RandomBelow(0x32)`, discarded — the sound it then plays is the type record's `+0x24` plus 10 |
| `00409298` | `FUN_0040923c` | `next & 3`, added to `+0x28 * 4` into the object's `+0x26` |
| `0042f86f` | `FUN_0042f860` | `next`, into the third word of its output ([Open](#open)) |
| `0042f9c7` | `FUN_0042f970` | `next`, one per element into a table at `+0x5a` ([Open](#open)) |
| `0043404e`, `004340cc` | `Cockpit_StartHitShake`, `Cockpit_HitShakeTick` | `(next & 0xffff) % 10`, the palette flash interval |
| `004340ea` | `Cockpit_HitShakeTick` | `(next & 0xffff) % 5`, the shake step |
| `00435cb5` | `FUN_00435c48`, the squad port's post | `(next & 0xffff) % variants`, drawn only for two or more |
| `00436a67` | `MessagePort_PickVariant` | the same |
| `00438d7f` | `FUN_00438d6c` | `(next & 0xffff) % (hi - lo) + lo`, not drawn when `hi == lo` |
| `0044b138` | `HddGauge_PaintPilotFrame` | `next % 3`, discarded — [`../formats/heads-down-display.md`](../formats/heads-down-display.md#the-three-paints) |
| `0044b381` | `HddGauge_PaintScream` | `Math_RandomBelow(0x14)` |
| `0045db2f` | `FUN_0045d840` | `(next & 0xffff) % 5`, a shake step once a frame for `0x1e` coarse ticks |
| `0045dcfb` | `FUN_0045dc34`, the mech-death screen flash | `Math_RandomBelow(10)`, the same kind of shake step |
| `00462753`, `004627ff` | `Sound_Play`, `Sound_PlayAt` | `Math_RandomBelow` over the sound's variation count |

## Ported

`Numerics.SimRandom`. Its default constructor is that starting state exactly — the same table, the same two cursors — so it and the original step in lockstep from there. `MissionScene` builds one instance and hands it to both the terrain pass and `SimWorld`, because DBSIM has a single simulation generator that the zone load draws from before anything else does; two instances would produce the same stream twice rather than one continuing stream.

`SimWorld.PresentationRandom` is the second generator, a separate default-constructed `SimRandom`. `SimWorld.SpawnImpactEffect` makes `Explosion_Construct`'s discarded draw on it, and the host hands it to the sound director, the squad comm channel (its message variants, the scream's roll and the portrait paint's discarded draw) and the cockpit hit shake.

The `SimRandom(int)` constructor is this engine's own device, for a test that wants a pinned stream independent of the retail table.

A roll's result depends on how many draws preceded it, so matching a specific retail roll means matching tick order, not just the seed; any particular roll is statistically faithful rather than replay-faithful today ([Open](#open)).

## Open

- **Open:** match call order (tick order), not just the seed, so a specific retail roll replays exactly rather than only statistically — see [`../../ROADMAP.md`](../../ROADMAP.md).
- **Open:** what `FUN_0040923c`, `FUN_0042f860`, `FUN_0042f970` and `FUN_00438d6c` use their presentation-generator draws for, and whether this engine has a counterpart that should draw. Until they are read, the generator's separation from the simulation rests on the other sites.
- **Unported:** the mech-death sequence, and with it the shake steps `FUN_0045d840` and `FUN_0045dc34` draw on the presentation generator.
- **Open:** whether DBSIM draws from the generator before a zone populates. The terrain scatter is the visible case: if it does, the scatter lands on different cells than this engine's — see [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md).
