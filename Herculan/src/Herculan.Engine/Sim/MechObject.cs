using HercWorks.Core.Data.File.Dat.Sim;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// A HERC in the simulation. In DBSIM this is the class with the 34-slot vtable — by a wide margin
/// the most elaborate <see cref="SimObject"/> subtype, carrying shields, a 29-slot component health
/// array with its own dependency graph, a weapon-mount manager object, reactor energy bookkeeping
/// and an AI/input controller. None of that is here yet: the first milestone spawns one mech to
/// stand in a loaded zone, with combat and movement systems explicitly out of scope (see
/// docs/engine/planning.md, "First milestone").
///
/// <para>What <i>is</i> real is the per-type data — <see cref="HercSimDat"/> is read from the game's
/// own <c>dat\&lt;name&gt;.dat</c>, so speeds, turn rates and the texture-group id are the mech's
/// actual stats rather than invented placeholders. As of the mission-loading milestone the
/// <see cref="Loadout"/> is real too: it is the fit the mission author gave this machine, out of
/// <c>script.dat</c>'s own roster record (or <c>player.mec</c> for the player's lance).</para>
/// </summary>
public sealed class MechObject : SimObject {
	private readonly int _hitRadius;

	public MechObject(string name, HercSimDat simData, int hitRadius, MechLoadout loadout) {
		Name = name;
		SimData = simData;
		_hitRadius = hitRadius;
		Loadout = loadout;
	}

	/// <summary>Base name of the mech's data files, e.g. <c>SAMSON</c> for <c>dat\SAMSON.DAT</c>.</summary>
	public string Name { get; }

	/// <summary>The mech type's stats, straight out of the game's own per-mech <c>.DAT</c>.</summary>
	public HercSimDat SimData { get; }

	/// <summary>The weapon fit the mission gave this machine — see the type's summary.</summary>
	public MechLoadout Loadout { get; }

	/// <summary>
	/// Coarse collision radius. DBSIM reads a per-type hit-cylinder radius from its in-memory mech
	/// type record at <c>+0x1a</c>; that record is assembled from more than just the <c>.DAT</c>
	/// file and its offsets have not been mapped onto <see cref="HercSimDat"/>'s fields yet, so the
	/// caller supplies a radius derived from the loaded model's bounds instead. Swapping in the real
	/// per-type value once that mapping is resolved is a constructor-argument change.
	/// </summary>
	public override int HitRadius => _hitRadius;

	/// <summary>
	/// Keeps the mech standing on the terrain. This is the whole of its behavior for now: no
	/// locomotion (the first milestone's mech is stationary), no shields, no AI. The one thing it
	/// does do is go through the ported terrain height query rather than caching a spawn height, so
	/// a mech placed anywhere in the zone sits correctly on the interpolated surface.
	/// </summary>
	public override void Tick(SimWorld world) {
		Position = new Vec3i(Position.X, Position.Y, world.GroundHeightAt(Position));
	}
}

/// <summary>
/// A mech's weapon fit, as the mission states it. AI machines get theirs from <c>script.dat</c>
/// block 7's own weapon array; the player's lance gets theirs from <c>player.mec</c>. Both feed the
/// same <c>Mech_ConfigureLoadout</c> in the original.
/// </summary>
/// <param name="WeaponIds">
/// The mount ids the mission assigned, with the file's empty-slot sentinels already dropped. Nothing
/// fires them yet — weapon systems are a later milestone — so for now this is data carried
/// faithfully rather than data acted on.
/// </param>
public readonly record struct MechLoadout(IReadOnlyList<int> WeaponIds) {
	/// <summary>An empty fit, for a machine spawned outside a mission.</summary>
	public static MechLoadout None => new(Array.Empty<int>());
}
