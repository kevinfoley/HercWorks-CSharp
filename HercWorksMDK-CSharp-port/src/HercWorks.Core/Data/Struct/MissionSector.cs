namespace HercWorks.Core.Data.Struct;

/// <summary>Ported from org.hercworks.core.data.struct.MissionSector.</summary>
public sealed class MissionSector {
	public static readonly MissionSector Razr = new("RAZR", 5);
	public static readonly MissionSector Alph = new("ALPHA", 0);
	public static readonly MissionSector Delt = new("DELTA", 1);
	public static readonly MissionSector Omic = new("OMICRON", 2);
	public static readonly MissionSector Brav = new("BRAVO", 3);
	public static readonly MissionSector Luna = new("LUNA", 4);

	private static readonly IReadOnlyList<MissionSector> All = new[] { Razr, Alph, Delt, Omic, Brav, Luna };
	private static readonly Dictionary<int, MissionSector> ById = All.ToDictionary(m => (int)m.Id);

	public string Val { get; }
	public short Id { get; }

	private MissionSector(string val, short id) {
		Val = val;
		Id = id;
	}

	public static MissionSector? GetById(int id) => ById.GetValueOrDefault(id);
}
