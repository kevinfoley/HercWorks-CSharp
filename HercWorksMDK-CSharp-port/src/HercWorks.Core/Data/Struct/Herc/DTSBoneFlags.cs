namespace HercWorks.Core.Data.Struct.Herc;

/// <summary>
/// Ported from org.hercworks.core.data.struct.herc.DTSBoneFlags.
///
/// NOTE: in the original Java, the enum constructor never actually assigns its "flagNum" parameter
/// to the "flag" field, so flag() always returned 0 for every value. That's very likely a bug, but
/// this is a literal, bug-compatible port — the flag values passed to each member are preserved
/// below as constructor arguments only, exactly as unused as they were in the Java source.
/// </summary>
public sealed class DTSBoneFlags {
	public static readonly DTSBoneFlags LegLeftCalf = new(256);
	public static readonly DTSBoneFlags LegRightCalf = new(512);
	public static readonly DTSBoneFlags LegLeftThigh = new(768);
	public static readonly DTSBoneFlags LegRightThigh = new(1024);
	public static readonly DTSBoneFlags LegRightFoot = new(1280);
	public static readonly DTSBoneFlags LegLeftFoot = new(1536);

	private readonly short _flag;

	// Mirrors the original Java bug (see class doc) — flagNum is intentionally unused.
	private DTSBoneFlags(short flagNum) {
	}

	public short Flag() => _flag;
}
