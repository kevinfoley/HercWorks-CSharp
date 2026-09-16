namespace HercWorks.Core.Data.Struct;

/// <summary>
/// Ported from org.hercworks.core.data.struct.ProjectileType.
///
/// Independently cross-confirmed against DBSIM.EXE disassembly (see
/// docs/simulation/damage-system.md): these are the exact 4 literal values DBSIM's own
/// PROJ.DAT lookup function (<c>FUN_0040ffc8</c>) is ever called with — a closed set, found from
/// scratch via disassembly with no reference to this enum, matching it value-for-value. Each value
/// corresponds to a genuinely different construction path, not just a data variant: `0` (Missile)
/// and `3` (Grenade) each build via their own distinct projectile-family C++ class (own vtable, own
/// type table), `2` (Bullet) builds via a third with real flight time but no guidance or splash, and
/// `4` (Beam) resolves its hit synchronously at fire time with no persisting object at all (every
/// real `Beam`-typed PROJ.DAT record has `Speed=0`) — the mechanical definition of a hitscan weapon
/// in this engine.
///
/// <para>The C++ class behind each is named by the binary itself — `0` is <c>ROCKET</c>, `2` is
/// <c>BULLET</c>, `3` is <c>GRENADE</c>, `4` has no class at all. `0` keeps the name `Missile` here
/// because that is the weapon it carries in the game's own terms. See
/// docs/simulation/damage-system.md, "Type — a firing-mechanism selector".</para>
/// </summary>
public sealed class ProjectileType {
	public static readonly ProjectileType Missile = new("MISSILE", 0);
	public static readonly ProjectileType Bullet = new("BULLET", 2);

	/// <summary>
	/// Type 3 — the cut <c>GRENADE</c> class. Unreachable in retail: nothing constructs it, so no
	/// <c>PROJ.DAT</c> record carrying this type is ever looked up.
	/// </summary>
	public static readonly ProjectileType Grenade = new("GRENADE", 3);

	public static readonly ProjectileType Beam = new("BEAM", 4);

	private static readonly IReadOnlyList<ProjectileType> All = new[] { Missile, Bullet, Grenade, Beam };
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
