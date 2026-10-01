namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// A <c>.MSN</c> row record that has a GUID at 0x00 — what the other rows' refs name, and what
/// <see cref="MissionFile"/>'s lookups search by. Rows #4 and #17 have none.
/// </summary>
public abstract class MapObject {
	public short GUID { get; set; }
	public const int GUIDWord = 0x00 / 2;
}
