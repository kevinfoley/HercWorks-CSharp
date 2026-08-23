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
	/// <para>The original also takes the range to the current target here and runs
	/// <c>FUN_0041a74c</c> with it, which converges the gun mounts. That is weapon aiming rather than
	/// torso movement and is not ported.</para>
	/// </summary>
	public void TorsoPitchTick(short axis, short snapTarget = -1, bool snapEnable = false) {
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
	}

	/// <summary>
	/// <c>FUN_0041e8d4</c> — the [Backspace] "Center Turret" command, run every tick until the pilot
	/// takes the torso back. It drives both axes from the angles themselves, so the torso runs home
	/// fast and eases off as it arrives, and enables the snap so it stops exactly on centre.
	/// </summary>
	public void CenterTorsoTick() {
		TorsoTwistTick((short)-ClampAxis(SimMath.Q10Multiply(CenterGain, TorsoTwistAngle)),
			snapTarget: 0, snapEnable: true);
		TorsoPitchTick((short)-ClampAxis(SimMath.Q10Multiply(CenterGain, TorsoPitchAngle)),
			snapTarget: 0, snapEnable: true);
	}

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
