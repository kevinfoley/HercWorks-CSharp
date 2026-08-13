using System.Numerics;
using System.Runtime.InteropServices;

namespace Herculan.Engine.Gl;

/// <summary>
/// The engine's single vertex format: position, normal, colour. One format covers both terrain and
/// model geometry for now because both are flat-shaded untextured triangles — the first milestone
/// excludes textured rendering (see docs/engine/planning.md), and the DBSIM-side texture-selection
/// convention is still the one open RE item for that work.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MeshVertex {
	public Vector3 Position;
	public Vector3 Normal;
	public Vector3 Color;

	public MeshVertex(Vector3 position, Vector3 normal, Vector3 color) {
		Position = position;
		Normal = normal;
		Color = color;
	}

	/// <summary>Bytes per vertex, used as the vertex-attribute stride.</summary>
	public const uint SizeInBytes = 9 * sizeof(float);
}
