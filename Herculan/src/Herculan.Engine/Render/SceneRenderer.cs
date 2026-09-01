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
		layout (location = 7) in float aShadeRamp;
		layout (location = 8) in vec3 aFaceNormal;
		layout (location = 9) in float aUvWeight;

		uniform mat4 uModel;
		uniform mat4 uView;
		uniform mat4 uProjection;
		uniform vec3 uLightDirection;

		out vec3 vColor;
		out vec2 vUV;
		out float vUvWeight;
		out float vTextured;
		out float vUnlit;
		out float vShade;
		out float vShadeRamp;
		out float vLightShade;
		out float vViewDistance;

		void main() {
			vec4 worldPosition = uModel * vec4(aPosition, 1.0);
			vec4 viewPosition = uView * worldPosition;

			// Normals only ever see rotation and uniform scale here, so the plain model matrix is
			// enough; a normal matrix becomes necessary if non-uniform scaling ever appears.
			// aNormal is the shape's own per-corner normal, which for a TSGouraudPoly differs between
			// the three corners; aFaceNormal is the flat one they share.
			vec3 normal = normalize(mat3(uModel) * aNormal);
			vec3 faceNormal = normalize(mat3(uModel) * aFaceNormal);

			// The face is turned to meet the eye before it is lit, exactly as the original does it:
			// every poly renderer runs TSPoly_FrontBackVisibilityTest (0048c620) on the poly's own
			// stored normal and centre, and negates the poly's normals when the answer is "back". The
			// test is on the FACE normal so all three corners agree — in view space the camera is the
			// origin, so a face is turned away when its normal and its position point the same way.
			float sideSign = dot(mat3(uView) * faceNormal, viewPosition.xyz) > 0.0 ? -1.0 : 1.0;

			// The shade byte Light_ComputeShadeForFace (0048bedc) gives this corner:
			//     t = (dot - 0x400000) >> 1;  if (t < 0) shade -= (0x100 * t) >> 22
			// which with normals at length 0x800 and the sun at 0x1000/0x100 collapses to
			// clamp(128 + 256 * facing). See MissionSun.ShadeForFace.
			//
			// Computed HERE, per vertex, and interpolated — which is what makes a TSGouraudPoly
			// Gouraud. TSGouraudPoly_Render (004755c8) calls the light function once per vertex,
			// walking NormalList and VertexList in step, stashes the bytes and lets the span routine
			// interpolate between them. Doing it per fragment from an interpolated normal instead is
			// Phong, and it differs wherever the clamp bites: the original clamps each corner first
			// and then interpolates, so a corner that bottoms out at 0 still ramps linearly to its
			// neighbour rather than holding a dead flat region. Flat polys are unaffected — their
			// three corners share a normal, so this is constant across the face.
			float facing = dot(normal * sideSign, -normalize(uLightDirection));
			vLightShade = clamp(128.0 + 256.0 * facing, 0.0, 255.0);

			vColor = aColor;
			vUV = aUV;
			vUvWeight = aUvWeight;
			vTextured = aTextured;
			vUnlit = aUnlit;
			vShade = aShade;
			vShadeRamp = aShadeRamp;
			vViewDistance = length(viewPosition.xyz);

			gl_Position = uProjection * viewPosition;
		}
		""";

	private const string FragmentShaderSource = """
		#version 330 core
		in vec3 vColor;
		in vec2 vUV;
		in float vUvWeight;
		in float vTextured;
		in float vUnlit;
		in float vShade;
		in float vShadeRamp;
		in float vLightShade;
		in float vViewDistance;

		uniform vec3 uHazeColor;
		uniform float uHazeStart;
		uniform float uHazeEnd;
		uniform sampler2D uTexture;
		uniform bool uTextureEnabled;
		uniform sampler2D uShadeRampTable;
		uniform bool uShadeRampEnabled;
		uniform sampler2D uPaletteRamp;
		uniform bool uPaletteRampEnabled;
		uniform float uShadeLevels;

		out vec4 FragColor;

		void main() {
			// Interpolated from the three corners' own shade bytes — the vertex shader computes them,
			// which is what makes a TSGouraudPoly Gouraud rather than Phong. See there.
			float shade = clamp(vLightShade, 0.0, 255.0);

			// A surface the original shades once ahead of time and stores carries its own byte rather
			// than one computed here — terrain, whose per-cell shades Terrain_BuildSurface bakes at
			// zone load and Terrain_DrawCellQuad hands straight to the span setup.
			if (vUnlit > 0.5) {
				shade = clamp(vShade, 0.0, 255.0);
			}

			// Per-vertex, not per-draw: a mesh mixes textured and fallback-coloured triangles, and
			// vTextured is flat across each triangle so this never interpolates between the two.
			vec3 baseColor = vColor;
			bool textured = uTextureEnabled && vTextured > 0.5;
			bool texturedExact = false;
			if (textured) {
				// A textured quad's corners carry homogeneous UVs so that both of its triangles
				// resolve to the one projective map the original's quad rasterizer walks — see
				// MeshVertex.UvWeight. Everything else carries a plain coordinate and weight 0.
				vec2 uv = vUvWeight > 0.0 ? vUV / vUvWeight : vUV;

				vec4 texel = texture(uTexture, uv);

				// Palette index 0 decodes to alpha 0 in a bank whose frames are cutouts — the lattice
				// girders on a structure. The original's span routine skips that index rather than
				// writing it, so the hole is a hole and not a black polygon. See
				// SceneModelLibrary.LoadAtlas.
				if (texel.a < 0.5) {
					discard;
				}

				// The exact indexed path: uTexture's red channel is the texel's PALETTE INDEX, not its
				// colour, and the original's span writes rampRow(shade)[index] — the light level picks
				// a row of the theater .RMP and the texel picks the column. uPaletteRamp is that table
				// expanded through the palette, so this is one sample and no approximation. The row is
				// Raster_ShadeRampRow's own selection, floor(shade * (levels - 1) / 256).
				if (uPaletteRampEnabled) {
					float row = clamp(floor(shade * (uShadeLevels - 1.0) / 256.0), 0.0, uShadeLevels - 1.0);
					baseColor = texture(uPaletteRamp,
						vec2((floor(texel.r * 255.0 + 0.5) + 0.5) / 256.0, (row + 0.5) / uShadeLevels)).rgb;
					texturedExact = true;
				} else {
					baseColor = texel.rgb;
				}
			}

			vec3 lit;
			if (texturedExact) {
				lit = baseColor;
			} else if (uShadeRampEnabled && !textured && vShadeRamp >= 0.0) {
				// A lit flat poly (TSShadedPoly and its Gouraud sibling) has no colour for a light
				// term to multiply — its surface names a material ramp and the face's shade byte
				// picks a step along that ramp, and that lookup IS the shading. uShadeRampTable is
				// the whole ramp-by-shade grid, so this is one sample rather than the original's two
				// table reads.
				// The row is the surface's ramp number, biased into the Gouraud half of the table for
				// a TSGouraudPoly — see SurfaceRampTable.GouraudRowOffset.
				lit = texture(uShadeRampTable,
					vec2((floor(shade) + 0.5) / 256.0, (vShadeRamp + 0.5) / 512.0)).rgb;
			} else {
				// What is left is untextured and names no material ramp: a plain TSSolidPoly, whose
				// colour already came out of the theater ramp at a fixed shade and is never lit, or a
				// fallback colour for a surface nothing could resolve. Both are final as they stand.
				lit = baseColor;
			}

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
	private GpuTexture? _shadeRampTexture;
	private GpuTexture? _paletteRampTexture;
	private int _paletteRampRows;

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
	/// Installs the theater's shaded-surface colours, which every <c>TSShadedPoly</c> in the scene is
	/// drawn through — see <see cref="SurfaceRampTable"/>. Passing null (a theater whose palette
	/// carries no ramp table) leaves those surfaces on the mesh builder's fallback colour instead.
	///
	/// <para>Call once per loaded mission, before the first <see cref="Render"/>. Uploading again
	/// replaces the previous table.</para>
	/// </summary>
	public void SetShadeRamps(SurfaceRampTable? table) {
		_shadeRampTexture?.Dispose();
		_shadeRampTexture = table == null
			? null
			: new GpuTexture(_gl, table.Pixels, SurfaceRampTable.Width, SurfaceRampTable.Height);
	}

	/// <summary>
	/// Installs the theater's palette-by-shade-row table, which every <b>lit textured</b> surface in
	/// the scene is drawn through — see <see cref="PaletteRampTable"/>. Passing null leaves those
	/// surfaces sampling their expanded colour unlit.
	///
	/// <para>Call once per loaded mission alongside <see cref="SetShadeRamps"/>. A caller that
	/// installs this <b>must</b> bind atlases built from
	/// <see cref="TextureAtlas.IndexPixels"/> rather than <see cref="TextureAtlas.Pixels"/>: the
	/// shader reads the red channel as a palette index once this is set.</para>
	/// </summary>
	public void SetPaletteRamp(PaletteRampTable? table) {
		_paletteRampTexture?.Dispose();
		_paletteRampTexture = table == null
			? null
			: new GpuTexture(_gl, table.Pixels, PaletteRampTable.Width, table.Height);
		_paletteRampRows = table?.Height ?? 0;
	}

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

		// Unit 1, so a per-item atlas can keep unit 0 without rebinding this every draw.
		if (_shadeRampTexture != null) {
			_shader.SetSamplerTexture("uShadeRampTable", _shadeRampTexture.Handle, 1);
			_shader.SetInt("uShadeRampEnabled", 1);
		} else {
			_shader.SetInt("uShadeRampEnabled", 0);
		}

		if (_paletteRampTexture != null) {
			_shader.SetSamplerTexture("uPaletteRamp", _paletteRampTexture.Handle, 2);
			_shader.SetInt("uPaletteRampEnabled", 1);
			_shader.SetFloat("uShadeLevels", _paletteRampRows);
		} else {
			_shader.SetInt("uPaletteRampEnabled", 0);
		}

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
		_shadeRampTexture?.Dispose();
		_paletteRampTexture?.Dispose();
		_gl.DeleteVertexArray(_skyVertexArray);
	}
}
