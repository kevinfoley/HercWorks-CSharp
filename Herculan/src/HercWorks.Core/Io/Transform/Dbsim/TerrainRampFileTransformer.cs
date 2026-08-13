using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .RMP terrain files (see <see cref="TerrainRampFile"/> for
/// the format writeup and its honesty caveat — the grid shape is an evidence-backed hypothesis,
/// not a confirmed fact).
/// </summary>
public class TerrainRampFileTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		var rmp = new TerrainRampFile {
			RawBytes = inputArray,
			Ext = FileType.Rmp,

			Unk0_val = IndexIntLE(),
			Unk4_val = IndexIntLE(),
		};

		rmp.Body = IndexSegment(inputArray.Length - Index);

		return rmp;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			return null;
		}

		var rmp = (TerrainRampFile)source;
		using var outStream = new MemoryStream();

		void Write(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		Write(WriteIntLE(rmp.Unk0_val));
		Write(WriteIntLE(rmp.Unk4_val));
		if (rmp.Body != null) {
			Write(rmp.Body);
		}

		return outStream.ToArray();
	}
}
