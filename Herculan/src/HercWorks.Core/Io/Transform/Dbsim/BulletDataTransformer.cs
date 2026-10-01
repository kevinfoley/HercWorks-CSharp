using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.Struct.Dbsim;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Transforms byte[] data to and from BULLETS.DAT (see <see cref="BulletData"/>).</summary>
public class BulletDataTransformer : ByteTransformer<BulletData> {
	public override BulletData? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}
		SetBytes(inputArray);

		short total = IndexShortLE();
		var data = new BulletData { Total = total, Entries = new BulletType[total] };

		for (int i = 0; i < total; i++) {
			data.Entries[i] = new BulletType {
				ModelId = IndexShortLE(),
				Lifetime = IndexShortLE(),
				ClipRadius = IndexShortLE(),
				FrameInterval = IndexShortLE(),
				FireSoundId = IndexShortLE(),
				Scatter = IndexShortLE(),
				LifetimeRateFlag = IndexShortLE()
			};
		}

		return data;
	}

	public override byte[]? Write(BulletData data) {
		if (data?.Entries == null) {
			return null;
		}

		using var outStream = new MemoryStream();
		Emit(outStream, WriteShortLE((short)data.Entries.Length));

		foreach (var entry in data.Entries) {
			Emit(outStream, WriteShortLE(entry.ModelId));
			Emit(outStream, WriteShortLE(entry.Lifetime));
			Emit(outStream, WriteShortLE(entry.ClipRadius));
			Emit(outStream, WriteShortLE(entry.FrameInterval));
			Emit(outStream, WriteShortLE(entry.FireSoundId));
			Emit(outStream, WriteShortLE(entry.Scatter));
			Emit(outStream, WriteShortLE(entry.LifetimeRateFlag));
		}

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
