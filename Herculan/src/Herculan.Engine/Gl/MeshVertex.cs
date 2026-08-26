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

	public MeshVertex(Vector3 position, Vector3 normal, Vector3 color, Vector2 uv = default,
			bool textured = false, bool unlit = false) {
		Position = position;
		Normal = normal;
		Color = color;
		UV = uv;
		Textured = textured ? 1f : 0f;
		Unlit = unlit ? 1f : 0f;
	}

	/// <summary>Bytes per vertex, used as the vertex-attribute stride.</summary>
	public const uint SizeInBytes = 13 * sizeof(float);
}
