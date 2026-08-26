using System.Numerics;
using HercWorks.Core.Data.File.Dyn;

namespace Herculan.Engine.Content;

/// <summary>
/// The theater's sky: sixteen horizontal bands taken straight from palette entries 208-223, the
/// zenith colour first and the horizon colour last.
///
/// <para><b>Measured, not guessed.</b> <c>Reference/Apocalypse_Cockpit.png</c> is a lossless capture
/// of the zone <c>DATA\script.dat</c> currently points at (zone 888, theater 1, so
/// <c>WORLD2</c>), and every band in its sky is an exact match for a consecutive
/// <c>WORLD2.DPL</c> entry — <c>#D4D0D4</c>, <c>#D4D0D8</c>, <c>#D8D0D8</c> … running 208, 209, 210
/// downward toward the horizon, with flat 208 above. <c>Reference/Simulator5_Preferences.jpg</c>
/// shows the same structure in a <c>WORLD0</c> zone, where the run goes orange <c>#985C20</c> at the
/// top to olive <c>#747060</c> at the horizon, and there — over flat ground, where no ridge cuts the
/// gradient short — it is visible all the way to entry 223.</para>
///
/// <para>That the run ends where it does is corroborated by the ramp: a theater's fog colour
/// (<see cref="ShadeRamp.FogColor"/>) lands on entry 222 or 223, the last of this same gradient.
/// The sky's bottom band and the colour distant terrain fades into are the same colour, which is why
/// retail's horizon reads as continuous rather than as a seam.</para>
///
/// <para><b>What is not RE'd.</b> The band geometry is measured off those two captures, not taken
/// from the exe: the draw routine registers itself into a frame-callback table that lives in
/// uninitialised memory (<c>FUN_00401d94</c>'s table at <c>004a80d0</c>), so it cannot be reached by
/// static analysis. The bands are ~6 pixels tall in a 480-row view, anchored on the horizon —
/// see <see cref="BandHeightFor"/>.</para>
/// </summary>
public sealed class SkyGradient {
	/// <summary>The palette entry the zenith band uses; the gradient runs upward from here.</summary>
	public const int FirstPaletteIndex = 208;

	/// <summary>How many bands the gradient has — entries 208 through 223.</summary>
	public const int BandCount = 16;

	/// <summary>
	/// Rows per band in the 480-row view both captures were taken at. <c>Apocalypse_Cockpit.png</c>
	/// changes band at y = 107, 113, 119 … exactly six rows apart.
	/// </summary>
	private const float BandRowsAt480 = 6f;

	/// <summary>The view height those six rows were measured in.</summary>
	private const float ReferenceViewHeight = 480f;

	private SkyGradient(Vector3[] bands) {
		Bands = bands;
	}

	/// <summary>
	/// The sixteen band colours, <c>[0]</c> the zenith (palette 208) through <c>[15]</c> the horizon
	/// (palette 223).
	/// </summary>
	public Vector3[] Bands { get; }

	/// <summary>The colour at the top of the gradient, and of all sky above it.</summary>
	public Vector3 Zenith => Bands[0];

	/// <summary>The colour the bottom band meets the horizon in.</summary>
	public Vector3 Horizon => Bands[BandCount - 1];

	/// <summary>
	/// How tall one band is, in pixels, for a view of <paramref name="viewportHeight"/> rows — the
	/// measured six rows at 480, scaled so the gradient keeps the same share of the view at any
	/// window size.
	/// </summary>
	public static float BandHeightFor(int viewportHeight) =>
		MathF.Max(viewportHeight / ReferenceViewHeight * BandRowsAt480, 1f);

	/// <summary>
	/// Reads the sixteen entries out of a theater palette. Returns null when the palette is missing
	/// or does not carry the whole run, in which case the renderer falls back to a flat sky.
	/// </summary>
	public static SkyGradient? FromPalette(DynamixPalette? palette) {
		if (palette == null) {
			return null;
		}

		var bands = new Vector3[BandCount];
		for (int band = 0; band < BandCount; band++) {
			if (!palette.Colors.TryGetValue(FirstPaletteIndex + band, out var entry)) {
				return null;
			}

			var color = entry.GetColor();
			bands[band] = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
		}

		return new SkyGradient(bands);
	}
}
