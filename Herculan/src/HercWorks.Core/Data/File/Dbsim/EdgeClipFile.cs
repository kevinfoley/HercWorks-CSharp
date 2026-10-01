namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/EDG/HDDCLIP.EDG, /SIMVOL0/EDG/MFDCLIP.EDG — per-scanline horizontal clip range
/// for the HDD (Heads-Down Display) and MFD panel's non-rectangular screen shape. No header — the
/// file is a flat array of 4-byte (Left:INT16, Right:INT16) rows, one per display scanline, plus a
/// single trailing INT16 (0 in both retail files) after the last row. Rows widen from a narrow span
/// at row 0 to the full display width and narrow again at the bottom — the panel's rounded corners.
/// </summary>
public class EdgeClipFile {
	public Row[]? Rows { get; set; }

	/// <summary>Trailing INT16 after the last row — 0 in both retail files; meaning not established.</summary>
	public short Trailer { get; set; }

	public class Row {
		public short Left { get; set; }
		public short Right { get; set; }
	}
}
