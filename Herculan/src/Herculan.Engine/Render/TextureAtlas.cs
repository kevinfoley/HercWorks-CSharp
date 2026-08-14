using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.Struct;

namespace Herculan.Engine.Render;

/// <summary>Where one DBA frame ended up inside a <see cref="TextureAtlas"/>, in 0..1 UV space.</summary>
public readonly record struct AtlasRect(float U0, float V0, float U1, float V1);

/// <summary>
/// Decodes every frame of a <c>.DBA</c> (Dynamix Bitmap Array) through a <c>.DPL</c> palette and
/// packs them into one RGBA image, so a whole mech draws in a single call with a single texture
/// bound.
///
/// <para><b>The original does not ship a packed atlas.</b> A <c>.DBA</c> on disk is an array of
/// independently-sized <see cref="DynamixBitmap"/> frames, and DBSIM's software rasterizer has no
/// reason to merge them — it rasterizes one poly at a time against whichever frame that poly
/// resolved to, so switching "textures" costs it nothing. A GPU pays per texture bind, so packing
/// here is an engine-side optimisation, not a reproduction of an original data layout. It is also
/// the *smaller* change: the alternative is splitting each mech mesh into one sub-mesh per referenced
/// frame and issuing a draw call each, which is more machinery and more per-frame cost.</para>
///
/// <para>Packing is purely a relocation — a frame's UV corners still span its own full extent, so
/// the RE-confirmed corner order in <see cref="DtsMeshBuilder"/> is unaffected (see
/// docs/formats/dts-texture-binding.md's "UV-generation formula — FOUND"; the exe builds the same
/// rect-corner mapping from a per-frame descriptor whose top-left is assumed, not confirmed, to be
/// (0,0)). Frames are padded apart by one pixel, and sampling is nearest-neighbour at the GL end,
/// so neighbours cannot bleed into each other.</para>
///
/// <para>CPU-side on purpose, touching no GL, so <see cref="Scene.MissionScene"/> stays buildable
/// headlessly — <see cref="Gl.GpuTexture"/> does the upload.</para>
/// </summary>
public sealed class TextureAtlas {
	/// <summary>Blank pixels between packed frames, so no filtering or rounding can sample a neighbour.</summary>
	private const int Padding = 1;

	/// <summary>Refuse to pack beyond this on either axis; every retail mech atlas lands far below it.</summary>
	private const int MaxDimension = 4096;

	private readonly AtlasRect?[] _frames;

	private TextureAtlas(byte[] pixels, int width, int height, AtlasRect?[] frames) {
		Pixels = pixels;
		Width = width;
		Height = height;
		_frames = frames;
	}

	/// <summary>Tightly-packed RGBA8 pixels, top row first, <see cref="Width"/> * <see cref="Height"/> * 4 bytes.</summary>
	public byte[] Pixels { get; }

	public int Width { get; }

	public int Height { get; }

	/// <summary>How many frame slots the source DBA had — including any that failed to pack.</summary>
	public int FrameCount => _frames.Length;

	/// <summary>
	/// Where DBA frame <paramref name="frameIndex"/> sits in the atlas, or null when that frame is
	/// out of range or was empty. Callers fall back to flat shading on null rather than sampling
	/// arbitrary atlas pixels.
	/// </summary>
	public AtlasRect? Frame(int frameIndex) =>
		frameIndex >= 0 && frameIndex < _frames.Length ? _frames[frameIndex] : null;

	/// <summary>
	/// Decodes and packs <paramref name="bank"/>. Returns null when the bank holds no usable frames.
	///
	/// <para>A null <paramref name="palette"/> falls back to reading each index byte as a grey level,
	/// matching <c>HercWorks.UI</c>'s renderer — a rough preview, never an accurate render, since a
	/// <c>.DBM</c> does not bind its own palette (see <see cref="DynamixBitmap"/>).</para>
	/// </summary>
	public static TextureAtlas? Build(DynamixBitmapArray bank, DynamixPalette? palette) {
		if (bank.Images is not { Length: > 0 } images) {
			return null;
		}

		// Decode first: a frame's real size is only known once its pixels are in hand, and the
		// packer needs every size up front to choose a sensible atlas width.
		var decoded = new Decoded?[images.Length];
		long totalArea = 0;
		int widest = 0;

		for (int i = 0; i < images.Length; i++) {
			var frame = images[i];
			if (frame == null || frame.Cols <= 0 || frame.Rows <= 0) {
				continue;
			}

			decoded[i] = new Decoded(DecodeFrame(frame, palette), frame.Cols, frame.Rows);
			totalArea += (frame.Cols + Padding) * (long)(frame.Rows + Padding);
			widest = System.Math.Max(widest, frame.Cols);
		}

		if (widest == 0) {
			return null;
		}

		// Start square-ish, then widen until the shelves fit under the height cap. Retail banks pack
		// on the first attempt; the loop only exists so a hypothetical oversized bank degrades into a
		// wider atlas instead of failing.
		int width = System.Math.Max(NextPowerOfTwo(widest + Padding), NextPowerOfTwo((int)System.Math.Sqrt(totalArea) + 1));

		while (true) {
			if (TryPack(decoded, width, out var placements, out int usedHeight)) {
				return Compose(decoded, placements, width, NextPowerOfTwo(usedHeight));
			}

			if (width >= MaxDimension) {
				throw new InvalidDataException(
					$"Texture bank of {images.Length} frames does not fit a {MaxDimension}x{MaxDimension} atlas.");
			}

			width *= 2;
		}
	}

	/// <summary>
	/// Shelf packer: frames are placed tallest-first into rows, each row as tall as its first
	/// (tallest) member. Good enough for this data — a mech bank is a few dozen small frames of
	/// similar size, where the gap between shelf packing and an optimal pack is a few percent of a
	/// texture that is already tiny by modern standards.
	/// </summary>
	private static bool TryPack(Decoded?[] decoded, int width, out Placement[] placements, out int usedHeight) {
		placements = new Placement[decoded.Length];
		usedHeight = 0;

		var byHeight = Enumerable.Range(0, decoded.Length)
			.Where(i => decoded[i] != null)
			.OrderByDescending(i => decoded[i]!.Height)
			.ThenByDescending(i => decoded[i]!.Width);

		int shelfX = 0;
		int shelfY = 0;
		int shelfHeight = 0;

		foreach (int i in byHeight) {
			var frame = decoded[i]!;

			if (shelfX + frame.Width > width) {
				shelfY += shelfHeight + Padding;
				shelfX = 0;
				shelfHeight = 0;
			}

			if (frame.Width > width || shelfY + frame.Height > MaxDimension) {
				return false;
			}

			placements[i] = new Placement(shelfX, shelfY, true);
			shelfX += frame.Width + Padding;
			shelfHeight = System.Math.Max(shelfHeight, frame.Height);
			usedHeight = System.Math.Max(usedHeight, shelfY + frame.Height);
		}

		return usedHeight > 0;
	}

	private static TextureAtlas Compose(Decoded?[] decoded, Placement[] placements, int width, int height) {
		var pixels = new byte[width * height * 4];
		var frames = new AtlasRect?[decoded.Length];

		for (int i = 0; i < decoded.Length; i++) {
			if (decoded[i] is not { } frame || !placements[i].Placed) {
				continue;
			}

			var at = placements[i];
			for (int row = 0; row < frame.Height; row++) {
				Array.Copy(
					frame.Pixels, row * frame.Width * 4,
					pixels, ((at.Y + row) * width + at.X) * 4,
					frame.Width * 4);
			}

			frames[i] = new AtlasRect(
				at.X / (float)width,
				at.Y / (float)height,
				(at.X + frame.Width) / (float)width,
				(at.Y + frame.Height) / (float)height);
		}

		return new TextureAtlas(pixels, width, height, frames);
	}

	/// <summary>
	/// Indexed-colour expansion, one <see cref="ImageData"/> byte per pixel. Deliberately mirrors
	/// <c>HercWorks.UI.DynamixImageRenderer.RenderFrame</c>, including its lack of any special
	/// treatment for palette index 0 — that renderer's output is what has actually been eyeballed
	/// against the real game, so diverging here would mean the engine and the tool disagree about
	/// what a frame looks like.
	/// </summary>
	private static byte[] DecodeFrame(DynamixBitmap frame, DynamixPalette? palette) {
		var pixels = new byte[frame.Cols * frame.Rows * 4];
		byte[] indices = frame.ImageData ?? Array.Empty<byte>();
		int count = System.Math.Min(indices.Length, frame.Cols * frame.Rows);

		for (int i = 0; i < count; i++) {
			byte index = indices[i];

			RgbaColor color = palette != null && palette.Colors.TryGetValue(index, out var entry)
				? entry.GetColor()
				: new RgbaColor(255, index, index, index);

			pixels[i * 4] = color.R;
			pixels[i * 4 + 1] = color.G;
			pixels[i * 4 + 2] = color.B;
			pixels[i * 4 + 3] = 255;
		}

		return pixels;
	}

	private static int NextPowerOfTwo(int value) {
		int result = 1;
		while (result < value) {
			result *= 2;
		}
		return result;
	}

	private sealed record Decoded(byte[] Pixels, int Width, int Height);

	private readonly record struct Placement(int X, int Y, bool Placed);
}
