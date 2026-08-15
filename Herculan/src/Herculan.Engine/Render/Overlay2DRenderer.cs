using System.Numerics;
using HercWorks.Core.Data.File.Gau;
using HercWorks.Core.Data.Struct;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Render;

/// <summary>
/// Draws one panel's cockpit-art quad — at its own native aspect ratio, never stretched — plus, for
/// the center panel only, GAU HUD widgets as flat-color placeholder shapes. See
/// docs/formats/cockpit-hud.md and docs/engine/planning.md's Milestone 8.
///
/// <para>Orthographic, own minimal shader (position/UV/color, no lighting) — same precedent as
/// <see cref="WireframeRenderer"/> using its own shader rather than forcing 2D content through
/// <see cref="SceneRenderer"/>'s lit-3D layout. Disables depth test and enables alpha blending for
/// its own draw, then restores both — the 3D pass (<see cref="SceneRenderer"/>) assumes depth test is
/// always on, and nothing else in the engine uses blending.</para>
///
/// <para>Widgets are outline/fill placeholder geometry, not real art — this milestone has no HUD
/// font/icon assets decoded yet (see <see cref="GAUFile"/>'s own doc comment). They exist to prove
/// the coordinate mapping (<see cref="CockpitArt.GauToPixelScale"/>) is right, verifiable by
/// comparing a captured frame against the real cockpit console art.</para>
/// </summary>
public sealed class Overlay2DRenderer : IDisposable {
	private const string VertexShaderSource = """
		#version 330 core
		layout (location = 0) in vec2 aPosition;
		layout (location = 1) in vec2 aUV;
		layout (location = 2) in vec3 aColor;
		layout (location = 3) in float aTextured;

		uniform vec2 uViewportSize;

		out vec2 vUV;
		out vec3 vColor;
		out float vTextured;

		void main() {
			// aPosition is in pixel space, origin top-left, +Y down (PixelPoint's own convention) —
			// flip Y and rescale to NDC's -1..1, +Y up.
			vec2 ndc = vec2(
				aPosition.x / uViewportSize.x * 2.0 - 1.0,
				1.0 - aPosition.y / uViewportSize.y * 2.0);
			gl_Position = vec4(ndc, 0.0, 1.0);
			vUV = aUV;
			vColor = aColor;
			vTextured = aTextured;
		}
		""";

	private const string FragmentShaderSource = """
		#version 330 core
		in vec2 vUV;
		in vec3 vColor;
		in float vTextured;

		uniform sampler2D uTexture;

		out vec4 FragColor;

		void main() {
			vec4 texColor = texture(uTexture, vUV);
			vec3 rgb = mix(vColor, texColor.rgb, vTextured);
			// Flat placeholder shapes are fully opaque; the cockpit-art quad carries its own alpha —
			// 0 over the flood-filled 3D-viewport hole (see CockpitArt), 255 over painted art — so the
			// scene rendered underneath shows through the hole once blending is enabled.
			float alpha = mix(1.0, texColor.a, vTextured);
			FragColor = vec4(rgb, alpha);
		}
		""";

	/// <summary>Placeholder HUD outline color — a legible green, not an attempt at final art.</summary>
	private static readonly Vector3 WidgetColor = new(0.2f, 1f, 0.4f);

	private readonly GL _gl;
	private readonly ShaderProgram _shader;
	private readonly GpuOverlayMesh _mesh;
	private readonly List<Overlay2DVertex> _vertices = new();

	public Overlay2DRenderer(GL gl) {
		_gl = gl;
		_shader = new ShaderProgram(gl, VertexShaderSource, FragmentShaderSource);
		_mesh = new GpuOverlayMesh(gl);
	}

	/// <summary>
	/// Draws one panel into the given viewport sub-rect: the cockpit-art quad at its own native aspect
	/// ratio (never stretched), then (when <paramref name="widgets"/> is non-null) HUD widget
	/// placeholders on top, aligned to the same transform.
	/// </summary>
	/// <param name="mirrorHorizontally">
	/// True for the left panel, which reuses the same side (<c>.HB2</c>) texture as the right panel
	/// with its UVs flipped — see <see cref="CockpitArt"/>'s doc comment; there is no separate
	/// mirrored asset.
	/// </param>
	/// <param name="widgets">
	/// The GAU file to overlay, or null to draw the cockpit-art quad alone — pass null for the side
	/// panels, since the console instruments physically live in the front view only.
	/// </param>
	public void Draw(int viewportX, int viewportY, int viewportWidth, int viewportHeight,
			GpuTexture cockpitTexture, int cockpitTextureWidth, int cockpitTextureHeight,
			bool mirrorHorizontally, GAUFile? widgets) {
		_gl.Viewport(viewportX, viewportY, (uint)Math.Max(viewportWidth, 1), (uint)Math.Max(viewportHeight, 1));
		_gl.Disable(EnableCap.DepthTest);
		_gl.Enable(EnableCap.Blend);
		_gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

		// Fit by height, preserving the art's native aspect ratio — never stretched. When the panel is
		// narrower than the art (the common case: each of three side-by-side panels is much taller
		// than 4:3), the quad is wider than the viewport and GL's own clipping crops its left/right
		// edges symmetrically, no explicit UV cropping needed. When the panel is wider than the art
		// (an ultrawide window), the quad is narrower than the viewport and sits centered, leaving the
		// live 3D view visible at the flanks instead of stretching the cockpit art to cover them.
		float scale = viewportHeight / (float)cockpitTextureHeight;
		float quadWidth = cockpitTextureWidth * scale;
		float quadX0 = (viewportWidth - quadWidth) / 2f;

		_vertices.Clear();
		AddCockpitQuad(quadX0, quadWidth, viewportHeight, mirrorHorizontally);
		if (widgets != null) {
			// Widget positions are authored in the cockpit texture's own native pixel space (after
			// CockpitArt.GauToPixelScale) — the same uniform scale and horizontal offset the quad
			// itself uses keeps them aligned to the console art regardless of panel aspect ratio.
			AddWidgets(widgets, scale, quadX0);
		}

		_shader.Use();
		_shader.SetVector2("uViewportSize", new Vector2(viewportWidth, viewportHeight));
		_shader.SetSamplerTexture("uTexture", cockpitTexture.Handle, 0);
		_mesh.SubmitAndDraw(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_vertices));

		_gl.Disable(EnableCap.Blend);
		_gl.Enable(EnableCap.DepthTest);
	}

	/// <summary>The cockpit-art quad at its native aspect ratio, positioned by <see cref="Draw"/>'s fit-by-height math.</summary>
	private void AddCockpitQuad(float quadX0, float quadWidth, int viewportHeight, bool mirror) {
		float u0 = mirror ? 1f : 0f;
		float u1 = mirror ? 0f : 1f;
		AddTexturedQuad(quadX0, 0, quadX0 + quadWidth, viewportHeight, u0, 0f, u1, 1f);
	}

	private void AddWidgets(GAUFile gau, float scale, float quadX0) {
		const float S = CockpitArt.GauToPixelScale;
		float Px(int gauX) => quadX0 + gauX * S * scale;
		float Py(int gauY) => gauY * S * scale;

		void Outline(WidgetBase? widget) {
			if (widget == null || widget.Size.Width <= 0 || widget.Size.Height <= 0) {
				return;
			}
			AddRectOutline(
				Px(widget.Origin.X), Py(widget.Origin.Y),
				Px(widget.Origin.X + widget.Size.Width), Py(widget.Origin.Y + widget.Size.Height),
				WidgetColor);
		}

		if (gau.Weapons != null) {
			foreach (var weapon in gau.Weapons) {
				Outline(weapon);
			}
		}

		Outline(gau.ChainButton);
		Outline(gau.LinkButton);
		Outline(gau.AutoTrackButton);
		Outline(gau.EnergyMeter);
		Outline(gau.MfdPanel);
		Outline(gau.TorsoTwist);

		if (gau.ShieldDisplay is { } shield) {
			Outline(shield);
			AddFilledRect(
				Px(shield.FillOrigin.X), Py(shield.FillOrigin.Y),
				Px(shield.FillOrigin.X + shield.FillSize.Width), Py(shield.FillOrigin.Y + shield.FillSize.Height),
				WidgetColor);
			AddFilledRect(
				Px(shield.DividerOrigin.X), Py(shield.DividerOrigin.Y),
				Px(shield.DividerOrigin.X + Math.Max(shield.DividerSize.Width, 1)),
				Py(shield.DividerOrigin.Y + Math.Max(shield.DividerSize.Height, 1)),
				WidgetColor);
		}

		if (gau.Throttle is { } throttle) {
			Outline(throttle);
			foreach (PixelPoint point in throttle.DetentPoints) {
				AddFilledRect(Px(point.X - 1), Py(point.Y - 1), Px(point.X + 1), Py(point.Y + 1), WidgetColor);
			}
		}

		if (gau.Reticle is { } reticle) {
			AddCrosshair(Px(reticle.Origin.X), Py(reticle.Origin.Y), 6f, WidgetColor);
		}
	}

	private const float LineThickness = 1.5f;

	private void AddCrosshair(float cx, float cy, float halfExtent, Vector3 color) {
		AddFilledRect(cx - halfExtent, cy - LineThickness / 2, cx + halfExtent, cy + LineThickness / 2, color);
		AddFilledRect(cx - LineThickness / 2, cy - halfExtent, cx + LineThickness / 2, cy + halfExtent, color);
	}

	private void AddRectOutline(float x0, float y0, float x1, float y1, Vector3 color) {
		AddFilledRect(x0, y0, x1, y0 + LineThickness, color);
		AddFilledRect(x0, y1 - LineThickness, x1, y1, color);
		AddFilledRect(x0, y0, x0 + LineThickness, y1, color);
		AddFilledRect(x1 - LineThickness, y0, x1, y1, color);
	}

	private void AddFilledRect(float x0, float y0, float x1, float y1, Vector3 color) {
		var a = new Overlay2DVertex(new Vector2(x0, y0), color);
		var b = new Overlay2DVertex(new Vector2(x1, y0), color);
		var c = new Overlay2DVertex(new Vector2(x1, y1), color);
		var d = new Overlay2DVertex(new Vector2(x0, y1), color);
		_vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
		_vertices.Add(a); _vertices.Add(c); _vertices.Add(d);
	}

	private void AddTexturedQuad(float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1) {
		var a = new Overlay2DVertex(new Vector2(x0, y0), new Vector2(u0, v0));
		var b = new Overlay2DVertex(new Vector2(x1, y0), new Vector2(u1, v0));
		var c = new Overlay2DVertex(new Vector2(x1, y1), new Vector2(u1, v1));
		var d = new Overlay2DVertex(new Vector2(x0, y1), new Vector2(u0, v1));
		_vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
		_vertices.Add(a); _vertices.Add(c); _vertices.Add(d);
	}

	public void Dispose() {
		_shader.Dispose();
		_mesh.Dispose();
	}
}
