using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Core.Data.File.Dat.Sim;
using Herculan.Engine.Numerics;
using Herculan.Engine.World;

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
/// how they read in game. Flight model and patrol behaviour are still out of scope.</para>
///
/// <para><b>It is shootable</b>, and by the simplest of the three hit paths: no shields, no volume,
/// just the type's <c>col\&lt;NAME&gt;.COL</c> sphere model and a one-component health record. See
/// <see cref="DirectFireHitTest"/>.</para>
/// </summary>
public sealed class FlyerObject : SimObject {
	/// <summary>
	/// Altitude the original substitutes when a flyer's spawn coordinate carries no Z, in world
	/// units. Straight from <c>FUN_00421ee8</c>'s trailing <c>if (z == 0) z = 5000;</c>.
	/// </summary>
	public const int DefaultHoverHeight = 5000;

	/// <summary>
	/// The effect id a hit on an already-wrecked flyer spawns instead of the shot's own — the
	/// original's literal 10 in <c>Flyer_DirectFireHitTest</c>, the same id the component-destruction
	/// path uses for its ordinary blast.
	/// </summary>
	private const short WreckHitEffect = 10;

	private readonly int _hitRadius;
	private readonly ColliderNode[] _collision;
	private readonly ComponentDamage? _damage;

	/// <param name="collision">
	/// The type's <c>col\&lt;NAME&gt;.COL</c> hit-sphere model. Empty for two of the three retail
	/// flyer names — only <c>SKIMMER</c> ships one — and a flyer with no model cannot be struck at
	/// all, which is the original's outcome too.
	/// </param>
	/// <param name="damage">
	/// The type's <c>dmg\&lt;NAME&gt;.DMG</c> health record, sized to a flyer's one component and one
	/// dependent. Null alongside a missing <c>.COL</c>, for the same reason.
	/// </param>
	public FlyerObject(string name, FlyerSimData? simData, int hitRadius,
			ColliderNode[]? collision = null, ComponentDamage? damage = null) {
		Name = name;
		SimData = simData;
		_hitRadius = hitRadius;
		_collision = collision ?? Array.Empty<ColliderNode>();
		_damage = damage;
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

	/// <summary>
	/// <c>obj+0x99</c> — whether the flyer's one component has been destroyed. The original also
	/// drops it out of the sky from here (it writes a large negative rate into the object's own
	/// <c>+0x2e</c>); with no flight model to fall through, this only records that it is a wreck, and
	/// a wreck still stops shots.
	/// </summary>
	public bool Destroyed { get; private set; }

	/// <summary>
	/// Who landed the shot that finished it, for the kill credit the original passes back through the
	/// shooter's own vtable <c>+0x60</c>.
	/// </summary>
	public SimObject? LastAttacker { get; private set; }

	/// <summary>Per-component health, or null for a type the install ships no <c>.DMG</c> for.</summary>
	public ComponentDamage? Damage => _damage;

	/// <summary>
	/// The flyer's shape-to-world transform. Like a structure it has no lean and no torso: its
	/// heading is the whole of its orientation.
	/// </summary>
	public Transform3 WorldTransform {
		get {
			var transform = Transform3.FromEuler(0, 0, (short)Heading);
			var position = Position;
			transform.X = position.X;
			transform.Y = position.Y;
			transform.Z = position.Z;
			return transform;
		}
	}

	/// <summary>
	/// <c>Flyer_DirectFireHitTest</c> (<c>00421c8c</c>), the flyer's vtable <c>+0x20</c> — the
	/// shortest of the three hit tests in the simulation, and the only one with no second piece of
	/// geometry behind it.
	///
	/// <list type="number">
	/// <item><b>The same coarse reject</b> every hit test opens with: muzzle to aircraft, against the
	/// ray's length plus the shape's radius plus the shot's clearance.</item>
	/// <item><b>The sphere model</b>, straight away — <see cref="CollisionModel.Test"/> against the
	/// type's <c>.COL</c>, in the flyer's own frame. There are no shields to absorb anything first
	/// and no collision volume to fall back on.</item>
	/// <item><b>Damage</b> to whichever component the geometry named, which for a flyer is always the
	/// only one it has.</item>
	/// </list>
	///
	/// <para>The effect a hit spawns is the shot's <see cref="WeaponShot.ImpactFxGroup.Armor"/> array
	/// — the same array a structure hit uses — <b>unless the flyer is already a wreck</b>, in which
	/// case the original substitutes a fixed effect id of its own instead of anything the shot
	/// carries.</para>
	///
	/// <para>Two things the original does here that this does not, both effects: the secondary spawn
	/// off its own pool that follows every hit, and the debris variant it switches that spawn to once
	/// the aircraft is wrecked.</para>
	/// </summary>
	public override int DirectFireHitTest(SimWorld world, WeaponShot shot) {
		var muzzle = new Vec3i(shot.Muzzle.X, shot.Muzzle.Y, shot.Muzzle.Z);
		if (_hitRadius + shot.Clearance + shot.Distance < Position.ApproxDistanceTo(muzzle)) {
			return 0;
		}

		var hit = CollisionModel.Test(
			_collision, Transform3.Concat(WorldTransform, shot.MuzzleInverse),
			shot.Distance, shot.Clearance, ComponentAlive);

		if (hit is not { } struck) {
			return 0;
		}

		// The effect is picked before the damage lands, which matters only for the generator: the
		// original draws here whether or not the wreck substitution below throws the result away.
		short effect = world.PickImpactEffect(shot.ImpactFx(WeaponShot.ImpactFxGroup.Armor));

		int struckAt = struck.Distance + 1;
		ApplyDamage(struck.ComponentIndex, shot.DamageArmor, shot.Owner);

		world.SpawnImpactEffect(
			Destroyed ? WreckHitEffect : effect, shot.Muzzle.TransformPoint(0, struckAt, 0));

		return struckAt;
	}

	/// <summary>
	/// Whether one of the type's components is still standing. A flyer with no <c>.DMG</c> has none,
	/// so nothing of it is shootable — which is what leaves <c>HOVTANK</c> and <c>DROPSHIP</c>
	/// untouchable on retail data.
	/// </summary>
	private bool ComponentAlive(int index) => _damage?.IsActive(index) ?? false;

	/// <summary>
	/// <c>FUN_00421bb4</c>, the flyer's vtable <c>+0x74</c> — a thin wrapper over
	/// <see cref="ComponentDamage.ApplyDamage"/> that turns the loss of component 0 into the loss of
	/// the aircraft.
	///
	/// <para>Unlike a mech, which weighs limbs and cockpit sections against each other before it
	/// decides it is dead, a flyer's death test is that one component index. Everything the original
	/// does past setting the flag belongs to systems that are not here: the fall it starts, the
	/// mission action it fires, the kill credit through the shooter's <c>+0x60</c> slot (recorded on
	/// <see cref="LastAttacker"/> instead) and the alert it plays for the player.</para>
	/// </summary>
	private void ApplyDamage(int componentIndex, short damage, SimObject? attacker) {
		if (_damage == null || !_damage.ApplyDamage(componentIndex, damage) || componentIndex != 0) {
			return;
		}

		Destroyed = true;
		LastAttacker = attacker;
	}

	/// <summary>Holds station. See the type summary for why there is no terrain query here.</summary>
	public override void Tick(SimWorld world) {
	}
}
