using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>Ported from org.hercworks.core.data.file.gau.HButtonBasic.</summary>
public class HButtonBasic : WidgetBase {
	public PixelPoint LabelOfs { get; set; }

	public HButtonBasic() { }

	public HButtonBasic(PixelPoint origin, PixelPoint labelOfs) {
		Origin = origin;
		LabelOfs = labelOfs;
	}

	public override string ToString() {
		string name = HWidgetId != null ? HWidgetId.Name : GetType().Name;
		return $"{name} [origin=({Origin.X},{Origin.Y}), labelOfs=({LabelOfs.X},{LabelOfs.Y})]";
	}
}
