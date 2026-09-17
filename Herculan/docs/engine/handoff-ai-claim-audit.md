# Handoff — audit the quantified claims in the `ai-*.md` docs

> **This file is a scratchpad, not a status record.** It is ephemeral. Nothing here is authoritative: the topic docs are. As each claim below is checked, fix the doc if it is wrong and delete the row from this file; when the queue is empty, delete the file.

## What this is

The audit run over [`../simulation/damage-system.md`](../simulation/damage-system.md), applied to the AI docs. It checks **claims, not implementations** — the goal is not to re-read the AI, it is to re-verify the statements other docs and the port are built on.

## The queue

One doc at a time, or two if they are a pair. Each is independent: nothing carries between them except the method below, so a fresh session per doc is the cheapest way to run this and gives each doc full attention.

The counts are `grep -Eio` hits for the claim vocabulary in "Why only these claims", which is a rough density signal, not a to-do count — a single claim often spends several of those words.

Take them in this order. It is not the density order: it front-loads the docs that [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md) leans on, for the reason in "Audit the KNOWN_ISSUES entries with their doc" below.

| Doc | Lines | Claim words | KNOWN_ISSUES entries it owns | Status |
|---|---|---|---|---|
| `ai-dispatch.md` | 246 | 24 | — | Done |
| `ai-combat-states.md` | 411 | 71 | ramming self-destructs on a stale bump; ramming killed by any obstacle | Done bar one hand-off to `ai-weapons.md`; both entries re-verified and hold |
| `ai-navigation.md` | 235 | 59 | `travelling` skips waypoints; `follow` never finishes; slows for nothing it steers around | Done. All three entries hold; `follow` gained the action-path qualifier |
| `ai-weapons.md` | 215 | 41 | front shield whatever the facing; Cybrid aim skewed one way | **Next.** Also settles `ai-combat-states.md`'s aspect hand-off |
| `ai-goals.md` | 151 | 38 | route loaded once; **orderless group would crash, "no shipped mission has one"** | Not started |
| `ai-flyers.md` | 270 | 45 | **flyer `ram`/`guard`/`follow` unimplemented — self-flagged unverified** | Not started |
| `ai-squadmates.md` | 157 | 29 | — | Not started |
| `ai-targeting.md` | 247 | 20 | aim component reads own damage; `rand & 1000` | Not started. Its `flanking` gate paragraph is already corrected — do not re-derive it |

**Do not pre-write inventories for the un-started docs.** Generate each one at the start of its own session with the grep below. A pre-written list goes stale as the doc is edited, and costs more to carry than to regenerate.

## Why only these claims

In the `damage-system.md` audit, every single wrong statement was a negative or a count — "nothing calls", "never cleared", "not deleted", "exactly 5 call sites", "both callers". Not one positive claim ("this function computes X this way") failed. That is structural, not luck: a positive claim is verified by reading one function, and a negative claim requires searching the whole image, which is where corners get cut. So audit the quantified language and leave the descriptions alone unless something else contradicts them.

Build each doc's inventory by grepping it for `nothing|never|no one|only|always|exactly|both|neither|none|unused|dead code|unreachable|no reader|no writer|cannot|impossible|every|all`, reading each hit, and keeping the ones that assert a quantity. Re-run the grep after editing rather than trusting the inventory to stay complete.

That held on the spine: of the 24 claims checked across the two docs, the ten that failed were all negatives, counts or retail data figures. Not one descriptive claim was wrong.

`ai-navigation.md` held the pattern and widened it. Six failures, all negatives or counts: two "never/only" claims about which distance function the layer calls, a state count, a caller count, a "no third reference", and a "never uses one". **Both of its retail data claims survived** — the chassis reverse speed is nonzero on all 21, and the AI cruise speed really is zero in 90.7% of the 1,683 roster records across all 62 `.MSN` files. Its two arithmetic claims about the disassembly survived too. The one shape not yet seen elsewhere: **a claim that reads a field for the wrong meaning.** `Sim_RaycastShapeList`'s filter tests `typeRec+0x06`, which is a machine's top speed *and* a structure's animated flag, and the doc had glossed it as the first where the code wants the second — which then propagated into an engine-port note with its categories inverted. When a doc explains *why* a test is written the way it is, check the field's other readers for what it means there.

## Method

1. `python tools/scripts/es2_xref.py ADDR|SymbolName` for anything about **call sites, callers, or reachability**. It sweeps the whole PE for rel32 branches, stored pointers and vtable slots. Exit 1 means nothing references it.
2. `python tools/scripts/es2_fieldscan.py OFFSET --range LO-HI` for anything about **who reads or writes a field**. Always pass `--range`: offsets are struct-relative, and the same `+0x5a` is a behaviour scratch byte, some UI widget's field and a type record's. The AI lives roughly in `00411000-00424000`.
3. Read the site before believing the tool. A hit only counts if the base register really holds the object you mean.
4. Anything about **retail data values** ("nonzero on 8 of the 65 types", "14000 across the whole fleet") is not a binary question — parse the file, and read "Reading a retail data value" below first. Two of the spine's three data claims were wrong and they were the most consequential errors found.

## Reading a retail data value

Two of the spine's three data claims were wrong, both for the same reason: a figure had been read off the `.DAT` file at the offset the *exe* uses, and the two are not the same number.

**A `.DAT` dumped out of a VOL keeps a 9-byte entry prefix** — one compression byte, four size bytes, four magic. `HercSimDataTransformer`'s doc comment says so and the bytes show it: a chassis file opens `02 d8 00 00 00` and its stated size, 216, is exactly what `MechType_InitOne` reads. `VolFileReader` strips those nine; a file copied to disk keeps them, and the result is plausible-looking nonsense rather than an obvious failure.

So for a chassis: **`typeRec+R` is at file offset `R + 7`** — nine for the prefix, less the two the record body sits at. Three independently-documented fields pin it, and a new figure should be checked against them before it is quoted:

| Anchor | Record | File | Retail value |
|---|---|---|---|
| body radius, `Mech_GetBodyRadius` | `+0x70` | `+0x77` | 750 on all 21 |
| flyer flag, `MechType_InitOne`'s own test | `+0x50` | `+0x57` | set on RAZOR alone |
| torso-twist limit | `+0x22` | `+0x29` | 14000 on 20 of 21 |

`BASES.DAT` has no stride to sample — a fixed head, a nested component array counted at `+0x12`, then a fixed tail. Walk it as `BaseTypeTable.Load` does; the walk validates itself by landing on the last byte (65 records, 6,422 bytes after the prefix).

**And read the loader, not just the file.** `typeRec+0xc8` is zero in every chassis file and never zero at runtime: `MechType_InitOne` (`004202c1`) overwrites it with a copy of `typeRec+0x06`. A field the loader writes is not a record field, whatever the file holds at that offset.

## Audit the KNOWN_ISSUES entries with their doc

`KNOWN_ISSUES.md`'s own preamble asks for skepticism about two shapes of claim Claude files: *this feature is unreachable in retail*, and *no retail mission uses it*. The spine audit confirmed both warnings — `flanking` and `bulldog travel` were entries of exactly that shape, and both were wrong, so both entries are gone.

Twelve of the file's retail entries cite an `ai-*.md` doc, and ten cite a doc still on the queue. Audit them **with** their doc rather than as a separate sweep: the entry is a summary of a claim the doc owns, so checking it means opening that doc anyway, and doing it doc-first amortises the context. The table above says which entries belong to which doc; treat them as mandatory inventory rows on top of whatever the grep turns up.

**Check the port annotation too, not just the retail claim.** "Reproduced as-is" and "Not reproduced in HERCULAN Engine" are testable statements about the engine, and that is where the expensive error was: `MechTypeRecord.FlankingGate` returned a hardcoded `0`, so the port was faithfully reproducing a bug that does not exist. Grep the engine for the claim's own words.

The ~33 entries that cite shell and format docs are a separate programme — those docs have had no audit at all. Schedule it deliberately rather than drifting into it from here.

## What is left

Nothing on the spine, and nothing on navigation. Two claims belong to other docs' audits:

- The combat geometry's aspect passes through `Ai_AimAndFire` (`0041ea7c`) untouched into `Ai_AimAndFireAtMech` or `Ai_FireAtPoint`, so whether `Ai_ChooseWeapon`'s shield-facing test is its **only** consumer is settled in `ai-weapons.md`.
- `Group_OrderTick` (`00423a74`) advances a group past an order whose own action has fired, whether or not the order completed. Navigation's `follow` entry now says so; `ai-goals.md` owns the mechanism and should check its own wording against it.

A warning for the docs still to audit, since it cost time here: **a symbol name cited in a doc can outlive the address it was attached to.** `known_symbols.json` is the arbiter, and it says so — check the name there before treating a failed grep of the dump as a finding. `Bases_LoadTypeTable` was cited by two docs and four code comments and is in no dump; the function is `Base_LoadResources` (`00405fac`).

## Traps

- **Run nothing by grep alone.** The AI reaches its scratch fields through `LEA reg,[mech+0x5a]` and then `[reg+3]`: the write to `mech+0x5d` is literally `MOV word ptr [EBX + 0x3],AX`. A text search for `0x5d` finds none of it.
- **Where a doc describes the scan it used, that scan is weaker than the tool.** `ai-combat-states.md`'s `mech+0x5d` note follows `LEA` rebases and bare displacements only; it misses `ADD reg,imm` rebasing and Borland's spill-a-rebased-pointer-and-reload idiom, both common. That claim survives re-running, but treat any "a scan found nothing" written the same way as unverified until re-run.
- **A pointer handed to a timer is not read.** `Timer_CountDown` (`004679a4`) and `Math_CountdownTimerTick` (`00467944`) both take `p` and touch only what follows it, never `p[0]`. So a "no reader" claim about a byte that doubles as a timer handle is about the handle, not the countdown — this is what made `mech+0x5a`'s claim provable rather than merely unfalsified.
- **`es2_fieldscan.py` runs two alias passes and unions them, and that is load-bearing.** Carrying aliases across basic blocks recovers straddling accesses but silently skews offsets when a base register picked up a stale alias on another path — it drops real hits rather than erroring. Neither pass alone is sound. Do not "simplify" the tool to one pass.
- **Both tools read the disassembly, which has undecoded stretches — including inside AI code.** `Mech_BehaviourDriveOffThink` is 444 undefined bytes, `Flyer_Constructor` 402. A write in one of those is invisible to `es2_fieldscan.py`, and shows as no hit rather than as a warning. Grep `; \.\.\. [0-9]+ undefined bytes` over the range a claim depends on. The fix is to cross-check against `DBSIM_decomp_full.c`, which covers those functions **and** folds Borland's rebase back into the true offset — the read at `mech+0xb1` renders as `*(char *)((int)this + 0xb1)`, so grepping the decompile for an offset is sound where grepping the disassembly for it is not.
- **`es2_fieldscan.py` cannot see a bulk write at all.** It matches `[reg + disp]` forms, so a `memset` or `memcpy` covering the field never appears. Before calling a field unwritten, sweep the image's `memset`/`memcpy` call sites and check the destination and size of the ones on your class — that is the third method the `mech+0xb1` claim rests on, and neither of the other two could have found it.
- **A null result is never proof.** If the field carries a meaningful value, the most that can be said is that no reader was found. Write it that way.

## What to do with a finding

1. **Fix the doc first** — it is what the next port reads. Edit the wrong passage; never annotate it (documentation rule 1).
2. **Code comments get a one-line summary and a link**, not the derivation (rule 5). Grep the engine for the claim's own words; the same sentence is often pasted into a C# doc comment.
3. **If the engine's behaviour actually diverges**, it belongs in [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md), not only in the doc.
4. **Fix `tools/ghidra_scripts/known_symbols.json` too.** Its descriptions are re-emitted into every regenerated decompile dump, so a wrong one regenerates forever. Change the `name` field as well as the description when a symbol turns out to be misnamed — and check `known_vtables.json`, which holds separate copies of some names.
