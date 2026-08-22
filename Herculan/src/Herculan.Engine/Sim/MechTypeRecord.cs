using HercWorks.Core.Data.File.Dat.Sim;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// A mech type's locomotion parameters as the <i>simulation</i> sees them, which is not the same as
/// what the file says. <c>MechType_InitOne</c> (<c>004201a8</c>) rescales four of the speed fields
/// at load, so what the file holds and what the control law reads are not the same numbers. This type
/// applies the rescale and names each field for the role the sim gives it, leaving the parsed file
/// untouched.
///
/// <para><b>The load-time rescale.</b> Record fields 194 and 196 are a stride-calibration pair, not
/// the friction coefficients they were once taken for:</para>
/// <code>
///   scale = Q16Divide(rec196 * 400, rec194)
///   MaxReverse, MaxForward, GaitThreshold, ReverseGaitThreshold  *= scale
///   MaxTurnRate is NOT rescaled
/// </code>
/// <para>What it normalises is the designer's speed points against the model's actual stride length,
/// so that <c>maxSpeed x strideLengthPerTick</c> lands on the same real speed for every machine.
/// Verified across all 18 HERCs by predicting run-gait top speed from the animation data alone —
/// see docs/simulation/mech-locomotion.md. Without it, APOCA comes out 2x wrong.</para>
///
/// <para>Only non-flyers are rescaled; the Razor (<see cref="IsFlyer"/>) keeps its raw numbers and
/// takes different code paths throughout.</para>
/// </summary>
public sealed class MechTypeRecord {
	/// <summary>The HUD's fixed speed constant — <c>Q10(315 x rawMaxForward)</c>, computed pre-rescale.</summary>
	private const int HudSpeedConstant = 0x13b;

	public MechTypeRecord(HercSimDat data) {
		Data = data;

		IsFlyer = data.InputFlagFlyer != 0;
		RawMaxForward = data.SpeedForward;
		MaxTurnRate = data.SpeedTurn;

		int maxReverse = data.SpeedReverse;
		int maxForward = data.SpeedForward;
		int gaitThreshold = data.GaitThreshold;
		int reverseGaitThreshold = data.GaitThresholdReverse;

		if (!IsFlyer && data.StrideScaleDivisor != 0) {
			// The HUD scale is computed before the rescale, which is why the readout always tops out
			// at 315 * rawMax / 1024 no matter what the scale does to the simulated speed.
			int scale = SimMath.Q16Divide(data.StrideScaleNumerator * 400, data.StrideScaleDivisor);
			maxReverse = (short)SimMath.Q16Multiply(maxReverse, scale);
			maxForward = (short)SimMath.Q16Multiply(maxForward, scale);
			gaitThreshold = (short)SimMath.Q16Multiply(gaitThreshold, scale);
			reverseGaitThreshold = (short)SimMath.Q16Multiply(reverseGaitThreshold, scale);
			StrideScale = scale;
		} else {
			StrideScale = 1 << 16;
		}

		MaxReverse = (short)maxReverse;
		MaxForward = (short)maxForward;
		GaitThreshold = (short)gaitThreshold;
		ReverseGaitThreshold = (short)reverseGaitThreshold;
		HudSpeedScale = (short)SimMath.Q10Multiply(HudSpeedConstant, RawMaxForward);
	}

	/// <summary>The parsed file this record was derived from.</summary>
	public HercSimDat Data { get; }

	/// <summary>True for the Razor. Its movement is a different set of code paths and is not ported.</summary>
	public bool IsFlyer { get; }

	/// <summary>The Q16 ratio the speed fields were scaled by. 1.0 when the type is not rescaled.</summary>
	public int StrideScale { get; }

	/// <summary>Record field 4 before rescaling — what the HUD's own constant is calibrated against.</summary>
	public short RawMaxForward { get; }

	/// <summary>Record field 0 — peak turn rate, in BAM per tick. Not rescaled.</summary>
	public short MaxTurnRate { get; }

	/// <summary>Record field 2 — maximum reverse speed, negative, rescaled.</summary>
	public short MaxReverse { get; }

	/// <summary>Record field 4 — maximum forward speed, rescaled.</summary>
	public short MaxForward { get; }

	/// <summary>
	/// Record field 6 — how much the speed scalar moves toward its target each tick. The original
	/// applies it as a raw per-tick step with no timestep scaling, making its acceleration ramp
	/// frame-rate dependent; the engine puts it through <see cref="SimMath.ScalePerTickStep"/> at
	/// the call site instead (see <see cref="SimWorld.TicksPerSecond"/>). The value here is the
	/// unscaled constant, as the file holds it.
	/// </summary>
	public short SpeedAccel => Data.SpeedAccelDecel;

	/// <summary>Record field 8 — the same, for the turn rate. Also scaled at the call site.</summary>
	public short TurnAccel => Data.DecelTurning;

	/// <summary>Record field 10 — the model node the cockpit eye rides.</summary>
	public short CameraBoneId => Data.CameraBoneId;

	/// <summary>Record field 12 — walk sequence id.</summary>
	public short WalkSequence => Data.AnimId_Walk;

	/// <summary>Record field 14 — run sequence id.</summary>
	public short RunSequence => Data.AnimId_Run;

	/// <summary>Record field 16 — the stop / step-off sequence played when slowing to a halt forwards.</summary>
	public short StopForwardSequence => Data.AnimId_StopMove;

	/// <summary>
	/// Record field 18 — the reverse-facing stop sequence. Nothing to do with torso pitch, which this
	/// field was long named for; those parameters are fields 36-42.
	/// </summary>
	public short StopReverseSequence => Data.AnimId_StopReverse;

	/// <summary>Record field 20 — ride height, added to the terrain height under the machine.</summary>
	public short RideHeight => Data.UnitOffsetYAdjust;

	/// <summary>Record field 44 — the speed at which the walk gait gives way to the run gait.</summary>
	public short GaitThreshold { get; }

	/// <summary>
	/// Record field 108 — the same threshold on the reverse side. Nothing to do with the camera, which
	/// this field was long named for.
	/// </summary>
	public short ReverseGaitThreshold { get; }

	/// <summary>
	/// Record field 68 — the death / fall sequence. Not a leg-damage flag word, which this field was
	/// long named for.
	/// </summary>
	public short DeathSequence => Data.AnimId_Death;

	/// <summary>
	/// Record field 122 — the turn-in-place sequence. Uniform
	/// across the fleet: 7 frames of 1820 BAM (10.00 degrees) each, no translation.
	/// </summary>
	public short TurnInPlaceSequence => Data.AnimId_TurnInPlace;

	/// <summary>
	/// The HUD's speed divisor, set at load to <c>Q10(315 x rawMaxForward)</c>. The readout is
	/// <c>speed * HudSpeedScale / MaxForward</c>, so the simulated maximum always displays the same
	/// number whatever <see cref="StrideScale"/> did to it.
	/// </summary>
	public short HudSpeedScale { get; }

	/// <summary>
	/// The speed readout for a given speed scalar, in km/h — <c>Hud_GetDisplaySpeedKph</c>'s form.
	/// Note it is calibrated for the run gait only: below <see cref="GaitThreshold"/> a HERC
	/// physically covers roughly half what this claims.
	/// </summary>
	public int DisplaySpeedKph(int speed) =>
		MaxForward != 0 ? speed * HudSpeedScale / MaxForward : 0;
}
