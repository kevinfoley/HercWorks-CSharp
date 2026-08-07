using System.Text;

namespace HercWorks.Core.Data.File.Dts.Anim;

/// <summary>Ported from org.hercworks.core.data.file.dts.anim.ANAnimListTransition.</summary>
public class ANAnimListTransition {
	public short Tick { get; set; }
	public short DestSequence { get; set; }
	public short DestFrame { get; set; }
	public short GroundMovement { get; set; }
	public int Index { get; set; }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append("{ \"class\" : \"").Append(GetType().Name).Append("\",\n");
		str.Append("\"index\" : ").Append(Index).Append(",\n");
		str.Append("\"tick\" :").Append(Tick).Append(",\n");
		str.Append("\"destSequence\" :").Append(DestSequence).Append(",\n");
		str.Append("\"destFrame\" :").Append(DestFrame).Append(",\n");
		str.Append("\"groundMovement\" :").Append(GroundMovement).Append("\n");
		str.Append("}\n");

		return str.ToString();
	}
}
