using Herculan.Engine.Numerics;

namespace Herculan.Engine.Render;

/// <summary>
/// Computes the yaw offset for the left/right panels of Milestone 8's simultaneous three-panel
/// cockpit view, so the three panels tile edge-to-edge with no seam or gap regardless of window size.
///
/// <para>This is an explicit engine-modernization choice, not a recovered original value — the
/// original showed one view at a time, panned via a keyboard hotkey (<c>F9</c>/<c>F10</c>). Flagged
/// as such per docs/engine/planning.md's "vanilla by default" principle, which allows purely cosmetic
/// exceptions; nothing about simulation behavior changes.</para>
/// </summary>
public static class CockpitViewLayout {
	/// <summary>
	/// Half the horizontal field of view of a viewport with the given aspect ratio, given the
	/// camera's vertical FOV — i.e. <c>atan(tan(fovY/2) * aspect)</c>.
	/// </summary>
	public static float HalfFovX(float verticalFovRadians, float aspectRatio) =>
		MathF.Atan(MathF.Tan(verticalFovRadians / 2f) * aspectRatio);

	/// <summary>
	/// The binary-angle yaw offset for a side panel so it tiles edge-to-edge against the center panel:
	/// the sum of each panel's own half-horizontal-FOV, since that is the angle from each panel's
	/// center to its shared edge with the other.
	/// </summary>
	public static int SideYawOffset(float verticalFovRadians, float centerAspectRatio, float sideAspectRatio) {
		float halfFovCenter = HalfFovX(verticalFovRadians, centerAspectRatio);
		float halfFovSide = HalfFovX(verticalFovRadians, sideAspectRatio);
		return BinaryAngle.FromRadians(halfFovCenter + halfFovSide);
	}
}
