using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .RMP colour-ramp files — see <see cref="TerrainRampFile"/>
/// for the format and the RE behind it.
/// </summary>
public class TerrainRampFileTransformer : ByteTransformer<TerrainRampFile> {
	public override TerrainRampFile? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length < 8) {
			return null;
		}

		SetBytes(inputArray);

		var rmp = new TerrainRampFile {
			ShadeLevels = IndexIntLE(),
			DepthSlices = IndexIntLE(),
		};

		rmp.Rows = IndexSegment(inputArray.Length - Index);

		return rmp;
	}

	public override byte[]? Write(TerrainRampFile rmp) {
		if (rmp == null) {
			return null;
		}

		using var outStream = new MemoryStream();

		void Emit(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		Emit(WriteIntLE(rmp.ShadeLevels));
		Emit(WriteIntLE(rmp.DepthSlices));
		if (rmp.Rows != null) {
			Emit(rmp.Rows);
		}

		return outStream.ToArray();
	}
}
