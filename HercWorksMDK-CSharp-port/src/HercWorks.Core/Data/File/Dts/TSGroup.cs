using HercWorks.Core.Data.Struct;
using System.Text;

namespace HercWorks.Core.Data.File.Dts;

/// <summary>Ported from org.hercworks.core.data.file.dts.TSGroup.</summary>
public class TSGroup : TSBasePart {
	public short[]? Indexes { get; set; }
	public Vec3Short[]? Points { get; set; }
	public TSSurfaceEntry[]? Surfaces { get; set; }
	public TSObject[]? Polys { get; set; }

	public TSGroup() : base(TSObjectHeader.TSGroup) { }

	public TSGroup(TSObjectHeader hdr) : base(hdr) { }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append(MetaInfoString(GetType().Name + "_" + ListIndex));

		str = JsonString(str);
		str.Append("\n");
		str.Append("}\n");

		return str.ToString();
	}

	public override StringBuilder JsonString(StringBuilder str) {
		str = base.JsonString(str);

		str.Append(",\n");
		str.Append("\"indexes\" : ").Append(Indexes == null ? "[]" : "[" + string.Join(", ", Indexes) + "]").Append(",\n");
		str.Append("\"points\" : [\n");
		for (int s = 0; s < Points!.Length; s++) {
			str.Append(Points[s].ToString());
			if (s < Points.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("],\n");

		str.Append("\"surfaces\" : [\n");
		for (int c = 0; c < Surfaces!.Length; c++) {
			str.Append(Surfaces[c].ToString());

			if (c < Surfaces.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("],\n");

		str.Append("\"polys\" : [\n");
		for (int s = 0; s < Polys!.Length; s++) {
			str.Append(Polys[s].ToString());
			if (s < Polys.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("]");

		return str;
	}
}
