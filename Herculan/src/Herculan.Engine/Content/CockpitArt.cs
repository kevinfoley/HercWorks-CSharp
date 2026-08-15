using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.File.Gau;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Core.Io.Transform.Dbsim;

namespace Herculan.Engine.Content;

/// <summary>One decoded cockpit-art frame, RGBA8, top row first — CPU-side, no GL.</summary>
public sealed record CockpitFrame(byte[] Pixels, int Width, int Height);

/// <summary>
/// A herc's cockpit canopy art and HUD widget layout — see docs/formats/cockpit-hud.md (Milestone 8
/// Phase 0) for the RE and the real-data verification behind every constant here.
///
/// <para><see cref="Front"/> is <c>(herc).HB0</c> (the center/front view) and <see cref="Side"/> is
/// <c>(herc).HB2</c> (a real, distinct side view — not a duplicate of the front). There is no
/// separate mirrored asset: the left panel reuses <see cref="Side"/> with its UVs flipped
/// horizontally at draw time. <c>(herc).HB1</c> exists but is a rear/overhead equipment-bay view, not
/// a third front-facing angle — it is not loaded here because this milestone's simultaneous
/// front+left+right layout has no use for it.</para>
///
/// <para>Both frames decode through the single shared <c>dpl\COCKPIT.DPL</c> palette. Palette index 0
/// (pure black) marks the "3D viewport" cutout. It is not one clean region, though — the canopy strut
/// pinches it into several disconnected islands (a real per-pixel connectivity scan of
/// <c>COLOSSUS.HB0</c>/<c>.HB2</c>, 2026-08-15, found 100% of index-0 pixels sit in components that
/// touch the image's outer edge, split across 2-3 separate components per frame) — a single-seed flood
/// fill missed the islands that don't happen to connect to that one seed, leaving them opaque and
/// blocking the 3D view behind the canopy's raked struts. <see cref="Load"/> instead floods from every
/// border pixel at once: any black region reachable from the image edge is "outside the cockpit" and
/// becomes part of the hole, while any that never reaches the edge stays opaque console detail (the
/// scan found none of the latter in real data, but the algorithm leaves room for it). The result bakes
/// into each frame's own alpha channel (0 = viewport hole, 255 = opaque art).</para>
///
/// <para><b>The palette index→RGB mapping needs a lighting-state offset, not a plain lookup.</b>
/// <c>COCKPIT.DPL</c> is not one flat 256-color space — it's a sequence of ~15 short brightness
/// *ramps* (10-16 entries each: e.g. index 42-47 is a 6-step neutral-gray ramp, 80-89 a 10-step
/// warm-brown ramp, 240-247 a blue-tinted ramp), and a real <c>.HB0</c>'s pixel data only ever
/// exercises a narrow band of indices within one or two adjacent ramps (COLOSSUS.HB0 uses only
/// indices 0 and roughly 42-71 — a real, exhaustive per-pixel histogram, not a sample). A plain,
/// unmodified lookup (index 0-based, i.e. offset 0) renders a legible but visibly wrong cockpit — a
/// real screenshot comparison (2026-08-15) shows retail COLOSSUS is neutral gray/white, not the
/// purple/lavender tint offset 0 produces, because it happens to land on the "48-55" ramp's purple
/// end for some of the shading detail. Disassembly did not find the mechanism (see
/// docs/formats/cockpit-hud.md's Q1 for what was and wasn't found — <c>Palette_InstallRange</c>'s
/// every caller was traced and none installs <c>COCKPIT.DPL</c> at a nonzero base for normal
/// gameplay), so this is resolved empirically: <see cref="PaletteIndexOffset"/> circularly shifts
/// every <i>nonzero</i> index (index 0 is a fixed sentinel — the viewport-hole/background marker, and
/// shifting it lands on fully-saturated EGA-style colors at indices 1-15, wildly wrong) by a constant.
/// Different offsets select what look like different in-game lighting states — offset 14 matches a
/// neutral/daylight look for both COLOSSUS and APOCA (verified against a real reference screenshot),
/// while e.g. offset 246 gives APOCA a darker, redder look the user identified as matching retail
/// gameplay screenshots specifically. <b>14 is a reasonable static default, not a proven "correct"
/// constant</b> — the real selection mechanism (plausibly tied to an ambient-lighting system, given
/// the terrain/mech renderer already has one) was not located. Revisit if a future session traces the
/// real blit/lighting function — DBSIM's <c>CockpitViewInstance</c> global (see cockpit-hud.md's
/// symbol table) is the next lead: it owns the widget tree but its background-art field wasn't
/// identified either.</para>
/// </summary>
public sealed class CockpitArt {
	/// <summary>The single shared palette every herc's cockpit art decodes through.</summary>
	public const string PaletteName = "COCKPIT";

	/// <summary>
	/// GAU widget coordinates address a 320x400 logical space; cockpit art is 640x480 pixels. A plain
	/// uniform 2x scale on both axes maps one onto the other — confirmed by overlaying five
	/// independently-positioned real widgets (MFD panel, shield display, throttle, chain button,
	/// energy meter) on real <c>APOCA.HB0</c> art and finding every one lands exactly on its physical
	/// console graphic at this scale, and no other tried combination (1x, 2x/1x, 2x/1.2x) fits more
	/// than one widget at once. See docs/formats/cockpit-hud.md, "Q3".
	/// </summary>
	public const float GauToPixelScale = 2f;

	/// <summary>
	/// Empirically-tuned default lighting-state offset — see the class doc comment for the full
	/// reasoning. Applied to every nonzero palette index, circularly within 1..255; index 0 is never
	/// shifted.
	/// </summary>
	public const int PaletteIndexOffset = 14;

	private CockpitArt(CockpitFrame front, CockpitFrame side, GAUFile gau) {
		Front = front;
		Side = side;
		Gau = gau;
	}

	/// <summary>The center/front cockpit view — console, HUD instruments, and the 3D viewport cutout.</summary>
	public CockpitFrame Front { get; }

	/// <summary>A side cockpit view — mirror horizontally at draw time for the opposite panel.</summary>
	public CockpitFrame Side { get; }

	/// <summary>The HUD widget layout to overlay on <see cref="Front"/> — center panel only (see Overlay2DRenderer).</summary>
	public GAUFile Gau { get; }

	/// <summary>
	/// Loads and decodes one herc's cockpit art and HUD layout. Returns null when any required
	/// resource is missing from the mounted archives, in which case the caller should fall back to
	/// drawing no cockpit overlay rather than a partially-wrong one.
	/// </summary>
	public static CockpitArt? Load(GameContent content, string hercName) {
		byte[]? paletteBytes = content.Read("dpl", PaletteName + ".DPL");
		if (paletteBytes == null
			|| new DynamixPaletteTransformer().BytesToObject(paletteBytes) is not DynamixPalette palette) {
			return null;
		}

		byte[]? gauBytes = content.Read("gau", hercName + ".GAU");
		if (gauBytes == null || new GauFileTransformer().BytesToObject(gauBytes) is not GAUFile gau) {
			return null;
		}

		var front = LoadFrame(content, "hb0", hercName + ".HB0", palette);
		var side = LoadFrame(content, "hb2", hercName + ".HB2", palette);
		if (front == null || side == null) {
			return null;
		}

		CutViewportHole(front);
		CutViewportHole(side);

		return new CockpitArt(front, side, gau);
	}

	/// <summary>
	/// Reads and decodes one <c>.HBx</c> file's single frame through <paramref name="palette"/>.
	/// Mirrors <c>TextureAtlas.DecodeFrame</c>'s indexed-colour expansion exactly (including no
	/// special-casing of index 0 here — the viewport cutout is a separate, deliberate pass, not a
	/// side effect of decoding).
	/// </summary>
	private static CockpitFrame? LoadFrame(GameContent content, string folder, string name, DynamixPalette palette) {
		byte[]? bytes = content.Read(folder, name);
		if (bytes == null
			|| new DynamixBitmapArrayTransformer().BytesToObject(bytes) is not DynamixBitmapArray array
			|| array.Images is not { Length: > 0 } images
			|| images[0] is not { } frame
			|| frame.Cols <= 0 || frame.Rows <= 0) {
			return null;
		}

		int width = frame.Cols;
		int height = frame.Rows;
		var pixels = new byte[width * height * 4];
		byte[] indices = frame.ImageData ?? Array.Empty<byte>();
		int count = Math.Min(indices.Length, width * height);

		for (int i = 0; i < count; i++) {
			byte raw = indices[i];
			// Index 0 is a fixed sentinel (background/viewport marker) and is never shifted — see the
			// class doc comment for why every other index gets PaletteIndexOffset applied circularly
			// within 1..255.
			int index = raw == 0 ? 0 : ((raw - 1 + PaletteIndexOffset) % 255 + 255) % 255 + 1;
			var color = palette.Colors.TryGetValue(index, out var entry)
				? entry.GetColor()
				: new HercWorks.Core.Data.Struct.RgbaColor(255, (byte)index, (byte)index, (byte)index);

			pixels[i * 4] = color.R;
			pixels[i * 4 + 1] = color.G;
			pixels[i * 4 + 2] = color.B;
			pixels[i * 4 + 3] = 255;
		}

		return new CockpitFrame(pixels, width, height);
	}

	/// <summary>
	/// Flood-fills every connected region of pure-black (0,0,0) pixels that touches the image's outer
	/// edge, setting each one's alpha to 0. Multi-source (every border pixel is a candidate seed) rather
	/// than a single interior point — see the class doc comment: the canopy strut splits the viewport
	/// hole into 2-3 disconnected islands, and a single seed only ever catches the one it happens to sit
	/// in. Border-touching is the discriminator, not a single seed's reachability — real per-pixel data
	/// shows every viewport-hole pixel touches the edge and no interior console-shadow black pixel does.
	/// </summary>
	private static void CutViewportHole(CockpitFrame frame) {
		byte[] pixels = frame.Pixels;
		bool IsBlackOpaque(int x, int y) {
			int i = (y * frame.Width + x) * 4;
			return pixels[i] == 0 && pixels[i + 1] == 0 && pixels[i + 2] == 0 && pixels[i + 3] != 0;
		}

		var queue = new Queue<(int X, int Y)>();
		for (int x = 0; x < frame.Width; x++) {
			queue.Enqueue((x, 0));
			queue.Enqueue((x, frame.Height - 1));
		}
		for (int y = 0; y < frame.Height; y++) {
			queue.Enqueue((0, y));
			queue.Enqueue((frame.Width - 1, y));
		}

		while (queue.Count > 0) {
			var (x, y) = queue.Dequeue();
			if (x < 0 || x >= frame.Width || y < 0 || y >= frame.Height || !IsBlackOpaque(x, y)) {
				continue;
			}

			pixels[(y * frame.Width + x) * 4 + 3] = 0;

			queue.Enqueue((x + 1, y));
			queue.Enqueue((x - 1, y));
			queue.Enqueue((x, y + 1));
			queue.Enqueue((x, y - 1));
		}
	}
}
