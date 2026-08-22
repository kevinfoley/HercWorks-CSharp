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

	public SimWorld(HeightGrid terrain) {
		Terrain = terrain;
	}

	/// <summary>The loaded zone's terrain. One zone is active at a time, as in the original.</summary>
	public HeightGrid Terrain { get; }

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

	/// <summary>Adds an object to the simulation.</summary>
	public void Add(SimObject simObject) => _objects.Add(simObject);

	/// <summary>
	/// Advances the simulation by one tick: publishes the timestep, then updates every live object.
	/// Objects flagged removed are skipped, matching how the original's tick walks its lists.
	/// </summary>
	public void Tick() {
		SimMath.TickDelta = TickDelta;

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
