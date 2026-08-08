using HercWorks.Vol;

namespace HercWorks.Core.Data.File;

/// <summary>
/// FILE - .STR — found in multiple locations in the game data (simvol0/str, SHELL0/GAM), and they
/// all follow the same format, confirmed byte-exact against every real .STR file in the retail
/// data (13 in simvol0/str + SHELL0/GAM/CAMPAIGN.STR):
///   0 - UINT32 - total content size in bytes, minus this field's own 4 bytes (i.e. contentLength - 4).
///   4 - UINT16 - total strings in file.
///   SEQ_0 - String entry: 0_0 UINT16 byte length of the string segment INCLUDING its null
///     terminator, 0_2 the string segment itself (length-1 chars + a trailing 0x00 byte).
///   Between one entry's null terminator and the next entry's length field, some files (not all)
///   insert extra per-entry trailer bytes whose meaning is undecoded — likely playback metadata
///   consumed by DBSIM (voice-line id, duration, priority, etc. are plausible guesses, not
///   confirmed). The trailer's size is constant within one file but varies between files (0, 1, 2,
///   8, and 9 bytes have all been observed in real retail data) — there is no in-file field that
///   states it directly, so <see cref="StringFileTransformer"/> resyncs to each entry by scanning
///   forward for the next well-formed length-prefixed, null-terminated string instead of assuming
///   a fixed trailer size.
/// Ported from org.hercworks.core.data.file.StringFile; extended with per-entry trailer bytes
/// (the original only modeled TotalSize + a flat string array, with no trailer handling).
/// </summary>
public class StringFile : DataFile {
	public int TotalSize { get; set; }
	public StringEntry[]? Entries { get; set; }

	public class StringEntry {
		public string Text { get; set; } = "";

		/// <summary>Undecoded bytes between this entry's null terminator and the next entry's length field (or end of file, for the last entry). Empty for files that don't use trailers.</summary>
		public byte[] Trailer { get; set; } = Array.Empty<byte>();
	}
}
