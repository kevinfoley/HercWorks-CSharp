using System.Drawing;
using System.Text;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>Ported from org.hercworks.core.data.file.gau.HWeaponPanel.</summary>
public class HWeaponPanel : WidgetBase {
	public int ActiveTotal { get; set; }

	public HWeaponPanel() { }

	public HWeaponPanel(Point origin, Size size) {
		Origin = origin;
		Size = size;
		Components = new WidgetBase[10];
	}

	public HWeaponPanel(Point origin, Size size, int activeTotal) {
		Origin = origin;
		Size = size;
		ActiveTotal = activeTotal;
		Components = new WidgetBase[10];
	}

	public override string ToString() {
		var sb = new StringBuilder();

		sb.Append(HWidgetId != null ? HWidgetId.Name : GetType().Name);
		sb.Append(" ");

		sb.Append($"[activeTotal={ActiveTotal}");

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
