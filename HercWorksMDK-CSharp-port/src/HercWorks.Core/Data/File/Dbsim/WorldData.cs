using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/WLD/ worldX.wld — sky/world rendering parameters for one map's environment.
///   0 - UINT16 - always 2
///   2 - UINT16 - sky palette id
///   4 - UINT16 - sky horizon height
///   6 - UINT16 - sky horizon start height
///   8 - UINT16 - unknown, varies
///   10 - UINT16 - unknown, always 1
///   12 - UINT16 - unknown, always 1
///   14 - UINT16 - spacer, always 0
///   16 - UINT16 - unknown, always 233
///   18 - UINT16 - unknown, always 1
///   20 - UINT16 - unknown, always 47
///   22 - UINT16 - unknown, always 223
///   24 - UINT16 - unknown, always 6000
///   26 - UINT16 - unknown, always 7
///   28 - UINT16 - unknown, always 16
///   30 - UINT16 - spacer, always 0
///   32 - UINT32 - unknown, always 60000
///   36 - UINT32 - unknown, always 64400
///   40 - <see cref="MidSectionA"/> — 190 bytes (95 shorts), NOT decoded. Confirmed against real
///     data to contain a real (non-zero) 14-value UINT32 arithmetic progression (step +4400,
///     starting ~68800) repeated twice, each repeat preceded by the same 60000/64400 pair seen at
///     offset 32/36 — plausibly terrain/fog distance-band thresholds, but not confirmed further.
///   230 - <see cref="MidSectionB"/> — 48 bytes (24 shorts) immediately before the string section,
///     also NOT decoded (the original Java doc comment guessed this section started around offset
///     240 with several "always N" observations, but that didn't line up with real file offsets
///     when checked here — the values it lists don't appear at the real offset 240).
///   278 - 8-byte null-terminated string - world type, always "world24" in retail data (possibly
///     a version/format tag, per the original author's guess).
///   286 - 8-byte null-terminated string - clouds name, always "clouds2" in retail data (matches
///     the Java doc's "clouds1/clouds2" — ES2 apparently only ships clouds2).
///   294 - 8-byte null-terminated string - impact graphic id, "impact0".."impact9" (one per world
///     file — WORLDn.WLD always pairs with "impactN").
///   302 - two null-terminated strings running to EOF: ground texture base name ("urban", "bsnow",
///     "volcan", "ice", "moon" observed) then literally the extension "tex" — NOT one dotted
///     "name.tex" string as the original Java doc comment guessed; confirmed against real bytes
///     the two are separately null-terminated.
/// Ported from org.hercworks.core.data.file.dbsim.WorldData; extended here with a working
/// transformer and the confirmed string-section layout (2026-08-08) — the original Java version
/// only ever modeled the header fields (TODO "finish" left in the source, never completed) and had
/// no transformer at all.
/// </summary>
public class WorldData : DataFile {
	public short Unk0_val2 { get; set; } = 2;

	public short SkyPaletteId { get; set; } = 208;
	public short SkyHorizonHeight { get; set; }
	public short SkyHorizonStartHeight { get; set; }

	public short Unk8_val { get; set; }
	public short Unk10_val { get; set; } = 1;
	public short Unk12_val { get; set; } = 1;

	public short Spacer14 { get; set; }

	public short Unk16_val { get; set; } = 233;
	public short Unk18_val { get; set; } = 1;
	public short Unk20_val { get; set; } = 47;
	public short Unk22_val { get; set; } = 223;
	public short Unk24_val { get; set; } = 6000;
	public short Unk26_val { get; set; } = 7;
	public short Unk28_val { get; set; } = 16;

	public short Spacer30 { get; set; }

	public int Unk32_val { get; set; } = 60000;
	public int Unk34_val { get; set; } = 64400;

	/// <summary>Raw, undecoded 190-byte block at content offset 40 — see class doc comment.</summary>
	public byte[]? MidSectionA { get; set; }

	/// <summary>Raw, undecoded 48-byte block at content offset 230 — see class doc comment.</summary>
	public byte[]? MidSectionB { get; set; }

	public string? WorldTypeStr { get; set; }
	public string? CloudStr { get; set; }
	public string? ImpactSt { get; set; }
	public string? TextureBaseName { get; set; }
	public string? TextureExtension { get; set; }

	public WorldData() { }

	public WorldData(string fileName, string dirPath) : base(fileName, dirPath) { }

	/// <summary>Unused in the original (no accessors, no instances created) — ported as-is.</summary>
	public class Sky {
		public short PaletteId { get; set; }
		public short HorizonHeight { get; set; }
		public short StartHeight { get; set; }
	}
}
