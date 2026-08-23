namespace Herculan.Engine.Sim;

/// <summary>
/// One tick's worth of pilot input for a HERC, in the axis convention DBSIM's own device layer
/// hands to <c>Mech_ApplyThrottleInput</c> (<c>004160dc</c>): two signed stick axes, full deflection
/// at ±0x100, and the throttle-lever mode the original keeps as a global because only the player
/// has one.
/// </summary>
/// <param name="Turn">
/// Stick X. Negative is left, positive right — a joystick's own sign convention. The control law
/// inverts it internally depending on the direction of travel, which is what makes reverse steer
/// the way reversing a vehicle does.
/// </param>
/// <param name="Throttle">
/// Stick Y, applied as a rate: each tick moves the throttle setting by
/// <c>Q8(0x91, -Throttle)</c>, so holding full deflection covers the whole range in about seven
/// ticks. Negative (stick forward) opens the throttle.
///
/// <para>Held against the stop it runs the setting all the way from full forward to full reverse,
/// pausing exactly one tick at zero on the way through — the sign-crossing guard snaps to zero
/// rather than stepping past it, and the following tick starts again from there. That one-tick
/// detent is what the manual means by "Centered is stopped".</para>
///
/// <para>When <see cref="ThrottleLever"/> is set this axis is read as an absolute lever position
/// instead, and the rate path is skipped.</para>
/// </param>
/// <param name="ThrottleLever">
/// Whether a physical throttle lever is driving the throttle, and which way round: 0 for none —
/// keyboard and plain stick, and the value everything in Herculan currently produces — +1 for a
/// lever, and -1 for a lever whose sense is inverted.
///
/// <para>This is the original's <c>DAT_0049a06e</c>. <c>FUN_00459d20</c> sets it to 1 only when the
/// input configuration reports a throttle control <i>and</i> the preferences page has it assigned to
/// THROTTLE rather than TURRET, and to 0 otherwise; the key and the cockpit slider that "toggle" it
/// only ever flip between +1 and -1, and are themselves gated on that same pair, so they invert a
/// lever that exists rather than selecting anything.</para>
///
/// <para>It is <b>not</b> a forward/reverse gear selector, which is what the symbol table and an
/// earlier port of this file both took it for. Nothing in DBSIM selects a gear: the sign of the
/// throttle setting is the direction of travel, and zero here is what leaves the setting free to
/// take either sign. With a lever present the clamp closes to one side of zero, because a lever's
/// travel only spans one direction and the other has to come from inverting it.</para>
/// </param>
/// <param name="TorsoTwist">
/// The turret axis, left/right. Full deflection at ±0x100, as the two above. It is a
/// <i>rate</i> demand, not a position: <see cref="MechObject.TorsoTwistTick"/> builds the torso's
/// turn rate toward what this asks for and integrates that into the angle.
/// </param>
/// <param name="TorsoPitch">The turret axis, up/down. Positive looks up.</param>
/// <param name="CenterTorso">
/// The manual's [Backspace] "Center Turret" command — a mode, not a keypress: the original latches
/// it (<c>DAT_004d2588</c>) and runs the centring tick every tick until the pilot moves either
/// turret axis, which clears it. The host holds it the same way.
/// </param>
/// <param name="CenterBody">
/// The manual's [\] "Center Body" command — the other half of the pair, and a locomotion assist
/// rather than a turret one: it steers the <i>legs</i> round to line up under the turret, rather
/// than bringing the turret back to the legs.
///
/// <para>Read on its rising edge, not held: latching it (<c>DAT_004d2af4</c>) captures the world
/// direction the turret is pointing in (<c>DAT_004d2af8</c>), and everything after that is measured
/// against that one number, so it has to be taken once. It clears <see cref="CenterTorso"/> and is
/// cleared by it — the original's dispatch sets one of the two globals and zeroes the other
/// wherever it touches either.</para>
/// </param>
public readonly record struct MechControls(short Turn, short Throttle, int ThrottleLever = 0,
		short TorsoTwist = 0, short TorsoPitch = 0, bool CenterTorso = false,
		bool CenterBody = false) {
	/// <summary>Full stick deflection, in either direction.</summary>
	public const short AxisFull = 0x100;

	/// <summary>Hands off the controls.</summary>
	public static MechControls Neutral => new(0, 0);
}
