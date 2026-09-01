using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// Observed: unlabeled, horizontal, will generate meter ticks from origin to maximum.
/// Ported from org.hercworks.core.data.file.gau.HMeter.
/// </summary>
public class HMeter : WidgetBase {
	public HMeter() { }

	public HMeter(PixelPoint origin, PixelPoint extent) {
		Origin = origin;
		Size = new PixelSize(extent.X - origin.X, extent.Y - origin.Y);
	}
}
