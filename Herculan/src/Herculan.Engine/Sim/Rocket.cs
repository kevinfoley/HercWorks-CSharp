using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Dbsim;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// A launcher's round — DBSIM's <c>Missile</c> class, built by <c>Missile_Construct</c>
/// (<c>0040a948</c>, vtable <c>PTR_Bullet_Draw_00498448</c>) and advanced by
/// <c>Rocket_TickUpdate</c> (<c>0040a538</c>, vtable <c>+0x14</c>). It is the third and last fire
/// branch, and the one weapon class that fired nothing at all until now.
///
/// <para>Mechanically it is a <see cref="Projectile"/> with a different engine in it. Both are
/// allocated out of the same effect pool (<c>DAT_004a9746</c>) that <c>Sim_MainTick</c> walks ahead
/// of the machine list, both carry a euler triple plus a transform whose translation is the
/// position, and both sweep the segment they are about to cross rather than testing a point. What is
/// different is everything about how they move:</para>
///
/// <list type="bullet">
/// <item><b>It accelerates.</b> A gun round leaves the barrel at its final speed; a rocket leaves at
/// a flat <see cref="LaunchSpeed"/> over the launching machine's own travel speed and climbs from
/// there toward the <c>PROJ.DAT</c> record's <c>Speed</c>. That ceiling is 6000 on every retail
/// launcher and the round never reaches it: the burn adds about 39 per tick and the round has 80
/// ticks to live, so it burns out at roughly 3600 — a rocket is slow off the rail and still
/// accelerating when it arrives.</item>
/// <item><b>Its lifetime is in ticks.</b> A bullet ages by <c>0x200</c> per tick against
/// <c>Lifetime * 0x200</c>; a rocket's counter is a plain <c>+1</c> against <c>Lifetime</c>
/// directly.</item>
/// <item><b>It has no firing scatter</b> and no power scaling — a launcher is neither a magazine
/// weapon that disperses nor a capacitor weapon whose shot is worth what it was charged to. The
/// aim angles go in exactly as the mount handed them over and the record's damage applies at face
/// value.</item>
/// <item><b>It is the class guidance was written for.</b> The plasma round borrows a cut-down
/// version; this one leads its target, has a per-subtype gate on whether it may lock at all, and a
/// player-flown branch. None of it runs yet — see <see cref="HomingTick"/>.</item>
/// </list>
///
/// <para>Only <c>PROJ.DAT</c> <see cref="ProjectileType.Missile"/> records reach here.
/// <see cref="ProjectileType.Rocket"/> (type 3) records exist in retail data and are unreachable:
/// <c>Rocket_ConstructGuided</c> (<c>0040ac3c</c>) builds their class, nothing calls it, and its
/// vtable's per-tick slot is <c>FUN_0040acb4</c> — a stub returning zero, so an instance of it would
/// never move and never die. The ammunition dispatch tests for type 0 and nothing else.</para>
/// </summary>
public sealed class Rocket {
	private readonly ProjMissileDatEntry _record;
	private Transform3 _frame;
	private short _eulerX;
	private short _eulerY;
	private short _eulerZ;
	private bool _frameStale;
	private short _age;
	private short _animationTimer;
	private short _speed;

	/// <summary>
	/// <c>Rocket_Fire</c> (<c>0040a9c4</c>) — the spawn, which is the whole of what happens between
	/// the allocation and the round's first tick.
	/// </summary>
	/// <param name="projectile">The firing <c>PROJ.DAT</c> record.</param>
	/// <param name="record">Its <c>ROCKETS.DAT</c> record, looked up by the same subtype id.</param>
	/// <param name="muzzle">Where the round starts — the fire prologue's world muzzle point.</param>
	/// <param name="aim">
	/// Which way it points, as the euler triple the prologue extracts from the shot transform. Unlike
	/// <see cref="Projectile"/>'s, it is used verbatim: no <c>ROCKETS.DAT</c> field is a scatter and
	/// the spawn draws no random numbers at all.
	/// </param>
	/// <param name="ownerSpeed">The launching machine's own travel speed — see <see cref="Speed"/>.</param>
	/// <param name="owner">The machine that fired, which the sweep skips.</param>
	internal Rocket(ProjectileData.Projectile projectile, ProjMissileDatEntry record,
			Vec3i muzzle, (short X, short Y, short Z) aim, short ownerSpeed, SimObject? owner) {
		Data = projectile;
		_record = record;
		Owner = owner;

		_eulerX = aim.X;
		_eulerY = aim.Y;
		_eulerZ = aim.Z;
		_frameStale = true;

		_frame.X = muzzle.X;
		_frame.Y = muzzle.Y;
		_frame.Z = muzzle.Z;

		// The launch speed is a literal in the spawn, not a record field: every rocket leaves the rail
		// at the same rate regardless of what it is, and the launching machine's own travel speed is
		// added on top exactly as it is for a gun round.
		_speed = (short)(ownerSpeed + LaunchSpeed);
		_animationTimer = AnimationInterval(record);
	}

	/// <summary>
	/// <c>Rocket_Fire</c>'s literal <c>0x1f4</c>: the speed a round leaves the launcher at, before
	/// the machine's own travel speed is added and before <see cref="Tick"/> starts accelerating it.
	/// </summary>
	public const short LaunchSpeed = 500;

	/// <summary>The <c>PROJ.DAT</c> record this round came from — its damage, its splash and its subtype id.</summary>
	public ProjectileData.Projectile Data { get; }

	/// <summary>
	/// The record's subtype id, <c>+0x41</c>. It indexes <see cref="RocketCatalog"/>, picks the shape
	/// drawn, and selects which of the guidance branches the round would take.
	/// </summary>
	public short MissileId => Data.MissileId;

	/// <summary>
	/// Subtype 2 — <c>ARM</c>, the anti-radiation missile. <c>Rocket_HomingSteer</c> singles it out by
	/// literal value: alone among the five it steers only while its target is <i>emitting</i>
	/// (<c>target+0x96</c>, the scanner the pilot toggles, or <c>target+0xa1</c>, its jammer), and it
	/// is exempt from the spoofing wobble every other subtype can suffer.
	/// </summary>
	public const short AntiRadiationSubtype = 2;

	/// <summary>
	/// Subtype 3 — <c>EO</c>, the electro-optical missile, the one the pilot flies himself.
	/// <c>Rocket_TickUpdate</c> sends it to <c>Rocket_PlayerSteer</c> (<c>0040a488</c>) instead of
	/// the seeker when its owner is the locally-simulated machine, and that reads the player's stick
	/// straight out of the global input block at <c>0x4d234a</c>. See <see cref="Tick"/>.
	/// </summary>
	public const short PlayerFlownSubtype = 3;

	/// <summary>The machine that fired. The sweep skips it, so nothing shoots itself.</summary>
	public SimObject? Owner { get; }

	/// <summary>
	/// <c>+0x52</c>. How far the round travels per 125 ms, as
	/// <see cref="SimMath.IntegrateRateOverTick"/> reads a rate. Unlike a gun round's this is not
	/// fixed at launch: it climbs each tick toward the <c>PROJ.DAT</c> record's own <c>Speed</c>.
	/// </summary>
	public short Speed => _speed;

	/// <summary>
	/// The round's frame: where it is, and which way it is going. The translation is the position —
	/// the original keeps no separate one.
	///
	/// <para>Reading it settles the rotation if the angles have moved since the last tick, for the
	/// same reason <see cref="Projectile.Frame"/> does: without it a round would be drawn unrotated
	/// for the frames between the tick that spawned it and the tick that first moves it.</para>
	/// </summary>
	public Transform3 Frame {
		get {
			RebuildFrame();
			return _frame;
		}
	}

	/// <summary>Where the round is, in world units.</summary>
	public Vec3i Position => new(_frame.X, _frame.Y, _frame.Z);

	/// <summary>
	/// <c>+0x54</c>, and it is a plain tick count — <c>Rocket_TickUpdate</c>'s <c>+ 1</c> against the
	/// record's <see cref="ProjMissileDatEntry.Lifetime"/> with no scaling in between. Retail's 80
	/// is 3.2 s at the simulation's rate.
	///
	/// <para>That makes a rocket the one shot in the simulation whose <i>range</i> is frame-rate
	/// dependent in the original: it lives a fixed number of frames while each frame's step scales
	/// with the timestep, so a slower machine threw its missiles further. The engine's fixed timestep
	/// pins it to what the original produces at its own 40 ms cap.</para>
	/// </summary>
	public short Age => _age;

	/// <summary>
	/// What the round struck, if it struck anything — set on the tick that ends its life. Not part of
	/// the original, which simply frees the object.
	/// </summary>
	public SimObject? HitObject { get; private set; }

	/// <summary>Whether the round ended by burning out rather than by hitting something.</summary>
	public bool Expired { get; private set; }

	/// <summary>
	/// The shape's cell-animation frame counter, which the renderer wraps against the drawn shape's
	/// own frame count — the same arrangement <see cref="Projectile.AnimationFrame"/> has, and for the
	/// same reason: the simulation stays clear of needing to know what the shape looks like.
	/// </summary>
	public int AnimationFrame { get; private set; }

	/// <summary>
	/// <c>Rocket_TickUpdate</c> (<c>0040a538</c>), vtable <c>+0x14</c> — one step of flight and the
	/// hit test that goes with it.
	///
	/// <list type="number">
	/// <item><b>Animation</b>, when the record asks for it.</item>
	/// <item><b>Age.</b> One per tick, against the record's lifetime. A round that outlives it is
	/// dropped where it is, with no impact of any kind — a rocket burns out, it does not detonate on
	/// a timer.</item>
	/// <item><b>Acceleration</b>, see below.</item>
	/// <item><b>Guidance</b> — the seeker, or the player's own stick for
	/// <see cref="PlayerFlownSubtype"/> fired by the locally-simulated machine.</item>
	/// <item><b>The step</b>, <c>IntegrateRateOverTick(speed)</c>, taken along the frame's Y axis.</item>
	/// <item><b>The hit test is a raycast over that step alone</b>, exactly as a gun round's is, with
	/// the record's own slack in place of the beam's literal 200.</item>
	/// </list>
	///
	/// <para><b>The acceleration is damped, not linear.</b> The original adds the record's rate to
	/// the speed and then averages the result with the speed it started the tick at —
	/// <c>speed = (speed + rate·dt + speed) / 2</c> — so the round takes half the step it would
	/// otherwise, and the whole climb is capped at the <c>PROJ.DAT</c> record's <c>Speed</c>. Kept
	/// literally: it is what the burn curve is.</para>
	///
	/// <para><b>Two things the original does here are left out</b>, both belonging to systems that do
	/// not exist. The proximity warning — the beep a rocket plays once it comes within 40000 units of
	/// the camera's own machine — is sound, which is unported throughout. And the pair of globals that
	/// track the missile the player is flying (<c>DAT_0049c394</c> and <c>DAT_0049c398</c>) exist to
	/// tell the cockpit that its missile view is over; there is no missile view.</para>
	/// </summary>
	/// <returns>Whether the round is finished and should be freed.</returns>
	internal bool Tick(SimWorld world) {
		AnimationTick();

		_age = (short)(_age + 1);
		if (_record.Lifetime < _age) {
			Expired = true;
			return true;
		}

		AccelerationTick();
		GuidanceTick();

		short step = (short)SimMath.IntegrateRateOverTick(_speed);
		RebuildFrame();
		var advanced = _frame.TransformPoint(0, step, 0);

		// Power is zero: a launcher spends a round out of a rack, never a capacitor charge, so the
		// record's two damage figures apply at face value. The clearance is the ROCKETS.DAT record's
		// own — 200 for the four ordinary missiles, 300 for the big one, which is what makes BMSL the
		// more forgiving hit.
		var shot = new WeaponShot(_frame, step, Data, 0, Owner, ClipRadius(_record));
		if (world.Raycast(shot) != 0) {
			HitObject = shot.HitObject;
			world.RecordProjectileHit(shot);
			return true;
		}

		_frame.X = advanced.X;
		_frame.Y = advanced.Y;
		_frame.Z = advanced.Z;
		return false;
	}

	/// <summary>
	/// The burn: <c>speed += IntegrateRateOverTick(record.Acceleration)</c>, averaged with the speed
	/// the tick opened at, then capped at the <c>PROJ.DAT</c> record's <c>Speed</c>.
	///
	/// <para>Every retail record carries the same rate, 250, which the timestep turns into 79 and the
	/// damping halves to 39 per tick. Against a ceiling of 6000 and a life of 80 ticks the cap is a
	/// ceiling the round never touches — it is still burning when it burns out, and what the record's
	/// <c>Speed</c> really sets on retail data is nothing at all.</para>
	/// </summary>
	private void AccelerationTick() {
		short opening = _speed;
		_speed = (short)(_speed + SimMath.IntegrateRateOverTick(Acceleration(_record)));
		_speed = (short)((_speed + opening) >> 1);

		if (Data.Speed < _speed) {
			_speed = Data.Speed;
		}
	}

	/// <summary>
	/// <c>Rocket_TickUpdate</c>'s guidance branch: the pilot flies an <see cref="PlayerFlownSubtype"/>
	/// round of his own, everything else seeks.
	///
	/// <para><b>The player's branch is not ported.</b> <c>Rocket_PlayerSteer</c> (<c>0040a488</c>)
	/// reads two axis accumulators straight out of the global input block the frame loop fills
	/// (<c>0x4d234a</c>), consumes them and zeroes them, and steers by <c>Q8Multiply(0x500, axis)</c>
	/// per tick with no rate limit and no deadband — it is a missile the pilot flies from a camera in
	/// its nose, and there is no missile view here to fly it from. The original's own no-input state
	/// is destructive rather than inert (it drops the round's target and rewrites its subtype id to
	/// zero, which would change both the record it reads and the shape it draws mid-flight), so
	/// reproducing that with an input source that can never be fed would be reproducing a state the
	/// original only ever passes through, not one it sits in. A player-flown round flies straight
	/// instead.</para>
	/// </summary>
	private void GuidanceTick() {
		if (MissileId == PlayerFlownSubtype && Owner is MechObject { IsPlayer: true }) {
			return;
		}

		HomingTick();
	}

	/// <summary>
	/// <c>Rocket_HomingSteer</c> (<c>0040a254</c>) — the seeker, which is a steer of the round's euler
	/// angles rather than of a velocity, exactly as the plasma round's is, but with a real lead and
	/// three gates on top.
	///
	/// <para><b>Nothing homes</b>, and for the same reason nothing homes in <see cref="Projectile"/>:
	/// <c>Rocket_Fire</c> attaches the launching machine's <i>selected target</i>
	/// (<c>mech+0x1a4</c>), and there is no target selection — see <see cref="SimWorld.FireRocket"/>.
	/// A rocket therefore flies where it was pointed, which is what the original does with nothing
	/// selected too.</para>
	///
	/// <para>Three gates the original applies are consequently not reachable either, and are recorded
	/// here rather than written as branches on inputs that do not exist:</para>
	///
	/// <list type="bullet">
	/// <item><b>The lead point.</b> A round whose lock is on a specific node of the target
	/// (<c>+0x5a</c>, filled at launch from the target's own vtable <c>+0x54</c>) steers at that
	/// node's world position rather than at the object's origin; a round without one steers at the
	/// target's extrapolated position.</item>
	/// <item><b>The emission gate.</b> <see cref="AntiRadiationSubtype"/> steers only while the target
	/// has its scanner or its jammer on.</item>
	/// <item><b>The spoofing wobble.</b> When the launching machine's own <c>+0x9c</c> flag is set —
	/// which <c>Mech_PerTickSystemsUpdate</c> (<c>0041aa5c</c>) rolls for each tick the machine's
	/// selected target is jamming — every subtype but the anti-radiation one has its aim error pushed
	/// <i>away</i> by <c>0xc00</c> whenever it falls inside <c>±0xc00</c>, so the round weaves around
	/// the target instead of converging on it. That is the mechanical form of the manual's ECM.</item>
	/// </list>
	/// </summary>
	private void HomingTick() {
		if (Target == null) {
			return;
		}

		// The emission gate. An anti-radiation round steers only while its target is emitting — its
		// scanner or its jammer — and coasts on its current heading the moment either goes quiet.
		// Reachable now that a target can be selected; nothing turns either emitter on yet, so in
		// practice this subtype flies straight, which is also what it does against a silent target in
		// the original.
		if (MissileId == AntiRadiationSubtype
				&& !Target.ScannerActive && !Target.JammerActive) {
			return;
		}

		var (bearingX, _, bearingZ) = SimTrig.EulerToward(Target.AimPoint, Position);

		short pitchRate = 0;
		short yawRate = 0;
		SimMath.RateLimitedMoveToward(ref pitchRate, (short)(bearingX - _eulerX), HomingTurnRate);
		SimMath.RateLimitedMoveToward(ref yawRate, (short)(bearingZ - _eulerZ), HomingTurnRate);

		_eulerX = (short)(_eulerX + SimMath.IntegrateRateOverTick(pitchRate));
		_eulerZ = (short)(_eulerZ + SimMath.IntegrateRateOverTick(yawRate));
		_frameStale = true;
	}

	/// <summary>
	/// The cap <c>Rocket_HomingSteer</c> puts on how fast a seeking round may turn — <c>0x500</c> per
	/// 125 ms, twice what the plasma round is allowed.
	/// </summary>
	public const short HomingTurnRate = 0x500;

	/// <summary>
	/// <c>+0x56</c>, what a seeking round is chasing. <c>Rocket_Fire</c> fills it from the launching
	/// machine's selected target, but only when the machine's vtable <c>+0x6c</c> says this subtype is
	/// available — and that reads the mount manager's per-ammunition-type counter array at
	/// <c>manager+0x0a</c>, which has readers and no traced writer. See
	/// <see cref="SimWorld.FireRocket"/>.
	/// </summary>
	public SimObject? Target { get; internal set; }

	/// <summary>
	/// <c>Rocket_TickUpdate</c>'s opening step, and the same countdown-and-reload
	/// <see cref="Projectile"/> runs — with one difference: a rocket's record names <i>which</i> of
	/// the shape's sequences the interval steps (<c>ROCKETS.DAT +0x0a</c>), where a bullet always
	/// steps the first. Retail names sequence zero on all five records, and every
	/// <c>TSCellAnimPart</c> in both <c>ROCKETS.DTS</c> roots carries <c>AnimSequence == 0</c>, so the
	/// record really does drive them.
	///
	/// <para><b>What animates is the exhaust flame.</b> Unlike the EMP round's, a rocket's flipbook is
	/// geometry rather than billboards: two alternate cones of flat polys at the tail, in the
	/// palette's red and yellow-white range, beside a static grey body. See
	/// <see cref="Scene.SceneModelLibrary.Rocket"/>.</para>
	///
	/// <para>Zero interval means a static shape, which is the big missile alone — its shape has the
	/// two cells but nothing ever steps them, so its flame is frozen. The four ordinary rounds run at
	/// 256, the same figure the EMP rounds use, which works out to a cell every four ticks.</para>
	/// </summary>
	private void AnimationTick() {
		if (AnimationInterval(_record) == 0) {
			return;
		}

		if (SimMath.CountdownTimerTick(ref _animationTimer) == 0) {
			_animationTimer = AnimationInterval(_record);
			AnimationFrame++;
		}
	}

	/// <summary>
	/// The dirty-flag rebuild at <c>+0x32</c>, which the tick performs twice and the draw a third
	/// time. Identical to <see cref="Projectile"/>'s: the two classes share the base object's frame.
	/// </summary>
	private void RebuildFrame() {
		if (!_frameStale) {
			return;
		}

		var rotation = Transform3.FromEuler(_eulerX, _eulerY, _eulerZ);
		rotation.X = _frame.X;
		rotation.Y = _frame.Y;
		rotation.Z = _frame.Z;
		_frame = rotation;
		_frameStale = false;
	}

	/// <summary>
	/// <c>ROCKETS.DAT +0x04</c>. The field's property name is <c>ClipRadius</c> because
	/// <c>BULLETS.DAT</c> keeps that there and the two files share one parser; on a rocket record it
	/// is the burn rate — see <see cref="RocketCatalog"/>.
	/// </summary>
	private static short Acceleration(ProjMissileDatEntry record) => record.ClipRadius;

	/// <summary>
	/// <c>ROCKETS.DAT +0x06</c>, the shot record's slack. Same story as <see cref="Acceleration"/>:
	/// the property is named for what a bullet keeps at that offset.
	/// </summary>
	private static short ClipRadius(ProjMissileDatEntry record) => record.Unk2Flag;

	/// <summary><c>ROCKETS.DAT +0x08</c>, the animation frame interval. As above.</summary>
	private static short AnimationInterval(ProjMissileDatEntry record) => record.SfxFireIdBullets;
}
