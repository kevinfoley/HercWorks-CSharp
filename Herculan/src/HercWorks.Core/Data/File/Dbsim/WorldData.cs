namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/WLD/ worldX.wld — one theater's environment descriptor: sky and fog parameters,
/// two distance-band tables, a colour-ramp pair, and the resource names the terrain wears.
///
/// <para><b>The file is variable-length, not a fixed struct.</b> Every array in it is preceded by
/// its own count or dimension, so the layout is a walk, matching <c>maybe_World_LoadTheater</c>
/// (<c>0042e010</c>) read for read:</para>
/// <code>
/// 14 x int16                       -- sky/fog setup, dispatched straight into 0042ebbc
/// int32 count, count x int32       -- distance bands A
/// int32 count, count x int32       -- distance bands B
/// int16 rampRows, int16 rampColumns
/// rampColumns x int32              -- ramp table A
/// int16                            -- loose field between the tables
/// rampColumns x int32              -- ramp table B
/// 4 bytes, 4 bytes                 -- two more entries expanded the same way (Palette_InterpolateIndexRanges, 00430d08)
/// int16, int16, int32, int32
/// 5 x null-terminated string
/// </code>
///
/// <para>A fixed-offset reading of the middle — two raw blocks of 190 and 48 bytes — parses every
/// retail file only because their arrays happen to have the same lengths; the counts are what size
/// it.</para>
///
/// <para>The five trailing strings are constant in retail data except the third and fourth:
/// <c>world24</c>, <c>clouds2</c>, <c>impact&lt;n&gt;</c> (one per world file), the terrain texture
/// bank (<c>urban</c>, <c>bsnow</c>, <c>volcan</c>, <c>ice</c>, <c>moon</c>), then literally
/// <c>tex</c> — five separately terminated strings, not one dotted name. The fourth is the one
/// <c>Terrain_BindTextureBank</c> receives. Layout: docs/formats/terrain-texturing.md.</para>
/// </summary>
public class WorldData {
	/// <summary>Shorts in <see cref="Header"/>.</summary>
	public const int HeaderShorts = 14;

	/// <summary>
	/// The 14 leading shorts, in file order. The original hands them to its sky/fog setup
	/// (<c>0042ebbc</c>) rather than storing a struct, and their individual meanings are not
	/// established except short 9, <see cref="FlatSetSelector"/>. Only short 4 varies across the ten
	/// retail files.
	/// </summary>
	public short[] Header { get; set; } = new short[HeaderShorts];

	/// <summary>
	/// <see cref="Header"/> short 9, byte 18 — which ground-shape set the theater loads.
	/// <c>World_LoadTheater</c> reads it into <c>World_FlatSetSelector</c> (<c>0049aeea</c>) and
	/// hands it to <c>FlatObj_LoadResources</c> (<c>004097a8</c>): 0 loads <c>flat</c>, anything else
	/// <c>flat2</c>. 1 in all ten retail files. See docs/simulation/ground-shapes.md.
	/// </summary>
	public short FlatSetSelector => Header.Length > 9 ? Header[9] : (short)0;

	/// <summary>
	/// First distance-band table — 16 entries in every retail file, ascending from 60000 in steps of
	/// 4400. What reads it is not established.
	/// </summary>
	public int[] DistanceBandsA { get; set; } = Array.Empty<int>();

	/// <summary>Second distance-band table, identical to <see cref="DistanceBandsA"/> in retail data.</summary>
	public int[] DistanceBandsB { get; set; } = Array.Empty<int>();

	/// <summary>Ramp dimensions; only <see cref="RampColumns"/> sizes anything.</summary>
	public short RampRows { get; set; }

	/// <inheritdoc cref="RampRows"/>
	public short RampColumns { get; set; }

	/// <summary>First colour-ramp table, <see cref="RampColumns"/> entries.</summary>
	public int[] RampTableA { get; set; } = Array.Empty<int>();

	/// <summary>The loose short the original reads between the two ramp tables.</summary>
	public short BetweenRampTables { get; set; }

	/// <summary>Second colour-ramp table, also <see cref="RampColumns"/> entries.</summary>
	public int[] RampTableB { get; set; } = Array.Empty<int>();

	/// <summary>
	/// Two further 4-byte entries the original expands through the same helper as the ramp tables
	/// (<c>Palette_InterpolateIndexRanges</c>, <c>00430d08</c>). Kept raw: what the expansion means is not established.
	/// </summary>
	public byte[] RampExtraA { get; set; } = new byte[4];

	/// <inheritdoc cref="RampExtraA"/>
	public byte[] RampExtraB { get; set; } = new byte[4];

	/// <summary>The four loose fields between the ramp section and the strings.</summary>
	public short Trailer0 { get; set; }

	/// <inheritdoc cref="Trailer0"/>
	public short Trailer1 { get; set; }

	/// <inheritdoc cref="Trailer0"/>
	public int Trailer2 { get; set; }

	/// <inheritdoc cref="Trailer0"/>
	public int Trailer3 { get; set; }

	/// <summary>World type tag, <c>world24</c> in every retail file.</summary>
	public string? WorldTypeStr { get; set; }

	/// <summary>Cloud layer name, <c>clouds2</c> in every retail file.</summary>
	public string? CloudStr { get; set; }

	/// <summary>Impact/explosion palette base name, <c>impact0</c>..<c>impact9</c>.</summary>
	public string? ImpactStr { get; set; }

	/// <summary>
	/// Terrain texture bank base name — the string <c>Terrain_BindTextureBank</c> receives, which
	/// loads <c>dba\&lt;name&gt;.DBA</c>.
	/// </summary>
	public string? TextureBaseName { get; set; }

	/// <summary>Literally <c>tex</c> in every retail file; separately terminated, not a suffix.</summary>
	public string? TextureExtension { get; set; }

	public WorldData() { }
}
