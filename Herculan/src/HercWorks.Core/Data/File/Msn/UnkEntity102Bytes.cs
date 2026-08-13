namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #13 (102 bytes/record). The macro-structure pass's cross-ref note ("inherit only") undersold
/// this row — reading the fresh-data branch (not just the inherit branch) turned up four real
/// cross-refs the macro pass missed entirely. A cleanly-gated template-inheritance branch (0x04):
/// -1 = read fresh, anything else = wholesale-copy the rest of the record from the referenced
/// parent. The first of two 20-short flag blocks (<see cref="FlagsA"/>) is the real "Flags" array
/// the old name gestured at — just half the declared length the old model assumed (20, not 49).
/// See docs/formats/msn-mission-file.md, "Row #13 field decode".
/// </summary>
public class UnkEntity102Bytes : MapObject {
	/// <summary>0x02 — condition ref; 24% real, unusually high (row #1/#3 are the only comparably high rows).</summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — parent/inherit index; 30% real, the highest inheritance usage of any row decoded at
	/// the time this row was analyzed. Both condition and inheritance are simultaneously well-used
	/// here, unlike most other rows where one dominates or both are dead.
	/// </summary>
	public short InheritIndex { get; set; }

	/// <summary>0x06 — always -1; dead, excluded from the inherit-copy range.</summary>
	public short Unk06 { get; set; }

	/// <summary>0x08-0x30 — the real, genuinely-used flag array (20 shorts), values only 0 or 1.</summary>
	public short[] FlagsA { get; set; } = new short[20];

	/// <summary>0x30 — ref into row #6 (<see cref="MapPoint22"/>); declared, resolved, but always -1 in real data.</summary>
	public short RefRow6 { get; set; }

	/// <summary>0x32 — ref into row #7 (<see cref="Flag10"/>); declared, resolved, but always -1 in real data.</summary>
	public short RefRow7 { get; set; }

	/// <summary>0x34 — binary field, 68% real, every real value exactly 0 — a presence flag, not a scaled value.</summary>
	public short BinaryField { get; set; }

	/// <summary>0x36 — not part of the inherit-copy list; essentially always 0.</summary>
	public short Unk36 { get; set; }

	/// <summary>0x38-0x60 — second 20-short flag block, copied wholesale by inheritance but essentially always -1 (functionally inert).</summary>
	public short[] FlagsB { get; set; } = new short[20];

	/// <summary>0x60 — ref into row #10 (<see cref="Action82"/>), slot 1; declared, resolved, but always -1 in real data.</summary>
	public short RefRow10Slot1 { get; set; }

	/// <summary>0x62 — ref into row #10, slot 2; the only one of the four declared cross-refs genuinely exercised (21% real).</summary>
	public short RefRow10Slot2 { get; set; }

	/// <summary>0x64 — trailing field, always exactly 100 in all real data (a hardcoded constant, not a variable value).</summary>
	public short UnkVal_100 { get; set; }
}
