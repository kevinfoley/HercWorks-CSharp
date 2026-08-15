using System.Numerics;
using System.Runtime.InteropServices;

namespace Herculan.Engine.Gl;

/// <summary>
/// The vertex format for <see cref="Render.Overlay2DRenderer"/>'s 2D HUD/cockpit-art overlay — a
/// separate, lighter layout from <see cref="MeshVertex"/> on purpose, since forcing flat 2D quads
/// through a 12-float lit-3D-geometry layout (normals, no meaning here) would waste bandwidth for no
/// benefit. <see cref="Position"/> is in viewport pixel space, origin top-left, +Y down — the same
/// convention <c>PixelPoint</c>/<c>PixelSize</c> already use throughout <c>HercWorks.Core</c> — so
/// GAU widget rects need no axis-flip to place, only the shared 2x scale
/// (<c>Content.CockpitArt.GauToPixelScale</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Overlay2DVertex {
	public Vector2 Position;
	public Vector2 UV;
	public Vector3 Color;

	/// <summary>1 to sample the bound texture (mixed with <see cref="UV"/>'s alpha), 0 for a flat <see cref="Color"/> shape.</summary>
	public float Textured;

	public Overlay2DVertex(Vector2 position, Vector3 color) {
		Position = position;
		UV = Vector2.Zero;
		Color = color;
		Textured = 0f;
	}

	public Overlay2DVertex(Vector2 position, Vector2 uv) {
		Position = position;
		UV = uv;
		Color = Vector3.Zero;
		Textured = 1f;
	}

	/// <summary>Bytes per vertex, used as the vertex-attribute stride.</summary>
	public const uint SizeInBytes = 8 * sizeof(float);
}
