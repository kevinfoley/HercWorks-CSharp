using Silk.NET.OpenGL;

namespace Herculan.Engine.Host;

/// <summary>
/// Writes the framebuffer to a file, so a run can be checked without somebody watching it.
///
/// <para>Dependency-free 24bpp BMP — no System.Drawing, no ImageSharp, per Herculan.Engine's
/// no-imaging-dependency precedent (docs/engine/planning.md). It reads straight out of the
/// framebuffer with <c>glReadPixels</c>, and BMP's bottom-up row order happens to match GL's
/// bottom-left origin, so no row flip is needed.</para>
///
/// <para>Host-side, and shared by both entry points: the mission loop's <c>--screenshot</c> and the
/// shell's.</para>
/// </summary>
static class Screenshot {
	public static void Capture(GL gl, int width, int height, string path) {
		int rowSize = width * 3;
		int rowPadding = (4 - rowSize % 4) % 4;
		int paddedRowSize = rowSize + rowPadding;
		int pixelDataSize = paddedRowSize * height;

		var pixels = new byte[width * height * 3];
		gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Bgr, PixelType.UnsignedByte, pixels.AsSpan());

		using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
		using var writer = new BinaryWriter(file);

		int fileSize = 14 + 40 + pixelDataSize;
		writer.Write((byte)'B'); writer.Write((byte)'M');
		writer.Write(fileSize);
		writer.Write(0); // reserved
		writer.Write(14 + 40); // pixel data offset

		writer.Write(40); // DIB header size (BITMAPINFOHEADER)
		writer.Write(width);
		writer.Write(height); // positive = bottom-up row order
		writer.Write((short)1); // planes
		writer.Write((short)24); // bits per pixel
		writer.Write(0); // no compression
		writer.Write(pixelDataSize);
		writer.Write(2835); // ~72 DPI
		writer.Write(2835);
		writer.Write(0); // colors used
		writer.Write(0); // important colors

		var padding = new byte[rowPadding];
		for (int row = 0; row < height; row++) {
			writer.Write(pixels, row * rowSize, rowSize);
			if (rowPadding > 0) {
				writer.Write(padding);
			}
		}

		Console.WriteLine($"Wrote screenshot to {path} ({width}x{height}).");
	}
}
