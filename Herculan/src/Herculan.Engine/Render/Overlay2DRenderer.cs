using System.Numerics;
using HercWorks.Core.Data.File.Gau;
using HercWorks.Core.Data.Struct;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Herculan.Engine.Sim;
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
			AddGaugeFills(hud, scale, quadX0,
				fillFraction: (hudState ?? CockpitHudState.Default).EnergyFraction / 1024f);
			if (hud.Sprites is { } sprites) {
				AddWidgets(hud, sprites, scale, quadX0, hudState ?? CockpitHudState.Default);
			}

			if (_vertices.Count > 0) {
				_shader.SetSamplerTexture("uTexture", spriteTexture.Handle, 0);
				_mesh.SubmitAndDraw(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_vertices));
			}
		}

		_gl.Disable(EnableCap.Blend);
		_gl.Enable(EnableCap.DepthTest);
	}

	/// <summary>
	/// Draws the Heads-Down Display's background — <c>(herc).HB1</c> — filling the given viewport
	/// edge to edge horizontally.
	///
	/// <para>The art itself is placed exactly as <see cref="Draw"/> places a cockpit panel: fit by
	/// height at its own 4:3 aspect ratio, horizontally centered, never stretched. On a window wider
	/// than 4:3 that leaves a margin on each side, and this fills those margins by stretching the
	/// art's own outermost pixel column outward. That is a Herculan addition with no original behind
	/// it — DBSIM only ever ran at 4:3 and had no margins to fill — chosen because the HDD's art is a
	/// flat console surround whose edge columns are near-uniform, so the stretch reads as the panel
	/// continuing rather than as a smear. The alternative, letting the cleared framebuffer show
	/// through at the flanks, reads as a hole in the cockpit.</para>
	/// </summary>
	/// <param name="hud">
	/// The cockpit whose Heads-Down Display widgets to overlay, or null to draw the <c>.HB1</c> art
	/// alone. Everything drawn comes from <see cref="CockpitArt.HeadsDownLayout"/>, which is the
	/// herc's own <c>.GAU</c> block rather than a hardcoded layout — see <see cref="HddLayout"/>.
	/// </param>
	public void DrawHeadsDown(int viewportX, int viewportY, int viewportWidth, int viewportHeight,
			GpuTexture texture, int textureWidth, int textureHeight,
			CockpitArt? hud = null, GpuTexture? spriteTexture = null, CockpitHudState? hudState = null) {
		_gl.Viewport(viewportX, viewportY, (uint)Math.Max(viewportWidth, 1), (uint)Math.Max(viewportHeight, 1));
		_gl.Disable(EnableCap.DepthTest);
		_gl.Enable(EnableCap.Blend);
		_gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

		float scale = viewportHeight / (float)textureHeight;
		float quadWidth = textureWidth * scale;
		float quadX0 = (viewportWidth - quadWidth) / 2f;
		float quadX1 = quadX0 + quadWidth;

		_shader.Use();
		_shader.SetVector2("uViewportSize", new Vector2(viewportWidth, viewportHeight));

		_vertices.Clear();

		// Texel centres, so nearest sampling lands on the outermost column and not on whatever the
		// clamp boundary rounds to.
		float leftEdgeU = 0.5f / textureWidth;
		float rightEdgeU = 1f - 0.5f / textureWidth;

		if (quadX0 > 0f) {
			AddTexturedQuad(0f, 0f, quadX0, viewportHeight, leftEdgeU, 0f, leftEdgeU, 1f);
		}

		if (quadX1 < viewportWidth) {
			AddTexturedQuad(quadX1, 0f, viewportWidth, viewportHeight, rightEdgeU, 0f, rightEdgeU, 1f);
		}

		AddTexturedQuad(quadX0, 0f, quadX1, viewportHeight, 0f, 0f, 1f, 1f);

		_shader.SetSamplerTexture("uTexture", texture.Handle, 0);
		_mesh.SubmitAndDraw(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_vertices));

		// Second bind, second draw, exactly as Draw layers the console instruments over .HB0: the
		// display's own sprites and glyphs live in the shared HUD atlas, not in the canopy texture.
		if (hud?.HeadsDownLayout is { } layout && hud.Sprites is { } sprites && spriteTexture != null) {
			_vertices.Clear();
			AddHeadsDown(hud, layout, sprites, scale, quadX0, hudState ?? CockpitHudState.Default);

			if (_vertices.Count > 0) {
				_shader.SetSamplerTexture("uTexture", spriteTexture.Handle, 0);
				_mesh.SubmitAndDraw(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_vertices));
			}
		}

		_gl.Disable(EnableCap.Blend);
		_gl.Enable(EnableCap.DepthTest);
	}

	/// <summary>
	/// The Heads-Down Display's live content, built the way <c>FUN_00448cc8</c> and its two page
	/// constructors build it — see <see cref="HddLayout"/> for where every rect and frame index comes
	/// from, and docs/formats/cockpit-hud.md for the pan that reaches this view.
	///
	/// <list type="number">
	/// <item>the screen area, flooded with colour id 19 the way whichever page owns it floods it;</item>
	/// <item>that page's own content — the map viewport and order list, or the paper doll and
	/// component list;</item>
	/// <item>the widgets the page shows: two page buttons lit for the current page, four arrow
	/// buttons, the map's two magnifiers, and XMIT/CANCEL;</item>
	/// <item>the three squad comm boxes;</item>
	/// <item>the indicator block and the page title, which the display paints after everything
	/// else.</item>
	/// </list>
	///
	/// <para><b>Layout and text, not state.</b> The tactical map's terrain and its 140 unit markers,
	/// the pilots' video and the static that replaces it when their comms are out, which orders the
	/// selected squadmate can currently take, and real per-component damage percentages all need
	/// simulation state the engine does not carry — so the map viewport draws its flood, the comm
	/// boxes draw the empty-slot fill the original uses for a slot no squadmate occupies, every order
	/// draws available, and each component reads the undamaged 100 its own value column is sized
	/// around.</para>
	///
	/// <para>The page's content goes down before the widgets rather than after, which is the reverse
	/// of the display's own paint loop. That loop can afford the other order because a page only
	/// floods its screen on a full repaint; here every frame is a full repaint, and XMIT and CANCEL
	/// sit inside the screen rect, so painting the page last would erase them.</para>
	/// </summary>
	private void AddHeadsDown(CockpitArt hud, HddLayout layout, HudSpriteSheet sprites,
			float scale, float quadX0, CockpitHudState state) {
		var strings = hud.Strings;

		// Device pixels relative to the .HB1 art's top-left, which is the space HddLayout reports in
		// and the space the sprite banks and .HFN glyphs are authored in.
		float Dx(float x) => quadX0 + x * scale;
		float Dy(float y) => y * scale;

		void Fill(HddLayout.Rect rect, Vector3 color) =>
			AddFilledRect(Dx(rect.X0), Dy(rect.Y0), Dx(rect.X1 + 1), Dy(rect.Y1 + 1), color);

		void Blit(string bank, int frame, float left, float top) {
			if (sprites.Sprite(bank, frame) is not { } sprite || sprite.Width <= 0 || sprite.Height <= 0) {
				return;
			}

			var r = sprite.Rect;
			AddTexturedQuad(Dx(left), Dy(top), Dx(left + sprite.Width), Dy(top + sprite.Height),
				r.U0, r.V0, r.U1, r.V1);
		}

		// One run of glyphs left to right, with the character at hotkeyIndex drawn in an alternate
		// font — FUN_00438aac's own behaviour, and where the order list's red hotkey letters come from.
		// Pass -1 for a plain run.
		void DrawText(string fontName, string alternateFont, string text, int hotkeyIndex,
				float left, float top) {
			if (sprites.Font(fontName) is not { } font) {
				return;
			}

			float pen = left;
			for (int i = 0; i < text.Length; i++) {
				string face = i == hotkeyIndex && sprites.Font(alternateFont) != null ? alternateFont : fontName;
				var metrics = sprites.Font(face)!;
				if (metrics.GlyphIndex(text[i]) is { } glyph) {
					Blit(face, glyph, pen, top);
					pen += metrics.Width(text[i]);
				}
			}
		}

		// Label_SetRect/Label_SetText's shared placement — see HudFont.Place.
		void DrawLabel(string fontName, string alternateFont, string text, int hotkeyIndex,
				HddLayout.Rect rect, bool centered, float marginX = 0f) {
			if (sprites.Font(fontName) is not { } metrics) {
				return;
			}

			var (textX, textY) = metrics.Place(text, rect.X0, rect.Y0, rect.X1, rect.Y1,
				centered ? LabelAlign.Center : LabelAlign.Left, (int)marginX);
			DrawText(fontName, alternateFont, text, hotkeyIndex, textX, textY);
		}

		var background = hud.HeadsDownColors?.Background;
		if (background is { } screenFill) {
			Fill(layout.Screen, screenFill);
		}

		if (state.Hdd == HddPage.CommandDisplay) {
			AddHddCommandDisplay(layout, strings, background, Fill, DrawLabel);
		} else {
			AddHddDamageDetail(hud, layout, sprites, strings, state.HddDamage, state, Blit, Fill, DrawLabel);
		}

		// Visibility, position and lit state come from CockpitWidgets so the click regions agree with
		// what is drawn. The frame check stays here and stays a draw-side concern: a widget with no
		// sprite of its own — the title box, the dead slot — is still clickable in the original's flat
		// list, so CockpitWidgets reports it and only this loop skips it.
		foreach (var clickable in CockpitWidgets.VisibleHddWidgets(hud, state)) {
			var widget = clickable.Id.AsHddWidget!.Value;
			int i = clickable.Id.Index;
			if (layout.UnlitFrame(widget) is not { } unlit) {
				continue;
			}

			bool lit = clickable.Lit;
			var rect = layout[widget];
			Blit(HddLayout.Bank, lit ? unlit + 1 : unlit, rect.X0, rect.Y0);

			// A page button captions itself "F7"/"F8" from the shared "Fx" literal, in DARK for the
			// page that is showing and WHITE otherwise — the same pair the MFD's mode buttons use, and
			// keyed on selection rather than on Lit for the same reason (see CockpitWidget.Selected).
			// XMIT and CANCEL take their captions from the string table and their font from
			// ColorSchemePanels[4 + lit].
			if (widget is HddLayout.Widget.PageButton0 or HddLayout.Widget.PageButton1) {
				DrawLabel(clickable.Selected ? "DARK" : HddLayout.TitleFont, string.Empty,
					"F" + (7 + i), -1, rect, centered: true);
			} else if (widget is HddLayout.Widget.Transmit or HddLayout.Widget.Cancel
				&& strings?.Text(HddLayout.ButtonCaptionGroup, i - (int)HddLayout.Widget.Transmit)
					is { Length: > 0 } caption) {
				DrawLabel(HddLayout.TransmitButtonFont, string.Empty, caption, -1,
					layout.TransmitCaptionBox(widget), centered: true);
			}
		}

		// A comm box with nobody in it: the display's own paint floods the box inset one device pixel
		// on every edge with colour id 19 and draws no labels. With no squad state every slot is that
		// slot — the retail screenshot's third box, beside the two carrying video.
		if (background is { } boxFill) {
			for (int i = 0; i < HddLayout.PilotSlotCount; i++) {
				Fill(layout[HddLayout.Widget.PilotBox0 + i].Inset(1, 1), boxFill);
			}
		}

		if (hud.HeadsDownColors?.Indicator is { } indicator) {
			Fill(layout.Indicator, indicator);
		}

		// Last, after the page has painted — the display's own order, so a page that draws into the
		// header strip cannot cover its own caption. Centred, and on its own black background.
		if (HddLayout.Title(strings, state.Hdd, state.HddDamage) is { Length: > 0 } title) {
			var titleBox = layout[HddLayout.Widget.TitleBox];
			if (background is { } titleFill) {
				Fill(titleBox, titleFill);
			}

			DrawLabel(HddLayout.TitleFont, string.Empty, title, -1, titleBox, centered: true);
		}
	}

	/// <summary>
	/// The command display (<c>FUN_0044c264</c>): the tactical map on the left and, down the right,
	/// nine rows — a message row and the eight orders you transmit to the selected squadmate.
	///
	/// <para>Orders come from <c>STRINGS0.STR</c> group 0 entries 10-17, and each entry's single
	/// attribute byte is the index of its hotkey character within its own text, which is why DEFEND
	/// POSITION highlights its F and SCAN FOR HOSTILES its C — the manual's own key bindings, stored
	/// beside the strings rather than in the code. The row refresh (<c>FUN_0044ddec</c>) draws an
	/// available order in <c>CPGREEN</c> with the hotkey in <c>CPRED</c>, an unavailable one wholly in
	/// <c>CPBLUE</c>, and the selected one in <c>CPYLW</c>; availability and selection are squad state,
	/// so every row draws available here.</para>
	///
	/// <para>The map viewport gets the flood its own render target sits on and nothing more. What
	/// belongs in it is a live overhead terrain raster plus up to 140 unit markers — the same
	/// rasterizer the MFD's NAV MAP is waiting on.</para>
	/// </summary>
	private static void AddHddCommandDisplay(HddLayout layout, SimStringTable? strings,
			Vector3? background, Action<HddLayout.Rect, Vector3> fill, HddLabelWriter drawLabel) {
		if (background is { } mapFill) {
			fill(layout.MapViewport, mapFill);
		}

		var orders = strings?.Group(HddLayout.OrderGroup);
		if (orders == null) {
			return;
		}

		for (int i = 0; i < HddLayout.OrderCount; i++) {
			int entry = HddLayout.FirstCommandOrder + i;
			if (entry >= orders.Count || orders[entry].Text is not { Length: > 0 } text) {
				continue;
			}

			int hotkey = orders[entry].Attributes is { Length: > 0 } attributes ? attributes[0] : -1;

			// Row 0 of the nine is the incoming-message row, which is blank outside a transmission —
			// the orders start at row 1.
			drawLabel(HddLayout.OrderFont, HddLayout.OrderHotkeyFont, text, hotkey,
				layout.OrderRow(i + 1), false, HddLayout.OrderTextMargin);
		}
	}

	/// <summary>
	/// The damage detail (<c>FUN_0045079c</c>): the herc's paper doll on the left of the screen and,
	/// down the right, thirteen component rows — a name and a percentage each.
	///
	/// <para>Names come from the string table by category: 19 structural sections or 12 internal
	/// systems, of which thirteen fit at once. The percentage column's width is the measured width of
	/// the literal "100", which the constructor reserves before placing either label — so an undamaged
	/// component fills its column exactly. The manual's damage colours (green normal through red
	/// imminent, grey inoperative) are a paint-time re-font from four colour ids the screen resolves
	/// at construction; with no damage model, every row draws in the green its constructor
	/// installs.</para>
	///
	/// <para>The paper doll is the herc's own <c>.PDG</c> view for the category — front for structural,
	/// rear for internal — blitted at the screen rect's top-left plus that view's own origin, which is
	/// the paint's own arithmetic rather than a centring rule. The weapons category has no doll and
	/// lists the mech's fitted hardpoints instead; those are already in
	/// <see cref="CockpitHudState.HardpointNames"/>.</para>
	/// </summary>
	private static void AddHddDamageDetail(CockpitArt hud, HddLayout layout, HudSpriteSheet sprites,
			SimStringTable? strings, HddDamageView view, CockpitHudState state,
			Action<string, int, float, float> blit, Action<HddLayout.Rect, Vector3> fill,
			HddLabelWriter drawLabel) {
		const float S = CockpitArt.GauToPixelScale;

		// Whose herc is being inspected, on its own plate at the screen's bottom-left. The engine only
		// ever inspects the player, which is entry 0 of the display's own five-name array — the three
		// squadmates and "TARGET" behind it need squad and targeting state.
		if (strings?.Text(HddLayout.SubjectNameGroup, 0) is { Length: > 0 } subject) {
			if (hud.HeadsDownColors?.SubjectPlate is { } plate) {
				fill(layout.DamageFooter, plate);
			}

			drawLabel(HddLayout.SubjectFont, string.Empty, subject, -1, layout.DamageFooter, true, 0f);
		}

		if (HddLayout.PaperDollView(view) is { } dollView
			&& hud.PaperDoll?.Entries is { } views && dollView < views.Length && views[dollView] is { } doll) {
			blit(hud.HercName, dollView,
				layout.Screen.X0 + doll.Origin.X * S, layout.Screen.Y0 + doll.Origin.Y * S);
		}

		// The weapons category lists the subject's own fitted hardpoints instead of a fixed table —
		// FUN_00450c54 copies each weapon's name straight off the mech. Those are already loaded.
		var names = view == HddDamageView.Weapons
			? state.HardpointNames.Where(n => n.Length > 0).ToList()
			: HddLayout.ComponentNames(strings, view).Select(e => e.Text).ToList();

		float valueWidth = sprites.Font(HddLayout.DamageRowFont)?.Measure(HddLayout.DamageValueReservation) ?? 0f;

		for (int i = 0; i < HddLayout.DamageRowCount && i < names.Count; i++) {
			if (names[i] is not { Length: > 0 } text) {
				continue;
			}

			var row = layout.DamageRow(i);
			var nameBox = row with { X1 = row.X1 - (int)valueWidth };
			var valueBox = row with { X0 = nameBox.X1 };

			drawLabel(HddLayout.DamageRowFont, string.Empty, text, -1, nameBox, false, 0f);
			drawLabel(HddLayout.DamageRowFont, string.Empty, HddLayout.DamageValueReservation, -1,
				valueBox, false, 0f);
		}
	}

	/// <summary>
	/// Places one Heads-Down Display label: a font, the alternate its hotkey character is drawn in,
	/// the text, that character's index (-1 for none), the device-pixel rect it sits in, whether it
	/// centres horizontally, and how far its text is indented from the anchoring edge.
	/// </summary>
	private delegate void HddLabelWriter(string font, string alternateFont, string text, int hotkeyIndex,
		HddLayout.Rect rect, bool centered, float marginX);

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
	/// <para><paramref name="fillFraction"/> is the piloted machine's Master Energy Pool, over the
	/// same 0-1024 range the widget's bar was built with — see
	/// <c>Herculan.Engine.Sim.MechObject.EnergyPoolFraction</c>. The bar's fill <i>direction</i> is
	/// still assumed rather than read: the original derives it from the sign of its precomputed span,
	/// and every retail rect authors x0 left of x1, so it fills left to right here.</para>
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
	private void AddWidgets(CockpitArt hud, HudSpriteSheet sprites, float scale, float quadX0,
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

			// The .GAU rect's Origin + Size is the file's own inclusive second corner, so scaling both
			// corners gives the device-pixel rect Label_SetRect takes. See HudFont.Place.
			var (textX, textY) = font.Place(text,
				(int)(gauX0 * S), (int)(gauY0 * S), (int)(gauX1 * S), (int)(gauY1 * S),
				LabelAlign.Center);
			DrawText(fontName, text, textX, textY);
		}

		AddMfd(hud, state, BlitDevice, DrawText,
			(x0, y0, x1, y1, color) => AddFilledRect(Dx(x0), Dy(y0), Dx(x1), Dy(y1), color));
		BlitAt("HUDHTICK", 0, gau.TorsoTwist);

		// The Rotation Indicator, above the heading tape: a fixed track with a bar sliding along it at
		// the turret's twist angle, in one of two colours depending on whether the turret is centred.
		// Its geometry is derived from the heading tape's rect rather than read from the file — see
		// RotationIndicator.
		if (RotationIndicator.From(hud) is { } rotation) {
			BlitDevice(RotationIndicator.SpriteBank, RotationIndicator.TrackFrame,
				rotation.TrackX, rotation.TrackY);
			BlitDevice(RotationIndicator.SpriteBank, RotationIndicator.FrameFor(state.TorsoTwist),
				rotation.BarLeftFor(state.TorsoTwist), rotation.BarY);
		}

		// The throttle slider: frame 1 is the knob, riding the track at the setting's own height, and
		// frame 0 the 2px tick the gauge parks beside the track's centre — ThrottleGauge_Ctor captures
		// that tick's position once, at the knob's neutral height, and never moves it again. Its x
		// nudge is the .GAU's own offset-1072 int, the one field of the block the loader does not
		// pre-scale, which is why it is shifted here instead.
		if (ThrottleTrack.From(hud) is { } track) {
			BlitDevice(ThrottleTrack.SpriteBank, ThrottleTrack.KnobFrame,
				track.Left, track.KnobTopFor(state.Throttle));
			BlitDevice(ThrottleTrack.SpriteBank, ThrottleTrack.TickFrame,
				track.Left + track.TickOffsetX, track.KnobTopFor(0));
		}

		AddWeaponRows(gau, state, hud.WeaponBarColors, BlitDevice, DrawText,
			(x0, y0, x1, y1, color) => AddFilledRect(Dx(x0), Dy(y0), Dx(x1), Dy(y1), color));
		AddShieldReadouts(gau, state, hud.GaugeColors?.Remainder, DrawTextCentered);
		AddConsoleButtons(gau, hud.Strings, state, BlitDevice, DrawTextCentered);
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
	/// <item>the hardpoint's state box from <c>PWEAPONS</c> frames 4 and 5 (6x14) at the rect's
	/// <c>+12</c> device offset — the constructor's own <c>+6</c> GAU literal. It is drawn only for a
	/// mount that is armed or in the current fire group, lit (frame 4) when the mount could fire and
	/// dark (frame 5) when it could not, which is why a pod's row never has one;</item>
	/// <item>the slot number at <c>+6</c> device, then the weapon's name in the label rect the
	/// constructor puts at <c>+11..+35</c> GAU. Colour is the font: <c>WHITE</c> for the selected
	/// row, <c>GRAY</c> for the rest.</item>
	/// <item>the value field past the name, at <c>+0x24..+0x35</c> GAU — a round count for an
	/// ammunition mount (<c>FUN_004411b4</c> prints <c>itoa(rounds)</c> there) and an LED charge bar
	/// for an energy one (<c>FUN_00442b38</c> paints one across the same span). A pod has neither: its
	/// own constructor widens the name label across both fields instead.</item>
	/// </list>
	///
	/// <para><c>WPN_DMG</c> is not drawn: its ten frames are damage fill levels and nothing damages a
	/// mount yet. The original blits its frame 0 as every row's underlay, before the plate; here the
	/// plate is the underlay, which comes to the same thing while no row is damaged.</para>
	/// </summary>
	private static void AddWeaponRows(GAUFile gau, CockpitHudState state,
			(Vector3 FillEven, Vector3 FillOdd)? barColors,
			Action<string, int, float, float> blit, Func<string, string, float, float, float> drawText,
			Action<float, float, float, float, Vector3> fillRect) {
		if (gau.Weapons is not { } weapons) {
			return;
		}

		const float S = CockpitArt.GauToPixelScale;
		int slots = Math.Min(gau.WeaponListTotal, weapons.Length);
		for (int i = 0; i < slots; i++) {
			var rect = weapons[i];
			var row = i < state.Weapons.Count ? state.Weapons[i] : WeaponRowState.Empty;
			string font = row.Selected ? "WHITE" : "GRAY";
			float left = rect.Origin.X * S;
			float top = rect.Origin.Y * S;

			blit("PWEAPONS", row.Selected ? 0 : 1, left - 1, top - 1);
			if (row.Selected || row.InGroup) {
				blit("PWEAPONS", row.Ready ? ReadyStateFrame : UnreadyStateFrame, left + 12, top);
			}

			drawText(font, (i + 1).ToString(), left + 6, top);
			if (row.Name is { Length: > 0 } name) {
				drawText(font, name, left + 22, top);
			}

			switch (row.Kind) {
				case WeaponMountKind.Ammunition:
					drawText(font, row.Rounds.ToString(), left + ValueFieldLeft * S, top);
					break;

				case WeaponMountKind.Energy when barColors is var (fillEven, fillOdd):
					AddChargeBar(row.ChargeMeter,
						left + ValueFieldLeft * S, top + ChargeBarTop * S,
						(ValueFieldRight - ValueFieldLeft) * S,
						(ChargeBarBottom - ChargeBarTop) * S, fillEven, fillOdd, fillRect);
					break;
			}
		}
	}

	/// <summary><c>PWEAPONS</c> frame for a mount that could fire this instant.</summary>
	private const int ReadyStateFrame = 4;

	/// <summary>And for one that could not — out of ammunition, still charging, or inside its refire delay.</summary>
	private const int UnreadyStateFrame = 5;

	/// <summary>
	/// Where a weapon row's value field starts and ends, in <c>.GAU</c> units from the row's own
	/// left edge — the ammunition gauge's <c>+0x24..+0x35</c> label rect (<c>FUN_00440f78</c>), which
	/// is also the span the energy gauge hands its LED bar (<c>FUN_00442950</c>).
	/// </summary>
	private const int ValueFieldLeft = 0x24;

	private const int ValueFieldRight = 0x35;

	/// <summary>
	/// The charge bar's top and bottom edges, in <c>.GAU</c> units below the row's own top. The
	/// energy gauge builds the bar's rect as the value field at <c>y0..y0+5</c>
	/// (<c>FUN_00440a68</c>), and <c>FUN_00442950</c> then drops the top edge by one more unit — so
	/// the bar is a touch shorter than the row and sits clear of the plate's upper bezel.
	/// </summary>
	private const int ChargeBarTop = 1;

	private const int ChargeBarBottom = 5;

	/// <summary>
	/// An energy mount's capacitor bar — the same one-pixel pinstripe of two near-identical shades
	/// <see cref="AddGaugeFills"/> paints for the Master Energy Pool, since both are the same
	/// <c>LEDBarGraph</c> class.
	///
	/// <para>Unlike the pool's meter, the unfilled remainder is left alone: the weapon row's bar sits
	/// on the plate art rather than on its own box, and <c>LedBarGraph_PaintToValue</c>'s remainder
	/// colour for this instance is the row background it was built with.</para>
	/// </summary>
	/// <param name="meterValue">The bar's value over its own 0-1024 range.</param>
	private static void AddChargeBar(int meterValue, float left, float top, float width, float height,
			Vector3 fillEven, Vector3 fillOdd, Action<float, float, float, float, Vector3> fillRect) {
		const int Range = 0x400;
		int columns = (int)MathF.Round(width);
		int filled = (int)(Math.Clamp(meterValue, 0, Range) * (long)columns / Range);

		for (int x = 0; x < filled; x++) {
			fillRect(left + x, top, left + x + 1, top + height, (x & 1) == 0 ? fillEven : fillOdd);
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
	/// The multi-function display, built the way <c>MfdDisplay_Ctor</c> (<c>00445218</c>) builds it —
	/// see <see cref="MfdLayout"/> for where every rect comes from and
	/// docs/formats/cockpit-hud.md for the panel it sits in.
	///
	/// <list type="number">
	/// <item>the screen itself, <c>MFD</c> frame 0, at the panel rect inset 18 GAU units from the left;
	/// its 196x122 art fills that inset region exactly;</item>
	/// <item>the F1-F6 mode column down the strip the inset left free, each button lit (frame 4) for
	/// the current screen and unlit (frame 3) otherwise, captioned "F1".."F6" — the original composes
	/// those from its own <c>"Fx"</c> literal rather than storing six strings;</item>
	/// <item>the aux buttons the current screen shows, from the 6x13 visibility table — SELECT on the
	/// two status screens, XMIT on FLASH COMM, PASS/ACTIVE/RANGE/TARGET on the scanner, none on the
	/// nav map or missile cam;</item>
	/// <item>the screen title, and then whichever screen's own content is implemented.</item>
	/// </list>
	///
	/// <para>A lit button captions in <c>DARK</c> and an unlit one in <c>WHITE</c>, which is
	/// <c>FUN_004474e4</c>'s own choice: it picks <c>ColorSchemePanels[12]</c> when the button's
	/// <c>+0x40</c> lit flag is set and <c>[10]</c> when it is clear.</para>
	///
	/// <para><b>Screens are laid out, not driven.</b> STATUS, FLASH COMM and NAV MAP draw their real
	/// widget geometry with the text the string table actually holds; the values in that text are the
	/// power-up placeholders in <see cref="CockpitHudState"/>, since the sim carries no damage,
	/// squad-order or map state yet. SCANNER, TARGET STATUS and MISSILE CAM draw their screen and
	/// buttons only — TARGET STATUS shares STATUS's layout but needs a target to name, and the other
	/// two are wholly state-driven.</para>
	/// </summary>
	private static void AddMfd(CockpitArt hud, CockpitHudState state,
			Action<string, int, float, float> blitDevice,
			Func<string, string, float, float, float> drawText,
			Action<float, float, float, float, Vector3> fillRect) {
		if (hud.Sprites is not { } sprites || MfdLayout.InsetOrigin(hud.Gau) is not { } inset) {
			return;
		}

		const float S = CockpitArt.GauToPixelScale;
		float insetX = inset.X * S;
		float insetY = inset.Y * S;
		var strings = hud.Strings;

		// Device-pixel positions measured from the inset origin, which is the space every rect in
		// MfdLayout is expressed in once scaled.
		float X(int gau) => insetX + gau * S;
		float Y(int gau) => insetY + gau * S;

		// Places one label in a device-pixel rect through the shared Label_SetRect/Label_SetText rule
		// — see HudFont.Place, which is where that pair's arithmetic lives.
		void DrawLabel(string font, string text, float x0, float y0, float x1, float y1,
				bool centered, float marginX = 0f) {
			if (sprites.Font(font) is not { } metrics) {
				return;
			}

			var (textX, textY) = metrics.Place(text, (int)x0, (int)y0, (int)x1, (int)y1,
				centered ? LabelAlign.Center : LabelAlign.Left, (int)marginX);
			drawText(font, text, textX, textY);
		}

		if (MfdLayout.BackgroundFrame(state.Mfd) is { } background) {
			blitDevice(MfdLayout.Bank, background, insetX, insetY);
		}

		// The nav map owns the whole inset region and its paint (FUN_004405e4) floods that rect with
		// COLORS.DAT id 19 — black, the same id the gauge remainder resolves through — before
		// rasterizing terrain into it. That flood is why the repaint blits no chrome for this mode at
		// all, and it goes down before the buttons and title for the same reason the original paints
		// them after. The terrain it would cover needs a map rasterizer the engine does not have.
		if (state.Mfd == MfdMode.NavMap
			&& hud.GaugeColors?.Remainder is { } mapBackground
			&& sprites.Sprite(MfdLayout.Bank, 0) is { } screen) {
			fillRect(insetX, insetY, insetX + screen.Width, insetY + screen.Height, mapBackground);
		}

		// Which buttons this mode shows, where they sit and which are lit all come from
		// CockpitWidgets, so the same answers drive the click regions — see that type. Only the
		// caption's own right and bottom bounds are taken from the table directly: those are a text
		// layout box, not the button's extent, and the original measures them to the rect's inclusive
		// GAU edge rather than to the last pixel its sprite covers.
		foreach (var widget in CockpitWidgets.VisibleMfdButtons(hud, state)) {
			int i = widget.Id.Index;
			var button = MfdLayout.Buttons[i];

			blitDevice(MfdLayout.Bank, widget.Lit ? button.LitFrame : button.UnlitFrame, widget.X0, widget.Y0);
			if (MfdLayout.Caption(strings, i) is { Length: > 0 } caption) {
				// The plate follows Lit, the caption follows Selected. MfdButton_Repaint is the only
				// thing that re-fonts a caption, and it reads the button's own selection flag (+0x40),
				// not the press byte — so a held button lights its plate with its text unchanged.
				DrawLabel(widget.Selected ? "DARK" : "WHITE", caption,
					widget.X0, widget.Y0, X(button.X1), Y(button.Y1), centered: true);
			}
		}

		switch (state.Mfd) {
			case MfdMode.Status:
				AddMfdStatusScreen(hud, blitDevice, DrawLabel, X, Y);
				break;
			case MfdMode.FlashComm:
				AddMfdFlashComm(strings, DrawLabel, insetX, insetY);
				break;
			case MfdMode.NavMap:
				// Its background is flooded above, before the buttons and title go down over it.
				break;
		}

		// The title goes down last, after the screen has painted — the repaint's own order, so a
		// screen that draws into the header strip cannot cover its own caption. Left-aligned, not
		// centred: the title passes alignment 1 where the button captions pass 2, and retail's own
		// "STATUS" starts 44 device pixels from the panel's left edge, which is this rect's left edge.
		if (MfdLayout.Title(strings, state.Mfd) is { Length: > 0 } title) {
			DrawLabel("WHITE", title,
				X(MfdLayout.TitleRect.X0), Y(MfdLayout.TitleRect.Y0),
				X(MfdLayout.TitleRect.X1), Y(MfdLayout.TitleRect.Y1), centered: false);
		}
	}

	/// <summary>
	/// The status screen shared by F1 and F5 (<c>0043a2e0</c>): five stacked labels down the left of
	/// the screen and the herc's damage wireframe in a viewport whose left edge is the labels' right
	/// edge.
	///
	/// <para>The wireframe is the herc's own paper-doll art — <c>hba\&lt;HERC&gt;.HBA</c> frame 2, the
	/// compact third view of the three its <c>.PDG</c> describes, at 48x82 device pixels against a
	/// 102x92 viewport. The original tints individual body regions by damage from the <c>.PDG</c>
	/// region list; with no damage model to drive that, this draws the undamaged frame whole.</para>
	/// </summary>
	private static void AddMfdStatusScreen(CockpitArt hud,
			Action<string, int, float, float> blitDevice, MfdLabelWriter drawLabel,
			Func<int, float> x, Func<int, float> y) {
		const float S = CockpitArt.GauToPixelScale;
		var strings = hud.Strings;

		// The five labels, in the constructor's own order: identifier caption, subject name, status
		// caption, damage state, and a structural-integrity readout the original formats at runtime.
		string?[] texts = {
			strings?.Text(MfdLayout.IdentLabelGroup, 0),
			strings?.Text(MfdLayout.SelfNameGroup, 0),
			strings?.Text(MfdLayout.StatusLabelGroup, 0),
			strings?.Text(MfdLayout.ConditionGroup, 0),
			MfdLayout.IntegrityReadout(0),
		};

		for (int i = 0; i < MfdLayout.StatusLabelY.Length; i++) {
			if (texts[i] is { Length: > 0 } text) {
				drawLabel(MfdLayout.StatusLabelFonts[i], text,
					x(MfdLayout.StatusLabelX), y(MfdLayout.StatusLabelY[i]),
					x(MfdLayout.WireframeRect.X0),
					y(MfdLayout.StatusLabelY[i] + MfdLayout.StatusLabelHeight),
					false, 0f);
			}
		}

		// The paper doll blits at the viewport's top-left plus the .PDG view's own origin plus a fixed
		// (0x11, 2) device nudge — the paint's own arithmetic, not a centring rule. The view's origin
		// is authored in the 320-wide space like every other .PDG coordinate, so it scales the same way.
		if (hud.PaperDoll?.Entries is { } views
			&& MfdLayout.WireframeViewIndex < views.Length
			&& views[MfdLayout.WireframeViewIndex] is { } view) {
			blitDevice(hud.HercName, MfdLayout.WireframeViewIndex,
				x(MfdLayout.WireframeRect.X0) + view.Origin.X * S + MfdLayout.WireframeArtOffset.X,
				y(MfdLayout.WireframeRect.Y0) + view.Origin.Y * S + MfdLayout.WireframeArtOffset.Y);
		}
	}

	/// <summary>
	/// FLASH COMM's order list (<c>0043f5d8</c>): six evenly stacked rows spanning almost the whole
	/// screen, listing the first six of the eighteen squadmate orders the string table holds. Rows are
	/// 7 GAU units apart and drawn in <c>CPGREEN</c>.
	///
	/// <para>The original highlights the row the cursor is on and re-fonts orders the squad cannot
	/// currently take; both need squad state, so every row draws available here.</para>
	/// </summary>
	private static void AddMfdFlashComm(SimStringTable? strings, MfdLabelWriter drawLabel,
			float insetX, float insetY) {
		var rows = MfdLayout.FlashCommRows;
		var orders = strings?.Group(MfdLayout.OrderGroup);
		if (orders == null) {
			return;
		}

		for (int i = 0; i < MfdLayout.FlashCommRowCount && i < orders.Count; i++) {
			if (orders[i].Text is { Length: > 0 } text) {
				float top = insetY + rows.Y0 + i * rows.RowHeight;
				drawLabel(MfdLayout.FlashCommFont, text,
					insetX + rows.X0, top, insetX + rows.X1, top + rows.RowHeight,
					false, MfdLayout.FlashCommTextMarginX);
			}
		}
	}

	/// <summary>
	/// Places one MFD label: a font, its text, the device-pixel rect it sits in, whether it centres
	/// horizontally, and how far its text is indented from the anchoring edge. Vertical centring is
	/// unconditional — see the implementation inside <see cref="AddMfd"/> for why.
	/// </summary>
	private delegate void MfdLabelWriter(string font, string text,
		float x0, float y0, float x1, float y1, bool centered, float marginX);

	/// <summary>
	/// The three console buttons: a <c>PWEAPONS</c> plate with a caption centred on it.
	///
	/// <para>The plate is not canopy art — <c>FUN_00442c88</c> (ConsoleButton_Paint) blits it per
	/// frame from <c>PWEAPONS</c> frames 2 and 3, indexed <c>bank[2 + state]</c>, at the widget's
	/// own rect. Frame 2 is the unlit plate (solid palette index 34, the blue the retail screenshot
	/// shows at RGB (77,77,182)) and frame 3 the lit one (index 14, green). Both are 50x16 against
	/// a 48x14 rect, the same one-pixel overhang the weapon-row plates have, and all three buttons 
	/// are that same 24x7 GAU size in every retail file.</para>
	///
	/// <para>The chain button's caption is its count in Roman numerals, read from DBSIM's own
	/// three-entry table at <c>0049c71c</c> ("I", "II", "III") — a literal table in <c>.rdata</c>,
	/// unrelated to the string file. LINK and TRACK are not fixed: <c>ConsoleButton_Paint</c>
	/// (<c>00442c88</c>) reads them out of <c>DAT_004d13d0</c>, the <c>.bss</c> array
	/// <c>SimStrings_LoadAll</c> fills from <c>STRINGS0.STR</c> group <see cref="CaptionGroup"/>,
	/// indexed by the widget's own kind field (1 = LINK, 2 = TRACK) — see
	/// docs/formats/str-strings.md.</para>
	/// </summary>
	private static void AddConsoleButtons(GAUFile gau, SimStringTable? strings, CockpitHudState state,
			Action<string, int, float, float> blit,
			Action<string, string, int, int, int, int, Vector3?> drawCentered) {
		const float S = CockpitArt.GauToPixelScale;

		void Button(string? text, WidgetBase? widget, bool lit = false) {
			if (widget == null) {
				return;
			}

			blit("PWEAPONS", lit ? 3 : 2, widget.Origin.X * S, widget.Origin.Y * S);
			if (text is { Length: > 0 }) {
				drawCentered("WHITE", text, widget.Origin.X, widget.Origin.Y,
					widget.Origin.X + widget.Size.Width, widget.Origin.Y + widget.Size.Height, null);
			}
		}

		// Firing chain and LINK light only while held; TRACK latches. ConsoleButton_Paint
		// (00442c88) takes the first two from the shared press byte and the third from its own flag.
		bool Held(ConsoleButton which) => state.PressedWidget == CockpitWidgetId.Console(which);

		Button(new string('I', Math.Clamp(state.ChainGroup + 1, 1, 3)), gau.ChainButton,
			Held(ConsoleButton.Chain));
		Button(strings?.Text(CaptionGroup, 1), gau.LinkButton, Held(ConsoleButton.Link));
		Button(strings?.Text(CaptionGroup, 2), gau.AutoTrackButton, state.AutoTrack);
	}

	/// <summary>
	/// <c>STRINGS0.STR</c> group 4: the console button captions — index 0 is <c>"I"</c> (unused; the
	/// chain button gets its numerals from <see cref="AddConsoleButtons"/>'s own table instead), 1 is
	/// <c>"LINK"</c>, 2 is <c>"TRACK"</c>, 3 is empty.
	/// </summary>
	private const int CaptionGroup = 4;

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
