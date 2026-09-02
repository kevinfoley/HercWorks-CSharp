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
	/// A <b>shade byte</b>, 0-255, for a surface that carries its own instead of having one computed
	/// per frame — read only when <see cref="Unlit"/> is set.
	///
	/// <para>This is how a surface the original shades <i>ahead of time</i> gets drawn. Terrain is the
	/// case: <c>Terrain_BuildSurface</c> lights every cell at zone load and stores the two shade
	/// bytes in the cell itself (offsets <c>+0xd</c> and <c>+0xe</c>, one per triangle), and
	/// <c>Terrain_DrawCellQuad</c> hands the byte straight to the span setup. Nothing about it is
	/// recomputed per frame, so running the renderer's own light term over terrain is not a stand-in
	/// for the original, it is a second, different light.
	/// Terrain carries <see cref="Unlit"/> set and its baked byte here instead.</para>
	///
	/// <para>See <see cref="Render.MissionSun"/> for where the byte comes from and
	/// <see cref="Render.PaletteRampTable"/> for what the shader does with it — it selects a row of
	/// the theater ramp, and each texel's palette index selects the column, which is the original's
	/// own per-pixel operation rather than a brightness applied over an expanded colour.</para>
	/// </summary>
	public float Shade;

	/// <summary>
	/// Which of the theater palette's <b>material shade ramps</b> this surface names, or -1 for a
	/// surface that is not shaded that way. The default.
	///
	/// <para>This is the vertex half of <see cref="Render.SurfaceShading.ShadedColor"/>. A
	/// <c>TSShadedPoly</c>'s colour is not a colour at all until a light level is known — the surface
	/// value picks a ramp and the face's shade picks a step along it — and the shade depends on the
	/// face's <i>world</i> normal, which differs per instance because one built mesh is shared by
	/// every structure of a type at its own heading. So the ramp number travels to the GPU and the
	/// lookup happens per fragment, against <see cref="Render.SurfaceRampTable"/>. Baking it here
	/// would pin every instance to the rest pose's lighting.</para>
	///
	/// <para>It is more than a bare ramp number: a <c>TSGouraudPoly</c>'s value carries
	/// <see cref="Render.SurfaceRampTable.GouraudRowOffset"/> on top, because the two lit types spend
	/// the same ramp through different chains — see <see cref="Render.SurfaceShading.GouraudColor"/>.
	/// The pair names the chain and the ramp; the shader turns it into a row, since the shaded chain
	/// is stored once per depth slice and the Gouraud chain once in total.</para>
	/// </summary>
	public float ShadeRamp;

	/// <summary>
	/// The <b>face's</b> own normal, identical across the triangle's three corners, where
	/// <see cref="Normal"/> may be a smoothed per-corner one.
	///
	/// <para>It exists for the front/back decision, which the original makes once per poly rather
	/// than per pixel: <c>TSPoly_FrontBackVisibilityTest</c> takes the poly's stored normal and
	/// centre, and every renderer negates <i>all</i> of the poly's normals together when the answer
	/// is "back". Making that call from the smoothed normal instead would let one corner of a
	/// Gouraud poly flip while another did not, which shows up as a seam along a silhouette.</para>
	///
	/// <para>Defaults to <see cref="Normal"/>, which is right for every flat poly — there the two
	/// are the same vector.</para>
	/// </summary>
	public Vector3 FaceNormal;

	/// <summary>
	/// The homogeneous weight <see cref="UV"/> is premultiplied by, or 0 for a vertex whose
	/// <see cref="UV"/> is a plain coordinate — the default, and what terrain and every non-quad poly
	/// carry.
	///
	/// <para>Interpolating <c>(u·w, v·w)</c> and <c>w</c> and dividing per fragment is what makes a
	/// textured quad's two triangles share one projective map instead of each getting its own affine
	/// one. See <see cref="Render.DtsMeshBuilder"/>'s <c>QuadUvWeights</c> for the weights and
	/// docs/formats/dts-texture-binding.md's "Quad mapping on triangle hardware" for why.</para>
	/// </summary>
	public float UvWeight;

	public MeshVertex(Vector3 position, Vector3 normal, Vector3 color, Vector2 uv = default,
			bool textured = false, bool unlit = false, float shade = 1f, int shadeRamp = -1,
			Vector3? faceNormal = null, float uvWeight = 0f) {
		Position = position;
		Normal = normal;
		FaceNormal = faceNormal ?? normal;
		Color = color;
		UV = uv;
		Textured = textured ? 1f : 0f;
		Unlit = unlit ? 1f : 0f;
		Shade = shade;
		ShadeRamp = shadeRamp;
		UvWeight = uvWeight;
	}

	/// <summary>Bytes per vertex, used as the vertex-attribute stride.</summary>
	public const uint SizeInBytes = 19 * sizeof(float);
}
