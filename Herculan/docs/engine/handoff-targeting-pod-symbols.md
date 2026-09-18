# Handoff — naming the targeting pod's three functions

Scratchpad for one small naming job. Nothing here is settled; the reading below is one pass over the
decompilation and has not been through `Check-Symbol.ps1` or a second look. Delete the file once the
three entries are in `known_symbols.json`.

## What it is

`docs/simulation/damage-system.md`'s `+0x58` section cites the targeting pod as
`` `Player_ResolveTargetAimPoint` (`FUN_0040e4dc`, `0040e530`) ``. That citation is well-formed —
the name is the caller's (`0041b728`, registered), `0040e4dc` is the pod helper it calls and
`0040e530` is the `CALL dword ptr [EAX + 0x58]` **inside** that helper, which is the call site the
paragraph is about. Nothing there is wrong.

What is missing is that the helper and its two siblings have no names at all, so a paragraph about
them can only spell them `FUN_`. Three functions, one caller each:

| Address | Caller | Body |
|---|---|---|
| `0040e484` | `Player_PerFrameCockpitUpdate+0x26b` | writes `pod+0x7d` |
| `0040e4ac` | `0040e4dc` only | writes `pod+0x7d` from the target's vtable `+0x80` |
| `0040e4dc` | `Player_ResolveTargetAimPoint+0x51` | resolves the aim point |

`es2_xref.py` gives each exactly one rel32 branch, no stored pointers and no vtable slots, so these
are ordinary direct calls and not slots anything dispatches through.

## What they do

`0040e484(pod, target)` sets `pod+0x7d` to 0 when the target exists and its `TargetClass`
(`obj+0x1a8`) is 0, and to `-1` for everything else. Its caller runs it immediately after the
player's selected target (`mech+0x1a4`) is reassigned, on `equipmentPods[1]` — so it is the
"new target selected, reset the lock" call, and `-1` is what fences the `+0x58` slot off from
anything that is not a mech.

`0040e4ac(pod, target)` asks the target's vtable `+0x80` for a component index, passing the current
`+0x7d` and `pod+0x86`, and stores the answer back at `+0x7d`.

`0040e4dc(pod, target, outPoint, ?, outHasComponent, outComponentId)` is the pod half of
`Player_ResolveTargetAimPoint`. With no lock (`+0x7d < 0`) or `+0x7f` above `0x9a` it takes the
target's vtable `+0x24` aim node, falling back to the raw origin, and reports 0 through
`outHasComponent`. Otherwise it queries the target's `+0x84`, calls `0040e4ac` if that returns 0,
reads the component position through `+0x58` (the call at `0040e530`), and reports 1. Above
`+0x7f > 0x68` it also runs a countdown at `+0x81`; on expiry it reloads `+0x82` with 5000 and drops
the lock back to `-1`.

## Proposed names

Provisional, and the reason this is a handoff rather than three register entries:

| Address | Proposed | Confidence |
|---|---|---|
| `0040e484` | `TargetPod_ResetComponentLock` | good — the body and the caller agree |
| `0040e4ac` | `TargetPod_AdvanceComponentLock` | good |
| `0040e4dc` | `TargetPod_ResolveAimPoint` | the shape is clear, the prefix is not — see below |

## Traps

- **`+0x7d` and `+0x86` are not obviously the same thing.** `+0x7d` is what `0040e484` resets and
  what gates the whole branch; `+0x86` is what gets passed to `+0x84`/`+0x58` and copied out as the
  component id. A name that treats them as one field would be worse than `FUN_`. Settle which is the
  lock and which is the query index before writing any of these down.
- **The prefix may be wrong.** Every named neighbour in `0040e0` – `0040e6` is `WeaponMount_*` or
  `ElfMount_*`, and `0040e40c`/`0040e434`/`0040e45c` immediately above are three byte-identical
  per-subclass registrars (`FUN_004321d4(CockpitViewInstance, template+7, this)` → `this+0x79`). So
  the pod is a member of the mount family sharing its translation unit and much of its layout, and
  `TargetPod_` may be inventing a class that the original did not have. Check the class registry
  dump before committing to a prefix.
- **`0040e4dc`'s fourth parameter.** `Player_ResolveTargetAimPoint` passes `*(mech+0x20e)` — the
  per-slot weapon-mount index array — and the decompiled body shows no read of it. That is not the
  same as it being unread; run `es2_fieldscan.py` rather than trusting the decompiler's parameter
  list, and if it really is dead, say so as a finding rather than letting the name imply it.
- **`+0x7f`'s two thresholds, `0x9a` and `0x68`**, are unexplained. Both look like a quality or
  range figure rather than a count. Whatever it is will probably name the field, and may name the
  function.

## When the names land

Two lines to fix: `docs/simulation/damage-system.md:229-230`. Nothing else in `Herculan/docs` or
`Herculan/src` mentions any of the three addresses. Then the rest of the usual workflow — the
entries into `known_symbols.json`, then `ES2ApplySymbolNames.java`.
