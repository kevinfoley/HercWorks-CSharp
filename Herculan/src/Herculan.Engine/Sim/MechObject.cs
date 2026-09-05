using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.File.Dbsim;
using Herculan.Engine.Numerics;
using Herculan.Engine.Sim.Anim;
using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

/// <summary>
/// A HERC in the simulation. In DBSIM this is the class with the 34-slot vtable — by a wide margin
/// the most elaborate <see cref="SimObject"/> subtype, carrying shields, a 29-slot component health
/// array with its own dependency graph, a weapon-mount manager object, reactor energy bookkeeping
/// and an AI/input controller. Combat and AI are still out of scope; <b>locomotion is not</b>.
///
/// <para>A HERC has <b>no velocity vector</b>. Every metre of translation and every degree of
/// turn-in-place rotation comes out of the walk / run / turn animations' root-node motion; the
/// control law only picks a speed scalar, a turn rate and an animation playback rate, and the
/// animation does the moving (<see cref="AnimationThread"/>). That is why <see cref="Speed"/> below
/// is a scalar with no direction attached, and why a HERC with no <see cref="ShapeAnimation"/>
/// simply stands still. See docs/simulation/mech-locomotion.md.</para>
///
/// <para>The per-type data is real: <see cref="HercSimDat"/> comes from the game's own
/// <c>dat\&lt;name&gt;.dat</c> and <see cref="Type"/> applies the load-time rescale on top, so the
/// speeds and turn rates are the machine's actual stats. <see cref="Loadout"/> is the fit the
/// mission author gave this machine.</para>
/// </summary>
public sealed partial class MechObject : SimObject {
	/// <summary>
	/// How long a HERC keeps backing away from something it walked into, in timer units.
	/// <c>Mech_MovementTick</c>'s own constant. Only AI machines arm it — a blocked player just
	/// stops.
	/// </summary>
	private const int CollisionBackoffTime = 10000;

	private readonly int _hitRadius;
	private readonly GunLayout? _hardpoints;
	private readonly WeaponCatalog? _weapons;
	private readonly ColliderNode[] _collision;
	private readonly ComponentDamage? _damage;
	private readonly Func<int, int>? _weaponModelCellCount;

	/// <param name="hardpoints">
	/// The chassis' own <c>gl\&lt;HERC&gt;.GL</c> hardpoint list — where each weapon physically sits,
	/// which cockpit row it owns, and which slot of <paramref name="loadout"/> it draws from. Without
	/// it the machine is fitted with nothing, because the fit alone does not say where anything goes.
	/// </param>
	/// <param name="weapons">The simulator's weapon tables — see <see cref="WeaponCatalog"/>.</param>
	/// <param name="collision">
	/// The chassis' <c>col\&lt;HERC&gt;.COL</c> hit-sphere model — every cluster of it mounted on one
	/// of the shape's animated nodes, which is what makes the hit geometry follow the walk cycle. A
	/// machine without one cannot be struck at all; see <see cref="DirectFireHitTest"/>.
	/// </param>
	/// <param name="damage">
	/// The chassis' <c>dmg\&lt;HERC&gt;.DMG</c> component health, sized to a mech's 29 components and
	/// 22 dependents. Without it a struck component has nowhere to record the hit and the machine is
	/// likewise untouchable.
	/// </param>
	/// <param name="weaponModelCellCount">
	/// How long a fitted weapon's muzzle-flash flipbook is, by its <c>dts\MECHWPNS.DTS</c> shape
	/// index — the one thing the mounts need from the model library, and what sets the length of
	/// both the flash and the ELF's spin-up. See <see cref="WeaponMount.FlashCell"/>.
	/// </param>
	/// <param name="flightModel">
	/// The chassis' <c>fm\&lt;NAME&gt;.FM</c>, which only a flyer has and which
	/// <c>MechType_InitOne</c> loads only for a type whose record sets
	/// <see cref="MechTypeRecord.IsFlyer"/>. Non-null is what puts the machine on the flight path —
	/// see <see cref="Flight"/>.
	/// </param>
	public MechObject(string name, HercSimDat simData, int hitRadius, MechLoadout loadout,
			ShapeAnimation? animation = null, GunLayout? hardpoints = null,
			WeaponCatalog? weapons = null, ColliderNode[]? collision = null,
			ComponentDamage? damage = null, Func<int, int>? weaponModelCellCount = null,
			FlightModel? flightModel = null) {
		Name = name;
		SimData = simData;
		Type = new MechTypeRecord(simData);
		_hitRadius = hitRadius;
		Loadout = loadout;
		_hardpoints = hardpoints;
		_weapons = weapons;
		_collision = collision ?? Array.Empty<ColliderNode>();
		_damage = damage;
		_weaponModelCellCount = weaponModelCellCount;

		if (Type.IsFlyer && flightModel != null) {
			// Mech_Constructor's own flyer seed: airborne at 1000, with the throttle field the
			// flight model owns starting where the machine's own throttle is.
			Flight = new FlightModelRecord(flightModel);
			FlightVelocity = new Vec3i(0, InitialAirSpeed, 0);
			FlightThrottle = Throttle;
		}

		// A HERC powers up in its stop / step-off sequence, not its walk cycle — the mech constructor
		// builds this thread with typeRec+0x12 and a rate of zero. It matters: the gait state
		// machine only enters the turn-in-place cycle from a stop sequence, so a machine started on
		// the walk cycle at zero speed can never begin turning.
		if (animation != null && animation.HasSequence(Type.StopForwardSequence)) {
			Animation = animation;
			Shape = new ShapeInstance(animation);
			Thread = Shape.AddThread(Type.StopForwardSequence);

			// The torso's two threads, in the constructor's own order — it matters, because the
			// first-registered thread wins any node two of them both animate. Neither ever plays:
			// their rate stays zero and the torso tick seeks them by angle instead. A type record
			// with a negative sequence id gets no thread, exactly as the original skips one.
			TorsoTwistThread = AddTorsoThread(animation, Type.TorsoTwistSequence);
			TorsoPitchThread = AddTorsoThread(animation, Type.TorsoPitchSequence);
		}

		// The original splits this in two: the constructor sizes the shield array and fills the pool,
		// and the spawn path calls Mech_ConfigureLoadout straight afterwards to fit the pods and
		// resize the array around them. Both run before the machine ever ticks, so they are one call
		// here — see MechObject.Power.cs.
		ConfigureLoadout();
	}

	private AnimationThread? AddTorsoThread(ShapeAnimation animation, short sequence) =>
		sequence >= 0 && animation.HasSequence(sequence) ? Shape!.AddThread(sequence) : null;

	/// <summary>Base name of the mech's data files, e.g. <c>SAMSON</c> for <c>dat\SAMSON.DAT</c>.</summary>
	public string Name { get; }

	/// <summary>The mech type's stats, straight out of the game's own per-mech <c>.DAT</c>.</summary>
	public HercSimDat SimData { get; }

	/// <summary>The same stats with the load-time rescale applied and the fields correctly named.</summary>
	public MechTypeRecord Type { get; }

	/// <summary>The weapon fit the mission gave this machine — see the type's summary.</summary>
	public MechLoadout Loadout { get; }

	/// <summary>The type's animation data, or null when its model carried none.</summary>
	public ShapeAnimation? Animation { get; }

	/// <summary>
	/// This machine's animated shape — its animation data plus the three threads playing on it.
	/// Null when its model carried no animation.
	/// </summary>
	public ShapeInstance? Shape { get; }

	/// <summary>
	/// This machine's locomotion thread, the first of the three and the only one that ever plays.
	/// Null when it has no animation.
	/// </summary>
	public AnimationThread? Thread { get; }

	/// <summary>
	/// The thread the torso's twist angle is seeked on (<c>mech+0x230</c>), or null when the type
	/// names no twist sequence.
	/// </summary>
	public AnimationThread? TorsoTwistThread { get; }

	/// <summary>The pitch counterpart (<c>mech+0x234</c>).</summary>
	public AnimationThread? TorsoPitchThread { get; }

	/// <summary>
	/// One node of this machine's shape, posed as it stands this tick — see
	/// <see cref="ShapeInstance.NodeTransform"/>. Identity when the machine has no animation.
	/// </summary>
	public Transform3 NodeTransform(int transformId) =>
		Shape?.NodeTransform(transformId) ?? Transform3.Identity;

	/// <summary>Whether this is the machine the player pilots. Only it slides on steep ground.</summary>
	public bool IsPlayer { get; set; }

	/// <inheritdoc />
	public override bool LocallyPiloted => IsPlayer;

	/// <inheritdoc />
	/// <remarks><c>Mech_Constructor</c> (<c>00415bb0</c>) writes 0 at <c>0x00415d35</c>.</remarks>
	public override TargetClass TargetClass => TargetClass.Herc;

	/// <inheritdoc />
	/// <remarks>
	/// <c>FUN_00417b98</c>, the mech vtable <c>+0x24</c>: it pushes the <b>type record's <c>+0x0c</c></b>
	/// as a shape part id — <c>.DAT</c> file offset 10, <see cref="MechTypeRecord.CameraBoneId"/>, the
	/// same node the cockpit eye rides — resolves it to that node's transform, and the callers put its
	/// translation through the machine's own rotation. So a HERC is aimed at from its cockpit, which
	/// walks and leans with it, and not from the ground point its model origin sits on.
	///
	/// <para>A machine with no shape falls back to the origin, which is the original's own branch for
	/// a part the shape does not have.</para>
	/// </remarks>
	public override Vec3i AimPoint {
		get {
			var node = CameraNodeTransform;
			return new Vec3i(node.X, node.Y, node.Z);
		}
	}

	/// <inheritdoc />
	/// <remarks>The same node's <i>model-space</i> Z, which is the <c>+0x1c</c> the sweep reads.</remarks>
	public override int SightHeight =>
		Shape is { } shape && Animation?.TransformIdOfPart(Type.CameraBoneId) is { } node && node >= 0
			? shape.NodeTransform(node).Z
			: Detection.DefaultSightHeight;

	/// <inheritdoc />
	/// <remarks>
	/// Both halves of the original's pair, which for a HERC are two different things: destroyed, or
	/// merely unable to walk. Losing the legs takes a machine off the target list.
	/// </remarks>
	public override bool Neutralised => Destroyed || Immobilised;

	/// <inheritdoc />
	/// <remarks>Mech vtable <c>+0x3c</c> (<c>FUN_00415488</c>), which returns <c>mech+0x298</c>.</remarks>
	public override short AimTwist => TorsoTwistAngle;

	/// <summary>
	/// <c>mech+0x96</c> — the radar mode: false is PASSIVE, true is ACTIVE. <b>A HERC powers up
	/// passive</b>; nothing in the original writes this field at construction and only the toggle
	/// below ever sets it for a player.
	///
	/// <para>It does two things at once, which is the trade the mode is: an active scanner lets this
	/// machine paint contacts out to <see cref="Detection.RadarTargetingRange"/> instead of relying
	/// on line of sight inside visual range, and it makes this machine visible to everything else at
	/// the same range — and it is one of the two emissions an anti-radiation missile homes on.</para>
	/// </summary>
	public bool Scanner { get; private set; }

	/// <summary>
	/// <c>FUN_0041b468</c> — the manual's [R], and the F4 scanner screen's PASS/ACTIVE button pair,
	/// which both reach the same place. <b>Only the machine the player is flying toggles</b>: the
	/// original gates the flip on <c>mech+0xa3</c> and then repaints the console lights for whatever
	/// the mode now is, so calling it on an AI machine only refreshes the display.
	///
	/// <para>The tone the flip plays is the cockpit's own confirmation, one sound for each mode —
	/// <c>gnract</c> going active and <c>gnrdact</c> going passive. It is not positional: the
	/// original plays it through <c>Sound_Play</c>, because it is a noise the cockpit makes rather
	/// than one the world does.</para>
	///
	/// <para>The tone is only half of it. The flip also posts the computer message that says the new
	/// mode aloud, and withdraws <b>both</b> of them first — so flipping twice in quick succession
	/// leaves the machine announcing where it ended up rather than reading out the whole sequence.
	/// </para>
	///
	/// <para>The two console lights are not ported.</para>
	/// </summary>
	/// <param name="world">
	/// Optional, and only so the tone has somewhere to go. A call with none still flips the mode.
	/// </param>
	public void ToggleScanner(SimWorld? world = null) {
		if (!LocallyPiloted) {
			return;
		}

		Scanner = !Scanner;

		if (world?.Sounds is not { } sounds) {
			return;
		}

		sounds.Play(Scanner ? Audio.SoundId.ScannerActive : Audio.SoundId.ScannerPassive);

		sounds.Unsay(Content.SystemMessages.ActiveRadarMode);
		sounds.Unsay(Content.SystemMessages.PassiveRadarMode);
		sounds.Say(Scanner
			? Content.SystemMessages.ActiveRadarMode
			: Content.SystemMessages.PassiveRadarMode);
	}

	/// <summary>
	/// The scanner screen's PASS and ACTIVE buttons, which <b>set</b> rather than toggle:
	/// <c>MfdButton_OnClick</c> writes <c>mech+0x96</c> directly with 0 or 1 rather than going through
	/// the [R] path, so pressing PASS twice leaves the machine passive.
	/// </summary>
	public void SetScanner(bool active) {
		if (LocallyPiloted) {
			Scanner = active;
		}
	}

	/// <inheritdoc />
	public override bool ScannerActive => Scanner;

	/// <summary>
	/// <c>mech+0xa1</c> — this machine's jammer. Nothing toggles it yet either; see
	/// <see cref="SimObject.JammerActive"/> for why it is declared.
	/// </summary>
	public bool Jammer { get; set; }

	/// <inheritdoc />
	public override bool JammerActive => Jammer;

	/// <summary>This tick's pilot input. The host writes it before the world ticks.</summary>
	public MechControls Controls { get; set; } = MechControls.Neutral;

	/// <summary>
	/// Current speed scalar (<c>mech+0x28e</c>) — <b>not</b> a velocity. It scales the animation
	/// rate, and the animation's root motion is what actually moves the machine.
	/// </summary>
	public short Speed { get; set; }

	/// <summary>Current turn rate (<c>mech+0x28c</c>), in BAM per tick, added straight to the heading.</summary>
	public short TurnRate { get; set; }

	/// <summary>
	/// Throttle setting (<c>mech+0x290</c>), Q10 over ±0x400. Its sign is the direction of travel —
	/// there is no separate gear — and only a physical throttle lever closes the range to one side.
	/// </summary>
	public short Throttle { get; set; }

	/// <summary>
	/// Set when input moved <see cref="Throttle"/> this frame (<c>mech+0x93</c>). The original uses
	/// it to arbitrate between the stick and the cockpit's own throttle gauge, which are two-way
	/// bound — dragging the gauge works because whichever moved last wins.
	/// </summary>
	public bool ThrottleDirty { get; set; }

	/// <summary>
	/// The cockpit's throttle-gauge exchange, once per frame — the part of
	/// <c>Player_PerFrameCockpitUpdate</c> (<c>0041b130</c>) that reads the gauge's own value out of
	/// <c>gauge+0xb5</c> and settles which of the two moved last.
	///
	/// <para>Whichever side moved wins, and the loser is brought to it: with the dirty flag clear the
	/// gauge drives <see cref="Throttle"/>, and with it set the machine's throttle is handed back for
	/// the gauge to follow. Either way both hold the same number when this returns, which is what
	/// makes the slider track the keyboard and the keyboard pick up where a drag left off.</para>
	///
	/// <para><b>A flyer takes the gauge's value twice.</b> The original's own line here is gated on
	/// the type record's flyer flag and writes <c>mech+0x2d7</c> as well as <c>mech+0x290</c> —
	/// <see cref="FlightThrottle"/> as well as <see cref="Throttle"/> — because the flight model
	/// reads its own copy and nothing else would ever reach it. So the cockpit slider is a working
	/// throttle on a RAZOR, and on a chassis with no keyboard throttle binding it is the <i>only</i>
	/// one.</para>
	///
	/// <para>The gauge's <b>speed</b> half is a different matter: the original feeds it
	/// <c>mech+0x28e</c>, the walker speed scalar, which no flight path ever writes. A RAZOR's
	/// throttle bar therefore moves and its speed bar does not — a retail quirk, not an omission
	/// here. <see cref="DisplaySpeedKph"/> takes the flyer branch and does read airspeed.</para>
	/// </summary>
	/// <param name="gaugeThrottle">The gauge's current value, Q10 in the same ±0x400 range.</param>
	/// <returns>The value both should now read.</returns>
	public short ExchangeCockpitThrottle(short gaugeThrottle) {
		if (ThrottleDirty) {
			ThrottleDirty = false;
			return Throttle;
		}

		if (Flight != null) {
			FlightThrottle = gaugeThrottle;
		}

		Throttle = gaugeThrottle;
		return gaugeThrottle;
	}

	/// <summary>
	/// All stop — the keypad <c>[5]</c> command, case 7 of <c>Sim_PollPlayerInput</c>'s key switch
	/// (<c>00460764</c>): zero the throttle and mark it dirty, so the gauge follows the machine
	/// rather than putting the old setting straight back.
	/// </summary>
	public void AllStop() {
		Throttle = 0;
		ThrottleDirty = true;
	}

	/// <summary>Animation playback rate (<c>mech+0x2a0</c>). In steady state it equals <see cref="Speed"/>.</summary>
	public short AnimRate { get; set; }

	/// <summary>
	/// Body pitch, as a binary angle. A walking HERC keeps it at zero — nothing tilts its legs — but
	/// it is the flight path's primary control axis; see <see cref="MechObject.FlightVelocity"/>.
	/// </summary>
	public short Pitch { get; set; }

	/// <summary>Body roll, as a binary angle. The same: level on a walker, and how a flyer turns.</summary>
	public short Roll { get; set; }

	/// <summary>
	/// The speed the HUD would read for this machine, in km/h. <c>Mech_GetDisplaySpeedKph</c>
	/// (<c>0041bb3c</c>) branches on the flyer flag: a walker's speed scalar goes through a fixed
	/// ratio, while a flyer's <see cref="AirSpeed"/> is remapped from its own speed range onto the
	/// same readout scale, so both chassis kinds fill the same gauge.
	/// </summary>
	public int DisplaySpeedKph => Flight is { } flight
		? Type.DisplayAirSpeedKph(AirSpeed, flight.Data.AirSpeedMax)
		: Type.DisplaySpeedKph(Speed);

	/// <summary>
	/// Where the pilot's eye is, in world units — the machine's own position with the pose of the
	/// node its type record names in <see cref="MechTypeRecord.CameraBoneId"/> applied.
	///
	/// <para>There is no cockpit-bob code anywhere in DBSIM, and none is needed: the eye rides a
	/// model node, the walk cycle animates that node's parent, and the bob falls out.
	/// <c>FUN_0041ef14</c> reads the same node the same way to work out where a target sits relative
	/// to the pilot.</para>
	///
	/// <para>Falls back to the machine's own origin when its model names no such node, which is any
	/// shape with no animation data.</para>
	/// </summary>
	public Vec3i EyePosition {
		get {
			var eye = EyeTransform;
			return new Vec3i(eye.X, eye.Y, eye.Z);
		}
	}

	/// <summary>
	/// The node the eye rides, in world space — the camera node's own posed transform composed with
	/// the machine's, and nothing else. This is the frame <see cref="EyeTransform"/> is measured in.
	/// </summary>
	public Transform3 CameraNodeTransform => PartTransform(Type.CameraBoneId);

	/// <summary>
	/// The pilot's whole frame in world space, orientation included: the camera node's frame with the
	/// type's own eye offset (<see cref="MechTypeRecord.EyeOffsetY"/>) put through it.
	/// <see cref="EyePosition"/> is its translation.
	///
	/// <para>The offset is the cockpit branch of <c>FUN_004011a0</c>'s own step — it takes the node's
	/// world matrix from the mech vtable's <c>+0x24</c> accessor (<c>00417b98</c>) and calls
	/// <c>Transform_ApplyToShortPoint</c> with the offset point the <c>+0x30</c> accessor
	/// (<c>004155c4</c>) built out of the type record. Without it the eye sits at the node's own
	/// origin, which on a HERC is around its waist.</para>
	///
	/// <para>The orientation is not decorative either: <c>Mech_TargetRelativeToPilot</c>
	/// (<c>0041ef14</c>) brings a target into exactly this frame to work out where the HUD should
	/// draw it, and the view takes its whole euler triple — roll included — from it. It is also why
	/// torso twist and pitch turn the view without anything having to add them to it: the camera node
	/// hangs off the two nodes those sequences drive (see
	/// docs/simulation/mech-locomotion.md's chain table).</para>
	/// </summary>
	public Transform3 EyeTransform {
		get {
			var node = CameraNodeTransform;
			var eye = node.TransformPoint(0, Type.EyeOffsetY, Type.EyeOffsetZ);
			node.X = eye.X;
			node.Y = eye.Y;
			node.Z = eye.Z;
			return node;
		}
	}

	/// <summary>
	/// Where one part of this machine's model has ended up in the world, orientation included: the
	/// part's posed node transform composed with the machine's own. The camera bone and every weapon
	/// hardpoint are both resolved this way — <c>WeaponMount_PrepareShot</c> (<c>0040e788</c>) reads
	/// the firing hardpoint's bone exactly as <c>Mech_TargetRelativeToPilot</c> reads the pilot's.
	///
	/// <para>Falls back to the machine's own frame for a part the model does not have, which is what
	/// the original's own fallback transform amounts to.</para>
	/// </summary>
	/// <param name="partId">The model part, in the <c>.DTS</c> part id space the type record and the <c>.GL</c> hardpoint list both use.</param>
	public Transform3 PartTransform(int partId) {
		if (Shape is not { } shape || Animation is not { } animation) {
			return Rotation();
		}

		int node = animation.TransformIdOfPart(partId);
		return node < 0 ? Rotation() : Transform3.Concat(shape.NodeTransform(node), Rotation());
	}

	/// <summary>
	/// The machine's own shape-to-world transform: its lean and heading with its world position in
	/// the translation. Anything turning a point of the machine's shape into a world point goes
	/// through this, as <see cref="EyePosition"/> does.
	/// </summary>
	public Transform3 WorldTransform => Rotation();

	/// <inheritdoc />
	/// <remarks>A machine's frame is its full attitude, lean included — see <see cref="WorldTransform"/>.</remarks>
	public override Transform3 WorldFrame => WorldTransform;

	/// <summary>
	/// The machine's body radius — <see cref="MechTypeRecord.BodyRadius"/>, the flat 750 every retail
	/// HERC states. Not <see cref="MechTypeRecord.HitRadius"/>, which is the generous <i>shot</i>
	/// radius <see cref="DirectFireHitTest"/> rejects against, and not the drawn model's bound
	/// either, which is <see cref="ShapeRadius"/>.
	/// </summary>
	public override int HitRadius => Type.BodyRadius;

	/// <inheritdoc />
	/// <remarks>The same figure — the original hands both radii out of the same field.</remarks>
	public override int CollisionRadius => Type.BodyRadius;

	/// <summary>
	/// The drawn model's own bound. Nothing in the simulation reads it; the HUD target box sizes
	/// itself from it.
	/// </summary>
	public override int ShapeRadius => _hitRadius;

	/// <summary>
	/// This machine's per-component health, or null for a type whose <c>.DMG</c> the install is
	/// missing. Every hit past shields lands in here.
	/// </summary>
	public ComponentDamage? Damage => _damage;

	// Post-collision back-off, for AI machines: a countdown during which desired speed is pinned to
	// one extreme so the machine walks itself clear of whatever it hit.
	private int _backoffTimer;
	private bool _backoffReverse;

	// The player's slide down steep ground. DBSIM keeps these as three globals because only one
	// mech is ever the player's; they are per-object here for the same reason SimWorld has no
	// globals.
	private int _slideX;
	private int _slideY;
	private bool _sliding;

	// The object's rotation matrix (mech+0x12) and its dirty flag (mech+0x32). Rebuilt from the
	// euler angles on demand, and invalidated at the end of every locomotion tick.
	private Transform3 _rotation;
	private bool _rotationValid;

	/// <summary>
	/// One simulation step: the control law, then the move it implies.
	///
	/// <para>The original reaches these through two different paths — the input poll calls the
	/// control law for the player and the AI think function calls it for everyone else, while the
	/// move runs from the object list's own per-tick dispatch. Their order within a frame is the
	/// same either way, and running them back to back here is what makes a headless tick
	/// reproducible.</para>
	/// </summary>
	public override void Tick(SimWorld world) {
		// Firing comes before the power tick, which is the original's order within a frame and not an
		// arbitrary choice: Sim_PollPlayerInput runs the trigger path, and the mech list's own
		// Mech_PerTickSystemsUpdate pass follows it. That pass is what counts the refire timer down,
		// so a shot fired this tick has already lost a tick's worth of its delay by the end of it.
		FireTick(world);

		// Reactor and pool first. In the original this is a separate dispatch entirely —
		// Sim_MainTick walks the global mech list calling Mech_PerTickSystemsUpdate, while the
		// control law comes in from the input poll or the AI think — but it runs once per mech per
		// tick either way, and its inputs are last tick's, so its position within the tick is free.
		PowerTick(world);

		if (Flight is { } flight) {
			// A flyer takes a different behaviour class entirely: no throttle law, no turret, and a
			// move of its own. See MechObject.Flight.cs.
			FlightTick(world, flight);
			return;
		}

		LatchCenterBody();

		if (_centeringBody) {
			// Center Body replaces the pilot's steering and his twist axis both, and is the reason
			// the original runs the throttle and the turret from the same branch: the two commands
			// it substitutes have to be worked out together, from the same pair of errors.
			CenterBodyTick(world);
		} else {
			ApplyThrottleInput(world, Controls.Turn);
			TorsoTick();
		}

		MovementTick(world);
	}

	/// <summary>
	/// The <c>[\]</c> "Center Body" command's own latch, from <c>Sim_DispatchCommand</c>'s scancode
	/// <c>0x2b</c> case (<c>0045fdac</c>) and the identical one in <c>Sim_PollPlayerInput</c>: it
	/// takes the world direction the turret is pointing in, <c>heading - twist</c>, and everything
	/// the mode does afterwards is measured against that one number.
	///
	/// <para>The two centring commands are exclusive. Each one's dispatch clears the other's global,
	/// so pressing [Backspace] mid-manoeuvre abandons this and brings the turret home instead.</para>
	/// </summary>
	private void LatchCenterBody() {
		var controls = Controls;

		if (controls.CenterBody && !_centerBodyHeld) {
			_centeringBody = true;
			_centeringTorso = false;
			_centerBodyReference = (short)((short)Heading - TorsoTwistAngle);
		}

		_centerBodyHeld = controls.CenterBody;

		if (_centeringBody && controls.CenterTorso) {
			_centeringBody = false;
			_centeringTorso = true;
		}
	}

	/// <summary>
	/// <c>Sim_PollPlayerInput</c>'s Center Body branch (<c>00460764</c>) — the machine walks its legs
	/// round until they point where the turret was when the command was given, unwinding the turret
	/// by exactly as much as the body gains so the pilot keeps looking at the same place throughout.
	///
	/// <para>Two errors drive it, both measured against the captured direction: how far the
	/// <i>heading</i> still is from it, which steers, and how far the <i>turret</i> has drifted off
	/// it, which twists. Both go to zero together, and only then, since heading meeting the reference
	/// forces the twist to be zero.</para>
	///
	/// <para>Each error is gained, then <b>squared</b> and rescaled — the original's own
	/// <c>e² >> 8</c> with the sign put back afterwards. That makes it soft near the target and hard
	/// away from it, which is what stops the legs hunting about the reference. The mode ends when
	/// both squared terms fall under their own thresholds, on the same tick it issues its last
	/// commands.</para>
	///
	/// <para>The pilot keeps the throttle and the pitch axis; only steering and twist are taken.</para>
	/// </summary>
	private void CenterBodyTick(SimWorld world) {
		short heading = (short)Heading;
		short bodyError = (short)(heading - _centerBodyReference);
		short turretError = (short)((short)(heading - TorsoTwistAngle) - _centerBodyReference);

		short steerGain = (short)SimMath.Q10Multiply(CenterBodySteerGain, bodyError);
		short twistGain = (short)SimMath.Q10Multiply(CenterBodyTwistGain, turretError);

		int steer = steerGain * steerGain >> 8;
		int twist = twistGain * twistGain >> 8;

		if (steer < CenterBodySteerDeadband && twist < CenterBodyTwistDeadband) {
			_centeringBody = false;
		}

		if (steerGain < 0) {
			steer = -steer;
		}

		if (twistGain < 0) {
			twist = -twist;
		}

		// Steering inverts when the machine is travelling backwards, read off the object's own speed
		// accessor (mech vtable +0x38, 00415498) rather than off the throttle — the control law does
		// its own inversion from the stick, and this one is on top of it.
		if (TravelSpeed < 0) {
			steer = -steer;
		}

		ApplyThrottleInput(world, (short)steer);
		TorsoTwistTick((short)twist);
		TorsoPitchTick(Controls.TorsoPitch);
	}

	/// <summary>
	/// The mech vtable's <c>+0x38</c> speed accessor (<c>00415498</c>): the speed scalar in the units
	/// the rest of the simulation quotes distances in. The control law above reads only its sign; a
	/// travelling shot adds the whole of it to its own speed, so a round fired from a machine running
	/// forward flies faster than one fired standing still (see <see cref="Projectile.Speed"/>).
	/// </summary>
	public short TravelSpeed => (short)SimMath.Q10Multiply(TravelSpeedScale, Speed);

	/// <summary>The accessor's own Q10 factor.</summary>
	private const int TravelSpeedScale = 2000;

	/// <summary>Q10 gain on the heading error before it is squared into a steering command.</summary>
	private const int CenterBodySteerGain = 100;

	/// <summary>Q10 gain on the turret error, lower than the steering one so the turret trails.</summary>
	private const int CenterBodyTwistGain = 0x46;

	/// <summary>Squared-steering term the mode disengages under, with the turret one below.</summary>
	private const int CenterBodySteerDeadband = 0x1e;

	private const int CenterBodyTwistDeadband = 10;

	/// <summary>
	/// <c>Sim_PollPlayerInput</c>'s turret block (<c>00460764</c>), which runs between the throttle
	/// and the move. Either the pilot is holding the turret axes, or the centring command is latched
	/// and drives them instead — touching either axis clears it, which is why the original tests the
	/// axes before it tests the mode.
	///
	/// <para>Automatic Turret Tracking, the third case there, needs a selected target and is not
	/// ported.</para>
	/// </summary>
	private void TorsoTick() {
		var controls = Controls;

		if (controls.TorsoTwist != 0 || controls.TorsoPitch != 0) {
			_centeringTorso = false;
		} else if (controls.CenterTorso) {
			_centeringTorso = true;
		}

		if (_centeringTorso) {
			CenterTorsoTick();
			return;
		}

		TorsoTwistTick(controls.TorsoTwist);
		TorsoPitchTick(controls.TorsoPitch);
	}

	/// <summary>Whether [Backspace] centring is latched, for the debug readout.</summary>
	public bool CenteringTorso => _centeringTorso;

	/// <summary>
	/// Whether [\] Center Body is latched, and the turret world direction it is steering the legs
	/// onto — both for the debug readout.
	/// </summary>
	public bool CenteringBody => _centeringBody;

	/// <inheritdoc cref="CenteringBody"/>
	public short CenterBodyReference => _centerBodyReference;

	// DAT_004d2588 — the latched centring mode. A global in the original, since only the player has
	// one; per-object here for the same reason SimWorld has no globals.
	private bool _centeringTorso;

	// DAT_004d2af4 and DAT_004d2af8 — the Center Body mode and the turret world direction it was
	// latched on, globals in the original for the same reason. _centerBodyHeld is the edge detector
	// the original gets for free from being dispatched on a keystroke rather than on a held key.
	private bool _centeringBody;
	private bool _centerBodyHeld;
	private short _centerBodyReference;

	/// <summary>
	/// <c>Mech_MovementTick</c> (<c>0041a360</c>) — advances the animation, takes whatever ground
	/// movement that produced, drops the machine onto the terrain, and undoes the step if it turned
	/// out to be blocked.
	///
	/// <para>The undo is the interesting part. A blocked step restores the position <i>and</i> the
	/// animation thread, reverses the speed scalar and the playback rate, and tries again — so a
	/// HERC that walks into something takes a step backwards rather than sticking. If that step is
	/// blocked too, it gives up and stops. Note the restore puts back the rotation <i>matrix</i> but
	/// not the euler angles, so the heading change a blocked step made survives while its
	/// translation does not; that is the original's behaviour, not an oversight here.</para>
	/// </summary>
	private void MovementTick(SimWorld world) {
		ResolveMovement(world);

		// Last thing in the tick, as it is in the original — it reads the pose the move settled on.
		PlaceLegsOnGround(world);
	}

	/// <summary>Everything Mech_MovementTick does before its closing Mech_PlaceLegsOnGround call.</summary>
	private void ResolveMovement(SimWorld world) {
		if (Thread == null) {
			Position = new Vec3i(Position.X, Position.Y,
				world.GroundHeightAt(Position) + Type.RideHeight);
			return;
		}

		var startPosition = Position;
		if (IsPlayer) {
			_slideOrigin = startPosition;
		}

		var saved = Capture();
		IntegrateMotion();

		var moved = Position;
		int x = moved.X;
		int y = moved.Y;
		if (IsPlayer && _sliding) {
			x += _slideX;
			y += _slideY;
		}

		Position = new Vec3i(x, y, world.Terrain.HeightAtWorld(x, y) + Type.RideHeight);

		if (!CollisionTest(world)) {
			return;
		}

		Restore(saved);

		if (!IsPlayer) {
			_backoffTimer = CollisionBackoffTime;
			_backoffReverse = Speed > 0;
		}

		AllStop();

		if (Thread.InTransition) {
			// Mid-transition there is no sensible step to reverse, so the machine is cut straight
			// into its stop animation instead.
			Thread.SetSequence(Type.StopForwardSequence, 0, 0);
			return;
		}

		int reversed = -Speed;
		if (reversed >= Type.MaxForward) {
			reversed = Type.MaxForward;
		} else if (reversed <= Type.ReverseGaitThreshold) {
			reversed = Type.ReverseGaitThreshold;
		}

		Speed = (short)reversed;
		AnimRate = (short)-AnimRate;

		IntegrateMotion();

		// The retry is not terrain-clamped before being tested, and the original has only one save
		// slot — so a second refusal restores the same tick-start state again.
		if (CollisionTest(world)) {
			Restore(saved);
			Speed = 0;
		}
	}

	/// <summary>
	/// <c>Mech_IntegrateMotion</c> (<c>00418f40</c>) + <c>SimObject_ApplyRootMotion</c>
	/// (<c>0040250c</c>) — the whole of a HERC's translation and turn-in-place rotation.
	///
	/// <para>Seed the thread's root transform to identity, step the animation by this tick's worth
	/// of animation time, then read the root back: what comes out is exactly the ground movement
	/// that step covered, ramped within the current frame and committed whole at each frame
	/// boundary. Rotate it into world space, add it on, and reset.</para>
	/// </summary>
	private void IntegrateMotion() {
		if (Thread == null) {
			return;
		}

		Thread.Rate = AnimRate;

		// dt is the timestep in animation ticks: Q8(SimTickDelta, 100), where the 100 is the
		// original's own animation-time-per-sim-time constant.
		short delta = (short)SimMath.IntegrateRateOverTick(100);

		Thread.WriteRoot(Transform3.Identity);
		Thread.Advance(delta);
		var motion = Thread.ReadRoot();

		var rotation = Rotation();
		var moved = rotation.TransformPoint(motion.X, motion.Y, motion.Z);
		Position = moved;

		var euler = motion.ToEuler();
		Pitch = (short)(Pitch + euler.X);
		Roll = (short)(Roll + euler.Y);
		Heading = (Heading + euler.Z) & 0xffff;
		_rotationValid = false;
	}

	/// <summary>
	/// <c>Mech_CollisionTest</c> (<c>00418f74</c>) — whether the machine's new position is refused,
	/// either by another object or by the ground being too steep to stand on.
	///
	/// <para><b>The gap is asymmetric</b>, as the original's is: this machine contributes its own
	/// <see cref="HitRadius"/> and the other object its <see cref="SimObject.CollisionRadius"/>,
	/// which is a different figure with different values — zero for a flyer and for every static
	/// structure.</para>
	///
	/// <para>A structure the radius test walks past is stopped by its collision volume instead —
	/// <see cref="BaseObject.BlocksWalker"/>, a separate sweep in the original too. Between them no
	/// structure is walked through.</para>
	///
	/// <para>Running into another machine hurts both of them — see
	/// <see cref="CollisionDamage"/>. One part of the original's sweep is still missing: it also
	/// picks out a nearby structure as a lock-on candidate, which nothing here consumes.</para>
	///
	/// <para>The sweep's first test <i>is</i> here: an object whose mission group carries an action
	/// is skipped outright, before any distance is measured. See
	/// <see cref="SimObject.AwaitingDeployment"/> for why the retail missions depend on it.</para>
	/// </summary>
	private bool CollisionTest(SimWorld world) {
		var position = Position;

		var objects = world.Objects;
		for (int i = 0; i < objects.Count; i++) {
			var other = objects[i];
			if (ReferenceEquals(other, this) || other.Removed || other.CollisionRadius == 0
					|| other.AwaitingDeployment) {
				continue;
			}

			var theirs = other.Position;
			int distance = SimMath.FastMagnitude3D(
				position.X - theirs.X, position.Y - theirs.Y, position.Z - theirs.Z);

			if (distance >= HitRadius + other.CollisionRadius) {
				continue;
			}

			// The original also latches "something ran into me" on the struck object here. Exactly
			// one AI behaviour state reads it back, and no behaviour state is ported, so there is
			// nothing yet that would notice.

			// Only a machine is hurt by the impact -- the original's gate is the struck object's
			// target class, and MechObject is the only class that answers TargetClass.Herc.
			if (other is MechObject struck) {
				CollisionDamage(world, struck);
			}

			return true;
		}

		// The second object test, and a wholly separate one: every structure the radius test above
		// passes over is stopped here by its collision volume instead.
		for (int i = 0; i < objects.Count; i++) {
			if (objects[i] is BaseObject structure && !structure.Removed
					&& !structure.AwaitingDeployment && structure.BlocksWalker(position)) {
				return true;
			}
		}

		var normal = world.Terrain.SurfaceNormalAt(position.X, position.Y);

		// Ground steeper than about 45 degrees is not walkable. Off the grid entirely counts as
		// blocked, which is what keeps a machine inside the zone.
		bool tooSteep = normal is not { } face || System.Math.Abs(face.Z) < SteepNormalZ;

		if (!IsPlayer) {
			return tooSteep;
		}

		if (tooSteep) {
			// Uphill onto a cliff is refused outright; downhill onto one turns into a slide.
			if (!_sliding && _slideOrigin.Z < position.Z) {
				return true;
			}

			_sliding = true;
			if (normal is { } slope) {
				_slideX += SimMath.Q10Multiply(10, slope.X);
				_slideY += SimMath.Q10Multiply(10, slope.Y);
			}
		} else if (_sliding) {
			_sliding = false;

			// A long enough slide hurts on landing. The damage itself needs the component system,
			// so for now only the slide's own bookkeeping is reproduced.
			_lastSlideDistance = SimMath.FastMagnitude2D(_slideX, _slideY);
			_slideX = 0;
			_slideY = 0;
		}

		return false;
	}

	/// <summary>
	/// <b>Walking into another machine hurts both of you</b>, through the same explosive-damage slot
	/// a blast uses. It is a direct call on each party rather than a sweep, so a third machine
	/// standing beside the crash takes nothing.
	///
	/// <para><b>Only a machine.</b> A structure or an aircraft blocks the move and is not hurt by
	/// it.</para>
	///
	/// <para><b>What lands is a difference of momenta, not a speed.</b> Each side's speed is weighed
	/// by its own <see cref="MechTypeRecord.Mass"/> in Q10, and the other machine's is taken as the
	/// component of <i>its</i> travel along <i>this</i> machine's forward axis — so a head-on meeting
	/// adds (the term is negative and is subtracted) and being rear-ended by something slower
	/// subtracts. Both machines then take the same figure. The threshold under it is what keeps a
	/// machine shuffling against a wall from grinding itself down.</para>
	///
	/// <para><b>The impact point mixes two frames</b>, and that is the original's arithmetic: X and Y
	/// are the world midpoint of the two machines, but Z is the lower of the two cockpit-eye nodes'
	/// <i>model-space</i> heights — roughly torso height above each machine's own feet — used as
	/// though it were a world height. Reproduced as-is; see KNOWN_ISSUES.md.</para>
	/// </summary>
	private void CollisionDamage(SimWorld world, MechObject other) {
		// Rotation only: the original copies the two matrices without their translations, so this is
		// a direction being re-expressed, not a point being moved.
		var mine = Rotation();
		mine.TransposeRotation();

		var theirTravel = other.Rotation().RotateVector(0, other.Speed, 0);
		int closing = mine.RotateVector(theirTravel.X, theirTravel.Y, theirTravel.Z).Y;

		int impulse = SimMath.Q10Multiply(Type.Mass, Speed)
			- SimMath.Q10Multiply(other.Type.Mass, closing);
		int damage = SimMath.Q10Multiply(CollisionDamageScale, impulse);

		if (damage <= CollisionDamageThreshold) {
			return;
		}

		var theirs = other.Position;
		var at = new Vec3i(
			(theirs.X + Position.X) >> 1,
			(theirs.Y + Position.Y) >> 1,
			System.Math.Min(EyeNodeHeight(), other.EyeNodeHeight()));

		ExplosiveDamage(world, (short)damage, at, CollisionBlastRadius, other);
		other.ExplosiveDamage(world, (short)damage, at, CollisionBlastRadius, this);
	}

	/// <summary>
	/// The model-space height of the machine's cockpit-eye node, or zero when the shape has no such
	/// node — the original's own fallback, which reads an identity transform. Only
	/// <see cref="CollisionDamage"/> reads it.
	/// </summary>
	private int EyeNodeHeight() {
		int transformId = Animation?.TransformIdOfPart(Type.CameraBoneId) ?? -1;
		return transformId < 0 || Shape == null ? 0 : Shape.NodeTransform(transformId).Z;
	}

	/// <summary>The Q10 factor the momentum difference is scaled by before it becomes damage.</summary>
	private const int CollisionDamageScale = 300;

	/// <summary>Below this the collision does nothing at all — the original's literal 200.</summary>
	private const int CollisionDamageThreshold = 200;

	/// <summary>
	/// How far the impact reaches on each machine, in world units. Small: it is applied through the
	/// explosive path, so this is the denominator each component's falloff is measured against, and
	/// 1200 keeps it to the parts nearest the point of contact.
	/// </summary>
	private const int CollisionBlastRadius = 0x4b0;

	/// <summary>
	/// How shallow a surface normal's vertical component may get before the ground counts as
	/// unwalkable — <c>Mech_CollisionTest</c>'s own threshold, against normals scaled to
	/// <see cref="Terrain.HeightGrid.NormalOne"/>. It works out at about 45 degrees.
	/// </summary>
	private const int SteepNormalZ = 0x5aa;

	private Vec3i _slideOrigin;
	private int _lastSlideDistance;

	/// <summary>
	/// How far the last slide down a steep face carried the machine. Above 250 world units the
	/// original applies leg damage on landing.
	/// </summary>
	public int LastSlideDistance => _lastSlideDistance;

	/// <summary>
	/// The object's world transform, rebuilt from the euler angles when they have moved.
	///
	/// <para>In DBSIM the rotation matrix at <c>mech+0x12</c> and the world position at
	/// <c>mech+0x26</c> are one contiguous 0x20-byte transform — the position <i>is</i> the matrix's
	/// translation — which is why root motion applied through it lands in world space and why
	/// restoring the matrix after a blocked step restores the position with it.</para>
	/// </summary>
	private Transform3 Rotation() {
		if (!_rotationValid) {
			_rotation = Transform3.FromEuler(Pitch, Roll, (short)Heading);
			_rotationValid = true;
		}

		var position = Position;
		_rotation.X = position.X;
		_rotation.Y = position.Y;
		_rotation.Z = position.Z;
		return _rotation;
	}

	private readonly record struct Snapshot(
		Vec3i Position, Transform3 Rotation, bool RotationValid, AnimationThread.State Thread);

	/// <summary><c>SimObject_PushTransform</c> (<c>00402628</c>), narrowed to what a HERC needs.</summary>
	private Snapshot Capture() => new(Position, Rotation(), true, Thread!.Capture());

	/// <summary><c>SimObject_PopTransform</c> (<c>004027fc</c>).</summary>
	private void Restore(in Snapshot snapshot) {
		Position = snapshot.Position;
		_rotation = snapshot.Rotation;
		_rotationValid = snapshot.RotationValid;
		Thread!.Restore(snapshot.Thread);
	}
}

/// <summary>
/// A mech's weapon fit, as the mission states it. AI machines get theirs from <c>script.dat</c>
/// block 7's own weapon array; the player's lance gets theirs from <c>player.mec</c>. Both feed the
/// same <c>Mech_ConfigureLoadout</c> in the original.
/// </summary>
/// <param name="WeaponIds">
/// The mount ids the mission assigned, <b>in slot order and with the file's holes left in</b> —
/// <c>0</c> or <c>-1</c> for an unfitted slot. The order and the holes both matter: a chassis'
/// <c>.GL</c> hardpoint list addresses this array by slot index, so closing a hole or sorting the
/// list would fit the wrong weapon to the wrong hardpoint. See <see cref="WeaponMounts.Build"/>.
/// </param>
/// <param name="SecondaryKeys">
/// The parallel second value per slot, the array DBSIM's loadout call takes alongside the first.
/// It is the ammunition type a missile launcher is loaded with — the value a launcher's mount takes
/// through <c>Proj_LookupRecord(Missile, key)</c> and then prints as its name. Retail data puts
/// <see cref="WeaponCatalog.DefaultSecondaryKey"/> in every slot that is not a launcher. May be
/// shorter than <see cref="WeaponIds"/>, or empty, in which case the default is assumed.
/// </param>
public readonly record struct MechLoadout(
	IReadOnlyList<int> WeaponIds,
	IReadOnlyList<short> SecondaryKeys) {

	/// <summary>An empty fit, for a machine spawned outside a mission.</summary>
	public static MechLoadout None => new(Array.Empty<int>(), Array.Empty<short>());

	/// <summary>The weapon id in fit slot <paramref name="slot"/>, or 0 when the fit has no such slot.</summary>
	public int WeaponAt(int slot) => slot >= 0 && slot < WeaponIds.Count ? WeaponIds[slot] : 0;

	/// <summary>The ammunition type in fit slot <paramref name="slot"/>, defaulting to retail's own filler.</summary>
	public short SecondaryAt(int slot) =>
		slot >= 0 && slot < SecondaryKeys.Count ? SecondaryKeys[slot] : WeaponCatalog.DefaultSecondaryKey;
}
