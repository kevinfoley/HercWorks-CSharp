using System.Drawing;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>Ported from org.hercworks.core.data.file.gau.HWeaponPanelItem.</summary>
public class HWeaponPanelItem : WidgetBase {
	public HWeaponPanelItem() { }

	public HWeaponPanelItem(Point org, Size size) {
		Origin = org;
		Size = size;
	}

	public override string ToString() {
		string name = HWidgetId != null ? HWidgetId.Name : GetType().Name;
		return $"{name} [origin=({Origin.X},{Origin.Y}), size=({Size.Width},{Size.Height})]";
	}
}
