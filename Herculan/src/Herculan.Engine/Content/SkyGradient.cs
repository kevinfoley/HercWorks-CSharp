using System.Numerics;
using HercWorks.Core.Data.File.Dyn;

namespace Herculan.Engine.Content;

/// <summary>
/// The theater's sky: sixteen horizontal bands taken straight from palette entries 208-223, the
/// zenith colour first and the horizon colour last.
///
/// <para><b>Measured off retail captures, not RE'd.</b> The draw routine registers itself into a
/// frame-callback table that lives in uninitialised memory, so static analysis cannot reach it; the
/// palette run, its end at 223, and the band geometry all come from two lossless screenshots, and
/// the fog colour <see cref="ShadeRamp.FogColor"/> resolves to lands on the same last entries. The
/// captures, the per-entry colour runs and that corroboration are in
/// docs/formats/distance-fog-and-sky.md's "The sky — palette entries 208-223" and "Where the two
/// meet".</para>
///
/// <para>Flat 208 covers everything above the gradient. Bands are ~6 pixels tall in a 480-row view,
/// anchored on the horizon — see <see cref="BandHeightFor"/>.</para>
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
