using HercWorks.Core.Data.Struct;
using System.Text;

namespace HercWorks.Core.Data.File.Dts;

/// <summary>Ported from org.hercworks.core.data.file.dts.TSBasePart.</summary>
public class TSBasePart : TSObject {
	public short Transform { get; set; }
	public short IdNumber { get; set; }
	public short Radius { get; set; }
	public Vec3Short? Center { get; set; }

	public TSBasePart() : base(TSObjectHeader.TSBasePart) { }

	public TSBasePart(TSObjectHeader hdr) : base(hdr) { }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append(MetaInfoString(GetType().Name));

		str = JsonString(str);

		str.Append("\n");
		str.Append("}\n");

		return str.ToString();
	}

	public override StringBuilder JsonString(StringBuilder str) {
		str.Append("\"transform\" : ").Append(Transform).Append(",\n");
		str.Append("\"uid\" : \"").Append(IdNumber).Append("\",\n");
		str.Append("\"radius\" : ").Append(Radius).Append(",\n");
		str.Append("\"center\" : ").Append(Center?.ToString());

		return str;
	}
}
