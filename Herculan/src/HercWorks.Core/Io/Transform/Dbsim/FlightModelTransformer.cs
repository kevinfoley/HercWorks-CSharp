using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Ported from org.hercworks.core.io.transform.dbsim.FlightModelTransformer.
///
/// <para>Verified against real RAZOR.FM/SKIMMER.FM from a retail install. Both are the standard
/// VOL-entry-prefixed loose-file shape (9-byte prefix + content + 1 trailing byte), and the
/// prefix's own declared content-size field reads 54 bytes — which is also the count
/// <c>MechType_InitOne</c> (<c>004201a8</c>) asks for when it reads the file into the type record,
/// so read and write are both held to 54 here.</para>
///
/// <para>The skipped runs are zero in both retail files, and stay zero on the way out. They are not
/// all unused, though: bytes 14-17 are the slot the loader computes the ceiling-versus-airspeed
/// slope into once the file is in memory. See <see cref="FlightModel"/>.</para>
/// </summary>
public class FlightModelTransformer : ByteTransformer<FlightModel> {
	public override FlightModel? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var fm = new FlightModel {
			MaxPitchRate = IndexShortLE(),
			MaxRollRate = IndexShortLE(),
			MaxYawRate = IndexShortLE(),
			MaxPitchAccel = IndexShortLE(),
			MaxRollAccel = IndexShortLE(),
			ThrustResponse = IndexShortLE()
		};

		// Offsets 12-17: two spare bytes and the loader's derived ceiling slope, both zero on disk.
		Skip(6);

		fm.AngularDamping = IndexShortLE();

		Skip(2);

		fm.PitchLevelShift = IndexShortLE();

		Skip(2);

		fm.RollLevelShift = IndexShortLE();

		Skip(2);

		fm.BankTurnShift = IndexShortLE();

		Skip(2);

		fm.CeilingAtMaxSpeed = IndexIntLE();
		fm.CeilingAtMinSpeed = IndexIntLE();
		fm.AirSpeedMax = IndexIntLE();
		fm.AirSpeedMin = IndexIntLE();
		fm.LateralDrag = IndexIntLE();

		return fm;
	}

	public override byte[]? Write(FlightModel fm) {

		using var bytes = new MemoryStream();

		Emit(bytes, WriteShortLE(fm.MaxPitchRate));
		Emit(bytes, WriteShortLE(fm.MaxRollRate));
		Emit(bytes, WriteShortLE(fm.MaxYawRate));
		Emit(bytes, WriteShortLE(fm.MaxPitchAccel));
		Emit(bytes, WriteShortLE(fm.MaxRollAccel));
		Emit(bytes, WriteShortLE(fm.ThrustResponse));

		Pad(bytes, 6);

		Emit(bytes, WriteShortLE(fm.AngularDamping));

		Pad(bytes, 2);

		Emit(bytes, WriteShortLE(fm.PitchLevelShift));

		Pad(bytes, 2);

		Emit(bytes, WriteShortLE(fm.RollLevelShift));

		Pad(bytes, 2);

		Emit(bytes, WriteShortLE(fm.BankTurnShift));

		Pad(bytes, 2);

		Emit(bytes, WriteIntLE(fm.CeilingAtMaxSpeed));
		Emit(bytes, WriteIntLE(fm.CeilingAtMinSpeed));
		Emit(bytes, WriteIntLE(fm.AirSpeedMax));
		Emit(bytes, WriteIntLE(fm.AirSpeedMin));
		Emit(bytes, WriteIntLE(fm.LateralDrag));

		return bytes.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);

	private static void Pad(MemoryStream outArr, int count) {
		for (int i = 0; i < count; i++) {
			outArr.WriteByte(0x00);
		}
	}
}
