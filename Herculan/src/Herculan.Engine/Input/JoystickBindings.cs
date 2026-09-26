using Herculan.Engine.Content;

namespace Herculan.Engine.Input;

/// <summary>
/// Turns a <see cref="JoystickReading"/> into pilot input according to the twelve binding bytes of a
/// <c>prefs.cfg</c> controls block — the joystick arm of <c>Input_BuildPlayerDevice</c>, the
/// per-frame input build, plus the button dispatch <c>Sim_PollPlayerInput</c> (<c>00460764</c>) runs off it.
///
/// <para><b>The bindings name no hardware.</b> Four bytes say which pair of game axes each control
/// feeds and eight say which action each button fires; nothing in the file identifies a device, an
/// axis number or a HID usage. That is why a modern stick can drive a retail file unchanged, and why
/// this class takes an already-abstract <see cref="JoystickReading"/> — <see cref="JoystickDeviceMap"/>
/// owns the step above it.</para>
///
/// <para>Held state (the axes and the trigger) is recomputed every tick; the eight buttons are
/// <b>press-once</b>. <c>Input_LatchButton</c> (<c>0045b718</c>) latches a button the moment its action fires and
/// <c>Input_BuildPlayerDevice</c> masks it to zero on every following tick until the player lets go,
/// so holding a button repeats nothing. This class keeps that latch, which is why it is an instance rather than
/// a static.</para>
/// </summary>
public sealed class JoystickBindings {
	/// <summary>How many buttons a controls block binds, and the panel has rows for.</summary>
	public const int ButtonCount = JoystickCapabilities.MaxButtons;

	/// <summary>
	/// What a hat direction is worth on an axis when the HAT row is set to
	/// <see cref="JoystickAxisAssignment.Turret"/> — <c>0xc0</c>, three quarters of
	/// <see cref="Sim.MechControls.AxisFull"/>. It sits between a held key's half and a stick's whole,
	/// which is about right for a control that only has on and off: the turret sweeps briskly without
	/// a hat tap being worth more than pushing the stick over.
	/// </summary>
	public const short HatAxis = 0xc0;

	/// <summary>Which buttons are latched down, waiting for a release before they can fire again.</summary>
	private readonly bool[] _latched = new bool[ButtonCount];

	/// <summary>
	/// Which block is read, <c>ControlsOptionBase</c> (<c>DAT_004d25fb</c>): the walker's twelve bytes
	/// or the RAZOR's.
	/// </summary>
	public bool PilotingRazor { get; set; }

	/// <summary>The four axis-sense switches from <c>data\keyjoy.cfg</c>.</summary>
	public KeyjoyConfig Keyjoy { get; set; } = KeyjoyConfig.Defaults;

	/// <summary>
	/// Whether the throttle lever is being read upside down — the <c>-1</c> half of
	/// <c>ThrottleLeverMode</c> (<c>0049a06e</c>).
	///
	/// <para><see cref="JoystickAction.ChangeDirection"/> flips it, and so does committing the cockpit
	/// slider (<c>ThrottleSlider_OnValue</c>, <c>00448378</c>). Both only ever move it between <c>+1</c> and <c>-1</c>: whether
	/// there is a lever at all is <see cref="ThrottleLeverMode"/>'s question, not this one.</para>
	/// </summary>
	public bool ThrottleLeverInverted { get; set; }

	/// <summary>
	/// Whether the lever is read centre-zero — <see cref="JoystickDeviceMap.BipolarThrottle"/>,
	/// which the host copies here because the control law reaches the mode through this and not
	/// through the map. <b>This engine's invention</b>; retail has only the end-to-end lever.
	/// </summary>
	public bool BipolarThrottle { get; set; }

	/// <summary>
	/// <c>Input_SetThrottleLeverMode</c> (<c>00459d20</c>): 0 when no physical lever is driving the
	/// throttle, otherwise non-zero for one, the sign being <see cref="ThrottleLeverInverted"/>.
	///
	/// <para>The magnitude is the mode: <see cref="Sim.MechControls.ThrottleLeverUnipolar"/>, or
	/// <see cref="Sim.MechControls.ThrottleLeverBipolar"/> when <see cref="BipolarThrottle"/> is
	/// set. Retail returns only the first.</para>
	///
	/// <para>Both of its conditions have to hold — the device must report a throttle control
	/// <b>and</b> the THROTTLE row must be assigned to <see cref="JoystickAxisAssignment.Movement"/>,
	/// which is the word THROTTLE rather than TURRET ELEVATION. A lever bound to the turret leaves this
	/// at 0, so the throttle keeps the keyboard's full ±0x400 range through zero into reverse.</para>
	/// </summary>
	public int ThrottleLeverMode(JoystickCapabilities capabilities, SimulatorPreferences preferences) {
		if (!capabilities.Present || !capabilities.HasThrottle
			|| Assignment(preferences, 1) != JoystickAxisAssignment.Movement) {
			return 0;
		}

		int mode = BipolarThrottle
			? Sim.MechControls.ThrottleLeverBipolar
			: Sim.MechControls.ThrottleLeverUnipolar;

		return ThrottleLeverInverted ? -mode : mode;
	}

	/// <summary>Drops every latch, so a held button fires once more.</summary>
	public void ResetLatches() => Array.Clear(_latched);

	/// <summary>
	/// Latches everything currently held, so nothing fires when the pilot's controls come back. Call it
	/// on every tick the stick is not being read — a modal panel is up, or the player is out of the
	/// cockpit.
	///
	/// <para>This is <c>AlertPanel_Enter</c>'s own "consume buttons 1-4 so one held at open does not
	/// act". Without it, a button held down through a panel fires the instant the panel closes, which
	/// is exactly what a player pressing a stick button to dismiss a dialog would trigger.</para>
	/// </summary>
	public void Suspend(JoystickReading reading) {
		for (int i = 0; i < ButtonCount; i++) {
			_latched[i] = reading.Button(i);
		}
	}

	/// <summary>
	/// Reads one axis row out of <paramref name="preferences"/>. Rows 0-3 are JOYSTICK, THROTTLE,
	/// RUDDER and HAT.
	/// </summary>
	public JoystickAxisAssignment Assignment(SimulatorPreferences preferences, int row) {
		byte value = preferences[SimulatorPreferences.ControlsBase(PilotingRazor) + row];
		return value <= (byte)JoystickAxisAssignment.Turret
			? (JoystickAxisAssignment)value
			: JoystickAxisAssignment.Unassigned;
	}

	/// <summary>What button <paramref name="index"/> (0-based) is bound to.</summary>
	public JoystickAction Action(SimulatorPreferences preferences, int index) =>
		(JoystickAction)preferences[
			SimulatorPreferences.ControlsBase(PilotingRazor)
			+ SimulatorPreferences.ControlsAxisCount + index];

	/// <summary>
	/// Resolves one tick. <paramref name="reading"/> is the stick as the device map built it and
	/// <paramref name="capabilities"/> what it can do; a row whose control the device does not have is
	/// skipped exactly as the original skips it, so a stick with no rudder cannot be steered by a
	/// RUDDER binding left over from one that had.
	/// </summary>
	public JoystickPilotInput Resolve(JoystickReading reading, JoystickCapabilities capabilities,
			SimulatorPreferences preferences) {
		if (!capabilities.Present) {
			ResetLatches();
			return JoystickPilotInput.None;
		}

		// The four axis sources, in destination order, and a second rank behind them. A control whose
		// pair-mate is already spoken for lands in the second rank instead, and the combine below
		// prefers the second when both have moved — the original's own arbitration, which is what lets
		// a rudder assigned to DIRECTION override the stick's own steering while it is being pushed.
		int?[] primary = new int?[4];
		int?[] secondary = new int?[4];

		var stickRow = Assignment(preferences, 0);

		switch (stickRow) {
			case JoystickAxisAssignment.Movement:
				primary[0] = reading.StickX;
				primary[1] = reading.StickY;
				break;

			case JoystickAxisAssignment.Turret:
				// A RAZOR flips the stick's fore/aft sense when it drives the turret pair, which for a
				// flyer is the rudder-and-throttle pair rather than a turret.
				primary[2] = reading.StickX;
				primary[3] = PilotingRazor ? -reading.StickY : reading.StickY;
				break;
		}

		var throttleRow = JoystickAxisAssignment.Unassigned;
		if (capabilities.HasThrottle) {
			throttleRow = Assignment(preferences, 1);

			// Unconditional in the original, ahead of the row test — a RAZOR reads the lever the other
			// way up whatever it is assigned to.
			int throttle = PilotingRazor ? -reading.Throttle : reading.Throttle;

			switch (throttleRow) {
				case JoystickAxisAssignment.Movement:
					Assign(primary, secondary, 1, throttle);
					break;

				case JoystickAxisAssignment.Turret:
					Assign(primary, secondary, 3, throttle);
					break;
			}
		}

		if (capabilities.HasRudder) {
			int rudder = Keyjoy.ReverseRudder ? -reading.Rudder : reading.Rudder;

			switch (Assignment(preferences, 2)) {
				case JoystickAxisAssignment.Movement:
					Assign(primary, secondary, 0, rudder);
					break;

				case JoystickAxisAssignment.Turret:
					Assign(primary, secondary, 2, rudder);
					break;
			}
		}

		// Second rank first, then first rank, and zero where neither moved so the keyboard can still
		// reach the axis — Combine finishes the chain.
		var axes = new PilotAxes(
			Pick(secondary[0], primary[0]),
			Pick(secondary[1], primary[1]),
			Pick(secondary[2], primary[2]),
			Pick(secondary[3], primary[3]));

		// The hat, which is not an axis source but writes straight over the turret pair. Only one
		// direction can apply: the original tests north, south, east then west and stops at the first,
		// so a hat reporting two at once resolves to the earliest of them.
		var views = JoystickHat.None;

		switch (Assignment(preferences, 3)) {
			case JoystickAxisAssignment.Turret:
				if (reading.Hat.HasFlag(JoystickHat.North)) {
					axes = axes with { TorsoPitch = HatAxis };
				} else if (reading.Hat.HasFlag(JoystickHat.South)) {
					axes = axes with { TorsoPitch = -HatAxis };
				} else if (reading.Hat.HasFlag(JoystickHat.East)) {
					axes = axes with { TorsoTwist = HatAxis };
				} else if (reading.Hat.HasFlag(JoystickHat.West)) {
					axes = axes with { TorsoTwist = -HatAxis };
				}

				break;

			// Under VIEWS the four bytes pass through untouched to CockpitView_PollViewDevice
			// (00432b14), which queues view commands 1, 0, 5 and 4 off them. Under either other
			// setting the original zeroes them first, so the view path sees nothing.
			case JoystickAxisAssignment.Movement:
				views = reading.Hat;
				break;
		}

		return new JoystickPilotInput(axes, ResolveFire(reading, preferences),
			ResolveButtons(reading, capabilities, preferences), views,
			KeyboardAimsTurret: stickRow == JoystickAxisAssignment.Movement,
			SuppressKeyboardPitch: capabilities.HasThrottle
				&& throttleRow != JoystickAxisAssignment.Unassigned);
	}

	/// <summary>
	/// Finishes the axis chain and applies the last of <c>data\keyjoy.cfg</c>: the joystick where it
	/// has moved, otherwise <paramref name="keyboard"/>, and then <c>Backturn</c>.
	///
	/// <para><paramref name="keyboard"/> is the keyboard's own two axis pairs, already in game-axis
	/// order. When <see cref="JoystickPilotInput.KeyboardAimsTurret"/> is set the original
	/// <b>moves</b> the first pair onto the second — with the stick steering, the arrow keys aim the
	/// turret instead of duplicating it — and negates the pitch half on the way, so the caller hands
	/// over the pair it would otherwise have steered with and lets this do the shuffle.</para>
	/// </summary>
	public PilotAxes Combine(JoystickPilotInput input, PilotAxes keyboard) {
		if (input.KeyboardAimsTurret) {
			keyboard = new PilotAxes(0, 0,
				keyboard.Steer != 0 ? keyboard.Steer : keyboard.TorsoTwist,
				keyboard.Throttle != 0 ? (short)-keyboard.Throttle : keyboard.TorsoPitch);
		}

		if (input.SuppressKeyboardPitch) {
			keyboard = keyboard with { TorsoPitch = 0 };
		}

		if (Keyjoy.ReverseTilt) {
			keyboard = keyboard with { TorsoPitch = (short)-keyboard.TorsoPitch };
		}

		var axes = input.Axes.Or(keyboard);

		// Backturn, applied dead last to the combined result: while the throttle axis is positive —
		// backing up — the steering axis is inverted, so the machine turns the way reversing a vehicle
		// turns.
		if (Keyjoy.ReverseBackturn && axes.Throttle > 0) {
			axes = axes with { Steer = (short)-axes.Steer };
		}

		return axes;
	}

	/// <summary>
	/// Whether the trigger is held. The button bound to <see cref="JoystickAction.Fire"/> is pulled
	/// out ahead of the dispatch loop and read as a held state, not an edge, which is why it never
	/// appears in <see cref="JoystickPilotInput.Pressed"/>.
	///
	/// <para><b>The original looks in the wrong block for it.</b> Its scan is
	/// <c>SimOptions[0x11 + i]</c> — a literal <c>0x11</c> at <c>0045b22b</c>, the walking block's
	/// first button row, where the dispatch loop one step later correctly uses
	/// <c>ControlsOptionBase + 4</c>. So a RAZOR finds its trigger through the <i>walker's</i>
	/// bindings. This engine uses the current block on both paths; see KNOWN_ISSUES.</para>
	/// </summary>
	private bool ResolveFire(JoystickReading reading, SimulatorPreferences preferences) {
		for (int i = 0; i < ButtonCount; i++) {
			if (Action(preferences, i) == JoystickAction.Fire) {
				return reading.Button(i);
			}
		}

		return false;
	}

	/// <summary>
	/// The actions whose buttons went down this tick, in button order, with the press-once latch
	/// applied.
	///
	/// <para><b>At most one action fires per tick</b>, which is the original's behaviour and a slip in
	/// it: the dedupe array is 21 entries wide, sized for the action codes, but
	/// <c>Sim_PollPlayerInput</c> indexes it with the <i>button byte</i> — always 0 or 1 — so the
	/// first pressed button claims the only usable slot. It costs nothing visible, because the button
	/// that fired is latched immediately and the next tick lets the one behind it through. Two buttons
	/// pressed together therefore act one tick apart rather than together.</para>
	/// </summary>
	private IReadOnlyList<JoystickAction> ResolveButtons(JoystickReading reading,
			JoystickCapabilities capabilities, SimulatorPreferences preferences) {
		List<JoystickAction>? pressed = null;
		bool claimed = false;
		int live = Math.Min(capabilities.ButtonCount, ButtonCount);

		for (int i = 0; i < ButtonCount; i++) {
			bool held = i < live && reading.Button(i);

			if (_latched[i]) {
				// Masked until it comes back up.
				if (!held) {
					_latched[i] = false;
				}

				continue;
			}

			if (!held || claimed) {
				continue;
			}

			claimed = true;
			_latched[i] = true;

			var action = Action(preferences, i);

			// FIRE is read as a held state elsewhere and has no case in the dispatch switch; OFF has
			// none either. Both still consume the tick's one slot, as they do in the original.
			if (action is not (JoystickAction.Off or JoystickAction.Fire)) {
				(pressed ??= new List<JoystickAction>(1)).Add(action);
			}
		}

		return pressed ?? (IReadOnlyList<JoystickAction>)Array.Empty<JoystickAction>();
	}

	/// <summary>Puts <paramref name="value"/> in the first free rank of destination <paramref name="axis"/>.</summary>
	private static void Assign(int?[] primary, int?[] secondary, int axis, int value) {
		if (primary[axis] is null) {
			primary[axis] = value;
		} else {
			secondary[axis] = value;
		}
	}

	/// <summary>The second rank where it has moved, else the first, else nothing.</summary>
	private static short Pick(int? second, int? first) =>
		(short)(second is { } value && value != 0 ? value : first ?? 0);
}
