using HercWorks.Core.Util;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dyn;

/// <summary>
/// FILE - VOL file, .DBA — Dynamix Bitmap Array (somewhat guessed). A bitmap with defined frames.
/// Ported from org.hercworks.core.data.file.dyn.DynamixBitmapArray. This replaces the earlier
/// placeholder stub of the same name/namespace used ahead of this package's port.
/// </summary>
public class DynamixBitmapArray : DataFile {
	/// <summary>
	/// Expected magic-byte header value. Separate from the inherited instance <see cref="DataFile.Header"/>
	/// (which holds whatever header bytes were actually read from a given file) — same split as the
	/// Java original's static `header` constant vs. the inherited instance field.
	/// </summary>
	public static readonly byte[] HeaderMagic = EndianOps.GetIntBEBytes(0x01002800);

	public short ArrayRow { get; set; }
	public short ArrayCols { get; set; }

	public DynamixBitmap[]? Images { get; set; }
	public DynamixPalette? Palette { get; set; }
}
