namespace Herculan.Engine.Content;

/// <summary>
/// The live values the cockpit's readouts display. Separate from <see cref="CockpitArt"/>, which is
/// the herc's layout and art and is loaded once: this is what changes frame to frame.
///
/// <para>Most of it is not wired to the simulation yet. <see cref="Default"/> is the state a herc
/// powers up in — full even shields, first hardpoint selected, unchained, unlinked — which is also
/// what the retail reference screenshots show, so what the engine draws can be compared against them
/// directly. Fields become live as the sim grows the state behind them.</para>
/// </summary>
/// <param name="WeaponNames">
/// Fitted hardpoint names, in <c>.GAU</c> slot order. Slots past the end draw their plate and number
/// with no name rather than an invented one.
/// </param>
/// <param name="SelectedWeapon">Index of the armed hardpoint — its row draws the selected plate and the brighter font.</param>
/// <param name="ShieldFront">Front shield readout, 0-200 against <see cref="ShieldRear"/>'s complement.</param>
/// <param name="ShieldRear">Rear shield readout.</param>
/// <param name="SpeedKph">Ground speed in K/H, as the readout under the reticle spells it.</param>
/// <param name="Throttle">
/// The console throttle slider's setting, Q10 over the gauge's own +/-0x400 range, positive
/// forward. It is the same number the piloted machine holds at <c>mech+0x290</c>, kept in step with
/// it by <c>MechObject.ExchangeCockpitThrottle</c> once a frame — see <see cref="ThrottleTrack"/>.
/// </param>
/// <param name="TorsoTwist">
/// The turret's twist angle, <c>mech+0x298</c> — what the front window's Rotation Indicator
/// shows. Binary angle relative to the machine's own heading, positive to the same side
/// <see cref="Herculan.Engine.Sim.MechControls.TorsoTwist"/> positive drives it.
/// </param>
/// <param name="MissionTime">Mission clock, rendered mm:ss.</param>
/// <param name="ChainCount">How many weapons the chain button fires per pull, 1-3 — its caption is that many <c>I</c>s.</param>
/// <param name="Mfd">Which screen the multi-function display is showing. F1-F6 select it, exactly as DBSIM's own mode buttons do.</param>
/// <param name="Hdd">Which screen the Heads-Down Display is showing. F7 and F8 select it, and either one also pans down to it.</param>
/// <param name="HddDamage">
/// Which component category <see cref="HddPage.DamageDetail"/> is listing. The manual binds [S], [I]
/// and [W] to it; the display's own up/down arrow buttons step through the same three.
/// </param>
/// <param name="PressedWidget">
/// The widget currently held down under the pointer, drawn in its lit frame for as long as it is —
/// the original's own <c>DAT_0049dbdc</c> plus the state byte it sets (docs/formats/cockpit-input.md
/// §7). Transient input state rather than simulation state, and it lives here for the same reason the
/// rest does: <see cref="CockpitWidgets"/> folds it into each widget's lit flag, so the one place that
/// decides what a widget looks like stays the one place, and no renderer needs a second parameter
/// threaded through it.
/// </param>
public readonly record struct CockpitHudState(
	IReadOnlyList<string> WeaponNames,
	int SelectedWeapon,
	int ShieldFront,
	int ShieldRear,
	int SpeedKph,
	short Throttle,
	short TorsoTwist,
	TimeSpan MissionTime,
	int ChainCount,
	MfdMode Mfd,
	HddPage Hdd,
	HddDamageView HddDamage,
	CockpitWidgetId? PressedWidget = null) {

	/// <summary>
	/// Power-up state: shields full and evenly balanced at 100/100 out of the 200-point pool
	/// <c>FUN_00444a68</c> scales to, first hardpoint armed, chain at one, stationary clock, and the
	/// MFD on the scanner — which is the screen <c>Gau_MfdPanelWidget</c> boots the display to with
	/// its own <c>SetMode(obj, 3)</c> call.
	///
	/// <para>The Heads-Down Display boots to its command display and structural damage for the same
	/// reason: <c>FUN_00448cc8</c> ends with <c>FUN_0044a5e4(obj, 0)</c> and <c>FUN_0045079c</c> with
	/// <c>FUN_00450b60(obj, 0)</c>.</para>
	/// </summary>
	public static CockpitHudState Default { get; } = new(
		WeaponNames: Array.Empty<string>(),
		SelectedWeapon: 0,
		ShieldFront: 100,
		ShieldRear: 100,
		SpeedKph: 0,
		Throttle: 0,
		TorsoTwist: 0,
		MissionTime: TimeSpan.Zero,
		ChainCount: 1,
		Mfd: MfdMode.Scanner,
		Hdd: HddPage.CommandDisplay,
		HddDamage: HddDamageView.Structural,
		PressedWidget: null);
}
