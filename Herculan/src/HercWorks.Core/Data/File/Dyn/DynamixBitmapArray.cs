using HercWorks.Core.Util;

namespace HercWorks.Core.Data.File.Dyn;

/// <summary>
/// FILE - VOL file, .DBA — Dynamix Bitmap Array (somewhat guessed). A bitmap with defined frames.
/// Ported from org.hercworks.core.data.file.dyn.DynamixBitmapArray. This replaces the earlier
/// placeholder stub of the same name/namespace used ahead of this package's port.
/// </summary>
public class DynamixBitmapArray {
	/// <summary>
	/// Raw little-endian size bytes as read from the file. Kept because
	/// <see cref="Io.Transform.Common.DynamixBitmapArrayTransformer.Write"/> writes them back
	/// exactly as stored rather than recomputing them — see the note in that method.
	/// </summary>
	public byte[]? FileSize { get; set; }

	/// <summary>
	/// Expected magic-byte header value; the bytes actually read from a given file are not
	/// retained, since the write path always emits this constant.
	/// </summary>
	public static readonly byte[] HeaderMagic = EndianOps.GetIntBEBytes(0x01002800);

	public short ArrayRow { get; set; }
	public short ArrayCols { get; set; }

	public DynamixBitmap[]? Images { get; set; }
	public DynamixPalette? Palette { get; set; }
}
