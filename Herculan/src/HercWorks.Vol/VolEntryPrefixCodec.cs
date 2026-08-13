using HercWorks.Vol.Util;

namespace HercWorks.Vol;

/// <summary>
/// Result of <see cref="VolEntryPrefixCodec.StripIfPresent"/>: the file's content bytes plus
/// whatever per-entry prefix fields were captured (if any) so they can be preserved on export.
/// </summary>
public readonly record struct VolEntryPrefixResult(
    byte[] Content, bool HadPrefix, byte CompressionType, byte[]? MagicPrefix, bool HadTrailingByte);

/// <summary>
/// Handles the same 9-byte per-entry prefix (1-byte compression flag + 4-byte little-endian
/// content size + 4-byte magic) that <see cref="VolEntry"/> carries when parsed out of a packed
/// .VOL, plus the single trailing marker byte observed after an entry's content. Real loose
/// copies of these files (e.g. a retail install's own external-override GAM\ tree) carry this
/// same prefix — it isn't unique to files still packed inside a .VOL — so any code that opens an
/// arbitrary loose file (rather than reading it via <see cref="VolEntry"/> off an already-loaded
/// Voln) needs to detect and round-trip it independently. Centralized here so that logic exists
/// in exactly one place instead of being re-derived per editor.
/// </summary>
public static class VolEntryPrefixCodec {
	private const int PrefixLength = 9;

	/// <summary>
	/// Detects the prefix via the entry's own self-declared content-size field (reliable — it's
	/// the same signal <see cref="Io.VolFileReader"/> relies on) and strips it off, returning
	/// clean content either way. If the file doesn't match that shape, it's assumed to already be
	/// content-only and is returned unchanged.
	/// </summary>
	public static VolEntryPrefixResult StripIfPresent(byte[] bytes) {
		if (bytes.Length > PrefixLength) {
			int declaredContentSize = ByteOps.ReadInt32LE(bytes, 1);
			long expectedTotal = PrefixLength + declaredContentSize;

			// Allow for the single trailing "unknown EOF byte" VolFileReader also observed.
			if (expectedTotal == bytes.Length || expectedTotal + 1 == bytes.Length) {
				bool hasTrailingByte = expectedTotal + 1 == bytes.Length;
				byte compressionType = bytes[0];
				byte[] magicPrefix = ByteOps.Slice(bytes, 5, 4);
				byte[] content = ByteOps.Slice(bytes, PrefixLength, declaredContentSize);

				return new VolEntryPrefixResult(content, true, compressionType, magicPrefix, hasTrailingByte);
			}
		}

		return new VolEntryPrefixResult(bytes, false, 0, null, false);
	}

	/// <summary>
	/// Rebuilds the retail-format byte layout around edited content: original compression type
	/// and magic preserved byte-for-byte, size field recalculated for the new content length.
	/// </summary>
	public static byte[] Wrap(byte[] content, byte compressionType, byte[] magicPrefix, bool includeTrailingByte) {
		using var outStream = new MemoryStream();
		outStream.WriteByte(compressionType);

		var sizeBytes = ByteOps.GetInt32LEBytes(content.Length);
		outStream.Write(sizeBytes, 0, sizeBytes.Length);
		outStream.Write(magicPrefix, 0, magicPrefix.Length);
		outStream.Write(content, 0, content.Length);

		if (includeTrailingByte) {
			outStream.WriteByte(0x00);
		}

		return outStream.ToArray();
	}
}
