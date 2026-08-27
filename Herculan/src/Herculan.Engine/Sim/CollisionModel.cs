using Herculan.Engine.Numerics;
using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

/// <summary>
/// The ray-versus-sphere-model hit test — <c>Mech_SelectStruckComponent</c> (<c>0040c9d4</c>) and
/// the three functions under it. Given an object's collision model and a shot, it answers <b>which
/// component was struck and how far along the ray</b>, which is what turns a hit into damage to a
/// specific part of a specific building.
///
/// <para>Despite the name it is not mech-specific: structures reach it through
/// <c>Base_DirectFireHitTest</c> (<c>00405038</c>) and flyers through <c>FUN_00421c8c</c>, all three
/// passing a model loaded by the same readers. The engine has models for structures only — see
/// <see cref="BaseCollisionTable"/>.</para>
///
/// <para><b>The ray shortens as the test runs.</b> Every sphere that is struck clips the working
/// distance to its own entry point, and later spheres are tested against the clipped ray, so the
/// result is the nearest cluster rather than the first. The original does this through three
/// globals (<c>004a9894</c>, <c>004a9898</c>, <c>004a989c</c>/<c>004a98a0</c>) because only one hit
/// test runs at a time; here it is a local, which is the same thing without the aliasing.</para>
/// </summary>
public static class CollisionModel {
	/// <summary>What a hit test found: how far along the ray, and which component owns the geometry.</summary>
	/// <param name="Distance">
	/// Distance along the ray to the entry point, <b>plus one</b> — the original's own
	/// <c>FUN_0040c524</c> returns <c>distance + 1</c> so that a hit at zero range is still
	/// distinguishable from a miss, and the caller adds another one on top.
	/// </param>
	/// <param name="ComponentIndex">Which of the type's components was struck.</param>
	public readonly record struct Hit(int Distance, short ComponentIndex);

	/// <summary>
	/// Runs the model against a shot.
	/// </summary>
	/// <param name="model">The object's collision model, one entry per shape node.</param>
	/// <param name="toMuzzleSpace">
	/// The object's own frame expressed in the shot's — model space in, muzzle space out, where the
	/// ray is the Y axis.
	/// </param>
	/// <param name="distance">The ray's remaining length.</param>
	/// <param name="clearance">The shot's clearance — see <see cref="WeaponShot.Clearance"/>.</param>
	/// <param name="componentAlive">
	/// Whether each of the type's components is still standing. A destroyed component's spheres are
	/// skipped outright, so a building that has lost a wing stops stopping shots through it.
	/// </param>
	/// <returns>The nearest component struck, or null for a miss.</returns>
	public static Hit? Test(CollisionNode[] model, in Transform3 toMuzzleSpace, int distance,
			int clearance, IReadOnlyList<bool> componentAlive) {
		Hit? best = null;

		foreach (var node in model) {
			// A node-placed cluster is expressed in one of the shape's node transforms, not in the
			// object's frame, and the engine has no node transforms for structures — testing those
			// spheres against the object frame would put them in the wrong place, which is worse than
			// not testing them. Only the eight animated structure types carry any.
			if (node.NodeIndex != BaseCollisionTable.ObjectFrameNode) {
				continue;
			}

			foreach (var cluster in node.Clusters) {
				if (cluster.ComponentIndex < 0 || cluster.ComponentIndex >= componentAlive.Count
						|| !componentAlive[cluster.ComponentIndex]) {
					continue;
				}

				// FUN_0040c8c8: the cluster's own bound first, and the spheres only if it passes.
				if (!BoundStruck(cluster.Bound, toMuzzleSpace, distance, clearance)) {
					continue;
				}

				bool struck = false;
				foreach (var sphere in cluster.Spheres) {
					var center = toMuzzleSpace.TransformPoint(sphere.X, sphere.Y, sphere.Z);
					if (SphereStruck(center, sphere.Radius, clearance, ref distance)) {
						struck = true;
					}
				}

				if (struck) {
					best = new Hit(distance + 1, cluster.ComponentIndex);
				}
			}
		}

		return best;
	}

	/// <summary>
	/// <c>FUN_0040c4c4</c> — the cluster bound's coarse test, which is the same
	/// ray-versus-vertical-cylinder shape every hit test in the simulation uses: the centre has to
	/// be in front of the muzzle and inside the ray's length, and its distance off the ray axis
	/// under the bound's radius. The length comparison is unsigned, which is what rejects anything
	/// behind the muzzle without a second test.
	/// </summary>
	private static bool BoundStruck(CollisionSphere bound, in Transform3 toMuzzleSpace,
			int distance, int clearance) {
		var center = toMuzzleSpace.TransformPoint(bound.X, bound.Y, bound.Z);
		return (uint)center.Y < (uint)(bound.Radius + distance)
			&& SimMath.FastMagnitude2D(center.X, center.Z) < bound.Radius + clearance;
	}

	/// <summary>
	/// <c>FUN_0040c428</c> — one sphere against the ray, clipping <paramref name="distance"/> to the
	/// entry point on a hit.
	///
	/// <para>The entry point is the original's linearisation, <c>alongAxis - (radius - offAxis)</c>,
	/// floored at zero — the same construction the mech's shield test uses, and just as approximate:
	/// it treats the sphere as a cylinder whose front face is a cone. The 16-bit comparison it is
	/// written as is kept because the doubled radius can overflow a signed short, and the original
	/// relies on the wrap.</para>
	/// </summary>
	private static bool SphereStruck(Vec3i center, short radius, int clearance, ref int distance) {
		if ((uint)center.Y >= (uint)distance) {
			return false;
		}

		int offAxis = SimMath.FastMagnitude2D(center.X, center.Z);
		short reach = (short)(clearance + radius);
		if (offAxis >= 0x7fff || (ushort)(unchecked((short)offAxis) + reach) >= (ushort)(reach * 2)) {
			return false;
		}

		int entry = reach - offAxis;
		distance = entry < center.Y ? center.Y - entry : 0;
		return true;
	}
}
