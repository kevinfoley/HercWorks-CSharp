using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.Struct.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Ported from org.hercworks.core.io.transform.dbsim.MissileDatFileTransformer.</summary>
public class MissileDatFileTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}
		SetBytes(inputArray);

		short total = IndexShortLE();

		var data = new MissileDatFile(total) {
			Ext = FileType.Dat,
			Dir = FileType.Dat,
			RawBytes = inputArray
		};

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

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			// TODO (carried over from Java): log null
			return null;
		}

		using var outStream = new MemoryStream();

		var data = (MissileDatFile)source;

		var totalBytes = WriteShortLE((short)data.Entries!.Length);
		outStream.Write(totalBytes, 0, totalBytes.Length);

		foreach (var bullet in data.Entries) {
			Write(outStream, WriteShortLE(bullet.ModelId));
			Write(outStream, WriteShortLE(bullet.Lifetime));
			Write(outStream, WriteShortLE(bullet.ClipRadius));
			Write(outStream, WriteShortLE(bullet.Unk2Flag));
			Write(outStream, WriteShortLE(bullet.SfxFireIdBullets));
			Write(outStream, WriteShortLE(bullet.Unk3Uint16));
			Write(outStream, WriteShortLE(bullet.SfxFireIdMissiles));
		}

		return outStream.ToArray();
	}

	private static void Write(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
