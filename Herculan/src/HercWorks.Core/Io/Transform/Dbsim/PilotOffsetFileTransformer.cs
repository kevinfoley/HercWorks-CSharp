using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .OFS pilot-portrait-offset files (see
/// <see cref="PilotOffsetFile"/> for the format writeup). New: no Java equivalent, not a ported
/// format — reverse-engineered directly against real retail data.
/// </summary>
public class PilotOffsetFileTransformer : ByteTransformer<PilotOffsetFile> {
	private const int EntrySize = 12;

	public override PilotOffsetFile? Parse(byte[]? inputArray) {
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
			Entries = entries
		};
	}

	public override byte[]? Write(PilotOffsetFile ofs) {
		if (ofs == null) {
			return null;
		}

		using var outStream = new MemoryStream();

		void Emit(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		foreach (var entry in ofs.Entries ?? Array.Empty<PilotOffsetFile.Entry>()) {
			Emit(WriteShortLE(entry.Index));
			Emit(WriteShortLE(entry.Unk1));
			Emit(WriteShortLE(entry.OffsetA));
			Emit(WriteShortLE(entry.OffsetB));
			Emit(WriteShortLE(entry.OffsetC));
			Emit(WriteShortLE(entry.OffsetD));
		}

		return outStream.ToArray();
	}
}
