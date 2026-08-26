using System.Numerics;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Render;

/// <summary>
/// A view onto the world. Its position and orientation are kept in simulation terms — an integer
/// world position and binary-angle yaw/pitch — and only converted to float matrices when a frame is
/// actually drawn. That keeps the camera something a <see cref="Sim.SimObject"/> can drive with the
/// same ported math as anything else (see <see cref="Sim.FlyCameraObject"/>) instead of it being a
/// separate float-space concept bolted onto the side of the renderer.
/// </summary>
public sealed class Camera {
	/// <summary>Eye position in world units.</summary>
	public Vec3i Position { get; set; }

	/// <summary>Heading, as a binary angle. 0 looks along +Y (the world's "north").</summary>
	public int Yaw { get; set; }

	/// <summary>Pitch, as a binary angle. Positive looks up.</summary>
	public int Pitch { get; set; }

	/// <summary>Vertical field of view, in radians.</summary>
	public float FieldOfView { get; set; } = MathF.PI / 3f;

	/// <summary>
	/// Near plane, in render units (metres). Kept well out from zero on purpose: depth precision is
	/// governed by the far/near ratio, and the smallest thing in this world is a ~10 m mech, so
	/// there is nothing to gain from a near plane closer than a couple of metres and a lot of depth
	/// resolution to lose across a 12 km view.
	/// </summary>
	public float NearPlane { get; set; } = 2f;

	/// <summary>
	/// Far plane, in render units. A retail zone is 12.6 km across (128 cells of 16384 units) at
	/// <see cref="WorldScale.WorldUnitsPerMeter"/>, so this is set to see most of one from altitude;
	/// the original's own draw distance is a separate question (it has a visibility/LOD system that
	/// hasn't been RE'd) and this is not an attempt to match it.
	/// </summary>
	public float FarPlane { get; set; } = 12000f;

	/// <summary>
	/// Where the view axis lands in the viewport, as a fraction of it from the top-left — the
	/// projection's principal point. (0.5, 0.5) is the ordinary symmetric frustum and is the default;
	/// anything else builds an off-centre one of the same scale, shifting where the vanishing point
	/// sits without changing how large anything is drawn.
	///
	/// <para><b>The cockpit needs this.</b> DBSIM's own projection ends in
	/// <c>screenX = x + centreX; screenY = centreY - y</c> with the pair coming from the herc's
	/// <c>.VUE</c> (see <see cref="Content.CockpitViewGeometry.ProjectionCenter"/>), and that point is
	/// well above the middle of the view — APOCA's is 95 rows down a 240-row view. It is also where
	/// the gunsight reticle is drawn, which is what makes it load-bearing rather than cosmetic: a
	/// shot leaves the muzzle parallel to the view axis, so its vanishing point is the principal
	/// point, and centring the frustum instead puts every shot below the sight it is supposed to
	/// converge on.</para>
	/// </summary>
	public Vector2 PrincipalPoint { get; set; } = new(0.5f, 0.5f);

	/// <summary>Unit forward direction in render space.</summary>
	public Vector3 Forward {
		get {
			float yaw = BinaryAngle.ToRadians(Yaw);
			float pitch = BinaryAngle.ToRadians(Pitch);
			float cosPitch = MathF.Cos(pitch);

			// Yaw 0 faces world +Y, which is render -Z (see WorldScale.ToRender).
			return Vector3.Normalize(new Vector3(
				MathF.Sin(yaw) * cosPitch,
				MathF.Sin(pitch),
				-MathF.Cos(yaw) * cosPitch));
		}
	}

	/// <summary>View matrix for the current position and orientation.</summary>
	public Matrix4x4 ViewMatrix {
		get {
			Vector3 eye = WorldScale.ToRender(Position);
			return Matrix4x4.CreateLookAt(eye, eye + Forward, Vector3.UnitY);
		}
	}

	/// <summary>
	/// Projection matrix for a viewport of the given aspect ratio (width / height), honouring
	/// <see cref="PrincipalPoint"/>.
	///
	/// <para>The off-centre form keeps the near plane's extent — and therefore the focal length and
	/// everything's on-screen size — identical to the symmetric one; it only moves which point of that
	/// extent the axis passes through. At (0.5, 0.5) it reduces to
	/// <c>CreatePerspectiveFieldOfView</c> exactly.</para>
	/// </summary>
	public Matrix4x4 ProjectionMatrix(float aspectRatio) {
		float aspect = MathF.Max(aspectRatio, 0.0001f);
		if (PrincipalPoint == new Vector2(0.5f, 0.5f)) {
			return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, aspect, NearPlane, FarPlane);
		}

		float height = 2f * NearPlane * MathF.Tan(FieldOfView / 2f);
		float width = height * aspect;

		// PrincipalPoint is measured from the top-left with +Y down; the frustum's top and bottom are
		// signed distances from the axis with +Y up, hence the flip on the vertical pair.
		return Matrix4x4.CreatePerspectiveOffCenter(
			left: -PrincipalPoint.X * width,
			right: (1f - PrincipalPoint.X) * width,
			bottom: -(1f - PrincipalPoint.Y) * height,
			top: PrincipalPoint.Y * height,
			NearPlane, FarPlane);
	}

	/// <summary>
	/// Builds a world-space (render units) pick ray through a point on the viewport, for
	/// click-to-select. <paramref name="ndc"/> is normalized device coordinates: -1..1 on each
	/// axis, +X right, +Y up, origin at the viewport's center — the caller converts screen pixels
	/// (usually +Y down) itself.
	/// </summary>
	public (Vector3 Origin, Vector3 Direction) ViewportPointToRay(Vector2 ndc, float aspectRatio) {
		Vector3 forward = Forward;
		Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
		Vector3 up = Vector3.Cross(right, forward);

		float tanHalfFov = MathF.Tan(FieldOfView / 2f);

		// Where the view axis itself sits in this NDC space. At the default centred principal point
		// this is (0, 0) and the two subtractions below vanish.
		float axisX = 2f * PrincipalPoint.X - 1f;
		float axisY = 1f - 2f * PrincipalPoint.Y;

		Vector3 direction = Vector3.Normalize(
			forward
			+ right * ((ndc.X - axisX) * tanHalfFov * aspectRatio)
			+ up * ((ndc.Y - axisY) * tanHalfFov));

		return (WorldScale.ToRender(Position), direction);
	}
}
