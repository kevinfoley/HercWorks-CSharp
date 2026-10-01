namespace HercWorks.Core.Data.File;

/// <summary>
/// FILE - .STR — a string table (<c>simvol0\str\*.STR</c>, <c>LANG0\ENG\CAMPAIGN.STR</c>,
/// <c>data\mission.str</c>): an <c>int32</c> content length, then groups of
/// <c>{ int16 count; count x { int16 length incl. NUL; text; uint8 attributeCount; attributes } }</c>.
/// See <c>docs/formats/str-strings.md#layout</c>.
///
/// <para>This model holds the first group only. <see cref="Entries"/> are its strings, and each
/// entry's <see cref="StringEntry.Trailer"/> is everything up to the next string's length field: its
/// attribute count and attributes — and, for the group's last entry, the rest of the file.</para>
/// </summary>
public class StringFile {
	/// <summary>The leading <c>int32</c>, the byte count that follows it.</summary>
	public int ContentLength { get; set; }

	public StringEntry[]? Entries { get; set; }

	public class StringEntry {
		public string Text { get; set; } = "";

		/// <summary>
		/// The bytes between this entry's NUL terminator and the next entry's length field (or the end
		/// of the file, for the last entry): the attribute count byte and the attributes.
		/// </summary>
		public byte[] Trailer { get; set; } = Array.Empty<byte>();
	}
}
