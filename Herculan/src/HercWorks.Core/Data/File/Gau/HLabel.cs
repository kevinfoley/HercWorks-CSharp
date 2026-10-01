using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>A widget kind <see cref="Io.Transform.Dbsim.GauFileTransformer"/> does not produce.</summary>
public class HLabel : WidgetBase {
	public HLabel() { }

	public HLabel(PixelPoint origin, PixelSize size) {
		Origin = origin;
		Size = size;
	}

	public override string ToString() {
		string name = HWidgetId != null ? HWidgetId.Name : GetType().Name;
		return $"{name} [origin=({Origin.X},{Origin.Y}), size=({Size.Width},{Size.Height})]";
	}
}
