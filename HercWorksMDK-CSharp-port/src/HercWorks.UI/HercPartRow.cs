namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape shared by HercBayEditorForm's Externals/Internals/Hardpoints grids —
/// each is just a fixed-identity label paired with an editable Health value (ShellHercPart).
/// </summary>
public class HercPartRow {
	public short Id { get; set; }
	public string Label { get; set; } = string.Empty;
	public short Health { get; set; }
}
