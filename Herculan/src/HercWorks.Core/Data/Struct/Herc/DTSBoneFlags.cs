namespace HercWorks.Core.Data.Struct.Herc;

/// <summary>
/// Ported from org.hercworks.core.data.struct.herc.DTSBoneFlags.
/// </summary>
public sealed class DTSBoneFlags {
	public static readonly DTSBoneFlags LegLeftCalf = new(256);
	public static readonly DTSBoneFlags LegRightCalf = new(512);
	public static readonly DTSBoneFlags LegLeftThigh = new(768);
	public static readonly DTSBoneFlags LegRightThigh = new(1024);
	public static readonly DTSBoneFlags LegRightFoot = new(1280);
	public static readonly DTSBoneFlags LegLeftFoot = new(1536);

	private readonly short _flag;

	private DTSBoneFlags(short flagNum) {
		_flag = flagNum;
	}

	public short Flag() => _flag;
}
