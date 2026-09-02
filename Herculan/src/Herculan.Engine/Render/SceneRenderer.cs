using System.Numerics;
using Herculan.Engine.Gl;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Render;

/// <summary>One mesh plus the transform that places it in the world.</summary>
public sealed class SceneItem {
	public SceneItem(GpuMesh mesh, Matrix4x4 transform, uint? textureHandle = null, bool fullbright = false) {
		Mesh = mesh;
		Transform = transform;
		TextureHandle = textureHandle;
		Fullbright = fullbright;
	}

	public GpuMesh Mesh { get; }

	/// <summary>Model-to-world transform, in render space.</summary>
	public Matrix4x4 Transform { get; set; }

	/// <summary>Optional texture for this item. If null, flat-shaded rendering is used.</summary>
	public uint? TextureHandle { get; set; }

	/// <summary>
	/// Whether the editor's measuring grid (<see cref="SceneRenderer.Grid"/>) is painted onto this
	/// item's surface. Set on the terrain only — the grid is a reading of the ground, and running it
	/// over the machines standing on it would just be stripes. Has no effect unless the renderer was
	/// built with the grid compiled in.
	/// </summary>
	public bool ShowGrid { get; set; }

	/// <summary>
	/// Whether this item's <b>textured</b> surfaces skip the theater ramp entirely and draw the
	/// palette straight through — no light term and no shade row. It is a property of the draw, not
	/// of the shape: <c>Bullet_Draw</c> (<c>0040a120</c>) zeroes the ramp's row count for the
	/// duration of a projectile's shape render, which switches <c>TSTexture4Poly_Render</c>
	/// (<c>00474e9c</c>) to a plain texture copy, and restores it afterwards. The same vtable slot
	/// draws launcher rounds, so both classes set it — see <see cref="PaletteRampTable.FullbrightRow"/>.
	///
	/// <para>Untextured surfaces are unaffected, as they are in the original: a projectile's
	/// <c>TSSolidPoly</c> geometry was never lit to begin with.</para>
	/// </summary>
	public bool Fullbright { get; set; }
}

/// <summary>
/// Tunables for the grid <see cref="SceneItem.ShowGrid"/> paints onto a surface. Spacing is in render
/// units (metres) and widths are in screen pixels, held constant at any distance or view angle.
/// </summary>
public sealed class TerrainGridOverlay {
	public Vector3 Color { get; set; } = new(0.72f, 0.76f, 0.82f);

	public float SpacingMeters { get; set; } = 10f;

	public float Opacity { get; set; } = 0.35f;

	public float MajorLineOpacity { get; set; } = 0.55f;

	public float LineWidthPixels { get; set; } = 1.5f;

	public int MajorLineEvery { get; set; } = 10;

	public float MajorLineWidthScale { get; set; } = 2f;

	/// <summary>On-screen cell size, in pixels, below which a set of lines fades out.</summary>
	public float MinCellPixels { get; set; } = 6f;

	/// <summary>Distance from the camera, in render units (metres), at which the grid starts fading.</summary>
	public float FadeStartMeters { get; set; } = 450f;

	/// <summary>Distance at which it has faded out completely.</summary>
	public float FadeEndMeters { get; set; } = 1000f;
}

/// <summary>
/// Draws a list of <see cref="SceneItem"/>s from a <see cref="Camera"/> with one directional light
/// plus ambient. Deliberately minimal — the first milestone's rendering goal is a correct,
/// legible view of real game geometry, not a material system.
/// </summary>
public sealed class SceneRenderer : IDisposable {
	private readonly GL _gl;
	private readonly ShaderProgram _shader;
	private readonly ShaderProgram _skyShader;
	private readonly uint _skyVertexArray;
	private readonly bool _hasGrid;
	private GpuTexture? _shadeRampTexture;
	private GpuTexture? _paletteRampTexture;
	private int _paletteRampRows;
	private int _paletteRampShadeRows;

	/// <param name="editorGrid">
	/// Compiles the measuring grid into the scene program, so <see cref="SceneItem.ShowGrid"/> and
	/// <see cref="Grid"/> do something. Off by default, and the simulator leaves it off: the grid is
	/// a tool, and this way none of it — not a uniform, not a varying, not a branch — reaches the
	/// program the game is drawn with.
	/// </param>
	public SceneRenderer(GL gl, bool editorGrid = false) {
		_gl = gl;
		_hasGrid = editorGrid;
		_shader = editorGrid
			? ShaderProgram.Load(gl, "Scene.glsl", "EDITOR_GRID")
			: ShaderProgram.Load(gl, "Scene.glsl");
		_skyShader = ShaderProgram.Load(gl, "Sky.glsl");
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
		_paletteRampShadeRows = table?.ShadeRows ?? 0;
	}

	/// <summary>
	/// What distant geometry fades into. The theater's own ramp knows this colour — see
	/// <see cref="Content.ShadeRamp.FogColor"/> — and <see cref="Scene.Atmosphere"/> supplies it.
	/// The value here is only the fallback for a mission whose ramp did not load.
	/// </summary>
	public Vector3 FogColor { get; set; } = new(0.55f, 0.60f, 0.68f);

	/// <summary>
	/// The theater's sky, sixteen banded colours out of its own palette — see
	/// <see cref="Content.SkyGradient"/>. Null draws a flat <see cref="SkyColor"/> instead.
	/// </summary>
	public Content.SkyGradient? Sky { get; set; }

	/// <summary>
	/// Flat fallback sky, used to clear the framebuffer and drawn instead of the gradient when
	/// <see cref="Sky"/> is null.
	///
	/// <para>Deliberately <b>not</b> <see cref="FogColor"/>, which it used to be — the sky and the
	/// colour distant terrain fades into are separate things in the original, even though its palette
	/// makes them meet: the sky's bottom band and the ramp's fog colour are neighbouring entries of
	/// one gradient.</para>
	/// </summary>
	public Vector3 SkyColor { get; set; } = new(0.55f, 0.60f, 0.68f);

	/// <summary>
	/// Distance in render units (metres) at which fog starts — half the zone's visibility range,
	/// which is where the original's ramp fade begins. See <see cref="Scene.Atmosphere"/>.
	/// </summary>
	public float FogStart { get; set; } = 900f;

	/// <summary>Distance in render units at which fog is total — the zone's visibility range.</summary>
	public float FogEnd { get; set; } = 9000f;

	/// <summary>
	/// Whether distance fog is applied at all. On by default, and the simulator never turns it off:
	/// the fade is the original's own behaviour rather than an effect. It exists for tools — the
	/// mission editor lets it be switched off so distant geometry stays legible while placing things
	/// out past the zone's visibility range.
	/// </summary>
	public bool FogEnabled { get; set; } = true;

	/// <summary>
	/// Fog bounds far enough out that the shader's fade fraction clamps to zero everywhere, which is
	/// how <see cref="FogEnabled"/> is spent — no shader branch and no second program.
	/// </summary>
	private const float FogDisabledDistance = 1e9f;

	/// <summary>
	/// Settings for the grid painted onto any item whose <see cref="SceneItem.ShowGrid"/> is set.
	/// Ignored unless this renderer was built with the grid compiled in.
	/// </summary>
	public TerrainGridOverlay Grid { get; } = new();

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
		_shader.SetVector3("uFogColor", FogColor);
		_shader.SetFloat("uFogStart", FogEnabled ? FogStart : FogDisabledDistance);
		_shader.SetFloat("uFogEnd", FogEnabled ? FogEnd : FogDisabledDistance);

		if (_hasGrid) {
			_shader.SetVector3("uGridColor", Grid.Color);
			_shader.SetFloat("uGridSpacing", MathF.Max(Grid.SpacingMeters, 0.001f));
			_shader.SetFloat("uGridMinorOpacity", Grid.Opacity);
			_shader.SetFloat("uGridMajorOpacity", Grid.MajorLineOpacity);
			_shader.SetFloat("uGridLineWidthPixels", MathF.Max(Grid.LineWidthPixels, 0.1f));
			_shader.SetFloat("uGridMajorEvery", System.Math.Max(Grid.MajorLineEvery, 1));
			_shader.SetFloat("uGridMajorWidthScale", MathF.Max(Grid.MajorLineWidthScale, 1f));
			_shader.SetFloat("uGridMinCellPixels", MathF.Max(Grid.MinCellPixels, 0.1f));
			_shader.SetFloat("uGridFadeStart", Grid.FadeStartMeters);
			_shader.SetFloat("uGridFadeEnd", MathF.Max(Grid.FadeEndMeters, Grid.FadeStartMeters + 0.001f));
		}

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
			_shader.SetFloat("uShadeLevels", _paletteRampShadeRows);
			_shader.SetFloat("uPaletteRampRows", _paletteRampRows);
		} else {
			_shader.SetInt("uPaletteRampEnabled", 0);
		}

		foreach (var item in items) {
			_shader.SetMatrix("uModel", item.Transform);
			if (_hasGrid) {
				_shader.SetInt("uGridEnabled", item.ShowGrid ? 1 : 0);
			}

			_shader.SetInt("uFullbright", item.Fullbright ? 1 : 0);

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
