namespace HercWorks.Core.Data.Struct.Herc;

/// <summary>Ported from org.hercworks.core.data.struct.herc.HercExternals.</summary>
public sealed class HercExternals {
	public static readonly HercExternals CockpitFront = new(0, "Cockpit Front");
	public static readonly HercExternals CockpitRear = new(1, "Cockpit Rear");
	public static readonly HercExternals TorsoLeftFront = new(2, "Left Torso");
	public static readonly HercExternals TorsoRightFront = new(3, "Right Torso");
	public static readonly HercExternals TorsoLeftRear = new(4, "Left Torso");
	public static readonly HercExternals TorsoRightRear = new(5, "Right Torso");
	public static readonly HercExternals Chassis = new(6, "Chassis");
	public static readonly HercExternals LegLeftTop = new(7, "Leg Left Thigh");
	public static readonly HercExternals LegRightTop = new(8, "Leg Right Thigh");
	public static readonly HercExternals LegLeftMid = new(9, "Leg Left Calf");
	public static readonly HercExternals LegRightMid = new(10, "Leg Right Calf");
	public static readonly HercExternals LegLeftFoot = new(11, "Leg Left Foot");
	public static readonly HercExternals LegRightFoot = new(12, "Leg Right Foot");

	private static readonly IReadOnlyList<HercExternals> All = new[]
	{
		CockpitFront, CockpitRear, TorsoLeftFront, TorsoRightFront, TorsoLeftRear, TorsoRightRear,
		Chassis, LegLeftTop, LegRightTop, LegLeftMid, LegRightMid, LegLeftFoot, LegRightFoot
	};

	private static readonly Dictionary<short, HercExternals> ById = All.ToDictionary(e => e.Id);

	public short Id { get; }
	public string Label { get; }

	private HercExternals(short id, string label) {
		Id = id;
		Label = label;
	}

	public static HercExternals? GetById(short id) => ById.GetValueOrDefault(id);

	/// <summary>Equivalent of Java's enum .values().</summary>
	public static IReadOnlyList<HercExternals> Values() => All;

	public static HercExternals? GetByName(string name) =>
		All.FirstOrDefault(e => string.Equals(name, e.Label, StringComparison.OrdinalIgnoreCase));
}
