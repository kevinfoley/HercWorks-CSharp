using System.Numerics;

namespace Herculan.Engine.Render;

/// <summary>
/// The one directional light every mission gets, and the shade byte it produces for a surface.
///
/// <para><c>Light_CreateMissionSun</c> (<c>004614fc</c>'s callee) runs once per mission,
/// unconditionally, and builds the sun from constants compiled into DBSIM: direction
/// <c>rotate((0,0x1000,0), eulerMatrix(-6000,0,21000))</c> in the sim's Z-up world space, intensity
/// <c>0x100</c>. No mission or theater file contributes to it, and no ambient light is created
/// anywhere in the binary — a face angled away from the sun gets shade 0, not a floor.</para>
///
/// <para><b>The shade calculation</b> is <c>FUN_0048c060</c>, the per-normal sibling of
/// <c>Light_ComputeShadeForFace</c>. It walks the active light list and, for a type-1 (directional)
/// entry, does exactly this:</para>
/// <code>
/// dot = normal . lightDirection            // plain integer dot product, no normalisation
/// if (dot &lt; 0) shade -= (intensity * dot) &gt;&gt; 22
/// shade = min(shade, 255)
/// </code>
/// <para>The magnitudes are what make this interesting. Surface normals are normalised to length
/// <c>0x800</c> (<c>FUN_0046c138</c> does that to both of a terrain cell's normals) and the sun's
/// direction vector to <c>0x1000</c>, so <c>|dot| = 0x800 * 0x1000 * cos</c> and the whole expression
/// collapses to <c>shade = min(255, 512 * cos)</c>. <b>It saturates at cos 0.5</b> — every surface
/// within 60 degrees of facing the sun is drawn at the ramp's top row, not on a falloff. Flat ground
/// sits at cos 0.544 and is therefore pinned at 255, which is why retail terrain reads as evenly lit
/// with shading confined to the steeper slopes.</para>
///
/// <para>The contribution is gated on <c>dot &lt; 0</c>, so the direction field is the direction the
/// light travels, pointing into the surfaces it lights.</para>
/// </summary>
public static class MissionSun {
	/// <summary>The sun's intensity, the literal <c>0x100</c> the light's setter is called with.</summary>
	public const int Intensity = 0x100;

	/// <summary>The length surface normals are normalised to before the dot product.</summary>
	public const int NormalLength = 0x800;

	/// <summary>The length the sun's direction vector carries.</summary>
	public const int DirectionLength = 0x1000;

	/// <summary>The largest shade byte the calculation can return.</summary>
	public const int MaxShade = 255;

	/// <summary>
	/// Direction the sun's light travels, in render space, as a unit vector.
	///
	/// <para>The 3-axis rotation order was not read out of the exe's fixed-point trig, but only one
	/// order is consistent with the game rendering at all: composing Z after X puts the direction's
	/// world Z at -0.544, lighting upward-facing ground, while the other order puts it at +0.233,
	/// which would leave every flat cell facing away from the sun and the whole zone at shade 0.</para>
	/// </summary>
	public static Vector3 Direction { get; } = ComputeDirection();

	private static Vector3 ComputeDirection() {
		// The sim's angle unit is a full circle per 0x10000, per BuildEulerRotationMatrixQ14.
		const float RadiansPerRawUnit = MathF.PI * 2f / 65536f;
		float xRadians = -6000f * RadiansPerRawUnit;
		float zRadians = 21000f * RadiansPerRawUnit;

		var qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, xRadians);
		var qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, zRadians);
		Vector3 worldDirection = Vector3.Transform(new Vector3(0f, 1f, 0f), qz * qx);

		// DBSIM world is Z-up with X/Y the ground plane; render space is Y-up — same mapping as
		// WorldScale.ToRender, which is a rotation, so the dot product below is unaffected by
		// computing it here rather than in world space.
		Vector3 renderDirection = new(worldDirection.X, worldDirection.Z, -worldDirection.Y);
		return Vector3.Normalize(renderDirection);
	}

	/// <summary>
	/// The shade byte <c>FUN_0048c060</c> returns for a surface with this normal, 0-255.
	/// </summary>
	/// <param name="renderNormal">The surface normal in render space; need not be unit length.</param>
	public static int ShadeFor(Vector3 renderNormal) {
		if (renderNormal.LengthSquared() <= 1e-12f) {
			return 0;
		}

		// The original's dot is negative when the face is lit; expressing it as a positive cosine
		// against the reversed direction keeps the sign convention readable here.
		float facing = -Vector3.Dot(Vector3.Normalize(renderNormal), Direction);
		if (facing <= 0f) {
			return 0;
		}

		long dot = (long)(facing * NormalLength * DirectionLength);
		return (int)Math.Min((Intensity * dot) >> 22, MaxShade);
	}
}
