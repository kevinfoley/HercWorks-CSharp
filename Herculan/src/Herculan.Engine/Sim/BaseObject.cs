using Herculan.Engine.Numerics;
using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

/// <summary>
/// A structure — a base building, a turret, a bunker. In DBSIM this is the class built by
/// <c>FUN_00405314</c> from a <c>script.dat</c> block-9 record and attached to its group by
/// <c>Base_AttachToGroup</c> (<c>00405c3c</c>); its type comes from <c>dat\BASES.DAT</c> (see
/// <see cref="BaseType"/>), which is also what names its model, its texture bank and its
/// destructible parts.
///
/// <para>Structures are the bulk of a mission's object count and none of its motion: they sit where
/// the mission puts them. The one thing the original does that this does not is flatten the terrain
/// underneath a structure as it places it (<c>FUN_00470dc8</c>, called with the object's radius just
/// before the height query) — that writes to the loaded heightmap, so it belongs with terrain
/// deformation rather than here, and leaving it out means a structure on a slope stands on the
/// interpolated surface instead of a levelled pad.</para>
///
/// <para><b>They are shootable</b>, which is what <see cref="DirectFireHitTest"/> and
/// <see cref="ApplyDamage"/> are; see those for the two very different pieces of geometry that
/// answer "did this shot hit this building".</para>
/// </summary>
public sealed class BaseObject : SimObject {
	private readonly ShapeVolume? _volume;
	private readonly CollisionNode[] _collision;
	private readonly int _shapeRadius;

	// Damage taken per component, against BaseComponentType.MaxDamage -- the original's own
	// direction, counting up to the maximum rather than down from it (obj+0x205, stride 11).
	private readonly int[] _damage;

	// obj+0x201: whether each component is still standing. A destroyed one is skipped by both the
	// hit test and the damage path, so it stops absorbing fire entirely.
	private readonly bool[] _alive;

	/// <param name="type">The <c>BASES.DAT</c> entry this structure is an instance of.</param>
	/// <param name="volume">
	/// The shape's collision volume, or null for a type whose shape has none — every
	/// <see cref="BaseShapeSource.AnimatedLibrary"/> type, since the volume is a field of the
	/// <c>.DGS</c> record and those shapes are ordinary DTS. That costs nothing on retail data:
	/// all eight animated types set <see cref="BaseType.HasCollisionModel"/>, so none of them would
	/// reach the volume path while standing.
	/// </param>
	/// <param name="collision">The type's <c>BASECOL.DAT</c> sphere model — see <see cref="CollisionModel"/>.</param>
	/// <param name="shapeRadius">
	/// The shape's own bounding radius — the original's vtable <c>+0x10</c> (<c>FUN_0046b80c</c>),
	/// which is simply <c>shape+8</c>. Both hit paths open with a coarse reject against it.
	///
	/// <para>Distinct from <see cref="HitRadius"/>, which is the <i>type</i>'s stated figure and is
	/// what the blast sweep asks for; the two disagree by up to a fifth on retail data, and four
	/// types state a type radius of zero while their shape has a real one.</para>
	///
	/// <para>Zero for an <see cref="BaseShapeSource.AnimatedLibrary"/> type, whose shape is a DTS
	/// root rather than a <c>.DGS</c> record and whose head fields this engine does not read; the
	/// type's own radius stands in there, which is close enough for a reject that only has to be
	/// generous.</para>
	/// </param>
	public BaseObject(BaseType type, ShapeVolume? volume, CollisionNode[]? collision, int shapeRadius) {
		Type = type;
		_volume = volume;
		_collision = collision ?? Array.Empty<CollisionNode>();
		_shapeRadius = shapeRadius != 0 ? shapeRadius : type.HitRadius;
		_damage = new int[type.Components.Length];
		_alive = new bool[type.Components.Length];
		Array.Fill(_alive, true);
	}

	/// <summary>The <c>BASES.DAT</c> entry this structure is an instance of.</summary>
	public BaseType Type { get; }

	/// <summary>
	/// <c>obj+0x99</c> — whether every component has been destroyed and the structure has fallen. It
	/// is set by <see cref="ApplyDamage"/> the moment <see cref="DamageFraction"/> reaches full, and
	/// once set the structure takes no further damage.
	/// </summary>
	public bool Destroyed { get; private set; }

	/// <summary>Whether one of the type's components is still standing.</summary>
	public bool ComponentAlive(int index) =>
		index >= 0 && index < _alive.Length && _alive[index];

	/// <summary>Damage taken by one component, against its <see cref="BaseComponentType.MaxDamage"/>.</summary>
	public int ComponentDamage(int index) =>
		index >= 0 && index < _damage.Length ? _damage[index] : 0;

	/// <summary>
	/// <c>FUN_004052b4</c>, the type's vtable <c>+0x40</c> — how far gone the structure is, as a Q8
	/// fraction: the sum of every component's damage over the sum of every component's maximum. A
	/// full 256 is what <see cref="ApplyDamage"/> tests for to decide the structure has fallen.
	///
	/// <para>Because it is a <i>ratio of sums</i> rather than a count of destroyed components, a
	/// structure with one 30000-point core and six small parts is effectively destroyed by killing
	/// the core alone — which is exactly how the two seven-component retail types are authored.</para>
	/// </summary>
	public int DamageFraction {
		get {
			int damage = 0;
			int maximum = 0;
			for (int i = 0; i < Type.Components.Length; i++) {
				damage += _damage[i];
				maximum += Type.Components[i].MaxDamage;
			}

			return maximum == 0 ? 0 : (damage << 8) / maximum;
		}
	}

	/// <summary>The Q8 value <see cref="DamageFraction"/> reaches when nothing is left standing.</summary>
	public const int FullyDestroyed = 0x100;

	/// <inheritdoc />
	public override int HitRadius => Type.HitRadius;

	/// <summary>
	/// The structure's shape-to-world transform. A structure has no lean and no torso: its heading
	/// is the whole of its orientation, so this is a Z rotation with its world position in the
	/// translation.
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
	/// <c>Base_DirectFireHitTest</c> (<c>FUN_00405038</c>) — the vtable <c>+0x20</c> every structure
	/// class shares (all five of the type-switched vtables <c>FUN_00405314</c> installs point at it),
	/// and, as everywhere else along this path, the hit test and the damage application in one call.
	///
	/// <para><b>There are two completely different pieces of hit geometry</b>, and which one runs is
	/// the type's <see cref="BaseType.HasCollisionModel"/> flag:</para>
	/// <list type="bullet">
	/// <item><b>The sphere model</b> — <c>dat\BASECOL.DAT</c>'s hand-authored clusters, one per
	/// destructible component. This is the path that can say <i>which part</i> of a building was
	/// struck, so it is the one that makes a structure come apart section by section. 25 of the 65
	/// retail types use it.</item>
	/// <item><b>The shape's collision volume</b> — the coarse height field in the <c>.DGS</c> record
	/// (see <see cref="ShapeVolume"/>), which knows only "solid here". Everything it hits is
	/// component 0.</item>
	/// </list>
	///
	/// <para>A destroyed structure that leaves a wreck behind (<see cref="BaseType.HulkTypeIndex"/>)
	/// is switched over to the volume path whichever it was using — the wreck is a different shape
	/// with different geometry, and its spheres would be the standing building's. The original's
	/// third condition on that switch, that no destruction effect is playing on component 0, is
	/// always false here: the effect comes out of a small fixed table in the executable
	/// (<c>0049741c</c>) that is not ported, so nothing ever starts one.</para>
	///
	/// <para>One thing the original does here that this does not is the 25%-chance secondary effect
	/// it rolls on every structure hit (<c>FUN_004089bc</c>/<c>FUN_00408530</c>, a different effect
	/// pool from the impact effect above it).</para>
	/// </summary>
	public override int DirectFireHitTest(SimWorld world, WeaponShot shot) {
		bool wreck = Destroyed && Type.HulkTypeIndex != -1;

		short component = -1;
		int struckAt;

		if (wreck || !Type.HasCollisionModel) {
			struckAt = VolumeStruck(shot);
		} else if (!WithinReach(shot)) {
			return 0;
		} else {
			var hit = CollisionModel.Test(
				_collision, Transform3.Concat(WorldTransform, shot.MuzzleInverse),
				shot.Distance, shot.Clearance, _alive);

			struckAt = hit is { } found ? found.Distance + 1 : 0;
			component = hit?.ComponentIndex ?? -1;
		}

		if (struckAt == 0) {
			return 0;
		}

		// The damage goes in before the effect, and only while the structure is standing: a wreck is
		// still solid and still stops shots, it just has nothing left to lose.
		if (!Destroyed) {
			ApplyDamage(world.Random, component, shot.DamageArmor, shot.Owner);
		}

		// Always the armour array. Unlike a mech, a structure has no shields to flash and no
		// component health band to fall through, so this is the one branch that exists — and it is
		// the only place in the engine that reaches ImpactFxGroup.Armor at all.
		world.SpawnImpactEffect(
			world.PickImpactEffect(shot.ImpactFx(WeaponShot.ImpactFxGroup.Armor)),
			shot.Muzzle.TransformPoint(0, struckAt, 0));

		return struckAt;
	}

	/// <summary>
	/// The volume half of <c>FUN_00427da8</c>, narrowed to the single object this is called on.
	///
	/// <para>Two rejects before any grid work: <see cref="WithinReach"/>, then the structure's centre
	/// brought into the shot's frame and tested against a box — <b>X and Y only</b>, with Z left out
	/// entirely, which is the original's own test and not an omission here. Only then is the ray
	/// brought into shape space and marched.</para>
	/// </summary>
	/// <returns>How far along the ray the volume was entered, or zero for a miss.</returns>
	private int VolumeStruck(WeaponShot shot) {
		if (_volume is not { IsSolid: true } || !WithinReach(shot)) {
			return 0;
		}

		int reach = _shapeRadius + shot.Clearance;
		int limit = shot.Distance + reach;

		var muzzle = new Vec3i(shot.Muzzle.X, shot.Muzzle.Y, shot.Muzzle.Z);
		var center = shot.MuzzleInverse.TransformPoint(Position.X, Position.Y, Position.Z);
		if (center.X >= reach || center.X <= -reach || center.Y <= -reach || center.Y >= limit) {
			return 0;
		}

		var toShapeSpace = WorldTransform.Inverted();
		var start = toShapeSpace.TransformPoint(muzzle.X, muzzle.Y, muzzle.Z);
		var far = shot.Muzzle.TransformPoint(0, shot.Distance, 0);
		var end = toShapeSpace.TransformPoint(far.X, far.Y, far.Z);

		return _volume.Raycast(start, end, shot.Clearance, out var hit)
			? hit.ApproxDistanceTo(start) + 1
			: 0;
	}

	/// <summary>
	/// The coarse reject both hit paths open with, and the same one every hit test in the simulation
	/// starts from: muzzle to structure, against the ray's remaining length plus the shape's radius
	/// plus the shot's clearance. It keeps the transform work off everything nowhere near the shot.
	/// </summary>
	private bool WithinReach(WeaponShot shot) {
		var muzzle = new Vec3i(shot.Muzzle.X, shot.Muzzle.Y, shot.Muzzle.Z);
		return Position.ApproxDistanceTo(muzzle) <= _shapeRadius + shot.Clearance + shot.Distance;
	}

	/// <summary>
	/// <c>Base_ApplyDamage</c> (<c>FUN_00404d70</c>), the vtable <c>+0x74</c> — writes one
	/// component's health and, if that finished it, checks whether the structure has fallen.
	///
	/// <para><b>A component can die early, at random.</b> Past half its maximum, the original rolls
	/// once per tenth of the component's health the shot moved it through, each roll a 10% chance of
	/// finishing it outright (<c>rand &amp; 0xfff &lt;= 0x199</c>). So a big hit on a half-wrecked
	/// section is likely to bring it down before its stated hit points run out, and the same hit
	/// twice does not do the same thing.</para>
	///
	/// <para>Two things a kill does in the original that are not here: it plays the component's
	/// destruction effect (a table that is not ported, see
	/// <see cref="BaseComponentType.DestroyedEffect"/>), and it credits the kill to the shooter
	/// through the shooter's own vtable <c>+0x60</c>. It does fire the structure's mission action,
	/// which is the part that matters to a mission, and that is recorded on
	/// <see cref="Removed"/>'s behalf as <see cref="Destroyed"/> until mission actions exist.</para>
	/// </summary>
	/// <param name="random">The simulation's shared generator, for the early-destruction roll.</param>
	/// <param name="componentIndex">
	/// Which component was struck, or <c>-1</c> for "the hit geometry could not say", which the
	/// original resolves to component 0 rather than dropping the damage.
	/// </param>
	/// <param name="damage">The shot's armour damage.</param>
	/// <param name="attacker">Who fired, recorded on the component that falls.</param>
	public void ApplyDamage(SimRandom random, int componentIndex, int damage, SimObject? attacker) {
		if (Type.Invulnerable) {
			return;
		}

		int index = componentIndex == -1 ? 0 : componentIndex;
		if (index < 0 || index >= _alive.Length || !_alive[index]) {
			return;
		}

		var component = Type.Components[index];
		int taken = _damage[index] + damage;
		bool destroyed = component.MaxDamage <= taken;

		if (!destroyed && component.MaxDamage / 2 < taken) {
			int tenth = SimMath.Q16Divide(10, component.MaxDamage);
			int from = SimMath.Q16Multiply(_damage[index], tenth);
			int to = SimMath.Q16Multiply(taken, tenth);
			while (to > from) {
				bool finished = random.NextMasked(0xfff) <= 0x199;
				from++;
				if (finished) {
					destroyed = true;
					break;
				}
			}
		}

		if (!destroyed) {
			_damage[index] = taken;
			return;
		}

		_damage[index] = component.MaxDamage;
		_alive[index] = false;
		LastAttacker = attacker;

		if (DamageFraction == FullyDestroyed) {
			Destroyed = true;
		}
	}

	/// <summary>
	/// Who landed the shot that destroyed the last component to fall — the original writes it into
	/// that component's own record (<c>state+7</c>) so the kill can be credited. Nothing consumes it
	/// yet.
	/// </summary>
	public SimObject? LastAttacker { get; private set; }

	/// <summary>
	/// Sits the structure on the ground. Same treatment mechs get, and for the same reason: the
	/// mission states X and Y, and the terrain states Z.
	/// </summary>
	public override void Tick(SimWorld world) {
		Position = new Vec3i(Position.X, Position.Y, world.GroundHeightAt(Position));
	}
}
