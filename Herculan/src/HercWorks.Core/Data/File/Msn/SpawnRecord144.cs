namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #12 (144 bytes/record) — a second, distinct 144-byte record type from row #4
/// (<see cref="RewardPackage144"/>); despite sharing a byte count and both being candidate "spawn"
/// records, they have no structural relationship (this row has an identity field and the file's
/// heaviest template-inheritance usage, 48%; row #4 has neither). Its four declared cross-refs
/// (#6/#7/#10x2) turned out to be almost entirely dead in retail (&lt;=2.4% used) — the record's
/// real payload is the unresolved 10-slot array at <see cref="UnresolvedRefs"/>, never touched by
/// any lookup call the load code makes. See docs/formats/msn-mission-file.md, "Row #12 field decode".
/// </summary>
public class SpawnRecord144 : MapObject {
	/// <summary>
	/// 0x02 — condition ref; 43% real. Every record has at least one of GUID or condition
	/// populated — the file carves records into named/inheritable (GUID, 48%), named-fresh (11%),
	/// and fully anonymous/condition-only (41%) groups.
	/// </summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — parent/inherit index; 48% real, the highest inheritance usage of any row in the file.
	/// Whenever this is real, GUID is always also real (0 counterexamples).
	/// </summary>
	public short InheritIndex { get; set; }

	/// <summary>
	/// 0x06 — compound-condition partner: 3.9% real, values only -99 or 2, always co-occurring with
	/// a real <see cref="ConditionRef"/>. Same idiom as row #15's 0x02/0x06 and row #16's 0x02/0x04.
	/// </summary>
	public short CompoundConditionPartner { get; set; }

	/// <summary>0x08 — binary flag; always populated, 0/1.</summary>
	public short BinaryFlag { get; set; }

	/// <summary>0x0A — near-constant, dominant 0 (91%), otherwise a large outlier (220/255/256) — bitmask-like.</summary>
	public short NearConstant { get; set; }

	/// <summary>0x0C-0x2E — 18-short dead zone, always exactly 0 in all real data; round-tripped raw.</summary>
	public short[] DeadZone { get; set; } = new short[18];

	/// <summary>0x30 — small discrete field; 47% real, range 0-20.</summary>
	public short SmallDiscrete { get; set; }

	/// <summary>
	/// 0x32-0x44 — the record's real workhorse: an unresolved 10-slot array, never touched by any
	/// lookup call in the load loop. Usage decays from 46% (slot 0) to 0.1% (slot 9), bursty
	/// within a record (typically all-or-nothing in blocks).
	/// </summary>
	public short[] UnresolvedRefs { get; set; } = new short[10];

	/// <summary>0x46 — ref into row #6 (<see cref="MapPoint22"/>); declared, resolved, but essentially dead (0.1% real).</summary>
	public short RefRow6 { get; set; }

	/// <summary>0x48 — ref into row #7 (<see cref="Flag10"/>); declared, resolved, but never used in retail data.</summary>
	public short RefRow7 { get; set; }

	/// <summary>0x4A — small discrete field; always populated, values 0-4, dominant 0 (84%).</summary>
	public short SmallDiscrete2 { get; set; }

	/// <summary>
	/// 0x4C-0x72 — declared 10-pair (20-slot) array; usage decays in matched pairs from 15.9% down
	/// to 0.5%, remaining pairs never used. First element of each pair has an unusually wide range
	/// (20-480); second is narrow (2-23, 4 distinct values) — a (ref-or-id, small-tag) pair list.
	/// </summary>
	public short[] PairedRefs { get; set; } = new short[20];

	/// <summary>
	/// 0x74-0x84 — 9-short block, unlike every other sub-block in this record 100% populated in
	/// every real record, values tightly bounded 0-5 and trending upward across the span.
	/// </summary>
	public short[] AlwaysPopulatedBlock { get; set; } = new short[9];

	/// <summary>0x86 — always exactly 5 in all real data.</summary>
	public short Constant5 { get; set; }

	/// <summary>0x88 — always exactly 2 in all real data.</summary>
	public short Constant2 { get; set; }

	/// <summary>0x8A — ref into row #10 (<see cref="Action82"/>), slot 1; declared, resolved, nearly dead (0.7% real).</summary>
	public short RefRow10Slot1 { get; set; }

	/// <summary>0x8C — ref into row #10, slot 2; declared, resolved, nearly dead (2.4% real).</summary>
	public short RefRow10Slot2 { get; set; }

	/// <summary>
	/// 0x8E — trailing field, always populated, `HealthModAdjust`-shaped: 100 (98.5%) or 50 (1.5%).
	/// </summary>
	public short TrailingField { get; set; }
}
