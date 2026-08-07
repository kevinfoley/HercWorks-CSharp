using System.Drawing;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>Ported from org.hercworks.core.data.file.gau.WidgetBase.</summary>
public abstract class WidgetBase {
	public HWidgetId? HWidgetId { get; set; }
	public Point Origin { get; set; }
	public Size Size { get; set; }
	public WidgetBase[]? Components { get; set; }
}
