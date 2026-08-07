using System.Text;

namespace HercWorks.Core.Data.File.Dts.Poly;

/// <summary>
/// An untextured poly, assigned an indexed palette color based on in-game light calculations.
/// Ported from org.hercworks.core.data.file.dts.poly.TSShadedPoly.
/// </summary>
public class TSShadedPoly : TSSolidPoly {
	public TSShadedPoly() : base(TSObjectHeader.TSShadedPoly) { }

	public TSShadedPoly(TSObjectHeader hdr) : base(hdr) { }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append(MetaInfoString(GetType().Name));

		str = JsonString(str);
		str.Append("\n");

		str.Append("}\n");

		return str.ToString();
	}

	public override StringBuilder JsonString(StringBuilder str) {
		return base.JsonString(str);
	}
}
