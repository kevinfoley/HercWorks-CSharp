namespace HercWorks.Core.Data.Struct.Vshell.Sav;

/// <summary>Ported from org.hercworks.core.data.struct.vshell.sav.PilotRank.</summary>
public sealed class PilotRank {
	public static readonly PilotRank Rookie = new(0, "Rookie");
	public static readonly PilotRank Regular = new(1, "Regular");
	public static readonly PilotRank Veteran = new(2, "Veteran");
	public static readonly PilotRank Elite = new(3, "Elite");
	public static readonly PilotRank Lieutenant = new(4, "Lieutenant");
	public static readonly PilotRank Captain = new(5, "Captain");
	public static readonly PilotRank Major = new(6, "Major");
	public static readonly PilotRank LtColonel = new(7, "Lt Colonel");

	private static readonly IReadOnlyList<PilotRank> All = new[]
	{
		Rookie, Regular, Veteran, Elite, Lieutenant, Captain, Major, LtColonel
	};

	private static readonly Dictionary<short, PilotRank> ById = All.ToDictionary(r => r.Id);

	public short Id { get; set; }
	public string Label { get; set; }

	private PilotRank(short id, string label) {
		Id = id;
		Label = label;
	}

	public static PilotRank? GetById(short id) => ById.GetValueOrDefault(id);

	/// <summary>Equivalent of Java's enum .values().</summary>
	public static IReadOnlyList<PilotRank> Values() => All;

	/// <summary>Original Java defaults to ROOKIE when no name matches; preserved here.</summary>
	public static PilotRank GetByName(string name) =>
		All.FirstOrDefault(r => string.Equals(name, r.Label, StringComparison.OrdinalIgnoreCase)) ?? Rookie;

	public override string ToString() => Label;
}
