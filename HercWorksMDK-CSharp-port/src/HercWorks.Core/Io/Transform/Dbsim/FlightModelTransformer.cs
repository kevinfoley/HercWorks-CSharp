using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Ported from org.hercworks.core.io.transform.dbsim.FlightModelTransformer.
///
/// FIXED (was two bugs, see KNOWN_ISSUES.md history): verified against real RAZOR.FM/SKIMMER.FM
/// from a retail install. Both are the standard VOL-entry-prefixed loose-file shape (9-byte
/// prefix + content + 1 trailing byte), and the prefix's own declared content-size field reads
/// 54 bytes — matching the read path's byte count, not the write path's (formerly 47). Decoding
/// the read path's layout against the real bytes confirms every `Skip()` region actually is
/// zero-padding in both files, and confirms the value at the "RollFriction" slot is genuinely
/// different from `RollForce` in both samples (400 vs 4, and 300 vs 4) — not a duplicate read.
/// Fixed by: (1) reading into `RollFriction` instead of overwriting `RollForce` a second time,
/// and (2) widening the write path's padding to match the read path's Skip() amounts exactly
/// (6/2/2/2/2 zero bytes instead of 3/1/1/1/1), making read and write byte-count symmetric at 54
/// bytes each.
/// </summary>
public class FlightModelTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var fm = new FlightModel {
			RawBytes = inputArray,
			Ext = FileType.Fm,
			Dir = FileType.Fm,

			PitchRate = IndexShortLE(),
			RollRate = IndexShortLE(),
			RudderForce = IndexShortLE(),
			PitchForce = IndexShortLE(),
			RollForce = IndexShortLE(),
			ThrustFactor = IndexShortLE()
		};

		Skip(6);

		fm.RollMax = IndexShortLE();

		Skip(2);

		fm.Unk22_val16 = IndexShortLE();

		Skip(2);

		fm.Unk26_5or6 = IndexShortLE();

		Skip(2);

		fm.RollFriction = IndexShortLE();

		Skip(2);

		fm.AltitudeMax = IndexIntLE();
		fm.Unk38_val6000 = IndexIntLE();
		fm.AirSpeedMax = IndexIntLE();
		fm.AirSpeedMin = IndexIntLE();
		fm.RollAccel = IndexIntLE();

		return fm;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			// TODO (carried over from Java): log null
			return null;
		}

		var fm = (FlightModel)source;

		using var bytes = new MemoryStream();

		Write(bytes, WriteShortLE(fm.PitchRate));
		Write(bytes, WriteShortLE(fm.RollRate));
		Write(bytes, WriteShortLE(fm.RudderForce));
		Write(bytes, WriteShortLE(fm.PitchForce));
		Write(bytes, WriteShortLE(fm.RollForce));
		Write(bytes, WriteShortLE(fm.ThrustFactor));

		bytes.WriteByte(0x00);
		bytes.WriteByte(0x00);
		bytes.WriteByte(0x00);
		bytes.WriteByte(0x00);
		bytes.WriteByte(0x00);
		bytes.WriteByte(0x00);

		Write(bytes, WriteShortLE(fm.RollMax));

		bytes.WriteByte(0x00);
		bytes.WriteByte(0x00);

		Write(bytes, WriteShortLE(fm.Unk22_val16));

		bytes.WriteByte(0x00);
		bytes.WriteByte(0x00);

		Write(bytes, WriteShortLE(fm.Unk26_5or6));

		bytes.WriteByte(0x00);
		bytes.WriteByte(0x00);

		Write(bytes, WriteShortLE(fm.RollFriction));

		bytes.WriteByte(0x00);
		bytes.WriteByte(0x00);

		Write(bytes, WriteIntLE(fm.AltitudeMax));
		Write(bytes, WriteIntLE(fm.Unk38_val6000));
		Write(bytes, WriteIntLE(fm.AirSpeedMax));
		Write(bytes, WriteIntLE(fm.AirSpeedMin));
		Write(bytes, WriteIntLE(fm.RollAccel));

		return bytes.ToArray();
	}

	private static void Write(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
