using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .OFS pilot-portrait-offset files (see
/// <see cref="PilotOffsetFile"/> for the format writeup). New: no Java equivalent, not a ported
/// format — read from DBSIM's own loader.
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
				Index = IndexIntLE(),
				X = IndexIntLE(),
				Y = IndexIntLE(),
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
			Emit(WriteIntLE(entry.Index));
			Emit(WriteIntLE(entry.X));
			Emit(WriteIntLE(entry.Y));
		}

		return outStream.ToArray();
	}
}
