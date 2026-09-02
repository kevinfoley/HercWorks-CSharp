namespace Herculan.Engine.Terrain;

/// <summary>
/// The simulator's terrain-detail setting, which is the whole of how far the game draws.
///
/// <para><c>Terrain_SetupVisibleRegion</c> (<c>0046ca98</c>) sets the grid's draw radius every frame
/// as <c>grid[+0x10c] = (short)DAT_004a0bcc[DAT_004d1fc3]</c> — a three-entry table indexed by the
/// setting — and then <c>&gt;&gt;= (cellShift - 14)</c> when the cell shift is above 14, which only
/// ever applies to the two shift-15 zones. Everything downstream is that one number:
/// <c>Terrain_BuildDrawRegionQuad</c> draws a square of <c>radius &lt;&lt; cellShift</c> world units
/// around the viewer, and <c>Terrain_DrawCellQuad</c> installs the same distance as the visibility
/// range the fog is measured against (<see cref="HeightGrid.VisibilityRange"/>). So the setting moves
/// the far edge of the world and the fog together, as one.</para>
/// </summary>
public static class TerrainDetail {
	/// <summary>
	/// The table at <c>DAT_004a0bcc</c>, in cells — the draw radius each of the option's three
	/// settings selects. Its neighbour at <c>DAT_0049e2da</c> maps the same three onto the option
	/// panel's labels (<c>FUN_004571f4</c> case 4), which is the second source for there being
	/// exactly three.
	/// </summary>
	public static readonly int[] RadiusInCells = { 6, 10, 14 };

	/// <summary>
	/// The setting used when none can be read. The highest, which is what the retail install this was
	/// measured against is set to — and the one whose draw distance the reference captures show.
	/// </summary>
	public const int DefaultLevel = 2;

	/// <summary>Where the simulator keeps the setting, relative to the game's data folder.</summary>
	public const string PreferencesFileName = "prefs.cfg";

	/// <summary>
	/// Which byte of that file is the setting. The file is not parsed: <c>Prefs_LoadOptions</c> (<c>00459754</c>) reads its
	/// 0x36 bytes straight over the option array at <c>DAT_004d1fbc</c>, so the file <i>is</i> the
	/// array and an option's index is its offset. The terrain-detail option is
	/// <c>DAT_004d1fc3</c>, seven bytes in.
	/// </summary>
	public const int PreferencesOffset = 7;

	/// <summary>How many bytes the simulator reads, and so how long a usable file is.</summary>
	private const int PreferencesLength = 0x36;

	/// <summary>
	/// The draw radius one setting selects, in cells, with the shift-15 correction applied — the
	/// whole of <c>Terrain_SetupVisibleRegion</c>'s write.
	/// </summary>
	public static int RadiusFor(int level, int cellShift) {
		int radius = RadiusInCells[Math.Clamp(level, 0, RadiusInCells.Length - 1)];
		return cellShift > 14 ? radius >> (cellShift - 14) : radius;
	}

	/// <summary>
	/// The setting the install at <paramref name="dataDirectory"/> is on, or
	/// <see cref="DefaultLevel"/> when there is no readable preferences file there. Reading it rather
	/// than picking one keeps the engine's draw distance in step with the retail install beside it,
	/// which is what a side-by-side capture is comparing against.
	/// </summary>
	/// <param name="dataDirectory">The game's <c>data</c> folder — where its <c>script.dat</c> is.</param>
	public static int LevelFrom(string? dataDirectory) {
		if (string.IsNullOrEmpty(dataDirectory)) {
			return DefaultLevel;
		}

		try {
			string path = Path.Combine(dataDirectory, PreferencesFileName);
			if (!File.Exists(path)) {
				return DefaultLevel;
			}

			byte[] bytes = File.ReadAllBytes(path);

			// A short file is not a partial one: the simulator memsets the array to zero and only
			// then reads over it, so anything it could not fill is option 0. Here that would silently
			// drop the draw distance to its lowest setting, which is worse than saying "unknown".
			if (bytes.Length < PreferencesLength) {
				return DefaultLevel;
			}

			return Math.Clamp(bytes[PreferencesOffset], 0, RadiusInCells.Length - 1);
		} catch (IOException) {
			return DefaultLevel;
		} catch (UnauthorizedAccessException) {
			return DefaultLevel;
		}
	}
}
