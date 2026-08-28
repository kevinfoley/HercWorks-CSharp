using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Ported from org.hercworks.core.io.transform.dbsim.BeamDatFileTransformer.</summary>
public class BeamDatFileTransformer : ByteTransformer<BeamData> {
	public override BeamData? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var data = new BeamData(IndexShortLE());

		for (int b = 0; b < data.Data!.Length; b++) {
			var beam = data.NewEntry(IndexShortLE(), IndexShortLE(), IndexShortLE());
			data.Data[b] = beam;
		}

		return data;
	}

	public override byte[]? Write(BeamData data) {

		using var outStream = new MemoryStream();

		var totalBytes = WriteShortLE((short)data.Data!.Length);
		outStream.Write(totalBytes, 0, totalBytes.Length);

		foreach (var beam in data.Data) {
			var w = WriteShortLE(beam.Width);
			outStream.Write(w, 0, w.Length);

			var c = WriteShortLE(beam.ColorId);
			outStream.Write(c, 0, c.Length);

			var f = WriteShortLE(beam.DBAFrameNum);
			outStream.Write(f, 0, f.Length);
		}

		return outStream.ToArray();
	}
}
