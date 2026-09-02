# Missile lock

Ported in `MechObject.MissileLockTick`, `WeaponMounts.MissileLock`.

## `manager+0x0a` is the lock state, not an ammunition count

Its reader is named `Mech_MissileAmmoCount` (mech vtable `+0x6c`, `004155ac`) and the name is wrong — <!-- doc-lint: ok -->
it counts nothing. The array is **five flags, one per `PROJ.DAT` missile subtype: has this class of
launcher achieved lock on the machine's current target.**

`Mech_PerTickSystemsUpdate` (`0041aa5c`) clears all five at the top of its target block and sets the
ones whose countdown has expired, for **every** mech each tick, the player's included. That is why a
player's missiles lock in retail, and why `Rocket_Fire`'s gate on it is a real gate.

The genuine ammunition count is a separate local array built by `FUN_0040fbdc`, which walks the
mounts calling each one's vtable `+0x60` (`WeaponMount_GetAmmoType`, `0040e644`) and accumulates its
out-parameter (`mount+0x7b`, rounds remaining) into `rounds[subtype]`. A subtype with no rounds gets
no lock timer.

## The mechanism

Each subtype the machine carries rounds for has its own countdown. Every tick the block either
**reloads** it — holding it at full so no lock can form — or lets it run; reaching zero latches the
flag.

- **Reload value** is `min(range >> 2, 0x7fff)`, so lock time is linear in range. A turretless
  chassis (type record `+0x50`, file offset 78, `InputFlagFlyer` — the RAZOR) uses 0 and locks
  instantly, and measures its bearing without a twist it does not have.
- **The whole block is held** when `mech+0x9d` is set (target just changed), the bearing error is
  outside ±`0x3000` of the turret centreline, or line of sight is broken. That branch resets the
  timers for subtypes 0, 1 and 2 — **not 4**, which keeps a partial lock across a broken moment — and
  clears `mech+0x9d`, which is what makes a target switch cost exactly one tick.
- Line of sight is asked from whichever end owns the cache row: human side asks about the target,
  Cybrid asks the target about itself.

| Subtype | Timer | Manager slot | Hold condition |
|---|---|---|---|
| 0 | `+0x258` | `+0x0a` | own scanner off |
| 1 | `+0x25b` | `+0x0c` | none — locks on sight |
| 2 (ARM) | `+0x25e` | `+0x0e` | **target** silent (`+0x96` and `+0xa1` both clear) |
| 3 (EO) | — | `+0x10` | never set; the pilot flies it |
| 4 | `+0x264` | `+0x12` | own scanner off |

Subtype 2's inverted condition is the anti-radiation missile: it locks *because* the target is
emitting, on the same pair of flags its guidance homes on.

## ECM

A target that is a HERC with its jammer (`+0xa1`) on re-rolls `mech+0x9c` whenever `mech+0x267`
expires: `(rand & 0xfff) < 0x14 * 0x29` — about 20% — holding for 5000 on a spoof and re-rolling
after `0x5dc` otherwise. While the flag stands, no subtype but 2 can complete a lock. It is the same
flag that makes a missile already in the air weave.

The original scales the weight to a quarter when `mech+0x30b` is present and its `+0x7f` is under
`0x33`. That slot is the **targeting computer** pod's mount (catalog id 29 — see
[`reactor-energy-pool.md`](reactor-energy-pool.md)); what `+0x7f` means on a pod mount is untested,
since on a weapon mount it is the energy charge rate. The port always uses the base weight, which
makes ECM at most as strong as the original's, never more.

## The lock lamp

`mech+0x9b` is set from the armed mount's own class: a launcher lights its own subtype's flag, a
mount that is not a launcher (class 5) lights if *any* subtype has lock. `Mech_LockTonePlay`
(`0041b0bc`) turns it into the cockpit's lock audio, for the locally-piloted machine only:
`Sound_Play(0x15)` once per phase of a `0x40`-coarse-tick blink while set, `0x14` when clear but the
target changed this tick, `0x16` once on loss. Two latches carry it — `0049a1d1` remembers that a
lock was held so its loss is announced once, `0049a1d0` that this phase's beep has sounded.

The loss branch **returns before** the target-changed test, so switching target while locked plays
the loss tone and not the acquisition blip. Ported as `MechObject.LockToneTick`.
→ [`../formats/audio.md`](../formats/audio.md)

## Verification

`SAV/script1.dat`, APOCA with two ARH (subtype 1) launchers: target at 20000 units locks after 63
ticks against a predicted `20000 >> 2 / 81` = 61.7; at 57205 units, 178 ticks against 176.6.

## Engine port

Runs from `SimWorld.Tick` after `Detection.Tick`, matching `Sim_MainTick`'s order — the gate reads
the line-of-sight cache that pass maintains. The reactor and shield half of
`Mech_PerTickSystemsUpdate` stays in `MechObject.PowerTick`, where its inputs are last tick's.

The five timers are one array indexed by subtype where the original has four separate fields; slot 3
is unused, as the original has no field for it.
