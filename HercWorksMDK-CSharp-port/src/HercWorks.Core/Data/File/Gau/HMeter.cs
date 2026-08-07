using System.Drawing;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// Observed: unlabeled, horizontal, will generate meter ticks from origin to maximum.
/// Ported from org.hercworks.core.data.file.gau.HMeter.
///
/// NOTE: the original Java constructor calls setOrigin(getOrigin()) instead of
/// setOrigin(origin) — it assigns Origin to its own (null/default) current value instead of the
/// constructor parameter. Looks like a bug; ported literally (the "origin" parameter is
/// effectively unused, matching the original).
/// </summary>
public class HMeter : WidgetBase {
	public HMeter() { }

	public HMeter(Point origin, Point extent) {
		Origin = Origin;
		Size = new Size(extent.X - origin.X, extent.Y - origin.Y);
	}
}
