using System.Text;

namespace HercWorks.Core.Data.File.Dts.Anim;

/// <summary>Ported from org.hercworks.core.data.file.dts.anim.ANSequence.</summary>
public class ANSequence : TSObject {
	public short Tick { get; set; }
	public short Priority { get; set; }
	public short GroundMovement { get; set; }

	public ANSequenceFrame[]? Frames { get; set; }
	public short[]? PartIds { get; set; }
	public short[]? TransformIndices { get; set; }

	public ANSequence() : base(TSObjectHeader.ANSequence) { }

	public ANSequence(TSObjectHeader hdr) : base(hdr) { }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append(MetaInfoString(GetType().Name));

		str = JsonString(str);

		str.Append("\n");
		str.Append("}\n");

		return str.ToString();
	}

	public override StringBuilder JsonString(StringBuilder str) {
		str.Append("\"tick\" : ").Append(Tick).Append(",\n");
		str.Append("\"priority\" : ").Append(Priority).Append(",\n");
		str.Append("\"groundMove\" : ").Append(GroundMovement).Append(",\n");

		str.Append("\"frames\" : [\n");
		for (int s = 0; s < Frames!.Length; s++) {
			str.Append(Frames[s].ToString());
			if (s < Frames.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("],\n");

		str.Append("\"partIds\" : ").Append(PartIds == null ? "null" : "[" + string.Join(", ", PartIds) + "]").Append(",\n");
		str.Append("\"transformIndices\" : ").Append(TransformIndices == null ? "null" : "[" + string.Join(", ", TransformIndices) + "]");

		return str;
	}
}
