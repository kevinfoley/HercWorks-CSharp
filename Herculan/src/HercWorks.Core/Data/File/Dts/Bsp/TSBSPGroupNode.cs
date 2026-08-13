using System.Text;

namespace HercWorks.Core.Data.File.Dts.Bsp;

/// <summary>Ported from org.hercworks.core.data.file.dts.bsp.TSBSPGroupNode.</summary>
public class TSBSPGroupNode {
	public int Index { get; set; }
	public int Len { get; set; }

	public short Coeff { get; set; }
	public short Poly { get; set; }
	public short Front { get; set; }
	public short Back { get; set; }

	public TSBSPGroupNode() { }

	public TSBSPGroupNode(short coeff, short poly, short front, short back) {
		Coeff = coeff;
		Poly = poly;
		Front = front;
		Back = back;
	}

	public override string ToString() {
		var str = new StringBuilder();

		str.Append("{\n");
		str.Append("\"class\" : ").Append("TSBSPGroupNode").Append(",\n");
		str.Append("\"index\" : ").Append(Index).Append(",\n");
		str.Append("\"coeff\" : ").Append(Coeff).Append(",\n");
		str.Append("\"poly\" : ").Append(Poly).Append(",\n");
		str.Append("\"front\" : ").Append(Front).Append(",\n");
		str.Append("\"back\" : ").Append(Back).Append("\n");
		str.Append("}\n");

		return str.ToString();
	}
}
