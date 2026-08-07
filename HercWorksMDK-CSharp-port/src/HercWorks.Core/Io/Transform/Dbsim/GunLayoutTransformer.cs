using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Ported from org.hercworks.core.io.transform.dbsim.GunLayoutTransformer.</summary>
public class GunLayoutTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}
		SetBytes(inputArray);

		var data = new GunLayout(IndexShortLE()) {
			RawBytes = inputArray,
			Ext = FileType.Gl,
			Dir = FileType.Gl
		};

		for (int i = 0; i < data.TotalGuns; i++) {
			var entry = data.NewEntry();

			entry.BoneId = IndexShortLE();
			entry.Unk1_val = IndexShortLE();
			entry.Unk2_val = IndexShortLE();
			entry.AngleDirOption = IndexByte();
			entry.FireChainNumber = IndexByte();
			entry.Unk3_0or_Neg5000 = IndexShortLE();
			entry.Unk4_0or_5000 = IndexShortLE();
			entry.Unk5_Neg8000 = IndexShortLE();
			entry.Unk6_16000 = IndexShortLE();
			entry.Offset[0] = IndexShortLE();
			entry.Offset[1] = IndexShortLE();
			entry.Offset[2] = IndexShortLE();
			entry.Unk7_val = IndexByte();
			entry.HardpointId = IndexByte();
			entry.Unk8_val = IndexShortLE();

			data.Hardpoints![i] = entry;
		}

		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var src = (GunLayout)source!;

		using var outStream = new MemoryStream();

		var totalBytes = WriteShortLE(src.TotalGuns);
		outStream.Write(totalBytes, 0, totalBytes.Length);

		if (src.Hardpoints != null) {
			for (int i = 0; i < src.TotalGuns; i++) {
				var entry = src.Hardpoints[i];

				Write(outStream, WriteShortLE(entry.BoneId));
				Write(outStream, WriteShortLE(entry.Unk1_val));
				Write(outStream, WriteShortLE(entry.Unk2_val));
				outStream.WriteByte(entry.AngleDirOption);
				outStream.WriteByte(entry.FireChainNumber);
				Write(outStream, WriteShortLE(entry.Unk3_0or_Neg5000));
				Write(outStream, WriteShortLE(entry.Unk4_0or_5000));
				Write(outStream, WriteShortLE(entry.Unk5_Neg8000));
				Write(outStream, WriteShortLE(entry.Unk6_16000));
				Write(outStream, WriteShortLE(entry.Offset[0]));
				Write(outStream, WriteShortLE(entry.Offset[1]));
				Write(outStream, WriteShortLE(entry.Offset[2]));
				outStream.WriteByte(entry.Unk7_val);
				outStream.WriteByte(entry.HardpointId);
				Write(outStream, WriteShortLE(entry.Unk8_val));
			}
		}

		return outStream.ToArray();
	}

	private static void Write(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
