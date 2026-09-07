using Herculan.Engine.Numerics;
using Herculan.Engine.Sim.Anim;

namespace Herculan.Engine.Sim;

/// <summary>
/// The torso — the manual's "turret": the part of a HERC that carries the pilot and the weapons and
/// aims independently of the legs.
///
/// <para>It has no rotation of its own. The type record names a sequence for each axis, each one a
/// single full sweep of one node, and the angle is used as a <i>position</i> within that sequence
/// (<see cref="AnimationThread.SeekToPosition"/>) rather than as an angle anything rotates by. Twist
/// and pitch are therefore the same kind of thing as the walk cycle, and reach the screen the same
/// way — see docs/formats/dts-node-posing.md.</para>
/// </summary>
public sealed partial class MechObject {
	/// <summary>
	/// Twist rate, <c>mech+0x294</c>, in binary angle per second. Built up toward what the stick asks
	/// for, then integrated into <see cref="TorsoTwistAngle"/>.
	/// </summary>
	public short TorsoTwistRate { get; private set; }

	/// <summary>
	/// Twist angle, <c>mech+0x298</c>, a binary angle relative to the machine's own heading. Positive
	/// is the direction <see cref="MechControls.TorsoTwist"/> positive drives it.
	/// </summary>
	public short TorsoTwistAngle { get; private set; }

	/// <summary>Pitch rate, <c>mech+0x296</c>.</summary>
	public short TorsoPitchRate { get; private set; }

	/// <summary>Pitch angle, <c>mech+0x29a</c>. Positive looks up.</summary>
	public short TorsoPitchAngle { get; private set; }

	/// <summary>
	/// <c>Mech_TorsoTwistTick</c> (<c>0041a550</c>) — one tick of the twist axis.
	///
	/// <para>The rate ramps toward <c>Q8(axis, TorsoTwistMaxRate)</c> at the type's own acceleration,
	/// but <b>only while it is growing</b>: the moment the stick asks for less than the torso is
	/// already doing, the rate snaps to it. Releasing the stick therefore stops the torso dead, and
	/// so does reversing it. The angle then integrates the mean of the rate before and after, which
	/// is a trapezoid rule while ramping and a plain step while not, and clamps to ±the type's
	/// limit.</para>
	///
	/// <para><paramref name="snapTarget"/> is only consulted when <paramref name="snapEnable"/> is
	/// set, which normal piloting does not: the input path passes it disabled. It stops the torso
	/// dead on the tick it crosses the target angle, and is how
	/// <see cref="CenterTorsoTick"/> lands exactly on centre instead of oscillating about it.</para>
	/// </summary>
	public void TorsoTwistTick(short axis, short snapTarget = -1, bool snapEnable = false) {
		short previousAngle = TorsoTwistAngle;
		short rate = TorsoTwistRate;
		short angle = TorsoTwistAngle;

		StepTorsoAxis(ref rate, ref angle, axis, Type.TorsoTwistMaxRate, Type.TorsoTwistAccel,
			(short)-Type.TorsoTwistLimit, Type.TorsoTwistLimit);

		TorsoTwistRate = rate;
		TorsoTwistAngle = angle;

		if (snapEnable && CrossedTarget(previousAngle, TorsoTwistAngle, snapTarget)) {
			TorsoTwistAngle = snapTarget;
			TorsoTwistRate = 0;
		}

		TorsoTwistThread?.SeekToPosition(Type.TorsoTwistSequence, SequencePosition(TorsoTwistAngle));
	}

	/// <summary>
	/// <c>Mech_TorsoPitchTick</c> (<c>0041a808</c>) — the same, on the pitch axis. The only
	/// differences are the fields it works on and that its limits are asymmetric: a HERC looks
	/// further up than down.
	///
	/// <para>Its third argument is the range the guns are to converge on — the distance to whatever
	/// the machine is aiming at, and zero for nothing. That is the original's own placement: the
	/// convergence pass is this tick's tail. See
	/// <see cref="WeaponMounts.ConvergeOnRange"/>.</para>
	/// </summary>
	public void TorsoPitchTick(short axis, int convergeRange = 0, short snapTarget = -1,
			bool snapEnable = false) {
		short previousAngle = TorsoPitchAngle;
		short rate = TorsoPitchRate;
		short angle = TorsoPitchAngle;

		StepTorsoAxis(ref rate, ref angle, axis, Type.TorsoPitchMaxRate, Type.TorsoPitchAccel,
			Type.TorsoPitchMin, Type.TorsoPitchMax);

		TorsoPitchRate = rate;
		TorsoPitchAngle = angle;

		if (snapEnable && CrossedTarget(previousAngle, TorsoPitchAngle, snapTarget)) {
			TorsoPitchAngle = snapTarget;
			TorsoPitchRate = 0;
		}

		TorsoPitchThread?.SeekToPosition(Type.TorsoPitchSequence, SequencePosition(TorsoPitchAngle));

		Weapons.ConvergeOnRange(this, convergeRange);
	}

	/// <summary>
	/// <c>FUN_0041e8d4</c> — the [Backspace] "Center Turret" command, run every tick until the pilot
	/// takes the torso back. It drives both axes from the angles themselves, so the torso runs home
	/// fast and eases off as it arrives, and enables the snap so it stops exactly on centre.
	///
	/// <para><paramref name="convergeRange"/> is passed straight through to the pitch tick's
	/// convergence pass, so the guns keep toeing in on the selected target while the turret comes
	/// home. The player's input path is the only caller that has a range to give; every AI caller
	/// passes zero.</para>
	/// </summary>
	public void CenterTorsoTick(int convergeRange = 0) {
		TorsoTwistTick((short)-ClampAxis(SimMath.Q10Multiply(CenterGain, TorsoTwistAngle)),
			snapTarget: 0, snapEnable: true);
		TorsoPitchTick((short)-ClampAxis(SimMath.Q10Multiply(CenterGain, TorsoPitchAngle)),
			convergeRange: convergeRange, snapTarget: 0, snapEnable: true);
	}

	/// <summary>
	/// <c>Cockpit_TargetAnglesFromCameraBone</c> (<c>0041ef14</c>) — bring <paramref name="point"/>
	/// into the pilot's own frame, drive both turret axes at it, and hand back what is left of the
	/// error. It is the whole of "point the turret at that", and the AI's fire path reaches it exactly
	/// as the player's automatic tracking does.
	///
	/// <list type="bullet">
	/// <item>Each axis' demand is the residual angle scaled by <see cref="CenterGain"/>, clamped to
	/// the stick's own <c>±0x100</c> and then <b>squared</b> (<c>Q8(|v|, v)</c>), so the turret runs
	/// hard while it is far off and eases as it arrives.</item>
	/// <item>Past <see cref="TrackFarRange"/> a small residual is damped and a very small one is
	/// discarded outright — a dead band that stops the turret hunting on a distant target.</item>
	/// <item>The snap target is the angle the turret <i>would</i> have if it were already on the
	/// point, so the axis stops exactly there rather than swinging through it.</item>
	/// </list>
	/// </summary>
	/// <returns>The residual aim error: yaw and pitch, in binary angle.</returns>
	public (short Yaw, short Pitch) TrackWorldPoint(Vec3i point) {
		var local = CameraNodeTransform.Inverted().TransformPoint(point.X, point.Y, point.Z);
		local = new Vec3i(local.X, local.Y, local.Z - Type.EyeOffsetZ);

		var (pitchError, _, yawError) = SimTrig.EulerToward(local, default);

		int yawDemand = SimMath.Q10Multiply(CenterGain, yawError);
		int pitchDemand = SimMath.Q10Multiply(CenterGain, pitchError);

		if (local.Y > TrackFarRange) {
			yawDemand = DampedFarDemand(yawDemand, SaturatingAbs(yawError), TrackYawDeadband);
			pitchDemand = DampedFarDemand(pitchDemand, SaturatingAbs(pitchError), TrackPitchDeadband);
		}

		short yawAxis = (short)-ClampAxis(yawDemand);
		short pitchAxis = (short)ClampAxis(pitchDemand);

		TorsoTwistTick((short)SimMath.Q8Multiply(SaturatingAbs(yawAxis), yawAxis),
			snapTarget: (short)(TorsoTwistAngle - yawError), snapEnable: true);
		TorsoPitchTick((short)SimMath.Q8Multiply(SaturatingAbs(pitchAxis), pitchAxis),
			convergeRange: SimMath.FastMagnitude3D(local.X, local.Y, local.Z),
			snapTarget: (short)(TorsoPitchAngle + pitchError), snapEnable: true);

		return (yawError, pitchError);
	}

	/// <summary>
	/// The far-range dead band: below <paramref name="deadband"/> of error the axis is stilled
	/// outright, and otherwise its demand is cut to <see cref="TrackFarGain"/>.
	/// </summary>
	private static int DampedFarDemand(int demand, short error, short deadband) {
		if (error >= TrackTrackingBand) {
			return demand;
		}

		return error < deadband ? 0 : SimMath.Q10Multiply(TrackFarGain, demand);
	}

	/// <summary>Beyond this range in the pilot's frame the dead band applies — the original's 50000.</summary>
	public const int TrackFarRange = 50000;

	/// <summary>The error the far-range damping applies below, in binary angle.</summary>
	private const short TrackTrackingBand = 1000;

	/// <summary>Q10 gain the far-range damping cuts the demand to.</summary>
	private const int TrackFarGain = 700;

	/// <summary>Yaw error the far-range band stills the axis under.</summary>
	private const short TrackYawDeadband = 0x32;

	/// <summary>Pitch error the far-range band stills the axis under — larger than the yaw's.</summary>
	private const short TrackPitchDeadband = 0x55;

	/// <summary>How hard the centring command pulls, Q10 — the original's own <c>0xfa</c>.</summary>
	private const int CenterGain = 0xfa;

	/// <summary>
	/// The shared body of the two ticks: they are the same code in the original, differing only in
	/// which pair of fields and which pair of limits they use.
	/// </summary>
	private static void StepTorsoAxis(ref short rate, ref short angle, short axis, short maxRate,
			short accel, short limitMin, short limitMax) {
		short target = (short)SimMath.Q8Multiply(axis, maxRate);
		short meanFrom = rate;

		if (SaturatingAbs(rate) < SaturatingAbs(target)) {
			short step = (short)SimMath.IntegrateRateOverTick(accel);
			SimMath.RateLimitedMoveToward(ref rate, target, step);
		} else {
			// Slowing down is not rate-limited, and neither is turning round: both land on the
			// target immediately, and the angle integrates the new rate over the whole tick.
			rate = target;
			meanFrom = target;
		}

		short moved = (short)(angle + SimMath.IntegrateRateOverTick((short)((meanFrom + rate) >> 1)));
		angle = moved >= limitMax ? limitMax : moved <= limitMin ? limitMin : moved;
	}

	/// <summary>
	/// Whether the angle moved onto or across <paramref name="target"/> this tick — the original's
	/// own sign test on the before and after differences, which counts landing exactly on it.
	/// </summary>
	private static bool CrossedTarget(short before, short after, short target) {
		int now = (short)(after - target);
		int then = (short)(before - target);
		return (now >= 0 && then < 0) || (now <= 0 && then > 0);
	}

	/// <summary>
	/// An angle as a Q14 position in its sequence: the <b>unsigned</b> angle shifted down two bits,
	/// so a whole turn spans the sequence exactly once and a negative angle lands in its far end
	/// rather than off the front.
	/// </summary>
	private static short SequencePosition(short angle) => (short)((ushort)angle >> 2);

	/// <summary>
	/// <c>|x|</c> as the original computes it, saturating rather than wrapping: negating
	/// <see cref="short.MinValue"/> cannot be represented, and it yields <see cref="short.MaxValue"/>.
	/// </summary>
	private static short SaturatingAbs(short value) =>
		value == short.MinValue ? short.MaxValue : value < 0 ? (short)-value : value;

	/// <summary>Clamps to one stick's travel, as the centring command does before handing it on.</summary>
	private static short ClampAxis(int value) =>
		value >= MechControls.AxisFull ? MechControls.AxisFull
		: value < -MechControls.AxisFull + 1 ? (short)-MechControls.AxisFull
		: (short)value;
}
