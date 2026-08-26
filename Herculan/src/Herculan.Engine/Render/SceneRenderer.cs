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
		layout (location = 5) in float aUnlit;
		layout (location = 6) in float aShade;

		uniform mat4 uModel;
		uniform mat4 uView;
		uniform mat4 uProjection;

		out vec3 vNormal;
		out vec3 vColor;
		out vec2 vUV;
		out float vTextured;
		out float vUnlit;
		out float vShade;
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
			vUnlit = aUnlit;
			vShade = aShade;
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
		in float vUnlit;
		in float vShade;
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
			// A flat solid poly is not lit at all in the original — its colour already came out of the
			// theater ramp at a fixed shade — so it takes no runtime light term. See MeshVertex.Unlit.
			// vShade is the other half of that: a multiplier for surfaces the original shades once,
			// ahead of time, and stores — terrain, whose per-cell shade bytes are computed at zone
			// load by Terrain_BuildSurface. It is 1.0 everywhere else.
			float light = vUnlit > 0.5 ? 1.0 : (0.35 + 0.65 * lambert);
			vec3 lit = baseColor * light * vShade;

			// Distance haze, so a 10 km zone reads as depth instead of a flat wall of terrain.
			float haze = clamp((vViewDistance - uHazeStart) / max(uHazeEnd - uHazeStart, 0.001), 0.0, 1.0);
			FragColor = vec4(mix(lit, uHazeColor, haze), 1.0);
		}
		""";

	/// <summary>
	/// The sky pass. A single full-viewport triangle built from <c>gl_VertexID</c> alone, so it needs
	/// no vertex buffer — only a bound (empty) VAO, which core-profile GL still requires.
	/// </summary>
	private const string SkyVertexShaderSource = """
		#version 330 core
		void main() {
			// (-1,-1), (3,-1), (-1,3): one oversized triangle covering the whole clip rect.
			vec2 corner = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
			gl_Position = vec4(corner * 2.0 - 1.0, 0.0, 1.0);
		}
		""";

	/// <summary>
	/// Bands the sky by distance above the horizon in pixels, which is how the original's raster sky
	/// is built — see <see cref="Content.SkyGradient"/>. <c>uHorizonY</c> is the horizon's own window
	/// y, so the gradient rides the camera's pitch instead of being pinned to the middle of the view.
	/// </summary>
	private const string SkyFragmentShaderSource = """
		#version 330 core
		uniform vec3 uBands[16];
		uniform float uHorizonY;
		uniform float uBandHeight;

		out vec4 FragColor;

		void main() {
			// Band 0 sits on the horizon and they climb from there; below it, the bottom band, which
			// terrain covers anyway except where the view looks down past the far edge of the world.
			float above = (gl_FragCoord.y - uHorizonY) / uBandHeight;
			int band = int(clamp(floor(above), 0.0, 15.0));

			// uBands is ordered zenith-first, so the horizon is the last entry and bands count back.
			FragColor = vec4(uBands[15 - band], 1.0);
		}
		""";

	private readonly GL _gl;
	private readonly ShaderProgram _shader;
	private readonly ShaderProgram _skyShader;
	private readonly uint _skyVertexArray;

	public SceneRenderer(GL gl) {
		_gl = gl;
		_shader = new ShaderProgram(gl, VertexShaderSource, FragmentShaderSource);
		_skyShader = new ShaderProgram(gl, SkyVertexShaderSource, SkyFragmentShaderSource);
		_skyVertexArray = gl.GenVertexArray();

		_gl.Enable(EnableCap.DepthTest);
		_gl.DepthFunc(DepthFunction.Less);
	}

	/// <summary>
	/// Direction the sun's light travels, in render space — <see cref="MissionSun.Direction"/>.
	/// Settable so a tool can override it; every mission uses the one hardcoded sun.
	/// </summary>
	public Vector3 LightDirection { get; set; } = MissionSun.Direction;

	/// <summary>
	/// What distant geometry fades into. The theater's own ramp knows this colour — see
	/// <see cref="Content.ShadeRamp.FogColor"/> — and <see cref="Scene.Atmosphere"/> supplies it.
	/// The value here is only the fallback for a mission whose ramp did not load.
	/// </summary>
	public Vector3 HazeColor { get; set; } = new(0.55f, 0.60f, 0.68f);

	/// <summary>
	/// The theater's sky, sixteen banded colours out of its own palette — see
	/// <see cref="Content.SkyGradient"/>. Null draws a flat <see cref="SkyColor"/> instead.
	/// </summary>
	public Content.SkyGradient? Sky { get; set; }

	/// <summary>
	/// Flat fallback sky, used to clear the framebuffer and drawn instead of the gradient when
	/// <see cref="Sky"/> is null.
	///
	/// <para>Deliberately <b>not</b> <see cref="HazeColor"/>, which it used to be — the sky and the
	/// colour distant terrain fades into are separate things in the original, even though its palette
	/// makes them meet: the sky's bottom band and the ramp's fog colour are neighbouring entries of
	/// one gradient.</para>
	/// </summary>
	public Vector3 SkyColor { get; set; } = new(0.55f, 0.60f, 0.68f);

	/// <summary>
	/// Distance in render units (metres) at which haze starts — half the zone's visibility range,
	/// which is where the original's ramp fade begins. See <see cref="Scene.Atmosphere"/>.
	/// </summary>
	public float HazeStart { get; set; } = 900f;

	/// <summary>Distance in render units at which haze is total — the zone's visibility range.</summary>
	public float HazeEnd { get; set; } = 9000f;

	/// <summary>
	/// Clears the whole framebuffer once per frame. Split out from <see cref="Render"/> so a host can
	/// draw several panels (Milestone 8's three-panel cockpit view) into disjoint viewport sub-rects
	/// of the same frame without each call wiping the ones already drawn — call this once, then
	/// <see cref="Render"/> once per panel.
	/// </summary>
	public void Clear() {
		_gl.ClearColor(SkyColor.X, SkyColor.Y, SkyColor.Z, 1f);
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

		DrawSky(camera, viewportY, viewportWidth, viewportHeight);

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

	/// <summary>
	/// Paints the panel's sky before any geometry goes into it. Depth-testing and depth-writing are
	/// both off, so this is a background fill rather than something at the far plane — the scene draws
	/// straight over it and nothing needs the far plane to sit beyond the sky.
	/// </summary>
	private void DrawSky(Camera camera, int viewportY, int viewportWidth, int viewportHeight) {
		if (Sky is not { } sky) {
			return;
		}

		_skyShader.Use();
		for (int band = 0; band < Content.SkyGradient.BandCount; band++) {
			_skyShader.SetVector3($"uBands[{band}]", sky.Bands[band]);
		}

		_skyShader.SetFloat("uHorizonY", HorizonWindowY(camera, viewportY, viewportWidth, viewportHeight));
		_skyShader.SetFloat("uBandHeight", Content.SkyGradient.BandHeightFor(viewportHeight));

		_gl.Disable(EnableCap.DepthTest);
		_gl.DepthMask(false);
		_gl.BindVertexArray(_skyVertexArray);
		_gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
		_gl.BindVertexArray(0);
		_gl.DepthMask(true);
		_gl.Enable(EnableCap.DepthTest);
	}

	/// <summary>
	/// Where the horizon lands in window coordinates (the frame <c>gl_FragCoord</c> is in, so measured
	/// from the bottom of the whole framebuffer, not of the panel).
	///
	/// <para>Found by projecting the camera's own forward direction flattened onto the ground plane —
	/// a direction, not a point, so it is the vanishing point of every horizontal line and therefore
	/// the horizon itself. It has to be computed rather than assumed to be the middle of the view:
	/// pitch moves it, and so does <see cref="Camera.PrincipalPoint"/>, which the cockpit sets well
	/// above centre.</para>
	/// </summary>
	private static float HorizonWindowY(Camera camera, int viewportY, int viewportWidth, int viewportHeight) {
		int height = System.Math.Max(viewportHeight, 1);

		Vector3 forward = camera.Forward;
		var flattened = new Vector3(forward.X, 0f, forward.Z);

		// Looking straight up or down leaves no horizontal component to project. Nothing in the game
		// does, but a free camera can, and the fallback keeps the sky drawable rather than NaN.
		if (flattened.LengthSquared() < 1e-9f) {
			return viewportY + height * 0.5f;
		}

		var viewProjection = camera.ViewMatrix * camera.ProjectionMatrix((float)viewportWidth / height);
		var clip = Vector4.Transform(new Vector4(Vector3.Normalize(flattened), 0f), viewProjection);
		if (MathF.Abs(clip.W) < 1e-6f) {
			return viewportY + height * 0.5f;
		}

		return viewportY + (clip.Y / clip.W * 0.5f + 0.5f) * height;
	}

	public void Dispose() {
		_shader.Dispose();
		_skyShader.Dispose();
		_gl.DeleteVertexArray(_skyVertexArray);
	}
}
