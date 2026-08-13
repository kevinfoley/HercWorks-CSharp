using System.Text;

namespace HercWorks.Core.Data.File.Dts.Anim;

/// <summary>Ported from org.hercworks.core.data.file.dts.anim.ANSequenceFrame.</summary>
public class ANSequenceFrame {
	public int Index { get; set; }
	public byte[]? Data { get; set; }
	public int ByteLen { get; set; }

	public short Tick { get; set; }
	public short NumTransitions { get; set; }
	public short FirstTransition { get; set; }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append("{ \"class\" : \"").Append(GetType().Name).Append("\",\n");
		str.Append("\"index\" : ").Append(Index).Append(",\n");
		str.Append("\"len\" : ").Append(ByteLen).Append(",\n");
		str.Append("\"data\" : ").Append(Data == null ? "null" : "[" + string.Join(", ", Data) + "]").Append(",\n");
		str.Append("\"tick\" :").Append(Tick).Append(",\n");
		str.Append("\"numTransitions\" :").Append(NumTransitions).Append(",\n");
		str.Append("\"firstTransition\" :").Append(FirstTransition).Append("\n");
		str.Append("}\n");

		return str.ToString();
	}
}
