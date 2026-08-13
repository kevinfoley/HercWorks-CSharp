namespace Herculan.Engine.Terrain;

/// <summary>
/// Port of DBSIM's <c>HeightGrid</c> — the loaded terrain of one zone. The original is a 0x129-byte
/// struct built by <c>HeightGrid_Constructor</c> (<c>0046bdf8</c>) and installed as the global
/// <c>ActiveHeightGrid</c> by <c>Terrain_LoadZone</c> (<c>0042789c</c>); see
/// docs/simulation/dbsim-physics-notes.md, "Terrain system", for the full field map.
///
/// <para>Storage differs from the original in one deliberate way: DBSIM allocates a single array of
/// 16-byte cells, of which only byte <c>+0x0</c> (raw height) and byte <c>+0xf</c> (diagonal
/// selector + material index) are written by either loader path — the intervening 14 bytes are
/// undecoded and never touched. Rather than carry 14 bytes per cell of known-dead space, this holds
/// two parallel byte arrays. The addressing (<c>index = x + (y &lt;&lt; WidthShift)</c>, row-major)
/// and every value are unchanged, so the height query below is still a literal translation.</para>
/// </summary>
public sealed class HeightGrid {
	private readonly byte[] _rawHeights;
	private readonly byte[] _cellFlags;

	internal HeightGrid(int widthShift, int heightShift, int cellShift, int heightScale, int detailLod,
			byte[] rawHeights, byte[] cellFlags) {
		WidthShift = widthShift;
		HeightShift = heightShift;
		CellShift = cellShift;
		HeightScale = heightScale;
		DetailLod = detailLod;
		_rawHeights = rawHeights;
		_cellFlags = cellFlags;
	}

	/// <summary>log2 of the grid width in cells (<c>+0x100</c>). Re-derived from the heightmap image.</summary>
	public int WidthShift { get; }

	/// <summary>log2 of the grid height in cells (<c>+0x104</c>). Re-derived from the heightmap image.</summary>
	public int HeightShift { get; }

	/// <summary>
	/// log2 of world units per cell (<c>+0x108</c>), and the shift that converts a world (x,y) to a
	/// cell (x,y). Comes from the per-zone <c>.DAT</c> header; 14 (16384 units per cell) in every
	/// retail zone checked.
	/// </summary>
	public int CellShift { get; }

	/// <summary>
	/// Additive height offset (<c>+0x110</c>). Always 0 for the real binary zone format — the
	/// loader sets it to 0xff as a working value and zeroes it on completion. Nonzero only in the
	/// ASCII debug format, which no retail data uses.
	/// </summary>
	public int HeightBase => 0;

	/// <summary>Multiplicative height scale applied to each cell's raw byte (<c>+0x118</c>), from the zone header.</summary>
	public int HeightScale { get; }

	/// <summary>
	/// The load-time LOD value at <c>+0x10c</c>, derived as <c>10 >> (CellShift - 14)</c> (clamped,
	/// default 10). <c>Terrain_HeightQuery</c> never reads it; it is presumably a renderer/chunking
	/// parameter, and its consumer in the original hasn't been located.
	/// </summary>
	public int DetailLod { get; }

	/// <summary>Grid width in cells.</summary>
	public int Width => 1 << WidthShift;

	/// <summary>Grid height in cells.</summary>
	public int Height => 1 << HeightShift;

	/// <summary>World units spanned by one cell.</summary>
	public int CellSize => 1 << CellShift;

	/// <summary>World units spanned by the whole zone along X.</summary>
	public long WorldWidth => (long)Width << CellShift;

	/// <summary>World units spanned by the whole zone along Y.</summary>
	public long WorldHeight => (long)Height << CellShift;

	/// <summary>
	/// Highest world-space height anywhere in the grid — the original's <c>+0x114</c>, which the
	/// loader tracks as a running max of raw bytes and multiplies by <see cref="HeightScale"/> on
	/// completion.
	/// </summary>
	public int MaxWorldHeight { get; internal set; }

	/// <summary>Flat cell index for a cell coordinate, matching the original's row-major addressing.</summary>
	public int CellIndex(int cellX, int cellY) => cellX + (cellY << WidthShift);

	/// <summary>Raw 0–255 heightmap byte for a cell.</summary>
	public byte RawHeightAt(int cellX, int cellY) => _rawHeights[CellIndex(cellX, cellY)];

	/// <summary>World-space height of a cell corner: <c>raw * HeightScale + HeightBase</c>.</summary>
	public int WorldHeightAt(int cellX, int cellY) => _rawHeights[CellIndex(cellX, cellY)] * HeightScale + HeightBase;

	/// <summary>
	/// The cell's diagonal-split selector — bits [0:1] of the original's <c>+0xf</c> byte, which
	/// <see cref="HeightAtWorld"/> uses to decide which way the quad's diagonal runs. See
	/// <see cref="TerrainZoneLoader"/> for which values the retail loader actually produces.
	/// </summary>
	public int DiagonalSelectorAt(int cellX, int cellY) => _cellFlags[CellIndex(cellX, cellY)] & 3;

	/// <summary>
	/// The cell's material/detail-type index — bits [2:7] of the original's <c>+0xf</c> byte, an
	/// index into the shared <c>dat\mat0</c> table. Purely a detail-texture selector; nothing in
	/// the height query or collision reads it.
	/// </summary>
	public int MaterialIndexAt(int cellX, int cellY) => _cellFlags[CellIndex(cellX, cellY)] >> 2;

	/// <summary>
	/// Literal port of <c>Terrain_HeightQuery</c> (<c>0046e07c</c>): converts a world (x, y) to a
	/// grid cell, takes that cell's four corner heights, and interpolates barycentrically across
	/// whichever of the cell's two triangles the point falls in — with the split direction chosen
	/// per cell by <see cref="DiagonalSelectorAt"/>, not fixed grid-wide. Returns 0 for points
	/// outside the grid, same as the original.
	///
	/// <para>Two faithfulness notes worth keeping visible. First, all the interpolation arithmetic
	/// stays in integers with <c>>> CellShift</c> divides, so the result quantizes exactly the way
	/// the original's does — this feeds ballistic ground-impact and the flyer terrain-avoidance
	/// autopilot, where a float rewrite would silently shift impact points. Second, the original
	/// bounds-checks only the *flat* cell index of each of the four corners, not the cell x+1
	/// separately, so a query in the last column reads the next row's first cell as its east
	/// neighbour. That wrap is reproduced rather than fixed: it is exactly the kind of original-game
	/// quirk docs/engine/planning.md's "vanilla by default" principle says to keep by default.</para>
	/// </summary>
	public int HeightAtWorld(int worldX, int worldY) {
		int cellX = worldX >> CellShift;
		int cellY = worldY >> CellShift;

		if (cellX < 0 || cellX >= (1 << WidthShift) || cellY < 0 || cellY >= (1 << HeightShift)) {
			return 0;
		}

		int cellSize = 1 << CellShift;
		int fracX = worldX - (cellX << CellShift);
		int fracY = worldY - (cellY << CellShift);

		int i00 = Corner(cellX + (cellY << WidthShift));
		int i01 = Corner(cellX + ((cellY + 1) << WidthShift));
		int i11 = Corner(cellX + ((cellY + 1) << WidthShift) + 1);
		int i10 = Corner(cellX + (cellY << WidthShift) + 1);

		if (i00 < 0 || i01 < 0 || i11 < 0 || i10 < 0) {
			return 0;
		}

		int h00 = _rawHeights[i00] * HeightScale + HeightBase;
		int h01 = _rawHeights[i01] * HeightScale + HeightBase;
		int h11 = _rawHeights[i11] * HeightScale + HeightBase;
		int h10 = _rawHeights[i10] * HeightScale + HeightBase;

		int diagonal = _cellFlags[i00] & 3;

		if (diagonal == 2) {
			// Split along the (0,0)-(1,1) diagonal.
			if (fracY < fracX) {
				return ((h00 - h10) * (cellSize - fracX) >> CellShift) + h10
					 + ((h11 - h10) * fracY >> CellShift);
			}
			return ((h00 - h01) * (cellSize - fracY) >> CellShift) + h01
				 + ((h11 - h01) * fracX >> CellShift);
		}

		if (diagonal == 0) {
			// Split along the (0,1)-(1,0) anti-diagonal.
			if (cellSize - fracX < fracY) {
				return ((h01 - h11) * (cellSize - fracX) >> CellShift) + h11
					 + ((h10 - h11) * (cellSize - fracY) >> CellShift);
			}
			return ((h10 - h00) * fracX >> CellShift) + h00
				 + ((h01 - h00) * fracY >> CellShift);
		}

		// Selectors 1 and 3 fall through to the plane through the (0,0)/(1,0)/(0,1) corners with no
		// triangle test at all. The original handles them; neither loader path has been observed
		// producing them (see TerrainZoneLoader), so this branch is reachable only if some
		// not-yet-located code writes those bits.
		return ((h10 - h00) * fracX >> CellShift) + h00
			 + ((h01 - h00) * fracY >> CellShift);
	}

	/// <summary>
	/// The original's per-corner bounds test: a flat cell index is valid only within
	/// <c>1 &lt;&lt; (WidthShift + HeightShift)</c>. Returns -1 for "out of range", where the
	/// original produces a null pointer and then bails out of the whole query.
	/// </summary>
	private int Corner(int index) =>
		index < 0 || index >= 1 << (WidthShift + HeightShift) ? -1 : index;
}
