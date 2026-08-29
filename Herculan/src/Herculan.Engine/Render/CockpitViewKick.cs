namespace Herculan.Engine.Render;

/// <summary>
/// The cockpit's step kick — the view bob a pilot actually sees when his machine walks.
///
/// <para><b>It is not a camera move.</b> DBSIM shifts the <i>projection centre</i> instead: a
/// footfall runs a canned curve into the render target's own centre offset (<c>target+0x224</c>, via
/// <c>FUN_0042d82c</c>), which <c>Raster_InstallViewProjection</c> subtracts when it installs the
/// projection. Every projected point moves with it, so the whole image — the horizon included —
/// slides behind a cockpit that stays put. That is why the effect reads as the view being pitched
/// even though nothing in the machine's pose or the eye's frame rotates at all: measured across the
/// fleet, the camera node's world orientation does not move by a single BAM over a stride.</para>
///
/// <para>The curve is ten shorts at <c>0049b046</c> — <c>1 2 3 4 5 5 4 3 2 1</c> — played over
/// <c>0x3c</c> coarse ticks, which <c>Time_GetCoarseTicks</c> makes 16 ms each, so 960 ms in ten
/// steps of 96. <c>FUN_00434194</c> indexes it per frame as
/// <c>((now - start) * 10) / 0x3c</c> and installs the value; <c>FUN_00434144</c> restarts it on
/// each footfall, so a machine stepping faster than the curve runs simply re-triggers it. The
/// original halves the figure in the 320x240 mode and takes it as-is in the 640x480 ones
/// (<c>value &lt;&lt; (YCoordShift - 1)</c>), so <b>five device pixels</b> is the peak.</para>
/// </summary>
public sealed class CockpitViewKick {
	/// <summary>The ten samples at <c>0049b046</c>, in device pixels for the 640x480 modes.</summary>
	private static readonly int[] Curve = { 1, 2, 3, 4, 5, 5, 4, 3, 2, 1 };

	/// <summary>
	/// How long one run of the curve lasts, in seconds — the original's <c>0x3c</c> coarse ticks at
	/// <see cref="CoarseTickSeconds"/> each.
	/// </summary>
	public const double DurationSeconds = 0x3c * CoarseTickSeconds;

	/// <summary><c>Time_GetCoarseTicks</c>' unit: <c>GetTickCount() >> 4</c>, so 16 ms.</summary>
	private const double CoarseTickSeconds = 0.016;

	private double _elapsed = DurationSeconds;
	private int _lastFootfalls;

	/// <summary>
	/// This kick's current offset, in the art's device pixels. Positive slides the image up the
	/// screen, which is what a machine dropping onto a planted foot does to what the pilot sees.
	/// </summary>
	public int OffsetPixels {
		get {
			if (_elapsed >= DurationSeconds) {
				return 0;
			}

			// The original's integer index, and its own truncation with it.
			int index = (int)(_elapsed / DurationSeconds * Curve.Length);
			return Curve[index < 0 ? 0 : index >= Curve.Length ? Curve.Length - 1 : index];
		}
	}

	/// <summary>
	/// Advances the curve and restarts it on each new footfall. <paramref name="footfalls"/> is
	/// <see cref="Sim.MechObject.Footfalls"/>; passing a machine's running count rather than a flag
	/// is what lets this be driven from the render loop without missing a step the simulation took
	/// between two frames.
	/// </summary>
	public void Update(double deltaSeconds, int footfalls) {
		if (footfalls != _lastFootfalls) {
			_lastFootfalls = footfalls;
			_elapsed = 0;
			return;
		}

		if (_elapsed < DurationSeconds) {
			_elapsed += deltaSeconds;
		}
	}

	/// <summary>Drops any kick in progress — for leaving the cockpit view.</summary>
	public void Reset() {
		_elapsed = DurationSeconds;
	}
}
