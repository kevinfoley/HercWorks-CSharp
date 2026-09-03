using HercWorks.Core.Data.File.Dat.Sim;
using Herculan.Engine.Audio;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

// The RAZOR's flight path: everything a chassis whose type record sets MechTypeRecord.IsFlyer does
// instead of walking. Ported from Razor_ApplyFlightInput (0041bb9c, the input hand-off),
// FlightModel_Step (00466a54, the flight model proper) and Razor_MovementTick (004198f4, the
// per-tick move). See docs/simulation/razor-flight.md.
public sealed partial class MechObject {
	/// <summary>
	/// The airspeed a flyer powers up at — <c>Mech_Constructor</c>'s literal into <c>mech+0x2bd</c>.
	/// An aircraft cannot be handed to the pilot at rest, and this is well above the RAZOR's own
	/// idle airspeed of 250.
	/// </summary>
	private const int InitialAirSpeed = 1000;

	/// <summary>Full throttle either way — the same ±0x400 range the walker's throttle spans.</summary>
	private const short FlightThrottleFull = 0x400;

	/// <summary>Q8 gain from the throttle axis to throttle movement per unit time.</summary>
	private const int FlightThrottleRate = 100;

	/// <summary>
	/// Q10 gain from pitch attitude to demanded airspeed, <b>nose down</b>. Four times the nose-up
	/// figure, so a dive builds speed far faster than a climb sheds it. This is the whole of the
	/// aircraft's gravity, and note where it lands: on the speed the throttle asks for, not on
	/// velocity. Level out and the speed returns to whatever the throttle wants — there is no
	/// momentum to trade.
	/// </summary>
	private const int DiveSpeedGain = 250;

	/// <inheritdoc cref="DiveSpeedGain"/>
	private const int ClimbSpeedGain = 62;

	/// <summary>Q10 gain from how far over the ceiling the aircraft is to the nose-down push.</summary>
	private const int CeilingPushGain = 10;

	/// <summary>
	/// The fraction of any forward speed lost to a turn that is handed straight back, Q10. Velocity
	/// is re-expressed in the airframe's new body frame every tick, which costs forward speed
	/// whenever it rotates; 900/1024 of that is returned, so a hard turn scrubs about 12% and no
	/// more.
	/// </summary>
	private const int TurnSpeedRecovery = 900;

	/// <summary>The bank angle a lost wing drifts the aircraft toward — 0x1000 is 22.5°.</summary>
	private const short LostWingBank = 0x1000;

	/// <summary>The Q14 gain toward <see cref="LostWingBank"/>. Small: it is a lean, not a spin.</summary>
	private const int LostWingGain = 0x14;

	/// <summary>
	/// The airframe components the flight path knows by name, in the game's own terms: the
	/// <c>STRINGS0</c> group 14 damage-readout list a flyer subject takes in place of the walker's
	/// group 13 reads 0 COCKPIT ARMOR, 4/5 L/R NACELLE ARMOR, 6 FUSELAGE ARMOR and 7/8 L/R WING
	/// ARMOR. The wings and nacelles are the four the flight model answers to; the cockpit and
	/// fuselage are the two whose loss ends the flight.
	///
	/// <para>Component 4 being the <i>left</i> nacelle is also what settles the frame's handedness:
	/// its probe point sits at negative X, so -X is port and +X starboard.</para>
	/// </summary>
	private const int ComponentCockpit = 0;

	/// <inheritdoc cref="ComponentCockpit"/>
	private const int ComponentLeftNacelle = 4;

	/// <inheritdoc cref="ComponentCockpit"/>
	private const int ComponentRightNacelle = 5;

	/// <inheritdoc cref="ComponentCockpit"/>
	private const int ComponentFuselage = 6;

	/// <inheritdoc cref="ComponentCockpit"/>
	private const int ComponentLeftWing = 7;

	/// <inheritdoc cref="ComponentCockpit"/>
	private const int ComponentRightWing = 8;

	/// <summary>
	/// This chassis' flight parameters, or null when it does not fly. Non-null is what puts the
	/// machine on the flight path in <see cref="Tick"/>. A RAZOR built without its <c>.FM</c> is left
	/// on the walker paths, which is wrong but is at least not a crash.
	/// </summary>
	public FlightModelRecord? Flight { get; }

	/// <summary>
	/// Velocity in the airframe's own frame, world units per unit time — <c>mech+0x2b9</c>. X is
	/// sideslip, <b>Y is airspeed</b>, Z is the vertical component. The flight model works entirely
	/// in this frame and derives <see cref="FlightWorldVelocity"/> from it; only the move itself uses
	/// the world one.
	///
	/// <para>A flyer having a velocity vector at all is what most sets it apart from a HERC, which
	/// has only a speed scalar the walk animation consumes — see the type summary.</para>
	/// </summary>
	public Vec3i FlightVelocity { get; private set; }

	/// <summary>The same velocity in world space — <c>mech+0x2c5</c>, and what the move integrates.</summary>
	public Vec3i FlightWorldVelocity { get; private set; }

	/// <summary>
	/// Airspeed — <c>mech+0x2bd</c>, and what <c>Mech_GetSpeed</c> (<c>00415498</c>) returns for a
	/// flyer where it returns a scaled <see cref="Speed"/> for a walker. <see cref="Speed"/> itself
	/// is never written on this path.
	/// </summary>
	public int AirSpeed => FlightVelocity.Y;

	/// <summary>Pitch rate, <c>mech+0x2d1</c>, in binary angle per unit time.</summary>
	public short PitchRate { get; private set; }

	/// <summary>Roll rate, <c>mech+0x2d3</c>.</summary>
	public short RollRate { get; private set; }

	/// <summary>
	/// Yaw rate, <c>mech+0x2d5</c> — what the rudder builds. Distinct from
	/// <see cref="BankTurnRate"/>, which is not a rate the airframe carries.
	/// </summary>
	public short YawRate { get; private set; }

	/// <summary>
	/// <c>mech+0x2fd</c> — the heading rate the current bank angle is producing. <b>This is the
	/// aircraft's turning.</b> The rudder yaws the airframe about its own axis, but what swings the
	/// nose round the sky is the bank: roll the wings and the heading follows, at a rate the flight
	/// model reads straight off the bank angle. A RAZOR pilot turns by rolling, not by steering.
	/// </summary>
	public int BankTurnRate { get; private set; }

	/// <summary>
	/// <c>mech+0x2d7</c> — the flyer's own throttle setting, a second copy of <see cref="Throttle"/>
	/// rather than the same field. The flight model owns this one, and the control tick pushes it
	/// onto <see cref="Throttle"/> whenever the pilot moved the axis, so the cockpit gauge follows
	/// the flight model rather than driving it.
	/// </summary>
	public short FlightThrottle { get; private set; }

	// mech+0x2dd and mech+0x2f1 — last tick's transform, inverted. The flight model needs it twice:
	// to bring the world-space drag back into the body frame, and to re-express world velocity in
	// the new body frame after the airframe has rotated.
	private Transform3 _previousInverse;
	private bool _previousInverseValid;

	/// <summary>
	/// One tick of the flight path, replacing the walker's throttle law, turret tick and move
	/// together. <c>Sim_PollPlayerInput</c> (<c>00460764</c>) branches on the type record's flyer
	/// flag and calls <c>Razor_ApplyFlightInput</c> (<c>0041bb9c</c>) in place of
	/// <c>Mech_ApplyThrottleInput</c> and the two turret ticks, while the object list dispatches
	/// <c>Razor_MovementTick</c> (<c>004198f4</c>) in place of
	/// <c>Mech_MovementTick</c> — a whole distinct behaviour class, picked in
	/// <c>Mech_Constructor</c> (<c>00415bb0</c>) by (is this the player, is this a flyer).
	///
	/// <para><b>The turret does not move.</b> Neither turret tick is on this path at all, so a
	/// RAZOR's guns point where its nose points and aiming means flying.</para>
	///
	/// <para><b>Nor is there any of the walker's collision handling.</b> No swept body test, no
	/// terrain clamp, no back-off — see <see cref="FlyerMovementTick"/> for what stands in.</para>
	/// </summary>
	private void FlightTick(SimWorld world, FlightModelRecord flight) {
		FlightControlTick(world, flight);
		FlyerMovementTick(world, flight);
	}

	/// <summary>
	/// <c>Razor_ApplyFlightInput</c> (<c>0041bb9c</c>) — the flyer's input hand-off. It gathers this
	/// tick's four stick axes, the
	/// ground height under the aircraft and the four wing-damage flags, runs the flight model, and
	/// hands the throttle it settled on back to the cockpit gauge.
	///
	/// <para><b>The axes are remapped.</b> The device layer hands the same four axes to both paths,
	/// but a flyer reads them as an aircraft's controls rather than a walker's:</para>
	/// <list type="bullet">
	/// <item><see cref="MechControls.Turn"/> — stick X — is the <b>aileron</b>.</item>
	/// <item><see cref="MechControls.Throttle"/> — stick Y — is the <b>elevator</b>. On a walker this
	/// axis is the throttle; on an aircraft the primary stick axes have to be pitch and roll, so the
	/// throttle moves elsewhere.</item>
	/// <item><see cref="MechControls.TorsoTwist"/> is the <b>rudder</b>.</item>
	/// <item><see cref="MechControls.TorsoPitch"/> is the <b>throttle</b> — the axis a walker pitches
	/// its turret with, which a flyer has no use for.</item>
	/// </list>
	/// </summary>
	private void FlightControlTick(SimWorld world, FlightModelRecord flight) {
		var controls = Controls;

		FlightModelStep(flight,
			aileron: controls.Turn,
			elevator: controls.Throttle,
			rudder: controls.TorsoTwist,
			throttleAxis: controls.TorsoPitch,
			groundHeight: world.GroundHeightAt(Position));

		// Only a tick the pilot actually moved the throttle axis on pushes the setting onto the
		// machine's throttle field, so a gauge being dragged is not immediately overwritten. It is
		// the same exchange the walker's gauge makes, the other way round — see
		// ExchangeCockpitThrottle.
		if (controls.TorsoPitch != 0) {
			Throttle = FlightThrottle;
			ThrottleDirty = true;
		}
	}

	/// <summary>
	/// <c>FlightModel_Step</c> (<c>00466a54</c>) — the flight model. It settles, in this order: the
	/// throttle setting and
	/// the airspeed it asks for, the sideslip drag, the three angular rates, the new attitude, and
	/// finally the velocity vector in that new attitude.
	///
	/// <para><b>Nothing here moves the aircraft</b>, exactly as nothing in the walker's control law
	/// moves a HERC. What it produces is <see cref="FlightWorldVelocity"/>, which
	/// <see cref="FlyerMovementTick"/> integrates.</para>
	/// </summary>
	private void FlightModelStep(FlightModelRecord flight, short aileron, short elevator,
			short rudder, short throttleAxis, int groundHeight) {
		var fm = flight.Data;

		if (!_previousInverseValid) {
			// The original builds this in the constructor. Here it is seeded on the first tick
			// instead, because a mech is positioned and headed after it is constructed and the
			// constructor's copy would be of an attitude the machine never actually had.
			_previousInverse = Rotation().Inverted();
			_previousInverseValid = true;
		}

		// --- Throttle ---------------------------------------------------------------------------
		if (Controls.ThrottleLever != 0) {
			// An analogue throttle is a position, not a rate. Half the axis' travel covers the whole
			// range, and unlike the walker's lever there is no inverted sense and no clamp to one
			// side of zero — a flyer's throttle spans the same signed range either way. The original
			// gates this on an input-preferences byte rather than on the walker's lever global; the
			// host signal is the same one either way.
			FlightThrottle = ClampThrottle(throttleAxis << 3);
		} else {
			short rate = (short)SimMath.Q8Multiply(FlightThrottleRate, throttleAxis);
			if (rate != 0) {
				FlightThrottle =
					ClampThrottle(FlightThrottle + SimMath.IntegrateRateOverTick(rate));
			}
		}

		// --- Airspeed ---------------------------------------------------------------------------
		int speedRange = fm.AirSpeedMax - fm.AirSpeedMin;
		int demand = SimMath.Q10Multiply(speedRange, (FlightThrottle + FlightThrottleFull) >> 1)
			+ fm.AirSpeedMin;
		demand -= SimMath.Q10Multiply(Pitch < 0 ? DiveSpeedGain : ClimbSpeedGain, Pitch);

		int airSpeed = FlightVelocity.Y;
		MoveToward(ref airSpeed, demand, SimMath.IntegrateRateOverTick(fm.ThrustResponse));

		// --- Sideslip drag ----------------------------------------------------------------------
		// The sideways and vertical components of body velocity are taken into world space, scaled
		// down, brought back through *last* tick's frame and subtracted. Forward is excluded, which
		// is what makes this drag rather than braking, and it is why a RAZOR flies where it is
		// pointing instead of sliding round its own turns.
		var frame = Rotation();
		int lateral = FlightVelocity.X;
		int vertical = FlightVelocity.Z;

		var slip = frame.RotateVector(lateral, 0, vertical);

		// Only the two ground-plane components are scaled by the coefficient; the world-vertical one
		// is subtracted whole, at an effective coefficient of 1. The asymmetry is the original's and
		// is spelled out in its own instructions (00466c26-00466c67 scales two of the three), and it
		// is load-bearing: it is why a RAZOR sheds vertical speed far harder than sideslip, and so
		// why it settles onto its flight path rather than floating.
		var drag = _previousInverse.RotateVector(
			SimMath.Q10Multiply(fm.LateralDrag, slip.X),
			SimMath.Q10Multiply(fm.LateralDrag, slip.Y),
			slip.Z);

		lateral -= SimMath.IntegrateRateOverTick((short)drag.X);
		airSpeed -= SimMath.IntegrateRateOverTick((short)drag.Y);
		vertical -= SimMath.IntegrateRateOverTick((short)drag.Z);

		FlightVelocity = new Vec3i(lateral, airSpeed, vertical);
		FlightWorldVelocity = frame.RotateVector(lateral, airSpeed, vertical);

		// --- Angular commands -------------------------------------------------------------------
		short previousPitchRate = PitchRate;
		short previousRollRate = RollRate;
		short previousYawRate = YawRate;

		bool leftWingGone = !AirframeIntact(ComponentLeftWing);
		bool rightWingGone = !AirframeIntact(ComponentRightWing);
		bool leftNacelleGone = !AirframeIntact(ComponentLeftNacelle);
		bool rightNacelleGone = !AirframeIntact(ComponentRightNacelle);

		short roll = Roll;
		int ceiling = flight.Ceiling(airSpeed);

		if (rightNacelleGone || leftNacelleGone) {
			// A nacelle gone takes the elevator with it and jams it nose-down, resolved through
			// the bank so that "down" stays down however the aircraft is lying.
			elevator = (short)(-(int)SimTrig.Cos(roll) >> 6);
		}

		int pitchCommand;
		int pitchDamping = 0;
		if (elevator == 0) {
			// Pitch self-levelling, which on retail data is switched off: both flight models state a
			// shift of 16, and a 16-bit angle shifted 16 is nothing. An aircraft holds the attitude
			// it was trimmed to and bleeds its pitch rate off through the damping term instead.
			pitchCommand = -(int)Pitch >> (fm.PitchLevelShift & 0x1f);
			pitchDamping = -SimMath.Q10Multiply(fm.AngularDamping, PitchRate);
		} else {
			pitchCommand = SimMath.Q8Multiply(fm.MaxPitchRate, elevator);
		}

		int yawCommand = SimMath.Q8Multiply(fm.MaxYawRate, -rudder);

		int altitudeAboveGround = Position.Z - groundHeight;
		if (ceiling < altitudeAboveGround) {
			// Over the ceiling. The push is a vector in the aircraft's own frame pointing at the
			// ground — cosine of the bank onto pitch, its quarter-turn shift onto yaw — so a RAZOR
			// held over the ceiling inverted is pushed the way that actually takes it down. Note it
			// only ever *lowers* the pitch command: it can refuse a climb but never force one.
			short push =
				(short)-SimMath.Q10Multiply(CeilingPushGain, altitudeAboveGround - ceiling);

			yawCommand = (short)SimMath.Q14Multiply(
				SimTrig.Cos((short)(roll - BinaryAngle.QuarterTurn)), push);

			int pitchPush = (short)SimMath.Q14Multiply(SimTrig.Cos(roll), push);
			if (pitchPush < pitchCommand) {
				pitchCommand = pitchPush;
			}
		} else if (elevator == 0) {
			// The original recomputes here the damping it already has; transcribed rather than
			// folded away so the two branches stay comparable with the disassembly.
			pitchDamping = -SimMath.Q10Multiply(fm.AngularDamping, PitchRate);
		}

		// --- Aileron, and what a lost wing does to it ---------------------------------------------
		int aileronCommand = aileron;
		if (rightNacelleGone) {
			aileronCommand = MechControls.AxisFull;
		} else if (leftNacelleGone) {
			aileronCommand = -MechControls.AxisFull;
		} else if (rightWingGone) {
			// A lost wing is survivable where a lost nacelle is not: rather than pinning the
			// stick it adds a small bias that settles the aircraft at a permanent 22.5° lean, which
			// the pilot can hold off but has to keep holding off.
			if (roll < LostWingBank) {
				aileronCommand += (short)SimMath.Q14Multiply(
					LostWingGain, (short)(LostWingBank - roll));
			}
		} else if (leftWingGone) {
			if (roll > -LostWingBank) {
				aileronCommand -= (short)SimMath.Q14Multiply(
					LostWingGain, (short)(roll + LostWingBank));
			}
		}

		int bankMagnitude = roll == short.MinValue ? short.MaxValue : System.Math.Abs((int)roll);

		// Past a quarter turn of bank the sense inverts, measured from the half turn instead — which
		// is what lets an inverted RAZOR turn the way its wings say rather than backwards.
		BankTurnRate = bankMagnitude < BinaryAngle.QuarterTurn
			? -(int)roll >> (fm.BankTurnShift & 0x1f)
			: (short)(roll - short.MinValue) >> (fm.BankTurnShift & 0x1f);

		int rollCommand;
		int rollDamping = 0;
		if (aileronCommand == 0) {
			rollCommand = -(int)roll >> (fm.RollLevelShift & 0x1f);
			rollDamping = -SimMath.Q10Multiply(fm.AngularDamping, RollRate);

			short settled = (short)(RollRate + rollDamping);
			int settledMagnitude =
				settled == short.MinValue ? short.MaxValue : System.Math.Abs((int)settled);

			if (bankMagnitude < settledMagnitude) {
				// The wings would cross level this tick. Stop them exactly there rather than let the
				// self-levelling term carry them past and set up a wallow.
				rollCommand = 0;
				rollDamping = -bankMagnitude - RollRate;
			}
		} else {
			rollCommand = SimMath.Q8Multiply(fm.MaxRollRate, aileronCommand);

			// Damping only when the stick is fighting the roll already under way, so reversing a
			// roll is crisp while holding one costs nothing.
			if ((aileronCommand > 0 && RollRate < 0) || (aileronCommand < 0 && RollRate > 0)) {
				rollDamping = -SimMath.Q10Multiply(fm.AngularDamping, RollRate);
			}
		}

		// --- Rates --------------------------------------------------------------------------------
		// Each axis' command is clamped to a maximum acceleration, the damping goes on outside that
		// clamp, and the resulting rate is clamped to a maximum rate. Yaw takes the roll axis' own
		// acceleration limit; the flight model has only the two.
		short pitchAccel =
			(short)(pitchDamping + ClampSymmetric((short)pitchCommand, fm.MaxPitchAccel));
		short rollAccel = (short)(rollDamping + ClampSymmetric((short)rollCommand, fm.MaxRollAccel));
		short yawAccel = (short)(ClampSymmetric((short)yawCommand, fm.MaxRollAccel)
			- SimMath.Q10Multiply(fm.AngularDamping, YawRate));

		PitchRate = ClampSymmetric(
			(short)(PitchRate + SimMath.IntegrateRateOverTick(pitchAccel)), fm.MaxPitchRate);
		RollRate = ClampSymmetric(
			(short)(RollRate + SimMath.IntegrateRateOverTick(rollAccel)), fm.MaxRollRate);
		YawRate = ClampSymmetric(
			(short)(YawRate + SimMath.IntegrateRateOverTick(yawAccel)), fm.MaxYawRate);

		// --- Attitude -----------------------------------------------------------------------------
		// The rotation is integrated as a *matrix*, from the mean of this tick's rates and last
		// tick's, and the euler triple is read back out of the result. That is what keeps a RAZOR
		// flyable through a vertical climb, where integrating the three angles directly would
		// gimbal — and it is the one place in the simulation that composes a rotation this way.
		var step = Transform3.FromEuler(
			(short)SimMath.IntegrateRateOverTick(Mean(previousPitchRate, PitchRate)),
			(short)SimMath.IntegrateRateOverTick(Mean(previousRollRate, RollRate)),
			(short)SimMath.IntegrateRateOverTick(Mean(previousYawRate, YawRate)));

		var (pitch, rolled, heading) = Transform3.Concat(step, frame).ToEuler();
		Pitch = pitch;
		Roll = rolled;

		// The bank-driven turn goes on top of the integrated attitude rather than through it, which
		// is why a banked RAZOR turns about the world's vertical axis and not about its own.
		Heading = (heading + SimMath.IntegrateRateOverTick((short)BankTurnRate)) & 0xffff;
		_rotationValid = false;

		var settledFrame = Rotation();
		_previousInverse = settledFrame.Inverted();

		// --- Velocity in the new frame -------------------------------------------------------------
		short before = (short)FlightVelocity.Y;
		var body = _previousInverse.RotateVector(
			FlightWorldVelocity.X, FlightWorldVelocity.Y, FlightWorldVelocity.Z);
		FlightVelocity = body;

		if (body.Y < before) {
			// Rotating velocity into the new attitude costs forward speed. Most of it is handed
			// straight back, so a hard turn scrubs a little energy rather than stalling the aircraft.
			FlightVelocity = new Vec3i(body.X,
				body.Y + SimMath.Q10Multiply(TurnSpeedRecovery, before - body.Y), body.Z);
			FlightWorldVelocity = settledFrame.RotateVector(
				FlightVelocity.X, FlightVelocity.Y, FlightVelocity.Z);
		}
	}

	/// <summary>
	/// <c>Razor_MovementTick</c> (<c>004198f4</c>) — the flyer's move, in place of
	/// <c>Mech_MovementTick</c>. It integrates
	/// the velocity the flight model settled on, then runs six contact probes over the airframe.
	///
	/// <para><b>The probes are the collision model.</b> There is no swept body test and no terrain
	/// clamp on the airframe as a whole. Six points — the two wings, the two nacelles, the cockpit
	/// and the fuselage — are each checked against the ground beneath them and, bar the fuselage,
	/// swept forward as a ray one tick's travel long. A contact damages that component, kicks the
	/// airframe away from whatever it touched, and scales the damage by how fast it was going. So a
	/// RAZOR that clips a ridge with a wingtip is rolled off it and loses the wing rather than
	/// stopping dead.</para>
	///
	/// <para><b>Two of them end the flight.</b> A cockpit or fuselage contact that destroys its component
	/// latches <see cref="Immobilised"/>, and with that set the aircraft stops integrating position
	/// altogether — it is down, wherever it fell.</para>
	///
	/// <para>Not ported: the debris effect the original spawns at the crash point, out of the
	/// secondary effect pool nothing in the engine reaches yet (see
	/// <see cref="BaseObject.DirectFireHitTest"/>), and the gun-convergence pass that closes the
	/// function, which is weapon aiming and has no counterpart here.</para>
	/// </summary>
	private void FlyerMovementTick(SimWorld world, FlightModelRecord flight) {
		var frame = Rotation();

		if (!Immobilised) {
			var velocity = FlightWorldVelocity;
			Position = new Vec3i(
				Position.X + SimMath.IntegrateRateOverTick((short)velocity.X),
				Position.Y + SimMath.IntegrateRateOverTick((short)velocity.Y),
				Position.Z + SimMath.IntegrateRateOverTick((short)velocity.Z));
			frame = Rotation();
		}

		int airSpeed = FlightVelocity.Y;

		// The wing pair. Contact rolls the airframe away from what it touched, hard enough that a
		// wing dragged along a hillside flips the aircraft off it.
		WingProbe(world, ProbeRightWing, ComponentRightWing, frame, airSpeed, rollAway: -1);
		WingProbe(world, ProbeLeftWing, ComponentLeftWing, frame, airSpeed, rollAway: 1);

		// The nacelles, which have no terrain check at all — only the object ray, at half the wings'
		// clearance. They sit inboard and low, where the ground is already the wings' and the
		// fuselage's business.
		NacelleProbe(world, ProbeLeftNacelle, ComponentLeftNacelle, frame, airSpeed, rollAway: 1);
		NacelleProbe(world, ProbeRightNacelle, ComponentRightNacelle, frame, airSpeed, rollAway: -1);

		CockpitProbe(world, frame, airSpeed);
		GroundAvoidance(world, Rotation());
		FuselageContact(world, airSpeed);

		UpdateEngineNote(world);
	}

	// The six probe points, in the airframe's own frame and in world units. Model forward is +Y and
	// +Z is up, so the wingtips sit six metres out either side, slightly aft and slightly low; the
	// cockpit point is ahead; and the look-ahead point is far ahead and well below.
	private static readonly Vec3i ProbeRightWing = new(1000, -700, -100);
	private static readonly Vec3i ProbeLeftWing = new(-1000, -700, -100);
	private static readonly Vec3i ProbeRightNacelle = new(450, -500, 0);
	private static readonly Vec3i ProbeLeftNacelle = new(-450, -500, 0);
	private static readonly Vec3i ProbeCockpit = new(0, 1000, 0);
	private static readonly Vec3i ProbeLookAhead = new(0, 15000, -1500);

	/// <summary>How much slack a probe ray allows past its own length, per probe point.</summary>
	private const int WingClearance = 300;

	/// <inheritdoc cref="WingClearance"/>
	private const int NacelleClearance = 150;

	/// <inheritdoc cref="WingClearance"/>
	private const int CockpitClearance = 200;

	/// <summary>The shield figure every airframe contact carries. It never varies with speed.</summary>
	private const short ContactDamageShield = 8000;

	/// <summary>What striking an <i>object</i> costs a wing, where a ground contact is speed-scaled.</summary>
	private const short WingObjectDamage = 1000;

	/// <summary>The same for the nacelles and the cockpit, which take a solid hit rather than a scrape.</summary>
	private const short HeavyObjectDamage = 5000;

	/// <summary>Q10 gain from airspeed to the damage a ground contact does, per probe point.</summary>
	private const int WingGroundDamageGain = 500;

	/// <inheritdoc cref="WingGroundDamageGain"/>
	private const int CockpitGroundDamageGain = 1000;

	/// <inheritdoc cref="WingGroundDamageGain"/>
	private const int FuselageGroundDamageGain = 5000;

	/// <summary>Q10 gain from how deep a wing is in the ground to the roll rate it is kicked with.</summary>
	private const int WingGroundRollGain = 4000;

	/// <summary>The flat roll kick an object contact gives, a ground depth not being available.</summary>
	private const short WingObjectRollKick = 4000;

	/// <inheritdoc cref="WingObjectRollKick"/>
	private const short NacelleRollKick = 8000;

	/// <summary>Q10 gain from cockpit-probe depth to the pitch rate it is kicked with.</summary>
	private const int CockpitGroundPitchGain = 2000;

	/// <inheritdoc cref="CockpitGroundPitchGain"/>
	private const short CockpitObjectPitchKick = 2000;

	/// <summary>
	/// Q10 gain for the look-ahead pull-up — a hundredth of the cockpit probe's, because it is a
	/// warning rather than a collision.
	/// </summary>
	private const int LookAheadPitchGain = 0x14;

	/// <summary>
	/// One wing. The probe point is tested against the ground under it <i>and</i> swept forward as a
	/// ray, so a wing catches a building as readily as a hillside.
	/// </summary>
	/// <param name="rollAway">Which way a contact rolls the airframe: -1 for a surface out to starboard.</param>
	private void WingProbe(SimWorld world, Vec3i offset, int component, in Transform3 frame,
			int airSpeed, int rollAway) {
		if (!AirframeIntact(component)) {
			return;
		}

		var point = frame.TransformPoint(offset.X, offset.Y, offset.Z);
		int ground = world.Terrain.HeightAtWorld(point.X, point.Y);
		bool inGround = point.Z < ground;

		if (!inGround && !ProbeStruckObject(world, frame, point, airSpeed, WingClearance)) {
			return;
		}

		short damage;
		if (inGround) {
			RollRate = (short)(RollRate
				+ rollAway * SimMath.Q10Multiply(WingGroundRollGain, ground - point.Z));
			damage = (short)SimMath.Q10Multiply(airSpeed, WingGroundDamageGain);
		} else {
			RollRate = (short)(RollRate + rollAway * WingObjectRollKick);
			damage = WingObjectDamage;
		}

		// The kick lands on the attitude this tick, not through the flight model's integrator — the
		// airframe is pushed, and the rate it was pushed with is left standing for the flight model
		// to damp out over the ticks that follow.
		Roll = (short)(Roll + RollRate);
		_rotationValid = false;

		ApplyContactDamage(world, (short)component, damage,
			inGround ? new Vec3i(point.X, point.Y, ground) : point);
	}

	/// <summary>
	/// One nacelle. No ground test — only the object ray — and a flat roll kick twice the wings'.
	/// </summary>
	private void NacelleProbe(SimWorld world, Vec3i offset, int component, in Transform3 frame,
			int airSpeed, int rollAway) {
		if (!AirframeIntact(component)) {
			return;
		}

		var point = frame.TransformPoint(offset.X, offset.Y, offset.Z);
		if (!ProbeStruckObject(world, frame, point, airSpeed, NacelleClearance)) {
			return;
		}

		RollRate = (short)(RollRate + rollAway * NacelleRollKick);
		Roll = (short)(Roll + RollRate);
		_rotationValid = false;

		// The original reports both nacelle contacts at the *left* probe's point, whichever nacelle
		// was struck — its right-hand branch passes the left point's address. Reproduced: it decides
		// only where the impact effect is drawn, and correcting it would move an effect the retail
		// game draws in a fixed place.
		var reported = frame.TransformPoint(ProbeLeftNacelle.X, ProbeLeftNacelle.Y, ProbeLeftNacelle.Z);
		ApplyContactDamage(world, (short)component, HeavyObjectDamage, reported);
	}

	/// <summary>
	/// The cockpit. It pitches the aircraft <i>up</i> out of whatever it hit, and losing the cockpit
	/// section outright ends the flight.
	/// </summary>
	private void CockpitProbe(SimWorld world, in Transform3 frame, int airSpeed) {
		if (!AirframeIntact(ComponentCockpit)) {
			return;
		}

		var point = frame.TransformPoint(ProbeCockpit.X, ProbeCockpit.Y, ProbeCockpit.Z);
		int ground = world.Terrain.HeightAtWorld(point.X, point.Y);
		bool inGround = point.Z < ground;

		if (!inGround && !ProbeStruckObject(world, frame, point, airSpeed, CockpitClearance)) {
			return;
		}

		// A nose already dropping is zeroed first, so the kick is a pull-up from rest rather
		// than a correction applied to a dive steep enough to swallow it.
		if (PitchRate < 0) {
			PitchRate = 0;
		}

		short damage;
		if (inGround) {
			PitchRate =
				(short)(PitchRate + SimMath.Q10Multiply(CockpitGroundPitchGain, ground - point.Z));
			damage = (short)SimMath.Q10Multiply(airSpeed, CockpitGroundDamageGain);
		} else {
			PitchRate = (short)(PitchRate + CockpitObjectPitchKick);
			damage = HeavyObjectDamage;
		}

		Pitch = (short)(Pitch + PitchRate);
		_rotationValid = false;

		ApplyContactDamage(world, ComponentCockpit, damage,
			inGround ? new Vec3i(point.X, point.Y, ground) : point);

		if (!AirframeIntact(ComponentCockpit)) {
			Immobilised = true;
		}
	}

	/// <summary>
	/// The terrain look-ahead — a single point 15000 units ahead and 1500 below, which pulls the nose
	/// up when the ground rises into it. It is the closest thing the RAZOR has to a stall recovery,
	/// and it is why the aircraft skims a hillside rather than burying itself in it.
	///
	/// <para><b>It only runs on an intact airframe</b>: both nacelles and the cockpit have to be
	/// alive. A RAZOR that has lost any of the three flies straight into the hill.</para>
	/// </summary>
	private void GroundAvoidance(SimWorld world, in Transform3 frame) {
		if (!AirframeIntact(ComponentLeftNacelle) || !AirframeIntact(ComponentRightNacelle)
				|| !AirframeIntact(ComponentCockpit)) {
			return;
		}

		var point = frame.TransformPoint(ProbeLookAhead.X, ProbeLookAhead.Y, ProbeLookAhead.Z);
		int ground = world.Terrain.HeightAtWorld(point.X, point.Y);
		if (ground <= point.Z) {
			return;
		}

		if (PitchRate < 0) {
			PitchRate = 0;
		}

		PitchRate = (short)(PitchRate + SimMath.Q10Multiply(LookAheadPitchGain, ground - point.Z));
		Pitch = (short)(Pitch + PitchRate);
		_rotationValid = false;
	}

	/// <summary>
	/// The fuselage, and the only probe that moves the aircraft: below the ground, it is put back on
	/// it.
	/// The damage scales with airspeed, so setting a RAZOR down slowly is survivable and arriving at
	/// speed is not.
	/// </summary>
	private void FuselageContact(SimWorld world, int airSpeed) {
		int ground = world.Terrain.HeightAtWorld(Position.X, Position.Y);
		if (Position.Z >= ground) {
			return;
		}

		Position = new Vec3i(Position.X, Position.Y, ground);
		_rotationValid = false;

		ApplyContactDamage(world, ComponentFuselage,
			(short)SimMath.Q10Multiply(airSpeed, FuselageGroundDamageGain), Position);

		if (!AirframeIntact(ComponentFuselage)) {
			Immobilised = true;
		}
	}

	/// <summary>
	/// Whether the ray from one probe point struck anything. The ray is one tick's travel long,
	/// starts at the probe point and carries the airframe's own attitude — the original swaps each
	/// point in turn into the translation of a single shared copy of the aircraft's transform.
	/// </summary>
	private bool ProbeStruckObject(SimWorld world, in Transform3 frame, Vec3i point, int airSpeed,
			int clearance) {
		var ray = frame;
		ray.X = point.X;
		ray.Y = point.Y;
		ray.Z = point.Z;

		var probe = new WeaponShot(ray, airSpeed, ContactDamageShield, ContactDamageShield,
			AirframeContact, owner: null, excluded: this, clearance: clearance);

		return world.Raycast(probe) != 0;
	}

	/// <summary>
	/// Puts one contact through the ordinary damage path. The armour figure varies per contact; the
	/// shield figure never does, and <b>the shot has no attacker</b> — the original leaves the
	/// record's attacker field null, so flying into a hillside is nobody's kill.
	/// </summary>
	private void ApplyContactDamage(SimWorld world, short component, short damage, Vec3i point) {
		var contact = new WeaponShot(Rotation(), 0, damage, ContactDamageShield, AirframeContact,
			owner: null, excluded: this);

		ApplyDirectFireDamage(world, component, contact, point);
	}

	/// <summary>
	/// The impact-effect table airframe contacts draw from — a hand-built <c>PROJ.DAT</c>-shaped
	/// record in the executable's statics at <c>0049a158</c> rather than a real projectile's, so a
	/// wing clipping a ridge throws up its own effects rather than some weapon's.
	/// </summary>
	private static readonly ProjectileData.Projectile AirframeContact = new() {
		DamageArmor = ContactDamageShield,
		DamageShield = ContactDamageShield,
		SplashFactor = 0,
		ImpactFXShield = new short[] { 11, 11, 11, 11 },
		ImpactFXGround = new short[] { 0, 1, 4, 5 },
		ImpactFXArmor = new short[] { 0, 1, 4, 5 },
	};

	/// <summary>
	/// The engine note — <c>Sound_SetPitch</c> on the looping hum, at a rate that climbs with the
	/// magnitude of the whole velocity vector rather than with airspeed alone, so a RAZOR falling out
	/// of the sky screams as loudly as one running flat out. Only the player's machine has a hum
	/// (<see cref="Audio.GameAudio.PowerUp"/>), and a destroyed one is silenced.
	/// </summary>
	private void UpdateEngineNote(SimWorld world) {
		if (!IsPlayer || world.Sounds is not { } sounds) {
			return;
		}

		if (Destroyed) {
			sounds.Stop(SoundId.EngineLoop);
			return;
		}

		var v = FlightVelocity;
		int rate = SimMath.FastMagnitude3D(v.X, v.Y, v.Z) * EngineNoteSpeedGain
			+ EngineNoteIdlePitch;
		rate = rate > 0xfffe ? 0xffff : rate < 1 ? 0 : rate;

		sounds.SetPitch(SoundId.EngineLoop, rate);
		sounds.MoveTo(SoundId.EngineLoop, Position);
	}

	/// <summary>
	/// The playback rate the hum sits at with the aircraft stationary, 16.16 — a shade under
	/// <see cref="SoundId.EngineLoopPitch"/>, the rate the cockpit power-up drops it to.
	/// </summary>
	private const int EngineNoteIdlePitch = 28000;

	/// <summary>How much each unit of speed raises that rate.</summary>
	private const int EngineNoteSpeedGain = 16;

	/// <summary>
	/// Whether one airframe component is still there. A machine with no <c>.DMG</c> loaded counts as
	/// whole rather than as wrecked, which matters here where <see cref="ComponentAlive"/>'s "no,
	/// because there is no damage model" would jam the controls hard over.
	/// </summary>
	private bool AirframeIntact(int component) => _damage == null || _damage.IsActive(component);

	private static short ClampThrottle(int value) =>
		value >= FlightThrottleFull ? FlightThrottleFull
		: value <= -FlightThrottleFull ? (short)-FlightThrottleFull
		: (short)value;

	private static short ClampSymmetric(short value, short limit) {
		short low = (short)-limit;
		return value >= limit ? limit : value <= low ? low : value;
	}

	private static short Mean(short a, short b) => (short)((a + b) >> 1);

	/// <summary>
	/// <c>Math_RateLimitedMoveTowardInt</c> (<c>00467a24</c>) —
	/// <see cref="SimMath.RateLimitedMoveToward"/> on a 32-bit value. Airspeed
	/// is an int where every rate the walker slews is a short, so the original has both.
	/// </summary>
	private static void MoveToward(ref int current, int target, int step) {
		if (target < current) {
			current -= step;
			if (current < target) {
				current = target;
			}
		} else if (current < target) {
			current += step;
			if (target < current) {
				current = target;
			}
		}
	}
}
