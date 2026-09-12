using HercWorks.Core.Data.File.Dat.Sim;
using Herculan.Engine.Numerics;
using Herculan.Engine.Sim.Ai;
using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

// A Cybrid flyer's AI — docs/simulation/ai-flyers.md. The Flyer class has a behaviour table of its
// own, seven states at 00499cf8 built by Flyer_BuildStateTable (00414c65), reached through its own
// three descriptor dispatchers and so driven by the same Mech_AiTick (00411cec) a walking machine
// is; docs/simulation/ai-dispatch.md owns that shared dispatch model.
public sealed partial class FlyerObject {
	/// <summary>
	/// How long a leader waits between target sweeps — the 10000 ms both acquiring thinks reload
	/// <c>flyer+0x5b</c> with.
	/// </summary>
	private const short TargetSweepInterval = 10000;

	/// <summary>
	/// Ground range inside which the leader's route step takes the next waypoint — the original's
	/// 15000, half again what a walking machine wants, because an aircraft cannot stop and turn.
	/// </summary>
	private const int WaypointArrivalRange = 15000;

	/// <summary>
	/// The altitude a flight cruises at when it is not attacking anything, in world units — an
	/// absolute world height, not a height above the ground. 180 metres.
	/// </summary>
	private const int CruiseAltitude = 30000;

	/// <summary>Q16 gain from the bearing error to a waypoint to the steering command.</summary>
	private const int RouteTurnGain = 28000;

	/// <summary>
	/// How far ahead of its leader a wingman's station is set before the formation offset is added —
	/// so the flight's anchor is a point the leader is flying toward rather than the leader itself,
	/// and a wingman is chasing a moving spot instead of a machine that keeps turning under it.
	/// </summary>
	private const int StationLead = 0x2000;

	/// <summary>Distance inside which a wingman that has overshot its station turns back rather than through.</summary>
	private const int StationOvershootRange = 10000;

	/// <summary>Range past which an extending aircraft turns back in for another pass.</summary>
	private const int AttackTurnInRange = 90000;

	/// <summary>Range inside which an attacking aircraft is on its run — it aims at the target itself.</summary>
	private const int AttackRunRange = 65000;

	/// <summary>Range cap on the figure the lead calculation works from.</summary>
	private const int LeadRangeCap = 200000;

	/// <summary>How far above its target an extending aircraft holds, in world units.</summary>
	private const int AttackApproachHeight = 10000;

	/// <summary>Bearing error inside which the run continues rather than breaking off — ±45°.</summary>
	private const short RunBearingLimit = 0x2000;

	/// <summary>Height above the target the run must keep, or it breaks off and climbs away.</summary>
	private const int RunHeightFloor = 3000;

	/// <summary>Range inside which the run breaks off whatever else is true — the aircraft is past it.</summary>
	private const int RunBreakoffRange = 10000;

	/// <summary>Bearing and pitch error inside which the guns will fire, either way.</summary>
	private const short FiringError = 1000;

	/// <summary>Range inside which the first pass launches a missile rather than opening with guns.</summary>
	private const int MissileRange = 30000;

	/// <summary>The refire delay, in milliseconds — <c>flyer+0x21f</c>'s reload.</summary>
	private const short RefireDelay = 0x5dc;

	/// <summary>
	/// The muzzle point in the airframe's own frame. The gun pair is fired from this and from its
	/// mirror; the missile comes off this one alone.
	/// </summary>
	private static readonly Vec3i Muzzle = new(500, 200, -100);

	/// <summary>
	/// <c>PROJ.DAT</c> row 2 — the flat index <c>Proj_LookupRecordByIndex</c> is handed for the
	/// projectile speed the lead calculation works from, and the same record the guns fire
	/// (<c>Bullet_Fire</c>'s subtype 2, which in the retail file is that row).
	/// </summary>
	public const int GunProjectileIndex = 2;

	/// <summary>The missile subtype the launcher fires — <c>Rocket_Fire</c>'s literal 0.</summary>
	public const short MissileSubtype = 0;

	/// <summary>
	/// <c>flyer+0x4d</c> — the behaviour block. <c>Flyer_Constructor</c> installs
	/// <see cref="FlyerBehaviourState.Deciding"/> into it, so an aircraft that has not yet been given
	/// an order sits still.
	/// </summary>
	public FlyerBehaviourBlock Behaviour;

	/// <summary>
	/// <c>flyer+0x1a4</c> — this aircraft's selected target, with the same bookkeeping every writer
	/// of the field in the original performs: the old target's
	/// <see cref="SimObject.TargetedBy"/> count goes down, the new one's goes up, and
	/// <c>flyer+0x9d</c> is raised.
	/// </summary>
	public SimObject? Target {
		get => _target;
		set {
			if (ReferenceEquals(_target, value)) {
				return;
			}

			if (_target != null) {
				_target.TargetedBy--;
			}

			_target = value;

			if (_target != null) {
				_target.TargetedBy++;
			}

			TargetChanged = true;
		}
	}

	private SimObject? _target;

	/// <summary>
	/// <c>flyer+0x9d</c> — raised whenever <see cref="Target"/> changes. Set for the same reason
	/// <see cref="MechObject.TargetChanged"/> is: the setter is the only place that can.
	/// </summary>
	public bool TargetChanged { get; set; }

	/// <inheritdoc />
	/// <remarks>
	/// <c>FUN_00421584</c>, the flyer's vtable <c>+0x38</c> — <c>Q10(1000, flyer+0x233)</c>, and
	/// <c>flyer+0x233</c> is the type record's <c>SpeedForward</c>, written once at construction and
	/// never again. So an aircraft's travel speed is a chassis constant rather than anything it is
	/// actually doing, which is what the lead calculation and every shot's inherited speed read.
	/// </remarks>
	public override short TravelSpeed => (short)SimMath.Q10Multiply(1000, ChassisSpeed);

	/// <summary><c>flyer+0x233</c>, the type record's <c>+0x04</c>.</summary>
	private short ChassisSpeed => SimData?.SpeedForward ?? 0;

	/// <summary>
	/// <c>Flyer_Constructor</c>'s <c>Behaviour_SetState(flyer+0x4d, 0x499cf8)</c> — every aircraft
	/// starts in <c>deciding</c>, which has no think and no move, and waits for the reassess to read
	/// its group's order.
	/// </summary>
	internal void InstallInitialBehaviour() => Behaviour.SetState(FlyerBehaviourState.Deciding);

	/// <summary>
	/// <c>Mech_AiTick</c> (<c>00411cec</c>) as an aircraft runs it. Identical in shape to
	/// <see cref="MechObject.AiTick"/> — the dispatchers are the flyer's own vtable slots but the
	/// tick that drives them is the same function — so the order is reassess, dwell countdown, move,
	/// think, and the move is <see cref="Tick"/>'s, run from the object pass that precedes this one.
	///
	/// <para>The reassess is <c>FUN_00422d00</c> for every one of the seven states; there is no
	/// combat form. An aircraft picks its target inside its think and never changes state to do it.
	/// </para>
	/// </summary>
	public void AiTick(SimWorld world) {
		if (Behaviour.State is not { } state) {
			return;
		}

		if (Behaviour.DwellCountdown == 0) {
			SelectBehaviour();
		}

		if (Behaviour.State is { SuppressesDwell: false }) {
			SimMath.TimerCountDown(ref Behaviour.DwellCountdown);
		}

		bool finished = Behaviour.State?.Think switch {
			FlyerThinkSlot.Attack => AttackThink(world),
			FlyerThinkSlot.Patrol => PatrolThink(world),
			FlyerThinkSlot.SearchDestroy => SearchDestroyThink(world),
			FlyerThinkSlot.Scout => ScoutThink(world),
			_ => false
		};

		if (finished) {
			Behaviour.DwellCountdown = 0;
		}

		Behaviour.TickCount++;
	}

	/// <summary>
	/// <c>FUN_00422d00</c> — the flyer's one reassess: map the group's current order verb onto a
	/// state. Four of the seven verbs land somewhere.
	///
	/// <list type="bullet">
	/// <item>0 <c>search and destroy</c> → <see cref="FlyerBehaviourState.SearchAndDestroy"/></item>
	/// <item>3 <c>patrol</c> → <see cref="FlyerBehaviourState.Patrolling"/></item>
	/// <item>4 <c>sleep</c> → <see cref="FlyerBehaviourState.Sleeping"/>, and the aircraft is marked
	/// out of the fight</item>
	/// <item>5 <c>travel</c> → <see cref="FlyerBehaviourState.Scouting"/>, same</item>
	/// </list>
	///
	/// <para><b>Verbs 1, 2 and 6 install nothing</b>, and neither does an empty order slot. The
	/// original's switch has no default and the descriptor it is about to install lives in a register
	/// nothing on that path writes, so it hands <c>Behaviour_SetState</c> whatever the caller left
	/// there — the same defect <c>Mech_AiSelectBehaviour</c> has for an empty slot, and the same
	/// answer here: the aircraft keeps the state it has. See docs/simulation/ai-goals.md.</para>
	///
	/// <para><b>Sleeping and scouting latch <see cref="SimObject.OutOfAction"/>.</b> That is not a
	/// side effect — it is what keeps an aircraft that was sent somewhere rather than sent to fight
	/// from counting as something the other side has to contest, and nothing ever clears it.</para>
	/// </summary>
	private void SelectBehaviour() {
		switch (Group?.OrderVerb ?? MissionGroup.NoOrder) {
			case MissionOrder.VerbSearchDestroy:
				Behaviour.SetState(FlyerBehaviourState.SearchAndDestroy);
				break;
			case MissionOrder.VerbPatrol:
				Behaviour.SetState(FlyerBehaviourState.Patrolling);
				break;
			case MissionOrder.VerbSleep:
				Disarmed = true;
				Behaviour.SetState(FlyerBehaviourState.Sleeping);
				break;
			case MissionOrder.VerbTravel:
				Disarmed = true;
				Behaviour.SetState(FlyerBehaviourState.Scouting);
				break;
		}
	}

	/// <summary>
	/// <c>FUN_00422bdc</c> — <c>scouting</c>'s think, which is the movement step and nothing else.
	/// An aircraft travelling to somewhere does not look for anything on the way.
	/// </summary>
	private bool ScoutThink(SimWorld world) {
		FollowStep(world);
		return false;
	}

	/// <summary>
	/// <c>FUN_00422b34</c> — <c>patrolling</c>'s think. The movement step, and then, for the leader
	/// only and only once the sweep timer expires, a target sweep that takes <b>anything</b> it can
	/// see. <see cref="Ai.TargetFilter.RejectOwnClass"/> is the whole of the filter, so a flight on
	/// patrol will not chase other aircraft but will take any machine or structure.
	/// </summary>
	private bool PatrolThink(SimWorld world) {
		FollowStep(world);

		if (!IsFlightLeader || SimMath.CountdownTimerTick(ref _targetSweepTimer) != 0) {
			return false;
		}

		_targetSweepTimer = TargetSweepInterval;
		Target = AiTargeting.SelectTarget(world, this, TargetFilter.RejectOwnClass);

		if (Target != null) {
			EngageWithFlight();
		}

		return false;
	}

	/// <summary>
	/// <c>FUN_00422a80</c> — <c>search and destroy</c>'s think. The same shape as
	/// <see cref="PatrolThink"/> with two differences: the sweep also carries
	/// <see cref="Ai.TargetFilter.MissionTargetOnly"/>, and whatever it finds is then checked against
	/// <see cref="MissionGroup.IsOrderTarget"/> before it is taken. A flight under this order engages
	/// <b>only the thing the mission named</b> and ignores everything else it flies over.
	/// </summary>
	private bool SearchDestroyThink(SimWorld world) {
		FollowStep(world);

		if (!IsFlightLeader || SimMath.CountdownTimerTick(ref _targetSweepTimer) != 0) {
			return false;
		}

		_targetSweepTimer = TargetSweepInterval;

		var found = AiTargeting.SelectTarget(world, this,
			TargetFilter.MissionTargetOnly | TargetFilter.RejectOwnClass);

		if (found == null || Group?.IsOrderTarget(found) != true) {
			return false;
		}

		Target = found;
		EngageWithFlight();
		return false;
	}

	/// <summary>
	/// <c>FUN_00422ca8</c> — <c>attacking</c>'s think. It answers <b>finished</b> as soon as the
	/// target is crippled or destroyed, which zeroes the dwell and sends the aircraft back through
	/// the reassess onto whatever its group's order still says. Otherwise the leader flies the attack
	/// run and everyone else holds station on it.
	///
	/// <para>An aircraft in this state with nothing selected also answers finished. The original
	/// dereferences the null target here; there is no state in which it can legitimately be null,
	/// since the only way into <c>attacking</c> is a think that has just set one.</para>
	/// </summary>
	private bool AttackThink(SimWorld world) {
		if (Target is not { } target) {
			return true;
		}

		if (target.Destroyed || target is MechObject { Immobilised: true }) {
			return true;
		}

		if (IsFlightLeader) {
			AttackRun(world, target);
		} else {
			FormationStep(world);
		}

		return false;
	}

	/// <summary>
	/// <c>FUN_00422bf0</c> — puts the whole flight onto the leader's target. Called from a sweep that
	/// has just found something: the leader enters <c>attacking</c> and then hands every surviving
	/// wingman the same target and the same state, so a flight commits as one.
	///
	/// <para>The original is recursive and the recursion terminates because only the leader takes the
	/// loop; a wingman reached from it does no more than change state.</para>
	/// </summary>
	private void EngageWithFlight() {
		Behaviour.SetState(FlyerBehaviourState.Attacking);

		if (!IsFlightLeader || Group is not { } group) {
			return;
		}

		for (int i = 0; i < group.Members.Count; i++) {
			if (group.Members[i] is not FlyerObject wingman
					|| ReferenceEquals(wingman, this) || wingman.Destroyed) {
				continue;
			}

			wingman.Target = Target;
			wingman.EngageWithFlight();
		}
	}

	/// <summary>
	/// <c>FUN_00422a50</c> — the movement step the three non-combat thinks all open with: the flight
	/// leader flies the group's route, everybody else holds station on the leader.
	///
	/// <para>The original also zeroes <c>flyer+0x96</c> here, the radar mode — an aircraft's radar is
	/// held passive every tick it is not fighting. A flyer has no active-radar state in this engine
	/// (<see cref="SimObject.ScannerActive"/> is false for the class), so there is nothing to
	/// clear.</para>
	/// </summary>
	private void FollowStep(SimWorld world) {
		if (IsFlightLeader) {
			LeadRouteStep(world);
		} else {
			FormationStep(world);
		}
	}

	/// <summary>
	/// <c>FUN_004224c4</c> — the flight leader's route step. It looks one waypoint past the cursor,
	/// advances when it is within <see cref="WaypointArrivalRange"/>, and flies at the one it
	/// settled on while holding <see cref="CruiseAltitude"/>.
	///
	/// <para><b>The last waypoint is never reached.</b> The advance only runs on the look-ahead
	/// waypoint, so once there is nothing past the cursor the flight steers at the final point
	/// forever — and, since the route cursor is the group's, that is also what ends a movement order.
	/// See docs/simulation/ai-goals.md.</para>
	///
	/// <para>A group with no route at all leaves the original dereferencing a null waypoint. Here the
	/// aircraft flies straight and holds its altitude instead.</para>
	/// </summary>
	private void LeadRouteStep(SimWorld world) {
		int elevator;
		var group = Group;
		var ahead = group?.WaypointAt(group.RouteCursor + 1);
		var waypoint = ahead ?? group?.WaypointAt(group.RouteCursor);

		if (waypoint is not { } target) {
			elevator = PitchToAltitude(CruiseAltitude);
			SteerAndFly(world, 0, elevator);
			return;
		}

		if (ahead != null && GroundDistanceTo(target) < WaypointArrivalRange) {
			group!.AdvanceRouteCursor();
		}

		elevator = PitchToAltitude(CruiseAltitude);

		short turn = (short)SimMath.Q16Multiply(
			(short)(Detection.HeadingToward(target, Position) - (short)Heading), RouteTurnGain);

		SteerAndFly(world, turn, elevator);
	}

	/// <summary>
	/// <c>FUN_00422598</c> — a wingman's station-keeping. Its station is a point
	/// <see cref="StationLead"/> ahead of the leader along the leader's own heading, plus this
	/// aircraft's <c>FFORMS.DAT</c> offset rotated by that same heading — so the flight tracks a spot
	/// the leader is flying into rather than the leader itself.
	///
	/// <para>The overshoot case is worth reading twice: a wingman that is <i>ahead</i> of its station
	/// (the station bearing outside ±90°) while still lined up with the leader and inside
	/// <see cref="StationOvershootRange"/> has its steering command <b>mirrored</b> rather than
	/// reversed, so it eases back onto the station from in front instead of hauling round through a
	/// half turn.</para>
	///
	/// <para>Not reproduced: <c>FUN_00422260</c>, which works a throttle setting out of the station
	/// error and the leader's speed and writes it to <c>flyer+0x21c</c>. That field has no reader
	/// anywhere in the image, and the control law leaves the flight model's throttle element zero at
	/// every site, so nothing a Cybrid flyer decides can change its airspeed. See
	/// <see cref="InitialThrottle"/>.</para>
	/// </summary>
	private void FormationStep(SimWorld world) {
		if (Group?.Leader is not { } leader) {
			return;
		}

		var station = FormationStation(leader);
		int distance = station.ApproxDistanceTo(Position);
		short bearing = (short)(Detection.HeadingToward(station, Position) - (short)Heading);

		bool alignedWithLeader =
			(ushort)((short)Heading - (short)leader.Heading + 0x4000) < 0x8000;
		bool stationBehind = (ushort)(bearing + 0x4000) > 0x7fff;

		if (alignedWithLeader && stationBehind && distance < StationOvershootRange) {
			bearing = (short)(short.MinValue - bearing);
		}

		int elevator = PitchToAltitude(CruiseAltitude);
		SteerAndFly(world, bearing, elevator);
	}

	/// <summary>
	/// Where this wingman should be — <c>Math_OffsetPointByBearing</c> onto the leader's position
	/// followed by the flyer's own vtable <c>+0x78</c> (<c>FUN_00421e98</c>), which is
	/// <c>Formation_RotateAndAddOffset</c> (<c>00411d64</c>) with an <c>FFORMS.DAT</c> slot. The
	/// rotation is by the <b>leader's</b> heading, not this aircraft's — the original looks the
	/// leader up out of the group rather than using the object it was handed — and the offset's Z
	/// goes on unrotated, which is what stacks a flight vertically.
	/// </summary>
	private Vec3i FormationStation(SimObject leader) {
		var station = OffsetByBearing(leader.Position, (short)leader.Heading, StationLead);

		if (FormationOffset is not { } offset) {
			return station;
		}

		short cos = BinaryAngle.Cos(leader.Heading);
		short sin = BinaryAngle.Sin(leader.Heading);

		return new Vec3i(
			station.X + (int)(((long)offset.X * cos - (long)offset.Y * sin + 0x2000) >> 14),
			station.Y + (int)(((long)offset.X * sin + (long)offset.Y * cos + 0x2000) >> 14),
			station.Z + offset.Z);
	}

	/// <summary>
	/// <c>FUN_004226a0</c> — the attack run, and the only place a Cybrid flyer shoots. It is a
	/// two-phase circuit rather than a pursuit: an aircraft <b>extends away</b> from its target until
	/// it has <see cref="AttackTurnInRange"/> of room, turns back in, makes one pass, and breaks off
	/// again the moment the pass is over. A flight therefore keeps coming round rather than trying to
	/// stay on something that can turn inside it.
	///
	/// <list type="number">
	/// <item><b>Extending</b> (<see cref="_attackPhase"/> 0). The steering command is reversed — the
	/// aircraft flies <i>away</i> — and it holds <see cref="AttackApproachHeight"/> above the target.
	/// Past <see cref="AttackTurnInRange"/> it turns in.</item>
	/// <item><b>Running in</b> (phase 1). Outside <see cref="AttackRunRange"/> it is still just
	/// closing at the approach height; inside it, it aims its nose at the target itself. The run ends
	/// — back to extending — as soon as the target leaves ±45°, the aircraft drops below
	/// <see cref="RunHeightFloor"/> over it, or it gets inside <see cref="RunBreakoffRange"/>.</item>
	/// </list>
	///
	/// <para><b>The aim is led.</b> The target's position is offset along its own heading by the
	/// flight time of the shot, at the projectile's speed rather than the aircraft's once the run is
	/// close enough to shoot.</para>
	///
	/// <para><b>The first shot of a pass is a missile.</b> A flag on the aircraft (<c>flyer+0x5a</c>,
	/// cleared every time it goes back to extending) lets one round off the rail inside
	/// <see cref="MissileRange"/>, and every shot after it on that pass is a pair of gun rounds from
	/// the mirrored muzzle points.</para>
	///
	/// <para>One gate is not reproduced: the original also requires <c>|flyer+0x1f4| &lt; 10</c>, a
	/// field read here and written nowhere in the image, so it is zero and the gate always
	/// passes.</para>
	/// </summary>
	private void AttackRun(SimWorld world, SimObject target) {
		var aim = target.Position;
		int range = GroundDistanceTo(aim);
		short bearing = (short)(Detection.HeadingToward(aim, Position) - (short)Heading);

		short shotSpeed = TravelSpeed;
		short targetSpeed = target.TravelSpeed;

		if (_attackPhase == PhaseRunIn && range < AttackRunRange && GunProjectile is { } gun) {
			shotSpeed = gun.Speed;
		}

		if (shotSpeed > 0 && targetSpeed != 0) {
			int capped = range > LeadRangeCap ? LeadRangeCap : range;
			short lead = (short)((short)(capped >> 3) * (targetSpeed << 3) / shotSpeed);
			aim = OffsetByBearing(aim, (short)target.Heading, lead);
			bearing = (short)(Detection.HeadingToward(aim, Position) - (short)Heading);
		}

		int elevator;

		if (_attackPhase == PhaseExtend) {
			bearing = (short)(bearing + short.MinValue);
			_missileFired = false;

			if (range > AttackTurnInRange) {
				_attackPhase = PhaseRunIn;
			}

			elevator = PitchToAltitude(target.Position.Z + AttackApproachHeight);
		} else if (range < AttackRunRange) {
			elevator = PitchCommand((short)ElevationToward(aim));

			bool onTheRun = (ushort)(bearing + RunBearingLimit) < 0x4000
				&& Position.Z - target.Position.Z >= RunHeightFloor
				&& range >= RunBreakoffRange;

			if (!onTheRun) {
				_attackPhase = PhaseExtend;
			}
		} else {
			elevator = PitchToAltitude(target.Position.Z + AttackApproachHeight);
		}

		if (_attackPhase != PhaseExtend && range < AttackRunRange
				&& (ushort)(bearing + FiringError) < FiringError * 2
				&& (ushort)(ElevationToward(aim) - Pitch + FiringError) < FiringError * 2
				&& SimMath.CountdownTimerTick(ref _refireTimer) == 0) {
			Shoot(world, range);
			_refireTimer = RefireDelay;
		}

		SteerAndFly(world, bearing, elevator);
	}

	/// <summary>
	/// One trigger pull: the missile if this pass still has it and the target is inside
	/// <see cref="MissileRange"/>, otherwise a pair of gun rounds off the two mirrored muzzles.
	/// </summary>
	private void Shoot(SimWorld world, int range) {
		var frame = Rotation();
		var aim = ((short)Pitch, (short)Roll, (short)Heading);

		if (!_missileFired && range < MissileRange && MissileProjectile is { } missile) {
			world.FireRocket(missile, frame.TransformPoint(Muzzle.X, Muzzle.Y, Muzzle.Z), aim,
				ChassisSpeed, this);
			_missileFired = true;
			return;
		}

		if (GunProjectile is not { } gun) {
			return;
		}

		world.FireBullet(gun, frame.TransformPoint(Muzzle.X, Muzzle.Y, Muzzle.Z), aim,
			TravelSpeed, 0, this);
		world.FireBullet(gun, frame.TransformPoint(-Muzzle.X, Muzzle.Y, Muzzle.Z), aim,
			TravelSpeed, 0, this);
	}

	/// <summary>
	/// <c>FUN_00492850</c> — the elevation angle from this aircraft to a point, against the
	/// simulation's own sqrt-free ground distance.
	/// </summary>
	private int ElevationToward(Vec3i point) =>
		SimTrig.Atan2Guarded(point.Z - Position.Z, GroundDistanceTo(point));

	/// <summary>
	/// <c>Math_GroundDistanceBetweenPoints</c> (<c>004927c4</c>) — Z dropped before the magnitude, so
	/// height never counts toward a range the AI decides on.
	/// </summary>
	private int GroundDistanceTo(Vec3i point) =>
		SimMath.FastMagnitude2D(Position.X - point.X, Position.Y - point.Y);

	/// <summary>
	/// <c>Math_OffsetPointByBearing</c> (<c>004928f0</c>) — moves a point along a bearing on the
	/// ground plane, the bearing quarter-turned back because the simulation's forward axis is model Y.
	/// </summary>
	private static Vec3i OffsetByBearing(Vec3i point, short bearing, int distance) {
		short turned = (short)(bearing + BinaryAngle.QuarterTurn);
		return new Vec3i(
			point.X + SimMath.Q14Multiply(distance, SimTrig.Cos(turned)),
			point.Y + SimMath.Q14Multiply(distance, SimTrig.Sin(turned)),
			point.Z);
	}

	/// <summary>
	/// Whether this aircraft is its group's first member, which is the whole of what the flyer AI
	/// means by a leader: the leader decides and everyone else keeps station.
	/// </summary>
	private bool IsFlightLeader => ReferenceEquals(this, Group?.Leader);

	/// <summary><c>flyer+0x1fe</c> is 0 while the aircraft is extending away from its target.</summary>
	private const short PhaseExtend = 0;

	/// <summary>And 1 while it is running in.</summary>
	private const short PhaseRunIn = 1;

	// flyer+0x1fe, +0x5a, +0x5b and +0x21f. The sweep timer and the missile flag share a word in the
	// original, which writes the flag two bytes wide over the timer's own low byte; they are separate
	// here because no state runs both.
	private short _attackPhase;
	private bool _missileFired;
	private short _targetSweepTimer;
	private short _refireTimer;

	/// <summary>
	/// This aircraft's unrotated <c>FFORMS.DAT</c> station offset, or null for the flight leader and
	/// for a formation that names no slot for it. Resolved at mission load and applied to the
	/// leader's live position every tick by <see cref="FormationStation"/>.
	/// </summary>
	public Vec3i? FormationOffset { get; set; }

	/// <summary>The <c>PROJ.DAT</c> record the guns fire — see <see cref="GunProjectileIndex"/>.</summary>
	public ProjectileData.Projectile? GunProjectile { get; init; }

	/// <summary>The one the launcher fires — see <see cref="MissileSubtype"/>.</summary>
	public ProjectileData.Projectile? MissileProjectile { get; init; }
}
