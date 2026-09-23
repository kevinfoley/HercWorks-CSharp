# Joystick input (DBSIM.EXE)

How a stick reaches the simulation: the device layer that reads the hardware, the twelve bytes of `data\prefs.cfg` that say what each control does, and the per-frame build that turns one into the other. The two panels that *edit* those twelve bytes are [`../simulation/preferences.md`](../simulation/preferences.md)'s; this document owns everything below them. Mouse routing and keyboard command codes are [`cockpit-input.md`](cockpit-input.md)'s.

```
joyGetDevCapsA / joyGetPosEx     FUN_00477568 / FUN_00477614
  -> raw X, Y, Z, R + POV + buttons, two devices merged
  -> FUN_00477750                normalise each axis to +/-0x80
  -> FUN_0045c314                deadzone and the squared response curve, to +/-0x100
  -> FUN_0045ba8c                the device block at DAT_004d247a
  -> Input_BuildPlayerDevice     apply the bindings; write the four game axes and eight buttons
  -> Sim_PollPlayerInput         the control laws, and the button action switch
```

**The bindings name no hardware.** Four bytes say which *pair of game axes* each control feeds and eight say which *action* each button fires. Nothing in the file identifies a device, an axis number or a HID usage — which is why the file survives a change of input backend intact, and why the engine's port needs a side-car of its own for the step retail never had to take.

## Reading the hardware

`FUN_00477568` enumerates with `joyGetDevCapsA` over joystick ids **0 and 1 only**, filling a `JOYCAPS` per device at `DAT_006bb3fc` (stride `0x194`) and stamping each one's `MMRESULT` at `DAT_006bb724`. `FUN_00477614` then polls both with `joyGetPosEx` into `JOYINFOEX` blocks at `DAT_006bb394` (stride 52), `dwFlags` fixed at `0xccf`:

| Bit | Flag | |
|---|---|---|
| `0x001`-`0x008` | `JOY_RETURNX/Y/Z/R` | the four axes, and the only four asked for — **`U` and `V` are not requested**, so a fifth and sixth analogue control is invisible to the original |
| `0x040` | `JOY_RETURNPOV` | the hat |
| `0x080` | `JOY_RETURNBUTTONS` | the button mask |
| `0x400` | `JOY_RETURNCENTERED` | |
| `0x800` | `JOY_USEDEADZONE` | Windows' own band, on top of the game's |

There is no calibration of the game's own: `JOYCAPS`' `wXmin`/`wXmax` are never read, and the axis maths below assumes the full 16-bit range. Whatever the Windows control panel's calibration produces is what the game gets.

**Two devices, merged into one.** A second stick is not a second controller — it fills in whatever the first lacks:

| Field | From stick 0 | Or, when stick 0 lacks it |
|---|---|---|
| stick X, Y | `dwXpos`, `dwYpos` | — |
| throttle | `dwZpos`, if `JOYCAPS_HASZ` | stick 1's `dwXpos` |
| rudder | `dwRpos`, if `JOYCAPS_HASR` | stick 1's `dwYpos` |
| buttons | `dwButtons` | OR'd with stick 1's, shifted left by stick 0's `wMaxButtons` |
| hat | `dwPOV`, if `JOYCAPS_HASPOV` | — |

**The hat has four positions and no more.** `dwPOV` is tested against exactly `0`, `18000`, `27000` and `9000`, setting bits 0-3 of one byte — north, south, west, east. Anything between them, which is every diagonal a modern eight-way hat reports, matches nothing and the byte stays clear.

## Normalising an axis

Two steps, and reading only the first of them is the trap that costs a factor of two.

`FUN_00477750` maps a raw reading onto a signed byte: `(raw << 8) / 0xffff - 0x80`, so an axis arrives spanning `-0x80..+0x7f`. The width comes from the device object's resolution field (`+0x16`), which `FUN_004774d0` sets to 7.

`FUN_0045c314` then applies the deadzone and a **response curve** selected by the object's `+0x28`. The joystick is constructed as `FUN_0045c27c(obj, 3, 0x201, 0xf, 1)` by `FUN_00459dd4` — that last argument is `+0x28`, so a joystick is always **mode 1, the squared curve**:

```
span = (1 << 7) - 0x19                  = 103        // deadzone 0x19 subtracted, not rescaled away
k    = Math_Q16Divide(1, span*span >> 8) = 1/41 in Q16
out  = sign(x) * Math_Q16Multiply(|x|², k)
```

Full deflection is therefore `103² / 41 = 258` — a shade over `0x100`, which the control laws clamp to. **`0x100` is the scale every input source shares:** a held keyboard direction is worth `0x80`, half of it ([`../simulation/mech-locomotion.md`](../simulation/mech-locomotion.md)), and a hat direction `0xc0`.

Modes 0 and 2 are linear and are what the deadzone's other arm (`local_18`, a rescale to `0x80`) is for. Nothing constructs a joystick with either — see [Rejected readings](#rejected-readings).

## The capability block — `Input_QueryCapabilities` (`004777f8`)

Eight bytes rebuilt on every call, and the whole of what any caller is told about the hardware. It is straight `JOYCAPS`:

| Field | Is | From |
|---|---|---|
| `+0` | 2, or 1 when stick 1 enumerated. **Never 0** | `DAT_006bb728`, stick 1's `MMRESULT` |
| `+2` | button count, clamped to 8 | `wNumButtons[0] + wNumButtons[1]` |
| `+4` | has a throttle | `wCaps & JOYCAPS_HASZ`, **or** stick 1 being present |
| `+5` | has a rudder | `wCaps & JOYCAPS_HASR`, or the same |
| `+6` | has a hat | `wCaps & JOYCAPS_HASPOV` |

Field `+0` never being 0 is why the presence test is `FUN_0045c508(3)` — a lookup into the device table at `DAT_004d24ec` — rather than a field of this block. The CONTROLS panel's use of the block is [`../simulation/preferences.md`](../simulation/preferences.md#the-capability-block); the throttle row also reaches `+4` through `Input_SetThrottleLeverMode`.

## Sources and destinations — `Input_BuildSourceTable` (`0045a5c0`)

The four game axes the control laws read are the device struct's `+0x0e`, `+0x10`, `+0x12` and `+0x14` (`DAT_004d2358` onwards): steering, throttle, torso twist, torso pitch. The trigger is the byte at `+0x0d` and the eight button states are `+0x16`..`+0x1d`.

`Input_BuildSourceTable` fills a table of pointers at `DAT_004d2394` with the address of every value that can *feed* one of those — the two keyboard axis pairs `Input_BuildKeyboardAxes` (`0045a4b0`) writes, the four joystick axes, and the buttons — so that a binding is a choice of pointer rather than a hardcoded path. That indirection is the mechanism the whole scheme rests on: the control laws never learn which device moved an axis, and so never need a joystick case.

## Applying the bindings — `Input_BuildPlayerDevice` (`0045a7f4`)

The per-frame input build, and where the twelve bytes are read. `ControlsOptionBase` (`DAT_004d25fb`) selects the walking block or the RAZOR's.

### The four axis rows

Each row's byte is 0, 1 or 2, and **the three values mean the same thing on every row**: 0 is unassigned, 1 points the control at the *movement* pair of game axes (steering, throttle) and 2 at the *turret* pair (twist, pitch). The words `CTL_ALRT.STR` shows differ only because a given control reaches a different half of the pair:

| Row | Feeds under 1 | Feeds under 2 |
|---|---|---|
| JOYSTICK — stick X, Y | axes 0 and 1 | axes 2 and 3 |
| THROTTLE — the Z axis | axis 1 | axis 3 |
| RUDDER — the R axis | axis 0 | axis 2 |

A control whose destination is already spoken for lands in a **second rank** behind it, and the combine prefers the second when both have moved:

```
axis = secondRank  if it is non-zero
     : firstRank   if it is non-zero
     : the keyboard pair
```

So a rudder bound to DIRECTION overrides the stick's own steering while it is being pushed, and hands back when it centres. The same test is what lets the keyboard reach an axis the stick is bound to but not currently moving.

Two sign flips ride along: a RAZOR negates the stick's Y when the JOYSTICK row is 2, and negates the throttle axis unconditionally whenever a throttle exists.

**With the stick on the movement pair the keyboard is moved off it.** When the JOYSTICK row reads 1, the keyboard's first axis pair is copied onto the second and zeroed — the arrow keys aim the turret instead of duplicating the stick — with the pitch half negated on the way.

### The hat

Only under HAT = 2 does the hat drive axes, and then it writes straight over the turret pair at `0xc0`, testing north, south, east, west in that order and stopping at the first:

| Direction | Writes |
|---|---|
| north / south | axis 3 = `+0xc0` / `-0xc0` |
| east / west | axis 2 = `+0xc0` / `-0xc0` |

Under HAT = 1 the four bytes are left alone and reach `CockpitView_PollViewDevice` (`00432b14`), which queues view commands 1, 0, 5 and 4 off them — up, down, and the two outside-view steps. Under HAT = 0, and after the axis write under HAT = 2, they are zeroed, so the view path sees nothing.

### The buttons

Each of the eight destination bytes takes its device button OR'd with whatever key is bound alongside it. Two things then happen to them.

**The trigger is pulled out first.** The block is scanned for the row bound to action code 1, FIRE; that button's state is copied to the device struct's `+0x0d` and **its own byte is zeroed**, so it never reaches the dispatch below. The trigger is a held state polled every frame, not a command, which is why no scancode case for it exists anywhere — see [`../simulation/weapon-mounts.md`](../simulation/weapon-mounts.md).

That scan reads `SimOptions[0x11 + i]` — a **literal** `0x11` at `0045b22b`, the walking block's first button row, where the dispatch one step later correctly uses `ControlsOptionBase + 4`. In a RAZOR the trigger is therefore found through the walker's bindings.

**Everything else is press-once.** `Sim_PollPlayerInput` (`00460764`) walks the eight bytes and switches on `SimOptions[ControlsOptionBase + 4 + i]`; acting on one calls `FUN_0045b718`, which latches it, and the next `Input_BuildPlayerDevice` masks that button to zero until the player lets go. Holding a button repeats nothing.

The switch's twenty cases, against `CTL_ALRT.STR` group 2's names:

| Code | Name | Does |
|---|---|---|
| 1 | `FIRE` | never reaches the switch — see above |
| 2 | `TARGET` | `TargetSelect_Cycle`, what [Enter] does |
| 3 | `CENTER LEGS` | latches Center Body and caches the turret's heading |
| 4 | `CENTER TURRET` | latches the centring mode, [Backspace] |
| 5 | `CHANGE DIRECTION` | flips `ThrottleLeverMode` between ±1, gated on there being a lever bound to the throttle |
| 6 | `ATT TOGGLE` | dispatches command `0x14` |
| 7 | `ALL STOP` | throttle to zero, and the gauge dirty flag |
| 8 | `TARGET NEAREST` | `TargetSelect_Nearest`, ['] |
| 9, 10 | `SHIELDS FRONT`, `SHIELDS REAR` | mech commands `0x1a` / `0x1b`, the bracket keys |
| 11 | `HDD VIEW` | scancode `0x41` (F7) or `1` ([Esc]) to the widget tree — but see [below](#hdd-view-can-only-leave) |
| 12, 15 | `OUTSIDE VIEW`, `CHASE VIEW` | step `DAT_004d2572` through the external views; 15 is gated on `DAT_004d25ff` |
| 13 | `LINK WEAPON` | presses the console LINK button, scancode `0x26` |
| 14 | `MFD DISPLAYS` | `FUN_00446e14` — step the MFD's mode, wrapping at six |
| 16 | `WEAPON TOGGLE` | weapon command `0x202`, which is `ToggleChainMember(0)` — **row 1's chain membership, not a general toggle** |
| 17 | `COCKPIT VIEW` | scancode 1 to the widget tree |
| 18 | `NEXT CHAIN` | presses the console chain button, scancode `0x29` |
| 19, 20 | `NEXT WEAPON`, `PREV WEAPON` | weapon commands `0x11` / `0x211`, [W] and [Alt]+[W] |

Code 0 is `OFF`, which a row displays when its byte is zero and which the switch has no case for.

#### `HDD VIEW` can only leave

The case picks between F7 and [Esc] on `CockpitViewManager_Published` (`00429820`), and it tests **the pointer, not a field of it** — so it asks whether the cockpit view manager exists, not which view is up. `CockpitViewManager_LoadViews` publishes that pointer while the cockpit is being built and nothing ever clears it, so by the time `Sim_PollPlayerInput` runs it is always non-null and the button always sends [Esc]. A button bound to `HDD VIEW` can therefore leave the heads-down display and never enter it; see [`../../KNOWN_ISSUES.md`](../../KNOWN_ISSUES.md). The two branches read as the toggle the action's name promises, which is what the engine implements.

#### One action per tick

**At most one action fires per tick.** The loop keeps a 21-entry array to stop two buttons bound to the same action acting twice, but indexes it with the *button byte* — always 0 or 1 — rather than with the action code (`MOV AL, byte ptr [EDI]` at `00460c0f`). The first pressed button claims the only usable slot. It costs little, because that button is latched immediately and the next tick lets the one behind it through: two buttons pressed together act one tick apart.

## `data\keyjoy.cfg`

The only part of the input configuration outside `prefs.cfg`. `FUN_0045b78c` reads it once with four `GetPrivateProfileStringA` calls against section `[Keyjoy]`, each a case-insensitive compare against the word `Reverse` — anything else, the shipped `Default` included, leaves the flag clear. Nothing writes it; retail ships it with its own explanatory comments for the player to edit.

| Key | Global | Inverts |
|---|---|---|
| `Tilt` | `0049eab8` | the **keyboard's** turret-pitch axis, unconditionally |
| `Backturn` | `0049eabc` | the steering axis while the throttle axis is positive — while backing up. Applied last, to the combined axes |
| `Missile` | `0049eac0` | the pitch axis inside the missile camera (`DAT_004d25aa`) |
| `Rudder` | `0049eac4` | the joystick's rudder axis, and only when the device reports one |

## Engine port

### Axis order is not axis meaning

The host's `JoystickSource` reads the stick through Silk.NET, and **its axes are positional**: GLFW hands over an ordered array of floats with no HID usages attached. Nothing in that distinguishes a throttle from a twist grip; the order is whatever the driver enumerated, and on a HOTAS those two commonly come out swapped against the 1996 convention the twelve bytes assume.

**No API fixes this.** HID has no "throttle" usage — a vendor labels a throttle `Z`, `Rz` or `Slider` as it pleases — so `joyGetPosEx`'s named fields, DirectInput's object names and Raw Input's usage pages all just relay that choice. The original does not notice because a 1996 gameport stick had X, Y, Z and R and nothing else. Which physical control is which is a question only the player can answer, which is what `data\herculan-joystick.cfg` and `--joystick-probe` are for, and what a real axis assignment step would be for if this ever grows one.

Resting position does not identify a lever either: a throttle parked mid-travel reads 0.00, the same as a self-centring twist.

**Check the mode switch on the base of a HOTAS before reading anything into its numbering.** A Thrustmaster T.Flight's PC position numbers buttons and axes the way the retail format expects — the trigger is button 1 and the throttle is the throttle — and its PS3 position renumbers both, which looks exactly like the engine inferring the wrong convention. `Herculan/examples/herculan-joystick.cfg` is a worked map for that stick in PC mode.

**Two GLFW behaviours shape the rest.** It publishes a fixed sixteen joystick slots and leaves the unused ones reporting `IsConnected` false, so a device must be chosen by connectedness rather than by index; and a connected device reports **zero** axes, buttons and hats until the first `Update`, the counts arriving a frame after `CreateInput`. So the device map is derived lazily from whatever the device reports now and re-derived when that changes — deriving it at load time maps nothing at all, which leaves every CONTROLS row greyed but the JOYSTICK one, that being the row with no capability byte of its own. `ControlsPanel.Capabilities` refreshes its readouts when it is set, for the same reason: a panel opened before the shape arrived would keep the blanks it was built with.

`Input.JoystickReading` is the abstract device block and the normalisation; `Input.JoystickBindings` is the binding resolution, the press-once latch and the axis combine; `Input.JoystickAction` and `Input.JoystickAxisAssignment` are the two code sets; `Input.PilotAxes` is the four game axes. `Input.KeyjoyConfig` is the side file. The host's `JoystickSource` is the Silk.NET half — enumeration and polling, in place of `joyGetPosEx` — and `Program.cs` dispatches the action codes into the same handlers a key or a click reaches, as the original's switch does.

**A modal takes the stick.** `Sim_PollPlayerInput` is reached only from `Sim_MainTick`, and each of the four alert panels runs a loop of its own that never calls it — so while a panel is up the action switch does not run, no axis reaches the control laws, and the twelve bytes can be rebound with the stick without any of it touching the machine. The engine suspends the whole pilot-input path, keyboard included, for as long as any panel stays up, and refreshes every edge latch while it does, so nothing fires as the panel closes.

`--joystick-probe` prints each axis and button as it moves and `--write-joystick-map` writes the map in force out to the install, which together are how a stick is configured. Both are off by default, the map file being the player's to own. A rebinding made on the CONTROLS panel reaches the install's own `prefs.cfg` as that panel closes, which is retail's own timing — `--no-write-prefs` is the way out of it.

### `data\herculan-joystick.cfg` — this engine's invention

`Input.JoystickDeviceMap`, and the one layer of configuration the original has no equivalent of. Retail reads X, Y, Z and R in that fixed order and never asks which physical control any of them is, which works because a 1996 gameport stick had exactly those controls in exactly that order. A modern HOTAS does not: its twist is as likely to be axis 2 as its throttle, and it can carry six axes and thirty buttons.

So the split is that `prefs.cfg` keeps owning what each control **does**, byte for byte as retail wrote it, and this owns which piece of hardware each control **is**. Retail never reads it. It is an INI in `keyjoy.cfg`'s shape, and when it is absent the device's own reported counts supply retail's order as the default.

**What the retail format still cannot express**, and so neither can this: a fifth axis, a ninth button, a second hat, or a hat diagonal. Those are limits of the twelve bytes, and keeping the file readable by the retail simulator means keeping them.

A *fifth axis* is the one of those the map does reach, because an axis number is the map's own and not the format's: `Rudder = 4` reads the device's fifth axis into the rudder slot, which is how a HOTAS whose paddles and twist grip are separate controls gets the paddles rather than the twist. What cannot be expressed is a fifth axis *as well as* the other four — there are four slots, and naming a sixth control means giving one of them up.

#### `BipolarThrottle` — the centre-zero lever

**This engine's invention.** Retail's lever is end-to-end: `Mech_ApplyThrottleInput` reads it as `|axis - 0x100| x 2`, so idle sits at one stop, full at the other, and the whole travel is spent on one direction. Which direction is `ThrottleLeverMode`'s sign, a global that `CHANGE DIRECTION` and the cockpit slider flip; the axis has no say in it, and the rate path's clamp closes to the same side of zero so nothing else can cross it either. That is the right shape for a 1996 gameport throttle, which had no centre detent to make anything else meaningful.

With the setting on, the lever is read `-axis x 4` instead: the middle of the travel is idle, forward of it is forward and aft of it is reverse, each half covering the whole range. The negation is the axis' own sense rather than a choice — **negative is forward on this axis throughout**, which is why the rate path steps by `Q8(rate, -axis)` and why retail's arm measures full throttle at `-0x100`. The deadband and the mode's sign are unchanged, so `CHANGE DIRECTION` still reverses the lever bodily — of no use in this mode, but it costs nothing to leave working. The clamp keeps both limits, as it does with no lever at all.

The mode travels as the *magnitude* of `ThrottleLever` (`MechControls.ThrottleLeverUnipolar` and `ThrottleLeverBipolar`), leaving its sign to mean what it always did. The flyer needs none of this: `FlightPhysics.Step`'s analogue arm is already signed across the whole range.

### Divergences

- **The trigger is found in the current block**, not always the walking one. See KNOWN_ISSUES.
- **`OUTSIDE VIEW` and `CHASE VIEW` do nothing**: the engine has no external view chain to step, and approximating `DAT_004d2572`'s four states would be invention rather than a port.
- **`HDD VIEW` toggles**, where the original's test of the view-manager pointer leaves it able only to leave ([above](#hdd-view-can-only-leave)). The engine sends the branch the current view calls for, which is what the two branches were plainly meant to be.
- **A hat diagonal can be made to resolve** into its two cardinals, which retail never does. Off by default.
- **A throttle lever can be read centre-zero**, reaching reverse without `CHANGE DIRECTION`. Off by default; see `BipolarThrottle` above.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| The deadzone is a linear rescale, so a joystick axis spans `±0x80` | That is response mode 0/2's arm, and `FUN_0045c314` skips it for mode 1. `FUN_00459dd4` builds the joystick with mode 1, the squared curve, whose full deflection is `103²/41 = 258`. At `±0x80` a stick would turn at the keyboard's rate rather than twice it, and a throttle lever could neither idle nor open fully, `Mech_ApplyThrottleInput`'s absolute read being `\|axis − 0x100\| × 2` |
| A binding byte names an axis or a button on the device | It names a *destination* — which pair of game axes, or which action code. The device's own layout is fixed in the reading code and stored nowhere |
| The four axis rows each have their own meaning for 0, 1 and 2 | The numbers are the same three destinations on all four rows; only the words differ, because a one-axis control reaches half of a pair and the stick reaches both |
| The hat's VIEWS setting is dead because nothing reads `DAT_004d2368`-`236b` by name | `CockpitView_PollViewDevice` reads them off the device-struct pointer `Sim_PollPlayerInput` hands it, at `+0x1e`..`+0x21`, which produces no direct address reference |
| A second joystick is a second controller | It is a donor. Its X and Y stand in for a throttle and rudder the first stick lacks, and its buttons are OR'd into the first's mask |
| A `winmm` backend would identify the throttle, `dwZpos` being semantic where an ordered array is not | It is not: for a device whose `JOYCAPS.wCaps` reports only X, Y, Z and R — `0x33` on a T.Flight — GLFW enumerates those same four in that same order, so winmm's `Z` *is* GLFW's axis 2 and the mapping is identical. A platform-specific dependency for no behavioural difference |
| `Input_QueryCapabilities`' `+0` says whether a stick is present | It is 1 or 2 and never 0. Presence is `FUN_0045c508(3)`, a lookup in the device table |
