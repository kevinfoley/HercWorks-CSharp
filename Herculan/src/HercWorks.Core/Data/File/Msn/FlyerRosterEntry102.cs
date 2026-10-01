namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #13 (102 bytes/record) — one flyer or ground vehicle the mission can field, exported as
/// <c>script.dat</c> block 8 (<see cref="Script.ScriptEntity102Export"/>). A cleanly-gated
/// template-inheritance branch (0x04): -1 = read fresh, anything else = wholesale-copy the rest of
/// the record from the referenced parent. <see cref="FlagsA"/> is the real "Flags" array the row's
/// old name gestured at — 20 shorts, not the 49 the old model assumed — and its purpose is not
/// established; the record's read-out fields are <see cref="TypeIndex"/>, the two position refs, the
/// out-of-action report and the two action refs.
/// The member names match <see cref="Script.ScriptEntity102Export"/>'s and the engine's
/// <c>MissionPlacement</c> where they are the same datum.
/// See docs/formats/msn-mission-file.md, "Row #13 field decode".
/// </summary>
public class FlyerRosterEntry102 : MapObject {
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

	/// <summary>0x08-0x2F — 20 shorts, 100% populated, values only 0 or 1 (96.5% 0). What reads them is not established.</summary>
	public short[] FlagsA { get; set; } = new short[20];

	/// <summary>0x30 — ref into row #6 (<see cref="MapPoint22"/>) — a spawn-position override; always -1 in real data, but live: DBSIM reads it.</summary>
	public short PositionRef { get; set; }

	/// <summary>0x32 — ref into row #7 (<see cref="Heading10"/>) — the heading override, on the same terms.</summary>
	public short HeadingRef { get; set; }

	/// <summary>
	/// 0x34 — the flyer type, an index into <c>nam\FLYERS.NAM</c>; 68% real, every real value
	/// exactly 0 (retail ships one flyer chassis with data, <c>SKIMMER</c>).
	/// </summary>
	public short TypeIndex { get; set; }

	/// <summary>0x36 — how many of the (counter ref, operation) pairs at 0x38 are filled, from the front; 1 in one retail record, 0 in the rest. Not exported to script.dat, not copied by a variant.</summary>
	public short PairCount { get; set; }

	/// <summary>
	/// 0x38-0x5F — the flyer's out-of-action report: ten interleaved (counter ref, operation) pairs,
	/// written to the mission counters when it goes out of the fight. 99.9% -1; one retail record uses
	/// a slot. Copied wholesale by inheritance. The export separates them into
	/// <see cref="Script.ScriptEntity102Export.CounterRefs"/> and
	/// <see cref="Script.ScriptEntity102Export.CounterOps"/>.
	/// </summary>
	public short[] OutOfActionReport { get; set; } = new short[20];

	/// <summary>0x60 — ref into row #10 (<see cref="Action82"/>): the action this flyer fires when it is engaged; always -1 in real data.</summary>
	public short EngagementActionRef { get; set; }

	/// <summary>0x62 — ref into row #10: the action this flyer fires when it is defeated; the only one of the record's refs genuinely exercised (21% real).</summary>
	public short DefeatActionRef { get; set; }

	/// <summary>
	/// 0x64 — trailing field, always exactly 100 in all real data. The mech and base rows end in a
	/// starting-condition percentage of the same 100-or-less shape, but nothing establishes that this
	/// one is read as one, so it keeps its placeholder name.
	/// </summary>
	public short UnkVal_100 { get; set; }
}
