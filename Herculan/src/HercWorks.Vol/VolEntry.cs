using HercWorks.Vol.Util;

namespace HercWorks.Vol;

/// <summary>
/// Earthsiege 2 VOL entries follow this schema:
///   18 bytes total for the listing entry.
///     13 bytes for file name (plus odd trailing bytes observed on some entries)
///     UINT8  directory index number - mapped to the directory listing
///     UINT32 file's offset in the VOL (little-endian)
///
///   File prefix - 9 bytes, found at the offset above:
///     UINT8  possibly a compression type flag
///     UINT32 file size in bytes (little-endian)
///     UINT32 unknown 4-byte magic
///
/// Ported from org.hercworks.voln.VolEntry.
/// </summary>
public class VolEntry : DataFile {
	/// <summary>Raw little-endian offset bytes as read from the VOL file (round-tripped as-is on strict writes).</summary>
	public byte[]? VolOffset { get; set; }

	/// <summary>Raw 13-byte filename field, including any odd trailing bytes observed on some entries.</summary>
	public byte[]? VolListBytes { get; set; }

	public byte DirIdx { get; set; }

	public byte[]? UnknownEoFByte { get; set; }

	/// <summary>Raw 4-byte magic prefix observed ahead of every file's data in the VOL.</summary>
	public byte[]? MagicPrefix { get; set; }

	public byte FileCompressionType { get; set; }

	/// <summary>Numeric interpretation of <see cref="VolOffset"/> (little-endian).</summary>
	public int VolOffsetValue => VolOffset == null ? 0 : ByteOps.ReadInt32LE(VolOffset, 0);

	public override string ToString() {
		return "VolEntry [fileName=" + FileName
			 + ", volOffset=" + VolOffsetValue
			 + ", byteSize=" + (RawBytes?.Length ?? 0)
			 + ", magicPrefix=" + PrintMagicPrefix()
			 + ", dirIdx=" + DirIdx + "]";
	}

	/// <summary>
	/// Sometimes in a VOL, the file-list entry has trailing bytes after the filename string.
	/// This strips the trailing bytes to produce a clean file name.
	/// </summary>
	public static string NameFromListBytes(byte[] listBytes) {
		var chars = new System.Text.StringBuilder();
		foreach (byte b in listBytes) {
			if (b == 0x00) break;
			chars.Append((char)b);
		}
		return chars.ToString();
	}

	public string PrintMagicPrefix() {
		return $"{FileName}|\t({FileCompressionType})\t[{ByteOps.ToHex(MagicPrefix)}]\t";
	}
}
