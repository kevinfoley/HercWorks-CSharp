
namespace HercWorks.Core.Data.File.Dyn;

/// <summary>
/// A <c>.DBM</c> bitmap, or one frame of a <see cref="DynamixBitmapArray"/>. The file names no
/// palette; <see cref="Palette"/> is whichever <c>.DPL</c> a caller pairs with it.
///   UINT32 header tag (<see cref="HeaderMagic"/>)
///   UINT32 size of what follows
///   UINT16 rows (height)
///   UINT16 cols (width)
///   UINT16 bit depth
///   BYTE   <see cref="UnkSpacer1"/>
///   UINT32 image data length
///   UINT16 <see cref="UnkSpacer2"/>
///   [image data]
/// </summary>
public class DynamixBitmap {
	/// <summary>
	/// Not in the file: a name the toolkit gives the bitmap for exporting it —
	/// <see cref="Io.Transform.Common.DynamixBitmapArrayTransformer"/> sets "_&lt;index&gt;" on each
	/// frame of a <c>.DBA</c>, and <see cref="Io.Read.DynFileReader"/> a loose <c>.DBM</c>'s own file
	/// name.
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
	/// <summary>Meaning not established; read and written back verbatim.</summary>
	public byte UnkSpacer1 { get; set; }

	public int ImageDataLen { get; set; }

	/// <summary>Meaning not established; read and written back verbatim.</summary>
	public short UnkSpacer2 { get; set; }

	public DynamixPalette? Palette { get; set; }
	public byte[]? ImageData { get; set; }
}
