using System.Numerics;
using System.Runtime.InteropServices;

namespace Herculan.Engine.Gl;

/// <summary>
/// The engine's single vertex format: position, normal, colour, UV, and whether the UV means
/// anything.
///
/// <para><see cref="Textured"/> is per-vertex rather than per-draw because both meshes that carry
/// textures are mixed: a mech mesh has a handful of texture polys whose frame index does not resolve
/// (see docs/formats/dts-texture-binding.md's fleet audit), and a terrain mesh can have cells whose
/// material selects a frame the theater's bank does not have. Those fall back to
/// <see cref="Color"/> while their neighbours sample the atlas, which a single per-draw flag cannot
/// express — it would either sample garbage UVs for the strays or drop the whole mesh's texturing.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MeshVertex {
	public Vector3 Position;
	public Vector3 Normal;
	public Vector3 Color;
	public Vector2 UV;

	/// <summary>1 when <see cref="UV"/> addresses a real atlas rect, 0 to use <see cref="Color"/>.</summary>
	public float Textured;

	/// <summary>
	/// 1 to take <see cref="Color"/> exactly as given, with no light term over it; 0 to shade it.
	///
	/// <para>Sensed rather than styled: the original's flat solid poly (<c>TSSolidPoly_Render</c>,
	/// <c>00474db4</c>) never computes a light term — it looks its colour up in the theater's ramp at
	/// a fixed shade byte and fills. Its shaded sibling <c>TSShadedPoly</c> does light the face, and
	/// that is what almost every surface of a HERC or a building is. So the two need different
	/// treatment on the same mesh, per vertex, for the same reason <see cref="Textured"/> is per
	/// vertex — see <see cref="Content.ShadeRamp"/>.</para>
	///
	/// <para>Zero by default, so a vertex built any other way stays lit.</para>
	/// </summary>
	public float Unlit;

	/// <summary>
	/// A brightness multiplier already resolved through the theater's ramp, applied on top of whatever
	/// <see cref="Unlit"/> decides. 1 is the identity and the default.
	///
	/// <para>This is how a surface the original shades <i>ahead of time</i> gets drawn. Terrain is the
	/// case: <c>Terrain_BuildSurface</c> lights every cell once at zone load and stores the two shade
	/// bytes in the cell itself (offsets <c>+0xd</c> and <c>+0xe</c>, one per triangle), and
	/// <c>Terrain_DrawCellQuad</c> hands the byte straight to the span setup. Nothing about it is
	/// recomputed per frame, so running the renderer's own light term over terrain — which is what the
	/// engine used to do — is not a stand-in for the original, it is a second, different light.
	/// Terrain now carries <see cref="Unlit"/> set (no runtime term) and its shade here instead.</para>
	///
	/// <para>See <see cref="Render.MissionSun"/> for the shade byte and
	/// <see cref="Render.ShadeBrightness"/> for the byte-to-multiplier curve.</para>
	/// </summary>
	public float Shade;

	public MeshVertex(Vector3 position, Vector3 normal, Vector3 color, Vector2 uv = default,
			bool textured = false, bool unlit = false, float shade = 1f) {
		Position = position;
		Normal = normal;
		Color = color;
		UV = uv;
		Textured = textured ? 1f : 0f;
		Unlit = unlit ? 1f : 0f;
		Shade = shade;
	}

	/// <summary>Bytes per vertex, used as the vertex-attribute stride.</summary>
	public const uint SizeInBytes = 14 * sizeof(float);
}
