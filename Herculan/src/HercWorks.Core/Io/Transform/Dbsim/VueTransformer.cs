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

			entry.ViewportX0 = IndexIntLE();
			entry.ViewportY0 = IndexIntLE();
			entry.ViewportX1 = IndexIntLE();
			entry.ViewportY1 = IndexIntLE();

			entry.CenterX = IndexIntLE();
			entry.CenterY = IndexIntLE();
			entry.CanvasOriginX = IndexIntLE();
			entry.CanvasOriginY = IndexIntLE();

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
			WriteInt(entry.ViewportX0);
			WriteInt(entry.ViewportY0);
			WriteInt(entry.ViewportX1);
			WriteInt(entry.ViewportY1);
			WriteInt(entry.CenterX);
			WriteInt(entry.CenterY);
			WriteInt(entry.CanvasOriginX);
			WriteInt(entry.CanvasOriginY);
		}

		return outStream.ToArray();
	}
}
