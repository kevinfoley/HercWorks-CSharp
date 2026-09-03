namespace Herculan.Engine.Content;

/// <summary>
/// The live values the cockpit's readouts display. Separate from <see cref="CockpitArt"/>, which is
/// the herc's layout and art and is loaded once: this is what changes frame to frame.
///
/// <para>Some of it is not wired to the simulation yet. <see cref="Default"/> is the state a herc
/// powers up in — full pool, an even shield balance, no weapons fitted, unchained, unlinked —
/// which is also what the retail reference screenshots show, so what the engine draws can be
/// compared against them directly. Fields become live as the sim grows the state behind them.</para>
/// </summary>
/// <param name="Weapons">
/// One entry per <c>.GAU</c> weapon row, in row order — see <see cref="WeaponRowState"/>. A row no
/// mount claims draws its plate and number with no name rather than an invented one.
/// </param>
/// <param name="HardpointNames">
/// The same mounts' bare names in <b>mount order</b> — the order the <c>.GL</c> hardpoint list puts
/// them in, which is not the row order. This is the list the Heads-Down Display's weapon-damage page
/// prints (<c>FUN_00450c54</c> walks the mount array directly), and it takes the mount's own name
/// with none of the cockpit row's pod suffix.
/// </param>
/// <param name="HardpointSlots">
/// Each of those mounts' <c>WeaponMount.LoadoutSlot</c>, so the same page can find the damage
/// component behind a row — see <see cref="PaperDollDamage.WeaponRowReading"/>. Parallel to
/// <see cref="HardpointNames"/>; the two orders are not the same, which is why the slot has to
/// travel with the name.
/// </param>
/// <param name="ShieldFront">
/// Front shield readout, 0-200. This is the shield <i>balance</i>, not the charge — the original
/// prints `balance * 200 >> 10` here and its complement below, so the pair always sums to 200 even
/// on an empty array. Charge is shown by the meter rings instead.
/// </param>
/// <param name="ShieldRear">Rear shield readout — exactly 200 minus <see cref="ShieldFront"/>.</param>
/// <param name="EnergyFraction">
/// The Master Energy Pool's fill, Q10 over 0-1024 — <c>MechObject.EnergyPoolFraction</c>, which is
/// the same <c>(pool &lt;&lt; 10) / 10000</c> <c>Player_PerFrameCockpitUpdate</c> hands the meter
/// widget, and 1024 is the range that widget's LED bar was built with.
/// </param>
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
/// <param name="ChainGroup">
/// Which of the three fire chains is selected, 0-2 — <c>WeaponMounts.Group</c>. The chain button is
/// captioned with that many <c>I</c>s from a literal table in the executable, so chain 0 reads "I".
/// </param>
/// <param name="AutoTrack">Whether the TRACK button is latched — <c>WeaponMounts.AutoTrack</c>.</param>
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
/// <param name="Target">
/// The front-window target indicator's resolved state, or null when nothing is selected (or the
/// indicator has never been armed, which is <c>TargetSelection.IndicatorArmed</c>). See
/// <see cref="TargetIndicator"/> and <see cref="TargetBox"/>.
/// </param>
/// <param name="StatusSubject">
/// What F1's status screen is looking at - the player's own machine.
/// </param>
/// <param name="TargetSubject">
/// And what F5's is: the current selection. Same screen class, same record, different subject -
/// see <see cref="MfdStatusSubject"/>.
/// </param>
/// <param name="Command">
/// What the Heads-Down Display's command display draws that the herc's <c>.GAU</c> does not supply —
/// the map camera and its markers, the three comm boxes, and which pilot and order are selected. See
/// <see cref="HddCommandState"/>.
/// </param>
/// <param name="Scanner">
/// What F4's scanner plots — its contacts, its display range and the radar mode it mirrors from the
/// machine. See <see cref="MfdScannerState"/>.
/// </param>
public readonly record struct CockpitHudState(
	IReadOnlyList<WeaponRowState> Weapons,
	IReadOnlyList<string> HardpointNames,
	int ShieldFront,
	int ShieldRear,
	int EnergyFraction,
	int SpeedKph,
	short Throttle,
	short TorsoTwist,
	TimeSpan MissionTime,
	int ChainGroup,
	bool AutoTrack,
	MfdMode Mfd,
	HddPage Hdd,
	HddDamageView HddDamage,
	CockpitWidgetId? PressedWidget = null,
	TargetIndicator? Target = null,
	MfdStatusSubject StatusSubject = default,
	MfdStatusSubject TargetSubject = default,
	MfdScannerState Scanner = default,
	MessageTicker Message = default,
	HddCommandState Command = default,
	IReadOnlyList<int>? HardpointSlots = null) {

	/// <summary>
	/// Power-up state: an even shield balance printing 100/100 the way <c>ShieldsGauge_UpdateReadouts</c>
	/// scales it (a balance readout, not a charge one), no weapon panel, chain at one, stationary clock, and the
	/// MFD on the scanner — which is the screen <c>Gau_MfdPanelWidget</c> boots the display to with
	/// its own <c>SetMode(obj, 3)</c> call.
	///
	/// <para>The Heads-Down Display boots to its command display and structural damage for the same
	/// reason: <c>FUN_00448cc8</c> ends with <c>FUN_0044a5e4(obj, 0)</c> and <c>FUN_0045079c</c> with
	/// <c>FUN_00450b60(obj, 0)</c>.</para>
	/// </summary>
	public static CockpitHudState Default { get; } = new(
		Weapons: Array.Empty<WeaponRowState>(),
		HardpointNames: Array.Empty<string>(),
		ShieldFront: 100,
		ShieldRear: 100,
		EnergyFraction: 1024,
		SpeedKph: 0,
		Throttle: 0,
		TorsoTwist: 0,
		MissionTime: TimeSpan.Zero,
		ChainGroup: 0,
		AutoTrack: false,
		Mfd: MfdMode.Scanner,
		Hdd: HddPage.CommandDisplay,
		HddDamage: HddDamageView.Structural,
		PressedWidget: null,
		Target: null,
		StatusSubject: MfdStatusSubject.None,
		TargetSubject: MfdStatusSubject.None,
		Scanner: MfdScannerState.Empty,
		Message: default,
		Command: default,
		HardpointSlots: Array.Empty<int>());
}
