namespace Herculan.Engine.Numerics;

/// <summary>
/// Direct port of DBSIM.EXE's shared fixed-point math toolkit — the primitives every other
/// simulation subsystem builds on. Each method below corresponds 1:1 to a specific
/// reverse-engineered function; see docs/simulation/dbsim-physics-notes.md ("Fixed-point math
/// toolkit") for the full writeup, and docs/engine/planning.md's "Math" decision for why the
/// engine ports these rather than using floating-point System.Numerics throughout.
///
/// These are deliberately literal translations, quantization and clamping included — a naive
/// float reimplementation drifts from the original's behavior even when it looks equivalent on
/// paper. Rendering is free to convert to float at its own boundary (see Render/), but nothing
/// that feeds back into simulation state should.
///
/// The namespace is Herculan.Engine.Numerics rather than ...Math on purpose: a namespace whose
/// last segment is "Math" shadows System.Math for every file that imports it.
///
/// Ghidra symbol names (project ES2Recon, DBSIM.EXE) are given per method so a future session can
/// re-check a translation against the disassembly without re-deriving which function it came from.
/// </summary>
public static class SimMath {
	/// <summary>
	/// The global simulation timestep — DBSIM's <c>SimTickDelta</c> (<c>DAT_004d3be8</c>), read by
	/// <see cref="IntegrateRateOverTick"/> and <see cref="CountdownTimerTick"/>. In DBSIM this is
	/// refreshed once per frame from a hardware timer by the per-frame sim tick
	/// (<c>FUN_0045f464</c>); here the sim loop owns it the same way (see <c>Sim/SimWorld</c>).
	///
	/// Everything scaled by it is a "per this tick" quantity, not "per second" — which is what
	/// makes DBSIM a discrete fixed/semi-fixed timestep sim rather than a continuous-time
	/// integrator.
	/// </summary>
	public static short TickDelta { get; set; }

	/// <summary>
	/// <c>Math_Q8Multiply</c> (<c>0047df94</c>) — Q8 fixed-point multiply: <c>(int64)a * b >> 8</c>.
	/// Confirmed against raw disassembly (<c>SHRD EAX,EDX,0x8</c>), not just decompiler output.
	/// The decompiler renders that SHRD as a low-word/high-word recombination; taking the low 32
	/// bits of the 64-bit shifted product is the same value.
	/// </summary>
	public static int Q8Multiply(int a, int b) => (int)(((long)a * b) >> 8);

	/// <summary>
	/// Q10 sibling of <see cref="Q8Multiply"/> — same <c>IMUL</c>+<c>SHRD</c> shape, shift
	/// <c>0xa</c>, sitting immediately adjacent in the binary. Present for completeness of the
	/// toolkit; no caller has been traced to it yet, so which unit domain it serves is still open
	/// (see the physics notes' "Fixed-point math toolkit" section).
	/// </summary>
	public static int Q10Multiply(int a, int b) => (int)(((long)a * b) >> 10);

	/// <summary>
	/// Q14 sibling of <see cref="Q8Multiply"/> — shift <c>0xe</c> with a 16-bit signed operand.
	/// Its range fits a normalized -1.0..1.0 value such as a sine/cosine table output, though that
	/// is not yet confirmed by a traced caller. See <see cref="BinaryAngle"/> for the engine's
	/// trig, which uses this scale.
	/// </summary>
	public static int Q14Multiply(int a, short b) => (int)(((long)a * b) >> 14);

	/// <summary>
	/// <c>Math_IntegrateRateOverTick</c> (<c>00467820</c>) — "apply a per-unit-time rate as this
	/// tick's delta": <c>Q8Multiply(TickDelta, rate)</c>, clamped to signed 16-bit range. Called on
	/// velocity/acceleration-like fields to get a position delta, and on trig-adjacent values in
	/// rocket homing.
	/// </summary>
	public static int IntegrateRateOverTick(short rate) {
		int value = Q8Multiply(TickDelta, rate);
		if (value < -0x7fff) {
			return -0x7fff;
		}
		if (value > 0x7fff) {
			return 0x7fff;
		}
		return value;
	}

	/// <summary>
	/// <c>Math_CountdownTimerTick</c> (<c>00467944</c>) — decrements a timer by one tick, clamped
	/// at 0, returning the new value. DBSIM takes a pointer into a struct (the timer lives at a
	/// +1 byte offset inside its owner); the engine passes the field by reference instead, which
	/// is the same operation without the unaligned-pointer arithmetic.
	/// </summary>
	public static short CountdownTimerTick(ref short timer) {
		timer = (short)(timer - TickDelta);
		if (timer < 0) {
			timer = 0;
		}
		return timer;
	}

	/// <summary>
	/// <c>Math_RateLimitedMoveToward</c> (<c>004679d8</c>) — generic per-tick slew-rate limiter.
	/// Moves <paramref name="current"/> toward <paramref name="target"/> by at most
	/// <paramref name="step"/> (never overshooting), and returns the remaining error — 0 once it
	/// has arrived. Used by rocket guidance's turn-rate cap and by the shield recharge tick.
	/// </summary>
	public static short RateLimitedMoveToward(ref short current, short target, short step) {
		if (target < current) {
			current = (short)(current - step);
			if (target < current) {
				return (short)(current - target);
			}
			current = target;
		} else if (current < target) {
			current = (short)(current + step);
			if (current < target) {
				return (short)(target - current);
			}
			current = target;
		}
		return 0;
	}

	/// <summary>
	/// <c>Math_FastMagnitude3D</c> (<c>0047dd66</c>) — sqrt-free 3D magnitude approximation.
	/// Sorts <c>|dx|,|dy|,|dz|</c> into largest/mid/smallest and returns
	/// <c>L + M*0.34375 + S*0.25</c> (an alpha-max-plus-beta-min-style approximation, ~3.4% low).
	/// Verified against raw disassembly: three CMP/XCHG sort pairs and SAR+ADD coefficient chains.
	///
	/// This is a general math-library utility in the original, not something purpose-built for one
	/// subsystem — it backs both collision bounding-sphere radii and rocket proximity checks — so
	/// reproducing hit detection faithfully means using this rather than substituting a real sqrt.
	/// The branch structure below is transcribed from the decompilation as-is rather than rewritten
	/// as a clean three-way sort, to keep it checkable against the disassembly.
	/// </summary>
	public static int FastMagnitude3D(int dx, int dy, int dz) {
		int ax = Abs(dx);
		int ay = Abs(dy);
		int az = Abs(dz);

		// maxXy/mid start as the sorted pair of |dx|,|dy|; |dz| is then folded in, after which
		// large/mid/small hold the full L >= M >= S ordering.
		int maxXy = ax;
		int mid = ay;
		if (ax < ay) {
			maxXy = ay;
			mid = ax;
		}

		int small = az;
		int large = maxXy;
		if (mid < az) {
			small = mid;
			mid = az;
			if (maxXy < az) {
				large = az;
				mid = maxXy;
			}
		}

		return (small >> 2) + large + (mid >> 2) + (mid >> 4) + (mid >> 5);
	}

	/// <summary>
	/// The branchless absolute value the original compiles to (<c>(x ^ (x >> 31)) - (x >> 31)</c>).
	/// Used instead of <see cref="System.Math.Abs(int)"/> because that throws on
	/// <see cref="int.MinValue"/> where the original silently wraps — a faithful port shouldn't
	/// introduce an exception the game never had.
	/// </summary>
	private static int Abs(int value) {
		int sign = value >> 31;
		return (value ^ sign) - sign;
	}
}
