using System.Numerics;
using Herculan.Engine.Content;
using Herculan.Engine.Render;
using Herculan.Engine.Terrain;
using Herculan.Engine.World;

namespace Herculan.Engine.Scene;

/// <summary>
/// Everything about how a zone looks into the distance, taken from the zone and its theater rather
/// than chosen: how far the original draws, where inside that its fade begins, what colour it ends
/// on, and the banded sky above it.
///
/// <para>Both numbers come out of <c>Terrain_DrawCellQuad</c>, which per cell installs the
/// visibility range as <c>grid[+0x10c] &lt;&lt; grid[+0x108]</c> — the view radius in cells scaled
/// to world units (<see cref="HeightGrid.VisibilityRange"/>) — and then fades against it through
/// <c>Raster_SetDepthFadeFromDistance</c>, which is flat until half the range and saturated at it
/// (<see cref="ShadeRamp.DepthSliceFor"/>). The colour is the theater ramp's own far slice
/// (<see cref="ShadeRamp.FogColor"/>).</para>
///
/// <para>Retail zones run 246 m to 983 m, so this is a far shorter draw distance than the engine's
/// previous hand-picked 900 m/9000 m — that pair was a guess made before any of this was traced,
/// and it left distant terrain unfogged where the original has it fully washed out.</para>
///
/// <para>The engine spends this as continuous per-pixel fog in <see cref="Render.SceneRenderer"/>
/// rather than as the original's twelve per-object ramp steps. Same start, same end, same colour;
/// smoother in between.</para>
/// </summary>
/// <param name="FogStart">Render units (metres) at which the fade begins — half the visibility range.</param>
/// <param name="FogEnd">Render units at which it is total — the visibility range.</param>
/// <param name="FogColor">
/// What it fades to, or null when the theater's ramp or palette did not load, in which case the
/// renderer keeps its own fallback.
/// </param>
/// <param name="Sky">
/// The theater's banded sky (<see cref="SkyGradient"/>), or null when its palette did not load. The
/// gradient's own bottom band is a neighbour of <paramref name="FogColor"/> in the same palette run,
/// which is what makes fogged ground meet the sky without a seam.
/// </param>
public readonly record struct Atmosphere(float FogStart, float FogEnd, Vector3? FogColor, SkyGradient? Sky) {
	/// <summary>
	/// Reads all of it off the loaded zone and theater. <paramref name="shading"/> may be null (no
	/// ramp), which costs the colours but not the distances: those come from the grid alone.
	/// </summary>
	public static Atmosphere From(HeightGrid terrain, SurfaceShading? shading) {
		long range = terrain.VisibilityRange;
		return new Atmosphere(
			WorldScale.DistanceToRender((int)(range / 2)),
			WorldScale.DistanceToRender((int)range),
			shading?.Ramp.FogColor(shading.Palette),
			SkyGradient.FromPalette(shading?.Palette));
	}

	/// <summary>
	/// Applies this to a renderer. Also sets the flat <see cref="SceneRenderer.SkyColor"/> the frame
	/// is cleared to, so that whatever the gradient does not cover is at least the right hue.
	/// </summary>
	public void ApplyTo(SceneRenderer renderer) {
		renderer.FogStart = FogStart;
		renderer.FogEnd = FogEnd;
		if (FogColor is { } color) {
			renderer.FogColor = color;
		}

		renderer.Sky = Sky;
		if (Sky is { } sky) {
			renderer.SkyColor = sky.Zenith;
		}
	}
}
