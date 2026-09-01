namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #16 (164 bytes/record) — the richest record type in the file: a position/flag/route/action
/// quartet of cross-refs (rows #6/#7/#8/#10) layered with two declared-capacity-but-sparse ref
/// arrays (a 20-entry discriminated array into the #12/#13/#14 polymorphic family, and a 10-entry
/// plain array into row #15), plus a cleanly discriminated 3-way trailing payload
/// (<see cref="TrailingDiscriminator"/>) no other row in the file exhibits this cleanly. The
/// 20-entry array matched the old model's `MapEntIds[20]`/`MapEntities[20]` fields exactly, the
/// "strongest correspondence" the macro-structure pass found for any existing type — the rest of
/// this row's real shape (all 82 short-offsets analyzed individually, not just the ones the load
/// code explicitly resolves) went well beyond that. See docs/formats/msn-mission-file.md, "Row #16
/// field decode".
/// </summary>
public class EntitySpawn164 : MapObject {
	/// <summary>0x02 — condition ref; sparse, 2.5% real.</summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — compound-condition partner; 1.4% real, every real value exactly -99, always
	/// co-occurring with a real <see cref="ConditionRef"/>. Same idiom as row #15's 0x02/0x06 and
	/// row #12's 0x02/0x06.
	/// </summary>
	public short CompoundConditionPartner { get; set; }

	/// <summary>0x06 — binary flag, always populated, roughly 39/61 split.</summary>
	public short BinaryFlag { get; set; }

	/// <summary>0x08 — near-constant, always populated, essentially always 0 (1 exception).</summary>
	public short NearConstant { get; set; }

	/// <summary>0x0A-0x2C — 18-short dead zone, always exactly 0 in all real data (not a -1-sentinel span); round-tripped raw.</summary>
	public short[] DeadZone { get; set; } = new short[18];

	/// <summary>
	/// 0x2E — discriminator selecting the target row type for <see cref="DiscriminatedRefs"/>: 0 =
	/// row #12, 1 = row #13, 2 = row #14. 89% real.
	/// </summary>
	public short Discriminator { get; set; }

	/// <summary>0x30 — small discrete field, 85% real, range 0-16, all 17 values used. Meaning undetermined.</summary>
	public short SmallDiscrete { get; set; }

	/// <summary>0x32 — ref into row #6 (<see cref="MapPoint22"/>); 37% real.</summary>
	public short RefRow6 { get; set; }

	/// <summary>0x34 — ref into row #7 (<see cref="Heading10"/>); 45% real.</summary>
	public short RefRow7 { get; set; }

	/// <summary>0x36 — ref into row #8 (<see cref="WaypointGroup"/>); 43% real.</summary>
	public short RefRow8 { get; set; }

	/// <summary>
	/// 0x38-0x5E — 20-entry discriminated ref array (see <see cref="Discriminator"/>); real usage
	/// decays sharply, from 89% at slot 0 to 0% by slot 9 onward.
	/// </summary>
	public short[] DiscriminatedRefs { get; set; } = new short[20];

	/// <summary>
	/// 0x60-0x72 — 10-entry plain ref array into row #15 (<see cref="LinkedRef22"/>); same decay
	/// shape, 47% at slot 0 down to 0% by slot 3.
	/// </summary>
	public short[] Row15Refs { get; set; } = new short[10];

	/// <summary>0x74 — tri-state flag, 89% real (0 or 1), else -1.</summary>
	public short TriStateFlag { get; set; }

	/// <summary>0x76 — ref into row #10 (<see cref="Action82"/>); 31% real.</summary>
	public short RefRow10 { get; set; }

	/// <summary>
	/// 0x78 — discriminator that cleanly selects how many of the four trailing payload fields are
	/// populated: 0 = none, 1 = <see cref="Payload1"/>/<see cref="Payload2"/> both populated (100%
	/// of the time), 2 = all four trailing fields populated (100% of the time).
	/// </summary>
	public short TrailingDiscriminator { get; set; }

	/// <summary>0x7A — payload field 1; present iff <see cref="TrailingDiscriminator"/> &gt;= 1. Unusually wide range (20-650).</summary>
	public short Payload1 { get; set; }

	/// <summary>0x7C — payload field 2; present iff <see cref="TrailingDiscriminator"/> &gt;= 1. Narrow domain, only 2 or 23 observed.</summary>
	public short Payload2 { get; set; }

	/// <summary>0x7E — payload field 3; present iff <see cref="TrailingDiscriminator"/> == 2 only.</summary>
	public short Payload3 { get; set; }

	/// <summary>0x80 — payload field 4; present iff <see cref="TrailingDiscriminator"/> == 2 only, always exactly 2 when present.</summary>
	public short Payload4 { get; set; }

	/// <summary>0x82-0xA0 — 16-short dead zone, always -1 in all real data; round-tripped raw.</summary>
	public short[] DeadZone2 { get; set; } = new short[16];

	/// <summary>0xA2 — trailing flag; sparse, 6% real, values 0/1.</summary>
	public short TrailingFlag { get; set; }
}
