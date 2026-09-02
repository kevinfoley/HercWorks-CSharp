using Herculan.Engine.Content;

namespace Herculan.Engine.Render;

/// <summary>
/// The theater's <c>.RMP</c> expanded through its palette: every palette index at every shade row,
/// as one small RGBA image the fragment shader indexes.
///
/// <para>This is the whole of how the original colours a <b>lit textured</b> surface. It is an 8-bit
/// indexed rasterizer, so a span writes</para>
/// <code>
/// framebuffer[x] = Raster_ShadeRampRow(shade)[ texelPaletteIndex ]
/// </code>
/// <para>— the light level picks a <i>row</i> of the ramp and the texel picks the column within it,
/// per pixel. Row <c>r</c>, column <c>i</c> of this table is that lookup's result expanded to RGB,
/// so a shader that samples <see cref="TextureAtlas.IndexPixels"/> for <c>i</c> and this for the row
/// produces the exact byte the original would have written.</para>
///
/// <para><b>It replaces a brightness multiplier</b>, which is the shape of thing an RGB pipeline
/// reaches for and which this engine used until now: take the texel's expanded colour and scale it
/// by how bright the ramp row is on average. That is a different operation. The ramp is not a
/// uniform dimmer — it is a per-colour remap that preserves hue and compresses unevenly, and near
/// its ends it collapses distinct colours together, which no multiplier does.</para>
///
/// <para>Only <see cref="ShadeRamp.ShadeLevels"/> rows of the ramp are represented — depth slice
/// zero. Distance is applied as continuous fog in <see cref="SceneRenderer"/> rather than as the
/// original's twelve quantised slices, which is a deliberate difference recorded in
/// <see cref="ShadeRamp"/>.</para>
/// </summary>
public sealed class PaletteRampTable {
	/// <summary>Palette indices across — one column each.</summary>
	public const int Width = 256;

	private PaletteRampTable(byte[] pixels, int shadeRows) {
		Pixels = pixels;
		ShadeRows = shadeRows;
	}

	/// <summary>RGBA8, row-major, <see cref="Width"/> by <see cref="Height"/>.</summary>
	public byte[] Pixels { get; }

	/// <summary>
	/// The ramp's own rows — <see cref="ShadeRamp.ShadeLevels"/>, 32 in every retail theater. The
	/// shade byte selects among these; <see cref="FullbrightRow"/> sits past them.
	/// </summary>
	public int ShadeRows { get; }

	/// <summary>
	/// The row a fullbright textured surface reads instead of a shade row: the palette straight
	/// through, with no <c>.RMP</c> step at all.
	///
	/// <para><c>TSTexture4Poly_Render</c> (<c>00474e9c</c>) is the only reader of the ramp's row
	/// count (<c>DAT_004a5b1c</c>), and when that count is zero it fills through
	/// <c>FUN_00468078</c>'s mode 0 — a plain texture copy, with neither a light term nor a ramp
	/// lookup. <c>Bullet_Draw</c> (<c>0040a120</c>) zeroes it for the duration of a projectile's
	/// shape render and restores it after, which is what makes a round's textured polys fullbright.
	/// The one retail shape it reaches is <c>BULLETS.DTS</c> root 8, the plasma cannon's round; every
	/// other projectile shape is untextured. See <see cref="SceneItem.Fullbright"/> and
	/// docs/formats/dts-texture-binding.md's "<c>TSTexture4Poly</c> — frame index, ramp row by light,
	/// fullbright on demand".</para>
	/// </summary>
	public int FullbrightRow => ShadeRows;

	/// <summary>Rows in the uploaded image: the ramp's own, plus <see cref="FullbrightRow"/>.</summary>
	public int Height => ShadeRows + 1;

	/// <summary>
	/// Builds the table for a theater, or returns null without a ramp or a palette — in which case a
	/// textured surface falls back to its expanded colour and the renderer's own light term.
	/// </summary>
	public static PaletteRampTable? Build(SurfaceShading? shading) {
		if (shading?.Palette is not { } palette) {
			return null;
		}

		var ramp = shading.Ramp;
		int shadeRows = ramp.ShadeLevels;
		var pixels = new byte[Width * (shadeRows + 1) * 4];

		for (int row = 0; row <= shadeRows; row++) {
			for (int index = 0; index < Width; index++) {
				int at = (row * Width + index) * 4;

				// The last row skips the ramp entirely — that is what the original's plain copy
				// writes, and it is not any of the ramp's own rows: row 0 lands at 0.36x the source
				// colour and row 31 at 1.16x, so none of them is the identity.
				byte resolved = row == shadeRows ? (byte)index : ramp.AtRow(index, row);

				if (palette.Colors.TryGetValue(resolved, out var entry)) {
					var color = entry.GetColor();
					pixels[at] = color.R;
					pixels[at + 1] = color.G;
					pixels[at + 2] = color.B;
				}

				pixels[at + 3] = 255;
			}
		}

		return new PaletteRampTable(pixels, shadeRows);
	}
}
