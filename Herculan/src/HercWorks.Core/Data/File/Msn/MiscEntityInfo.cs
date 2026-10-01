namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #14 (62 bytes/record) — the largest real sample decoded this session (1,949 instances).
/// One structure the mission can place, exported as <c>script.dat</c> block 9
/// (<see cref="Script.ScriptMiscEntityExport"/>). Structurally the same shape as row #13: a
/// cleanly-gated inherit branch, four resolved refs in the fresh branch. <see cref="TrailingField"/>
/// reads as a `HealthModAdjust`-style percentage (100 = default) that's only meaningfully set when
/// <see cref="TypeIndex"/> is itself populated (99% correlated); what reads it is not established.
/// <see cref="TypeIndex"/> is never resolved via any lookup function in the load loop, so it's kept
/// as a raw short here rather than eagerly resolved against a LUT.
/// The member names match <see cref="Script.ScriptMiscEntityExport"/>'s and the engine's
/// <c>MissionPlacement</c> where they are the same datum.
/// See docs/formats/msn-mission-file.md, "Row #14 field decode".
/// </summary>
public class MiscEntityInfo : MapObject {
	/// <summary>0x02 — condition ref; 30% real, same elevated-usage tier as rows #1/#3/#13.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — parent/inherit index; only 0.4% real, dead-in-practice here unlike row #13's version.</summary>
	public short InheritIndex { get; set; }

	/// <summary>0x06 — always -1; dead, excluded from the inherit-copy range.</summary>
	public short Unk06 { get; set; }

	/// <summary>
	/// 0x08 — the base type, an index into the 65-entry table in <c>dat\BASES.DAT</c>; 71% real
	/// (43 distinct values, 0-56). Strongly correlated with <see cref="TrailingField"/>.
	/// </summary>
	public short TypeIndex { get; set; }

	/// <summary>0x0A — ref into row #6 (<see cref="MapPoint22"/>) — this structure's spawn-position override; sparse, 6.4% real.</summary>
	public short PositionRef { get; set; }

	/// <summary>0x0C — ref into row #7 (<see cref="Heading10"/>) — its heading; sparse, 6.7% real, narrow domain (only 10 distinct GUIDs referenced).</summary>
	public short HeadingRef { get; set; }

	/// <summary>0x0E — how many of the (counter ref, operation) pairs at 0x10 are filled, from the front: 0 (64%), 1 (33%), or 2 (3%). Not exported to script.dat.</summary>
	public short PairCount { get; set; }

	/// <summary>
	/// 0x10-0x37 — the structure's out-of-action report: ten interleaved (counter ref, operation)
	/// pairs, written to the mission counters when it goes out of the fight. Sparse (3.9% of slots
	/// populated); real operations concentrate on 2 (half of all populated slots) plus long runs of
	/// consecutive values. The export separates them into
	/// <see cref="Script.ScriptMiscEntityExport.CounterRefs"/> and
	/// <see cref="Script.ScriptMiscEntityExport.CounterOps"/>.
	/// </summary>
	public short[] OutOfActionReport { get; set; } = new short[20];

	/// <summary>0x38 — ref into row #10 (<see cref="Action82"/>): the action this structure fires when it is engaged; rare, 0.4% real.</summary>
	public short EngagementActionRef { get; set; }

	/// <summary>0x3A — ref into row #10: the action it fires when its last component goes; essentially dead, 0.1% real.</summary>
	public short DefeatActionRef { get; set; }

	/// <summary>
	/// 0x3C — trailing field, always populated: 100 (71%) or 0 (29%). ~99% correlated with whether
	/// <see cref="TypeIndex"/> is populated — a `HealthModAdjust`-style default.
	/// </summary>
	public short TrailingField { get; set; }
}
