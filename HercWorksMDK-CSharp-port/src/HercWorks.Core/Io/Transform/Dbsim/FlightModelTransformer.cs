using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Ported from org.hercworks.core.io.transform.dbsim.FlightModelTransformer.
///
/// TWO ISSUES FOUND HERE, flagged in KNOWN_ISSUES.md:
/// 1) The read path sets RollForce a second time (overwriting its earlier value) where the write
///    path uses RollFriction in the equivalent slot — RollFriction is never actually set on read.
/// 2) Read and write are NOT byte-count symmetric: tallying every field, the write path produces
///    47 bytes total but the read path consumes 54 bytes (the skip() amounts between fields
///    don't match the zero-byte padding actually written). This is a real inconsistency in the
///    original, not introduced by this port — since there's no real flight-model file available
///    to determine which side (or neither) reflects the true on-disk layout, both methods are
///    ported exactly as written rather than guessing at a fix.
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

		fm.RollForce = IndexShortLE(); // see class doc — likely meant RollFriction

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

		Write(bytes, WriteShortLE(fm.RollMax));

		bytes.WriteByte(0x00);

		Write(bytes, WriteShortLE(fm.Unk22_val16));

		bytes.WriteByte(0x00);

		Write(bytes, WriteShortLE(fm.Unk26_5or6));

		bytes.WriteByte(0x00);

		Write(bytes, WriteShortLE(fm.RollFriction));

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
