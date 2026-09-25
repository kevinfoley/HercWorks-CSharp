using System.Runtime.InteropServices;
using Herculan.Engine.Sim;
using Herculan.Engine.World;
using Silk.NET.Input;

namespace Herculan.Engine.Host;

/// <summary>
/// DBSIM's <c>-SPRUNKNOWN</c> developer keys, which <c>--developer</c> turns on here, and the
/// <c>Alt+S</c> freeze that retail also allows while a tape plays. What each key does is
/// docs/key-bindings.md's "Developer keys"; the retail cases behind them are docs/command-line.md's
/// "<c>-SPRUNKNOWN</c>: the developer keys". This holds their state; the host applies it.
///
/// <para>Retail keeps the freeze in the one word every modal panel also raises (<c>004d2576</c>),
/// but the two freeze different amounts: a panel's own loop never reaches <c>Sim_MainTick</c>,
/// while under <c>Alt+S</c> the tick runs with most of it switched off. So <see cref="Frozen"/> is
/// the developer half alone, and the host runs <see cref="SimWorld.TickFrozen()"/> for it.</para>
///
/// <para>Every key here acts on each key-down event, auto-repeat included, as the dispatcher's
/// commands do: the keyboard queues one per <c>WM_KEYDOWN</c>. So a held move key keeps moving, at
/// the repeat delay and rate Windows is set to.</para>
/// </summary>
sealed class DeveloperKeys(bool enabled) {
	/// <summary>
	/// <c>Mech_HandleCommand</c>'s two step tables for <c>Ctrl+Alt+1</c>-<c>9</c>, the move at
	/// <c>0049a020</c> and the turn at <c>0049a032</c>. The two hold the same nine values.
	/// </summary>
	private static readonly short[] StepSizes = { 500, 1000, 1500, 2000, 3000, 4500, 6000, 7500, 9000 };

	/// <summary>
	/// The entry both steps start on, <c>Ctrl+Alt+4</c>'s — loaded at every mission start by the mech
	/// module's phase-2 subsystem loader (<c>00415464</c>).
	/// </summary>
	private const int InitialStep = 3;

	private static readonly Key[] StepKeys = {
		Key.Number1, Key.Number2, Key.Number3, Key.Number4, Key.Number5,
		Key.Number6, Key.Number7, Key.Number8, Key.Number9,
	};

	/// <summary>The highest component <c>Ctrl+Alt+.</c> reaches: <c>004d2584</c> stops at <c>0x1d</c>.</summary>
	private const int LastComponent = 29;

	/// <summary><c>Ctrl+Alt+D</c>'s damage.</summary>
	private const short ComponentHit = 300;

	/// <summary><c>Ctrl+Alt+N</c>'s damage, all on component 0.</summary>
	private const short CybridHit = 32000;

	/// <summary>How far from the player <c>Ctrl+Alt+N</c> looks, by <c>Math_DistanceBetweenPoints</c>.</summary>
	private const int CybridHitRange = 99999;

	/// <summary>
	/// Below this height <c>Ctrl+N</c>/<c>Ctrl+P</c> step past an object — where a destroyed flyer is
	/// sent (<see cref="FlyerObject"/>'s wreck drop).
	/// </summary>
	private const int OffWorldHeight = -99000;

	/// <summary>Whether <c>--developer</c> is on — <c>DAT_0049ef60</c>.</summary>
	public bool Enabled { get; } = enabled;

	/// <summary>The <c>Alt+S</c> freeze.</summary>
	public bool Frozen { get; private set; }

	/// <summary>
	/// <c>Alt+keypad +</c>'s request for one tick, <c>004d2580</c>. It lifts the freeze, and
	/// <see cref="FinishStep"/> puts it back once the tick has run.
	/// </summary>
	public bool StepPending { get; private set; }

	/// <summary>
	/// <c>InputDrivesCamera</c> (<c>004d2574</c>) — the controls are off the machine: it neither fires
	/// nor takes the steering, throttle or turret axes.
	/// </summary>
	public bool InputDrivesCamera { get; private set; }

	/// <summary>
	/// The object <c>Ctrl+N</c>/<c>Ctrl+P</c> moved the camera to — <c>DAT_004d2708</c> — or null for
	/// the player's own machine, where it starts. <c>Ctrl+Alt+D</c> hits it and the move keys move it.
	/// </summary>
	public SimObject? Viewed { get; private set; }

	private short _moveStep = StepSizes[InitialStep];
	private short _turnStep = StepSizes[InitialStep];
	private int _component;
	private readonly HashSet<Key> _held = new();

	// Auto-repeat: only the last key to go down repeats, as the keyboard's own does, and a modifier
	// going down takes the repeat off whatever had it.
	private Key? _repeating;
	private double _repeatWait;
	private bool _modifiersWere;
	private readonly (double Delay, double Interval) _typematic = Typematic();

	/// <summary>Called once a stepped tick has run: the freeze goes back on.</summary>
	public void FinishStep() {
		StepPending = false;
		Frozen = true;
	}

	/// <summary>
	/// One frame of the keys. Every edge is read every frame, whatever the modifiers say, so a key
	/// already held when <c>Ctrl</c> or <c>Alt</c> goes down does not fire on it.
	/// </summary>
	public void Read(IKeyState keys, SimWorld world, MechObject player, bool tapePlaying,
			double deltaSeconds) {
		bool ctrl = keys.IsKeyPressed(Key.ControlLeft) || keys.IsKeyPressed(Key.ControlRight);
		bool alt = keys.IsKeyPressed(Key.AltLeft) || keys.IsKeyPressed(Key.AltRight);

		if ((ctrl || alt) && !_modifiersWere) {
			_repeating = null;
		}

		_modifiersWere = ctrl || alt;

		// The repeat that falls due this frame, if any, is decided once for every key below to read.
		Key? repeated = null;
		if (_repeating is { } repeatKey && keys.IsKeyPressed(repeatKey)) {
			_repeatWait -= deltaSeconds;
			if (_repeatWait <= 0) {
				_repeatWait += _typematic.Interval;
				repeated = repeatKey;
			}
		} else {
			_repeating = null;
		}

		bool KeyDown(Key key) => Pressed(keys, key, repeated);
		bool altOnly = alt && !ctrl;
		bool ctrlOnly = ctrl && !alt;
		bool ctrlAlt = ctrl && alt;

		bool freezeKey = KeyDown(Key.S);
		bool stepKey = KeyDown(Key.KeypadAdd);
		bool upKey = KeyDown(Key.Up) | KeyDown(Key.Keypad8);
		bool downKey = KeyDown(Key.Down) | KeyDown(Key.Keypad2);
		bool leftKey = KeyDown(Key.Left) | KeyDown(Key.Keypad4);
		bool rightKey = KeyDown(Key.Right) | KeyDown(Key.Keypad6);
		bool nextKey = KeyDown(Key.N);
		bool previousKey = KeyDown(Key.P);
		bool followKey = KeyDown(Key.F);
		bool handOffKey = KeyDown(Key.T);
		bool componentUpKey = KeyDown(Key.Period);
		bool componentDownKey = KeyDown(Key.Comma);
		bool damageKey = KeyDown(Key.D);
		int stepKeyIndex = -1;
		for (int i = 0; i < StepKeys.Length; i++) {
			if (KeyDown(StepKeys[i]) && stepKeyIndex < 0) {
				stepKeyIndex = i;
			}
		}

		// Sim_DispatchCommand's 0x21f: live under the flag, and while a tape records or plays without it.
		if (altOnly && freezeKey && (Enabled || tapePlaying)) {
			Frozen = !Frozen;
			Console.WriteLine($"Developer: simulation {(Frozen ? "frozen" : "running")}.");
		}

		if (!Enabled) {
			return;
		}

		if (altOnly && stepKey) {
			StepPending = true;
			Frozen = false;
		}

		// The move keys are the machine's own command handler's, and retail hands that handler's commands
		// to the viewed object, so they move a viewed machine and nothing else — see
		// docs/command-line.md's developer keys.
		if ((Viewed ?? player) is MechObject subject) {
			if (altOnly && upKey) {
				subject.Displace(0, _moveStep);
			}

			if (altOnly && downKey) {
				subject.Displace(0, (short)-_moveStep);
			}

			if (altOnly && rightKey) {
				subject.Displace(_moveStep, 0);
			}

			if (altOnly && leftKey) {
				subject.Displace((short)-_moveStep, 0);
			}

			if (ctrlOnly && leftKey) {
				subject.TurnBy(_turnStep);
			}

			if (ctrlOnly && rightKey) {
				subject.TurnBy(-_turnStep);
			}

			if (ctrlAlt && stepKeyIndex >= 0) {
				_moveStep = _turnStep = StepSizes[stepKeyIndex];
				Console.WriteLine($"Developer: move step {_moveStep}, turn step {_turnStep}.");
			}
		}

		if (ctrlOnly && (nextKey || previousKey)) {
			CycleViewed(world, player, forward: nextKey);
		}

		// Ctrl+F: the external-view cycle [V] runs, with DAT_004d25b8 set so the outside camera follows
		// the viewed object. Retail's external cameras are not ported; see ExternalCamera.
		if (ctrlOnly && followKey) {
			Console.WriteLine("Developer: Ctrl+F needs retail's external cameras, which are not implemented.");
		}

		if (ctrlOnly && handOffKey) {
			InputDrivesCamera = !InputDrivesCamera;
			Console.WriteLine($"Developer: controls {(InputDrivesCamera ? "off" : "back on")} the machine.");
		}

		if (ctrlAlt && componentUpKey && _component < LastComponent) {
			_component++;
			Console.WriteLine($"Developer: Ctrl+Alt+D hits component {_component}.");
		}

		if (ctrlAlt && componentDownKey && _component > 0) {
			_component--;
			Console.WriteLine($"Developer: Ctrl+Alt+D hits component {_component}.");
		}

		if (ctrlAlt && damageKey) {
			var victim = Viewed ?? player;
			victim.ApplyComponentDamage(world, _component, ComponentHit, player);
			Console.WriteLine($"Developer: {ComponentHit} damage to component {_component} of {victim.GetType().Name}.");
		}

		if (ctrlAlt && nextKey) {
			// The first object in the live list that is in the mission, Cybrid, not destroyed and in range.
			var victim = world.Objects.FirstOrDefault(candidate => !candidate.Removed
				&& !candidate.AwaitingDeployment && !candidate.Destroyed
				&& candidate.Side == MissionSide.Cybrid
				&& player.Position.ApproxDistanceTo(candidate.Position) <= CybridHitRange);
			victim?.ApplyComponentDamage(world, 0, CybridHit, player);
			Console.WriteLine(victim != null
				? $"Developer: {CybridHit} damage to a {victim.GetType().Name}."
				: "Developer: no Cybrid in range.");
		}
	}

	// Ctrl+N and Ctrl+P: the next or previous object in the live list from the one being viewed, round
	// the end of the list. Either hands the controls off, even when it lands back on the player's own
	// machine — the dispatcher sets 004d2574 after FUN_0045df18 has restored it.
	private void CycleViewed(SimWorld world, MechObject player, bool forward) {
		var objects = world.Objects;
		var current = Viewed ?? player;
		int start = current.ListIndex;
		if (objects.Count == 0 || start < 0) {
			return;
		}

		int step = forward ? 1 : objects.Count - 1;
		for (int i = (start + step) % objects.Count; i != start; i = (i + step) % objects.Count) {
			var candidate = objects[i];
			if (candidate.Removed || candidate.Position.Z < OffWorldHeight) {
				continue;
			}

			Viewed = ReferenceEquals(candidate, player) ? null : candidate;
			InputDrivesCamera = true;
			Console.WriteLine($"Developer: viewing {candidate.GetType().Name} {i}; the controls are off the machine.");
			return;
		}
	}

	// A key-down event: the key has just gone down, or it is the one repeating and a repeat is due.
	private bool Pressed(IKeyState keys, Key key, Key? repeated) {
		if (!keys.IsKeyPressed(key)) {
			_held.Remove(key);
			return false;
		}

		if (_held.Add(key)) {
			_repeating = key;
			_repeatWait = _typematic.Delay;
			return true;
		}

		return repeated == key;
	}

	// Windows' own keyboard delay (0-3, a quarter second each from 250 ms) and speed (0-31, about 2.5
	// to 30 repeats a second), which is what paced retail's repeats. Its defaults elsewhere.
	private static (double Delay, double Interval) Typematic() {
		int delay = 1;
		int speed = 31;
		if (OperatingSystem.IsWindows()) {
			SystemParametersInfo(GetKeyboardDelay, 0, ref delay, 0);
			SystemParametersInfo(GetKeyboardSpeed, 0, ref speed, 0);
		}

		return (0.25 * (Math.Clamp(delay, 0, 3) + 1), 1.0 / (2.5 + Math.Clamp(speed, 0, 31) * 27.5 / 31));
	}

	private const uint GetKeyboardSpeed = 0x0a;
	private const uint GetKeyboardDelay = 0x16;

	[DllImport("user32.dll")]
	private static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint winIni);
}
