using System.Text;

namespace HercWorks.Core.Data.File.Dts.Part;

/// <summary>
/// FIXME (carried over from Java): testing in /BULLETS.DTS, the tail bytes look very much like a
/// TSShape segment as the direct inherit vs a TSPartList — TSShape inherits directly from
/// TSPartList, so other sources aren't too far off. Will test other files.
/// Ported from org.hercworks.core.data.file.dts.part.TSCellAnimPart.
/// </summary>
public class TSCellAnimPart : TSPartList {
	public short AnimSequence { get; set; }

	public TSCellAnimPart() : base(TSObjectHeader.TSCellAnimPart) { }

	public TSCellAnimPart(TSObjectHeader hdr) : base(hdr) { }

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
		str.Append("\"animSequence\" : ").Append(AnimSequence);

		return str;
	}
}
