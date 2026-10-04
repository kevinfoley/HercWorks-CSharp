using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Herculan.Engine.Input;
using Herculan.Engine.Render;
using Herculan.Engine.Render.Cockpit;
using Herculan.Engine.Scene;
using Herculan.Engine.Sim;
using Herculan.Engine.World;
using Silk.NET.Input;
using static Herculan.Engine.Host.KeyChords;

namespace Herculan.Engine.Host.Simulator.Cockpit;

/// <summary>
/// The simulator's four modal panels: the status-alert family ([Q], [Ctrl+Q], [P] and the mission's own
/// alerts), the [F11] objectives, the [F12] preferences and the CONTROLS panel it raises. Each is modal in
/// the original — it runs its own event loop, which owns input until the panel comes down and never reaches
/// Sim_MainTick — so while any is up the simulation is frozen and nothing else takes the keyboard, the
/// pointer or the stick.
/// </summary>
sealed class ModalPanels {
	private readonly SimulatorInput _input;
	private readonly CockpitView _view;
	private readonly MissionOutcome _outcome;
	private readonly Mission _mission;
	private readonly SimWorld _world;
	private readonly bool _hasCockpit;

	private int _objectivesKeysDown;
	private int _preferencesKeysDown;
	private int _statusAlertKeysDown;
	private bool _pointerDown;
	private bool _rightButtonDown;

	// Which of the eight the CONTROLS panel has already acted on and is waiting to see released —
	// Input_LatchButton's mask, kept here because the panel's presses never go through JoystickBindings.
	private byte _controlsPanelLatched;

	public ModalPanels(SimulatorStart start, SimulatorInput input, CockpitView view, MissionOutcome outcome,
			bool hasCockpit, JoystickCapabilities stagedJoystick) {
		_input = input;
		_view = view;
		_outcome = outcome;
		_mission = start.Mission;
		_world = start.World;
		_hasCockpit = hasCockpit;

		// The [F11] objectives panel. Built once with the mission's own block-13 text rather than on every
		// press, which is the one place this diverges from the original's own lifetime: it constructs the
		// panel, runs it and destroys it per press. Nothing in it changes during a mission.
		Objectives = ObjectivesPanel.Build(start.Content, _mission.ObjectiveTextRefs, _mission.TextAt);

		// The two modal alert panels. Only one can be up at a time -- they are modal in the original, and
		// each owns the input while it is -- so they share the pointer's press state.
		StatusAlert = StatusAlertPanel.Build(start.Content);

		// The [F12] preferences panel, showing the install's own data\prefs.cfg.
		Preferences = PreferencesPanel.Build(start.Content, start.Preferences,
			start.SoundAvailable, start.VoiceAvailable);

		// The CONTROLS panel the preferences panel raises. Which half of CTL_ALRT.STR it is, and which
		// twelve bytes of prefs.cfg it reads, both hang off whether the player's machine is the RAZOR. It is
		// built with whatever --joystick staged, which is nothing by default: GLFW does not publish a device's
		// shape until a frame after enumeration, so the real capabilities arrive later through
		// PilotControls.AnnounceJoystick. Until they do every row greys itself, which is what retail also shows
		// for a stick it cannot enumerate.
		Controls = ControlsPanel.Build(start.Content, start.Preferences, start.PilotingRazor, stagedJoystick);
	}

	public StatusAlertPanel? StatusAlert { get; }
	public ObjectivesPanel? Objectives { get; }
	public PreferencesPanel? Preferences { get; }
	public ControlsPanel? Controls { get; }

	/// <summary>
	/// Whether a modal is up, and so holding the input. All four freeze the simulation behind them and all four
	/// take the keyboard's command keys; the stick goes with them.
	/// </summary>
	public bool AnyOpen =>
		StatusAlert is { IsOpen: true } || Objectives is { IsOpen: true }
		|| Preferences is { IsOpen: true } || Controls is { IsOpen: true };

	/// <summary>Raises the panels the staging flags ask for, and says which panels are missing their text.</summary>
	public void OpenStaged(StagingOptions staging) {
		if (staging.Objectives) {
			Objectives?.Open();
		}

		if (staging.Preferences) {
			Preferences?.Open();
		}

		if (staging.Controls) {
			Controls?.Open();
		}

		if (StatusAlert == null) {
			Console.Error.WriteLine("[Q] mission-status alert: GNL_ALRT.STR is missing; the panel will not open.");
		}
		if (Objectives == null) {
			Console.Error.WriteLine("[F11] objectives panel: OBJ_ALRT.STR is missing; the panel will not open.");
		}
		if (Preferences == null) {
			Console.Error.WriteLine("[F12] preferences panel: PRF_ALRT.STR is missing; the panel will not open.");
		}
		if (Controls == null) {
			Console.Error.WriteLine("CONTROLS panel: CTL_ALRT.STR is missing; the panel will not open.");
		}
	}

	/// <summary>
	/// The panels take the keyboard before anything else does. [Esc], which dismisses any of them, is also this
	/// host's menu-bar key, so they have to be asked first or two things would act on one keystroke. All are asked
	/// every frame — single <c>|</c>, not <c>||</c> — so each keeps its own key-edge state whether or not another
	/// claimed the keystroke. Returns whether any of them claimed it.
	/// </summary>
	public bool ReadKeys() => ReadStatusAlertKeys() | ReadObjectivesKeys() | ReadPreferencesKeys();

	/// <summary>
	/// The modal owns the pointer: the cockpit behind it takes no clicks. Reads the open panel's buttons, and for
	/// the CONTROLS panel the stick too.
	/// </summary>
	public void ReadPointer(int framebufferWidth, int framebufferHeight, JoystickSource? joystick) {
		if (StatusAlert is { IsOpen: true } liveAlert) {
			// The panel's own placement, not a fixed one: the status alert and the pause panel are
			// different sizes and so centre to different origins.
			ReadPanelPointer(
				liveAlert.Place(framebufferWidth, framebufferHeight),
				liveAlert.PointerDown, (x, y, _) => liveAlert.PointerUp(x, y));
		} else if (Objectives is { IsOpen: true } liveObjectives) {
			ReadPanelPointer(
				ObjectivesPanelLayout.Place(framebufferWidth, framebufferHeight),
				(x, y) => liveObjectives.PointerDown(x, y),
				(x, y, _) => liveObjectives.PointerUp(x, y));
		} else if (Controls is { IsOpen: true } liveControls) {
			// The controls panel is the one modal this engine draws that opens over another: it takes the
			// pointer while it is up and the preferences strip below it stays visible but inert.
			ReadPanelPointer(
				ControlsPanelLayout.Place(framebufferWidth, framebufferHeight),
				(x, y) => liveControls.PointerDown(x, y),
				(x, y, right) => liveControls.PointerUp(x, y, right));

			// And the stick itself, which on this one panel is an input device rather than a pair of
			// menu keys — see ControlsPanel.PressButtonRow.
			ReadControlsPanelJoystick(liveControls, joystick);
		} else if (Preferences is { IsOpen: true } livePreferences) {
			// This one is pinned to the bottom of the screen rather than centred, so its placement is
			// its own — see PreferencesPanelLayout.
			ReadPanelPointer(
				PreferencesPanelLayout.Place(framebufferWidth, framebufferHeight),
				(x, y) => livePreferences.PointerDown(x, y),
				(x, y, right) => livePreferences.PointerUp(x, y, right));

			// CONTROLS raises the controls panel over this one, which is how the original reaches it
			// and the only way in.
			if (livePreferences.ControlsRequested) {
				livePreferences.ClearControlsRequest();

				// Re-read what the stick can do on the way in rather than trusting what it could at
				// startup: ControlsPanel_Run (00458650) asks Input_QueryCapabilities at the top of its own
				// loop, and that function rebuilds its eight bytes every call. So a stick plugged in
				// mid-mission lights the rows up, and one unplugged greys them.
				if (Controls is not null && joystick is { Capabilities.Present: true } liveStick) {
					Controls.Capabilities = liveStick.Capabilities;
				}

				Controls?.Open();
			}
		}
	}

	/// <summary>
	/// Everything held while the CONTROLS panel is down counts as already acted on, so a button being used for
	/// something else when the panel comes up does not also step a row. The mask then decays to the buttons
	/// actually held as the panel's stick read intersects it — which on the panel's first frame is exactly the
	/// set to swallow. It is the same priming JoystickBindings.Suspend does for the simulation's own latch, and
	/// for the same reason.
	/// </summary>
	public void PrimeControlsPanelLatch() {
		if (Controls is not { IsOpen: true }) {
			_controlsPanelLatched = 0xff;
		}
	}

	/// <summary>
	/// Sim_MainTick's own arms: once the mission is decided the poll raises the status alert by itself, and once
	/// the player is down the death camera does. Latched on SimWorld.PendingMissionAlert by the tick that
	/// produced it.
	/// </summary>
	public void RaisePendingMissionAlert() {
		if (_world is { PendingMissionAlert: not MissionStatus.None } alerted
			&& StatusAlert is { IsOpen: false } && Objectives is not { IsOpen: true }
			&& !_outcome.Over) {
			var raised = alerted.PendingMissionAlert;
			alerted.PendingMissionAlert = MissionStatus.None;
			OpenStatusAlert(raised, alerted.Objectives);
		}
	}

	/// <summary>
	/// [Q] asks the mission how it stands and offers a way out of it -- Sim_DispatchCommand's scancode 0x10,
	/// which calls Mission_Status with its "just answer the question" flag set, raises the status alert for the
	/// answer, and takes the button pressed as the decision. The same call records the answer as the status
	/// already raised, so the poll will not raise that same status again as a change, and it disarms the poll's
	/// pending alert delay.
	///
	/// <para>Returns whether the panel was raised, so [Q] does not also reach anything below it.</para>
	/// </summary>
	public bool RaiseStatusAlertForQuit() {
		if (StatusAlert == null || _world is not { } quitWorld
			|| quitWorld.PlayerMech is not { } quitPlayer || quitWorld.Objectives is not { } quitObjectives) {
			return false;
		}

		var status = quitObjectives.QueryForPlayer(quitWorld, quitPlayer);
		return OpenStatusAlert(status, quitObjectives);
	}

	/// <summary>
	/// The panel for one status, with the outstanding objective's own failure text alongside it -- the
	/// substitution the constructor makes for status 5 and no other.
	/// </summary>
	public bool OpenStatusAlert(MissionStatus status, MissionObjectives objectives) {
		if (StatusAlert == null || !StatusAlertPanel.CanShow((int)status)) {
			return false;
		}

		var failure = objectives.Outstanding is { } outstanding
			? _mission.DescriptionOf(outstanding.Record)
			: null;

		if (!StatusAlert.Open((int)status, failure)) {
			return false;
		}

		_pointerDown = _input.Pointer().Buttons.HasFlag(CockpitMouseButtons.Left);
		return true;
	}

	/// <summary>
	/// What the player answered. Only the button the status's own table names ends the mission; every other
	/// answer just puts the panel away and carries on.
	/// </summary>
	public void ApplyStatusAlertAnswer() {
		if (StatusAlert == null || !StatusAlert.TryTakeAnswer(out int button, out bool ends)) {
			return;
		}

		if (!ends) {
			return;
		}

		// Both endings leave the simulator. EXIT EARTHSIEGE? is not a mission outcome at all -- its QUIT
		// sets DAT_004d2582, the global quit flag that AlertPanel_Present also watches to tear down any
		// panel still up -- while a mission-ending answer goes up through Sim_PollPlayerInput and
		// Sim_MainTick as the tick's own return, which ends the main loop. Either way Sim_Shutdown then
		// writes the results and picks the exit code, after the window has gone (SimulatorHost's end).
		bool quitGame = StatusAlert.Status == StatusAlertPanel.ExitGameStatus;
		Console.WriteLine(quitGame
			? "Quitting EarthSiege 2."
			: $"Mission over — status {StatusAlert.Status} "
				+ $"({(MissionStatus)StatusAlert.Status}), answered '{StatusAlert.Buttons[button]}'.");
		_outcome.End(quitGame);
	}

	/// <summary>
	/// Both panel families are modal, so they go over everything the cockpit drew — and over the external view
	/// too, where one stays up if the player switched views with it open. Only one can be up at a time.
	/// </summary>
	public void Draw(AlertPanelPainter painter, int width, int height, GpuTexture spriteTexture, HudSpriteSheet sprites) {
		if (StatusAlert is { IsOpen: true } openAlert) {
			painter.DrawStatusAlertPanel(width, height, spriteTexture, sprites, openAlert);
		} else if (Objectives is { IsOpen: true } openObjectives) {
			painter.DrawObjectivesPanel(width, height, spriteTexture, sprites, openObjectives);
		} else if (Preferences is { IsOpen: true } openPreferences) {
			painter.DrawPreferencesPanel(width, height, spriteTexture, sprites, openPreferences);

			// And the controls panel over it, in that order: the original's AlertPanel_Enter saves the
			// screen under the panel it raises, so the strip it was opened from is still there behind it.
			if (Controls is { IsOpen: true } openControls) {
				painter.DrawControlsPanel(width, height, spriteTexture, sprites, openControls);
			}
		}
	}

	// The three keys that raise a panel of the status-alert family, and the two that answer one.
	//
	// [Q] asks how the mission stands, [Ctrl+Q] asks to leave the game, and [P] pauses — the manual's
	// own "Quit Mission", "Quit EarthSiege 2" and "Pause Mission", and Sim_DispatchCommand's commands
	// 0x10, 0x410 and 0x19. [Return] and [Esc] both answer with button 0, which is CONTINUE on every
	// one of them. While a panel is up nothing else may act: AlertPanel_HandleEvent answers those two
	// keys and the panel's own loop owns the rest.
	//
	// Returns whether the panel claimed the keystroke.
	private bool ReadStatusAlertKeys() {
		var keyboard = _input.Keyboard;
		if (StatusAlert == null || keyboard == null
			|| _input.KeyboardCapturedByImGui) {
			_statusAlertKeysDown = 0;
			return false;
		}

		bool ctrl = keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight);
		bool q = Edge(Key.Q, 0);
		// `|`, not `||`: both edges must be read every frame or the one that is skipped never updates
		// its held state, and the next press of it is swallowed.
		// With [Alt] or [Ctrl] down these are other codes, which AlertPanel_HandleEvent does not answer.
		bool enter = (Edge(Key.Enter, 1) | Edge(Key.KeypadEnter, 2)) && Unmodified(keyboard);
		bool escape = Edge(Key.Escape, 3) && Unmodified(keyboard);
		bool pause = Edge(Key.P, 4);

		if (StatusAlert.IsOpen) {
			return StatusAlert.HandleKey(enter, escape) || q || pause;
		}

		// Not while another panel is up, which is already holding the input. The external view keeps both:
		// they are the dispatcher's own cases, not the widgets'.
		if (_outcome.Over || !_hasCockpit
			|| Objectives is { IsOpen: true } || Preferences is { IsOpen: true }) {
			return false;
		}

		// [Ctrl+Q] is command 0x410 — the Ctrl bit over Q's own scancode — and asks to leave the game
		// rather than the mission. [Q] alone asks how the mission stands, and [P] pauses.
		if (q) {
			return ctrl
				? OpenStatusAlert((MissionStatus)StatusAlertPanel.ExitGameStatus, _world.Objectives)
				: RaiseStatusAlertForQuit();
		}

		// [Ctrl+P] is 0x419, a developer key, and never reaches the pause.
		if (pause && !ctrl) {
			return OpenStatusAlert((MissionStatus)StatusAlertPanel.PauseStatus, _world.Objectives);
		}

		return false;

		bool Edge(Key key, int bit) => KeyBit(keyboard, key, bit, ref _statusAlertKeysDown);
	}

	// [F11] puts the objectives panel up, and [Return] or [Esc] takes it down — the three keys the
	// original answers, scancode 0x57 through CockpitWidgets_HandleCommand (00432bc8) on the way in and
	// 0x1c/0x01 through the panel's own handler (AlertPanel_HandleEvent, 00454e10) on the way out. Nothing in that handler
	// answers 0x57, so [F11] does not close the panel it opened; that is retail behaviour, not an
	// oversight here.
	//
	// Returns whether the panel claimed the keystroke, so [Esc] does not also reach the debug panel.
	private bool ReadObjectivesKeys() {
		var keyboard = _input.Keyboard;
		if (Objectives == null || keyboard == null
			|| _input.KeyboardCapturedByImGui) {
			_objectivesKeysDown = 0;
			return false;
		}

		bool open = Edge(Key.F11, 0);
		// `|`, not `||`: both edges must be read every frame or the one that is skipped never updates
		// its held state, and the next press of it is swallowed.
		// With [Alt] or [Ctrl] down these are other codes, which AlertPanel_HandleEvent does not answer.
		bool enter = (Edge(Key.Enter, 1) | Edge(Key.KeypadEnter, 2)) && Unmodified(keyboard);
		bool escape = Edge(Key.Escape, 3) && Unmodified(keyboard);

		if (Objectives.IsOpen) {
			return Objectives.HandleKey(enter, escape) || open;
		}

		// Only from inside the machine, and not while another panel is up: the command reaches the
		// panel through the cockpit's own widget tree, which is not on screen in the external view, and
		// one modal is already holding the input.
		if (open && _hasCockpit && !_view.ExternalViewActive
			&& StatusAlert is not { IsOpen: true } && Preferences is not { IsOpen: true }) {
			Objectives.Open();
			return true;
		}

		return false;

		bool Edge(Key key, int bit) => KeyBit(keyboard, key, bit, ref _objectivesKeysDown);
	}

	// [F12] puts the preferences panel up, and [Return] or [Esc] takes it down — scancode 0x58 through
	// CockpitWidgets_HandleCommand on the way in (PreferencesPanel_Raise, 0045cfd4), and the panel's own
	// handler on the way out, where [Esc] presses the cancel widget the constructor set to DONE. As with
	// [F11], nothing in the panel's loop answers 0x58, so a second press does not close it.
	//
	// The original also reaches this panel on [Alt+P], command 0x219. Not bound here: [P] alone is the
	// pause panel, and this host has no Alt-modified command bank yet.
	//
	// Returns whether the panel claimed the keystroke, so [Esc] does not also reach the debug panel.
	private bool ReadPreferencesKeys() {
		var keyboard = _input.Keyboard;
		if (Preferences == null || keyboard == null
			|| _input.KeyboardCapturedByImGui) {
			_preferencesKeysDown = 0;
			return false;
		}

		bool open = Edge(Key.F12, 0);
		// `|`, not `||`: both edges must be read every frame or the one that is skipped never updates
		// its held state, and the next press of it is swallowed.
		// With [Alt] or [Ctrl] down these are other codes, which AlertPanel_HandleEvent does not answer.
		bool enter = (Edge(Key.Enter, 1) | Edge(Key.KeypadEnter, 2)) && Unmodified(keyboard);
		bool escape = Edge(Key.Escape, 3) && Unmodified(keyboard);

		// The controls panel is modal over this one: while it is up it answers [Return] and [Esc], and
		// this panel answers nothing.
		if (Controls is { IsOpen: true } liveControls) {
			return liveControls.HandleKey(enter, escape) || open;
		}

		if (Preferences.IsOpen) {
			return Preferences.HandleKey(enter, escape) || open;
		}

		// Not while another modal is up. Unlike the objectives panel it opens from the external view as
		// well: [F12] is the dispatcher's own case (0x58), not the widgets'.
		if (open && _hasCockpit
			&& StatusAlert is not { IsOpen: true } && Objectives is not { IsOpen: true }) {
			Preferences.Open();
			return true;
		}

		return false;

		bool Edge(Key key, int bit) => KeyBit(keyboard, key, bit, ref _preferencesKeysDown);
	}

	// One key's down edge, against its bit in a panel's held-key mask.
	private static bool KeyBit(IKeyState keyboard, Key key, int bit, ref int keysDown) {
		bool down = keyboard.IsKeyPressed(key);
		bool edge = down && (keysDown & (1 << bit)) == 0;
		keysDown = down
			? keysDown | (1 << bit)
			: keysDown & ~(1 << bit);
		return edge;
	}

	// A modal panel's buttons, pressed and released. Read straight off the device rather than through
	// CockpitInput: that queue is the cockpit's, and while a modal is up the cockpit is not taking
	// clicks at all. Press and release must both land on the same button for it to fire, which is
	// Widget_OnMouseUp's own re-hit-test.
	private void ReadPanelPointer(AlertPanelLayout.Placement place, Action<float, float> onDown,
			Action<float, float, bool> onUp) {
		var (pointerX, pointerY, buttons) = _input.Pointer();
		var (panelX, panelY) = place.ToPanel(pointerX, pointerY);

		// Both buttons press a widget; which one was released is what the click carries, since
		// CockpitMouse_ProcessQueue ORs the button bit into the click value on the release edge and the
		// panel reads bit 1 off it. Two of these panels step a setting backwards on the right button.
		bool right = buttons.HasFlag(CockpitMouseButtons.Right);
		bool down = buttons.HasFlag(CockpitMouseButtons.Left) || right;
		if (down && !_pointerDown) {
			_rightButtonDown = right;
			onDown(panelX, panelY);
		} else if (!down && _pointerDown) {
			onUp(panelX, panelY, _rightButtonDown);
			_rightButtonDown = false;
		}

		_pointerDown = down;
	}

	// The stick as the CONTROLS panel reads it, which is not how the rest of the session reads it: here a
	// button press picks the row it belongs to rather than firing whatever that row is bound to. The panel
	// owns what a press means (ControlsPanel.PressButtonRow); this owns only which press is new.
	//
	// JoystickReading.Buttons is the device's own eight, before JoystickBindings lifts the trigger out of
	// them, so BUTTON 1's row is reachable with the trigger — the thing ControlsPanel_HandleEvent restores
	// by hand before it reads the device block.
	//
	// Retail latches the button it acts on and the next input build masks it to zero, so a held button is
	// one step and no more, and the panel sees a latched button as not pressed at all. Masking first is
	// the same arrangement, and it is why this can simply take the lowest pressed row — the original
	// breaks at the first set byte it finds, which is the same row.
	private void ReadControlsPanelJoystick(ControlsPanel panel, JoystickSource? joystick) {
		if (joystick is not { Capabilities.Present: true }) {
			return;
		}

		byte pressed = joystick.Read().Buttons;
		_controlsPanelLatched &= pressed;

		for (int row = 0; row < JoystickCapabilities.MaxButtons; row++) {
			int bit = 1 << row;
			if ((pressed & ~_controlsPanelLatched & bit) == 0) {
				continue;
			}

			_controlsPanelLatched |= (byte)bit;
			panel.PressButtonRow(row);
			return;
		}
	}
}
