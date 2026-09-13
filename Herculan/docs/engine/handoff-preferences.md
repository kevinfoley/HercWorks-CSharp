# Handoff — preferences and controls panels

Scratchpad. The authoritative doc is
[`../simulation/preferences.md`](../simulation/preferences.md); anything here that outlives the next
session should be drained into it and deleted from here.

## State

Done: both panels' layout, their text, reading the install's own `data\prefs.cfg`, and in-memory
cycling of every row (including RECOMMEND). Verified against `ES2/DATA/PREFS.CFG` and pixel-matched
to `Reference/F12_Preferences.png` and `Reference/Preferences_HERC_Controls.png`.

Not done, in the order they were scoped:

1. **Persistence.** Nothing writes `prefs.cfg` back. `PreferencesPanel_Run` case 10 calls
   `FUN_004574cc` on DONE and `ControlsPanel_Run` case 14 calls `FUN_00459140` then `FUN_00459878`;
   none of those three is decoded, and one of them is presumably the writer. Note this touches the
   user's real retail install — gate it behind a flag or a copy, don't write by default.
2. **Applying a setting live.** The handler table at `004d2060`, five entries, registered by
   `Prefs_Init` (`0045a19c`) via `FUN_00459c58`: options 0, 1, 2, 8 and 0x0e take `00459c98`,
   `00459c6c`, `00459cc4`, `00459d4c` and `Input_SetThrottleLeverMode`. Only the last is decoded.
   `Prefs_SetOption`'s third argument is what calls them.
3. **Joystick input.** None exists, so `JoystickCapabilities.None` is the default and every controls
   row greys. `--joystick [count]` stages a stick to exercise the live path.

## Where things are

| | |
|---|---|
| `Content.SimulatorPreferences` | the file, plus `Set` / `Step` / `Toggle` |
| `Content.PreferencesPanel`, `Content.ControlsPanel` | text, state, click rules |
| `Content.PreferencesPanelLayout`, `Content.ControlsPanelLayout` | geometry |
| `Content.JoystickCapabilities` | the `Input_QueryCapabilities` block |
| `Render.Overlay2DRenderer.DrawPreferencesPanel` / `DrawControlsPanel` | over the shared `DrawAlertPanel` |
| host | `--preferences`, `--controls`, `--joystick [count]` |

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

## Loose ends worth a look

- Options 4-6, 12 and 37-53 are unidentified. The video-mode byte `VideoMode_Configure` (`0045e4f4`)
  reads is somewhere in there and would be worth pinning while the option map is fresh.
- `FUN_0045c508(3)` is the joystick-present gate; `FUN_0045bee8` under it is undecoded.
- `ControlsPanel_Run`'s key handler is its own (`00458f9c`), not the family's
  `AlertPanel_HandleEvent`. Not read — [Esc]/[Return] were modelled from the cancel-widget
  convention, which every other panel in the family follows.
