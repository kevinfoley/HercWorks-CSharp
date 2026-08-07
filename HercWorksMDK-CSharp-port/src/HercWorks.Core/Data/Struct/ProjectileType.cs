namespace HercWorks.Core.Data.Struct;

/// <summary>Ported from org.hercworks.core.data.struct.ProjectileType.</summary>
public sealed class ProjectileType {
	public static readonly ProjectileType Missile = new("MISSILE", 0);
	public static readonly ProjectileType Bullet = new("BULLET", 2);
	public static readonly ProjectileType Rocket = new("ROCKET", 3);
	public static readonly ProjectileType Beam = new("BEAM", 4);

	private static readonly IReadOnlyList<ProjectileType> All = new[] { Missile, Bullet, Rocket, Beam };
	private static readonly Dictionary<short, ProjectileType> ById = All.ToDictionary(p => p.Val);

	public string Type { get; }
	public short Val { get; }

	private ProjectileType(string type, short bit) {
		Type = type;
		Val = bit;
	}

	public override string ToString() => Type;

	public static ProjectileType? ForId(short id) => ById.GetValueOrDefault(id);
}
