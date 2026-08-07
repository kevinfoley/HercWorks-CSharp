using System.Text;

namespace HercWorks.Core.Data.File.Dts;

/// <summary>
/// UTILITY — used in TSGroup. 'Colors' here really are 4 config options expressed as flags. The
/// first int is the 'surface' color/shade, but for TSTexture4Poly this is read as a DBA frame
/// number. The second int is the 'outline'/'edge' color, but only for TSShadedPoly. The 'flags'
/// for each entry are also unknown — TSTexture4Poly cannot have any flags, while TSShadedPoly has
/// 1024 for front and 5120 for back.
/// Ported from org.hercworks.core.data.file.dts.TSSurfaceEntry.
/// </summary>
public class TSSurfaceEntry {
	public short FrontColor { get; set; }
	public short FrontFlag { get; set; }

	public short FrontLineColor { get; set; }
	public short FrontLineFlag { get; set; }

	public short BackColor { get; set; }
	public short BackColorFlag { get; set; }

	public short BackLineColor { get; set; }
	public short BackLineFlag { get; set; }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append("{");

		str.Append("\"frontColor\" : ").Append(FrontColor).Append(",\n");
		str.Append("\"frontFlag\" : ").Append(FrontFlag).Append(",\n");
		str.Append("\"frontEdgeColor\" : ").Append(FrontLineColor).Append(",\n");
		str.Append("\"frontEdgeFlag\" : ").Append(FrontLineFlag).Append(",\n");
		str.Append("\"backColor\" : ").Append(BackColor).Append(",\n");
		str.Append("\"backFlag\" : ").Append(BackColorFlag).Append(",\n");
		str.Append("\"backEdgeColor\" : ").Append(BackLineColor).Append(",\n");
		str.Append("\"backEdgeFlag\" : ").Append(BackLineFlag).Append("\n");

		str.Append("}\n");

		return str.ToString();
	}
}
