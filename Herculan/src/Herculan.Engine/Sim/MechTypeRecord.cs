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

	/// <summary>
	/// True for the Razor — record offset 78, the type record's <c>+0x50</c>. It selects a different
	/// set of code paths throughout: a different behaviour class with its own per-tick move, a
	/// different control law reading the stick axes as an aircraft's, a different HUD speed
	/// conversion, and a <c>fm\&lt;NAME&gt;.FM</c> flight model the loader reads only for a type
	/// that sets it. See <see cref="MechObject.Flight"/>.
	/// </summary>
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

	/// <summary>
	/// Record fields 98 and 100 (the exe's <c>typeRecord+0x64</c> and <c>+0x66</c>) — where the
	/// pilot's eye sits relative to the node <see cref="CameraBoneId"/> names, in that node's own
	/// frame and in world units. The X component is always zero: the mech vtable's <c>+0x30</c>
	/// accessor (<c>004155c4</c>) builds the point as <c>(0, +0x64, +0x66)</c>, and the cockpit
	/// branch of <c>FUN_004011a0</c> puts it through the node's world matrix to get the eye.
	///
	/// <para>The lift is the load-bearing half. Retail states 0-820 for it across the fleet, which on
	/// OUTLAW moves the eye from 44% of the model's height to 82% — waist to cockpit. It is also what
	/// gives the eye a lever arm on the node: the node rotates through a turn-in-place, and an eye
	/// sitting on top of it swings where one sitting at its origin would not.</para>
	/// </summary>
	public short EyeOffsetY => Data.CameraYAxisAdj;

	/// <inheritdoc cref="EyeOffsetY"/>
	public short EyeOffsetZ => Data.CameraXAxisAdj;

	/// <summary>
	/// Record field 22 (the exe's <c>typeRecord+0x18</c>) — how high above the machine's origin its
	/// hit cylinder is centred, in world units. <c>Mech_ShieldAbsorb_DirectFire</c> builds the point
	/// it measures every direct-fire shot against as <c>(0, 0, this)</c> in the machine's own frame,
	/// which is why a beam over a HERC's feet misses and one through its torso does not.
	///
	/// <para>Retail states 1000 for every heavy and medium chassis, 750 for the three light ones and
	/// 0 for the RAZOR — a class figure, not a per-model measurement.</para>
	/// </summary>
	public short HitCenterHeight => Data.Unk22_Val750Razor0;

	/// <summary>
	/// Record field 24 (the exe's <c>typeRecord+0x1a</c>) — the machine's hit radius, in world units,
	/// and the only radius any of its hit tests use: the coarse reject at the top of
	/// <c>Mech_DirectFireHitTest</c> and the cylinder <c>Mech_ShieldAbsorb_DirectFire</c> tests the
	/// ray against are both this. 2500 for a heavy, 1500 for a medium, 1000 for the SPIDER.
	///
	/// <para>It is deliberately generous — it only has to be wide enough that nothing which could hit
	/// is rejected, because the <c>col\&lt;NAME&gt;.COL</c> sphere model behind it is what actually
	/// decides. The field was named <c>AiAimTargOffset</c> on a guess; these two consumers identify
	/// it.</para>
	/// </summary>
	public short HitRadius => Data.AiAimTargOffset;

	/// <summary>
	/// Record field 110 (the exe's <c>typeRecord+0x70</c>) — the machine's body radius, and the one
	/// figure behind <i>both</i> of its radius accessors: what the blast sweep subtracts before
	/// deciding a machine is inside a blast, and what the collision test keeps machines apart by.
	///
	/// <para><b>Every retail HERC states 750</b>, so a SPIDER and a PITBULL occupy the same footprint
	/// as far as the simulation is concerned and any two machines stop 1500 units — 9 m — apart. It
	/// is a third of <see cref="HitRadius"/>, which is the generous <i>shot</i> radius and a
	/// different field for a different job.</para>
	/// </summary>
	public short BodyRadius => Data.BodyRadius;

	/// <summary>
	/// Record field 76 (the exe's <c>typeRecord+0x4e</c>) — the chassis' mass, and the reason a heavy
	/// machine wins a collision: each party's speed is weighed by its own mass and the difference
	/// lands on both as blast damage. See <see cref="MechObject.CollisionDamage"/>.
	///
	/// <para>5000 for a light, 20000 for the PITBULL — and <b>0 for the SPIDER</b>, which is not a
	/// missing value but a working one: a SPIDER contributes nothing to an impact from either side of
	/// it.</para>
	/// </summary>
	public short Mass => Data.Mass;

	/// <summary>
	/// Record field 72 (the exe's <c>typeRecord+0x4a</c>) — how many legs the chassis walks on, which
	/// is 2 for every retail HERC but the four-legged PITBULL.
	/// <c>Mech_ComponentDamageWrite</c> reads it to decide whether the leg-condition check covers the
	/// two front dependent slots alone or averages them with the rear pair at slots 10 and 11.
	/// </summary>
	public short LegCount => Data.ModelLegsTotal;

	/// <summary>
	/// Whether a hit can knock this chassis' weapon mounts out — record offset 84, the type record's
	/// <c>+0x56</c>, which <c>Mech_ApplyDirectFireDamage</c> tests before it will roll for mount
	/// destruction at all.
	///
	/// <para>Retail states <b>1 on every biped</b> (SAMSON, APOCA, OUTLAW, RAZOR) and <b>0 on the
	/// PITBULL</b>, the four-legged chassis, whose hardpoints are therefore immune to the roll. The
	/// certain path is not gated on it: a PITBULL mount whose component is shot to
	/// <see cref="MechObject.FullyDamaged"/> still dies, through
	/// <see cref="WeaponMount.ConditionChanged"/>.</para>
	///
	/// <para>The record offsets are the file's; the type record lives at <c>MECH_TYPE_DATA[i]+2</c>,
	/// so record offset 84 is the <c>+0x56</c> the damage code names. Nothing else in the image was
	/// traced reading this field, so "mounts are destructible" is the whole of what it is known to
	/// mean.</para>
	/// </summary>
	public bool WeaponMountsDestructible => Data.Unk84_val != 0;

	/// <summary>
	/// The shape part each leg's node hangs on, and the leg's kind byte — record offsets 117 and 112
	/// read per leg. Only kind 0 walks; <c>Mech_PlaceLegsOnGround</c> skips anything else outside the
	/// falling case. Every retail HERC states parts 14 and 15, both kind 0.
	/// </summary>
	public int LegPartId(int leg) =>
		leg >= 0 && leg < Data.LegPartIds.Length ? Data.LegPartIds[leg] : -1;

	/// <inheritdoc cref="LegPartId"/>
	public int LegKind(int leg) => leg >= 0 && leg < Data.LegKinds.Length ? Data.LegKinds[leg] : -1;

	/// <summary>
	/// The fore/aft position a leg node passes through as the foot plants, per gait — see
	/// <see cref="HercSimDat.FootfallTriggerWalk"/>. <paramref name="gait"/> is the original's own
	/// index: 0 walking, 1 reversing, 2 running.
	/// </summary>
	public short FootfallTrigger(int gait) => gait switch {
		0 => Data.FootfallTriggerWalk,
		1 => Data.FootfallTriggerReverse,
		2 => Data.FootfallTriggerRun,
		_ => Data.FootfallTriggerLand,
	};

	/// <summary>The arming counterpart of <see cref="FootfallTrigger"/>.</summary>
	public short FootfallRearm(int gait) => gait switch {
		0 => Data.FootfallRearmWalk,
		1 => Data.FootfallRearmReverse,
		2 => Data.FootfallRearmRun,
		_ => 0,
	};

	/// <summary>
	/// Record field 190 (the exe's <c>typeRecord+0xc0</c>) — the shield array's total capacity before
	/// any Shield Pod, which <c>Shield_Init</c> reads straight out of here. Despite living in the
	/// per-type record it is not a per-type stat: every retail HERC carries 3500. See
	/// <see cref="ShieldCharge"/>.
	/// </summary>
	public short ShieldCapacity => Data.ShieldMaxTotal;

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

	/// <summary>
	/// Record field 26 — the torso-twist sequence, a full turn of the twist node. The machine's
	/// twist angle is a <i>position</i> within it rather than something it plays through; see
	/// <see cref="MechObject.TorsoTwistTick"/>. Uniform across the fleet at sequence 0.
	/// </summary>
	public short TorsoTwistSequence => Data.AnimId_TorsoTwist;

	/// <summary>Record field 28 — twist rate at full stick, in binary angle per second.</summary>
	public short TorsoTwistMaxRate => Data.TorsoTwistSpeed;

	/// <summary>
	/// Record field 30 — how fast the twist rate itself may build. Unlike the locomotion accel pair
	/// this one is already integrated over the tick by the original, so it needs no rescale.
	/// </summary>
	public short TorsoTwistAccel => Data.TorsoRotateAccel;

	/// <summary>
	/// Record field 32 — how far the torso may twist either way, as a binary angle. 14000 across the
	/// whole fleet, which is 76.9 degrees.
	/// </summary>
	public short TorsoTwistLimit => Data.TorsoTwistDegreeMax;

	/// <summary>Record field 34 — the torso-pitch sequence; see <see cref="TorsoTwistSequence"/>.</summary>
	public short TorsoPitchSequence => Data.AnimId_TorsoPitch;

	/// <summary>Record field 36 — pitch rate at full stick.</summary>
	public short TorsoPitchMaxRate => Data.TorsoPitchMaxRate;

	/// <summary>Record field 38 — how fast the pitch rate may build.</summary>
	public short TorsoPitchAccel => Data.TorsoPitchRate;

	/// <summary>Record field 40 — pitch limit looking up. Asymmetric with <see cref="TorsoPitchMin"/>.</summary>
	public short TorsoPitchMax => Data.TorsoPitchMax;

	/// <summary>Record field 42 — pitch limit looking down, negative.</summary>
	public short TorsoPitchMin => Data.TorsoPitchMin;

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

	/// <summary>
	/// The flyer's branch of the same readout — <c>Mech_GetDisplaySpeedKph</c> (<c>0041bb3c</c>)
	/// remaps airspeed from <c>[0, airSpeedMax]</c> onto <c>[0, HudSpeedScale]</c> through
	/// <c>Math_MapRange</c> (<c>0047de3c</c>) rather than dividing by the walker's top speed,
	/// which a flyer's record does not describe.
	/// </summary>
	public int DisplayAirSpeedKph(int airSpeed, int airSpeedMax) =>
		airSpeedMax != 0 ? MapRange(airSpeed, 0, airSpeedMax, 0, HudSpeedScale) : 0;

	/// <summary>
	/// <c>Math_MapRange</c> (<c>0047de3c</c>) — a linear remap of <paramref name="value"/> from
	/// <c>[fromLow, fromHigh]</c> onto <c>[toLow, toHigh]</c>, with a rounding bias of one less than
	/// the input span, signed to match the output's direction.
	/// </summary>
	private static int MapRange(int value, int fromLow, int fromHigh, int toLow, int toHigh) {
		int span = fromHigh - fromLow;
		long bias = System.Math.Abs(span) - 1;
		if (toHigh < toLow) {
			bias = -bias;
		}

		return (int)(((long)(value - fromLow) * (toHigh - toLow) + bias) / span) + toLow;
	}
}
