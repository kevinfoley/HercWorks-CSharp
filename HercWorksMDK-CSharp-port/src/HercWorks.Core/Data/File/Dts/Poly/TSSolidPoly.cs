using System.Text;

namespace HercWorks.Core.Data.File.Dts.Poly;

/// <summary>Ported from org.hercworks.core.data.file.dts.poly.TSSolidPoly.</summary>
public class TSSolidPoly : TSPoly {
	public short ColorIndexId { get; set; }

	public TSSolidPoly() : base(TSObjectHeader.TSSolidPoly) { }

	public TSSolidPoly(TSObjectHeader hdr) : base(hdr) { }

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
		str.Append("\"colorIndexId\" : ").Append(ColorIndexId);

		return str;
	}
}
