using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.Struct.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Ported from org.hercworks.core.io.transform.dbsim.MissileDatFileTransformer.</summary>
public class MissileDatFileTransformer : ByteTransformer<MissileDatFile> {
	public override MissileDatFile? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}
		SetBytes(inputArray);

		short total = IndexShortLE();

		var data = new MissileDatFile(total);

		for (int b = 0; b < total; b++) {
			var bullet = new ProjMissileDatEntry {
				ModelId = IndexShortLE(),
				Lifetime = IndexShortLE(),
				ClipRadius = IndexShortLE(),
				Unk2Flag = IndexShortLE(),
				SfxFireIdBullets = IndexShortLE(),
				Unk3Uint16 = IndexShortLE(),
				SfxFireIdMissiles = IndexShortLE()
			};

			data.Entries![b] = bullet;
		}

		return data;
	}

	public override byte[]? Write(MissileDatFile data) {
		if (data == null) {
			// TODO (carried over from Java): log null
			return null;
		}

		using var outStream = new MemoryStream();

		var totalBytes = WriteShortLE((short)data.Entries!.Length);
		outStream.Write(totalBytes, 0, totalBytes.Length);

		foreach (var bullet in data.Entries) {
			Emit(outStream, WriteShortLE(bullet.ModelId));
			Emit(outStream, WriteShortLE(bullet.Lifetime));
			Emit(outStream, WriteShortLE(bullet.ClipRadius));
			Emit(outStream, WriteShortLE(bullet.Unk2Flag));
			Emit(outStream, WriteShortLE(bullet.SfxFireIdBullets));
			Emit(outStream, WriteShortLE(bullet.Unk3Uint16));
			Emit(outStream, WriteShortLE(bullet.SfxFireIdMissiles));
		}

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
