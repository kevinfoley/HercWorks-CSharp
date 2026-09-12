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

- **CD music.** Both message ports are ported — the cockpit computer's ticker and the pilot and
  squad channel, with its `PILOT<n>.STR` sets, its speaker-coloured box, the `P*_*` voice clips and
  the comm box's `.SNC` portrait animation. Red Book music through MCI is not, so a mission runs
  without its track.
  → [`docs/formats/audio.md`](docs/formats/audio.md)
- **Combat gaps.** Hit detection, weapon-mount destruction and the explosive blast sweep are
  complete for all three shootable classes. One of the sweep's three call sites is still unreachable
  because the function that owns it is unported: the AI ramming attack (`FUN_0041e488`, part of the
  `ramming` entry below).
  → [`docs/simulation/damage-system.md`](docs/simulation/damage-system.md)
- **The `ramming` behaviour state.** Every other behaviour state's think is ported; state 17's pair
  (`Mech_BehaviourRamThink` `0041e570`, `Mech_BehaviourRamTick` `0041e488`) is not, so a group given
  order verb 1 takes the state and stands still in it.
  → [`docs/simulation/ai-combat-states.md`](docs/simulation/ai-combat-states.md),
  [`docs/simulation/damage-system.md`](docs/simulation/damage-system.md)
- **A machine's LOD roots are not selected.** Root 0 is hard-coded where the original picks one per
  frame from projected size and a detail bias.
  → [`docs/formats/mech-shape-drawing.md`](docs/formats/mech-shape-drawing.md)
- **Weapon input divergences.** A right press dragged off its widget before release fires nothing
  here, where the original re-hits on release; clicking a pod's row does nothing, where the original
  toggles the pod.
  → [`docs/simulation/weapon-mounts.md`](docs/simulation/weapon-mounts.md)
- **Terrain raycast, swept-volume mode.** Only thin-ray mode is ported; the swept-volume mode
  (movement collision) is not, because nothing in the engine needs it yet.
  → [`docs/formats/terrain-heightmap.md`](docs/formats/terrain-heightmap.md)
- **The HUD's "ATT" legend.** Automatic Turret Tracking itself works, and the legend is a label of
  the gunsight complex's, but the engine does not draw it — so nothing on screen says the tracker is
  on but the TRACK button's own lamp and the computer's spoken confirmation.
  → [`docs/formats/cockpit-hud.md`](docs/formats/cockpit-hud.md#front-window-hud--the-gunsight-complex),
  [`docs/simulation/torso-aim.md`](docs/simulation/torso-aim.md)
- **The heading tape does not scroll.** The engine blits `hudhtick` frame 0 at the tape's rect and
  leaves it there, so the compass reads the same degrees whichever way the machine faces. The
  original slides a two-frame window across the bank from the heading. The waypoint indicators over
  it are correct, so they and the compass disagree.
  → [`docs/formats/cockpit-hud.md`](docs/formats/cockpit-hud.md#heading-tape)
- **The objective layer's two panels.** The records, the status and the four lines the computer
  speaks are ported; neither panel that shows them is. The modal alert (`gnl_alrt`, `FUN_00455934`)
  that a status change raises is latched on `SimWorld.PendingMissionAlert` and nothing draws it, and
  the in-mission objectives list (`obj_alrt`, `FUN_0045751c`) has its data on `Mission.BriefingLines`
  and `Mission.Text` and nothing lists it.
  → [`docs/simulation/mission-objectives.md`](docs/simulation/mission-objectives.md)
- **The computer's damage messages.** The engine speaks the objective set, the waypoint, the radar
  and auto-track toggles and the power-up line. The rest of `Computer_PostMessage`'s traffic — the
  `INTERNAL DAMAGE` family, `WEAPON DESTROYED`, `DAMAGE LEVEL CRITICAL`, `SHIELDS CRITICAL`, and
  `ENEMY TARGET DESTROYED`/`DISABLED` when the player kills what they had selected — is not posted.
  → [`docs/formats/audio.md`](docs/formats/audio.md#posters)
- **Mission difficulty.** Nothing sets it, so the two systems that index it — the AI's aim scatter
  and the explosive damage scale — run on entry 0.
  → [`docs/simulation/ai-weapons.md`](docs/simulation/ai-weapons.md),
  [`docs/simulation/projectiles.md`](docs/simulation/projectiles.md)
- **Flyer control bindings.** The flight model is ported and the axis roles are known, but key
bindings are hardcoded placeholders.
  → [`docs/simulation/razor-flight.md`](docs/simulation/razor-flight.md)

## Reverse-engineering still open

The engine cannot be faithful here until the original is understood.

- **SimRandom's 56-entry seed table isn't extracted** from DBSIM's data section. The algorithm is a
  literal port; the seeding is not, and a roll's result also depends on generator-advance count —
  treat as statistically faithful, not replay faithful.
  → [`docs/simulation/dbsim-physics-notes.md`](docs/simulation/dbsim-physics-notes.md)
- **The mission message an action queues.** `Action_Activate` queues the line named at action `+0x34`
  on the **pilot and squad** port (`view+0x207`), not the computer's ticker. `data\mission.str` is now
  loaded onto `Mission.Text`, but how that port resolves an id whose record names no speaker — its own
  catalog is the per-slot `PILOT*.STR` scatter — is not established, so nothing is posted.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md),
  [`docs/formats/audio.md`](docs/formats/audio.md#the-pilot-and-squad-channel)
- **The mission counters' reader.** `DAT_004a9ef4` is written by an activating action and dumped to
  `mission_var` at mission end. The reader is VSHELL's campaign layer (`MissionVar_Read`,
  `0040ea59`), which is not ported: nothing in this engine consumes the counters, persists them
  across missions, or gates `.msn` conditions on them.
  → [`docs/shell/campaign-loop.md`](docs/shell/campaign-loop.md)
- **The drop pod's ground mark.** The leftover effect a landed pod spawns comes from the theater's
  `flat`/`flat2` shape pool, which is not ported.
  → [`docs/simulation/mission-deployment.md`](docs/simulation/mission-deployment.md)
- **The cockpit widget class family's vtables.** `known_vtables.json` covers the simulation-object hierarchy only, so not one widget class is in it, and the slot offsets are not uniform across the family: a new cockpit control has to be reached by dumping its own class's table afresh rather than by looking a shape up. Fifteen tables are known to carry `Widget_ClickSound`, and of those only the shield facing's is tied to the class that owns it.
  → [`docs/formats/cockpit-input.md`](docs/formats/cockpit-input.md),
  [`docs/formats/cockpit-hud.md`](docs/formats/cockpit-hud.md)
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
- The Preferences screen (F12) is not implemented.
- Cheats (other than Alt-D to drop a waypoint at your position, which is implemented; this one isn't documented but also doesn't really seem like a cheat).

## Debugging features
- Launch option to disable AI (so units other than the player remain stationary, though still subject to damage and destruction)
- Support for editing the current script.dat in the Mission Editor, to facilitate setting up scenarios for rapid testing? The main short-term needs would be moving Cybrid or player spawnpoints)