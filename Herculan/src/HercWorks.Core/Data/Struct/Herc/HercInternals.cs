namespace HercWorks.Core.Data.Struct.Herc;

/// <summary>
/// Ported from org.hercworks.core.data.struct.herc.HercInternals.
///
/// <para><b>Only ids 0-9 exist on disk.</b> The internal condition array is ten shorts: ids 0-8 are
/// the nine components the repair bay names, and id 9 is the machine's overall condition — the mean
/// of the externals and internals, which the debrief copies into the pilot's own condition, hence
/// the <c>Pilot</c> label. Ids 10-12 are not in the file at all, which is why readers and editors
/// stop at <see cref="ServosLegLeftRear"/>. See <c>docs/formats/save-games.md</c>.</para>
/// </summary>
public sealed class HercInternals {
	public static readonly HercInternals ServosLegLeft = new(0, "Left Leg Servos");
	public static readonly HercInternals ServosLegRight = new(1, "Right Leg Servos");
	public static readonly HercInternals SensorArray = new(2, "Sensor Array");
	public static readonly HercInternals TargComp = new(3, "Targeting Computer");
	public static readonly HercInternals ShieldGen = new(4, "Shield Generator");
	public static readonly HercInternals Engine = new(5, "Engine");
	public static readonly HercInternals Hydraulics = new(6, "Hydraulics");
	public static readonly HercInternals Stabilizers = new(7, "Stabilizers");
	public static readonly HercInternals LifeSupport = new(8, "Life Support");
	public static readonly HercInternals Pilot = new(9, "Pilot");
	public static readonly HercInternals ServosLegLeftRear = new(10, "Rear Left Leg Servos");
	public static readonly HercInternals ServosLegRightRear = new(11, "Rear Right Leg Servos");
	public static readonly HercInternals Unknown = new(12, "unkown/empty");

	private static readonly IReadOnlyList<HercInternals> All = new[]
	{
		ServosLegLeft, ServosLegRight, SensorArray, TargComp, ShieldGen, Engine, Hydraulics,
		Stabilizers, LifeSupport, Pilot, ServosLegLeftRear, ServosLegRightRear, Unknown
	};

	private static readonly Dictionary<short, HercInternals> ById = All.ToDictionary(e => e.Id);

	public short Id { get; }
	public string Label { get; }

	private HercInternals(short id, string label) {
		Id = id;
		Label = label;
	}

	public static HercInternals? GetById(short id) => ById.GetValueOrDefault(id);

	/// <summary>Equivalent of Java's enum .values().</summary>
	public static IReadOnlyList<HercInternals> Values() => All;

	public static HercInternals? GetByName(string name) =>
		All.FirstOrDefault(e => string.Equals(name, e.Label, StringComparison.OrdinalIgnoreCase));
}
