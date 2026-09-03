using System.Numerics;
using Herculan.Engine.Content;
using Herculan.Engine.Terrain;

namespace Herculan.Engine.Render;

/// <summary>
/// The command display's terrain raster: the mission's own patch of the height grid, coloured by
/// height alone, as one RGBA image the map draws under its grid and markers.
/// </summary>
/// <remarks>
/// <para><b>Where it comes from.</b> <c>FUN_0044f6cc</c> builds this once per mission, not per
/// frame. It walks the active height grid, turns each cell's raw height into a palette index with
/// <see cref="HddMap.RasterPalette"/>, and Gouraud-shades two triangles per cell into an offscreen
/// 8-bit bitmap sized to fit the mission box inside 640x400. <c>FUN_004502e4</c> then blits that
/// bitmap between two projected corners every repaint, which is what makes panning and zooming
/// cost nothing.</para>
///
/// <para><b>What this does instead.</b> One texel per grid cell, and the map quad is drawn with
/// linear filtering. The intermediate bitmap's own scale factor is dropped because it exists only to
/// give the software rasterizer somewhere to Gouraud-shade into; sampling the cell grid smoothly is
/// the same picture without the round trip. What is preserved exactly is the part that is visible:
/// the colour rule, and therefore that the whole map lives in palette entries 16-31 and re-colours
/// with the theater.</para>
/// </remarks>
public sealed class HddMapRaster {
	private HddMapRaster(byte[] pixels, int width, int height, int worldX0, int worldY0, int worldX1, int worldY1) {
		Pixels = pixels;
		Width = width;
		Height = height;
		WorldX0 = worldX0;
		WorldY0 = worldY0;
		WorldX1 = worldX1;
		WorldY1 = worldY1;
	}

	/// <summary>RGBA8, row 0 at the <i>top</i> — i.e. at <see cref="WorldY1"/>, since world +y is up.</summary>
	public byte[] Pixels { get; }

	/// <summary>Texels across, one per grid cell.</summary>
	public int Width { get; }

	/// <summary>Texels down.</summary>
	public int Height { get; }

	/// <summary>World x of the left edge — the first cell's own origin.</summary>
	public int WorldX0 { get; }

	/// <summary>World y of the bottom edge.</summary>
	public int WorldY0 { get; }

	/// <summary>World x of the right edge, one cell past the last.</summary>
	public int WorldX1 { get; }

	/// <summary>World y of the top edge.</summary>
	public int WorldY1 { get; }

	/// <summary>
	/// Builds the raster for <paramref name="bounds"/> out of <paramref name="grid"/>, resolving each
	/// texel through <paramref name="palette"/> — <see cref="CockpitArt.PaletteEntry"/>, so the map
	/// takes the same live palette the terrain does. Returns null when the box falls outside the grid
	/// entirely, which no real mission does.
	/// </summary>
	public static HddMapRaster? Build(HeightGrid grid, HddMapBounds bounds, Func<int, Vector3?> palette) {
		ArgumentNullException.ThrowIfNull(grid);
		ArgumentNullException.ThrowIfNull(palette);
		if (bounds.IsEmpty) {
			return null;
		}

		var (cellX0, cellY0, cellX1, cellY1) = HddMap.RasterCells(grid, bounds);
		int width = cellX1 - cellX0 + 1;
		int height = cellY1 - cellY0 + 1;
		if (width <= 0 || height <= 0) {
			return null;
		}

		// Resolved once per palette entry rather than once per texel: the ramp is sixteen colours and
		// the grid can be a quarter of a million cells.
		var ramp = new byte[32 * 4];
		for (int step = 0; step <= HddMap.RasterHeightClamp / HddMap.RasterHeightDivisor; step++) {
			int index = HddMap.RasterBasePalette + step;
			var color = palette(index) ?? Vector3.Zero;
			ramp[index * 4] = (byte)Math.Clamp(color.X * 255f, 0f, 255f);
			ramp[index * 4 + 1] = (byte)Math.Clamp(color.Y * 255f, 0f, 255f);
			ramp[index * 4 + 2] = (byte)Math.Clamp(color.Z * 255f, 0f, 255f);
			ramp[index * 4 + 3] = 255;
		}

		var pixels = new byte[width * height * 4];
		for (int row = 0; row < height; row++) {
			// Row 0 is the top of the image and so the highest world y, which is the direction the
			// original's own build loop counts in as it steps its destination down.
			int cellY = cellY1 - row;
			for (int column = 0; column < width; column++) {
				int index = HddMap.RasterPalette(grid.RawHeightAt(cellX0 + column, cellY)) * 4;
				int at = (row * width + column) * 4;
				pixels[at] = ramp[index];
				pixels[at + 1] = ramp[index + 1];
				pixels[at + 2] = ramp[index + 2];
				pixels[at + 3] = ramp[index + 3];
			}
		}

		int cellSize = grid.CellSize;
		return new HddMapRaster(pixels, width, height,
			cellX0 << grid.CellShift, cellY0 << grid.CellShift,
			(cellX1 << grid.CellShift) + cellSize, (cellY1 << grid.CellShift) + cellSize);
	}
}
