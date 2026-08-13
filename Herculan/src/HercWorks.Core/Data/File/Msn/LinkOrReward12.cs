namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #9 (12 bytes/record) — a typed dual-purpose record sharing one layout, selected by
/// <see cref="TypeFlag"/>: a **link** (0, two refs to row #6 world positions forming a path leg —
/// real position-distance data shows the two endpoints are at genuine map distance, not spatially
/// adjacent) or a **reward/value marker** (1, one ref to a row #6 position plus a literal
/// round-number quantity — most likely a credits/reward value given the game's credit-based
/// Herc/weapon economy). See docs/formats/msn-mission-file.md, "Row #9 field decode".
/// </summary>
public class LinkOrReward12 : MapObject {
	/// <summary>0x02 — condition ref; always -1 in all real data.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — always -1; no read/write anywhere in this record's load loop.</summary>
	public short Unk04 { get; set; }

	/// <summary>0x06 — 0 = link (both refs resolve into row #6), 1 = reward (RefOrLiteral is a literal value).</summary>
	public short TypeFlag { get; set; }

	/// <summary>0x08 — ref into row #6 (<see cref="MapPoint22"/>); always resolved regardless of <see cref="TypeFlag"/>.</summary>
	public short RefA { get; set; }

	/// <summary>
	/// 0x0A — when <see cref="TypeFlag"/> is 0: a second ref into row #6. When 1: an unresolved
	/// literal value (real values are a small closed set of round numbers, e.g. 100, 500, ...,
	/// 20000 — never overlapping the index-like range seen when the type is a link).
	/// </summary>
	public short RefBOrLiteral { get; set; }
}
