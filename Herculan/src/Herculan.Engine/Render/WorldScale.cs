using System.Numerics;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Render;

/// <summary>
/// The one place where the simulation's integer world units become floating-point render units,
/// and where the original's Z-up axes become the renderer's Y-up ones. Simulation code never does
/// either conversion: per docs/engine/planning.md's math decision, anything that feeds back into
/// simulation state stays in the original's fixed-point domain, so the float boundary is here and
/// nowhere else.
/// </summary>
public static class WorldScale {
	/// <summary>
	/// How many DBSIM world units make up one metre of rendered space.
	///
	/// <para><b>Estimated, not recovered.</b> No RE'd constant states the game's unit scale
	/// directly, so this is triangulated from constants that are known: a missile's ground-impact
	/// blast radius is 3000 units and a mech's death explosion 2000, the rocket proximity warning
	/// fires at 40000, and a terrain cell spans 16384 (<c>CellShift</c> 14 in every retail zone).
	/// At 200 units per metre those read as a 15 m blast, a 200 m proximity warning, an ~82 m
	/// terrain cell and a ~10.5 km square zone — all plausible for this game, and no other scale
	/// within a factor of two makes all four plausible at once. It affects only how large the world
	/// looks and how fast the camera appears to move; nothing in the simulation reads it.</para>
	/// </summary>
	public const float WorldUnitsPerMeter = 200f;

	/// <summary>
	/// How many world units one raw DTS model unit spans — one, i.e. model coordinates are world
	/// coordinates with no conversion at all.
	///
	/// <para>This is a hypothesis, but a well-supported one, and it was <i>not</i> assumed: it fell
	/// out of measuring real models against the independently-derived
	/// <see cref="WorldUnitsPerMeter"/> above. Reading DTS point shorts as world units directly puts
	/// SAMSON (a heavy HERC) at 11.8 m tall and 7.1 m wide, OUTLAW (a light one) at 8.5 m, and APOCA
	/// at 11.7 m — the right absolute size for a HERC and the right ordering between classes, from
	/// two constants derived from completely separate evidence. Every mech model measured also has
	/// its lowest point at exactly model-space zero, i.e. authored standing on the ground plane,
	/// which is what a world-space authoring convention would produce.</para>
	///
	/// <para>Kept as a named constant rather than deleted so that the claim stays visible and one
	/// number changes if the real placement code is ever traced. Note this differs from the WinForms
	/// model viewer, which scales DTS points by 1/10 — that viewer picks whatever scale frames a
	/// model nicely in its own window and has no world to be consistent with.</para>
	/// </summary>
	public const float WorldUnitsPerDtsUnit = 1f;

	/// <summary>
	/// Converts a simulation position to render space. DBSIM is Z-up with X/Y as the ground plane;
	/// OpenGL here is Y-up. The mapping <c>(x, y, z) -> (x, z, -y)</c> is a rotation with
	/// determinant +1, not an axis swap — a bare swap would be a mirror reflection and would flip
	/// every triangle's winding.
	/// </summary>
	public static Vector3 ToRender(Vec3i world) =>
		new(world.X / WorldUnitsPerMeter, world.Z / WorldUnitsPerMeter, -world.Y / WorldUnitsPerMeter);

	/// <summary>Same mapping as <see cref="ToRender(Vec3i)"/>, for values already carrying a fractional part.</summary>
	public static Vector3 ToRender(float worldX, float worldY, float worldZ) =>
		new(worldX / WorldUnitsPerMeter, worldZ / WorldUnitsPerMeter, -worldY / WorldUnitsPerMeter);

	/// <summary>Converts a scalar distance in world units to render units.</summary>
	public static float DistanceToRender(int worldDistance) => worldDistance / WorldUnitsPerMeter;

	/// <summary>Converts a DTS model-space coordinate triple straight to render space.</summary>
	public static Vector3 DtsToRender(float dtsX, float dtsY, float dtsZ) =>
		ToRender(dtsX * WorldUnitsPerDtsUnit, dtsY * WorldUnitsPerDtsUnit, dtsZ * WorldUnitsPerDtsUnit);
}
