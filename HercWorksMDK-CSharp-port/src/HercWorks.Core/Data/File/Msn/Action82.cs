namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #10 (82 bytes/record) — referenced by four other record types via a type-discriminated
/// pointer (rows #12/#13/#14/#16), making it the strongest lead on actual mission
/// objective/trigger logic. Real payload is much smaller than 82 bytes suggests: two nominal
/// fixed-size arrays (8 slots, 5 slots) never use more than their first few slots in any real
/// mission, and a 42-byte middle span is constant (`0000` then twenty `-1` shorts) in 337/338 real
/// records. See docs/formats/msn-mission-file.md, "Row #10 field decode".
/// </summary>
public class Action82 : MapObject {
	/// <summary>0x02 — condition ref; always -1 in all real data.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — always -1; no observed use anywhere.</summary>
	public short Unk04 { get; set; }

	/// <summary>
	/// 0x06 — type/category discriminator, also read later by the caller-side type-7/8/9/10 remap
	/// that resolves <see cref="Target"/>. Codes 8 and 10 are valid switch cases but never occur in
	/// retail data.
	/// </summary>
	public short Type { get; set; }

	/// <summary>
	/// 0x08 — verb/operation code (how the <see cref="RefsRow9"/> sub-refs combine — AND/OR/sequence
	/// — or a priority level; not confirmed). Verb 3 correlates strongly (97%) with "link"-type row
	/// #9 sub-refs; verbs 1/2 lean toward "reward"-type but aren't exclusive.
	/// </summary>
	public short Verb { get; set; }

	/// <summary>
	/// 0x0A-0x19 — declared 8-slot ref array into row #9 (<see cref="LinkOrReward12"/>), authored as
	/// row #9 GUIDs (100% match rate confirmed against real row #9 data). Real usage never exceeds
	/// the first ~4 slots; slots 4-7 are always -1.
	/// </summary>
	public short[] RefsRow9 { get; set; } = new short[8];

	/// <summary>
	/// 0x1A-0x43 — 21-short (42-byte) span, constant (`0000` then twenty `-1` shorts) in 337/338
	/// real records. Functionally dead space in virtually all real missions; round-tripped raw
	/// rather than assumed constant on write, since it isn't provably constant in every possible file.
	/// </summary>
	public short[] ConstantSpan { get; set; } = new short[21];

	/// <summary>
	/// 0x44-0x4D — declared 5-slot ref array into the shared herc/unit-type LUT. Real usage never
	/// exceeds the first slot; slots 1-4 are always -1.
	/// </summary>
	public short[] LutRefs { get; set; } = new short[5];

	/// <summary>0x4E — secondary value; mostly 0, otherwise a discrete small number. Not pinned down.</summary>
	public short SecondaryValue { get; set; }

	/// <summary>
	/// 0x50 — polymorphic entity ref, type chosen by <see cref="Type"/> (7 -> row #12, 8 -> row #13
	/// [never exercised], 9 -> row #14, 10 -> row #16 [never exercised]). Only 4/338 real records
	/// use it at all.
	/// </summary>
	public short Target { get; set; }
}
