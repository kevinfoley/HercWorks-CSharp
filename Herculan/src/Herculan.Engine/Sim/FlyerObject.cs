using HercWorks.Core.Data.File.Dat.Sim;

namespace Herculan.Engine.Sim;

/// <summary>
/// A flyer or ground vehicle — the <c>SKIMMER</c>/<c>HOVTANK</c>/<c>DROPSHIP</c> class listed in
/// <c>nam\FLYERS.NAM</c>. In DBSIM this is the class constructed by <c>FUN_004215f4</c> from a
/// <c>script.dat</c> block-8 record and attached to its group by <c>FUN_00421ee8</c>.
///
/// <para>The one behavioural detail carried over from that attach function is the hover height: a
/// flyer does <b>not</b> get the terrain query mechs and structures get — it takes its Z straight
/// from its spawn coordinate, and if that leaves it at zero the original substitutes 5000 world
/// units (30 m). So a flyer holds an absolute altitude rather than following the ground, which is
/// how they read in game. Everything else — flight model, patrol behaviour, weapons — is out of
/// scope until the flyer systems are ported.</para>
/// </summary>
public sealed class FlyerObject : SimObject {
	/// <summary>
	/// Altitude the original substitutes when a flyer's spawn coordinate carries no Z, in world
	/// units. Straight from <c>FUN_00421ee8</c>'s trailing <c>if (z == 0) z = 5000;</c>.
	/// </summary>
	public const int DefaultHoverHeight = 5000;

	private readonly int _hitRadius;

	public FlyerObject(string name, FlyerSimData? simData, int hitRadius) {
		Name = name;
		SimData = simData;
		_hitRadius = hitRadius;
	}

	/// <summary>Base name of the flyer's data files, e.g. <c>SKIMMER</c>.</summary>
	public string Name { get; }

	/// <summary>
	/// The type's stats from <c>dat\&lt;name&gt;.DAT</c>, or null when the install has no such file —
	/// only <c>SKIMMER</c> ships one, so the other two types legitimately have none.
	/// </summary>
	public FlyerSimData? SimData { get; }

	/// <inheritdoc />
	public override int HitRadius => _hitRadius;

	/// <summary>Holds station. See the type summary for why there is no terrain query here.</summary>
	public override void Tick(SimWorld world) {
	}
}
