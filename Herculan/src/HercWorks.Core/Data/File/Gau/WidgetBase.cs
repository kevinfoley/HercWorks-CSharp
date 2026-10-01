using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>A <c>.GAU</c> widget: a rect, held as an origin and a size.</summary>
public abstract class WidgetBase {
	public HWidgetId? HWidgetId { get; set; }
	public PixelPoint Origin { get; set; }
	public PixelSize Size { get; set; }
	public WidgetBase[]? Components { get; set; }
}
