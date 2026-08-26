using Herculan.Engine.Content;

namespace Herculan.Engine.Render;

/// <summary>
/// What one row of the theater's <c>world&lt;n&gt;.rmp</c> does to a colour's brightness, measured
/// against that theater's own palette.
///
/// <para><b>Why this exists.</b> The original is an 8-bit indexed renderer: a shaded span looks each
/// texel's palette index up in the ramp row its shade byte selects and writes the byte it finds. The
/// engine draws expanded RGB, so it has no index to look up by the time the fragment shader runs, and
/// the closest honest substitute is a per-row multiplier. This measures that multiplier from the real
/// table rather than assuming one: for each row, the summed luminance the row produces over the
/// summed luminance it consumes, across the whole palette.</para>
///
/// <para>Summing luminances rather than averaging per-index ratios is deliberate. A handful of
/// near-black palette entries map onto much brighter bytes and produce ratios above 5, which drag a
/// plain mean around; weighting by the luminance actually present makes the curve reflect what the
/// ramp does to the colours that carry the picture. The two metrics agree to about 5% everywhere on
/// <c>WORLD0</c> regardless (row 30: 1.209 unweighted, 1.149 weighted).</para>
///
/// <para>Measured on <c>WORLD0</c>, the curve runs 0.36x at row 0 to 1.16x at row 31, crossing unity
/// near row 23 — see <see cref="ShadeRamp"/>. The consequence for terrain is that the saturated shade
/// <see cref="MissionSun"/> hands flat ground lands at about <b>1.15x</b> the texture's own colour,
/// where the engine's previous Lambert term put it at 0.70x.</para>
/// </summary>
public sealed class ShadeBrightness {
	/// <summary>
	/// Palette entries dimmer than this contribute nothing measurable and are skipped, so a ramp row
	/// is not judged by what it does to the palette's black.
	/// </summary>
	private const float MinimumLuminance = 4f;

	private readonly float[] _rows;
	private readonly ShadeRamp _ramp;

	private ShadeBrightness(ShadeRamp ramp, float[] rows) {
		_ramp = ramp;
		_rows = rows;
	}

	/// <summary>
	/// Measures the curve for a theater. Returns null without a ramp or a palette — there is nothing
	/// to measure against, and callers fall back to their own shading rather than to a made-up curve.
	/// </summary>
	public static ShadeBrightness? Build(SurfaceShading? shading) {
		if (shading?.Palette is not { } palette) {
			return null;
		}

		var ramp = shading.Ramp;
		var luminance = new float[ShadeRamp.RowLength];
		for (int index = 0; index < ShadeRamp.RowLength; index++) {
			if (palette.Colors.TryGetValue(index, out var entry)) {
				var color = entry.GetColor();
				luminance[index] = 0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B;
			}
		}

		var rows = new float[ramp.ShadeLevels];
		for (int row = 0; row < ramp.ShadeLevels; row++) {
			float produced = 0f;
			float consumed = 0f;
			for (int index = 0; index < ShadeRamp.RowLength; index++) {
				if (luminance[index] <= MinimumLuminance) {
					continue;
				}

				produced += luminance[ramp.AtRow(index, row)];
				consumed += luminance[index];
			}

			rows[row] = consumed > 0f ? produced / consumed : 1f;
		}

		return new ShadeBrightness(ramp, rows);
	}

	/// <summary>
	/// The multiplier for a shade byte, via the same row selection the original's
	/// <c>Raster_ShadeRampRow</c> does.
	/// </summary>
	public float For(int shade) => _rows[_ramp.RowFor(shade)];
}
