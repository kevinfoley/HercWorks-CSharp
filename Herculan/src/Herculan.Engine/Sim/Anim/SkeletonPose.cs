using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim.Anim;

/// <summary>
/// One node of a machine's skeleton on the frame it was sampled: which transform it is, which
/// transform it hangs from, and where that puts it in the world.
/// </summary>
/// <param name="TransformId">Index into the shape's transform table — the id
/// <see cref="AnimationThread.NodeTransform"/> takes.</param>
/// <param name="ParentId">The transform this one hangs from, or -1 at the root.</param>
/// <param name="World">The node's origin in world units.</param>
public readonly record struct SkeletonJoint(int TransformId, int ParentId, Vec3i World);

/// <summary>
/// Samples every node of an animating machine into world space — the debug counterpart to
/// <see cref="MechObject.EyePosition"/>, which asks the same question about one node only.
///
/// <para>This exists because the animation system currently has exactly one observable output: the
/// player's eye. The rendered mesh is baked at the shape's default pose (see
/// <c>DtsMeshBuilder.ResolveGroupOffset</c>) and drawn with a single per-object matrix, so nothing
/// on screen moves when a walk cycle plays, and a defect anywhere in the thread, the sequence data
/// or the root motion can only be inferred from how the camera feels. Drawing the pose makes the
/// whole of it visible without needing the per-node render path — see docs/engine/handoff-player-movement.md.</para>
///
/// <para>Nothing here is a port; the original has no such view. It reads the same
/// <see cref="AnimationThread"/> state the simulation reads and adds no state of its own, so it
/// cannot change what it is measuring.</para>
/// </summary>
public static class SkeletonPose {
	/// <summary>
	/// Every transform in <paramref name="mech"/>'s shape, in world units, on the frame its thread is
	/// currently showing. Empty when the machine has no animation data.
	///
	/// <para>Nodes the playing sequence never places come back at the machine's own origin:
	/// <see cref="AnimationThread.NodeTransform"/> accumulates nothing for them and returns identity,
	/// which is indistinguishable from a node genuinely posed at the origin. A cluster of joints
	/// sitting exactly on the origin is that case, not a collapsed skeleton.</para>
	/// </summary>
	public static SkeletonJoint[] Build(MechObject mech) {
		if (mech.Thread is not { } thread || mech.Animation is not { } animation) {
			return Array.Empty<SkeletonJoint>();
		}

		var parents = animation.ParentTransform;
		var world = mech.WorldTransform;

		var joints = new SkeletonJoint[parents.Length];
		for (int id = 0; id < parents.Length; id++) {
			var local = thread.NodeTransform(id);
			joints[id] = new SkeletonJoint(id, parents[id],
				world.TransformPoint(local.X, local.Y, local.Z));
		}

		return joints;
	}

	/// <summary>
	/// The transform the machine's camera node sits on — the one <see cref="MechObject.EyePosition"/>
	/// rides — or -1 when the shape does not carry it.
	/// </summary>
	public static int CameraTransformId(MechObject mech) =>
		mech.Animation?.TransformIdOfPart(mech.Type.CameraBoneId) ?? -1;
}
