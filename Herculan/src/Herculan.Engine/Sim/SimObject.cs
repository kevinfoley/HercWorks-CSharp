using Herculan.Engine.Numerics;
using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

/// <summary>
/// What an object counts as when something is deciding whether to shoot it — the shared
/// <c>obj+0x1a8</c> that every constructor writes and every target filter reads. Every constructor
/// writes <see cref="None"/> first and overwrites it, so an object that never finishes construction
/// stays unclassified. Which type gets which class is in docs/simulation/target-selection.md,
/// "Object classification".
/// </summary>
public enum TargetClass : short {
	/// <summary>Unclassified — the <c>0xffff</c> every constructor starts from.</summary>
	None = -1,

	/// <summary>A HERC.</summary>
	Herc = 0,

	/// <summary>An ordinary structure.</summary>
	Structure = 1,

	/// <summary>A flyer or ground vehicle.</summary>
	Flyer = 2,

	/// <summary>
	/// The second structure family — the <c>BASES.DAT</c> types <c>Base_Construct</c> gives a
	/// further-derived class of their own and a hit radius of 10 rather than 5.
	/// </summary>
	Emplacement = 3
}

/// <summary>
/// Base class for everything the simulation ticks — mechs, projectiles, flyers. Traditional OOP
/// and virtual dispatch rather than ECS, mirroring the vtable shape of DBSIM's own simulation
/// objects: see docs/engine/planning.md, "Simulation object architecture", for the evidence.
///
/// <para>Only the slots the engine currently needs are declared. The rest are identified in the
/// disassembly and get added alongside the systems that call them, so that each arrives as a
/// translation rather than a redesign.</para>
/// </summary>
public abstract class SimObject {
	/// <summary>Position in world units, X/Y on the ground plane and Z up (see <see cref="Vec3i"/>).</summary>
	public Vec3i Position { get; set; }

	/// <summary>Facing as a binary angle (see <see cref="BinaryAngle"/>).</summary>
	public int Heading { get; set; }

	/// <summary>
	/// <c>obj+0x4b</c> — this object's slot in the world's single live-object list, written by
	/// <c>ObjectList_Add</c> (<c>00411dd4</c>) as the object joins it and never changed after.
	///
	/// <para>It is not a diagnostic: it is the index everything that keeps a <i>per-object</i> table
	/// uses to address a row. <see cref="Detects"/> is one such table and the line-of-sight cache
	/// behind <see cref="Detection"/> is another, and both are flat arrays on each object indexed by
	/// the other object's slot. -1 until the object is added.</para>
	/// </summary>
	public int ListIndex { get; internal set; } = -1;

	/// <summary>
	/// Whose side this object is on. In the original it is not on the object at all — it is
	/// <c>group[+0x12]</c>, reached through the object's own group pointer at <c>obj+0x45</c>, and
	/// every "friend or foe" test in the simulation is a byte comparison of two objects' copies.
	/// Groups are not modelled here, so the placement's own side is copied onto the object at spawn;
	/// nothing in the original ever changes it mid-mission.
	/// </summary>
	public MissionSide Side { get; set; } = MissionSide.Human;

	/// <summary>
	/// <c>obj+0x1a8</c> — see <see cref="Sim.TargetClass"/>. The base leaves it unclassified, which is
	/// the value the original's constructors write before overwriting it, and the right answer for
	/// anything that is not a combatant.
	/// </summary>
	public virtual TargetClass TargetClass => TargetClass.None;

	/// <summary>
	/// <c>obj+0x45</c> — the mission group that placed this object, and the record every "is this one
	/// of ours" test reads its side off. It is also what the AI is driven from: see
	/// <see cref="MissionGroup"/>.
	/// </summary>
	public MissionGroup? Group { get; internal set; }

	/// <summary>
	/// Whether this object is out of the fight — the <c>obj+0x99 || obj+0xa4</c> pair that
	/// <c>TargetSelect_CanTarget</c> (<c>00433174</c>), <c>Detection_Sweep</c> (<c>004128f8</c>) and
	/// the AI's <c>Ai_ShouldAbandonTarget</c> all spell out identically. The AI's own copies add a
	/// third flag — see <see cref="OutOfAction"/>.
	///
	/// <para>Both halves count, which is worth saying plainly: a HERC whose legs are gone is no
	/// longer selectable even though it is still standing, still shooting and still solid. That is
	/// the original's behaviour, not a simplification here.</para>
	/// </summary>
	public virtual bool Neutralised => false;

	/// <summary>
	/// <c>obj+0x99</c> alone — <b>destroyed</b>, without the crippled half
	/// <see cref="Neutralised"/> folds in. The two are different tests in the original and different
	/// answers for a HERC: one whose legs are gone is neutralised but not destroyed.
	///
	/// <para>Only the three shootable classes override it; nothing else can be destroyed.</para>
	/// </summary>
	public virtual bool Destroyed => false;

	/// <summary>
	/// <c>obj+0xb7</c> — whether this object cannot be hurt at all, which also puts it outside the
	/// AI's candidate set entirely (<c>Ai_IsTargetable</c>). Only a structure can be: <c>Base_Construct</c>
	/// latches it from <c>BASES.DAT +0x1e</c>.
	/// </summary>
	public virtual bool Invulnerable => false;

	/// <summary>
	/// Object vtable <c>+0x40</c> — how far gone this object is, as a Q8 fraction: 0 pristine, 256
	/// destroyed. Every shootable class implements it; the classes that cannot be shot answer 0.
	///
	/// <para>Two readers outside the class that owns the number: the AI's flee check, and the
	/// mission-order layer's condition tier — see <see cref="MissionGroup"/>.</para>
	/// </summary>
	public virtual int OverallDamage => 0;

	/// <summary>
	/// <c>obj+0x95</c> — whether this object is currently showing on radar. Set by the detection
	/// sweep when an active scanner on either side of a pair has line of sight to it, and cleared
	/// wholesale each time its own contact list decays. Distinct from being a known contact
	/// (<see cref="Detects"/>): radar visibility is a property of the object, a contact is a
	/// property of the pair.
	/// </summary>
	public bool RadarVisible { get; internal set; }

	/// <summary>
	/// <c>obj+0x96</c> — whether this object's active scanner is running. The pilot toggles it
	/// (<c>Mech_ToggleRadarMode</c>, <c>0041b468</c>); <c>Base_Construct</c> latches it on for
	/// structure types 5, 6, <c>0x1d</c> and <c>0x1e</c>, which is what makes those buildings radar
	/// masts. It both extends what this object can see and makes it visible at range to everything
	/// else, and it is one of the two emissions the anti-radiation missile homes on.
	/// </summary>
	public virtual bool ScannerActive => false;

	/// <summary>
	/// <c>obj+0xa1</c> — whether this object's jammer is running. Nothing in the engine turns one on
	/// yet; it is declared because the two systems that read it (the anti-radiation missile's
	/// emission gate and the ECM spoofing roll) are both reachable now that a target can be selected.
	/// </summary>
	public virtual bool JammerActive => false;

	/// <summary>
	/// <b>Where this object is aimed at</b> — vtable <c>+0x24</c>, which both guidance routines
	/// (<c>Rocket_HomingSteer</c>, <c>0040a254</c>; <c>Bullet_HomingSteer</c>, <c>0040aff0</c>) and
	/// the HUD's target indicator (<c>Player_ResolveTargetAimPoint</c>, <c>0041b728</c>) take
	/// instead of the object's position.
	///
	/// <para>The base returns the origin, which is what a flyer and a structure both keep. Only
	/// <see cref="MechObject"/> overrides it — see there for which node it names and why aiming at a
	/// HERC's position puts a missile between its feet, and docs/simulation/target-selection.md,
	/// "Aim point", for the fallback every caller shares.</para>
	/// </summary>
	public virtual Vec3i AimPoint => Position;

	/// <summary>
	/// Height above this object's origin that the detection sweep sights from and to — the
	/// <c>+0x1c</c> of the same vtable <c>+0x24</c> record, which is that node transform's
	/// <i>model-space</i> Z. <see cref="Detection.LineOfSight"/> raises both ends of its terrain ray
	/// by it.
	///
	/// <para>The base is <c>Detection_LineOfSight</c>'s (<c>00412608</c>) own literal 500, used
	/// whenever that slot returns nothing — so a flyer and a structure always sight from 500 and only
	/// a HERC sights from its own geometry.</para>
	/// </summary>
	public virtual int SightHeight => Detection.DefaultSightHeight;

	/// <summary>
	/// The aim offset the sensor and target-selection arcs are measured from, on top of
	/// <see cref="Heading"/> — vtable <c>+0x3c</c>, which is a mech's turret twist and zero for
	/// everything else.
	/// </summary>
	public virtual short AimTwist => 0;

	/// <summary>
	/// Vtable <c>+0x38</c> — how fast this object is travelling, in the units the rest of the
	/// simulation quotes distances in. Zero for a structure, which is why the AI leads a shot at a
	/// machine and fires straight at a building.
	/// </summary>
	public virtual short TravelSpeed => 0;

	/// <summary>
	/// The AI's own "out of the fight" test: <c>+0x99</c>, <c>+0xa4</c> and <c>+0xa5</c> together,
	/// where <see cref="Neutralised"/> is the first two. Only a HERC can answer the third — it is the
	/// no-weapons-left latch — so everything else answers the same as <see cref="Neutralised"/>.
	///
	/// <para>Deliberately not folded into <see cref="Neutralised"/>: the detection sweep, the player's
	/// target selection and a group's condition tier all read that one, and none of them consults
	/// <c>+0xa5</c> in the original. The one place that does is a guard order's rival test — see
	/// <c>MissionGroup.IsWipedOut</c>, which is why that reads this and not <see cref="Neutralised"/>.</para>
	/// </summary>
	public virtual bool OutOfAction => Neutralised;

	/// <summary>
	/// Vtable <c>+0x34</c> — the shield facing <paramref name="heading"/> points at, front within
	/// ±90° and rear outside it. Zero for everything but a HERC.
	/// </summary>
	public virtual short ShieldByHeading(short heading) => 0;

	/// <summary>
	/// <c>obj+0xa3</c> — whether this object is the machine the player is flying. The detection tick
	/// treats it specially twice over: it is swept <b>last</b>, after every other friendly object, and
	/// a contact it makes for itself is not shared with its side. See <see cref="Detection.Tick"/>.
	/// </summary>
	public virtual bool LocallyPiloted => false;

	/// <summary>
	/// <c>obj+0x1a2</c> — how many machines currently hold this object as their selected target.
	/// Maintained by <see cref="MechObject.Target"/>'s setter, which is the only place the original
	/// touches it either (every writer of <c>mech+0x1a4</c> decrements the old target's counter and
	/// increments the new one's).
	/// </summary>
	public int TargetedBy { get; internal set; }

	/// <summary>
	/// Whether this object holds <paramref name="other"/> as a known contact — one row of
	/// <c>obj+0xc2</c>, the flat per-object contact table addressed by
	/// <see cref="ListIndex"/>. See <see cref="Detection"/> for what fills and empties it.
	/// </summary>
	public bool Detects(SimObject other) =>
		other.ListIndex >= 0 && other.ListIndex < _contacts.Length && _contacts[other.ListIndex];

	/// <summary>Sets one row of this object's contact table. Internal: only <see cref="Detection"/> writes it.</summary>
	internal void SetDetects(SimObject other, bool detected) {
		if (other.ListIndex >= 0 && other.ListIndex < _contacts.Length) {
			_contacts[other.ListIndex] = detected;
		}
	}

	/// <summary>
	/// Grows the two per-object tables to cover a world of <paramref name="objectCount"/> objects.
	/// The original allocates each object whole, with both tables sized to the sim's fixed object
	/// cap; this grows on demand because <see cref="SimWorld"/> has no cap.
	/// </summary>
	internal void EnsureTableSize(int objectCount) {
		if (_contacts.Length >= objectCount) {
			return;
		}

		Array.Resize(ref _contacts, objectCount);
		Array.Resize(ref _lineOfSight, objectCount);
	}

	/// <summary>One row of the line-of-sight cache at <c>obj+0x132</c>.</summary>
	internal bool LineOfSightTo(SimObject other) =>
		other.ListIndex >= 0 && other.ListIndex < _lineOfSight.Length && _lineOfSight[other.ListIndex];

	/// <inheritdoc cref="LineOfSightTo"/>
	internal void SetLineOfSightTo(SimObject other, bool clear) {
		if (other.ListIndex >= 0 && other.ListIndex < _lineOfSight.Length) {
			_lineOfSight[other.ListIndex] = clear;
		}
	}

	/// <summary>
	/// <c>obj+0x1e2</c>'s value — the countdown that rate-limits the line-of-sight cache. Reloaded
	/// with 5000 plus a roll of 1000 by <see cref="Detection.LineOfSight"/>; ticked once per
	/// simulation step by <see cref="Detection.Tick"/>.
	/// </summary>
	internal short SightCacheTimer;

	/// <summary>
	/// <c>obj+0x1e5</c>'s value — the countdown between passes of
	/// <see cref="Detection.DecayContacts"/>. Reloaded with 10000 plus a roll of 1000, so an object
	/// re-examines what it thinks it can see a few times a minute rather than every tick, and the
	/// roll keeps a mission's objects from all doing it on the same one.
	/// </summary>
	internal short ContactDecayTimer;

	private bool[] _contacts = Array.Empty<bool>();
	private bool[] _lineOfSight = Array.Empty<bool>();

	/// <summary>
	/// Whether the object is still part of the simulation. DBSIM's per-frame tick
	/// (<c>Sim_MainTick</c>, <c>0045f464</c>) walks its global object lists and skips entries flagged
	/// removed rather than compacting the list mid-walk; <see cref="SimWorld"/> does the same.
	/// </summary>
	public bool Removed { get; set; }

	/// <summary>
	/// Whether the object is built but has not entered the mission yet — <b>it exists, but it is not
	/// in the world</b>. It is not drawn, not simulated, and not collided with, exactly as if it had
	/// not spawned; the position it holds is a placeholder its arrival overwrites.
	///
	/// <para>The original spells this as one pointer, the group record's <c>+0x14</c>, and so does
	/// this engine: the flag lives on <see cref="MissionGroup.AwaitingDeployment"/> and <b>this is a
	/// read of it</b>, not a copy, because arrival is a group operation and two flags could disagree
	/// about a group half-way through one. An object with no group is in the mission.</para>
	///
	/// <para>Its three test sites, why an undeployed group's placed position is meaningless, and how
	/// such a group arrives are in docs/simulation/mission-deployment.md.</para>
	/// </summary>
	public bool AwaitingDeployment => Group is { AwaitingDeployment: true };

	/// <summary>
	/// <c>mech+0x29c</c> — which of <c>str\PILOTS.STR</c>'s 36 pilots flies this machine, or -1 for
	/// one no pilot is named for. Only the player's own squad ever carries one, from
	/// <c>player.mec</c>; it names that machine's comm box and picks the portrait that talks in it.
	/// See <see cref="Content.PilotRoster"/>.
	/// </summary>
	public int PilotIndex { get; set; } = -1;

	/// <summary>
	/// <c>obj+0x1b2</c> — the mission action this object fires when it is <b>engaged</b>: a hostile
	/// that already has contact on it has closed to <see cref="Detection.EngagementRange"/>. Set from
	/// its roster record's own ref; see <c>ScriptSpawnRecordExport.EngagementActionRef</c>.
	/// </summary>
	public MissionActionState? EngagementAction { get; set; }

	/// <summary>
	/// <c>obj+0x1b6</c> — the mission action this object fires when it is <b>defeated</b>, from its
	/// own roster record. Four sites fire it and they are the four ways an object stops being a
	/// threat: a machine, a flyer or a structure being destroyed, and a machine running out of
	/// working weapons. <b>It is not a death action</b>, which is why it is not named for one.
	///
	/// <para><b>This is how a retail mission chains its waves</b>: defeating the machine in front of
	/// the player is what brings the next group in. See docs/simulation/mission-deployment.md.</para>
	/// </summary>
	public MissionActionState? DefeatAction { get; set; }

	/// <summary>
	/// <c>obj+0x9e</c> — whether this object has been closed with by an enemy that can see it. Set by
	/// <see cref="Detection.Sweep"/> alongside the engagement action, on both parties at once.
	/// </summary>
	public bool Engaged { get; internal set; }

	/// <summary>
	/// <c>obj+0x9f</c> — this object has reached what the mission set it. Written in exactly one
	/// place, <see cref="MechObject.PlayerThink"/>, and only ever onto the player's own machine: the
	/// order target has come into range, or the machine has closed on its goal position. Read back by
	/// the AI's group-report cluster (<c>FUN_00412ef4</c>).
	/// </summary>
	public bool MissionGoalReached { get; internal set; }

	/// <summary>
	/// <c>obj+0xa0</c> — this object has completed a data link. Written in the same one place and,
	/// again, only onto the player's machine, at the end of the four-message transfer sequence. It is
	/// what objective conditions 3, 4, 9 and 10 read — see <see cref="World.MissionObjective"/>.
	/// </summary>
	public bool DataLinkComplete { get; internal set; }

	/// <summary>
	/// Activates <see cref="EngagementAction"/>, if there is one. The original also gates this on
	/// <c>obj+0xa2</c> being clear; no writer of that byte has been located, so it is not modelled
	/// and the gate reads as open. It would only ever suppress a second activation, which
	/// <see cref="MissionActionState.Activate"/> already refuses.
	/// </summary>
	internal void ActivateEngagementAction(SimWorld world) => EngagementAction?.Activate(world);

	/// <summary>
	/// Activates <see cref="DefeatAction"/>, if there is one. Every site guards on the object not already
	/// being in that state, so it goes off once — and <see cref="MissionActionState.Activate"/> is
	/// one-shot regardless.
	/// </summary>
	internal void ActivateDefeatAction(SimWorld world) => DefeatAction?.Activate(world);

	/// <summary>
	/// The object's body radius, in world units. The blast sweep subtracts it from every candidate's
	/// distance before comparing against the blast radius, and the collision test uses the moving
	/// machine's as its own half of the gap.
	///
	/// <para>It is not the drawn model's size (<see cref="ShapeRadius"/>) and not what a shot is
	/// rejected against. <b>A flyer's is zero</b>, so an aircraft is measured centre to centre by a
	/// blast. See docs/simulation/hit-detection.md, "The three radius slots", for all three.</para>
	/// </summary>
	public abstract int HitRadius { get; }

	/// <summary>
	/// The radius at which this object <i>blocks</i> a walking machine. Read only by
	/// <see cref="MechObject.CollisionTest"/>, and only of the <i>other</i> object. <b>Zero means
	/// walk through me</b>, and the test skips the object entirely.
	///
	/// <para>The base is zero, which is what a flyer keeps: nothing in the simulation is stopped by
	/// an aircraft. A structure that answers zero here is stopped by its collision volume instead —
	/// see <see cref="BaseObject.BlocksWalker"/>.</para>
	/// </summary>
	public virtual int CollisionRadius => 0;

	/// <summary>
	/// The drawn model's own radius, in world units - the original's vtable slot <c>+0x10</c>,
	/// <c>SimObject_GetShapeRadius</c> (<c>0046b80c</c>), which reads it straight off the shape the
	/// object instances rather than out of any type record. The HUD target box sizes itself from it
	/// (see <c>Herculan.Engine.Content.TargetBox</c>).
	///
	/// <para>All three shootable classes keep it apart from <see cref="HitRadius"/>: a structure's
	/// body radius is its own <c>BASES.DAT</c> figure, a machine's is the flat 750 of
	/// <see cref="MechTypeRecord.BodyRadius"/>, and a flyer has none at all. This is the radius a
	/// flyer's and a structure's hit test rejects against.</para>
	/// </summary>
	public virtual int ShapeRadius => HitRadius;

	/// <summary>
	/// The object's shape instance's per-sequence cell-frame array, or null for one whose shape has
	/// no cells the simulation drives. It is what makes a destroyed part stop being drawn — see
	/// <see cref="ShapeCellFrames"/>. The three classes damage can take apart override it; nothing
	/// else does, and a projectile's own flipbook is not this (it is stepped by the shot's frame
	/// counter and drawn from a mesh built per cell).
	/// </summary>
	public virtual ShapeCellFrames? CellFrames => null;

	/// <summary>
	/// Vtable <c>+0x20</c> — <b>the hit test and the damage application are the same call</b>, which
	/// is the shape of the original and not a shortcut here: <c>Sim_RaycastObjectList</c>
	/// (<c>00426528</c>) offers each live object the shot and the object decides both whether it was
	/// struck and what that did to it.
	///
	/// <para>The base returns "missed"; <see cref="MechObject"/> (<c>Mech_DirectFireHitTest</c>,
	/// <c>00418ba8</c>), <see cref="BaseObject"/> and <see cref="FlyerObject"/> each override it with
	/// the original's own — see docs/simulation/hit-detection.md.</para>
	///
	/// <para>The world is passed because a hit is more than a number: an implementation spawns the
	/// shot's impact effect from in here, which is where the original spawns it too — see
	/// <see cref="SimWorld.SpawnImpactEffect"/>.</para>
	/// </summary>
	/// <returns>
	/// How far along the ray the object was struck, or zero for a miss. The caller shortens the ray
	/// to this, so it has to be a distance rather than a flag.
	/// </returns>
	public virtual int DirectFireHitTest(SimWorld world, WeaponShot shot) => 0;

	/// <summary>
	/// Vtable <c>+0x70</c> — what an explosion does to this object, called on every object the blast
	/// sweep found in range (<see cref="SimWorld.ExplosiveBlastSweep"/>) and, for a machine, directly
	/// on itself by the direct-fire path when the shot carries a splash share.
	///
	/// <para>All three shootable classes implement it and no two of them alike — a machine rolls and
	/// places every component behind a shield, a structure walks its parts with no shield step at
	/// all, and an aircraft is one point. The base does nothing, so a projectile standing in someone
	/// else's blast ignores it. See docs/simulation/damage-system.md, "Explosive damage".</para>
	/// </summary>
	/// <param name="world">The simulation, for the generator the per-component roll draws from.</param>
	/// <param name="damage">The blast's own damage figure, before shields scale it.</param>
	/// <param name="hitPoint">Where the explosion went off, in world units.</param>
	/// <param name="blastRadius">How far it reaches, and the denominator of its falloff.</param>
	/// <param name="attacker">Who set it off, for the kill credit — the sweep's own fourth argument.</param>
	public virtual void ExplosiveDamage(SimWorld world, short damage, Vec3i hitPoint, int blastRadius,
			SimObject? attacker) {
	}

	/// <summary>
	/// One simulation step. Rate-based motion inside an override should go through
	/// <see cref="SimMath.IntegrateRateOverTick"/> rather than multiplying by a float delta —
	/// <see cref="SimWorld"/> maintains <see cref="SimMath.TickDelta"/> for exactly that.
	/// </summary>
	public abstract void Tick(SimWorld world);

	/// <summary>
	/// This object's shape-to-world frame — the transform every class keeps at <c>obj+0x12</c> with
	/// its position in the translation, and what anything riding an object is placed through. The
	/// base form is heading alone, which is all a class with no lean and no turret has; the classes
	/// that carry more override it.
	/// </summary>
	public virtual Transform3 WorldFrame {
		get {
			var frame = Transform3.FromEuler(0, 0, (short)Heading);
			var position = Position;
			frame.X = position.X;
			frame.Y = position.Y;
			frame.Z = position.Z;
			return frame;
		}
	}
}
