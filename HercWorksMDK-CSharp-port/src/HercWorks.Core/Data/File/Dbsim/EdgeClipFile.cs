using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/EDG/HDDCLIP.EDG, /SIMVOL0/EDG/MFDCLIP.EDG — per-scanline horizontal clip range
/// for the HDD (Heads-Down Display) and MFD panel's non-rectangular screen shape. No header — the
/// file is a flat array of 4-byte (Left:INT16, Right:INT16) rows, one per display scanline, plus a
/// single trailing INT16 (always 0 in both real files) after the last row.
///
/// New (no Java equivalent — not a ported format). Confirmed against both real files: rows taper
/// from a narrow span at row 0 (e.g. (0,1)) up to a wide, constant span for the bulk of the display
/// (e.g. (0,229) for HDDCLIP, repeated for ~95 of its 103 rows), then taper back down near-
/// symmetrically at the last few rows — exactly the shape you'd expect from a scanline clip mask
/// for a screen with rounded/angled top and bottom corners, matching "CLIP" in both filenames.
/// </summary>
public class EdgeClipFile : DataFile {
	public Row[]? Rows { get; set; }

	/// <summary>Trailing INT16 after the last row — always 0 in both real files checked, meaning unknown.</summary>
	public short Trailer { get; set; }

	public class Row {
		public short Left { get; set; }
		public short Right { get; set; }
	}
}
