namespace Herculan.Engine.Render;

/// <summary>
/// The theater's shaded-surface colours, every ramp against every light level, as one small RGBA
/// image the fragment shader indexes instead of doing two table lookups per pixel.
///
/// <para>Row <c>r</c>, column <c>s</c> is <see cref="SurfaceShading.ShadedColor"/> for ramp
/// <c>r</c> at shade <c>s</c> — so this is not an approximation of the original's colouring, it
/// <i>is</i> the original's colouring, evaluated once for all 65536 combinations at load. Sampling
/// it nearest-neighbour reproduces the exact palette byte a <c>TSShadedPoly</c> would have been
/// filled with, quantisation included.</para>
///
/// <para>Why a table rather than a bake: the shade a face is drawn at comes from its <b>world</b>
/// normal (<see cref="MissionSun.ShadeForFace"/>), and one built mesh is shared by every object of a
/// type standing at its own heading. See <see cref="Gl.MeshVertex.ShadeRamp"/>.</para>
/// </summary>
public sealed class SurfaceRampTable {
	/// <summary>Light levels across — one column per shade byte.</summary>
	public const int Width = 256;

	/// <summary>
	/// Ramps down — one row per slot of the palette's ramp table, <b>twice</b>: rows
	/// <c>0..255</c> are the <see cref="SurfaceShading.ShadedColor"/> chain that
	/// <c>TSShadedPoly</c> draws through, rows <c>256..511</c> the
	/// <see cref="SurfaceShading.GouraudColor"/> one that <c>TSGouraudPoly</c> does. The two are
	/// different mechanisms, not two qualities of one — see <see cref="GouraudRowOffset"/>.
	/// </summary>
	public const int Height = 512;

	/// <summary>
	/// What a vertex adds to its ramp number to select the Gouraud half of the table — see
	/// <see cref="Gl.MeshVertex.ShadeRamp"/>, which carries the biased value.
	///
	/// <para>Folding both chains into one texture keeps the fragment shader at a single sample and
	/// the vertex format at a single float, which is worth more than the 256 KB the second half
	/// costs.</para>
	/// </summary>
	public const int GouraudRowOffset = 256;

	private SurfaceRampTable(byte[] pixels) {
		Pixels = pixels;
	}

	/// <summary>RGBA8, row-major, <see cref="Width"/> by <see cref="Height"/>.</summary>
	public byte[] Pixels { get; }

	/// <summary>
	/// Builds the table for a theater, or returns null when <paramref name="shading"/> is absent or
	/// its palette carries no ramp table — in which case shaded surfaces keep whatever colour the
	/// mesh builder gave them.
	/// </summary>
	public static SurfaceRampTable? Build(SurfaceShading? shading) {
		if (shading is not { HasShadeRamps: true }) {
			return null;
		}

		var pixels = new byte[Width * Height * 4];
		for (int row = 0; row < Height; row++) {
			bool gouraud = row >= GouraudRowOffset;
			int ramp = row - (gouraud ? GouraudRowOffset : 0);

			for (int shade = 0; shade < Width; shade++) {
				int at = (row * Width + shade) * 4;
				var color = gouraud
					? shading.GouraudColor(ramp, shade)
					: shading.ShadedColor(ramp, shade);

				// A ramp slot the palette leaves empty falls back to mid grey rather than to black:
				// no retail surface names one, and a black hole would be a louder lie than a flat
				// tone if some mission's data ever did.
				pixels[at] = color is { } c ? Quantise(c.X) : (byte)128;
				pixels[at + 1] = color is { } c1 ? Quantise(c1.Y) : (byte)128;
				pixels[at + 2] = color is { } c2 ? Quantise(c2.Z) : (byte)128;
				pixels[at + 3] = 255;
			}
		}

		return new SurfaceRampTable(pixels);
	}

	private static byte Quantise(float channel) =>
		(byte)Math.Clamp((int)MathF.Round(channel * 255f), 0, 255);
}
