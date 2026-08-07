using System.Drawing;
using System.Text;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>Ported from org.hercworks.core.data.file.gau.HPanel.</summary>
public class HPanel : WidgetBase {
	public Point PanelOffset { get; set; }

	public HPanel() { }

	public HPanel(Point org, Size size, Point ofs) {
		Origin = org;
		Size = size;
		PanelOffset = ofs;
	}

	public override string ToString() {
		var sb = new StringBuilder();

		sb.Append(HWidgetId != null ? HWidgetId.Name : GetType().Name);
		sb.Append(" ");

		sb.Append($"[origin=({Origin.X},{Origin.Y}), size=({Size.Width},{Size.Height}), panelOffset=({PanelOffset.X},{PanelOffset.Y})");

		if (Components is { Length: > 0 }) {
			sb.Append(", \n component={ \n");
			foreach (var l in Components) {
				sb.Append(l).Append('\n');
			}
			sb.Append("}");
		}
		sb.Append("]");

		return sb.ToString();
	}
}
