namespace HercWorks.Help.Internal;

/// <summary>
/// Windows-1252, the help files' text encoding. .NET Core ships only Latin-1 without the code-page
/// provider, and the two differ only at <c>0x80</c>-<c>0x9F</c>, where 1252 has the curly quotes,
/// dashes and bullet the manual uses throughout.
/// </summary>
internal static class Windows1252 {
	// 0x80-0x9F. The five positions 1252 leaves undefined map to U+FFFD.
	private static readonly char[] High = {
		'€', '�', '‚', 'ƒ', '„', '…', '†', '‡',
		'ˆ', '‰', 'Š', '‹', 'Œ', '�', 'Ž', '�',
		'�', '‘', '’', '“', '”', '•', '–', '—',
		'˜', '™', 'š', '›', 'œ', '�', 'ž', 'Ÿ',
	};

	public static string Decode(ReadOnlySpan<byte> bytes) => string.Create(bytes.Length, bytes.ToArray(), (chars, source) => {
		for (int i = 0; i < source.Length; i++) {
			byte b = source[i];
			chars[i] = b is >= 0x80 and <= 0x9F ? High[b - 0x80] : (char)b;
		}
	});
}
