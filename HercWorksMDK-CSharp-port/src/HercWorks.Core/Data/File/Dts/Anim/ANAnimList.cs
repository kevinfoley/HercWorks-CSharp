using HercWorks.Core.Data.Struct;
using System.Text;

namespace HercWorks.Core.Data.File.Dts.Anim;

/// <summary>Ported from org.hercworks.core.data.file.dts.anim.ANAnimList.</summary>
public class ANAnimList : TSObject {
	public TSObject[]? Sequences { get; set; }
	public ANAnimListTransition[]? Transitions { get; set; }
	public ANAnimListTransform[]? Transforms { get; set; }

	// FIXME (carried over from Java): confirm datatype
	public short[]? DefaultTransforms { get; set; }

	public Vec2Short[]? Relations { get; set; }

	public ANAnimList() : base(TSObjectHeader.ANAnimList) { }

	public ANAnimList(TSObjectHeader hdr) : base(hdr) { }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append(MetaInfoString(GetType().Name));

		str = JsonString(str);

		str.Append("\n");
		str.Append("}\n");

		return str.ToString();
	}

	public override StringBuilder JsonString(StringBuilder str) {
		str.Append("\"sequences\" : [\n");
		for (int s = 0; s < Sequences!.Length; s++) {
			str.Append(Sequences[s].ToString());
			if (s < Sequences.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("],\n");

		str.Append("\"transitions\" : [\n");
		for (int t = 0; t < Transitions!.Length; t++) {
			str.Append(Transitions[t].ToString());
			if (t < Transitions.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("],\n");

		str.Append("\"transforms\" : [\n");
		for (int trs = 0; trs < Transforms!.Length; trs++) {
			str.Append(Transforms[trs].ToString());
			if (trs < Transforms.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("],\n");

		str.Append("\"defTransforms\" : ").Append(DefaultTransforms == null ? "null" : "[" + string.Join(", ", DefaultTransforms) + "]").Append(",\n");

		str.Append("\"relations\" : [\n");
		for (int r = 0; r < Relations!.Length; r++) {
			str.Append(Relations[r].ToString());
			if (r < Relations.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("]");

		return str;
	}
}
