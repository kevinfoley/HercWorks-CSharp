using HercWorks.Core.Data.Struct;
using System.Text;

namespace HercWorks.Core.Data.File.Dts.Anim;

/// <summary>Ported from org.hercworks.core.data.file.dts.anim.ANAnimListTransform.</summary>
public class ANAnimListTransform {
	public Vec3Short? Rotation { get; set; }
	public Vec3Short? Translation { get; set; }
	public int Index { get; set; }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append("{ \"class\" : \"").Append(GetType().Name).Append("\",\n");
		str.Append("\"index\" : ").Append(Index).Append(",\n");
		str.Append("\"rotation\" : ").Append(Rotation).Append(",\n");
		str.Append("\"translation\" : ").Append(Translation).Append("\n");
		str.Append("}\n");

		return str.ToString();
	}
}
