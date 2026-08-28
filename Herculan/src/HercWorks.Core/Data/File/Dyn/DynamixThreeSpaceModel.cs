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
public class DynamixThreeSpaceModel {
	/// <summary>
	/// Source file name, used only by <see cref="ToString"/>'s JSON dump. No parse path sets it
	/// today — the transformer never named the model — so the dump's "file" field comes out empty,
	/// exactly as it did when this was inherited from DataFile. Kept so a caller that does know
	/// the name can still supply it.
	/// </summary>
	public string? FileName { get; set; }

	/// <summary><see cref="FileName"/> with any extension stripped; empty when unset.</summary>
	private string NameNoExt() =>
		FileName == null ? string.Empty
			: FileName.LastIndexOf('.') != -1 ? FileName[..FileName.LastIndexOf('.')]
			: FileName;

	public List<TSObject>? Meshes { get; set; }

	public Vector3 Center { get; set; }

	// XXX (carried over from Java): these are set by game engine code, and thus must be set by
	// any code that would like to link a texture to the DTS model.
	public string? TextureName { get; set; }
	public DynamixBitmapArray? TextureDBA { get; set; }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append("{\"file\" : \"").Append(NameNoExt()).Append("\",\n");
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
