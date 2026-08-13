using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/RMP/ worldX.rmp — paired 1:1 by name with the corresponding worldX.wld (see
/// <see cref="WorldData"/>). Likely terrain data for that world (a heightmap or terrain-type/index
/// map), but NOT confirmed — see the honesty caveat below.
///   0 - UINT32 - always 32 in every real file checked.
///   4 - UINT32 - always 12 in every real file checked (32*12 = 384, which shows up again below).
///   8.. - <see cref="Body"/>, 98304 bytes to EOF in every real file checked, genuinely different
///     per world (not shared/templated data).
///
/// New (no Java equivalent — not a ported format). Investigated 2026-08-08: real per-world byte
/// content is far too locally irregular (short runs, sharp jumps between neighboring values) to be
/// a smooth real-world-style heightmap rendered directly as grayscale — but a per-width
/// vertical-continuity check (average difference between a byte and the byte exactly N positions
/// later) shows a sharp, isolated improvement at N=256 (and weaker echoes at its multiples 512,
/// 768) that random nearby widths don't show — evidence consistent with the data really being a
/// 256-wide 2D grid (384 rows: 98304 / 256 = 384, and 384 = 32*12, tying back to the header) even
/// though a naive grayscale render of it doesn't look like an obviously "smooth" heightmap. NOT
/// confirmed to the same standard as this session's other new parsers — treat the 256x384 grid
/// shape as a real, evidence-backed hypothesis, not a fact, and treat individual byte values'
/// meaning (height? terrain type index? something else?) as completely unknown. Exposed as a flat
/// array rather than a typed 2D grid so this class doesn't silently assert a width/height that
/// might be wrong.
/// </summary>
public class TerrainRampFile : DataFile {
	public int Unk0_val { get; set; } = 32;
	public int Unk4_val { get; set; } = 12;

	/// <summary>Raw per-world data, likely (not confirmed) a 256-wide x 384-tall byte grid — see class doc comment.</summary>
	public byte[]? Body { get; set; }
}
