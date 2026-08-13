using HercWorks.Core.Data.Struct;
using System.Text;

namespace HercWorks.Core.Data.File.Dts.Bsp;

/// <summary>Ported from org.hercworks.core.data.file.dts.bsp.TSBSPPartNode.</summary>
public class TSBSPPartNode {
	public int Index { get; set; }
	public int ByteLen { get; set; }
	public byte[]? Data { get; set; }

	public Vec3Short? Normal { get; set; }

	public int Coeff { get; set; }

	public short Front { get; set; }
	public short Back { get; set; }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append("{ \n\"class\" : \"TSBSPPartNode\",\n");
		str.Append("\"index\" : ").Append(Index).Append(",\n");
		str.Append("\"byteLen\" : ").Append(ByteLen).Append(",\n");
		str.Append("\"data\" : ").Append(Data == null ? "null" : "[" + string.Join(", ", Data) + "]").Append(",\n");
		str.Append("\"normal\" : ").Append(Normal).Append(",\n");
		str.Append("\"coeff\" : ").Append(Coeff).Append(",\n");
		str.Append("\"front\" : ").Append(Front).Append(",\n");
		str.Append("\"back\" : ").Append(Back).Append("\n");
		str.Append("}\n");

		return str.ToString();
	}
}
