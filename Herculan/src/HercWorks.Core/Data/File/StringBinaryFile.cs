using HercWorks.Vol;

namespace HercWorks.Core.Data.File;

/// <summary>
/// FILE - /LANG0/[lang]/foo.BIN — optimized strings file, contains null-terminated strings with
/// pre-computed offsets and a lookup index for each string. Mainly used for shell descriptions,
/// and for swapping languages.
///   0 - UINT32 - Number of strings stored in this file
///   4 - UINT32 - Total data in bytes for that text, counting null terminators
///   8 - SEQ 0 - file-offset of each string, allowing engine to jump to the string.
/// Ported from org.hercworks.core.data.file.StringBinaryFile.
/// </summary>
public class StringBinaryFile : DataFile {
	public string[]? Values { get; set; }
}
