using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Io.Transform.Common;
using Herculan.Engine.Render;

namespace Herculan.Engine.Content;

/// <summary>One HUD sprite: where it sits in the shared atlas, and how big it is in cockpit pixels.</summary>
public readonly record struct HudSprite(AtlasRect Rect, int Width, int Height);

/// <summary>
/// Every HUD sprite the cockpit draws, from several <c>.HBA</c> banks, packed into one atlas so the
/// whole HUD costs a single texture bind.
///
/// <para><b>Why <c>hba</c> and not <c>dba</c>.</b> The game ships each HUD sprite bank twice under the
/// same name — <c>dba\NAME.DBA</c> for its 320-wide mode and <c>hba\NAME.HBA</c> for its 640-wide one,
/// the latter being exactly twice the former on both axes, frame for frame. DBSIM picks between them
/// at load time off a video-mode global, with the literal folder names sitting adjacent to each bank
/// name in its .rdata (<c>"NAME\0hba\0dba\0"</c>). The same <c>d</c>/<c>h</c> split runs through the
/// whole resource set: <c>db0</c>/<c>hb0</c> canopy art, <c>dfn</c>/<c>hfn</c> fonts, <c>dmg</c>/
/// <c>hdg</c>. This engine renders the 640x480 <c>.HB0</c> canopy, so it takes the <c>hba</c> half
/// throughout — mixing the two would put half-scale sprites on full-scale art.</para>
///
/// <para>That same video mode is what <see cref="CockpitArt.GauToPixelScale"/> is: the original's
/// hardcoded widget coordinates appear in the disassembly as <c>value &lt;&lt; (shift &amp; 0x1f)</c>
/// against per-axis resolution-shift globals, so <c>.GAU</c> coordinates are authored in the 320-wide
/// space and doubled for the 640-wide one.</para>
///
/// <para>Index 0 decodes to alpha 0 here, meaning "leave the console art showing through". The canopy
/// frames in <see cref="CockpitArt"/> no longer use index 0 for anything: their cutout comes from the
/// herc's own <see cref="CockpitClipRegions"/> data, so black console detail stays opaque there.</para>
///
/// <para><b>Some banks do sit inside the per-herc cockpit scheme window.</b> Most author in the
/// 12-35 band, which the theater palette owns outright, but <c>WPN_DMG</c> is 1552/1568 pixels of
/// index 46 in frame 0 and <c>PWEAPONS</c> frame 1 is entirely index 42 — both inside slots 42-65,
/// which <see cref="CockpitPalette"/> replaces per herc. That is deliberate: those plates are meant
/// to match the console they sit on.</para>
/// </summary>
public sealed class HudSpriteSheet {
	/// <summary>Resource folder for the 640-wide sprite banks, and their extension.</summary>
	public const string ResourceFolder = "hba";

	private readonly Dictionary<string, Bank> _banks;
	private readonly Dictionary<string, HudFont> _fonts;

	private HudSpriteSheet(TextureAtlas atlas, Dictionary<string, Bank> banks, Dictionary<string, HudFont> fonts) {
		Atlas = atlas;
		_banks = banks;
		_fonts = fonts;
	}

	/// <summary>Every loaded bank's frames, packed together. Upload once, bind once.</summary>
	public TextureAtlas Atlas { get; }

	/// <summary>Names of the banks that actually loaded, in request order.</summary>
	public IEnumerable<string> BankNames => _banks.Keys;

	/// <summary>
	/// Sprite <paramref name="frame"/> of <paramref name="bank"/>, or null when that bank never
	/// loaded or the frame index is out of its range. Callers draw nothing on null rather than
	/// falling back to another frame — a missing sprite should look missing, not wrong.
	/// </summary>
	public HudSprite? Sprite(string bank, int frame) {
		if (!_banks.TryGetValue(bank, out var entry)
			|| frame < 0 || frame >= entry.FrameSizes.Length
			|| Atlas.Frame(entry.FirstAtlasFrame + frame) is not { } rect) {
			return null;
		}

		var (width, height) = entry.FrameSizes[frame];
		return new HudSprite(rect, width, height);
	}

	/// <summary>
	/// A loaded HUD font's metrics, or null when that font was not requested or could not be read.
	/// Its glyphs are packed into <see cref="Atlas"/> alongside the sprite banks and are addressed
	/// through <see cref="Sprite"/> under the font's own name, so text costs no extra texture bind.
	/// </summary>
	public HudFont? Font(string name) => _fonts.TryGetValue(name, out var font) ? font : null;

	/// <summary>
	/// Loads and packs the named sprite banks out of <c>hba\</c> and the named fonts out of
	/// <c>hfn\</c>. Banks and fonts that are missing from the mounted archives are skipped, so a
	/// partial set still draws what it has; returns null only when nothing loaded at all.
	///
	/// <para>Fonts share the sprite atlas rather than getting one of their own: a glyph is an indexed
	/// bitmap of exactly the same shape as a sprite frame, so one bank entry per font — its glyphs in
	/// character order — makes <see cref="Sprite"/> address glyphs as well.</para>
	/// </summary>
	public static HudSpriteSheet? Load(GameContent content, DynamixPalette? palette,
			IEnumerable<string> bankNames, IEnumerable<string>? fontNames = null) {
		var frames = new List<DynamixBitmap>();
		var banks = new Dictionary<string, Bank>(StringComparer.OrdinalIgnoreCase);
		var fonts = new Dictionary<string, HudFont>(StringComparer.OrdinalIgnoreCase);

		foreach (string name in bankNames) {
			if (banks.ContainsKey(name)) {
				continue;
			}

			if (content.Read(ResourceFolder, name + "." + ResourceFolder.ToUpperInvariant()) is not { } bytes
				|| new DynamixBitmapArrayTransformer().Parse(bytes) is not DynamixBitmapArray bank
				|| bank.Images is not { Length: > 0 } images) {
				continue;
			}

			banks[name] = new Bank(frames.Count, SizesOf(images));
			frames.AddRange(images);
		}

		foreach (string name in fontNames ?? Array.Empty<string>()) {
			if (banks.ContainsKey(name) || HudFont.Load(content, name) is not { } font) {
				continue;
			}

			var glyphs = font.Glyphs.ToArray();
			banks[name] = new Bank(frames.Count, SizesOf(glyphs));
			fonts[name] = font;
			frames.AddRange(glyphs);
		}

		if (frames.Count == 0) {
			return null;
		}

		// One combined bank so the existing shelf packer does the work — the atlas has no notion of
		// which source bank a frame came from, which is exactly what the FirstAtlasFrame offsets are for.
		var combined = new DynamixBitmapArray { Images = frames.ToArray() };
		return TextureAtlas.Build(combined, palette, transparentIndex0: true) is { } atlas
			? new HudSpriteSheet(atlas, banks, fonts)
			: null;
	}

	private static (int Width, int Height)[] SizesOf(IReadOnlyList<DynamixBitmap> images) {
		var sizes = new (int Width, int Height)[images.Count];
		for (int i = 0; i < images.Count; i++) {
			sizes[i] = (images[i]?.Cols ?? 0, images[i]?.Rows ?? 0);
		}

		return sizes;
	}

	private readonly record struct Bank(int FirstAtlasFrame, (int Width, int Height)[] FrameSizes);
}
