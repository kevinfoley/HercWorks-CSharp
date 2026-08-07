using System.Drawing;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>Ported from org.hercworks.core.data.file.gau.HButtonBasic.</summary>
public class HButtonBasic : WidgetBase {
	public Point LabelOfs { get; set; }

	public HButtonBasic() { }

	public HButtonBasic(Point origin, Point labelOfs) {
		Origin = origin;
		LabelOfs = labelOfs;
	}

	public override string ToString() {
		string name = HWidgetId != null ? HWidgetId.Name : GetType().Name;
		return $"{name} [origin=({Origin.X},{Origin.Y}), labelOfs=({LabelOfs.X},{LabelOfs.Y})]";
	}
}
