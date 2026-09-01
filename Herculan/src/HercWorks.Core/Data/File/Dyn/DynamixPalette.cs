using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Dyn;

/// <summary>
/// FILE - VOL file, .DPL — contains palette colors per-game object; some real optimization
/// there. Ported from org.hercworks.core.data.file.dyn.DynamixPalette.
/// </summary>
public class DynamixPalette {
	/// <summary>
	/// The 4-byte header, hex-decoded. Verified against a real <c>.DPL</c>
	/// (<c>ES2\VOL\SHELL0\DPL\ALPHA.DPL</c>): its first 4 content bytes are <c>0F 00 28 00</c>.
	/// Beware the Java original's <c>Bytes.from("0F002800", UTF_8)</c> idiom, which despite looking
	/// like hex yields the 8 ASCII bytes of that text — <see cref="File.Dat.Shell.InitHerc"/> still
	/// carries that form. The read path only ever skips 4 bytes for this header,
	/// which is consistent with this being the correct length all along.
	/// </summary>
	public static readonly byte[] Header = { 0x0F, 0x00, 0x28, 0x00 };

	public int ColorCount { get; set; }

	/// <summary>
	/// No mapping to binary data — used to account for incredibly dark colors in most palettes.
	/// The game binary probably scales values up too; this was likely a byte-saving measure.
	/// </summary>
	public int Scalar { get; set; } = 1;

	public int PaletteSizeByte { get; set; }
	public byte[]? RawIndexBytes { get; set; }
	public Dictionary<int, ColorBytes> Colors { get; set; } = new();

	public ColorBytes Index0AlphaKey { get; set; }

	/// <summary>
	/// The palette's <b>shade ramps</b>, which live in the file's tail after the colour entries: one
	/// ramp per entry of a 256-slot table, each a run of palette indices from darkest to brightest.
	///
	/// <para>A ramp is not a colour — it is a <i>material</i>. DBSIM's <c>TSShadedPoly_Render</c>
	/// (<c>0047542c</c>) treats a surface's <c>FrontColor</c> as an index into this table, not as a
	/// palette index, and picks the entry a face's computed light level lands on:
	/// <c>Palette_ShadeRampLookup</c> (<c>00430e34</c>) is
	/// <c>ramp[value &amp; 0xff].indices[(shade * ramp.length) &gt;&gt; 8]</c>, stepping back one when
	/// that lands past the end. That is the whole of a shaded surface's colour, and it is why the
	/// theater's palette changes what a HERC and a building look like.</para>
	///
	/// <para>Layout, read exactly and byte-complete on all four <c>WORLD&lt;n&gt;.DPL</c> files:
	/// <c>int32 rampCount</c> (256 in every retail file) followed by <c>rampCount</c> records of
	/// <c>int16 length</c> then <c>length</c> <c>int16</c> palette indices. Retail lengths are 1, 4,
	/// 7, 8, 13 and 16; most of the table is the single-entry ramp <c>[255]</c>, so only the low
	/// twenty-odd slots carry real material ramps.</para>
	///
	/// <para>Empty when the file carries no tail — the shell palettes are colours only.</para>
	/// </summary>
	public IReadOnlyList<short[]> ShadeRamps { get; set; } = Array.Empty<short[]>();

	/// <summary>
	/// The ramp table exactly as it was read, so the write path can put it back untouched rather
	/// than re-serialising a structure nothing in this project edits. Null when the file had none.
	/// </summary>
	public byte[]? ShadeRampBytes { get; set; }

	public DynamixPalette() {
		Index0AlphaKey = new ColorBytes(218, 164, 164, 255);
		Index0AlphaKey.SetColor(RgbaColor.FromArgb(255, 218, 164, 164));
	}

	public ColorBytes ColorAt(int idx) => Colors[idx];

	public int[] ToIntColorMap() {
		var cmap = new int[256];

		foreach (var shade in Colors.Keys) {
			cmap[shade] = Colors[shade].GetColor().ToArgb();
		}

		return cmap;
	}

	public byte[] ToByteArray() {
		var index = new List<byte>();

		foreach (var shade in Colors.Keys) {
			var arr = Colors[shade].Array;
			index.Add(arr[0]);
			index.Add(arr[1]);
			index.Add(arr[2]);
		}

		return index.ToArray();
	}
}
