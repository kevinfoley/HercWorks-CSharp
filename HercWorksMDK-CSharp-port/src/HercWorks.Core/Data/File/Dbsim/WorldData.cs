using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/WLD/ worldX.wld — mostly-unidentified sky/world rendering parameters (sky
/// palette id, horizon heights, several unknown fixed values), followed by four strings: world
/// type (e.g. "world24"), clouds name, impact graphic id, and ground texture name (variable
/// length, runs to EoF). See Java source for the full documented byte layout. TODO (carried over
/// from Java): finish.
/// Ported from org.hercworks.core.data.file.dbsim.WorldData.
/// </summary>
public class WorldData : DataFile {
	public short Unk0_val2 { get; set; } = 2;

	public short SkyPaletteId { get; set; } = 208;
	public short SkyHorizonHeight { get; set; }
	public short SkyHorizonStartHeight { get; set; }

	public short Unk8_val { get; set; }
	public short Unk10_val { get; set; } = 1;
	public short Unk12_val { get; set; } = 1;

	// 0x14 spacer bytes

	public short Unk16_val { get; set; } = 233;
	public short Unk18_val { get; set; } = 1;
	public short Unk20_val { get; set; } = 47;
	public short Unk22_val { get; set; } = 223;
	public short Unk24_val { get; set; } = 6000;
	public short Unk26_val { get; set; } = 7;
	public short Unk28_val { get; set; } = 16;

	// 0x14 spacer bytes

	public int Unk32_val { get; set; } = 60000;
	public int Unk34_val { get; set; } = 64400;

	// TODO (carried over from Java): finish

	public string? WorldTypeStr { get; set; }
	public string? CloudStr { get; set; }
	public string? ImpactSt { get; set; }
	public string? TexStr { get; set; }

	public WorldData() { }

	public WorldData(string fileName, string dirPath) : base(fileName, dirPath) { }

	/// <summary>Unused in the original (no accessors, no instances created) — ported as-is.</summary>
	public class Sky {
		public short PaletteId { get; set; }
		public short HorizonHeight { get; set; }
		public short StartHeight { get; set; }
	}
}
