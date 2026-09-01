namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #14 (62 bytes/record) — the largest real sample decoded this session (1,949 instances).
/// Structurally the same shape as row #13: a cleanly-gated inherit branch, four resolved refs in
/// the fresh branch. <see cref="TrailingField"/> reads as a `HealthModAdjust`-style percentage
/// (100 = default) that's only meaningfully set when <see cref="TypeLikeScalar"/> is itself
/// populated (99% correlated) — that correlation is the clearest signal in this row, but
/// <see cref="TypeLikeScalar"/> is never resolved via any lookup function in the load loop, so
/// it's kept as a raw short here rather than eagerly resolved against a LUT. See
/// docs/formats/msn-mission-file.md, "Row #14 field decode".
/// </summary>
public class MiscEntityInfo : MapObject {
	/// <summary>0x02 — condition ref; 30% real, same elevated-usage tier as rows #1/#3/#13.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — parent/inherit index; only 0.4% real, dead-in-practice here unlike row #13's version.</summary>
	public short InheritIndex { get; set; }

	/// <summary>0x06 — always -1; dead, excluded from the inherit-copy range.</summary>
	public short Unk06 { get; set; }

	/// <summary>
	/// 0x08 — type-like scalar, 71% real (43 distinct values, 0-56). Not resolved via any lookup
	/// function in this loop; strongly correlated with <see cref="TrailingField"/>.
	/// </summary>
	public short TypeLikeScalar { get; set; }

	/// <summary>0x0A — ref into row #6 (<see cref="MapPoint22"/>); sparse, 6.4% real.</summary>
	public short RefRow6 { get; set; }

	/// <summary>0x0C — ref into row #7 (<see cref="Heading10"/>); sparse, 6.7% real, narrow domain (only 10 distinct GUIDs referenced).</summary>
	public short RefRow7 { get; set; }

	/// <summary>0x0E — small discrete field, always populated: 0 (64%), 1 (33%), or 2 (3%).</summary>
	public short SmallDiscrete { get; set; }

	/// <summary>
	/// 0x10-0x38 — 20-short block, sparse (3.9% of slots populated). When present, real values
	/// concentrate on 2 (half of all populated slots) plus long runs of consecutive values.
	/// </summary>
	public short[] SparseBlock { get; set; } = new short[20];

	/// <summary>0x38 — ref into row #10 (<see cref="Action82"/>), slot 1; rare, 0.4% real.</summary>
	public short RefRow10Slot1 { get; set; }

	/// <summary>0x3A — ref into row #10, slot 2; essentially dead, 0.1% real.</summary>
	public short RefRow10Slot2 { get; set; }

	/// <summary>
	/// 0x3C — trailing field, always populated: 100 (71%) or 0 (29%). ~99% correlated with whether
	/// <see cref="TypeLikeScalar"/> is populated — a `HealthModAdjust`-style default.
	/// </summary>
	public short TrailingField { get; set; }
}
