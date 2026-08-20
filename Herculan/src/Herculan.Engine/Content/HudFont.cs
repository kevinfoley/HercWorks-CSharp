using System.Buffers.Binary;
using HercWorks.Core.Data.File.Dyn;

namespace Herculan.Engine.Content;

/// <summary>
/// How a label sits horizontally in its rect — DBSIM's own flag values, which
/// <c>Label_SetRect</c> tests as a bit mask (<c>&amp; 2</c> then <c>&amp; 4</c>, otherwise left).
/// There is no vertical counterpart: every label is vertically centred. See
/// <see cref="HudFont.Place"/>.
/// </summary>
public enum LabelAlign {
	/// <summary>Anchored to the rect's left edge. Retail's default, and what the MFD titles, the
	/// status labels and the order rows all pass.</summary>
	Left = 1,

	/// <summary>Centred in the rect — every button caption.</summary>
	Center = 2,

	/// <summary>Anchored to the rect's right edge.</summary>
	Right = 4,
}

/// <summary>
/// A <c>.DFN</c>/<c>.HFN</c> bitmap font — DBSIM's only HUD text mechanism. See
/// docs/formats/dfn-hfn-dci.md for the container; the glyph layout is:
///
/// <code>
/// 0x00 uint16 typeId = 0x0005      0x14 int16 G   -- baseline row (8 / 9)
/// 0x02 uint16        = 0x0028      0x16 int16     -- bits per pixel, 8 in every retail file
/// 0x04 uint32 totalSize            0x18 int16 I   -- 0 in every retail file
/// 0x08 int16  glyphCount           0x1a int16 inkHeight
/// 0x0a int16  B      -- 0          0x1c int16 K   -- array1 count, 0 in every retail file
/// 0x0c int16  firstCharCode = 32   0x1e uint32 L  -- glyph-pool byte length
/// 0x0e int16  cellHeight           0x22 [L bytes]           glyph pool
/// 0x10 int16  E      -- -1         .... [count x uint32]    pool offset per glyph
/// 0x12 int16  cellHeight (again)   .... [count bytes]       glyph width per glyph
/// </code>
///
/// <para>Each glyph is <c>width x cellHeight</c> bytes, row-major, one palette index per pixel: 0 is
/// transparent and every retail file uses exactly one other value as its ink — which is what makes
/// the 18 colour-scheme fonts (<c>WHITE</c> 30, <c>GRAY</c> 25, <c>GREEN</c> 14, <c>DARK</c> 19,
/// <c>RED</c> 10, <c>HUD1</c>/<c>2</c>/<c>3</c> 72/73/74, ...) copies of one typeface. A widget picks
/// its text colour by picking which of the loaded fonts to hand the label constructor
/// (<c>ColorSchemePanels</c>, <c>0049b0ac</c>), never by passing a colour.</para>
///
/// <para>The declared width is the advance, art included: glyph cells carry their own right-hand
/// spacing column, so runs are laid out by summing widths with no extra tracking. Verified against
/// all 54 retail font files — every glyph's pool slice is exactly <c>width * cellHeight</c> bytes,
/// with no exceptions.</para>
///
/// <para><c>.HFN</c> is the 640-wide mode's font (cell height 13, 217 glyphs) and <c>.DFN</c> the
/// 320-wide mode's (cell height 10, 223 glyphs); they are separate art, not a 2x scale of each other.
/// This engine renders the 640-wide cockpit, so <see cref="Load"/> reads <c>hfn\</c>.</para>
/// </summary>
public sealed class HudFont {
	/// <summary>Resource folder and extension for the 640-wide font set.</summary>
	public const string ResourceFolder = "hfn";

	private readonly DynamixBitmap[] _glyphs;

	private HudFont(string name, int firstCharCode, int cellHeight, int baseline, int inkHeight,
			DynamixBitmap[] glyphs) {
		Name = name;
		FirstCharCode = firstCharCode;
		CellHeight = cellHeight;
		Baseline = baseline;
		InkHeight = inkHeight;
		_glyphs = glyphs;
	}

	/// <summary>The font's resource name, e.g. <c>WHITE</c> — also its key in <see cref="HudSpriteSheet"/>.</summary>
	public string Name { get; }

	/// <summary>Character code of glyph 0. 32 (space) in every retail file.</summary>
	public int FirstCharCode { get; }

	/// <summary>Every glyph's cell height in pixels — 13 for <c>.HFN</c>.</summary>
	public int CellHeight { get; }

	/// <summary>Header field G: the row the glyph art sits on. Not used for layout, kept for reference.</summary>
	public int Baseline { get; }

	/// <summary>
	/// The height a label centres by — 11 in every <c>.HFN</c> against a 13-row cell, 8 in every
	/// <c>.DFN</c> against a 10-row one. <b>Not</b> <see cref="CellHeight"/>, which is what the glyph
	/// art occupies.
	///
	/// <para>Two functions read this field and only this one. <c>Label_SetRect</c> (<c>00438884</c>)
	/// puts a label's anchor at <c>rectCentreY + (ink &gt;&gt; 1) + margin + 1</c>, and the glyph
	/// blitter (<c>FUN_00482428</c>) draws each glyph with its top row at <c>anchor - ink</c>. Between
	/// them the ink-tall band is what gets centred in the rect, and the two rows of cell past it hang
	/// below as descender space. Centring the full cell instead sits every label a pixel and a half
	/// high, which is what this field being read as the cell height used to do here.</para>
	/// </summary>
	public int InkHeight { get; }

	/// <summary>Glyphs in character order, as bitmaps ready to pack into a texture atlas.</summary>
	public IReadOnlyList<DynamixBitmap> Glyphs => _glyphs;

	/// <summary>Glyph index for <paramref name="c"/>, or null when the font has no glyph for it.</summary>
	public int? GlyphIndex(char c) {
		int index = c - FirstCharCode;
		return index >= 0 && index < _glyphs.Length ? index : null;
	}

	/// <summary>Advance width of <paramref name="c"/> in pixels; 0 when the font has no glyph for it.</summary>
	public int Width(char c) => GlyphIndex(c) is { } index ? _glyphs[index].Cols : 0;

	/// <summary>Total advance width of <paramref name="text"/> in pixels.</summary>
	public int Measure(string text) {
		int total = 0;
		foreach (char c in text) {
			total += Width(c);
		}

		return total;
	}

	/// <summary>
	/// Device pixels a run's measured width is trimmed by before it is aligned — the original's
	/// <c>1 &lt;&lt; XCoordShift</c>, which drops the trailing advance past the last glyph so a
	/// centred or right-aligned run sits on its ink rather than on its spacing column.
	/// </summary>
	public const int TrailingAdvanceTrim = 1 << CockpitViewGeometry.CoordShift;

	/// <summary>
	/// Where a run of text lands inside a rect — the top-left device pixel of its first glyph — as
	/// <c>Label_SetRect</c> (<c>00438884</c>) and <c>Label_SetText</c> (<c>00438920</c>) place it
	/// between them. Every HUD label in the game goes through that pair, so this is the one placement
	/// rule the cockpit, the MFD and the Heads-Down Display all share.
	///
	/// <list type="bullet">
	/// <item>Horizontally the alignment flag picks an anchor — the rect's centre, its right edge, or
	/// its left edge — offset by <paramref name="marginX"/>, and the trimmed run is then placed
	/// against it.</item>
	/// <item>Vertically there is no flag: the anchor is always
	/// <c>rectCentre + (InkHeight &gt;&gt; 1) + marginY + 1</c>, and the glyph blitter draws from
	/// <c>anchor - InkHeight</c>. So it is <see cref="InkHeight"/> that gets centred, not
	/// <see cref="CellHeight"/>.</item>
	/// </list>
	///
	/// <para>All of it is integer arithmetic in the original, including the two <c>&gt;&gt; 1</c>s.
	/// Doing it in floating point instead shifts a label by up to a pixel on either axis, which is
	/// visible at this art's scale.</para>
	/// </summary>
	public (int X, int Y) Place(string text, int x0, int y0, int x1, int y1, LabelAlign align,
			int marginX = 0, int marginY = 0) {
		int anchorX = align switch {
			LabelAlign.Center => ((x1 - x0) >> 1) + x0 + marginX,
			LabelAlign.Right => x1 - marginX,
			_ => x0 + marginX,
		};

		int width = Measure(text) - TrailingAdvanceTrim;
		int textX = align switch {
			LabelAlign.Center => anchorX - (width >> 1),
			LabelAlign.Right => anchorX - width,
			_ => anchorX,
		};

		int anchorY = ((y1 - y0) >> 1) + y0 + (InkHeight >> 1) + marginY + 1;
		return (textX, anchorY - InkHeight);
	}

	/// <summary>
	/// Loads and parses <c>hfn\&lt;name&gt;.HFN</c>. Returns null when the resource is missing or does
	/// not parse as a panel resource — callers draw no text rather than substituting another font,
	/// since in this format the font *is* the colour.
	/// </summary>
	public static HudFont? Load(GameContent content, string name) {
		if (content.Read(ResourceFolder, name + "." + ResourceFolder.ToUpperInvariant()) is not { } bytes
			|| bytes.Length < 34) {
			return null;
		}

		short I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));

		int glyphCount = I16(0x08);
		int firstCharCode = I16(0x0c);
		int cellHeight = I16(0x0e);
		int baseline = I16(0x14);
		int inkHeight = I16(0x1a);
		int poolLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x1e));

		int poolStart = 0x22;
		int offsetsStart = poolStart + poolLength;
		int widthsStart = offsetsStart + glyphCount * 4;
		if (glyphCount <= 0 || cellHeight <= 0 || poolLength <= 0
			|| widthsStart + glyphCount > bytes.Length) {
			return null;
		}

		var glyphs = new DynamixBitmap[glyphCount];
		for (int i = 0; i < glyphCount; i++) {
			int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offsetsStart + i * 4));
			int end = i + 1 < glyphCount
				? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offsetsStart + (i + 1) * 4))
				: poolLength;
			int width = bytes[widthsStart + i];

			// The width byte and the slice length are two independent statements of the same fact in
			// every retail file. Trusting the slice keeps a malformed file from reading past its pool.
			int length = end - start;
			if (start < 0 || length < 0 || start + length > poolLength || length != width * cellHeight) {
				return null;
			}

			var pixels = new byte[length];
			Array.Copy(bytes, poolStart + start, pixels, 0, length);
			glyphs[i] = new DynamixBitmap {
				Rows = (short)cellHeight,
				Cols = (short)width,
				BitDepth = 8,
				ImageDataLen = length,
				ImageData = pixels,
			};
		}

		return new HudFont(name, firstCharCode, cellHeight, baseline, inkHeight, glyphs);
	}
}
