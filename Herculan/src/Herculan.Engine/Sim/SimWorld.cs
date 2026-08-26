using HercWorks.Core.Data.File.Dat.Sim;
using Herculan.Engine.Numerics;
using Herculan.Engine.Terrain;

namespace Herculan.Engine.Sim;

/// <summary>
/// The running simulation: one loaded zone's terrain plus the live object list, advanced on a fixed
/// timestep. This mirrors DBSIM's per-frame sim tick (<c>FUN_0045f464</c>), which refreshes the
/// global timestep from a timer and then walks each global object list calling every live object's
/// per-tick update.
///
/// <para>Deliberately holds no rendering state. Per docs/engine/planning.md's "library core + thin
/// front-end host" decision, a world can be ticked by a game loop, a future mission editor, or a
/// headless test with no assumption that a window exists.</para>
/// </summary>
public sealed class SimWorld {
	private readonly List<SimObject> _objects = new();
	private readonly List<WeaponShot> _beams = new();
	private readonly List<WeaponShot> _impacts = new();
	private readonly List<BeamTracer> _tracers = new();
	private readonly List<Projectile> _projectiles = new();
	private readonly List<ImpactEffect> _effects = new();

	/// <param name="terrain">The loaded zone.</param>
	/// <param name="bullets">
	/// <c>dat\BULLETS.DAT</c>, which everything that fires a travelling shot needs — see
	/// <see cref="FireBullet"/>. Null leaves those weapons firing blanks, the same way an unported
	/// branch does.
	/// </param>
	/// <param name="explosions">
	/// <c>dat\EXPLOS.DAT</c>, which everything that lands needs — see
	/// <see cref="SpawnImpactEffect"/>. Null leaves impacts invisible.
	/// </param>
	/// <param name="seed">Seed for <see cref="Random"/>.</param>
	public SimWorld(HeightGrid terrain, BulletCatalog? bullets = null,
			ExplosionCatalog? explosions = null, int seed = 0) {
		Terrain = terrain;
		Bullets = bullets;
		Explosions = explosions;
		Random = new SimRandom(seed);
	}

	/// <summary>The loaded zone's terrain. One zone is active at a time, as in the original.</summary>
	public HeightGrid Terrain { get; }

	/// <summary>The travelling-projectile table, or null when the resource was not loaded.</summary>
	public BulletCatalog? Bullets { get; }

	/// <summary>The impact-effect table, or null when the resource was not loaded.</summary>
	public ExplosionCatalog? Explosions { get; }

	/// <summary>
	/// The simulation's pseudo-random generator — DBSIM's single global state block at
	/// <c>0x4d261d</c>, which every roll in the simulation shares. Weapon scatter is the first thing
	/// in the engine to draw on it during a tick.
	/// </summary>
	public SimRandom Random { get; }

	/// <summary>Live simulation objects, including any flagged <see cref="SimObject.Removed"/>.</summary>
	public IReadOnlyList<SimObject> Objects => _objects;

	/// <summary>Ticks elapsed since the world was created.</summary>
	public long TickCount { get; private set; }

	/// <summary>
	/// Simulation rate — <b>the original's own</b>, recovered 2026-08-21. DBSIM's frame loop
	/// (<c>FUN_004677bc</c>) spins on <c>GetTickCount</c> until 40 ms have passed, so the sim runs
	/// at a 25 Hz cap and its timestep is however long the frame actually took.
	///
	/// <para>The engine's sim is a fixed timestep decoupled from rendering, so unlike the original
	/// its behaviour does not vary with how fast frames are drawn.</para>
	///
	/// <para>One deliberate deviation goes with that: <c>SpeedAccelDecel</c> and <c>DecelTurning</c>
	/// are raw per-tick steps in the original, with no timestep scaling at all, which is what made a
	/// HERC's acceleration and turn ramp frame-rate dependent there. The engine routes them through
	/// <see cref="SimMath.ScalePerTickStep"/> so the ramp is tied to elapsed time rather than to
	/// this constant. That is exact at 25 Hz — nothing about vanilla behaviour changes — but it
	/// means changing this constant no longer silently rescales acceleration.</para>
	/// </summary>
	public const int TicksPerSecond = 25;

	/// <summary>
	/// The value written into <see cref="SimMath.TickDelta"/> for each tick.
	///
	/// <para><b>Recovered, no longer a guess.</b> DBSIM computes it as
	/// <c>clamp((elapsedMs &lt;&lt; 8) / 125, 0x40, 0x1c2)</c>, so the Q8 unit 0x100 is
	/// <b>125 ms</b> and every rate in the sim is per-125 ms. At the 40 ms frame cap that is
	/// <c>40 * 256 / 125 = 81</c>. The engine's fixed timestep pins it there, which is what the
	/// original produces on any machine fast enough to hit its own cap.</para>
	///
	/// <para>This replaces an earlier provisional pairing of 256 at 30 Hz, chosen when neither the
	/// timer source nor the unit had been traced. Everything that integrates through
	/// <see cref="SimMath.IntegrateRateOverTick"/> now runs at the original's actual rate.</para>
	/// </summary>
	public const short TickDelta = SimMath.VanillaTickDelta;

	/// <summary>
	/// Every beam resolved during the tick just completed, in the order they were fired. Cleared at
	/// the top of each <see cref="Tick"/>.
	///
	/// <para><b>Not part of the original.</b> DBSIM resolves a beam and immediately spawns its tracer
	/// segments from inside <c>Bullet_FireBurst</c>, so the shot never outlives the call. Visuals are
	/// deliberately the last piece of this milestone, and until they exist this is what lets anything
	/// outside the simulation see that a shot happened at all.</para>
	/// </summary>
	public IReadOnlyList<WeaponShot> Beams => _beams;

	/// <summary>
	/// The live beam tracers — the original's <c>DAT_004a9746</c> pool. Unlike <see cref="Beams"/>
	/// these outlive the tick that made them (by exactly one tick, see
	/// <see cref="BeamTracer.InitialLife"/>) and are what a renderer draws.
	/// </summary>
	public IReadOnlyList<BeamTracer> Tracers => _tracers;

	/// <summary>
	/// The travelling shots in flight — the same <c>DAT_004a9746</c> pool <see cref="Tracers"/> comes
	/// from. Unlike a tracer these live for as long as their <c>BULLETS.DAT</c> lifetime or until
	/// they hit something, and they move and do damage while they do.
	/// </summary>
	public IReadOnlyList<Projectile> Projectiles => _projectiles;

	/// <summary>
	/// Every travelling shot that struck something during the tick just completed, as the shot record
	/// the raycast left behind. Cleared at the top of each <see cref="Tick"/>, exactly as
	/// <see cref="Beams"/> is, and not part of the original for the same reason.
	/// </summary>
	public IReadOnlyList<WeaponShot> Impacts => _impacts;

	/// <summary>
	/// The impact effects playing right now — the same <c>DAT_004a9746</c>-style effect pool
	/// <see cref="Tracers"/> and <see cref="Projectiles"/> come from, walked by the same loop. An
	/// entry lives for exactly one pass of its shape's flipbook; see <see cref="ImpactEffect"/>.
	/// </summary>
	public IReadOnlyList<ImpactEffect> Effects => _effects;

	/// <summary>
	/// <c>FUN_00407f1c</c> — puts one impact effect at <paramref name="position"/>. Called from the
	/// two places the original calls it from along this path: from inside an object's hit test, where
	/// the effect belongs to the object struck (and is spawned whether or not the sweep goes on to
	/// find something nearer), and from the tail of <see cref="Raycast"/> itself for a shot that ends
	/// on the ground.
	///
	/// <para>Silently does nothing when the table did not load or the id is outside it. A retail
	/// <c>ImpactFX</c> array can hold an id no type row exists for, and the original bounds nothing
	/// here — reading past the table is not a behaviour worth reproducing.</para>
	/// </summary>
	/// <param name="typeId">The <c>EXPLOS.DAT</c> type, out of a <c>PROJ.DAT</c> <c>ImpactFX</c> array.</param>
	/// <param name="position">Where the shot landed, in world units.</param>
	internal void SpawnImpactEffect(short typeId, Vec3i position) {
		if (Explosions?.Type(typeId) is not { } record) {
			return;
		}

		_effects.Add(new ImpactEffect(typeId, record, Explosions.FrameCount(record.ShapeIndex), position));
	}

	/// <summary>
	/// One of the four ids an <c>ImpactFX</c> array holds, drawn the way every spawn site draws it —
	/// <c>Math_RandomNext(...) &amp; 3</c>, so all four are equally likely and the same array gives a
	/// different effect shot to shot.
	/// </summary>
	internal short PickImpactEffect(short[]? effects) =>
		effects is { Length: > 0 } ? effects[Random.NextMasked(3) % effects.Length] : (short)0;

	/// <summary>Adds an object to the simulation.</summary>
	public void Add(SimObject simObject) => _objects.Add(simObject);

	/// <summary>
	/// <c>Sim_RaycastObjectList</c> (<c>00426528</c>) — the shared ray-versus-live-object query, which
	/// is a hit test and a damage application at once: each candidate's own
	/// <see cref="SimObject.DirectFireHitTest"/> resolves the geometry and applies whatever got
	/// through in the same call.
	///
	/// <para>The sweep <b>shortens the ray as it goes</b> and does not stop at the first hit: a
	/// candidate found later, but nearer, overwrites the one before it, because every subsequent
	/// candidate is tested against the shortened <see cref="WeaponShot.Distance"/> and can only pass
	/// if it is nearer still. It ends early only for a hit inside
	/// <see cref="WeaponShot.MinimumScanDistance"/>, which nothing can beat.</para>
	///
	/// <para>The terrain goes first, and is the reason an object sweep on its own would not do:
	/// <see cref="RaycastTerrain"/> shortens the ray at the ground before a single object is tested,
	/// so a machine standing behind a ridge cannot be shot through it.</para>
	///
	/// <para>Two things the original also does here are left out, both belonging to systems that do
	/// not exist yet: the AI "something just shot at me" notification on each candidate's
	/// <c>+0x50</c> slot, and the friendly-fire and lock-on filtering that reads each object's team
	/// byte.</para>
	/// </summary>
	/// <returns>The distance the shot travelled before it hit something, or zero if it hit nothing.</returns>
	public int Raycast(WeaponShot shot) {
		bool hit = RaycastTerrain(shot);

		for (int i = 0; i < _objects.Count; i++) {
			var candidate = _objects[i];
			if (candidate.Removed || ReferenceEquals(candidate, shot.Owner)) {
				continue;
			}

			int struckAt = candidate.DirectFireHitTest(this, shot);
			if (struckAt == 0) {
				continue;
			}

			shot.Distance = struckAt;
			shot.HitObject = candidate;
			hit = true;

			if (struckAt < WeaponShot.MinimumScanDistance) {
				break;
			}
		}

		// The ground impact, which is the sweep's own job and not the ground's: the original keeps two
		// flags — "something was struck" and "an object was struck" — and spawns an effect at the ray's
		// far end when the first is set and the second is not. So a shot that ends in the dirt puts one
		// down and a shot that ends on a machine does not, even though the ground clipped the ray
		// first in both cases.
		//
		// It comes out of the ImpactFxGroup.Ground array, and unlike every object hit it is spawned
		// with no owner and with the sound suppressed (the constructor's last argument is 0 here and 1
		// at every other site).
		if (hit && shot.HitObject == null) {
			SpawnImpactEffect(
				PickImpactEffect(shot.ImpactFx(WeaponShot.ImpactFxGroup.Ground)),
				shot.Muzzle.TransformPoint(0, shot.Distance, 0));
		}

		return hit ? shot.Distance + 1 : 0;
	}

	/// <summary>
	/// <c>Sim_RaycastTerrain</c> (<c>00428048</c>) — the ray-versus-ground query the shared raycast
	/// runs before it looks at any object. It rebuilds the ray's far end from the shot's own frame,
	/// walks the heightmap with <see cref="HeightGrid.RayWalk"/>, and measures the ground hit back
	/// to the muzzle; a hit nearer than the ray's current length clips it there.
	///
	/// <para>The measured distance uses the sim's sqrt-free magnitude, as the original does, so it
	/// reads a few percent short — the same bias every other range check in the simulation
	/// carries.</para>
	///
	/// <para>The original also hands the sweep a pseudo-object standing in for the ground, so that
	/// the AI notification path has something to name. Nothing here consumes that yet, so a terrain
	/// clip leaves <see cref="WeaponShot.HitObject"/> null and records the point instead.</para>
	/// </summary>
	/// <returns>Whether the shot was clipped at the ground.</returns>
	private bool RaycastTerrain(WeaponShot shot) {
		// The ray is the muzzle transform's Y axis, so its far end is that frame's own
		// (0, distance, 0) — the same construction the shot itself was built from.
		var muzzle = new Vec3i(shot.Muzzle.X, shot.Muzzle.Y, shot.Muzzle.Z);
		var end = shot.Muzzle.TransformPoint(0, shot.Distance, 0);

		if (!Terrain.RayWalk(muzzle, end, out var ground)) {
			return false;
		}

		int distance = ground.ApproxDistanceTo(muzzle);
		if (distance >= shot.Distance) {
			return false;
		}

		shot.Distance = distance;
		shot.GroundHit = ground;
		return true;
	}

	/// <summary>
	/// Resolves one beam and records it. The <c>PROJ.DAT</c> lookup <c>Bullet_FireBurst</c> repeats
	/// on the way in is skipped — the mount already resolved the same record at loadout time and
	/// <see cref="WeaponShot"/> carries what it holds.
	/// </summary>
	internal void FireBeam(WeaponShot shot) {
		int travelled = Raycast(shot);

		// Bullet_FireBurst's own fallback: a sweep that struck nothing returns zero, and the tracer is
		// drawn out to the weapon's full range instead — a miss is still a visible shot.
		if (travelled == 0) {
			travelled = shot.Range;
		}

		// The far end is rebuilt from the shot's frame rather than measured, the same construction the
		// terrain clip uses: the ray is the muzzle transform's Y axis.
		_tracers.Add(new BeamTracer(
			new Vec3i(shot.Muzzle.X, shot.Muzzle.Y, shot.Muzzle.Z),
			shot.Muzzle.TransformPoint(0, travelled, 0),
			shot.MissileId));

		_beams.Add(shot);
	}

	/// <summary>
	/// <c>FUN_0040b43c</c> — spawns one travelling shot. The powered form <c>FUN_0040b5a0</c> is the
	/// same call with two fields written afterwards, so it is this one method: an energy gun passes
	/// the capacitor charge it spent, an ammunition mount passes zero.
	///
	/// <para>The homing target the powered form also attaches, for the plasma subtype alone, is the
	/// firing machine's <b>selected target</b> (<c>mech+0x1a4</c>). <b>There is no target selection in
	/// the engine</b>, so nothing is attached and a plasma round flies straight — which is what the
	/// original does with nothing selected too. See <see cref="Projectile.Target"/>.</para>
	/// </summary>
	/// <param name="projectile">The firing <c>PROJ.DAT</c> record.</param>
	/// <param name="muzzle">The world muzzle point the fire prologue worked out.</param>
	/// <param name="aim">The shot transform's euler triple, before scatter.</param>
	/// <param name="ownerSpeed">The firing machine's travel speed, which the shot inherits.</param>
	/// <param name="power">The capacitor charge spent, or zero for a shot out of a magazine.</param>
	/// <param name="owner">The machine that fired.</param>
	/// <returns>The shot, or null when <see cref="Bullets"/> has no record for its subtype.</returns>
	internal Projectile? FireBullet(ProjectileData.Projectile projectile, Vec3i muzzle,
			(short X, short Y, short Z) aim, short ownerSpeed, short power, SimObject? owner) {
		if (Bullets?.Record(projectile.MissileId) is not { } record) {
			return null;
		}

		var shot = new Projectile(projectile, record, muzzle, aim, ownerSpeed, power, owner, Random);
		_projectiles.Add(shot);
		return shot;
	}

	/// <summary>Records a travelling shot's impact for <see cref="Impacts"/>. Not part of the original.</summary>
	internal void RecordProjectileHit(WeaponShot shot) => _impacts.Add(shot);

	/// <summary>
	/// Advances the simulation by one tick: publishes the timestep, then updates every live object.
	/// Objects flagged removed are skipped, matching how the original's tick walks its lists.
	/// </summary>
	public void Tick() {
		SimMath.TickDelta = TickDelta;
		_beams.Clear();
		_impacts.Clear();

		// The effect pool goes first, as it does in Sim_MainTick, where it is walked ahead of the
		// machine list. That ordering is what gives a tracer a full tick on screen: one spawned while
		// a machine updates is not counted down until the tick after. A travelling shot gets the same
		// deal — the round that leaves the barrel this tick does not move or hit anything until the
		// next one.
		// Impact effects share that deal, and want it more: one is spawned from inside a hit test, so
		// it is created part-way through this same tick and must not be counted down until the next.
		for (int i = _effects.Count - 1; i >= 0; i--) {
			if (_effects[i].Tick()) {
				_effects.RemoveAt(i);
			}
		}

		for (int i = _tracers.Count - 1; i >= 0; i--) {
			if (_tracers[i].Tick()) {
				_tracers.RemoveAt(i);
			}
		}

		for (int i = _projectiles.Count - 1; i >= 0; i--) {
			if (_projectiles[i].Tick(this)) {
				_projectiles.RemoveAt(i);
			}
		}


		for (int i = 0; i < _objects.Count; i++) {
			var simObject = _objects[i];
			if (!simObject.Removed) {
				simObject.Tick(this);
			}
		}

		TickCount++;
	}

	/// <summary>
	/// Terrain height under a world position, via the ported <c>Terrain_HeightQuery</c>. Provided
	/// here because it is the form simulation code wants — ground-impact checks and the flyer
	/// terrain-avoidance autopilot both ask "how high is the ground under this object".
	/// </summary>
	public int GroundHeightAt(Vec3i position) => Terrain.HeightAtWorld(position.X, position.Y);
}
