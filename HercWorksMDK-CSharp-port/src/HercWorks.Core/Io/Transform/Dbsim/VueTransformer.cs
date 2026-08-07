using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Ported from org.hercworks.core.io.transform.dbsim.VueTransformer.</summary>
public class VueTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var data = new Vue {
			Ext = FileType.Vue,
			Dir = FileType.Vue,
			RawBytes = inputArray
		};

		data.TotalViewports = IndexIntLE();
		data.Entries = new Vue.Entry[data.TotalViewports];

		for (int i = 0; i < data.TotalViewports; i++) {
			var entry = data.NewEntry();

			entry.OriginX = IndexIntLE();
			entry.OriginY = IndexIntLE();
			entry.WidthMax = IndexIntLE();
			entry.HeightMax = IndexIntLE();

			entry.UnkOfsX = IndexIntLE();
			entry.UnkOfsY = IndexIntLE();
			entry.UnkOfsW = IndexIntLE();
			entry.UnkOfsH = IndexIntLE();

			data.Entries[i] = entry;
		}

		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var data = (Vue)source!;

		using var outStream = new MemoryStream();

		void WriteInt(int i) {
			var b = WriteIntLE(i);
			outStream.Write(b, 0, b.Length);
		}

		WriteInt(data.TotalViewports);

		for (int i = 0; i < data.TotalViewports; i++) {
			var entry = data.Entries![i];
			WriteInt(entry.OriginX);
			WriteInt(entry.OriginY);
			WriteInt(entry.WidthMax);
			WriteInt(entry.HeightMax);
			WriteInt(entry.UnkOfsX);
			WriteInt(entry.UnkOfsY);
			WriteInt(entry.UnkOfsW);
			WriteInt(entry.UnkOfsH);
		}

		return outStream.ToArray();
	}
}
