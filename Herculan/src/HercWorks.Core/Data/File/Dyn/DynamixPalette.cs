using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Dyn;

/// <summary>
/// FILE - VOL file, .DPL — contains palette colors per-game object; some real optimization
/// there. Ported from org.hercworks.core.data.file.dyn.DynamixPalette.
/// </summary>
public class DynamixPalette {
	/// <summary>
	/// FIXED — see KNOWN_ISSUES.md history: the original Java built this from
	/// Bytes.from("0F002800", StandardCharsets.UTF_8) — the literal 8-byte ASCII/UTF-8 encoding of
	/// that string, not a hex-decoded 4-byte value, despite looking like hex. Confirmed wrong
	/// against a real .DPL file (ES2\VOL\SHELL0\DPL\ALPHA.DPL): its actual first 4 content bytes
	/// are 0F 00 28 00 — the genuine hex-decoded value — not the 8-byte ASCII string. Fixed to the
	/// real 4-byte value; DynamixPaletteTransformer.BytesToObject's read path already only ever
	/// skipped 4 bytes for this header (not the 8 the old ASCII encoding would have needed),
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
