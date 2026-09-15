# Handoff — structure and ground-vehicle behaviour

> **This file is a scratchpad, not a status record.** It is ephemeral: it may hold the newest lead
> before that lead reaches a topic doc, but it is never the authority on what is or is not done.
> For that, read the topic doc that owns the subsystem, or `KNOWN_ISSUES.md` for behavioural
> divergences. Anything here that becomes settled should move out into a topic doc and be deleted
> from this file.

What *is* settled is in [`../simulation/structure-behaviour.md`](../simulation/structure-behaviour.md),
which owns the `+0x18` tick slot, the five structure classes, the animation threads, the turret seek
and the ground vehicle's move half. The outstanding-work entries are in `ROADMAP.md`. Every function
named below is already named, described and applied in `known_symbols.json` — read those plate
comments in Ghidra before re-deriving anything.

## What is left

One of the five classes, and one render gap.

### 1. The triple turret — `Base_ArmedThinkTick`'s sibling at `004045c8`

Type `0x22` only. Fully read; the transcription is in the topic doc. The one thing it needs that is
not in any file is **`DAT_004a9640`, an 11-`short` per-entry weapon descriptor table** — dump it from
the data section. It also wants `WeaponMountTemplate_GetByWeaponId(8)` reachable.

Note that type `0x22` states **no animation threads** (`BASES.DAT +0x06` is 0 for it) and draws from
the static library, so whatever it aims, it does not aim it by seeking a thread the way the armed
tower does. Do not assume the turret-seek path applies here without checking what the tick actually
does with its three turret angles.

It writes `+0x1a4` too, but only across its `Rocket_Fire` call, so whatever it does with the field is
not a held selection — unlike the armed tower's, whose target is a real one every generic reader can
see. The boundary of what a tower's AI is and is not is in
[the topic doc](../simulation/structure-behaviour.md#what-the-turrets-ai-is-and-is-not); check there
before adding to it.

### 2. Nothing draws a structure's animation

The simulation poses the nodes and the shots leave the right muzzle point, but a tower's turret does
not visibly turn and a radar mast's dish does not sweep. `Herculan.Engine.Host` draws per-node
segments only for a `MechObject` (`animatedKeys`, and `MissionScene.PosedTransformOf`, which takes a
`MechObject`), and `SceneModelLibrary.Base` builds a structure model `celled` rather than `segmented`
because a structure needs its cells for damage states. A shape has to be split **both** ways at once
before this can work; the sim side is ready for it and `PosedTransformOf` only needs widening to
`SimObject`, which now carries `Shape` and `NodeTransform`.

A mobile ground vehicle now leans with the ground it drives over, which the renderer does follow — its
frame is `WorldFrame` like every other object's — so that half needs nothing.

### 3. `MissionLoader` cannot read a `.MSN`

Not structure work, but it is what stops the ground vehicle's follower arm being exercised at all: the
only mission the engine can load is the `script.dat` handoff, whose single ground vehicle is parked by
the class gate. Every `.MSN` in `ZONES.VOL` throws out of the loader. Until that is fixed, a convoy
can only be built by hand.

### 4. Timer constants are mislabelled "milliseconds" outside this subsystem

`Math_CountdownTimerTick` and `Timer_CountDown` subtract `SimTickDelta`, which is Q8 with 1.0 = 125 ms — so one count is about 0.49 ms and a reload of 10000 is about 4.9 seconds, not ten. `SimMath` and the structure docs now say so, and `BeamTracer` and `BulletCatalog` already did, but several files still call these counters milliseconds and so overstate every interval by about two:

- `Sim/Ai/BehaviourState.cs` and `Sim/Ai/FlyerBehaviourState.cs` — the descriptor dwell and its jitter
- `Sim/FlyerObject.Ai.cs` — the sweep interval and refire delay
- `Sim/MechObject.Ai.cs` — the retarget and friendly-fire cooldowns
- `Sim/WeaponMount.cs` — the refire countdown

The *code* is right everywhere (it subtracts `TickDelta`); only the prose is wrong. Sweeping it means checking each doc that quotes a figure in seconds — `ai-dispatch.md`'s dwell table especially, where a 50000-unit dwell is 24 s and not 50.

## Method notes that cost time here

- **Do not invent a term that fights its ordinary English meaning.** This family was called
  "emplacement" for months; an emplacement is a fixed position, and these are the one structure
  class that drives around, so every sentence about them read as a contradiction. They are
  `GroundVehicle` now. When the original names nothing, pick a word that survives being read by
  someone who does not already know what it means.
- **A constant's unit is a finding, not an assumption.** Reading the countdown reloads as
  milliseconds made every interval in the port twice its real length on paper. The value was always
  applied correctly in code, so nothing misbehaved and nothing caught it — only comparing a stated
  figure against the retail game did. `SimTickDelta` is Q8 of 125 ms; see `SimMath.TickDelta`.
- **A scalar search for a struct field offset is not sufficient, and fails silently.** Both
  instructions touching `obj+0xb1` do `ADD reg, 0x92` first and then address `[reg + 0x1f]`, so
  `ES2FindFieldRefs 0xb1` finds neither of them while still returning a confident-looking 64 hits
  from unrelated classes. Use `ES2FindImmediateRefs` over the whole program instead — the decompiler
  folds the rebase, so the offset renders — and cross-check the neighbouring offsets for a wider
  write that would straddle the byte without naming it. Treat "nothing else touches this field" as
  unproven until both have run.
- **A function can be reached through a table that is empty in the image.** `Subsystem_RunPhase`
  (`00401d94`) calls every entry a C++ static initialiser registered with `RegisterSubsystemLoader`,
  by phase id. A function reached only that way has **no `E8` caller, no `E9` tail jump, and no raw
  dword occurrence anywhere in the file**, because the table is filled at runtime — all four of the
  scans above report nothing, in agreement, and are all wrong. `LAB_0041544c` looked completely dead
  by every static measure and in fact runs once a frame. When a search says a function is
  unreferenced, check whether a nearby static initialiser registers it before concluding anything.
- **Check which types actually reach a tick slot before porting it.** Eight `BASES.DAT` types state
  an animation cell sequence, but only two of them reach `Base_ThinkTick`; for the other six the same
  cell array is a muzzle flash stepped from their own firing paths. An ungated port leaves those
  six flashing permanently. `Base_Construct`'s switch is the authority and is transcribed in
  `BaseObject.Classify`.
- **A vtable slot can be dispatched on the asker, not the candidate.** `Ai_SelectTarget` calls
  `+0x4c` on the object *doing* the asking. The structure and flyer tables install a bare `return 1`
  there, so a tower and a Cybrid aircraft score every candidate against the middle column of the four
  weight tables — which is nothing like the `return 0` fallback the port had assumed. Read the
  asking object's table, not only the candidate's.
- **A field the type table skips can still be the one that matters.** `BASES.DAT +0x06` was read as
  "0 selects the static library" and `+0x20` was an unread four-byte skip. `+0x06` is a thread count
  and `+0x20` is the pair of playback rates those threads start at — which is what actually turns a
  radar dish, a thing the doc had credited to the cell flipbook that all four radar types state `-1`
  for. The constructor's *tail*, below its five-case switch, is where that lives; a read that stops
  at the switch misses it.
- **A helper can reach past its caller for the object it works on.** `Formation_RotateAndAddOffset`
  (`00411d64`) is handed a position and an offset, and takes the *heading* off the group's member
  array slot 0 rather than off either argument. Reading it as "rotate by the anchor's heading" is
  right only while the group's first-claimed member is also the one leading it, which for an
  ground vehicle convoy stops being true the moment the lead vehicle is destroyed.
- **A `SAR reg, 0x64` is a shift by four.** x86 masks the count to five bits and the decompiler
  prints the folded value, but the raw disassembly reads like a shift by 100 and invites a "this
  zeroes the field" conclusion. Two of the ground vehicle's three steering divisions are written that
  way.
