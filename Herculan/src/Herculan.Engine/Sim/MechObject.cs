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
/// actual stats rather than invented placeholders. The loadout is the one deliberately stubbed
/// piece, and it is stubbed the way the milestone calls for: normal, low-risk technical debt to be
/// replaced by real VSHELL-driven data later, not something to be redesigned.</para>
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

	/// <summary>The stubbed weapon fit — see the type's summary.</summary>
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
/// A mech's weapon fit. Milestone 1 uses <see cref="Stubbed"/>; the real thing comes from the
/// player's loadout in <c>script.dat</c> once the VSHELL-side data path exists — which, per
/// docs/engine/planning.md, is already understood byte-exact and simply hasn't been wired up.
/// </summary>
/// <param name="WeaponIds">
/// Indices into the sim-side weapon tables. Empty in the stub — an empty fit is honestly "no
/// weapons modelled yet" rather than a plausible-looking fit that nothing can fire.
/// </param>
public readonly record struct MechLoadout(IReadOnlyList<int> WeaponIds) {
	/// <summary>The milestone-1 placeholder: no weapons, because no weapon systems exist yet.</summary>
	public static MechLoadout Stubbed => new(Array.Empty<int>());
}
