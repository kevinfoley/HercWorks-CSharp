using System.Numerics;
using Herculan.Engine.Gl;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Render;

/// <summary>One mesh plus the transform that places it in the world.</summary>
public sealed class SceneItem {
	public SceneItem(GpuMesh mesh, Matrix4x4 transform, uint? textureHandle = null) {
		Mesh = mesh;
		Transform = transform;
		TextureHandle = textureHandle;
	}

	public GpuMesh Mesh { get; }

	/// <summary>Model-to-world transform, in render space.</summary>
	public Matrix4x4 Transform { get; set; }

	/// <summary>Optional texture for this item. If null, flat-shaded rendering is used.</summary>
	public uint? TextureHandle { get; set; }
}

/// <summary>
/// Draws a list of <see cref="SceneItem"/>s from a <see cref="Camera"/> with one directional light
/// plus ambient. Deliberately minimal — the first milestone's rendering goal is a correct,
/// legible view of real game geometry, not a material system.
/// </summary>
public sealed class SceneRenderer : IDisposable {
	private const string VertexShaderSource = """
		#version 330 core
		layout (location = 0) in vec3 aPosition;
		layout (location = 1) in vec3 aNormal;
		layout (location = 2) in vec3 aColor;
		layout (location = 3) in vec2 aUV;
		layout (location = 4) in float aTextured;

		uniform mat4 uModel;
		uniform mat4 uView;
		uniform mat4 uProjection;

		out vec3 vNormal;
		out vec3 vColor;
		out vec2 vUV;
		out float vTextured;
		out float vViewDistance;

		void main() {
			vec4 worldPosition = uModel * vec4(aPosition, 1.0);
			vec4 viewPosition = uView * worldPosition;

			// Normals only ever see rotation and uniform scale here, so the plain model matrix is
			// enough; a normal matrix becomes necessary if non-uniform scaling ever appears.
			vNormal = normalize(mat3(uModel) * aNormal);
			vColor = aColor;
			vUV = aUV;
			vTextured = aTextured;
			vViewDistance = length(viewPosition.xyz);

			gl_Position = uProjection * viewPosition;
		}
		""";

	private const string FragmentShaderSource = """
		#version 330 core
		in vec3 vNormal;
		in vec3 vColor;
		in vec2 vUV;
		in float vTextured;
		in float vViewDistance;

		uniform vec3 uLightDirection;
		uniform vec3 uHazeColor;
		uniform float uHazeStart;
		uniform float uHazeEnd;
		uniform sampler2D uTexture;
		uniform bool uTextureEnabled;

		out vec4 FragColor;

		void main() {
			// Two-sided lighting: DTS geometry is not reliably wound, and nothing is backface-culled,
			// so shade by the absolute facing rather than letting flipped triangles go black.
			float lambert = abs(dot(normalize(vNormal), normalize(-uLightDirection)));

			// Per-vertex, not per-draw: a mesh mixes textured and fallback-coloured triangles, and
			// vTextured is flat across each triangle so this never interpolates between the two.
			vec3 baseColor = vColor;
			if (uTextureEnabled && vTextured > 0.5) {
				baseColor = texture(uTexture, vUV).rgb;
			}
			vec3 lit = baseColor * (0.35 + 0.65 * lambert);

			// Distance haze, so a 10 km zone reads as depth instead of a flat wall of terrain.
			float haze = clamp((vViewDistance - uHazeStart) / max(uHazeEnd - uHazeStart, 0.001), 0.0, 1.0);
			FragColor = vec4(mix(lit, uHazeColor, haze), 1.0);
		}
		""";

	private readonly GL _gl;
	private readonly ShaderProgram _shader;

	public SceneRenderer(GL gl) {
		_gl = gl;
		_shader = new ShaderProgram(gl, VertexShaderSource, FragmentShaderSource);

		_gl.Enable(EnableCap.DepthTest);
		_gl.DepthFunc(DepthFunction.Less);
	}

	/// <summary>
	/// Direction the sun's light travels, in render space.
	///
	/// <para>Every mission gets the identical hardcoded directional "sun" — see
	/// docs/formats/dts-texture-binding.md's "Flat-shaded lighting" section. Derived from
	/// <c>Light_CreateMissionSun</c>'s own math: <c>rotate((0,4096,0), eulerMatrix(-6000,0,21000))</c>
	/// in DBSIM's Z-up world space (angle unit <c>raw/65536*360</c> degrees), computed by
	/// <see cref="ComputeSunDirection"/>. The 3-axis rotation order was not independently verified
	/// against the exe's fixed-point trig, so this is a reasonable reading (intrinsic X then Z; Y is a
	/// no-op since its angle is 0), not a byte-exact one — it replaces an earlier, purely eyeballed
	/// guess with a value at least derived from the real constants.</para>
	/// </summary>
	public Vector3 LightDirection { get; set; } = ComputeSunDirection();

	private static Vector3 ComputeSunDirection() {
		const float RadiansPerRawUnit = MathF.PI * 2f / 65536f;
		float xRadians = -6000f * RadiansPerRawUnit;
		float zRadians = 21000f * RadiansPerRawUnit;

		var qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, xRadians);
		var qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, zRadians);
		Vector3 worldDirection = Vector3.Transform(new Vector3(0f, 1f, 0f), qz * qx);

		// DBSIM world is Z-up with X/Y the ground plane; render space is Y-up — same mapping as
		// WorldScale.ToRender.
		Vector3 renderDirection = new(worldDirection.X, worldDirection.Z, -worldDirection.Y);
		return Vector3.Normalize(renderDirection);
	}

	/// <summary>Horizon/haze colour; also what the frame is cleared to, so distant terrain melts into it.</summary>
	public Vector3 HazeColor { get; set; } = new(0.55f, 0.60f, 0.68f);

	/// <summary>Distance in render units at which haze starts.</summary>
	public float HazeStart { get; set; } = 900f;

	/// <summary>Distance in render units at which haze is total.</summary>
	public float HazeEnd { get; set; } = 9000f;

	/// <summary>
	/// Clears the whole framebuffer once per frame. Split out from <see cref="Render"/> so a host can
	/// draw several panels (Milestone 8's three-panel cockpit view) into disjoint viewport sub-rects
	/// of the same frame without each call wiping the ones already drawn — call this once, then
	/// <see cref="Render"/> once per panel.
	/// </summary>
	public void Clear() {
		_gl.ClearColor(HazeColor.X, HazeColor.Y, HazeColor.Z, 1f);
		_gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
	}

	/// <summary>
	/// Draws one pass into the viewport sub-rect (<paramref name="viewportX"/>, <paramref
	/// name="viewportY"/>, <paramref name="viewportWidth"/>, <paramref name="viewportHeight"/>) —
	/// origin bottom-left in GL viewport convention, matching <c>GL.Viewport</c>'s own. Does not clear
	/// — call <see cref="Clear"/> once per frame before the first panel. Setting `gl.Viewport` per call
	/// is what keeps each panel's rasterization confined to its own sub-rect with no scissor-rect
	/// bookkeeping needed.
	/// </summary>
	public void Render(Camera camera, IEnumerable<SceneItem> items,
			int viewportX, int viewportY, int viewportWidth, int viewportHeight) {
		_gl.Viewport(viewportX, viewportY, (uint)System.Math.Max(viewportWidth, 1), (uint)System.Math.Max(viewportHeight, 1));

		_shader.Use();
		_shader.SetMatrix("uView", camera.ViewMatrix);
		_shader.SetMatrix("uProjection", camera.ProjectionMatrix((float)viewportWidth / System.Math.Max(viewportHeight, 1)));
		_shader.SetVector3("uLightDirection", LightDirection);
		_shader.SetVector3("uHazeColor", HazeColor);
		_shader.SetFloat("uHazeStart", HazeStart);
		_shader.SetFloat("uHazeEnd", HazeEnd);

		foreach (var item in items) {
			_shader.SetMatrix("uModel", item.Transform);

			// Bind texture if available, otherwise use flat shading.
			if (item.TextureHandle.HasValue) {
				_shader.SetSamplerTexture("uTexture", item.TextureHandle.Value, 0);
				_shader.SetInt("uTextureEnabled", 1);
			} else {
				_shader.SetInt("uTextureEnabled", 0);
			}

			item.Mesh.Draw();
		}
	}

	public void Dispose() => _shader.Dispose();
}
