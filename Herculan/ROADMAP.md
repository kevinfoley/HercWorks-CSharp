# Roadmap — work not yet done

Everything the HERCULAN Engine does not implement yet, in one place. This is the counterpart of
[`KNOWN_ISSUES.md`](KNOWN_ISSUES.md), which records things that *are* implemented but behave
differently from retail. If a feature is missing entirely it belongs here; if it is present and
wrong it belongs there.

Each entry names the doc that owns the subject. **That doc is authoritative** for how much is
reverse-engineered and how much is still unknown — this file only tracks that the porting work is
outstanding.

## Reverse-engineered, not ported

The mechanism is understood; what is left is engine work.

- **The triple turret does not shoot.** Four of the five structure classes now fill their `+0x18`
  tick slot; type `0x22`'s three-turrets-from-one-object tick (`004045c8`) is the one left, and it
  needs the 11-`short` weapon descriptor table at `DAT_004a9640` dumped out of the data section.
  → [`docs/simulation/structure-behaviour.md`](docs/simulation/structure-behaviour.md)
- **A machine's crudest LOD roots are never drawn.** Root selection is ported, but the roots that
  compact their node numbering — the crudest one to three of each chassis — are excluded, because
  drawing them puts APOCA's upper body on a knee. The original composes every root through root 0's
  pose array too, so by the binary it should do the same; retail does not visibly do so, and what
  reconciles that is not yet found. **Settle that before changing anything here**: it decides
  whether the truncation is a divergence to lift or retail behaviour to match.
  → [`docs/formats/mech-shape-drawing.md`](docs/formats/mech-shape-drawing.md#the-pose-array-is-root-0s)
- **Terrain raycast, swept-volume mode.** Only thin-ray mode is ported; the swept-volume mode
  (movement collision) is not, because nothing in the engine needs it yet.
  → [`docs/formats/terrain-heightmap.md`](docs/formats/terrain-heightmap.md)
- **The HUD's "ATT" legend.** Automatic Turret Tracking itself works, and the legend is a label of
  the gunsight complex's, but the engine does not draw it — so nothing on screen says the tracker is
  on but the TRACK button's own lamp and the computer's spoken confirmation.
  → [`docs/formats/cockpit-hud.md`](docs/formats/cockpit-hud.md#front-window-hud--the-gunsight-complex),
  [`docs/simulation/torso-aim.md`](docs/simulation/torso-aim.md)
- **What happens after a mission ends.** The objective layer, both its panels and the answer that
  ends a mission are ported, but the engine has nowhere to hand that answer: the original returns it
  up through `Sim_MainTick` and the shell writes `(status == 9)` into `results.dat` and advances the
  campaign. Here the window simply closes.
  → [`docs/simulation/mission-objectives.md`](docs/simulation/mission-objectives.md),
  [`docs/formats/save-games.md`](docs/formats/save-games.md)
- **Flyer control bindings.** The flight model is ported and the axis roles are known, but key
bindings are hardcoded placeholders.
  → [`docs/simulation/razor-flight.md`](docs/simulation/razor-flight.md)
- **The two side windows, and the two screen-edge strips that reach them.** The cockpit has four
  views; the engine renders two. `.HB2`/`.HD2` are loaded and clipped as `CockpitArt.SideViewIndex`,
  but nothing ever selects view 2 or 3, so `[F9]`/`[F10]` do nothing and the left and right windows
  are unreachable. Retail's left and right `ScrollTrigger` strips go with them — the vertical one is
  ported (`CockpitWidgets.VisibleHeadsDownViewEdge`), but a side strip that queued a view nothing can
  render would be a dead click region rather than a faithful one.
  → [`docs/formats/cockpit-input.md`](docs/formats/cockpit-input.md#10-the-screen-edges-are-three-widgets),
  [`docs/formats/cockpit-hud.md`](docs/formats/cockpit-hud.md)
- **The engine can only load a `script.dat` handoff, not a `.MSN`.** `MissionLoader` builds a
  mission out of the campaign handoff alone; the ~50 real missions in `ZONES.VOL` are `.MSN` files,
  whose format is decoded and read by `HercWorks.Core`'s `MissionFileTransformer` but which nothing
  turns into a `Mission`. So the engine can be played against a handful of saved states and not
  against the game's own missions, and a scenario that is not in the handoff — a ground-vehicle
  convoy, for one — has to be built by hand.
  → [`docs/formats/msn-mission-file.md`](docs/formats/msn-mission-file.md),
  [`docs/formats/script-dat.md`](docs/formats/script-dat.md)

## Reverse-engineering still open

The engine cannot be faithful here until the original is understood.

- **How many times DBSIM has advanced its generator before any given roll.** The algorithm, the
  56-entry seed table and both cursor starts are ported, so the two generators produce identical
  streams from the same starting point — but a roll's result depends on its position in that stream,
  and this engine does not yet make the same draws in the same order. Replay parity needs the call
  history matched, which is really a question about tick order, not about the generator.
  → [`docs/simulation/random-generator.md`](docs/simulation/random-generator.md)
- **The mission message an action queues.** `Action_Activate` queues the line named at action `+0x34`
  on the **pilot and squad** port (`view+0x207`), not the computer's ticker. `data\mission.str` is now
  loaded onto `Mission.Text`, but how that port resolves an id whose record names no speaker — its own
  catalog is the per-slot `PILOT*.STR` scatter — is not established, so nothing is posted.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md),
  [`docs/formats/cockpit-messages.md`](docs/formats/cockpit-messages.md#the-pilot-and-squad-channel)
- **The mission counters' reader.** `DAT_004a9ef4` is written by an activating action and dumped to
  `mission_var` at mission end. The reader is VSHELL's campaign layer (`MissionVar_Read`,
  `0040ea59`), which is not ported: nothing in this engine consumes the counters, persists them
  across missions, or gates `.msn` conditions on them.
  → [`docs/shell/campaign-loop.md`](docs/shell/campaign-loop.md)
- **The drop pod's ground mark.** The leftover effect a landed pod spawns comes from the theater's
  `flat`/`flat2` shape pool, which is not ported.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md)
- **External view (`[V]` chase camera) is entirely engine-invented.** DBSIM's own external view
  placement, transitions, terrain handling and overlay chrome are unrecovered.
  `Render/ExternalCamera.cs` is the single place a real rule would replace the guess.
- **Pause (`[P]`) is a placeholder** that just stops the fixed-timestep tick loop. Retail DBSIM's own
  pause has not been traced.

## The shell front end
`--shell` draws the frame every tab screen shares — the tiled backdrop, the square button and the
eight captioned tabs, hit-tested, latching on the six tabs that latch, gated by campaign mode and
switching palette per tab — plus the save and repair screens behind tabs 1 and 3. The five widget
paints are ported onto an indexed software canvas, so a further screen is layout, text and
hit-testing rather than new drawing code. What is missing:
- **Six of the eight tab screens.** Each has its own builder in the executable and its own
  hundred-odd widget rects. Tab 1, `SAVED GAMES`, is drawn from the real `GAMEFILE.STR` and the real
  saves, and tab 3, `REPAIR`, from a real save's hangar bay and the real `damage.dat` price list; the
  other six show the bare frame. The dispatch that reaches them, and which builder each tab calls, is
  read.
  → [`docs/shell/screen-layout.md`](docs/shell/screen-layout.md)
- **Every repair-screen action.** Both damage lists, their selection rules, the three readout panels
  and the affordability gating work; REPAIR, REPAIR ALL, SCRAP and CANCEL do nothing, so no machine
  is ever repaired or scrapped and no salvage is spent. The manual/auto mode readout shows the flag
  and nothing changes it.
  → [`docs/shell/screen-layout.md`](docs/shell/screen-layout.md#the-repair-screen)
- **The repair screen's damage diagram.** The eight per-bay grids the builder puts down the left of
  the canvas, and the exploded chassis picture the squad panel puts over the same rect, are not
  drawn — so the six body groups have their list rows and no hotspots. Which `dba\` bank and frame
  each part binds is read.
  → [`docs/shell/screen-layout.md`](docs/shell/screen-layout.md#the-damage-diagram)
- **Every save-screen action.** The slot list, its selection and the summary panel work; renaming a
  slot — the rows are editable text fields with their own character set — and the SAVE, RESTORE and
  EXIT buttons do nothing, so no save is written, loaded or left.
  → [`docs/shell/screen-layout.md`](docs/shell/screen-layout.md#the-save-screen)
- **The per-slot chassis panel `wsquadi.cpp` shares across four tabs.** Its show/hide and the
  selected-slot state are read; the roster list itself (`Squad_BuildRosterList`, `0043c999`) is not,
  and none of it is ported.
  → [`docs/shell/screen-layout.md`](docs/shell/screen-layout.md)
- **The arming and repair hotspots.** Fully reverse-engineered — the file format, which component
  each area selects, and both screens' selection rules. The repair screen's rules are ported and its
  component conditions are displayed; no chassis picture is drawn and no hotspot is hit-tested, on
  either screen.
  → [`docs/shell/screen-layout.md`](docs/shell/screen-layout.md),
  [`docs/formats/herc-catalogs.md`](docs/formats/herc-catalogs.md)
- **Nothing sets the campaign mode.** The tab gate is ported and correct, but the flag behind it
  (`DAT_0048260c`) comes from the save the shell opened, and the shell host has no loaded game — the
  repair screen reads the first in-use slot directly instead, and the gate is driven by
  `--shell-training`.
  → [`docs/shell/screen-layout.md`](docs/shell/screen-layout.md)
- **The mouse cursor.** `dba\cursor.dba` is not drawn — the host shows the OS pointer.
- **Sound.** `SHLSOUND.VOL` is not mounted and no widget makes a noise. The shell's own click is
  `0042ee89`, fired at the end of every tab switch.

## Other unported features
- Currently missing is a quirk from retail where the player's shield meter fills in over ~10 seconds at the start of a mission. Claude says there's no explanation for this in the shield code, where the shields start out at full charge, and would take ~30 seconds to fully charge from empty. The fade-in-over-10-seconds may be a HUD animation that hasn't been discovered during RE yet.
- Similarly to the previous, currently missing is an animation where weapon buttons wink on one-at-a-time when the simulation first starts.
- Preferences (F12): the screen is laid out, reads the install's own `data\prefs.cfg` and cycles its
  settings, but three of the nine rows still change nothing — SOUNDS, PILOT MESSAGE and
  COMPUTER MESSAGE are saved to the file and read by no consumer, so wiring them up means
  reaching into the audio sink and the message port. TERRAIN DISTANCE, TERRAIN TEXTURE, HERC DETAIL
  and the audio half of EFFECTS DETAIL are live. What the other two want is not a hookup but a feature each:
  STRUCTURE DETAIL is the bias in `TSDetailPart`'s level selection, and the engine draws the finest
  level unconditionally rather than choosing one by projected size
  (→ [`docs/formats/dts-texture-binding.md`](docs/formats/dts-texture-binding.md#tsdetailpart-level-selection-and-structure-detail));
  what EFFECTS DETAIL does to the effects themselves, as against to the sound throttle, is undecoded.
  Separately, a changed setting is not applied while the panel is still up: the
  per-option handler table (`004d2060`) is unported. All five of its handlers are decoded; SOUNDS
  and PILOT MESSAGE have no other route to their setting, so those two are where wiring the
  table up would begin. Writing the file back
  is implemented on retail's own terms: each panel merges its own options into a fresh read of the
  file as it closes.
  → [`docs/simulation/preferences.md`](docs/simulation/preferences.md)
- The outside and chase views. The joystick's `OUTSIDE VIEW` and `CHASE VIEW` actions step a chain of
  external cameras (`DAT_004d2572`, four states) that the engine has no equivalent of, so those two
  bindings do nothing.
  → [`docs/formats/joystick-input.md`](docs/formats/joystick-input.md#the-buttons)
- Cheats (other than Alt-D to drop a waypoint at your position, which is implemented; this one isn't documented but also doesn't really seem like a cheat).

## Debugging features
- Launch option to disable AI (so units other than the player remain stationary, though still subject to damage and destruction)
- Support for editing the current script.dat in the Mission Editor, to facilitate setting up scenarios for rapid testing? The main short-term needs would be moving Cybrid or player spawnpoints)