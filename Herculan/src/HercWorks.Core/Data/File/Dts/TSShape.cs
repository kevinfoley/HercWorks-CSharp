using HercWorks.Core.Data.File.Dts.Part;
using System.Text;

namespace HercWorks.Core.Data.File.Dts;

/// <summary>A shape root: a part list plus two per-shape <c>int16</c> arrays.</summary>
public class TSShape : TSPartList {
	/// <summary>
	/// Frame count per animation sequence (<c>shape+0x20</c>) — what a cell-animation counter is
	/// taken modulo. See docs/formats/dts-billboards.md, "TSCellAnimPart_Render (004767e4)".
	/// </summary>
	public short[]? SequenceList { get; set; }

	/// <summary>Meaning not established; read and written back verbatim.</summary>
	public short[]? TransformList { get; set; }

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
		str.Append("\"transforms\" : ").Append(ArrayToString(TransformList));

		return str;
	}

	private static string ArrayToString(short[]? arr) =>
		arr == null ? "null" : "[" + string.Join(", ", arr) + "]";
}
