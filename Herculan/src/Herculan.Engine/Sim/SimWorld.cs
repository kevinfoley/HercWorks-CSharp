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
	/// Simulation rate. DBSIM drives its timestep off a hardware timer rather than a fixed rate, so
	/// this is the engine's own choice; a fixed rate is what makes the fixed-point integration
	/// reproducible, which is the whole reason for porting that math rather than using floats.
	/// </summary>
	public const int TicksPerSecond = 30;

	/// <summary>
	/// The value written into <see cref="SimMath.TickDelta"/> for each tick. 256 means "one whole
	/// sim time unit per tick" in the Q8 scale that <see cref="SimMath.Q8Multiply"/> works in, so a
	/// rate field integrates to exactly its own value each tick.
	///
	/// <para><b>Open RE item:</b> DBSIM's actual timestep value and its real-world unit have not
	/// been recovered — <c>DAT_004d3be8</c> is only ever seen being read, and the timer source that
	/// writes it wasn't traced. The pairing chosen here is at least self-consistent against a known
	/// constant: the rocket turn-rate cap is 0x500 BAM per tick, which at one unit per tick and 30
	/// ticks/second works out to a full revolution in about 1.7 seconds — a plausible missile turn
	/// rate. Treat any absolute timing as provisional until the timer is traced; relative behavior
	/// between systems is unaffected, since they all scale through this same value.</para>
	/// </summary>
	public const short TickDelta = 256;

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
