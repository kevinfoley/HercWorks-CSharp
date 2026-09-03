using HercWorks.Core.Data.File.Dbsim;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// A flyer chassis' <c>fm\&lt;NAME&gt;.FM</c> as the <i>simulation</i> sees it, which is not quite
/// what the file says. <c>MechType_InitOne</c> (<c>004201a8</c>) reads the 54 bytes into the type
/// record at <c>+0x1dc</c> and then writes one derived figure back into the middle of them, at the
/// four bytes the file itself leaves zero. This type holds that derivation and leaves the parsed
/// file untouched, the same split <see cref="MechTypeRecord"/> draws.
///
/// <para>Everything else here is a pass-through; see <see cref="FlightModel"/> for what each field
/// means. The one thing worth saying twice is <see cref="Ceiling"/>, because it is the mechanic that
/// gives the RAZOR its character.</para>
/// </summary>
public sealed class FlightModelRecord {
	public FlightModelRecord(FlightModel data) {
		Data = data;

		int speedRange = data.AirSpeedMax - data.AirSpeedMin;
		CeilingPerSpeed = speedRange != 0
			? SimMath.Q16Divide(data.CeilingAtMaxSpeed - data.CeilingAtMinSpeed, speedRange)
			: 0;
	}

	/// <summary>The parsed file this record was derived from.</summary>
	public FlightModel Data { get; }

	/// <summary>
	/// How much the flight ceiling rises per unit of airspeed above
	/// <see cref="FlightModel.AirSpeedMin"/>, Q16 — <c>MechType_InitOne</c>'s
	/// <c>Q16Divide(CeilingAtMaxSpeed - CeilingAtMinSpeed, AirSpeedMax - AirSpeedMin)</c>, written
	/// into the type record's copy of the file at load. 43.2 on the RAZOR.
	/// </summary>
	public int CeilingPerSpeed { get; }

	/// <summary>
	/// How high above the ground this airframe may fly at a given airspeed, in world units. The
	/// flight model does not clamp against it: past the ceiling it pushes the nose down in
	/// proportion to the overshoot, resolved through the current bank so the push is toward the
	/// ground rather than toward the airframe's own belly.
	///
	/// <para><b>Altitude is bought with speed.</b> The RAZOR's ceiling runs from 6000 world units at
	/// its 250 idle airspeed to 60000 at its 1500 maximum — 36 metres to 360 — so a pilot who wants
	/// height has to go and get it at full throttle, and one who throttles back is pushed down again.
	/// It is also why <see cref="FlightModel.CeilingAtMaxSpeed"/> is not the flat "max altitude" its
	/// position in the file suggests: nothing clamps to it, and it is only reached at the top of the
	/// speed range.</para>
	/// </summary>
	public int Ceiling(int airSpeed) =>
		SimMath.Q16Multiply(airSpeed - Data.AirSpeedMin, CeilingPerSpeed) + Data.CeilingAtMinSpeed;
}
