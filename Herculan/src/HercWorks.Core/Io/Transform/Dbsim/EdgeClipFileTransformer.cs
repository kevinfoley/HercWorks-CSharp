using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .EDG scanline-clip files (see <see cref="EdgeClipFile"/> for
/// the format writeup). New: no Java equivalent, not a ported format — reverse-engineered directly
/// against both real retail files.
/// </summary>
public class EdgeClipFileTransformer : ByteTransformer<EdgeClipFile> {
	public override EdgeClipFile? Parse(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		int rowCount = (inputArray.Length - 2) / 4;
		var rows = new EdgeClipFile.Row[rowCount];

		for (int i = 0; i < rowCount; i++) {
			rows[i] = new EdgeClipFile.Row {
				Left = IndexShortLE(),
				Right = IndexShortLE(),
			};
		}

		return new EdgeClipFile {
			Rows = rows,
			Trailer = IndexShortLE(),
		};
	}

	public override byte[]? Write(EdgeClipFile edg) {
		using var outStream = new MemoryStream();

		void Emit(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		foreach (var row in edg.Rows ?? Array.Empty<EdgeClipFile.Row>()) {
			Emit(WriteShortLE(row.Left));
			Emit(WriteShortLE(row.Right));
		}
		Emit(WriteShortLE(edg.Trailer));

		return outStream.ToArray();
	}
}
