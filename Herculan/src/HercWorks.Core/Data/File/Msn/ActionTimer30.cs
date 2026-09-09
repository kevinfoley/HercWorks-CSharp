namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #11 (30 bytes/record) — <b>a mission timer</b>. It names a primary row #10
/// (<see cref="Action82"/>) action that arms it, a delay, and up to ten more actions to fire when
/// that delay runs out. DBSIM resolves and fires all ten slots; retail data merely never populates
/// more than one (96% of records use slot 0 alone, 3 of 72 use none). See
/// docs/formats/msn-mission-file.md, "Row #11 field decode", and docs/simulation/mission-deployment.md
/// for what DBSIM does with it.
/// </summary>
public class ActionTimer30 : MapObject {
	/// <summary>0x02 — condition ref; always -1 in all real data.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — always -1; dead, same shape as elsewhere.</summary>
	public short Unk04 { get; set; }

	/// <summary>
	/// 0x06 — the action that arms the timer, a ref into row #10 (<see cref="Action82"/>); populated
	/// in 82% of real records. Unset means the timer runs from mission start.
	/// </summary>
	public short PrimaryActionRef { get; set; }

	/// <summary>
	/// 0x08 — the delay. DBSIM shifts it left 11 into milliseconds, so the unit is 2.048 seconds and
	/// the dominant stored 10 is a little over 20 s.
	/// </summary>
	public short TimerValue { get; set; }

	/// <summary>
	/// 0x0A-0x1D — the actions fired when the delay runs out, 10 ref slots into row #10. DBSIM reads
	/// and fires all ten; retail data populates at most slot 0 (3 of 72 records populate none).
	/// </summary>
	public short[] SequenceRefs { get; set; } = new short[10];
}
