using HercWorks.Core.Data.File.Dbsim;
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
	private readonly ColliderNode[] _collision;
	private readonly int _shapeRadius;

	// Damage taken per component, against BaseComponentType.MaxDamage -- the original's own
	// direction, counting up to the maximum rather than down from it (obj+0x205, stride 11).
	private readonly int[] _damage;

	// obj+0x201: whether each component is still standing. A destroyed one is skipped by both the
	// hit test and the damage path, so it stops absorbing fire entirely.
	private readonly bool[] _alive;

	// obj+0x205 +5 and +3: how many stages of its death sequence each component has left, and the
	// countdown to the next one. Both zero for a component that is not falling -- a live one, or one
	// that has already finished.
	private readonly short[] _deathStage;
	private readonly short[] _deathTimer;

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
	public BaseObject(BaseType type, ShapeVolume? volume, ColliderNode[]? collision, int shapeRadius) {
		Type = type;
		_volume = volume;
		_collision = collision ?? Array.Empty<ColliderNode>();
		_shapeRadius = shapeRadius != 0 ? shapeRadius : type.HitRadius;
		_damage = new int[type.Components.Length];
		_deathStage = new short[type.Components.Length];
		_deathTimer = new short[type.Components.Length];
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

	/// <inheritdoc />
	public override int ShapeRadius => _shapeRadius;

	/// <inheritdoc />
	/// <remarks>
	/// <b>Only a standing <see cref="BaseShapeSource.AnimatedLibrary"/> type blocks by radius</b> —
	/// the original's slot tests the same field that picks the model library. Every static type, and
	/// an animated one that has fallen to a hulk, blocks by <see cref="BlocksWalker"/> instead. The
	/// value is the type's <see cref="BaseType.HitRadius"/>, so a structure that blocks by radius
	/// blocks at the radius it is shot at.
	/// </remarks>
	public override int CollisionRadius =>
		Type.Source == BaseShapeSource.AnimatedLibrary && !(Destroyed && Type.HulkTypeIndex != -1)
			? Type.HitRadius
			: 0;

	/// <summary>
	/// The <c>BASES.DAT</c> type indices <c>Base_Construct</c> (<c>00405314</c>) sends down its last
	/// branch, which derives a further class and writes <see cref="Sim.TargetClass.Emplacement"/>
	/// (<c>0x00405848</c>) where every other branch writes <see cref="Sim.TargetClass.Structure"/>.
	/// The list is the switch's own case labels.
	/// </summary>
	private static readonly HashSet<int> EmplacementTypes = new() {
		0x2d, 0x2e, 0x2f, 0x30, 0x31, 0x32, 0x33, 0x34,
		0x37, 0x38, 0x39, 0x3a, 0x3b, 0x3c, 0x3d
	};

	/// <summary>
	/// The four type indices <c>Base_Construct</c> latches <c>obj+0x96</c> on for — structures that
	/// are radar masts, running an active scanner for as long as they stand. They are the only
	/// objects in a retail mission with a scanner on at spawn.
	/// </summary>
	private static readonly HashSet<int> ScannerTypes = new() { 5, 6, 0x1d, 0x1e };

	/// <inheritdoc />
	/// <remarks>
	/// Six of the 65 type indices (<c>0x0a</c>, <c>0x35</c>, <c>0x36</c> and <c>0x3e</c>-<c>0x40</c>)
	/// match no case in the original's switch, which leaves its object pointer uninitialised rather
	/// than classifying them; they are taken as ordinary structures here.
	/// </remarks>
	public override TargetClass TargetClass =>
		EmplacementTypes.Contains(Type.Index) ? TargetClass.Emplacement : TargetClass.Structure;

	/// <inheritdoc />
	public override bool ScannerActive => ScannerTypes.Contains(Type.Index);

	/// <inheritdoc />
	public override bool Neutralised => Destroyed;

	/// <summary>
	/// The structure's shape-to-world transform. A structure has no lean and no torso: its heading
	/// is the whole of its orientation, so this is a Z rotation with its world position in the
	/// translation.
	/// </summary>
	public Transform3 WorldTransform => WorldFrame;

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
	/// <para>It also rolls <see cref="HitDebrisOdds"/> in 4096 on every hit — a shade over a quarter —
	/// to shed <see cref="HitDebrisGroup"/> off the impact point.</para>
	/// </summary>
	public override int DirectFireHitTest(SimWorld world, WeaponShot shot) {
		bool wreck = Destroyed && Type.HulkTypeIndex != -1;

		short component = -1;
		short damage = shot.DamageArmor;
		int struckAt;

		if (wreck || !Type.HasCollisionModel) {
			struckAt = VolumeStruck(shot);

			// The one consumer of the plasma round's stash: a shot that arrives with both damage
			// figures at zero has been emptied by WeaponShot.StashDamage, and this branch — the volume
			// path only, never the sphere path — puts the armour figure back. See there.
			if (struckAt != 0 && shot.DamageArmor == 0 && shot.DamageShield == 0) {
				damage = shot.StashedDamageArmor;
			}
		} else if (!WithinReach(shot)) {
			return 0;
		} else {
			// No node-transform resolver: the engine has no posed node transforms for structures, so a
			// node-placed cluster is tested in the object's own frame. Only the eight animated types
			// carry any, and each keeps its body cluster in the object frame regardless.
			var hit = CollisionModel.Test(
				_collision, Transform3.Concat(WorldTransform, shot.MuzzleInverse),
				shot.Distance, shot.Clearance, ComponentAlive);

			struckAt = hit is { } found ? found.Distance + 1 : 0;
			component = hit?.ComponentIndex ?? -1;
		}

		if (struckAt == 0) {
			return 0;
		}

		// The damage goes in before the effect, and only while the structure is standing: a wreck is
		// still solid and still stops shots, it just has nothing left to lose.
		if (!Destroyed) {
			ApplyDamage(world.Random, component, damage, shot.Owner);
		}

		// Always the armour array. Unlike a mech, a structure has no shields to flash and no
		// component health band to fall through, so this is the one branch that exists — and it is
		// the only place in the engine that reaches ImpactFxGroup.Armor at all.
		var point = shot.Muzzle.TransformPoint(0, struckAt, 0);
		world.SpawnImpactEffect(
			world.PickImpactEffect(shot.ImpactFx(WeaponShot.ImpactFxGroup.Armor)), point);

		if (world.Random.NextMasked(0xfff) < HitDebrisOdds) {
			world.SpawnDebris(HitDebrisGroup, point, StructureDebris(world));
		}

		return struckAt;
	}

	/// <inheritdoc cref="DirectFireHitTest" />
	private const int HitDebrisOdds = 0x401;

	/// <inheritdoc cref="DirectFireHitTest" />
	private const short HitDebrisGroup = 1;

	/// <summary>
	/// <c>BASE_DEB</c> — the debris table the structure paths install as the alternate database
	/// before every throw they make, exactly as <c>Base_ExplosionSequenceTick</c> installs
	/// <c>g_DebrisStructure</c>. Null when the install has no such table.
	/// </summary>
	private static DebrisDatabase? StructureDebris(SimWorld world) =>
		world.Debris?.Database(DebrisDatabase.StructureName);

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

		// And the part starts to fall. A component that names no sequence is simply gone the instant
		// its health runs out; one that names a sequence is given that many stages and the first
		// interval, and DeathSequenceTick takes it from there.
		if (StructureDeathSequence.At(component.DestroyedEffect) is { } sequence) {
			_deathStage[index] = sequence.StageCount;
			_deathTimer[index] = StructureDeathSequence.StageInterval;
		}
	}

	/// <summary>
	/// Whether a part is still coming down — it has been destroyed but has stages of its death
	/// sequence left to run. A structure that has fallen but whose parts are still collapsing reads
	/// <see cref="Destroyed"/> and this at once.
	/// </summary>
	public bool Collapsing(int index) =>
		index >= 0 && index < _deathStage.Length && _deathStage[index] != 0;

	/// <summary>
	/// Whether a walking machine is stopped by this structure's <b>collision volume</b> — the second
	/// of the two ways a building blocks movement, and the one that covers everything
	/// <see cref="CollisionRadius"/> does not. The two are exact complements: a standing animated
	/// type blocks by its radius, and every static type plus every animated wreck blocks by its
	/// volume, so no structure is walked through and none is tested twice.
	///
	/// <para>The exception is a structure that is <i>gone</i>: destroyed, leaving no wreck, and built
	/// from a single component. That one stops blocking entirely.</para>
	///
	/// <para><b>The footprint tested is not the one a shot is tested against.</b> The original's walk
	/// test omits the grid-origin shift its ray march applies, so the two sample the volume displaced
	/// from each other by the grid's centre. Reproduced rather than corrected — see
	/// docs/simulation/hit-detection.md, "The collision volume".</para>
	/// </summary>
	/// <param name="point">Where the machine is trying to stand, in world units.</param>
	public bool BlocksWalker(Vec3i point) {
		bool wreck = Destroyed && Type.HulkTypeIndex != -1;
		if (Type.Source == BaseShapeSource.AnimatedLibrary && !wreck) {
			return false;
		}

		if (Type.HulkTypeIndex == -1 && Destroyed && Type.Components.Length <= 1) {
			return false;
		}

		if (_volume is not { IsSolid: true }
				|| SimMath.FastMagnitude2D(point.X - Position.X, point.Y - Position.Y) >= _shapeRadius) {
			return false;
		}

		// Only the heading matters, and only in 2D: the original transposes the transform's XY block
		// and rotates the offset by it rather than inverting the whole thing.
		var local = WorldTransform.Inverted().RotateVector(point.X - Position.X, point.Y - Position.Y, 0);
		return _volume.HeightAround(local.X, local.Y, 0) != 0;
	}

	/// <summary>
	/// What a blast does to a building. It shares nothing with a machine's but the shape of the
	/// falloff: a structure has no shields to absorb anything, no facing to be caught from behind,
	/// and its parts stand where the type record says rather than where an animation has put them.
	///
	/// <para><b>Every live component is measured, not a random subset.</b> The original's
	/// per-component draw is compared against a ceiling one above the largest value its mask can
	/// produce, so the test never fails. The draw is kept because it advances the shared generator,
	/// which is what everything downstream of it sees.</para>
	///
	/// <para>Two gates come first, the same two the hit test opens with: an
	/// <see cref="BaseType.Invulnerable"/> type takes nothing, and a structure is blast-damageable
	/// only through its <c>BASECOL.DAT</c> model — <b>a type without one stands in a blast untouched
	/// however close it is</b>, where direct fire would still hurt it through the shape's collision
	/// volume. A wreck that has a hulk is skipped for the reason the hit test switches it to the
	/// volume path: the spheres belong to the building that used to be there.</para>
	/// </summary>
	public override void ExplosiveDamage(SimWorld world, short damage, Vec3i hitPoint, int blastRadius,
			SimObject? attacker) {
		bool wreck = Destroyed && Type.HulkTypeIndex != -1;
		if (Type.Invulnerable || wreck || !Type.HasCollisionModel || blastRadius <= 0) {
			return;
		}

		for (int i = 0; i < Type.Components.Length; i++) {
			if (!_alive[i] || world.Random.NextMasked(0xfff) >= BlastConsiderationCeiling) {
				continue;
			}

			int distance = ComponentPosition(i).ApproxDistanceTo(hitPoint);
			if (distance >= blastRadius) {
				continue;
			}

			ApplyDamage(world.Random, i, (blastRadius - distance) * damage / blastRadius, attacker);
		}
	}

	/// <summary>
	/// The ceiling the blast's per-component draw is compared against — <c>0x1004</c>, against a draw
	/// masked to <c>0xfff</c>. Written out rather than folded away so the one place the original
	/// could have meant otherwise stays visible; see <see cref="ExplosiveDamage"/>.
	/// </summary>
	private const int BlastConsiderationCeiling = 0x1004;

	/// <summary>
	/// Where one of this building's parts stands in the world, which is what a blast measures its
	/// falloff from. The component's own <see cref="BaseComponentType.Position"/> put through
	/// <see cref="WorldTransform"/>, and nothing else: unlike a machine's there is no node to resolve
	/// and no animation to consult, because a structure's parts do not move.
	///
	/// <para>The type record is the only place the point comes from — the <c>BASECOL.DAT</c> spheres
	/// a shot is tested against are <i>not</i> consulted here, and several types put a component's
	/// blast point above the geometry that stops bullets.</para>
	/// </summary>
	public Vec3i ComponentPosition(int index) {
		if (index < 0 || index >= Type.Components.Length) {
			return Position;
		}

		var local = Type.Components[index].Position;
		return WorldTransform.TransformPoint(local.X, local.Y, local.Z);
	}

	/// <summary>
	/// Who landed the shot that destroyed the last component to fall — the original writes it into
	/// that component's own record (<c>state+7</c>) so the kill can be credited. Nothing consumes it
	/// yet.
	/// </summary>
	public SimObject? LastAttacker { get; private set; }

	/// <summary>
	/// Sits the structure on the ground and runs whatever is still falling off it. Same ground
	/// treatment mechs get, and for the same reason: the mission states X and Y, and the terrain
	/// states Z.
	/// </summary>
	public override void Tick(SimWorld world) {
		DeathSequenceTick(world);
		Position = new Vec3i(Position.X, Position.Y,
			Sunk ? SunkDepth : world.GroundHeightAt(Position));
	}

	/// <summary>
	/// <c>Base_DeathSequenceTick</c> — steps every part that is coming down through the next stage of its
	/// <see cref="StructureDeathSequence"/>. This is the whole of how a building collapses, and it
	/// runs backwards: the stage counter starts at the sequence's own length and each expiry of the
	/// stage timer takes one off it.
	///
	/// <list type="bullet">
	/// <item><b>The early stages are smoke.</b> A secondary explosion is scattered at a random point
	/// inside the part's <see cref="BaseComponentType.SmokeSpread"/> box around its
	/// <see cref="BaseComponentType.Position"/>. Stage 4 exactly also cascades: every part hanging off
	/// this one is finished off, so a tower goes when the block under it does.</item>
	/// <item><b>Stage 1 is the collapse.</b> The sequence's own explosion goes off, the part throws
	/// its debris out of <c>BASE_DEB</c>, and then either the part is redrawn as its own rubble or —
	/// for the last part of a type that leaves a wreck — the whole structure switches over to its
	/// hulk.</item>
	/// <item><b>Stage 0 is the fire</b>, and it is where the two scales meet: a type that states a
	/// whole-structure fire lights that one once every part is down, and every other part lights its
	/// own. A structure whose type and part both state none, and which has exactly one part, is
	/// dropped through the floor instead — <see cref="SunkDepth"/>, the original's own way of making
	/// a small object disappear.</item>
	/// </list>
	///
	/// <para>The part changes through <see cref="CellFrames"/>: its sequence steps to
	/// <see cref="CollapsedCell"/> and the renderer draws that cell of it instead, which for a
	/// structure is the part's rubble rather than nothing.</para>
	/// </summary>
	private void DeathSequenceTick(SimWorld world) {
		for (int i = 0; i < Type.Components.Length; i++) {
			if (_deathStage[i] == 0
					|| StructureDeathSequence.At(Type.Components[i].DestroyedEffect) is not { } sequence
					|| SimMath.CountdownTimerTick(ref _deathTimer[i]) != 0) {
				continue;
			}

			var component = Type.Components[i];
			_deathStage[i]--;

			switch (_deathStage[i]) {
				case 0:
					LightTheFire(world, component);
					break;

				case 1:
					Collapse(world, i, component, sequence);
					break;

				default:
					ScatterSmoke(world, component, sequence);
					_deathTimer[i] = StructureDeathSequence.StageInterval;
					if (_deathStage[i] == CascadeStage) {
						FinishDependents(world, i);
					}

					break;
			}
		}
	}

	/// <summary>
	/// The last stage. A type that states a whole-structure fire uses it in place of the part's own,
	/// but only once every part is down (<see cref="EveryPartGone"/>); otherwise the part burns
	/// alone. A structure with neither, and only one part, is dropped out of the world.
	/// </summary>
	private void LightTheFire(SimWorld world, BaseComponentType component) {
		if (Type.HulkTypeIndex == -1 && component.DestroyedSubShape == -1
				&& Type.Components.Length == 1) {
			// Dropped through the floor rather than taken out of the list: the original writes the
			// depth onto the object's Z and leaves it where it is, so it is still shootable and still
			// blocks nothing, it is simply nowhere the camera looks. Its own Tick puts it back on the
			// terrain every frame, so the sunk depth has to survive that -- see Sunk.
			Sunk = true;
			return;
		}

		if (Type.FireShapeIndex < 0 || component.FireShapeIndex < 0) {
			if (component.FireShapeIndex >= 0) {
				world.SpawnFire(this, -1, component.EmitPoint, component.FireShapeIndex);
			}

			return;
		}

		if (EveryPartGone()) {
			world.SpawnFire(this, -1, Type.FirePoint, Type.FireShapeIndex);
		}
	}

	/// <summary>
	/// The collapse. The explosion is the type's own when this was the last part standing and the
	/// part's own otherwise, and the sequence decides whether it goes off at the emission point or at
	/// the structure's origin. Then the debris, and then the shape change.
	/// </summary>
	private void Collapse(SimWorld world, int index, BaseComponentType component,
			StructureDeathSequence sequence) {
		bool whole = EveryPartGone() && Type.FireShapeIndex >= 0 && component.FireShapeIndex >= 0;

		if (whole) {
			SpawnCollapseExplosion(world, Type.DestroyedEffect, Type.FirePoint, ref _deathTimer[index]);
		} else if (component.DestroyedEffect >= 0) {
			SpawnCollapseExplosion(world, component.DestroyedEffect, component.EmitPoint,
				ref _deathTimer[index]);
		}

		ThrowDebris(world, component);

		// A type that leaves a wreck switches to it when its last part falls; anything else loses the
		// part's own geometry and takes everything hanging off that part with it.
		if (whole && Type.HulkTypeIndex != -1) {
			ShowingHulk = true;
			return;
		}

		if (component.DestroyedSubShape >= 0) {
			CellFrames[component.DestroyedSubShape] = CollapsedCell;
			FinishDependents(world, index);
		}
	}

	/// <summary>
	/// <c>Base_CollapseExplosion</c> — one collapse explosion, and the stage hold that goes with it. The
	/// sequence's <see cref="StructureDeathSequence.ExplodeAtOrigin"/> decides whether the point is
	/// the structure's own or the offset put through its frame.
	/// </summary>
	private void SpawnCollapseExplosion(SimWorld world, int sequenceIndex, Vec3i point,
			ref short timer) {
		if (StructureDeathSequence.At(sequenceIndex) is not { } sequence) {
			return;
		}

		world.SpawnImpactEffect(sequence.Explosion, sequence.ExplodeAtOrigin
			? Position
			: WorldTransform.TransformPoint(point.X, point.Y, point.Z));

		timer = sequence.CollapseHold;
	}

	/// <summary>
	/// <c>Base_ThrowDebris</c> — the debris a collapsing part throws, out of <c>BASE_DEB</c>. A part whose
	/// group is above <see cref="LargeDebrisGroup"/> throws two further fixed groups on top of its
	/// own, which is the difference between a shed falling over and a hangar coming apart.
	/// </summary>
	private void ThrowDebris(SimWorld world, BaseComponentType component) {
		var point = WorldTransform.TransformPoint(
			component.EmitPoint.X, component.EmitPoint.Y, component.EmitPoint.Z);
		var table = StructureDebris(world);

		world.SpawnDebris(component.DebrisGroup, point, table);

		if (component.DebrisGroup > LargeDebrisGroup) {
			world.SpawnDebris(LargeDebrisExtraA, point, table);
			world.SpawnDebris(LargeDebrisExtraB, point, table);
		}
	}

	/// <summary>
	/// <c>Base_FinishDependents</c> — finishes off every part that hangs off this one. Recursive in the
	/// original and here: a chain of dependent parts all comes down together, each through the
	/// ordinary damage path so each starts its own death sequence.
	/// </summary>
	private void FinishDependents(SimWorld world, int index) {
		for (int i = 0; i < Type.Components.Length; i++) {
			if (Type.Components[i].ParentComponent != index
					|| _damage[i] >= Type.Components[i].MaxDamage) {
				continue;
			}

			ApplyDamage(world.Random, i, Type.Components[i].MaxDamage, LastAttacker);
			FinishDependents(world, i);
		}
	}

	/// <summary>
	/// A smoke stage: one secondary explosion at a random point inside the part's own spread box
	/// around its position.
	/// </summary>
	private void ScatterSmoke(SimWorld world, BaseComponentType component,
			StructureDeathSequence sequence) {
		var spread = component.SmokeSpread;
		var local = new Vec3i(
			component.Position.X + world.Random.NextBelow((short)(spread.X * 2)) - spread.X,
			component.Position.Y + world.Random.NextBelow((short)(spread.Y * 2)) - spread.Y,
			component.Position.Z + world.Random.NextBelow((short)(spread.Z * 2)) - spread.Z);

		world.SpawnImpactEffect(sequence.SmokeExplosion,
			WorldTransform.TransformPoint(local.X, local.Y, local.Z));
	}

	/// <summary>
	/// <c>Base_EveryPartGone</c> — whether every part either has no fire of its own or is fully damaged,
	/// which is the original's test for "this structure, and not just this part, has gone".
	/// </summary>
	private bool EveryPartGone() {
		for (int i = 0; i < Type.Components.Length; i++) {
			if (Type.Components[i].FireShapeIndex >= 0 && _damage[i] < Type.Components[i].MaxDamage) {
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Whether this structure has been dropped out of sight — the last stage of a type that leaves no
	/// wreck, has one part, and nothing to hide. See <see cref="SunkDepth"/>.
	/// </summary>
	public bool Sunk { get; private set; }

	/// <summary>
	/// Whether this structure has switched over to its <see cref="BaseType.HulkTypeIndex"/> wreck — a
	/// root of <c>dgs\BHULKS.DGS</c> drawn in place of the building. The original writes the hulk
	/// shape straight onto the object's model instance; here it is a flag the scene reads.
	/// </summary>
	public bool ShowingHulk { get; private set; }

	/// <summary>
	/// The structure's shape instance's per-sequence cell-frame array, which is what a collapsed
	/// part's geometry disappears through — see <see cref="ShapeCellFrames"/>.
	/// </summary>
	public override ShapeCellFrames CellFrames { get; } = new();

	/// <summary>
	/// The cell a collapsed part's sequence is stepped to — the original's own literal, written
	/// straight into the shape instance's array at
	/// <see cref="BaseComponentType.DestroyedSubShape"/>.
	///
	/// <para>It is <b>1</b>, not the machine's <see cref="ComponentDamage.DestroyedCell"/>, and it
	/// does not blank the part: a structure's parts are two-cell flipbooks whose <i>second cell is
	/// its own rubble</i> — different geometry, not none. A collapsed building is redrawn as its
	/// wreckage part by part, where a machine's destroyed component simply comes off.</para>
	/// </summary>
	public const short CollapsedCell = 1;

	/// <summary>
	/// The stage at which a part takes its dependents with it — the original's literal 4, tested for
	/// equality rather than as a threshold, so a sequence shorter than five stages never cascades this
	/// way at all.
	/// </summary>
	private const int CascadeStage = 4;

	/// <summary>
	/// The debris group above which a collapsing part throws extra wreckage — the original's literal
	/// 5, which is the last group in <c>DEF_DEB</c>. So the test really reads "does this part throw
	/// structure debris rather than the shared default".
	/// </summary>
	public const short LargeDebrisGroup = 5;

	/// <inheritdoc cref="LargeDebrisGroup" />
	private const short LargeDebrisExtraA = 10;

	/// <inheritdoc cref="LargeDebrisGroup" />
	private const short LargeDebrisExtraB = 12;

	/// <summary>
	/// Where the original drops a structure that leaves nothing behind — <c>-100000</c> written
	/// straight onto its Z, well under any terrain, so it is simply no longer anywhere the camera
	/// looks.
	/// </summary>
	public const int SunkDepth = -100000;
}
