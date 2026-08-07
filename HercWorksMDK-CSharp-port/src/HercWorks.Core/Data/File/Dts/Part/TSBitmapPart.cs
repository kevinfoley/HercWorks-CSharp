using System.Text;

namespace HercWorks.Core.Data.File.Dts.Part;

/// <summary>
/// NOTES: ThreeSpace2 engine somehow already knows the target DBA to bind these to; it also
/// knows to generate a basic textured quad which is the destination. TODO (carried over from
/// Java): make sure this exports to a basic quad, but unsure how to find matching material.
/// Ported from org.hercworks.core.data.file.dts.part.TSBitmapPart.
/// </summary>
public class TSBitmapPart : TSBasePart {
	public short BmpTag { get; set; }
	public byte OfsX { get; set; }
	public byte OfsY { get; set; }

	public TSBitmapPart() : base(TSObjectHeader.TSBitmapPart) { }

	public TSBitmapPart(TSObjectHeader hdr) : base(hdr) { }

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
		str.Append("\"bmp_tag\" : ").Append(BmpTag).Append(",\n");
		str.Append("\"ofs_x\" : ").Append(OfsX).Append(",\n");
		str.Append("\"ofs_y\" : ").Append(OfsY);

		return str;
	}
}
