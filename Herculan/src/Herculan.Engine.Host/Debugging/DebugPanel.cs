using System.Numerics;
using Herculan.Engine.Content;
using Herculan.Engine.Numerics;
using Herculan.Engine.Render;
using Herculan.Engine.Sim;
using Herculan.Engine.Terrain;
using ImGuiNET;
using Silk.NET.Input;

namespace Herculan.Engine.Host.Debugging;

/// <summary>
/// The debug settings panel, on [Esc] — which therefore no longer quits the host; close the window
/// for that. It is drawn over whatever view is up, cockpit or external.
///
/// <para>Built with ImGui, the toolkit the editor host already uses, rather than the game's own HUD
/// font: that font and its sprite banks are the original's art placed from the original's own layout
/// files, and bending them into a live settings panel would cost far more than adding a toolkit
/// already in the tree. Nothing the <i>game</i> draws goes through ImGui.</para>
///
/// <para>Everything it shows is read from live simulation state and everything it sets is a
/// host-side view option — nothing here feeds back into the sim, so leaving it open cannot change
/// what it is reporting. The two [Drain] buttons are the exception, and are test seams rather than
/// mechanics. See docs/engine/handoff-player-movement.md for what the readouts are for.</para>
/// </summary>
sealed class DebugPanel {
	/// <summary>Whether the panel is up. Toggled by [Esc]; see <see cref="ReadToggleKey"/>.</summary>
	public bool IsOpen { get; set; }

	/// <summary>Whether the host should draw the animating skeleton over the world.</summary>
	public bool DrawSkeleton { get; private set; } = true;

	/// <summary>
	/// Joints in the last skeleton the host built, for the readout. Set by whatever draws the
	/// skeleton, since that is the only thing that knows.
	/// </summary>
	public int SkeletonJointCount { get; set; }

	private bool _escapeDown;

	// "Steady eye" pins the eye's *height* to whatever it was the moment the toggle went on and
	// leaves everything else — the machine's own travel, its lean, the eye's fore/aft swing — alone.
	// That isolates the vertical bob from the ride without touching the animation that produces
	// either, which is the A/B for "is it the eye or the machine?".
	private bool _steadyEye;
	private bool _steadyEyeCaptured;
	private int _steadyEyeRiseUnits;

	// What the panel reports about the walk. The eye's rise above the machine's own origin is the
	// whole of the cockpit bob (see MechObject.EyePosition), so tracking its swing turns "it feels
	// wrong" into a number that can be checked against the 0.24-0.42 m a retail stride is supposed
	// to cover.
	private float _eyeRiseMeters;
	private float _eyeRiseMin = float.MaxValue;
	private float _eyeRiseMax = float.MinValue;
	private float _lastStepMeters;
	private Vec3i _lastMechPosition;
	private bool _haveLastPosition;

	/// <summary>
	/// Opens and closes the panel on [Esc]. Call before the host's own ImGui keyboard-capture gate
	/// and on the key's own edge, so the key that opens the panel is also the key that closes it
	/// however ImGui feels about focus.
	/// </summary>
	public void ReadToggleKey(IKeyboard? keyboard) {
		if (keyboard == null) {
			return;
		}

		bool down = keyboard.IsKeyPressed(Key.Escape);
		if (down && !_escapeDown) {
			IsOpen = !IsOpen;
		}

		_escapeDown = down;
	}

	/// <summary>
	/// Applies "steady eye" to a cockpit eye position: with the option off this returns
	/// <paramref name="eye"/> untouched, with it on the eye's height is held at the rise it had the
	/// moment the option was switched on.
	/// </summary>
	public Vec3i PinEyeHeight(Vec3i eye, Vec3i mechPosition) {
		if (!_steadyEye) {
			_steadyEyeCaptured = false;
			return eye;
		}

		if (!_steadyEyeCaptured) {
			_steadyEyeRiseUnits = eye.Z - mechPosition.Z;
			_steadyEyeCaptured = true;
		}

		return new Vec3i(eye.X, eye.Y, mechPosition.Z + _steadyEyeRiseUnits);
	}

	/// <summary>
	/// Takes this frame's walk measurements. Called every frame whether or not the panel is open, so
	/// opening it mid-stride shows the stride rather than starting from nothing — and so the min/max
	/// swing is a record of the walk, not of how long the panel has been up.
	/// </summary>
	public void Sample(MechObject? mech) {
		if (mech == null) {
			return;
		}

		_eyeRiseMeters = (mech.EyePosition.Z - mech.Position.Z) / WorldScale.WorldUnitsPerMeter;
		_eyeRiseMin = Math.Min(_eyeRiseMin, _eyeRiseMeters);
		_eyeRiseMax = Math.Max(_eyeRiseMax, _eyeRiseMeters);

		if (_haveLastPosition) {
			var step = mech.Position;
			_lastStepMeters = new Vector2(
				(step.X - _lastMechPosition.X) / WorldScale.WorldUnitsPerMeter,
				(step.Y - _lastMechPosition.Y) / WorldScale.WorldUnitsPerMeter).Length();
		}

		_lastMechPosition = mech.Position;
		_haveLastPosition = true;
	}

	/// <summary>
	/// Builds this frame's ImGui draw list for the panel. Does nothing while <see cref="IsOpen"/> is
	/// false, so a host can call it unconditionally.
	/// </summary>
	public void Draw(in DebugPanelContext context, int windowHeight) {
		if (!IsOpen) {
			return;
		}

		ImGui.SetNextWindowPos(new Vector2(16f, 16f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new Vector2(340f, MathF.Min(windowHeight - 32f, 560f)), ImGuiCond.FirstUseEver);
		ImGui.Begin("Debug — Esc closes");

		bool skeleton = DrawSkeleton;
		if (ImGui.Checkbox("Draw skeleton", ref skeleton)) {
			DrawSkeleton = skeleton;
		}

		bool steady = _steadyEye;
		if (ImGui.Checkbox("Steady eye (pin cockpit height)", ref steady)) {
			_steadyEye = steady;
		}

		ImGui.Separator();
		ImGui.Text("View: " + (!context.Piloting ? "free camera"
			: context.ExternalView ? "external" : "cockpit"));

		if (context.PilotMech is not { } pilotMech) {
			ImGui.TextWrapped("No player machine in this mission, so there is nothing to report.");
			ImGui.End();
			return;
		}

		ImGui.Text($"Machine: {pilotMech.Name}");
		ImGui.Text($"Skeleton joints: {SkeletonJointCount}");
		ImGui.Text($"Posed geometry nodes: {context.PosedNodeCount}"
			+ " (visible in the external view, [V])");

		ImGui.Separator();
		if (pilotMech.Thread is { } thread) {
			var sequence = pilotMech.Animation?.Sequences[thread.Sequence];
			ImGui.Text($"Sequence: {thread.Sequence}  frame {thread.Frame}"
				+ (sequence != null ? $" / {sequence.FrameCount}" : ""));
			ImGui.Text($"Target: {thread.TargetSequence}  {(thread.AtTarget ? "reached" : "seeking")}"
				+ (thread.InTransition ? ", in transition" : ""));
			ImGui.Text($"Rate: {thread.Rate}  (mech AnimRate {pilotMech.AnimRate})");
			ImGui.Text($"Root motion: {(sequence?.GroundMovement == true ? "yes" : "no")}");
		} else {
			ImGui.TextWrapped("No animation data — this machine cannot walk.");
		}

		// The turret's own state. "Drawn" is where the animation actually put the eye, which is not
		// the same as the angle the sim holds — the twist sequence's keyframes are not evenly spaced,
		// so the two drift apart by up to about 7% across the travel. See
		// docs/simulation/mech-locomotion.md.
		ImGui.Separator();
		var turret = pilotMech.EyeTransform.ToEuler();
		ImGui.Text($"Turret twist: {Degrees(pilotMech.TorsoTwistAngle):F1} deg"
			+ $" (limit {Degrees(pilotMech.Type.TorsoTwistLimit):F1}), rate {pilotMech.TorsoTwistRate}");
		ImGui.Text($"Turret pitch: {Degrees(pilotMech.TorsoPitchAngle):F1} deg"
			+ $" ({Degrees(pilotMech.Type.TorsoPitchMin):F1} to {Degrees(pilotMech.Type.TorsoPitchMax):F1})"
			+ $", rate {pilotMech.TorsoPitchRate}");
		ImGui.Text($"Drawn: twist {Degrees((short)(turret.Z - pilotMech.Heading)):F1} deg,"
			+ $" pitch {Degrees(turret.X):F1} deg");
		ImGui.Text(pilotMech.CenteringBody
			? $"Centring: body, onto {Degrees(pilotMech.CenterBodyReference):F1} deg"
			: pilotMech.CenteringTorso ? "Centring: turret" : "Centring: none");

		ImGui.Separator();
		ImGui.Text($"Throttle: {pilotMech.Throttle} / {ThrottleTrack.Full}");
		ImGui.Text($"Speed: {pilotMech.Speed} raw, {pilotMech.DisplaySpeedKph} km/h");
		ImGui.Text($"Gait: {(Math.Abs(pilotMech.Speed) >= pilotMech.Type.GaitThreshold ? "run" : "walk")}");
		ImGui.Text($"Step this frame: {_lastStepMeters:F3} m");

		ImGui.Separator();
		var position = pilotMech.Position;
		ImGui.Text($"Position: {position.X}, {position.Y}, {position.Z}");
		ImGui.Text($"Heading: {BinaryAngle.ToRadians(pilotMech.Heading) * (180f / MathF.PI):F1} deg");
		ImGui.Text($"Lean: pitch {BinaryAngle.ToRadians(pilotMech.Pitch) * (180f / MathF.PI):F1}, "
			+ $"roll {BinaryAngle.ToRadians(pilotMech.Roll) * (180f / MathF.PI):F1} deg");
		ImGui.Text($"Ground clearance: "
			+ $"{(position.Z - context.Terrain.HeightAtWorld(position.X, position.Y)) / WorldScale.WorldUnitsPerMeter:F2} m");

		// Reactor and Master Energy Pool. Nothing spends the pool yet except the shields — weapons
		// are a later milestone — so a machine standing still sits at a full 10000 and the cockpit's
		// energy bar stays hard right, which is correct rather than unwired. [Drain shields] is the
		// test seam that makes the trickle visible: watch the pool dip below full while the array
		// rebuilds at its 5-a-tick cap, then climb back once the deficit closes.
		var pods = pilotMech.Pods;
		var shields = pilotMech.Shields;
		ImGui.Separator();
		ImGui.Text($"Energy pool: {pilotMech.EnergyPool} / {MechObject.EnergyPoolMax}"
			+ $"  (reserve {MechObject.EnergyPoolReserve})");
		ImGui.Text($"Reactor output: {pilotMech.ReactorOutputRate}"
			+ $"  (base {MechObject.BaseReactorOutputRate}{(pods.EnergyPod ? ", Energy Pod fitted" : "")})");
		ImGui.Text($"Shields: {shields.Front} front, {shields.Rear} rear"
			+ $" of {shields.Max}{(pods.ShieldPod ? " (Shield Pod fitted)" : "")}");
		ImGui.Text($"  rings: {CockpitPalette.ShieldFacingCharge(shields.Front, shields.BaseMax)}"
			+ $" / {CockpitPalette.ShieldFacingCharge(shields.Rear, shields.BaseMax)} of 0x400");
		// The printed numbers are the balance and nothing else — they sum to 200 even on an empty array.
		ImGui.Text($"Balance: {shields.Balance} / {ShieldCharge.BalanceMax}"
			+ $"  — prints {shields.FrontReadout} / {shields.RearReadout}   [ and ] move it");

		// Both are test seams, not mechanics — see their own summaries. They are the only way to
		// watch either system refill until weapons and incoming fire exist to empty them for real.
		if (ImGui.Button("Drain shields")) {
			shields.Empty();
		}

		ImGui.SameLine();
		if (ImGui.Button("Drain energy pool")) {
			pilotMech.DrainEnergyPoolForTest();
		}

		// The bob itself. A retail stride is supposed to swing the eye 0.24-0.42 m (see
		// MechObject.EyePosition), so a swing far outside that band is the measurement that turns the
		// complaint into a lead.
		ImGui.Separator();
		ImGui.Text($"Eye rise: {_eyeRiseMeters:F3} m");
		if (_eyeRiseMin <= _eyeRiseMax) {
			ImGui.Text($"  seen {_eyeRiseMin:F3} .. {_eyeRiseMax:F3} m");
			ImGui.Text($"  swing {_eyeRiseMax - _eyeRiseMin:F3} m (retail stride: 0.24-0.42)");
		}

		if (ImGui.Button("Reset swing")) {
			_eyeRiseMin = float.MaxValue;
			_eyeRiseMax = float.MinValue;
		}

		ImGui.End();
	}

	/// <summary>A binary angle in degrees, for the panel's readouts.</summary>
	private static float Degrees(int binaryAngle) => BinaryAngle.ToRadians(binaryAngle) * (180f / MathF.PI);
}

/// <summary>
/// The live state <see cref="DebugPanel.Draw"/> reports on, handed in each frame rather than held by
/// the panel: all of it belongs to the host's loop, and the panel only reads it.
/// </summary>
/// <param name="Piloting">Whether the player is flying the machine rather than the free camera.</param>
/// <param name="ExternalView">Whether the chase camera is up rather than the cockpit.</param>
/// <param name="PilotMech">The player's machine, or null if the mission fields none.</param>
/// <param name="PosedNodeCount">Geometry segments the player's model is drawn as.</param>
/// <param name="Terrain">The zone's heightmap, for the ground-clearance readout.</param>
readonly record struct DebugPanelContext(
	bool Piloting,
	bool ExternalView,
	MechObject? PilotMech,
	int PosedNodeCount,
	HeightGrid Terrain);
