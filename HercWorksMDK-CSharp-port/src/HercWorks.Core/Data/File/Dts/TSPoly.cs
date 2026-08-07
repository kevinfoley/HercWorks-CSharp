using System.Text;

namespace HercWorks.Core.Data.File.Dts;

/// <summary>Ported from org.hercworks.core.data.file.dts.TSPoly.</summary>
public class TSPoly : TSObject {
	public short Normal { get; set; }
	public short Center { get; set; }
	public short VertexCount { get; set; }
	public short VertexList { get; set; }

	public TSPoly() : base(TSObjectHeader.TSPoly) { }

	public TSPoly(TSObjectHeader hdr) : base(hdr) { }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append(MetaInfoString(GetType().Name));
		str = JsonString(str);

		str.Append("\n");
		str.Append("}\n");

		return str.ToString();
	}

	public override StringBuilder JsonString(StringBuilder str) {
		str.Append("\"normal\" : ").Append(Normal).Append(",\n");
		str.Append("\"center\" : ").Append(Center).Append(",\n");
		str.Append("\"vertexCount\" : ").Append(VertexCount).Append(",\n");
		str.Append("\"vertexList\" : ").Append(VertexList).Append("\n");

		return str;
	}
}
