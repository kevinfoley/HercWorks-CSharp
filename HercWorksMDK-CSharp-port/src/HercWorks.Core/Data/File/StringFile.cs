using HercWorks.Vol;

namespace HercWorks.Core.Data.File;

/// <summary>
/// FILE - .STR — found in multiple locations in the game data, and they follow a specific format.
///   0 - UINT32 - Total size in bytes of file.
///   4 - UINT16 - total strings in file.
///   SEQ_0 - String entry: 0_0 UINT16 char len, 0_2 string segment (some entries seem to have
///     trailing metadata after the segment, possibly used by DBSIM).
/// Ported from org.hercworks.core.data.file.StringFile.
/// </summary>
public class StringFile : DataFile {
	public int TotalSize { get; set; }
	public string[]? Strings { get; set; }
}
