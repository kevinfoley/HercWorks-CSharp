using Herculan.Engine.Numerics;
using Herculan.Engine.Sim;
using Herculan.Engine.Terrain;

namespace Herculan.Engine.Render;

/// <summary>
/// The external ("chase") view — the camera parked a fixed distance behind the machine the player
/// pilots, looking at it over its own shoulder, with the cockpit not drawn.
///
/// <para><b>Not reverse-engineered — placeholder geometry (2026-08-22).</b> Every number below is
/// this engine's own choice, picked to frame a HERC nicely; none of it is ported. DBSIM has its own
/// external views (the manual's [V] cycles through several, and there are missile/target cameras
/// besides), and their placement rules, transitions, whatever they do about terrain and whatever
/// chrome they draw over the view are all unrecovered. When those are RE'd this type is where the
/// real rule replaces the guess: the host only asks it to place a <see cref="Camera"/>, so nothing
/// outside it depends on how the position is chosen.</para>
/// </summary>
public static class ExternalCamera {
	/// <summary>How far behind the machine the eye sits, in metres.</summary>
	public const float DistanceMeters = 10f;

	/// <summary>How far above the machine's origin the eye sits, in metres.</summary>
	public const float HeightMeters = 6f;

	/// <summary>
	/// How high up the machine the eye aims, in metres above its origin — roughly torso height on a
	/// retail HERC, so the machine sits in frame rather than at the bottom edge.
	/// </summary>
	public const float AimHeightMeters = 4f;

	/// <summary>
	/// Least clearance kept above the ground under the eye, in metres, so backing into a rising
	/// slope does not bury the camera in it.
	/// </summary>
	public const float GroundClearanceMeters = 2f;

	private static int Units(float meters) => (int)(meters * WorldScale.WorldUnitsPerMeter);

	/// <summary>
	/// Points <paramref name="camera"/> at <paramref name="mech"/> from behind it.
	/// <paramref name="terrain"/> is optional and only used for the ground clearance floor.
	/// </summary>
	public static void Place(Camera camera, MechObject mech, HeightGrid? terrain = null) {
		// A HERC's forward vector is (-sin h, cos h) in world XY (see MissionScene.TransformOf), so
		// directly behind it is the negation of that.
		var target = mech.Position;
		int distance = Units(DistanceMeters);
		int eyeX = target.X + distance * BinaryAngle.Sin(mech.Heading) / BinaryAngle.TrigOne;
		int eyeY = target.Y - distance * BinaryAngle.Cos(mech.Heading) / BinaryAngle.TrigOne;
		int eyeZ = target.Z + Units(HeightMeters);

		if (terrain != null) {
			eyeZ = Math.Max(eyeZ, terrain.HeightAtWorld(eyeX, eyeY) + Units(GroundClearanceMeters));
		}

		camera.Position = new Vec3i(eyeX, eyeY, eyeZ);

		// Camera yaw runs opposite to a simulation heading (see MissionScene.TransformOf), and the
		// eye is directly behind the machine, so it faces the way the machine does. Pitch is whatever
		// tips the view down onto the aim point from however high the eye ended up.
		camera.Yaw = -mech.Heading & 0xffff;
		camera.Pitch = SimTrig.Atan2(target.Z + Units(AimHeightMeters) - eyeZ, distance) & 0xffff;
	}
}
