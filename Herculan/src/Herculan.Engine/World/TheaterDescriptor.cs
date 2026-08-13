using System.Buffers.Binary;
using System.Text;
using Herculan.Engine.Content;

namespace Herculan.Engine.World;

/// <summary>
/// One theater's <c>wld\WORLD&lt;n&gt;.WLD</c> descriptor — the file that names, in data rather than
/// in code, which texture bank the terrain wears.
///
/// <para>Decoded from <c>maybe_World_LoadTheater</c> (<c>0042e010</c>), which reads this file
/// field-by-field off a stream and finishes by handing one of its strings to
/// <c>Terrain_BindTextureBank</c>. See docs/formats/terrain-texturing.md.</para>
///
/// <para><b>There are ten descriptors for five theaters.</b> The original builds the base name as
/// <c>world&lt;theaterIndex * 2 + variant&gt;</c>, and retail data pairs up exactly:
/// WORLD0/1 <c>urban</c>, WORLD2/3 <c>bsnow</c>, WORLD4/5 <c>volcan</c>, WORLD6/7 <c>ice</c>,
/// WORLD8/9 <c>moon</c>. Both indices come from <see cref="ScriptDatHeader"/>.</para>
///
/// <para>Most of the file is still undecoded and is skipped rather than guessed at: two int32
/// arrays, two colour-ramp tables sized by a row/column pair, and about a dozen loose 16- and 32-bit
/// fields. What <i>is</i> certain is the structure, because reading it as written below consumes
/// every one of the ten retail files to its exact last byte, and the strings that fall out of the
/// end are real resource names (see <see cref="TerrainBankName"/> and
/// <see cref="ImpactPaletteName"/>).</para>
/// </summary>
public sealed class TheaterDescriptor {
	/// <summary>Resource folder and extension (the game uses one word for both).</summary>
	public const string ResourceFolder = "wld";

	/// <summary>How many <c>WORLD&lt;n&gt;.WLD</c> files retail data ships.</summary>
	public const int Count = 10;

	private TheaterDescriptor(int index, string paletteName, string terrainBankName,
			string impactPaletteName, IReadOnlyList<string> allStrings) {
		Index = index;
		PaletteName = paletteName;
		TerrainBankName = terrainBankName;
		ImpactPaletteName = impactPaletteName;
		Strings = allStrings;
	}

	/// <summary>Which <c>WORLD&lt;n&gt;</c> this is — <c>theaterIndex * 2 + variant</c>.</summary>
	public int Index { get; }

	/// <summary>
	/// The theater's palette, <c>dpl\WORLD&lt;n&gt;.DPL</c>. Not a string in the file — the name is
	/// the descriptor's own, and <c>maybe_World_LoadTheater</c> loads it as its very first act, which
	/// is what makes the retail WORLD0..9 <c>.DPL</c> family a per-theater palette set.
	/// </summary>
	public string PaletteName { get; }

	/// <summary>
	/// The terrain texture bank's base name (<c>urban</c>, <c>bsnow</c>, <c>volcan</c>, <c>ice</c>,
	/// <c>moon</c>) — the string the original passes to <c>Terrain_BindTextureBank</c>, which loads
	/// <c>dba\&lt;name&gt;.DBA</c>. All five exist in retail data and nothing else in the file names
	/// one of them.
	/// </summary>
	public string TerrainBankName { get; }

	/// <summary>
	/// The theater's impact/explosion palette base name (<c>impact&lt;n&gt;</c>), loaded from the
	/// <c>dpl</c> folder. Recorded because it independently corroborates the string order: all ten
	/// <c>IMPACT0.DPL</c>–<c>IMPACT9.DPL</c> exist and each descriptor names its own.
	/// </summary>
	public string ImpactPaletteName { get; }

	/// <summary>
	/// All five trailing strings in file order, for anyone decoding the rest. In retail data the
	/// other two are constant (<c>world24</c>, <c>clouds2</c>) plus a trailing <c>tex</c>; none names
	/// a file that exists, and the original reads them into a scratch buffer and discards them.
	/// </summary>
	public IReadOnlyList<string> Strings { get; }

	/// <summary>
	/// Loads <c>wld\WORLD&lt;theaterIndex * 2 + variant&gt;.WLD</c>.
	/// </summary>
	public static TheaterDescriptor Load(GameContent content, int theaterIndex, int variant = 0) =>
		LoadByWorldIndex(content, theaterIndex * 2 + variant);

	/// <summary>Loads a descriptor by its literal <c>WORLD&lt;n&gt;</c> number.</summary>
	public static TheaterDescriptor LoadByWorldIndex(GameContent content, int worldIndex) {
		if (worldIndex < 0 || worldIndex >= Count) {
			throw new ArgumentOutOfRangeException(nameof(worldIndex), worldIndex,
				$"Retail data has WORLD0..WORLD{Count - 1}.");
		}

		string name = $"WORLD{worldIndex}";
		return Parse(content.ReadRequired(ResourceFolder, name + ".WLD"), worldIndex, name);
	}

	/// <summary>
	/// Walks the descriptor exactly as <c>maybe_World_LoadTheater</c> does. Every read below is one
	/// stream call in the original, in this order; the sizes are its own.
	/// </summary>
	private static TheaterDescriptor Parse(byte[] bytes, int worldIndex, string baseName) {
		int p = 0;

		short Int16() {
			short value = BinaryPrimitives.ReadInt16LittleEndian(Take(2));
			return value;
		}

		int Int32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

		ReadOnlySpan<byte> Take(int count) {
			if (p + count > bytes.Length) {
				throw new InvalidDataException(
					$"{ResourceFolder}\\{baseName}.WLD ended after {bytes.Length} bytes, mid-field at {p}.");
			}
			var span = bytes.AsSpan(p, count);
			p += count;
			return span;
		}

		string String() {
			int start = p;
			while (p < bytes.Length && bytes[p] != 0) {
				p++;
			}
			if (p >= bytes.Length) {
				throw new InvalidDataException(
					$"{ResourceFolder}\\{baseName}.WLD ended inside an unterminated string at {start}.");
			}
			string value = Encoding.ASCII.GetString(bytes, start, p - start);
			p++;
			return value;
		}

		// 8 shorts, then 6 more. Several are dispatched straight into subsystem setup calls
		// (0042ebbc, the sky/haze globals) rather than stored as a struct, so they are skipped here
		// until whatever reads them is ported.
		for (int i = 0; i < 14; i++) {
			Int16();
		}

		// Two count-prefixed int32 arrays. Retail files carry 16 entries each, identical to one
		// another, ascending in even steps — a distance/time ramp of some kind, consumer not traced.
		for (int array = 0; array < 2; array++) {
			int count = Int32();
			if (count < 0 || p + count * 4 > bytes.Length) {
				throw new InvalidDataException(
					$"{ResourceFolder}\\{baseName}.WLD declares {count} entries in array {array}, which does not fit.");
			}
			Take(count * 4);
		}

		// A rows x cols colour-ramp pair: two tables of `cols` int32s each, with a loose short
		// between them, then two more 4-byte entries that get expanded the same way (FUN_00430d08).
		int rampRows = Int16();
		int rampColumns = Int16();
		if (rampColumns < 0 || rampRows < 0) {
			throw new InvalidDataException(
				$"{ResourceFolder}\\{baseName}.WLD has a negative ramp size ({rampRows}x{rampColumns}).");
		}
		Take(rampColumns * 4);
		Int16();
		Take(rampColumns * 4);
		Take(4);
		Take(4);

		Int16();
		Int16();
		Int32();
		Int32();

		// Five null-terminated strings. The original reads the first three into one scratch buffer,
		// keeping only the third (an impact palette) as it goes, then reads the fourth into that same
		// buffer — and it is the buffer's contents at the end of the function, i.e. the fourth string,
		// that Terrain_BindTextureBank receives.
		string[] strings = { String(), String(), String(), String(), String() };

		return new TheaterDescriptor(worldIndex, baseName, strings[3], strings[2], strings);
	}
}
