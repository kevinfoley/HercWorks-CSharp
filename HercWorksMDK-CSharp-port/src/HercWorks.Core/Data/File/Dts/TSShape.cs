using HercWorks.Core.Data.File.Dts.Part;
using System.Text;

namespace HercWorks.Core.Data.File.Dts;

/// <summary>Ported from org.hercworks.core.data.file.dts.TSShape.</summary>
public class TSShape : TSPartList {
	public short[]? SequenceList { get; set; }
	public short[]? TransformList { get; set; }

	// FIXME (carried over from Java): not sure if this is needed
	// private TSObject[] extraParts;

	public TSShape() : base(TSObjectHeader.TSShape) { }

	public TSShape(TSObjectHeader hdr) : base(hdr) { }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append(MetaInfoString(GetType().Name));

		str = JsonString(str);
		str.Append("\n");
		str.Append("}\n");

		return str.ToString();
	}

	public override StringBuilder JsonString(StringBuilder str) {
		str = base.JsonString(str);

		str.Append(",\n");
		str.Append("\"sequences\" : ").Append(ArrayToString(SequenceList)).Append(",\n");
		// NOTE: the original Java prints getSequenceList() again here instead of
		// getTransformList() — almost certainly a copy/paste bug. Ported literally.
		str.Append("\"transforms\" : ").Append(ArrayToString(SequenceList));

		return str;
	}

	private static string ArrayToString(short[]? arr) =>
		arr == null ? "null" : "[" + string.Join(", ", arr) + "]";
}
