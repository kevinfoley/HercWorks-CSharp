using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Ported from org.hercworks.core.io.transform.dbsim.WeaponPDGTransformer.
/// FIXED — see KNOWN_ISSUES.md history: BytesToObject reset Index but never called SetBytes, so
/// Bytes was left null/stale on a fresh instance.
/// </summary>
public class WeaponPDGTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		Index = 0;

		if (inputArray == null || inputArray.Length <= 0) {
			// TODO - null input
			return null;
		}

		SetBytes(inputArray);

		var data = new WeaponPaperDiagram {
			FileName = "WEAPONS",
			Ext = FileType.Pdg,
			Dir = FileType.Pdg,
			RawBytes = inputArray
		};

		var entries = new WeaponPaperDiagram.Entry[IndexIntLE()];

		for (int i = 0; i < entries.Length; i++) {
			var entry = data.NewEntry();
			entry.X = IndexIntLE();
			entry.Y = IndexIntLE();

			entries[i] = entry;
		}
		data.Entries = entries;

		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var data = (WeaponPaperDiagram)source!;

		using var outStream = new MemoryStream();

		void WriteInt(int i) {
			var b = WriteIntLE(i);
			outStream.Write(b, 0, b.Length);
		}

		WriteInt(data.Entries!.Length);

		for (int i = 0; i < data.Entries.Length; i++) {
			WriteInt(data.Entries[i].X);
			WriteInt(data.Entries[i].Y);
		}

		return outStream.ToArray();
	}
}
