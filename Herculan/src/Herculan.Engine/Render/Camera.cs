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

	/// <summary>
	/// Roll about the view axis, as a binary angle — the simulation's own euler Y, taken straight
	/// off whatever frame the camera is riding.
	///
	/// <para>Not decorative. DBSIM's view carries a full euler triple (<c>view+0x10</c>,
	/// <c>+0x12</c>, <c>+0x14</c>) and builds its view rotation from all three: the cockpit branch of
	/// <c>FUN_004011a0</c> takes the pilot node's world matrix, converts it with
	/// <c>FUN_0047f894</c>, and stores every angle it gets back. A HERC turning on the spot rolls
	/// that node by several degrees each step, which is what rocks the cockpit through a
	/// turn-in-place; dropping the angle turns the manoeuvre into a smooth yaw it is not.</para>
	/// </summary>
	public int Roll { get; set; }

	/// <summary>
	/// DBSIM's own focal length, in device pixels — the whole of its projection. The rasterizer
	/// projects a view-space point as <c>screen = (v &lt;&lt; shift) / depth</c>
	/// (<c>Raster_PerspectiveScale</c>, <c>0048c4c0</c>, reading the view's <c>+0x1a</c>), so the
	/// shift <i>is</i> the focal length: <c>2^shift</c> pixels.
	///
	/// <para><c>Sim_InitMissionSession</c> (<c>004614fc</c>) picks it as 9 when the mode's canvas
	/// width (<c>VideoMode_Configure</c>'s <c>DAT_004d25ca</c>) reaches 1201 and 8 otherwise — 8 for
	/// the 320x240 mode's 640, 9 for the 640x480 modes' 1280. The two work out to the same angle,
	/// which is the point: 256 pixels across a 240-row view and 512 across a 480-row one.</para>
	/// </summary>
	public const float FocalLengthPixels = 512f;

	/// <summary>The view height that focal length belongs to, in the same device pixels.</summary>
	public const float FocalViewHeightPixels = 480f;

	/// <summary>
	/// Vertical field of view, in radians. Defaults to the angle
	/// <see cref="FocalLengthPixels"/> subtends over <see cref="FocalViewHeightPixels"/> — 50.2
	/// degrees, the original's own.
	///
	/// <para>It is not a taste setting. The focal length decides how large everything is drawn, so a
	/// wider view than the original's shrinks the whole world in the same proportion: at the 60
	/// degrees this used to default to, every distance, every machine and the cockpit's own bob came
	/// out 23% smaller than retail draws them.</para>
	/// </summary>
	public float FieldOfView { get; set; } =
		2f * MathF.Atan(FocalViewHeightPixels / 2f / FocalLengthPixels);

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

	/// <summary>
	/// The camera's orientation as the simulation would hold it — the rotation
	/// <c>BuildEulerRotationMatrixQ14</c> builds from this view's euler triple. With row vectors its
	/// rows are the view's own axes: row 0 right, row 1 forward, row 2 up. <see cref="Yaw"/> negates
	/// the simulation's Z angle (see <c>MissionScene.TransformOf</c>), so it goes back in negated.
	/// </summary>
	private Transform3 SimRotation =>
		Transform3.FromEuler(unchecked((short)Pitch), unchecked((short)Roll), unchecked((short)-Yaw));

	/// <summary>Unit forward direction in render space.</summary>
	public Vector3 Forward {
		get {
			var rotation = SimRotation;
			return RenderDirection(rotation.M[2], rotation.M[3], rotation.M[5]);
		}
	}

	/// <summary>
	/// Unit up direction in render space. Plain world up until <see cref="Roll"/> or <see cref="Pitch"/>
	/// tilt it.
	/// </summary>
	public Vector3 Up {
		get {
			var rotation = SimRotation;
			return RenderDirection(rotation.M[6], rotation.M[7], rotation.M[8]);
		}
	}

	/// <summary>A Q14 direction in simulation axes as a unit direction in render axes.</summary>
	private static Vector3 RenderDirection(short x, short y, short z) =>
		Vector3.Normalize(new Vector3(x, z, -y));

	/// <summary>View matrix for the current position and orientation.</summary>
	public Matrix4x4 ViewMatrix {
		get {
			Vector3 eye = WorldScale.ToRender(Position);
			return Matrix4x4.CreateLookAt(eye, eye + Forward, Up);
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
		Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Up));
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
