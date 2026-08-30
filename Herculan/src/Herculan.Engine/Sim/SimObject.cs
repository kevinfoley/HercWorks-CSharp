using Herculan.Engine.Numerics;
using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

/// <summary>
/// What an object counts as when something is deciding whether to shoot it — the shared
/// <c>obj+0x1a8</c> that every constructor writes and every target filter reads.
///
/// <para>The values are the constructors' own literals: <c>Mech_Constructor</c> (<c>00415bb0</c>)
/// writes 0, <c>Flyer_Constructor</c> (<c>004215f4</c>) writes 2, and <c>Base_Construct</c>
/// (<c>00405314</c>) writes 1 for every structure except its last group of types (<c>0x2d</c>-
/// <c>0x3d</c>), which get 3. All three constructors write <see cref="None"/> first and overwrite
/// it, so an object that never finishes construction stays unclassified.</para>
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
	/// The second structure family — <c>BASES.DAT</c> types <c>0x2d</c>-<c>0x3d</c>, which get a
	/// further-derived class of their own and a hit radius of 10 rather than 5.
	/// </summary>
	Emplacement = 3
}

/// <summary>
/// Base class for everything the simulation ticks — mechs, projectiles, flyers. This is the
/// traditional OOP / virtual-dispatch model docs/engine/planning.md settles on instead of ECS, and
/// the choice is grounded in RE evidence rather than a guess about 1996-era convention: DBSIM's own
/// simulation objects are built on a shared base-object constructor (<c>FUN_00402188</c>) called by
/// every derived class right after its vtable pointer is set, with a 34-slot vtable for the Mech
/// class and smaller 6–9-slot ones for rockets and bullets.
///
/// <para>Only the slots the engine currently needs are declared. Several more are already
/// identified in the disassembly — direct-fire hit-test-and-damage (<c>+0x20</c>), the shield
/// charge getter (<c>+0x34</c>), the AI "I just took fire" notification (<c>+0x50</c>), the
/// lock-on tracking-handle request (<c>+0x54</c>), explosive damage (<c>+0x70</c>) and the shared
/// component health write (<c>+0x74</c>) — but declaring them before combat exists would only mean
/// stubbing them on every subclass. They get added alongside the systems that call them; the point
/// of recording the shape here is that when that happens it is a translation, not a redesign.</para>
/// </summary>
public abstract class SimObject {
	/// <summary>Position in world units, X/Y on the ground plane and Z up (see <see cref="Vec3i"/>).</summary>
	public Vec3i Position { get; set; }

	/// <summary>Facing as a binary angle (see <see cref="BinaryAngle"/>).</summary>
	public int Heading { get; set; }

	/// <summary>
	/// <c>obj+0x4b</c> — this object's slot in the world's single live-object list, written by
	/// <c>ObjectList_Add</c> (<c>FUN_00411dd4</c>) as the object joins it and never changed after.
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
	/// Whether this object is out of the fight — the <c>obj+0x99 || obj+0xa4</c> pair the target
	/// filter (<c>FUN_00433174</c>), the detection sweep (<c>FUN_004128f8</c>) and the AI's
	/// "is my target finished" check (<c>FUN_0041c4a8</c>) all spell out identically.
	///
	/// <para>Both halves count, which is worth saying plainly: a HERC whose legs are gone is no
	/// longer selectable even though it is still standing, still shooting and still solid. That is
	/// the original's behaviour, not a simplification here.</para>
	/// </summary>
	public virtual bool Neutralised => false;

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
	/// (<c>FUN_0041b468</c>); <c>Base_Construct</c> latches it on for structure types 5, 6,
	/// <c>0x1d</c> and <c>0x1e</c>, which is what makes those buildings radar masts. It both extends
	/// what this object can see and makes it visible at range to everything else, and it is one of
	/// the two emissions the anti-radiation missile homes on.
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
	/// (<c>Rocket_HomingSteer</c> <c>0040a254</c>, <c>Bullet_HomingSteer</c> <c>0040aff0</c>) and the
	/// HUD's target indicator (<c>FUN_0041b728</c>) take instead of the object's position.
	///
	/// <para>The base returns the origin, and for two of the three shootable classes that is the
	/// whole story: the flyer and structure classes both install <c>FUN_00411a9c</c> in that slot,
	/// which is <c>return 0</c>, and every caller's null branch falls back to the raw origin. Only
	/// <see cref="MechObject"/> overrides it — see there for which node it names and why aiming at a
	/// HERC's position puts a missile between its feet.</para>
	/// </summary>
	public virtual Vec3i AimPoint => Position;

	/// <summary>
	/// Height above this object's origin that the detection sweep sights from and to — the
	/// <c>+0x1c</c> of the same vtable <c>+0x24</c> record, which is that node transform's
	/// <i>model-space</i> Z. <see cref="Detection.LineOfSight"/> raises both ends of its terrain ray
	/// by it.
	///
	/// <para>The base is <c>FUN_00412608</c>'s own literal 500, used whenever that slot returns
	/// nothing — so a flyer and a structure always sight from 500 and only a HERC sights from its own
	/// geometry.</para>
	/// </summary>
	public virtual int SightHeight => Detection.DefaultSightHeight;

	/// <summary>
	/// The aim offset the sensor and target-selection arcs are measured from, on top of
	/// <see cref="Heading"/> — vtable <c>+0x3c</c>, which is a mech's turret twist and zero for
	/// everything else.
	/// </summary>
	public virtual short AimTwist => 0;

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
	/// (<c>FUN_0045f464</c>) walks its global object lists and skips entries flagged removed rather
	/// than compacting the list mid-walk; <see cref="SimWorld"/> does the same.
	/// </summary>
	public bool Removed { get; set; }

	/// <summary>
	/// Whether the object is built but has not entered the mission yet — <b>it exists, but it is not
	/// in the world</b>. It is not drawn, not simulated, and not collided with, exactly as if it had
	/// not spawned; the position it holds is a placeholder its arrival overwrites.
	///
	/// <para>The original spells this as one pointer: <c>DBSim_BuildGroupRecord</c>
	/// (<c>00423b34</c>) resolves the block-11 record's action ref into the group record's
	/// <c>+0x14</c> slot, and a non-null slot means "this group is waiting on that action". Three
	/// separate places test it and all three are honoured here —
	/// <c>maybe_Scene_SubmitFrameObjects</c> (<c>0042841c</c>) submits a mech, flyer or base for
	/// drawing only when it is null; <c>Sim_MainTick</c> (<c>0045f464</c>) sends the group to its
	/// arrival check instead of its per-frame order tick, and skips a base's own tick outright; and
	/// <c>Mech_CollisionTest</c> (<c>00418f74</c>) skips such an object before measuring any
	/// distance. Arrival clears the slot, and the whole group becomes real at once.</para>
	///
	/// <para><b>Why an undeployed group's placed position is meaningless.</b> A group waiting on an
	/// action is placed by the ordinary rules — usually on its route's first waypoint, which mission
	/// authors routinely share with the player's own squad — so several of them sit stacked on the
	/// player's spawn point until they arrive. That is harmless in the original precisely because
	/// nothing above can see them.</para>
	///
	/// <para><b>How they arrive</b> is <c>Group_DeploymentCheck</c> (<c>004236c4</c>), which
	/// <see cref="Herculan.Engine.World.MissionLoader"/>'s doc comment describes in full. The engine
	/// does not implement it yet: nothing here ever clears this flag, so a group that waits on an
	/// action stays out of the mission for the whole run.</para>
	/// </summary>
	public bool AwaitingDeployment { get; set; }

	/// <summary>
	/// Coarse collision radius, in world units — the original's vtable slot <c>+0x5c</c>, called by
	/// the area-of-effect sweep on every candidate object before comparing against a blast radius.
	/// </summary>
	public abstract int HitRadius { get; }

	/// <summary>
	/// The drawn model's own radius, in world units - the original's vtable slot <c>+0x10</c>,
	/// <c>SimObject_GetShapeRadius</c> (<c>0046b80c</c>), which reads it straight off the shape the
	/// object instances rather than out of any type record. The HUD target box sizes itself from it
	/// (see <c>Herculan.Engine.Content.TargetBox</c>).
	///
	/// <para>For a HERC and a flyer that is the same figure as <see cref="HitRadius"/>, both being
	/// the model bound; <see cref="BaseObject"/> keeps them apart because a structure's hit radius
	/// comes from <c>BASES.DAT</c> and can differ from what it draws.</para>
	/// </summary>
	public virtual int ShapeRadius => HitRadius;

	/// <summary>
	/// Vtable <c>+0x20</c> — <b>the hit test and the damage application are the same call</b>, which
	/// is the shape of the original and not a shortcut here: <c>Sim_RaycastObjectList</c>
	/// (<c>00426528</c>) offers each live object the shot and the object decides both whether it was
	/// struck and what that did to it. See <c>Mech_DirectFireHitTest</c> (<c>00418ba8</c>) for the
	/// only implementation that exists.
	///
	/// <para>The base returns "missed". <see cref="BaseObject"/> and <see cref="FlyerObject"/> both
	/// have their own <c>+0x20</c> in the original and neither is ported, so beams pass through
	/// structures and aircraft — see docs/simulation/damage-system.md.</para>
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
	/// One simulation step. Rate-based motion inside an override should go through
	/// <see cref="SimMath.IntegrateRateOverTick"/> rather than multiplying by a float delta —
	/// <see cref="SimWorld"/> maintains <see cref="SimMath.TickDelta"/> for exactly that.
	/// </summary>
	public abstract void Tick(SimWorld world);
}
