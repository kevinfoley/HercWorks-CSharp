using System.Text;

namespace HercWorks.Core.Data.File.Dts.Part;

/// <summary>Ported from org.hercworks.core.data.file.dts.part.TSPartList.</summary>
public class TSPartList : TSBasePart {
	public TSObject[]? Parts { get; set; }

	public TSPartList() : base(TSObjectHeader.TSPartList) { }

	public TSPartList(TSObjectHeader hdr) : base(hdr) { }

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
		str.Append("\"parts\" : [\n");
		for (int s = 0; s < Parts!.Length; s++) {
			str.Append(Parts[s].ToString());
			if (s < Parts.Length - 1) {
				str.Append(",");
			}
			str.Append("\n");
		}
		str.Append("]");

		return str;
	}
}
