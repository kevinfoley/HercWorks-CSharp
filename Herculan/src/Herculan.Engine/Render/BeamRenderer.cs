using System.Numerics;
using System.Runtime.InteropServices;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Herculan.Engine.Sim;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Render;

/// <summary>
/// Draws the live <see cref="BeamTracer"/>s — the GPU counterpart of <c>FUN_0040bc14</c>, the
/// tracer class's own paint method.
///
/// <para><b>What the original does.</b> It brings the tracer's two stored points into view space,
/// clips the pair against the near plane (<c>FUN_0040bb4c</c>), projects both to screen, and then
/// builds a four-vertex poly by stepping each screen point along the 2D perpendicular of the segment
/// by that end's own projected half-width — <c>(halfWidth &lt;&lt; shift) / viewZ</c>, floored at two
/// pixels, which floors the <i>half</i>-width and so keeps a beam four pixels across at worst. The
/// quad's UVs put the profile frame's u across the beam's <i>length</i> and its v across the width,
/// and nothing writes a z, so it is drawn flat over what is already there.</para>
///
/// <para><b>The fill is a plain texture copy.</b> The poly goes to <c>FUN_00468310</c> as mode
/// <c>0</c> with its last argument zero, which selects <c>FUN_0046ab10</c>'s opaque half: fetch
/// <c>atlasPage[v][u]</c>, store that palette byte, step the fixed-point u/v, repeat. No shade level,
/// no colour lookup, no transparency test, no blending of any kind — so the fragment shader here is
/// the same single texture fetch, and the profile is uploaded fully opaque. See
/// <see cref="BeamAppearance"/> for why the record's colour index does not enter into it.</para>
///
/// <para><b>What this does instead, and why.</b> The expansion happens in the vertex shader in clip
/// space: the perpendicular is <c>cross(axis, toCamera)</c> so the quad faces the viewer in three
/// dimensions rather than two, and the offset is applied at each vertex's own depth, which is the
/// same construction the original approximates and is exact rather than interpolated. The half-width
/// floor is kept literally, measured in the framebuffer the panel is drawn into. Depth testing is on
/// with depth writes off: the original has no depth at all here, but a beam already stops at
/// whatever it hit, so testing costs nothing visible and stops a shot fired past a ridge from being
/// painted over it.</para>
///
/// <para>Only the straight form is drawn. <c>FUN_0040b804</c> has a second branch for subtype ids 1
/// and 7 — ELF and ELF2 — which builds a jagged chain of nodes 1024 units apart, jittered by up to
/// 127 units on each axis, as a pair of points per node; its half of the paint method uses per-span
/// rasterizer state that has not been decoded, so those two weapons currently draw as straight beams
/// like the rest.</para>
/// </summary>
public sealed class BeamRenderer : IDisposable {
	private const string VertexShaderSource = """
		#version 330 core
		layout (location = 0) in vec3 aPosition;
		layout (location = 1) in vec3 aAxis;
		layout (location = 2) in float aSide;
		layout (location = 3) in float aProfile;

		uniform mat4 uView;
		uniform mat4 uProjection;
		uniform vec3 uCameraPosition;
		uniform vec2 uViewport;
		uniform float uHalfWidth;
		uniform float uMinimumHalfPixels;

		out float vProfile;

		void main() {
			// Perpendicular to the beam and to the line of sight, so the quad turns to face the
			// viewer as they move around it. Degenerate when looking straight down the beam, which is
			// exactly when the quad has no visible area anyway.
			vec3 toCamera = uCameraPosition - aPosition;
			vec3 perpendicular = cross(aAxis, toCamera);
			float length2 = length(perpendicular);
			perpendicular = length2 > 0.0 ? perpendicular / length2 : vec3(0.0);

			mat4 viewProjection = uProjection * uView;
			vec4 center = viewProjection * vec4(aPosition, 1.0);
			vec4 offset = viewProjection * vec4(perpendicular * uHalfWidth, 0.0);

			// The half-width floor, in the only units it can be stated in: how many pixels the offset
			// covers once divided through by this vertex's own w. See MinimumHalfPixels.
			float pixels = length((offset.xy / max(center.w, 0.0001)) * 0.5 * uViewport);
			if (pixels > 0.0 && pixels < uMinimumHalfPixels) {
				offset *= uMinimumHalfPixels / pixels;
			}

			vProfile = aProfile;
			gl_Position = center + offset * aSide;
		}
		""";

	private const string FragmentShaderSource = """
		#version 330 core
		in float vProfile;

		uniform sampler2D uProfileTexture;

		out vec4 FragColor;

		void main() {
			// Straight texel copy, which is all mode 0's span routine does — no tint, no shade level,
			// no blending. See BeamAppearance.
			FragColor = vec4(texture(uProfileTexture, vec2(0.5, vProfile)).rgb, 1.0);
		}
		""";

	/// <summary>
	/// <c>FUN_0040bc14</c>'s <c>if (halfWidth &lt; 2) halfWidth = 2</c>, applied to each end
	/// independently. It floors the <i>half</i>-width — the value is what each screen point is stepped
	/// by in both directions along the perpendicular — so a beam never draws narrower than four
	/// pixels, not two.
	/// </summary>
	private const float MinimumHalfPixels = 2f;

	private readonly GL _gl;
	private readonly ShaderProgram _shader;
	private readonly BeamAppearance _appearance;
	private readonly Dictionary<int, GpuTexture> _profiles = new();
	private readonly uint _vertexArray;
	private readonly uint _vertexBuffer;
	private readonly BeamVertex[] _quad = new BeamVertex[6];

	public BeamRenderer(GL gl, BeamAppearance appearance) {
		_gl = gl;
		_appearance = appearance;
		_shader = new ShaderProgram(gl, VertexShaderSource, FragmentShaderSource);

		_vertexArray = _gl.GenVertexArray();
		_gl.BindVertexArray(_vertexArray);
		_vertexBuffer = _gl.GenBuffer();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

		unsafe {
			uint stride = (uint)sizeof(BeamVertex);
			_gl.EnableVertexAttribArray(0);
			_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
			_gl.EnableVertexAttribArray(1);
			_gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
			_gl.EnableVertexAttribArray(2);
			_gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, (void*)24);
			_gl.EnableVertexAttribArray(3);
			_gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, (void*)28);
		}

		_gl.BindVertexArray(0);
	}

	/// <summary>
	/// Draws every tracer in <paramref name="tracers"/> into the viewport already set by whoever drew
	/// the world — call this straight after <see cref="SceneRenderer.Render"/> for the same panel,
	/// passing that panel's own camera and pixel size.
	/// </summary>
	public void Render(Camera camera, IReadOnlyList<BeamTracer> tracers,
			int viewportWidth, int viewportHeight) {
		if (tracers.Count == 0) {
			return;
		}

		float aspect = (float)viewportWidth / Math.Max(viewportHeight, 1);
		var cameraPosition = WorldScale.ToRender(camera.Position);

		_shader.Use();
		_shader.SetMatrix("uView", camera.ViewMatrix);
		_shader.SetMatrix("uProjection", camera.ProjectionMatrix(aspect));
		_shader.SetVector3("uCameraPosition", cameraPosition);
		_shader.SetVector2("uViewport", new Vector2(viewportWidth, Math.Max(viewportHeight, 1)));
		_shader.SetFloat("uMinimumHalfPixels", MinimumHalfPixels);

		// Opaque, and no depth write: the original submits these polys with no z at all, and nothing
		// is drawn into the 3D view after them.
		_gl.DepthMask(false);

		_gl.BindVertexArray(_vertexArray);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

		foreach (var tracer in tracers) {
			Draw(tracer, camera.ViewMatrix, camera.NearPlane);
		}

		_gl.BindVertexArray(0);
		_gl.DepthMask(true);
	}

	private void Draw(BeamTracer tracer, Matrix4x4 view, float nearPlane) {
		int halfWidth = _appearance.HalfWidth(tracer.MissileId);
		if (halfWidth <= 0 || Profile(tracer.MissileId) is not { } profile) {
			return;
		}

		var start = WorldScale.ToRender(tracer.Start);
		var end = WorldScale.ToRender(tracer.End);
		var axis = end - start;
		if (axis.LengthSquared() <= 0f) {
			return;
		}

		axis = Vector3.Normalize(axis);

		if (!ClipToNearPlane(view, nearPlane, ref start, ref end)) {
			return;
		}

		// v runs across the width, from one edge of the profile to the other; nothing varies along
		// the beam's length, which is why there is no u.
		_quad[0] = new BeamVertex(start, axis, -1f, 0f);
		_quad[1] = new BeamVertex(start, axis, 1f, 1f);
		_quad[2] = new BeamVertex(end, axis, 1f, 1f);
		_quad[3] = _quad[0];
		_quad[4] = _quad[2];
		_quad[5] = new BeamVertex(end, axis, -1f, 0f);

		_shader.SetFloat("uHalfWidth", WorldScale.DistanceToRender(halfWidth));
		_shader.SetSamplerTexture("uProfileTexture", profile.Handle, 0);

		_gl.BufferData<BeamVertex>(BufferTargetARB.ArrayBuffer, _quad, BufferUsageARB.DynamicDraw);
		_gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
	}

	/// <summary>
	/// <c>FUN_0040bb4c</c>, the tracer's own near-plane clip, which the original runs on the pair of
	/// view-space points before it projects either. It matters more here than anywhere else in the
	/// renderer: a beam starts at a hardpoint that is often <i>behind</i> the eye node, and an endpoint
	/// behind the plane projects with a negative w — which puts the muzzle end of the quad on the
	/// wrong side of the screen rather than off it.
	///
	/// <para>The original clips whichever end is short and leaves the other, which is what this does;
	/// a pair with both ends behind the plane is dropped, as its <c>return 0</c> does.</para>
	/// </summary>
	private static bool ClipToNearPlane(Matrix4x4 view, float nearPlane, ref Vector3 start, ref Vector3 end) {
		// Render space is right-handed with the camera looking down -Z, so a point's depth in front
		// of the eye is the negated view-space z.
		float startDepth = -Vector3.Transform(start, view).Z;
		float endDepth = -Vector3.Transform(end, view).Z;

		if (startDepth < nearPlane && endDepth < nearPlane) {
			return false;
		}

		if (startDepth < nearPlane) {
			start = Vector3.Lerp(start, end, (nearPlane - startDepth) / (endDepth - startDepth));
		} else if (endDepth < nearPlane) {
			end = Vector3.Lerp(end, start, (nearPlane - endDepth) / (startDepth - endDepth));
		}

		return true;
	}

	/// <summary>
	/// The uploaded cross-section for one subtype id, built on first use. The profile is one texel
	/// wide and as tall as the source frame's row count, so it is sampled purely by v.
	/// </summary>
	private GpuTexture? Profile(int missileId) {
		if (_profiles.TryGetValue(missileId, out var cached)) {
			return cached;
		}

		var texels = _appearance.Profile(missileId);
		if (texels.IsEmpty) {
			return null;
		}

		var texture = new GpuTexture(_gl, texels, 1, texels.Length / 4);
		_profiles[missileId] = texture;
		return texture;
	}

	public void Dispose() {
		foreach (var texture in _profiles.Values) {
			texture.Dispose();
		}

		_profiles.Clear();
		_gl.DeleteBuffer(_vertexBuffer);
		_gl.DeleteVertexArray(_vertexArray);
		_shader.Dispose();
	}

	/// <param name="Position">The endpoint this vertex sits on, in render space.</param>
	/// <param name="Axis">The beam's unit direction, so the shader can build the perpendicular.</param>
	/// <param name="Side">-1 or +1: which way along that perpendicular this vertex is pushed.</param>
	/// <param name="Profile">Where across the cross-section this vertex samples, 0 or 1.</param>
	[StructLayout(LayoutKind.Sequential)]
	private readonly record struct BeamVertex(Vector3 Position, Vector3 Axis, float Side, float Profile);
}
