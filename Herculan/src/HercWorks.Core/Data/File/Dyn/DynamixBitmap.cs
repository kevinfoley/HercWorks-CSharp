
namespace HercWorks.Core.Data.File.Dyn;

/// <summary>
/// FILE - VOL file, .DBM — Dynamix Bitmap. Needs a matching .DPL file to really be viewed. A
/// DynamixPalette field is provided, but DBMs don't explicitly bind a palette to themselves —
/// the game binaries seem to know which one has which.
///   UINT32 header tag
///   UINT32 file size value
///   UINT16 row count (height)
///   UINT16 col count (width)
///   UINT16 bitdepth length
///   null byte
///   UINT32 payload (raw image data) length
///   2 null bytes
///   [begin data]
/// Ported from org.hercworks.core.data.file.dyn.DynamixBitmap.
/// </summary>
public class DynamixBitmap {
	/// <summary>
	/// Frame name. Set by <see cref="Io.Transform.Common.DynamixBitmapArrayTransformer"/> to
	/// "_&lt;index&gt;" for each frame it unpacks out of a .DBA, and read back when exporting
	/// frames to disk. Declared here rather than inherited because this is the only Dynamix model
	/// whose name is actually consumed.
	/// </summary>
	public string? FileName { get; set; }

	/// <summary>
	/// Expected magic-byte header value; the bytes actually read from a given file are not
	/// retained, since the write path always emits this constant.
	/// </summary>
	public static readonly byte[] HeaderMagic = HercWorks.Core.Util.EndianOps.GetIntBEBytes(0x0E002800);

	public short Rows { get; set; }
	public short Cols { get; set; }
	public short BitDepth { get; set; }
	public byte UnkSpacer1 { get; set; }
	public int ImageDataLen { get; set; }
	public short UnkSpacer2 { get; set; }

	public DynamixPalette? Palette { get; set; }
	public byte[]? ImageData { get; set; }
}
