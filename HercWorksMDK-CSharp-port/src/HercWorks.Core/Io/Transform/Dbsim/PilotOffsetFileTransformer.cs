using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .OFS pilot-portrait-offset files (see
/// <see cref="PilotOffsetFile"/> for the format writeup). New: no Java equivalent, not a ported
/// format — reverse-engineered directly against real retail data.
/// </summary>
public class PilotOffsetFileTransformer : ThreeSpaceByteTransformer {
	private const int EntrySize = 12;

	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		int count = inputArray.Length / EntrySize;
		var entries = new PilotOffsetFile.Entry[count];

		for (int i = 0; i < count; i++) {
			entries[i] = new PilotOffsetFile.Entry {
				Index = IndexShortLE(),
				Unk1 = IndexShortLE(),
				OffsetA = IndexShortLE(),
				OffsetB = IndexShortLE(),
				OffsetC = IndexShortLE(),
				OffsetD = IndexShortLE(),
			};
		}

		return new PilotOffsetFile {
			RawBytes = inputArray,
			Ext = FileType.Ofs,
			Entries = entries
		};
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			return null;
		}

		var ofs = (PilotOffsetFile)source;
		using var outStream = new MemoryStream();

		void Write(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		foreach (var entry in ofs.Entries ?? Array.Empty<PilotOffsetFile.Entry>()) {
			Write(WriteShortLE(entry.Index));
			Write(WriteShortLE(entry.Unk1));
			Write(WriteShortLE(entry.OffsetA));
			Write(WriteShortLE(entry.OffsetB));
			Write(WriteShortLE(entry.OffsetC));
			Write(WriteShortLE(entry.OffsetD));
		}

		return outStream.ToArray();
	}
}
