using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .EDG scanline-clip files (see <see cref="EdgeClipFile"/> for
/// the format writeup). New: no Java equivalent, not a ported format — reverse-engineered directly
/// against both real retail files.
/// </summary>
public class EdgeClipFileTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
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
			RawBytes = inputArray,
			Ext = FileType.Edg,
			Rows = rows,
			Trailer = IndexShortLE(),
		};
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			return null;
		}

		var edg = (EdgeClipFile)source;
		using var outStream = new MemoryStream();

		void Write(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		foreach (var row in edg.Rows ?? Array.Empty<EdgeClipFile.Row>()) {
			Write(WriteShortLE(row.Left));
			Write(WriteShortLE(row.Right));
		}
		Write(WriteShortLE(edg.Trailer));

		return outStream.ToArray();
	}
}
