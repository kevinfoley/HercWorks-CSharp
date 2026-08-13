using HercWorks.Core.Data.File.Dts.Part;
using System.Text;

namespace HercWorks.Core.Data.File.Dts.Bsp;

/// <summary>Ported from org.hercworks.core.data.file.dts.bsp.TSBSPPart.</summary>
public class TSBSPPart : TSPartList {
	public TSBSPPartNode[]? Nodes { get; set; }
	public short[]? Transforms { get; set; }

	public TSBSPPart() : base(TSObjectHeader.BSPPart) { }

	public TSBSPPart(TSObjectHeader hdr) : base(hdr) { }

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
		str.Append("\"nodes\" : [");
		for (int s = 0; s < Nodes!.Length; s++) {
			str.Append(Nodes[s].ToString());
			if (s < Nodes.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("]");

		return str;
	}
}
