# Handoff — preferences, controls and joystick input

Scratchpad. The authoritative docs are
[`../simulation/preferences.md`](../simulation/preferences.md) for the panels and
[`../formats/joystick-input.md`](../formats/joystick-input.md) for everything below them; anything
here that outlives the next session should be drained into one of those and deleted from here.

## State

Done: both panels' layout, text and cycling; reading and writing the install's own `prefs.cfg`
(byte-exact, opt-in behind `--write-prefs`); the joystick path from Silk.NET through the device map
and the twelve binding bytes to the axes, the trigger and the action dispatch.

Verified on real hardware (a Thrustmaster T.Flight HOTAS, 4 axes / 17 buttons / 1 hat as GLFW reports
it): the CONTROLS panel lights every row and reads the install's own bindings, and the binding
resolution routes axes, hat and button edges correctly for a given device-axis assignment.

### The open problem: which axis is the throttle

**Silk.NET reports axes positionally and nothing says what any of them is.** On the T.Flight the
throttle and the rudder come out swapped against the order the retail format assumes, so the stick
only works once `data\herculan-joystick.cfg` has been written by hand.

This is not a bug with a fix hiding behind it, and the next session should not go looking for one:

- **No API answers it.** HID has no "throttle" usage; a vendor labels a throttle `Z`, `Rz` or
  `Slider` as it likes. `joyGetPosEx`'s named fields, DirectInput's object names and Raw Input's
  usage pages all relay the vendor's choice rather than a meaning.
- **A `winmm` backend was tried and reverted.** The theory was that `dwZpos`/`dwRpos` are semantic
  where an ordered array is not. They are not: for a device whose `JOYCAPS.wCaps` reports only
  X, Y, Z and R — `0x33` on the T.Flight — GLFW enumerates those same four in that same order, so
  winmm's `Z` *is* GLFW's axis 2 and the mapping is identical. Reverted as a platform-specific
  dependency for no behavioural difference. Do not retry it on the same reasoning.
- **Resting position does not identify a lever either.** A throttle parked mid-travel reads 0.00,
  the same as a self-centring twist. Confirmed on the T.Flight.

What is left is what every flight sim does: let the player assign axes. Two routes, neither started:

1. **An in-game assignment step.** The player moves a control and it is captured. This is the real
   answer, and it means a UI of this engine's own — the retail CONTROLS panel has no room for it, its
   four axis rows binding a *role* to a fixed control rather than naming any hardware. It would also
   want gameplay-side work, since the four game axes are reached through the retail bindings and not
   directly.
2. **A passive self-centring test.** Watch each axis over a session; one that returns to centre when
   released is a twist, one that stays put is a lever. Reliable once both have been moved, useless
   before, and it would want writing back to the map file.

Until then `--joystick-probe` prints each axis and button as it moves and `--write-joystick-map`
drops an editable file, which is the whole of the configuration story.

### Also not done

1. **Applying a setting live.** The handler table at `004d2060`, five entries, registered by
   `Prefs_Init` (`0045a19c`) via `FUN_00459c58`: options 0, 1, 2, 8 and 0x0e take `00459c98`,
   `00459c6c`, `00459cc4`, `00459d4c` and `Input_SetThrottleLeverMode`. Only the last is decoded.
   `Prefs_SetOption`'s third argument is what calls them. The controls block does not need this —
   the input layer re-reads it every tick — so it is the four sound and detail rows that are
   affected.
2. **The revert path.** `Prefs_SetOption` saves the outgoing byte to the shadow array at `004d2028`
   and nothing models it. Which panel button reads it back is untraced; `FUN_004574cc` (the
   preferences panel's DONE) and `FUN_00459140` / `FUN_00459878` (the controls panel's) are the
   three candidates, none decoded.
3. **The outside-view chain.** `DAT_004d2572`'s four states, stepped by `Sim_PollPlayerInput`'s
   cases 0x0c and 0x0f. Two joystick actions are inert until it exists — it is in ROADMAP.

## Where things are

| | |
|---|---|
| `Content.SimulatorPreferences` | the file, plus `Set` / `Step` / `Toggle` / `Save` |
| `Content.PreferencesPanel`, `Content.ControlsPanel` | text, state, click rules |
| `Content.PreferencesPanelLayout`, `Content.ControlsPanelLayout` | geometry |
| `Content.JoystickCapabilities` | the `Input_QueryCapabilities` block |
| `Input.JoystickBindings` | the twelve bytes applied — axes, trigger, button edges |
| `Input.JoystickDeviceMap` | hardware to abstract slot; this engine's own side-car |
| `Input.JoystickReading`, `Input.PilotAxes`, `Input.JoystickAction` | the device block and the two code sets |
| `Input.KeyjoyConfig` | `data\keyjoy.cfg` |
| host `JoystickSource` | Silk.NET enumeration and polling |
| `Render.Overlay2DRenderer.DrawPreferencesPanel` / `DrawControlsPanel` | over the shared `DrawAlertPanel` |
| host | `--preferences`, `--controls`, `--joystick [count]`, `--joystick-probe`, `--write-joystick-map`, `--write-prefs` |

## Traps

- **`PreferencesPanel_Run` (`00456d4c`) is the loop; `PreferencesPanel_Raise` (`0045cfd4`) is the
  [F12] entry point.** Older notes called `004566c4` "ctl_alrt"; it is `prf_alrt`. The controls
  panel is `00457d1c`.
- **A button row's cycling is over slot index, not action code**, and the slot is re-seeded from the
  stored code on open. Anything that writes a binding must keep `_slots` in step or the next click
  jumps.
- **The RAZOR has its own twelve-byte block** at option 25. Whatever persists must not assume 13.
- `Input_RecommendedBindings`' zeroing arm is unreachable through `Input_QueryCapabilities` — don't
  re-add it.
- **A joystick axis reaches `±0x100`, not `±0x80`.** The deadzone is subtracted and a squared curve
  scales it back up; the linear rescale in the same function belongs to response modes the joystick
  is never built with. See the doc's Rejected readings.
- **GLFW publishes sixteen joystick slots and fills in a device's shape a frame late.** Pick a device
  by `IsConnected`, never by index, and derive nothing from its axis and button counts until they are
  non-zero — at `Load` they are all zero even for a connected stick, which maps nothing and greys
  every CONTROLS row but JOYSTICK, that being the row with no capability byte of its own.
- **A throttle bound to THROTTLE puts `ThrottleLeverMode` at 1**, which switches
  `Mech_ApplyThrottleInput` to its absolute-lever arm: the machine takes its throttle from where the
  lever sits, so it walks off at half speed with a lever parked mid-travel. Retail behaviour, and
  surprising if you are expecting the keyboard's rate control.

## Loose ends worth a look

- Options 4-6, 12 and 37-53 are unidentified. The video-mode byte `VideoMode_Configure` (`0045e4f4`)
  reads is somewhere in there and would be worth pinning while the option map is fresh. A retail
  `prefs.cfg` has 39, 40, 44, 45, 46 and 47 non-zero.
- `ControlsPanel_Run`'s key handler is its own (`00458f9c`), not the family's
  `AlertPanel_HandleEvent`. Not read — [Esc]/[Return] were modelled from the cancel-widget
  convention, which every other panel in the family follows.
- `FUN_00429820` returns `DAT_004cfa20`, and `Sim_PollPlayerInput`'s `HDD VIEW` case branches on it
  between entering and leaving the heads-down display. What that global has to do with the current
  view is undecoded; the engine treats the case as the toggle its two branches plainly describe.
