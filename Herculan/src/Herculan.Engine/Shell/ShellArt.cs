using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Io.Transform.Common;
using Herculan.Engine.Content;

namespace Herculan.Engine.Shell;

/// <summary>One decoded shell bitmap, RGBA8, top row first — CPU-side, no GL.</summary>
public sealed record ShellImage(byte[] Pixels, int Width, int Height);

/// <summary>
/// Everything the shell draws with: its palette, the backdrop its screens sit on, its button art and
/// its fonts. The counterpart to <see cref="CockpitArt"/> on the other side of the two-process split
/// (see docs/shell/campaign-loop.md) — VSHELL and DBSIM share the resource container formats and
/// share almost nothing else.
///
/// <para><b>The shell's <c>dba\</c> is not DBSIM's <c>dba\</c>.</b> In the simulator archives that
/// folder holds the 320-wide half of each sprite bank, with <c>hba\</c> carrying the 640-wide half
/// (<see cref="HudSpriteSheet"/>). <c>SHELL0.VOL</c> ships one set only, and its screens are authored
/// at 640x480 — the full-screen panel rect is <c>{0, 0, 0x27f, 0x1df}</c> — so shell art is drawn at
/// its own size with no doubling. The same goes for <c>dfn\</c>: the shell has no <c>hfn\</c> half,
/// and its fonts are the text at canvas scale rather than a half-scale set to double.</para>
///
/// <para><b>The palette is inferred, not read.</b> VSHELL selects one by index into a pointer table
/// at <c>0046dcdc</c> whose first entry is <c>dpl\intr_pt1.dpl</c>, and neither the table's contents
/// nor the rest of its indices have been decoded. Index 1 is what the shell installs on entry
/// (<c>esglobal.cpp</c>, <c>004073bc</c>) and what the service bay re-installs whenever it is shown
/// (<c>0043b23d</c>), and <c>dpl\bay.dpl</c> is the archive's own name for that screen's palette —
/// hence <see cref="DefaultPaletteName"/>. Pass another name to <see cref="Load"/> to try one of the
/// archive's other palettes.</para>
/// </summary>
public sealed class ShellArt {
	/// <summary>
	/// The archives the shell reads. <c>SHELL0.VOL</c> carries the art, the fonts and the catalogs;
	/// <c>LANG0.VOL</c> carries the <c>.BIN</c> string tables, including <see cref="Text"/>'s.
	/// </summary>
	public static readonly string[] Archives = { "SHELL0.VOL", "LANG0.VOL" };

	/// <summary>See the class remarks — inferred from the palette index the shell and the bay both select.</summary>
	public const string DefaultPaletteName = "BAY";

	/// <summary>
	/// The backdrop every tab screen's root widget is textured with, loaded once by the shell's global
	/// init into <c>DAT_0046dcd4</c> and handed to each screen builder as its root's image.
	/// </summary>
	public const string BackdropName = "BAY2A_84";

	/// <summary>Resource folder for the shell's sprite banks, and their extension.</summary>
	public const string BankFolder = "dba";

	/// <summary>Resource folder for the shell's fonts, and their extension.</summary>
	public const string FontFolder = "dfn";

	/// <summary>Resource folder for the shell's single-frame bitmaps.</summary>
	public const string BitmapFolder = "dbm";

	/// <summary>
	/// The bank every framed button on the tab strip draws: frame 1 unlit, frame 2 lit, frame 3 the
	/// disabled plate every button on the strip shares.
	/// </summary>
	public const string ButtonBank = "MNU_BTTN";

	/// <summary>The two-frame bank the strip's leftmost square button draws instead.</summary>
	public const string MenuButtonBank = "ONLINE";

	/// <summary>Banks loaded for the shell frame. Screen-specific banks are loaded by their screens.</summary>
	public static readonly string[] BankNames = { ButtonBank, MenuButtonBank, "CURSOR" };

	/// <summary>
	/// The three fonts the shell's global init loads and keeps for the whole session — <c>FONT2</c>
	/// twice into two separate handles (<c>0046dcc4</c> and <c>0046dcc8</c>) and <c>BLACK</c> into a
	/// third (<c>0046dccc</c>), which is the one every tab caption is drawn in. <c>FONT</c> is in the
	/// archive and is loaded here alongside them, though the init does not ask for it.
	/// </summary>
	public static readonly string[] FontNames = { "FONT", "FONT2", "BLACK" };

	/// <summary>The font tab captions are drawn in — <c>DAT_0046dccc</c>, the handle each strip button is given.</summary>
	public const string ButtonFont = "BLACK";

	private ShellArt(DynamixPalette palette, ShellImage backdrop, HudSpriteSheet? sprites, ShellText? text) {
		Palette = palette;
		Backdrop = backdrop;
		Sprites = sprites;
		Text = text;
	}

	/// <summary>The palette every image and glyph here was decoded through.</summary>
	public DynamixPalette Palette { get; }

	/// <summary>
	/// The backdrop, at its own size — 640x480 in the retail archive, exactly the canvas, so it lands
	/// as one quad covering the screen.
	/// </summary>
	public ShellImage Backdrop { get; }

	/// <summary>
	/// The shell's sprite banks and fonts, packed into one atlas so the whole frame costs a single
	/// texture bind. Null when none of them could be loaded, in which case the backdrop draws alone.
	///
	/// <para>Palette index 0 decodes to alpha 0 in everything packed here, the sentinel role it plays
	/// in the simulator's banks too. It is the reading the shell's own art is assumed to share; a plate
	/// that spent index 0 as ink rather than as a cutout would show the backdrop through itself.</para>
	/// </summary>
	public HudSpriteSheet? Sprites { get; }

	/// <summary>The shell's UI text, or null when <c>LANG0.VOL</c> is not mounted.</summary>
	public ShellText? Text { get; }

	/// <summary>
	/// Loads the shell's shared art. Returns null when the palette or the backdrop is missing, since
	/// neither has a sensible substitute; a missing bank, font or string table is survivable and comes
	/// back as a null property instead.
	/// </summary>
	public static ShellArt? Load(GameContent content, string? paletteName = null) {
		if (ReadPalette(content, paletteName ?? DefaultPaletteName) is not { } palette
			|| LoadImage(content, BitmapFolder, BackdropName + ".DBM", palette) is not { } backdrop) {
			return null;
		}

		return new ShellArt(palette, backdrop,
			HudSpriteSheet.Load(content, palette, BankNames, FontNames,
				resourceFolder: BankFolder, fontFolder: FontFolder),
			ShellText.Load(content));
	}

	private static DynamixPalette? ReadPalette(GameContent content, string name) =>
		content.Read("dpl", name + ".DPL") is { } bytes
			? new DynamixPaletteTransformer().Parse(bytes) as DynamixPalette
			: null;

	/// <summary>
	/// Reads and decodes one <c>.DBM</c> through <paramref name="palette"/>. Index 0 stays opaque:
	/// a shell bitmap is a background plate, not a sprite with a cutout, so there is nothing behind it
	/// for a transparent index to reveal.
	/// </summary>
	private static ShellImage? LoadImage(GameContent content, string folder, string name, DynamixPalette palette) {
		if (content.Read(folder, name) is not { } bytes
			|| new DynamixBitmapTransformer().Parse(bytes) is not DynamixBitmap image
			|| image.Cols <= 0 || image.Rows <= 0) {
			return null;
		}

		int width = image.Cols;
		int height = image.Rows;
		var pixels = new byte[width * height * 4];
		byte[] indices = image.ImageData ?? Array.Empty<byte>();
		int count = Math.Min(indices.Length, width * height);

		for (int i = 0; i < count; i++) {
			int index = indices[i];
			var color = palette.Colors.TryGetValue(index, out var entry)
				? entry.GetColor()
				: new RgbaColor(255, (byte)index, (byte)index, (byte)index);

			pixels[i * 4] = color.R;
			pixels[i * 4 + 1] = color.G;
			pixels[i * 4 + 2] = color.B;
			pixels[i * 4 + 3] = 255;
		}

		return new ShellImage(pixels, width, height);
	}
}
