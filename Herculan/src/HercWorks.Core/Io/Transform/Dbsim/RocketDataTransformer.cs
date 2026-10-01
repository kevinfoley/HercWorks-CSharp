using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.Struct.Dbsim;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Transforms byte[] data to and from ROCKETS.DAT (see <see cref="RocketData"/>).</summary>
public class RocketDataTransformer : ByteTransformer<RocketData> {
	public override RocketData? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}
		SetBytes(inputArray);

		short total = IndexShortLE();
		var data = new RocketData { Total = total, Entries = new RocketType[total] };

		for (int i = 0; i < total; i++) {
			data.Entries[i] = new RocketType {
				ModelId = IndexShortLE(),
				Lifetime = IndexShortLE(),
				Acceleration = IndexShortLE(),
				ClipRadius = IndexShortLE(),
				FrameInterval = IndexShortLE(),
				AnimSequence = IndexShortLE(),
				FireSoundId = IndexShortLE()
			};
		}

		return data;
	}

	public override byte[]? Write(RocketData data) {
		if (data?.Entries == null) {
			return null;
		}

		using var outStream = new MemoryStream();
		Emit(outStream, WriteShortLE((short)data.Entries.Length));

		foreach (var entry in data.Entries) {
			Emit(outStream, WriteShortLE(entry.ModelId));
			Emit(outStream, WriteShortLE(entry.Lifetime));
			Emit(outStream, WriteShortLE(entry.Acceleration));
			Emit(outStream, WriteShortLE(entry.ClipRadius));
			Emit(outStream, WriteShortLE(entry.FrameInterval));
			Emit(outStream, WriteShortLE(entry.AnimSequence));
			Emit(outStream, WriteShortLE(entry.FireSoundId));
		}

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
