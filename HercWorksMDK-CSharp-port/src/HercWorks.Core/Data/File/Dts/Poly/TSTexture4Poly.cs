using System.Text;

namespace HercWorks.Core.Data.File.Dts.Poly;

/// <summary>Ported from org.hercworks.core.data.file.dts.poly.TSTexture4Poly.</summary>
public class TSTexture4Poly : TSSolidPoly {
	public TSTexture4Poly() : base(TSObjectHeader.TSTexture4Poly) { }

	public TSTexture4Poly(TSObjectHeader hdr) : base(hdr) { }

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
