using Herculan.Engine.Content;

namespace Herculan.Engine.Shell;

/// <summary>
/// How a run of text sits in its rect — the value a <c>Text</c> widget carries at <c>+0x45</c> and
/// hands the string blitter through the draw context's <c>+0x231</c>. <c>FUN_0045409c</c> tests it for
/// 1 and then for 2 and otherwise leaves the pen where it is, so the three cases are exactly these and
/// left is the default rather than a flag of its own.
/// </summary>
public enum ShellTextAlign {
	/// <summary>Anchored to the rect's left edge — every free-text value on the save screen.</summary>
	Left = 0,

	/// <summary>Anchored to the right edge — every field label, so a label's colon meets its value.</summary>
	Right = 1,

	/// <summary>Centred — every button caption, every panel title, and the numbers in a column.</summary>
	Center = 2,
}

/// <summary>
/// The shell's widget paints, ported one function each. Together they are the whole visual vocabulary
/// of a VSHELL screen: a flat panel, a bordered box, a titled group box, a label and an edit field.
/// The screen builders place them and set their colour fields; the derivation and the field offsets
/// are in docs/shell/screen-layout.md.
///
/// <para><b>Widget-local coordinates run 0 to <c>Width - 1</c>.</b> Every paint here works in the
/// original's own terms, where the extent it draws against is <c>+0x2d - +0x25</c> — the difference of
/// the widget's two horizontal corners, so one less than the inclusive width. Because it is a
/// difference it is the same whether the rect is the parent-relative one at <c>+0x25</c> or the
/// absolute one at <c>+0x15</c>, which is why no paint reads an origin: the origin arrives with the
/// drawing context. Keeping the arithmetic as-is is what lets each off-by-one below be checked against
/// the decompilation rather than reasoned about — the border sits at 0 and <c>W</c>, the fill runs 1 to
/// <c>W - 1</c>, and the four corners the border skips are painted back individually.</para>
///
/// <para><b>Colour is an index, never an RGB.</b> A paint writes palette indices into a
/// <see cref="ShellSurface"/> and the palette resolves them once at the end, which is what the
/// original's hardware palette does for it.</para>
/// </summary>
public static class ShellChrome {
	/// <summary>
	/// The index every panel clears its interior to before drawing anything else — a literal <c>0x10</c>
	/// in all four paints rather than a field, so the shell has one background colour and the widget
	/// chooses only what goes over it.
	/// </summary>
	public const byte InteriorColor = 0x10;

	/// <summary>
	/// The ink index the shell's fonts are drawn in, and so the index every text paint remaps to pick
	/// its colour. See <see cref="ShellSurface.Remap"/>.
	/// </summary>
	public const byte FontInkColor = 0x29;

	/// <summary>
	/// <c>FUN_0040a726</c> — the base fill and border under <see cref="PaintFramedPanel"/> and
	/// <see cref="PaintTitledPanel"/>, and the whole of <c>Panel_Paint</c> on its own.
	///
	/// <para>The border is a chamfer, not a rectangle: each of the four edges stops one pixel short at
	/// both ends, leaving the true corners empty, and then the four pixels one step <i>inside</i> each
	/// corner are painted instead. That is what gives every box in the shell its clipped corners.</para>
	///
	/// <para>Nothing is drawn at all when the widget's <c>+0x51</c> is clear, which is how each screen's
	/// backdrop-textured root draws its bitmap and no chrome.</para>
	/// </summary>
	/// <param name="fill">The widget's <c>+0x59</c>: whether to clear the interior first.</param>
	public static void PaintPanel(ShellSurface surface, ShellRect rect, byte borderColor, bool fill) {
		int w = rect.Width - 1;
		int h = rect.Height - 1;
		var clip = surface.PushClip(rect);

		if (fill) {
			Fill(surface, rect, 1, 1, w - 1, h - 1, InteriorColor);
		}

		Line(surface, rect, 1, 0, w - 1, 0, borderColor);
		Line(surface, rect, w, 1, w, h - 1, borderColor);
		Line(surface, rect, w - 1, h, 1, h, borderColor);
		Line(surface, rect, 0, h - 1, 0, 1, borderColor);
		PaintCorners(surface, rect, borderColor);
		surface.PopClip(clip);
	}

	/// <summary>
	/// <c>FramedPanel_Paint</c> — a plain bordered box: the interior cleared, the chamfered border, then
	/// a 50% checkerboard over the interior in <paramref name="faceColor"/>.
	///
	/// <para>The save screen sets that face colour to <see cref="InteriorColor"/> on all three of its
	/// framed panels, so retail's dither there lands the same colour it is dithering over and the box
	/// reads as flat. The class default is <c>0x25</c>, which does not.</para>
	/// </summary>
	public static void PaintFramedPanel(ShellSurface surface, ShellRect rect, byte borderColor,
			byte faceColor, bool fill) {
		int w = rect.Width - 1;
		int h = rect.Height - 1;
		var clip = surface.PushClip(rect);

		if (fill) {
			Fill(surface, rect, 1, 1, w - 1, h - 1, InteriorColor);
		}

		PaintPanel(surface, rect, borderColor, fill);
		Dither(surface, rect, firstRow: 1, faceColor);
		PaintCorners(surface, rect, borderColor);
		surface.PopClip(clip);
	}

	/// <summary>
	/// <c>TitledPanel_Paint</c> — the group box every tab screen's content sits in: a header strip with
	/// a diagonal hatch and a clear plate for its title, a divider under it, and a body that is either
	/// filled or dithered.
	///
	/// <para><b>The body is dithered when it is not filled.</b> The two are alternatives on the same
	/// flag: <paramref name="fill"/> set clears the body to <see cref="InteriorColor"/>, and clear
	/// leaves it to a 50% checkerboard in <paramref name="bodyDitherColor"/> — which over an untouched
	/// surface means the shell's one backdrop bitmap shows through at half strength. The save screen
	/// takes the second path.</para>
	/// </summary>
	/// <param name="headerHeight">
	/// The widget's <c>+0x61</c>, and the constructor's last argument: how tall the header strip is, and
	/// also the row the divider lands on. 19 on the save screen, 20 for the subclass at
	/// <c>FUN_0040afe0</c>.
	/// </param>
	/// <param name="headerChrome">
	/// The widget's <c>+0x65</c>, set by the constructor and never cleared: whether to draw the hatch,
	/// the title plate and the header's own side edges. Without it the strip is a flat band.
	/// </param>
	/// <param name="plateFirst">The widget's <c>+0x6d</c> — where the title's clear plate starts.</param>
	/// <param name="plateLast">And its <c>+0x71</c>, where the plate ends.</param>
	public static void PaintTitledPanel(ShellSurface surface, ShellRect rect, byte borderColor,
			byte faceColor, byte bodyDitherColor, int headerHeight, bool headerChrome,
			int plateFirst, int plateLast, bool fill) {
		int w = rect.Width - 1;
		int h = rect.Height - 1;
		var clip = surface.PushClip(rect);

		if (fill) {
			Fill(surface, rect, 1, 1, w - 1, h - 1, InteriorColor);
		}

		PaintPanel(surface, rect, borderColor, fill);

		// The header strip, drawn as its own run of horizontal lines rather than as a rectangle — the
		// original's loop, and the reason the strip starts at row 1 and stops one row short of the
		// divider.
		for (int y = 1; y < headerHeight; y++) {
			Line(surface, rect, 1, y, w - 1, y, faceColor);
		}

		if (headerChrome) {
			PaintHeaderHatch(surface, rect, headerHeight);

			Line(surface, rect, w, 1, w, headerHeight, borderColor);
			Line(surface, rect, 0, 1, 0, headerHeight, borderColor);

			// The title's plate: the hatch punched back out to the face colour across a fixed span, so
			// the caption reads against flat ground. Its span is a pair of widget fields rather than
			// anything derived from the string, and on the save screen it is centred on the panel to
			// within half a pixel.
			for (int y = 1; y < headerHeight; y++) {
				Line(surface, rect, plateFirst, y, plateLast, y, faceColor);
			}
		}

		if (!fill) {
			Dither(surface, rect, headerHeight, bodyDitherColor);
		}

		PaintCorners(surface, rect, borderColor);
		Line(surface, rect, 0, headerHeight, w, headerHeight, borderColor);
		surface.PopClip(clip);
	}

	/// <summary>
	/// <c>Text_Paint</c> (<c>FUN_0040b439</c>) — one label or readout.
	///
	/// <para><paramref name="backingColor"/> is the widget's <c>+0xc1</c>/<c>+0xc5</c> pair: a value
	/// field clears its own rect first so a refresh overwrites cleanly, and a static label does not, so
	/// it draws straight over whatever the panel put down. Passing null is the label case.</para>
	///
	/// <para>The vertical placement is the original's arithmetic, integer division included, and it
	/// differs from the edit field's below by a pixel or two — both are reproduced as written rather
	/// than unified, since which one a widget uses is a property of its class.</para>
	/// </summary>
	public static void PaintText(ShellSurface surface, ShellRect rect, HudFont? font, string? text,
			ShellTextAlign align, byte color, byte? backingColor = null) {
		int w = rect.Width - 1;
		int h = rect.Height - 1;
		var clip = surface.PushClip(rect);

		if (backingColor is { } backing) {
			Fill(surface, rect, 0, 1, w - 1, h - 1, backing);
		}

		if (font != null && !string.IsNullOrEmpty(text)) {
			// baseline = H - (H + 1 - cellHeight) / 2 - 2, and the glyph's top row is inkHeight above it
			// (FUN_00453fb4 subtracts the font's +0x16, which is the .DFN header's inkHeight field).
			int baseline = h - (h + 1 - font.CellHeight) / 2 - 2;
			DrawString(surface, font, text, rect.X0, rect.Y0 + baseline, align, w);
		}

		surface.Remap(rect.X0, rect.Y0 + 1, rect.X0 + w, rect.Y0 + h - 1, FontInkColor, color);
		surface.PopClip(clip);
	}

	/// <summary>
	/// <c>FUN_0040c14f</c> — the editable text field the save screen's ten slot rows are, painted whole:
	/// its rect cleared, its string drawn left-aligned one pixel in, an optional caret block, and the
	/// ink remapped to <paramref name="color"/>.
	///
	/// <para>The rows carry a permitted-character set at <c>+0x9f</c> and so are genuinely editable in
	/// retail — renaming a slot is typing into one. Nothing here types yet.</para>
	/// </summary>
	/// <param name="caret">
	/// The widget's <c>+0xa7</c> and <c>+0xb3</c> together: whether the field has the keyboard and
	/// whether it shows a caret at all. The save screen clears <c>+0xb3</c> on every row, so its rows
	/// never show one.
	/// </param>
	public static void PaintEditField(ShellSurface surface, ShellRect rect, HudFont? font, string? text,
			byte color, bool caret = false) {
		int w = rect.Width - 1;
		int h = rect.Height - 1;
		var clip = surface.PushClip(rect);

		Fill(surface, rect, 0, 0, w, h, InteriorColor);

		if (font != null && !string.IsNullOrEmpty(text)) {
			// The edit field's own centring: half the font's cell height plus half the rect's, which is
			// not the same expression Text_Paint uses.
			int baseline = font.CellHeight / 2 + (h + 1) / 2;
			DrawString(surface, font, text, rect.X0 + 1, rect.Y0 + baseline, ShellTextAlign.Left, w + 1);

			if (caret) {
				int pen = font.Measure(text);
				Fill(surface, rect, pen, baseline - 2, pen + 6, baseline, CaretColor);
			}
		}

		surface.Remap(rect.X0 + 1, rect.Y0 + 1, rect.X0 + w - 1, rect.Y0 + h - 1, FontInkColor, color);
		surface.PopClip(clip);
	}

	/// <summary>The caret block's colour, a literal in the edit field's paint.</summary>
	private const byte CaretColor = 0x27;

	/// <summary>
	/// The header's diagonal hatch: bands of fourteen 45-degree lines on a 28-pixel pitch, so half the
	/// strip is hatched and half is left as the face colour. It starts five pixels left of the panel and
	/// runs 26 bands, well past any panel this shell builds — the paint relies on clipping rather than
	/// measuring, which is why <see cref="ShellSurface"/>'s primitives clip.
	/// </summary>
	private static void PaintHeaderHatch(ShellSurface surface, ShellRect rect, int headerHeight) {
		for (int band = 0; band < HatchBands; band++) {
			int start = HatchFirstX + band * HatchPitch;
			for (int i = 0; i < HatchLinesPerBand; i++) {
				int x = start + i;
				Line(surface, rect, x, headerHeight - 1, x + headerHeight - 2, 1, HatchColor);
			}
		}
	}

	private const byte HatchColor = 0x0d;
	private const int HatchFirstX = -5;
	private const int HatchLinesPerBand = 14;
	private const int HatchPitch = HatchLinesPerBand * 2;
	private const int HatchBands = 26;

	/// <summary>
	/// The 50% checkerboard both the framed and the titled panel use, from
	/// <paramref name="firstRow"/> down to the bottom border. The phase flips every row, and the row it
	/// starts on is the only difference between the two callers.
	/// </summary>
	private static void Dither(ShellSurface surface, ShellRect rect, int firstRow, byte color) {
		int w = rect.Width - 1;
		int h = rect.Height - 1;
		int phase = 0;

		for (int y = firstRow; y < h; y++) {
			for (int x = phase + 1; x < w; x += 2) {
				surface.Plot(rect.X0 + x, rect.Y0 + y, color);
			}

			phase = 1 - phase;
		}
	}

	/// <summary>The four pixels one step inside each corner, which the chamfered border leaves out.</summary>
	private static void PaintCorners(ShellSurface surface, ShellRect rect, byte color) {
		int w = rect.Width - 1;
		int h = rect.Height - 1;
		surface.Plot(rect.X0 + 1, rect.Y0 + 1, color);
		surface.Plot(rect.X0 + w - 1, rect.Y0 + 1, color);
		surface.Plot(rect.X0 + w - 1, rect.Y0 + h - 1, color);
		surface.Plot(rect.X0 + 1, rect.Y0 + h - 1, color);
	}

	/// <summary>
	/// <c>FUN_0045409c</c> — a run of glyphs, aligned in a field of <paramref name="fieldWidth"/> and
	/// then advanced glyph by glyph. <paramref name="baselineY"/> is the ink baseline; each glyph's top
	/// row lands <see cref="HudFont.InkHeight"/> above it.
	/// </summary>
	private static void DrawString(ShellSurface surface, HudFont font, string text, int left,
			int baselineY, ShellTextAlign align, int fieldWidth) {
		int pen = align switch {
			ShellTextAlign.Right => left + (fieldWidth - font.Measure(text)),
			ShellTextAlign.Center => left + ((fieldWidth - font.Measure(text)) >> 1),
			_ => left,
		};

		int top = baselineY - font.InkHeight;
		foreach (char c in text) {
			if (font.GlyphIndex(c) is { } glyph && glyph < font.Glyphs.Count) {
				surface.Blit(font.Glyphs[glyph], pen, top);
			}

			pen += font.Width(c);
		}
	}

	private static void Fill(ShellSurface surface, ShellRect rect, int x0, int y0, int x1, int y1,
			byte color) =>
		surface.Fill(rect.X0 + x0, rect.Y0 + y0, rect.X0 + x1, rect.Y0 + y1, color);

	private static void Line(ShellSurface surface, ShellRect rect, int x0, int y0, int x1, int y1,
			byte color) =>
		surface.Line(rect.X0 + x0, rect.Y0 + y0, rect.X0 + x1, rect.Y0 + y1, color);
}
