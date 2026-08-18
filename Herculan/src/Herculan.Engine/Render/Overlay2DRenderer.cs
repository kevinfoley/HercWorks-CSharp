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
/// <para>Widgets draw the game's own <c>.HBA</c> sprite art (see <see cref="HudSpriteSheet"/>),
/// positioned by their <c>.GAU</c> rects scaled by <see cref="CockpitArt.GauToPixelScale"/>. That
/// costs a second texture bind per panel — the canopy quad and the sprite quads come from different
/// textures — so the two go out as separate draws against the same shader and vertex format.</para>
///
/// <para>Widgets whose art is not yet identified draw nothing rather than a placeholder shape: their
/// bezels are already painted into the canopy art, so an empty overlay reads as correct where a green
/// rectangle read as unfinished. Frame selection is fixed at each bank's first frame — which frame
/// index corresponds to which widget <i>state</i> (a weapon plate's ten frames, the MFD's three) is
/// not mapped yet, so nothing here animates.</para>
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
			// Everything drawn here is textured and carries its own alpha: the canopy quad is 0 over
			// the flood-filled 3D-viewport hole and 255 over painted art (see CockpitArt), and a HUD
			// sprite is 0 wherever its source palette index was 0 (see HudSpriteSheet). The untextured
			// path stays in the shader because Overlay2DVertex still offers a flat-colour vertex.
			float alpha = mix(1.0, texColor.a, vTextured);
			FragColor = vec4(rgb, alpha);
		}
		""";

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
	/// <param name="hud">
	/// The cockpit whose HUD widgets to overlay, or null to draw the cockpit-art quad alone — pass
	/// null for the side panels, since the console instruments physically live in the front view only.
	/// </param>
	/// <param name="spriteTexture">
	/// The texture <paramref name="hud"/>'s sprite atlas was uploaded to. Required alongside the
	/// atlas: the sheet supplies UVs and pixel sizes, the texture supplies the pixels.
	/// </param>
	public void Draw(int viewportX, int viewportY, int viewportWidth, int viewportHeight,
			GpuTexture cockpitTexture, int cockpitTextureWidth, int cockpitTextureHeight,
			bool mirrorHorizontally, CockpitArt? hud, GpuTexture? spriteTexture = null,
			CockpitHudState? hudState = null) {
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

		_shader.Use();
		_shader.SetVector2("uViewportSize", new Vector2(viewportWidth, viewportHeight));

		_vertices.Clear();
		AddCockpitQuad(quadX0, quadWidth, viewportHeight, mirrorHorizontally);
		_shader.SetSamplerTexture("uTexture", cockpitTexture.Handle, 0);
		_mesh.SubmitAndDraw(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_vertices));

		// Second bind, second draw: the sprites live in their own atlas. Widget positions are authored
		// in the cockpit texture's own native pixel space (after CockpitArt.GauToPixelScale), so the
		// same uniform scale and horizontal offset the canopy quad uses keeps them aligned to the
		// console art regardless of panel aspect ratio.
		// Flat-coloured gauge fills ride along in the same batch — Overlay2DVertex carries the
		// textured/flat choice per vertex, so they ignore whatever texture happens to be bound.
		if (hud != null && spriteTexture != null) {
			_vertices.Clear();
			AddGaugeFills(hud, scale, quadX0, fillFraction: 1f);
			if (hud.Sprites is { } sprites) {
				AddWidgetSprites(hud, sprites, scale, quadX0, hudState ?? CockpitHudState.Default);
			}

			if (_vertices.Count > 0) {
				_shader.SetSamplerTexture("uTexture", spriteTexture.Handle, 0);
				_mesh.SubmitAndDraw(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_vertices));
			}
		}

		_gl.Disable(EnableCap.Blend);
		_gl.Enable(EnableCap.DepthTest);
	}

	/// <summary>The cockpit-art quad at its native aspect ratio, positioned by <see cref="Draw"/>'s fit-by-height math.</summary>
	private void AddCockpitQuad(float quadX0, float quadWidth, int viewportHeight, bool mirror) {
		float u0 = mirror ? 1f : 0f;
		float u1 = mirror ? 0f : 1f;
		AddTexturedQuad(quadX0, 0, quadX0 + quadWidth, viewportHeight, u0, 0f, u1, 1f);
	}

	/// <summary>
	/// Draws the Master Energy Pool meter's LED bar: the unfilled remainder across the whole box,
	/// then the filled span as the original's one-pixel vertical pinstripe of two near-identical
	/// shades (see <see cref="HudColorTable.GaugeFillEvenId"/>).
	///
	/// <para>Geometry is the <c>.GAU</c> energy-meter rect at offset 564, which
	/// <c>EnergyPoolGauge_Ctor</c> (<c>00444d5c</c>) copies verbatim into the bar object before
	/// handing it to <c>LedBarGraph_Ctor</c> with range <c>0x400</c>. The bar fills along x in both
	/// class variants: <c>LedBarGraph_CtorBase</c> takes its start/end from the rect's x0/x1
	/// (<c>param_2[0]</c>/<c>param_2[2]</c>) and the pinstripe walk strides columns.</para>
	///
	/// <para>Nothing is drawn at <c>ShieldDisplay</c>: that widget is <c>ShieldsGauge</c>, a
	/// different class with its own nested-box geometry, not an LED bar.</para>
	///
	/// <para><paramref name="fillFraction"/> is not wired to simulation state yet and is passed as
	/// full — which also means the bar's fill <i>direction</i> does not matter yet: the original
	/// derives it from the sign of its precomputed span, and at full fill either direction covers the
	/// same box.</para>
	/// </summary>
	private void AddGaugeFills(CockpitArt hud, float scale, float quadX0, float fillFraction) {
		if (hud.GaugeColors is not var (fillEven, fillOdd, remainder)
			|| hud.Gau.EnergyMeter is not { } meter
			|| meter.Size.Width <= 0 || meter.Size.Height <= 0) {
			return;
		}

		const float S = CockpitArt.GauToPixelScale;
		float Px(int gauX) => quadX0 + gauX * S * scale;
		float Py(int gauY) => gauY * S * scale;

		int left = meter.Origin.X;
		int right = left + meter.Size.Width;
		int top = meter.Origin.Y;
		int bottom = top + meter.Size.Height;

		AddFilledRect(Px(left), Py(top), Px(right), Py(bottom), remainder);

		// Columns are stepped in the .GAU's own coordinate space, which is what the original strides
		// over — the x2 scale to cockpit pixels happens inside Px, so the stripe stays one source
		// pixel wide regardless of panel size.
		int filledTo = left + (int)MathF.Round(meter.Size.Width * Math.Clamp(fillFraction, 0f, 1f));
		for (int x = left; x < filledTo; x++) {
			AddFilledRect(Px(x), Py(top), Px(x + 1), Py(bottom), (x & 1) == 0 ? fillEven : fillOdd);
		}
	}

	private void AddFilledRect(float x0, float y0, float x1, float y1, Vector3 color) {
		var a = new Overlay2DVertex(new Vector2(x0, y0), color);
		var b = new Overlay2DVertex(new Vector2(x1, y0), color);
		var c = new Overlay2DVertex(new Vector2(x1, y1), color);
		var d = new Overlay2DVertex(new Vector2(x0, y1), color);
		_vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
		_vertices.Add(a); _vertices.Add(c); _vertices.Add(d);
	}
	/// <summary>
	/// Places each widget's sprite and text. A sprite is drawn at its own native pixel size anchored
	/// to the widget's top-left, not stretched to the widget rect: the two disagree by a few pixels in
	/// real data (a weapon plate is 116x18 against a 110x12 rect) because the <c>.GAU</c> rect is the
	/// widget's hit/layout box, not its art's extent, and stretching to match visibly softens art
	/// authored for exact pixels.
	/// </summary>
	private void AddWidgetSprites(CockpitArt hud, HudSpriteSheet sprites, float scale, float quadX0,
			CockpitHudState state) {
		const float S = CockpitArt.GauToPixelScale;
		var gau = hud.Gau;
		float Px(int gauX) => quadX0 + gauX * S * scale;
		float Py(int gauY) => gauY * S * scale;

		// Device pixels: the 640-wide space the .GAU x2 scale maps into, which is also the space the
		// sprite banks and .HFN glyphs are authored in, and the space the original's own widget code
		// works in once VideoMode_X/YCoordShift has been applied.
		float Dx(float deviceX) => quadX0 + deviceX * scale;
		float Dy(float deviceY) => deviceY * scale;

		void Blit(string bank, int frame, float left, float top) {
			if (sprites.Sprite(bank, frame) is not { } sprite || sprite.Width <= 0 || sprite.Height <= 0) {
				return;
			}

			var r = sprite.Rect;
			AddTexturedQuad(left, top, left + sprite.Width * scale, top + sprite.Height * scale,
				r.U0, r.V0, r.U1, r.V1);
		}

		void BlitDevice(string bank, int frame, float deviceLeft, float deviceTop) =>
			Blit(bank, frame, Dx(deviceLeft), Dy(deviceTop));

		void BlitAt(string bank, int frame, WidgetBase? widget) {
			if (widget != null) {
				Blit(bank, frame, Px(widget.Origin.X), Py(widget.Origin.Y));
			}
		}

		// Draws one run of glyphs left to right from a device-pixel top-left and reports where the run
		// ended — the original chains its readouts by measuring the previous one the same way.
		float DrawText(string fontName, string text, float deviceLeft, float deviceTop) {
			if (sprites.Font(fontName) is not { } font) {
				return deviceLeft;
			}

			float pen = deviceLeft;
			foreach (char c in text) {
				if (font.GlyphIndex(c) is { } glyph) {
					BlitDevice(fontName, glyph, pen, deviceTop);
					pen += font.Width(c);
				}
			}

			return pen;
		}

		// A label paints its own background before its text: the constructors write a background colour
		// id into the label object's field 0x1d — 0x2e for a weapon row, DAT_004d3c26 (colour id 19,
		// black) for the shield readouts — which is why retail's "100" sits on solid black rather than
		// on the bezel art under it.
		void DrawTextCentered(string fontName, string text, int gauX0, int gauY0, int gauX1, int gauY1,
				Vector3? background = null) {
			if (sprites.Font(fontName) is not { } font) {
				return;
			}

			if (background is { } fill) {
				AddFilledRect(Px(gauX0), Py(gauY0), Px(gauX1), Py(gauY1), fill);
			}

			DrawText(fontName, text,
				gauX0 * S + ((gauX1 - gauX0) * S - font.Measure(text)) / 2f,
				gauY0 * S + ((gauY1 - gauY0) * S - font.CellHeight) / 2f);
		}

		BlitAt("MFD", 0, gau.MfdPanel);
		BlitAt("HUDHTICK", 0, gau.TorsoTwist);

		// Frame 1 is the knob; frame 0 is a 2px tick. With no throttle state wired up yet it parks at
		// the top of its own track rather than floating at an invented position.
		BlitAt("THROTTLE", 1, gau.Throttle);

		AddWeaponRows(gau, state, BlitDevice, DrawText);
		AddShieldReadouts(gau, state, hud.GaugeColors?.Remainder, DrawTextCentered);
		AddButtonLabels(gau, state, BlitDevice, DrawTextCentered);
		AddGunsightReadouts(gau, sprites, state, DrawText);

		// The reticle is a point, not a rect — the only widget in the file that is — so its sprite
		// centers on it rather than hanging off a top-left corner.
		if (gau.Reticle is { } reticle && sprites.Sprite("HUD", 0) is { } crosshair) {
			Blit("HUD", 0,
				Px(reticle.Origin.X) - crosshair.Width * scale / 2f,
				Py(reticle.Origin.Y) - crosshair.Height * scale / 2f);
		}
	}

	/// <summary>
	/// One row per fitted hardpoint, built the way <c>WeaponGauge_Ctor</c> (<c>0044080c</c>) and its
	/// select-gadget child (<c>FUN_00442488</c>, painted by <c>FUN_004426c0</c>) build it:
	///
	/// <list type="bullet">
	/// <item>the row plate from <c>PWEAPONS</c> — frame 0 selected, frame 1 not — blitted one device
	/// pixel up and left of the <c>.GAU</c> rect, which is why its 116x18 art overhangs a 110x12 rect
	/// evenly;</item>
	/// <item>the hardpoint's state box from <c>PWEAPONS</c> frames 4-6 (6x14) at the rect's
	/// <c>+12</c> device offset — the constructor's own <c>+6</c> GAU literal;</item>
	/// <item>the slot number at <c>+6</c> device, then the weapon's name in the label rect the
	/// constructor puts at <c>+11..+35</c> GAU. Colour is the font: <c>WHITE</c> for the selected
	/// row, <c>GRAY</c> for the rest.</item>
	/// </list>
	///
	/// <para>The value field past the name — an LED charge bar for energy weapons, a round count for
	/// ballistic ones — needs weapon state the sim does not carry yet, so it is left to the plate art.
	/// <c>WPN_DMG</c> is likewise not drawn: its ten frames are damage fill levels, and frame 0, the
	/// only one this could pick blind, is a fully opaque console-coloured plate that would cover the
	/// row.</para>
	/// </summary>
	private static void AddWeaponRows(GAUFile gau, CockpitHudState state,
			Action<string, int, float, float> blit, Func<string, string, float, float, float> drawText) {
		if (gau.Weapons is not { } weapons) {
			return;
		}

		const float S = CockpitArt.GauToPixelScale;
		int slots = Math.Min(gau.WeaponListTotal, weapons.Length);
		for (int i = 0; i < slots; i++) {
			var rect = weapons[i];
			bool selected = i == state.SelectedWeapon;
			string font = selected ? "WHITE" : "GRAY";
			float left = rect.Origin.X * S;
			float top = rect.Origin.Y * S;

			blit("PWEAPONS", selected ? 0 : 1, left - 1, top - 1);
			blit("PWEAPONS", 4, left + 12, top);

			drawText(font, (i + 1).ToString(), left + 6, top);
			if (i < state.WeaponNames.Count && state.WeaponNames[i] is { Length: > 0 } name) {
				drawText(font, name, left + 22, top);
			}
		}
	}

	/// <summary>
	/// The shield meter's two numeric readouts, centred in the <c>.GAU</c> label rects at 664 and 680.
	/// <c>ShieldsGauge_Ctor</c> (<c>004434fc</c>) builds them with the <c>WHITE</c> font, and
	/// <c>FUN_00444a68</c> fills them with <c>balance * 200 &gt;&gt; 10</c> and its complement — so an
	/// even fore/aft split reads 100 and 100 out of a 200-point pool.
	///
	/// <para>The meter bodies themselves are not drawn here. They are painted into the canopy art in
	/// palette indices 66-71 and lit by <see cref="CockpitPalette.InstallShieldRamp"/>.</para>
	/// </summary>
	private static void AddShieldReadouts(GAUFile gau, CockpitHudState state, Vector3? background,
			Action<string, string, int, int, int, int, Vector3?> drawCentered) {
		if (gau.ShieldDisplay is not { } shields) {
			return;
		}

		void Label(string text, PixelPoint origin, PixelSize size) =>
			drawCentered("WHITE", text, origin.X, origin.Y, origin.X + size.Width, origin.Y + size.Height,
				background);

		Label(state.ShieldFront.ToString(), shields.FrontLabel, shields.FrontLabelSize);
		Label(state.ShieldRear.ToString(), shields.RearLabel, shields.RearLabelSize);
	}

	/// <summary>
	/// The three console buttons: a <c>PWEAPONS</c> plate with a caption centred on it.
	///
	/// <para>The plate is not canopy art — <c>FUN_00442c88</c> blits it per frame from
	/// <c>PWEAPONS</c> frames 2 and 3, indexed <c>bank[2 + state]</c>, at the widget's own rect. Frame
	/// 2 is the unlit plate (solid palette index 34, the blue the retail screenshot shows at RGB
	/// (77,77,182)) and frame 3 the lit one (index 14, green). Both are 50x16 against a 48x14 rect,
	/// the same one-pixel overhang the weapon-row plates have, and all three buttons are that same
	/// 24x7 GAU size in every retail file.</para>
	///
	/// <para>The chain button's caption is its count in Roman numerals, read from DBSIM's own
	/// three-entry table at <c>0049c71c</c> ("I", "II", "III"); the other two are fixed.</para>
	/// </summary>
	private static void AddButtonLabels(GAUFile gau, CockpitHudState state,
			Action<string, int, float, float> blit,
			Action<string, string, int, int, int, int, Vector3?> drawCentered) {
		const float S = CockpitArt.GauToPixelScale;

		void Button(string text, WidgetBase? widget, bool lit = false) {
			if (widget == null) {
				return;
			}

			blit("PWEAPONS", lit ? 3 : 2, widget.Origin.X * S, widget.Origin.Y * S);
			drawCentered("WHITE", text, widget.Origin.X, widget.Origin.Y,
				widget.Origin.X + widget.Size.Width, widget.Origin.Y + widget.Size.Height, null);
		}

		Button(new string('I', Math.Clamp(state.ChainCount, 1, 3)), gau.ChainButton);
		Button("LINK", gau.LinkButton);
		Button("TRACK", gau.AutoTrackButton);
	}

	/// <summary>
	/// The speed and mission-time readouts under the reticle, laid out as
	/// <c>Gau_RovingGunsightWidget</c> (<c>0043c7d8</c>) lays them out from the two anchor points in
	/// the gunsight complex's own <c>.GAU</c> block:
	///
	/// <list type="bullet">
	/// <item>the anchor at 1128/1132 is the <b>left</b> edge of "SPEED:"; the value follows two GAU
	/// pixels past the caption's measured width;</item>
	/// <item>the anchor at 1120/1124 is the <b>right</b> edge of the time field, whose left edge is
	/// that minus the measured width of "00000" — a five-digit reservation — with the "TIME:" caption
	/// right-aligned two GAU pixels before it.</item>
	/// </list>
	///
	/// <para>Captions use <c>HUD2</c> and values <c>HUD3</c>, the constructor's own font choices
	/// (<c>0049b0ec</c> and <c>0049b0f0</c>, entries 16 and 17 of <c>ColorSchemePanels</c>). That is
	/// where the retail screenshot's pale yellow-green captions and cyan values come from: they are
	/// theater palette indices 73 and 74, not colours the widget picks.</para>
	/// </summary>
	private static void AddGunsightReadouts(GAUFile gau, HudSpriteSheet sprites, CockpitHudState state,
			Func<string, string, float, float, float> drawText) {
		if (gau.RemainderBeforeReticle is not { Length: >= 16 } anchors
			|| sprites.Font("HUD2") is not { } captions
			|| sprites.Font("HUD3") is not { } values) {
			return;
		}

		const float S = CockpitArt.GauToPixelScale;
		int Anchor(int offset) => BitConverter.ToInt32(anchors, offset);

		float timeRight = Anchor(0) * S;
		float speedLeft = Anchor(8) * S;
		float row = Anchor(12) * S;

		float speedEnd = drawText("HUD2", SpeedCaption, speedLeft, row);
		drawText("HUD3", $"{state.SpeedKph} K/H", speedEnd + ReadoutGap, row);

		float timeLeft = timeRight - values.Measure(TimeFieldReservation);
		drawText("HUD3", state.MissionTime.ToString(@"mm\:ss"), timeLeft, row);
		drawText("HUD2", TimeCaption, timeLeft - ReadoutGap - captions.Measure(TimeCaption), row);
	}

	/// <summary>The two captions, and the five-digit field the time value is right-anchored through.</summary>
	private const string SpeedCaption = "SPEED:";

	private const string TimeCaption = "TIME:";

	private const string TimeFieldReservation = "00000";

	/// <summary>The gap between a caption and its value: the constructor's own <c>2 &lt;&lt; XCoordShift</c>, four device pixels wide.</summary>
	private const float ReadoutGap = 2 * CockpitArt.GauToPixelScale;


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
