using System.Text;

namespace HercWorks.Core.Data.File.Dts.Poly;

/// <summary>Ported from org.hercworks.core.data.file.dts.poly.TSGouraudPoly.</summary>
public class TSGouraudPoly : TSSolidPoly {
	public short NormalList { get; set; }

	public TSGouraudPoly() : base(TSObjectHeader.TSGouraudPoly) { }

	public TSGouraudPoly(TSObjectHeader hdr) : base(hdr) { }

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
		str.Append("\"color\" : ").Append(ColorIndexId).Append(",\n");
		str.Append("\"normalList\" : ").Append(NormalList);

		return str;
	}
}
