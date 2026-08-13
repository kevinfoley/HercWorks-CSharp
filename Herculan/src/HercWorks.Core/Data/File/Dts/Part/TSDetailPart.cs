using System.Text;

namespace HercWorks.Core.Data.File.Dts.Part;

/// <summary>Ported from org.hercworks.core.data.file.dts.part.TSDetailPart.</summary>
public class TSDetailPart : TSPartList {
	public short[]? Details { get; set; }

	public TSDetailPart() : base(TSObjectHeader.TSDetailPart) { }

	public TSDetailPart(TSObjectHeader hdr) : base(hdr) { }

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
		str.Append("\"details\" : ").Append(Details == null ? "null" : "[" + string.Join(", ", Details) + "]");

		return str;
	}
}
