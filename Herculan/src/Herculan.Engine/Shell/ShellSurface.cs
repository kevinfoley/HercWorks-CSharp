using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.Struct;

namespace Herculan.Engine.Shell;

/// <summary>
/// An 8-bit indexed drawing surface, the size of the shell canvas — what VSHELL's widget paints draw
/// into, ported as it stands rather than translated into quads.
///
/// <para><b>Why indexed and not RGBA.</b> Every shell paint works in palette indices and one of them
/// depends on it: both <c>Text</c> and the edit field colour their glyphs by installing an identity
/// 256-entry lookup table with entry <c>0x29</c> replaced, then re-blitting the area through it
/// (<see cref="Remap"/>) — the font's ink index recoloured after the fact. A surface that had already
/// resolved indices to colours could not do that. Working in indices also makes the checkerboard
/// dither and the title bar's diagonal hatch exact instead of approximated, and costs one texture
/// upload per repaint instead of tens of thousands of quads.</para>
///
/// <para><b>Index 0 is transparent</b>, the same sentinel the sprite banks use, and the surface starts
/// filled with it. That is load-bearing rather than a convenience: a <c>TitledPanel</c> whose body
/// fill is switched off dithers its body in a single colour and leaves the alternate pixels
/// untouched, so the backdrop showing through at 50% is the effect the original gets from not
/// painting them. See docs/shell/screen-layout.md.</para>
///
/// <para>Every primitive clips to the surface, so a widget may draw outside its own rect or off the
/// canvas without a bounds check of its own — which the title bar's hatch does, starting five pixels
/// left of the panel.</para>
/// </summary>
public sealed class ShellSurface {
	/// <summary>The palette index that means "leave this pixel alone", in the banks and here.</summary>
	public const byte Transparent = 0;

	private readonly byte[] _indices;

	public ShellSurface(int width = ShellLayout.CanvasWidth, int height = ShellLayout.CanvasHeight) {
		Width = Math.Max(width, 0);
		Height = Math.Max(height, 0);
		_indices = new byte[Width * Height];
		ClipRect = new ShellRect(0, 0, Width - 1, Height - 1);
	}

	public int Width { get; }

	public int Height { get; }

	/// <summary>
	/// What every primitive is clipped to. It starts as the whole surface and each widget's paint
	/// narrows it to that widget for the duration — <c>FUN_0041f585</c>, the first call in all five
	/// paints, which binds the drawing context to the widget being painted.
	///
	/// <para><b>The paints depend on this rather than measuring.</b> The title bar's diagonal hatch is
	/// the clearest case: it lays down 26 bands on a 28-pixel pitch starting five pixels left of the
	/// widget, which is over 700 pixels of hatch for a panel 357 wide, and it is the clip and nothing
	/// else that stops the surplus reaching the canvas.</para>
	/// </summary>
	public ShellRect ClipRect { get; private set; }

	/// <summary>
	/// Narrows the clip to the intersection of <paramref name="rect"/> and the current one, and returns
	/// what it was so the caller can put it back with <see cref="PopClip"/>.
	/// </summary>
	public ShellRect PushClip(ShellRect rect) {
		var previous = ClipRect;
		ClipRect = new ShellRect(
			Math.Max(rect.X0, previous.X0), Math.Max(rect.Y0, previous.Y0),
			Math.Min(rect.X1, previous.X1), Math.Min(rect.Y1, previous.Y1));
		return previous;
	}

	/// <summary>Restores a clip <see cref="PushClip"/> returned.</summary>
	public void PopClip(ShellRect previous) => ClipRect = previous;

	/// <summary>Whether nothing has been drawn since the last <see cref="Clear"/>.</summary>
	public bool IsBlank {
		get {
			foreach (byte index in _indices) {
				if (index != Transparent) {
					return false;
				}
			}

			return true;
		}
	}

	/// <summary>Returns every pixel to <see cref="Transparent"/>.</summary>
	public void Clear() => Array.Clear(_indices);

	/// <summary>The index at a pixel, or <see cref="Transparent"/> when it is off-surface.</summary>
	public byte At(int x, int y) =>
		x >= 0 && y >= 0 && x < Width && y < Height ? _indices[y * Width + x] : Transparent;

	/// <summary>One pixel — <c>FUN_0045999c</c>, which is what every dither and hatch is built from.</summary>
	public void Plot(int x, int y, byte index) {
		if (ClipRect.Contains(x, y)) {
			_indices[y * Width + x] = index;
		}
	}

	/// <summary>
	/// A solid rectangle, corners inclusive — <c>FUN_00457364</c>. The corners are normalized first,
	/// as the callers do for themselves before every call: each one sorts its two x and its two y so a
	/// widget too small for its own inset still fills something rather than nothing.
	/// </summary>
	public void Fill(int x0, int y0, int x1, int y1, byte index) {
		int left = Math.Max(Math.Min(x0, x1), ClipRect.X0);
		int top = Math.Max(Math.Min(y0, y1), ClipRect.Y0);
		int right = Math.Min(Math.Max(x0, x1), ClipRect.X1);
		int bottom = Math.Min(Math.Max(y0, y1), ClipRect.Y1);
		if (right < left || bottom < top) {
			return;
		}

		for (int y = top; y <= bottom; y++) {
			_indices.AsSpan(y * Width + left, right - left + 1).Fill(index);
		}
	}

	/// <summary>
	/// A one-pixel line between two inclusive endpoints — <c>FUN_004552e4</c>. The shell asks for
	/// horizontal, vertical and 45-degree runs only; Bresenham covers all three without a special case.
	/// </summary>
	public void Line(int x0, int y0, int x1, int y1, byte index) {
		int dx = Math.Abs(x1 - x0);
		int dy = Math.Abs(y1 - y0);
		int stepX = x0 < x1 ? 1 : -1;
		int stepY = y0 < y1 ? 1 : -1;
		int error = dx - dy;

		while (true) {
			Plot(x0, y0, index);
			if (x0 == x1 && y0 == y1) {
				return;
			}

			int doubled = error * 2;
			if (doubled > -dy) {
				error -= dy;
				x0 += stepX;
			}

			if (doubled < dx) {
				error += dx;
				y0 += stepY;
			}
		}
	}

	/// <summary>
	/// Replaces one index with another over a rectangle — the blit mode 6 both text paints finish with,
	/// where the table they install is the identity apart from a single entry.
	///
	/// <para>This is how a shell widget picks its text colour. The fonts carry one ink index each (see
	/// <see cref="Content.HudFont"/>) and the shell's is <c>0x29</c>, so a widget draws its string in
	/// whatever the font has and then remaps that one index to the colour it actually wants — which is
	/// why the same font serves a label, a value and a highlighted row.</para>
	/// </summary>
	public void Remap(int x0, int y0, int x1, int y1, byte from, byte to) {
		if (from == to) {
			return;
		}

		int left = Math.Max(Math.Min(x0, x1), ClipRect.X0);
		int top = Math.Max(Math.Min(y0, y1), ClipRect.Y0);
		int right = Math.Min(Math.Max(x0, x1), ClipRect.X1);
		int bottom = Math.Min(Math.Max(y0, y1), ClipRect.Y1);

		for (int y = top; y <= bottom; y++) {
			for (int x = left; x <= right; x++) {
				int at = y * Width + x;
				if (_indices[at] == from) {
					_indices[at] = to;
				}
			}
		}
	}

	/// <summary>
	/// One glyph, or any other indexed bitmap, at a top-left corner. Source index 0 is left alone, so a
	/// glyph shows only its ink and whatever is under it stays.
	/// </summary>
	public void Blit(DynamixBitmap bitmap, int left, int top) {
		byte[] source = bitmap.ImageData ?? Array.Empty<byte>();
		int cols = bitmap.Cols;
		int rows = bitmap.Rows;

		for (int y = 0; y < rows; y++) {
			for (int x = 0; x < cols; x++) {
				int at = y * cols + x;
				if (at < source.Length && source[at] != Transparent) {
					Plot(left + x, top + y, source[at]);
				}
			}
		}
	}

	/// <summary>
	/// Resolves the surface through <paramref name="palette"/> into RGBA8, top row first, with
	/// <see cref="Transparent"/> becoming alpha 0 so the backdrop behind it shows through. An index the
	/// palette does not carry comes out as opaque grey of that index's value, the same fallback
	/// <see cref="ShellArt"/> uses for a bitmap, so a wrong index reads as a wrong shade rather than as
	/// nothing at all.
	/// </summary>
	public ShellImage ToImage(DynamixPalette palette) {
		var pixels = new byte[Width * Height * 4];
		for (int i = 0; i < _indices.Length; i++) {
			byte index = _indices[i];
			if (index == Transparent) {
				continue;
			}

			var color = palette.Colors.TryGetValue(index, out var entry)
				? entry.GetColor()
				: new RgbaColor(255, index, index, index);

			pixels[i * 4] = color.R;
			pixels[i * 4 + 1] = color.G;
			pixels[i * 4 + 2] = color.B;
			pixels[i * 4 + 3] = 255;
		}

		return new ShellImage(pixels, Width, Height);
	}
}
