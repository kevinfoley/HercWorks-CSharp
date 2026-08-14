using Herculan.Engine.Numerics;
using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

/// <summary>
/// A structure — a base building, a turret, a bunker. In DBSIM this is the class built by
/// <c>FUN_00405314</c> from a <c>script.dat</c> block-9 record and attached to its group by
/// <c>FUN_00405c3c</c>; its type comes from <c>dat\BASES.DAT</c> (see <see cref="BaseTypeTable"/>),
/// which is also what names its model and texture bank.
///
/// <para>Structures are the bulk of a mission's object count and none of its motion: they sit where
/// the mission puts them. The one thing the original does that this does not is flatten the terrain
/// underneath a structure as it places it (<c>FUN_00470dc8</c>, called with the object's radius just
/// before the height query) — that writes to the loaded heightmap, so it belongs with terrain
/// deformation rather than here, and leaving it out means a structure on a slope stands on the
/// interpolated surface instead of a levelled pad.</para>
/// </summary>
public sealed class BaseObject : SimObject {
	private readonly int _hitRadius;

	public BaseObject(BaseType type, int hitRadius) {
		Type = type;
		_hitRadius = hitRadius;
	}

	/// <summary>The <c>BASES.DAT</c> entry this structure is an instance of.</summary>
	public BaseType Type { get; }

	/// <inheritdoc />
	public override int HitRadius => _hitRadius;

	/// <summary>
	/// Sits the structure on the ground. Same treatment mechs get, and for the same reason: the
	/// mission states X and Y, and the terrain states Z.
	/// </summary>
	public override void Tick(SimWorld world) {
		Position = new Vec3i(Position.X, Position.Y, world.GroundHeightAt(Position));
	}
}
