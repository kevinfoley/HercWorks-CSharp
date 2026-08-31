using System.Numerics;
using HercWorks.Core.Data.File.Dyn;
using Herculan.Engine.Content;

namespace Herculan.Engine.Render;

/// <summary>
/// What a flat surface needs to become a colour: the theater's <c>.RMP</c> ramp and the
/// <c>.DPL</c> palette its output is an index into. Both come from the same theater, and neither
/// means anything without the other.
///
/// <para><b>Three mechanisms, one per untextured poly type:</b></para>
/// <list type="bullet">
/// <item><b>Plain <see cref="HercWorks.Core.Data.File.Dts.Poly.TSSolidPoly"/></b> —
/// <see cref="Ramp"/>'s row for the fixed unlit shade, indexed by the surface value as a
/// <i>palette index</i>. No light term at all. <see cref="ShadeRamp.UnlitShade"/> and
/// <see cref="ShadeRamp.Resolve"/>; traced in <see cref="DtsMeshBuilder"/>'s
/// <c>ResolveSolidColors</c>.</item>
/// <item><b><see cref="HercWorks.Core.Data.File.Dts.Poly.TSShadedPoly"/></b> — the surface value is
/// a <i>ramp number</i> into <see cref="DynamixPalette.ShadeRamps"/>; the face's light level picks
/// an entry, then that entry goes through the <c>.RMP</c> at the fixed unlit shade.
/// <see cref="ShadedColor"/>.</item>
/// <item><b><see cref="HercWorks.Core.Data.File.Dts.Poly.TSGouraudPoly"/></b> — the same ramp
/// number, the same per-vertex light, and <b>no <c>.RMP</c> step</b>. <see cref="GouraudColor"/>.</item>
/// </list>
/// </summary>
public sealed record SurfaceShading(ShadeRamp Ramp, DynamixPalette? Palette) {
	/// <summary>Whether the theater's palette carries a shade-ramp table at all.</summary>
	public bool HasShadeRamps => Palette is { ShadeRamps.Count: > 0 };

	/// <summary>
	/// The colour a <c>TSShadedPoly</c> surface draws at one light level — the whole of
	/// <c>TSShadedPoly_Render</c> (<c>0047542c</c>)'s colour resolution, which is two lookups and
	/// not one:
	/// <code>
	/// paletteIndex = Palette_ShadeRampLookup(surface.FrontColor, shade)   // 00430e34
	/// byte         = Raster_ShadeRampRow(0x80)[paletteIndex]              // 00468054
	/// </code>
	///
	/// <para>The first is <see cref="RampedPaletteIndex"/> — the surface value names one of the
	/// palette's material ramps and <paramref name="shade"/> picks a step along it. The second is
	/// the theater <c>.RMP</c> at the <i>fixed</i> <see cref="ShadeRamp.UnlitShade"/>, the same
	/// literal <c>0x80</c> the solid renderer passes: all of a shaded face's lighting is in the
	/// first lookup, and the ramp row it lands on afterwards never varies.</para>
	///
	/// <para>Returns null when the palette has no ramp table, when the surface names a ramp outside
	/// it, or when the resolved byte has no palette entry — callers fall back to their own colour
	/// rather than to a made-up one.</para>
	/// </summary>
	/// <param name="rampNumber">The surface's <c>FrontColor</c>, masked to a byte as the original does.</param>
	/// <param name="shade">The face's light level, 0-255 — see <see cref="MissionSun.ShadeForFace"/>.</param>
	public Vector3? ShadedColor(int rampNumber, int shade) =>
		RampedPaletteIndex(rampNumber, shade) is { } index
			? Ramp.Resolve(index, ShadeRamp.UnlitShade, Palette)
			: null;

	/// <summary>
	/// The colour a <c>TSGouraudPoly</c> surface draws at one light level — <b>the material ramp's
	/// entry straight through the palette, with no <c>.RMP</c> step at all</b>.
	///
	/// <para><c>TSGouraudPoly_Render</c> (<c>004755c8</c>, the <c>+0x1c</c> slot of the vtable whose
	/// tag function returns <c>0x140009</c>) calls <c>Light_ComputeShadeForFace</c> once <i>per
	/// vertex</i> — walking the poly's <c>NormalList</c> and <c>VertexList</c> in step — stashes the
	/// bytes in a per-vertex array, points <c>DAT_006c60e4</c> at it, sets fill mode 1 and lets the
	/// span routine interpolate. <b>It never calls <c>Raster_ShadeRampRow</c></b>, where
	/// <c>TSShadedPoly_Render</c> calls it with the literal <c>0x80</c> before every fill: the ramp
	/// lookup moves into the span so it can vary per pixel, and the fixed <c>.RMP</c> row is not part
	/// of this path.</para>
	///
	/// <para>Distinguishing evidence: the <c>.RMP</c> row shifts every entry down one step and
	/// collapses two pairs, so for <c>WORLD2</c>'s ramp 8 the shaded chain can never emit palette 178
	/// (<c>#68687c</c>). A retail capture of the ramp-8 cylindrical structure shows
	/// <c>#9090a4 #848498 #7c7c90 #707084 #68687c #606074 #545468 #4c4c60 #444454 #3c3c4c
	/// #343444</c> — a consecutive run of the raw ramp entries, <c>#68687c</c> included.</para>
	/// </summary>
	/// <inheritdoc cref="ShadedColor" path="/param"/>
	public Vector3? GouraudColor(int rampNumber, int shade) {
		if (RampedPaletteIndex(rampNumber, shade) is not { } index
				|| Palette == null || !Palette.Colors.TryGetValue(index & 0xff, out var entry)) {
			return null;
		}

		var color = entry.GetColor();
		return new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
	}

	/// <summary>
	/// <c>Palette_ShadeRampLookup</c> (<c>00430e34</c>) on its own: which palette index the named
	/// material ramp reaches at <paramref name="shade"/>.
	///
	/// <code>
	/// idx = value &amp; 0xff;  if (idx &gt;= rampCount) idx = 0;
    /// pos = Q8Multiply(shade, ramp.length);  if (pos == ramp.length) pos = ramp.length - 1;
	/// return ramp.indices[pos];
	/// </code>
	///
	/// <para>The out-of-range fallback to ramp 0 is the original's, kept because a surface naming a
	/// ramp the palette does not have should look like whatever the game showed, not like an error.
	/// The step-back guard is what stops a shade of 255 running off the end.</para>
	/// </summary>
	public int? RampedPaletteIndex(int rampNumber, int shade) {
		if (Palette is not { ShadeRamps.Count: > 0 } palette) {
			return null;
		}

		int index = rampNumber & 0xff;
		if (index >= palette.ShadeRamps.Count) {
			index = 0;
		}

		var ramp = palette.ShadeRamps[index];
		if (ramp is not { Length: > 0 }) {
			return null;
		}

		int position = Math.Clamp(shade, 0, 255) * ramp.Length / 256;
		if (position >= ramp.Length) {
			position = ramp.Length - 1;
		}

		return ramp[position];
	}
}
