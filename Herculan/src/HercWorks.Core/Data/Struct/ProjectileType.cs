namespace HercWorks.Core.Data.Struct;

/// <summary>
/// Ported from org.hercworks.core.data.struct.ProjectileType.
///
/// Independently cross-confirmed against DBSIM.EXE disassembly (2026-08-09, see
/// docs/simulation/damage-system.md): these are the exact 4 literal values DBSIM's own
/// PROJ.DAT lookup function (<c>FUN_0040ffc8</c>) is ever called with — a closed set, found from
/// scratch via disassembly with no reference to this enum, matching it value-for-value. Each
/// value corresponds to a genuinely different construction path, not just a data variant: `0`
/// (Missile) and `3` (Rocket) each build via their own distinct rocket-family C++ class (own
/// vtable, own type table) — `3`'s is confirmed guided/homing (lead-prediction physics) — `2`
/// (Bullet) builds via a third, separate rocket-family class with real flight time but no
/// guidance or splash, and `4` (Beam) resolves its hit synchronously at fire time with no
/// persisting object at all (every real `Beam`-typed PROJ.DAT record has `Speed=0`) — the
/// mechanical definition of a hitscan weapon in this engine. Whether "Missile" vs "Rocket" (the
/// two splash-capable, real-flight types) map onto the manual's Plasma-cannon/Missile weapons the
/// way their names suggest is not independently verified — only the mechanical behavior and the
/// complete 4-value set are confirmed, not the English labels' exactness.
/// </summary>
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
