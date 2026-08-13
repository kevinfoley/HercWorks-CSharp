namespace HercWorks.Core.Data.Ref.Constants;

/// <summary>Ported from org.hercworks.core.data.ref.constants.DTSBoneId.</summary>
public sealed class DTSBoneId {
	public static readonly DTSBoneId None = new("NONE", -1, -1);
	public static readonly DTSBoneId Origin = new("ORIGIN", 0x00, 0);

	public static readonly DTSBoneId CenterTorso = new("CENTER_TORSO", 0x05, 1);

	public static readonly DTSBoneId Head = new("HEAD", 0x08, 5);
	public static readonly DTSBoneId Camera = new("CAMERA", 0x09, 4);
	public static readonly DTSBoneId Center1 = new("CENTER1", 0x0A, 2);
	public static readonly DTSBoneId Center2 = new("CENTER2", 0x0B, 3);
	public static readonly DTSBoneId Unk13 = new("UNK_13", 0x0D, 6);
	public static readonly DTSBoneId Unk66 = new("UNK_66", 0x42, 7);
	public static readonly DTSBoneId Unk77 = new("UNK_77", 0x4D, 8);

	public static readonly DTSBoneId Pelvis = new("PELVIS", 0x0C, 12);

	public static readonly DTSBoneId ThighRight = new("THIGH_RIGHT", 0x04, 13);
	public static readonly DTSBoneId CalfRight = new("CALF_RIGHT", 0x02, 14);
	public static readonly DTSBoneId AnkleRight = new("ANKLE_RIGHT", 0x0E, 15);
	public static readonly DTSBoneId FootRight = new("FOOT_RIGHT", 0x06, 16);
	public static readonly DTSBoneId ToeRight = new("TOE_RIGHT", 0x11, 17);

	public static readonly DTSBoneId ThighLeft = new("THIGH_LEFT", 0x03, 18);
	public static readonly DTSBoneId CalfLeft = new("CALF_LEFT", 0x01, 19);
	public static readonly DTSBoneId AnkleLeft = new("ANKLE_LEFT", 0x0F, 20);
	public static readonly DTSBoneId FootLeft = new("FOOT_LEFT", 0x07, 21);
	public static readonly DTSBoneId ToeLeft = new("TOE_LEFT", 0x10, 22);

	private static readonly IReadOnlyList<DTSBoneId> All = new[]
	{
		None, Origin, CenterTorso, Head, Camera, Center1, Center2, Unk13, Unk66, Unk77, Pelvis,
		ThighRight, CalfRight, AnkleRight, FootRight, ToeRight, ThighLeft, CalfLeft, AnkleLeft,
		FootLeft, ToeLeft
	};

	public string Name { get; }
	public short Val { get; }
	public int Order { get; }

	private DTSBoneId(string name, short val, int order) {
		Name = name;
		Val = val;
		Order = order;
	}

	/// <summary>Original Java defaults to ORIGIN when no value matches; preserved here.</summary>
	public static DTSBoneId ForVal(short val) =>
		All.FirstOrDefault(b => b.Val == val) ?? Origin;
}
