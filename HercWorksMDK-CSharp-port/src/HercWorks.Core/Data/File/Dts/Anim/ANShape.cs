using System.Text;

namespace HercWorks.Core.Data.File.Dts.Anim;

/// <summary>Ported from org.hercworks.core.data.file.dts.anim.ANShape.</summary>
public class ANShape : TSShape {
	public ANAnimList? AnimationList { get; set; }

	public ANShape() : base(TSObjectHeader.ANShape) { }

	public ANShape(TSObjectHeader hdr) : base(hdr) { }

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
		str.Append("\"animations\" : ").Append(AnimationList);

		return str;
	}
}
