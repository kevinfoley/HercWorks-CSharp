using HercWorks.Core.Data.File.Dts;
using HercWorks.Vol;
using System.Numerics;
using System.Text;

namespace HercWorks.Core.Data.File.Dyn;

/// <summary>
/// FILE - DTS - Dynamix ThreeSpace — primary 3D model format for the ES2 engine.
/// Ported from org.hercworks.core.data.file.dyn.DynamixThreeSpaceModel. Apache Commons Math's
/// Vector3D maps to System.Numerics.Vector3 here.
/// </summary>
public class DynamixThreeSpaceModel : DataFile {
	public List<TSObject>? Meshes { get; set; }

	public Vector3 Center { get; set; }

	// XXX (carried over from Java): these are set by game engine code, and thus must be set by
	// any code that would like to link a texture to the DTS model.
	public string? TextureName { get; set; }
	public DynamixBitmapArray? TextureDBA { get; set; }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append("{\"file\" : \"").Append(OriginNameNoExt()).Append("\",\n");
		str.Append("\"meshes\" : [\n");
		for (int s = 0; s < Meshes!.Count; s++) {
			str.Append(Meshes[s].ToString());
			if (s < Meshes.Count - 1) {
				str.Append(",\n");
			}
		}
		str.Append("]}");

		return str.ToString();
	}
}
