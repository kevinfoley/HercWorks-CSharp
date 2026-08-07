using HercWorks.Core.Data.Struct;
using HercWorks.Vol;
using System.Text;

namespace HercWorks.Core.Data.File.Dyn;

/// <summary>
/// FILE - VOL file, .DPL — contains palette colors per-game object; some real optimization
/// there. Ported from org.hercworks.core.data.file.dyn.DynamixPalette.
/// </summary>
public class DynamixPalette : DataFile {
	/// <summary>
	/// Original: Bytes.from("0F002800", StandardCharsets.UTF_8) — like InitHerc.Header, this
	/// looks like a hex string but is literally the UTF-8/ASCII bytes of that 8-character text.
	/// Ported literally.
	/// </summary>
	public static readonly byte[] Header = Encoding.UTF8.GetBytes("0F002800");

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
		Index0AlphaKey.SetColor(System.Drawing.Color.FromArgb(255, 218, 164, 164));
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
