using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .RMP colour-ramp files — see <see cref="TerrainRampFile"/>
/// for the format and the RE behind it.
/// </summary>
public class TerrainRampFileTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length < 8) {
			return null;
		}

		SetBytes(inputArray);

		var rmp = new TerrainRampFile {
			RawBytes = inputArray,
			Ext = FileType.Rmp,

			ShadeLevels = IndexIntLE(),
			DepthSlices = IndexIntLE(),
		};

		rmp.Rows = IndexSegment(inputArray.Length - Index);

		return rmp;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source is not TerrainRampFile rmp) {
			return null;
		}

		using var outStream = new MemoryStream();

		void Write(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		Write(WriteIntLE(rmp.ShadeLevels));
		Write(WriteIntLE(rmp.DepthSlices));
		if (rmp.Rows != null) {
			Write(rmp.Rows);
		}

		return outStream.ToArray();
	}
}
