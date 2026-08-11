namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #11 (30 bytes/record) — despite the nominal 10-slot ref array suggesting a multi-step
/// scripted sequence, real missions never use it that way (96% of real records populate at most
/// slot 0 of <see cref="SequenceRefs"/>, zero records use more than 1 slot). Functionally a pairing
/// of (at most) two row #10 (<see cref="Action82"/>) action records plus a small timer-shaped
/// parameter. See docs/formats/msn-mission-file.md, "Row #11 field decode".
/// </summary>
public class ActionPair30 : MapObject {
	/// <summary>0x02 — condition ref; always -1 in all real data.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — always -1; dead, same shape as elsewhere.</summary>
	public short Unk04 { get; set; }

	/// <summary>0x06 — primary ref into row #10 (<see cref="Action82"/>); populated in 82% of real records.</summary>
	public short PrimaryActionRef { get; set; }

	/// <summary>
	/// 0x08 — small int, real values cluster on round numbers (10 dominant). Shape consistent with
	/// a delay/timer in seconds; not confirmed.
	/// </summary>
	public short TimerValue { get; set; }

	/// <summary>
	/// 0x0A-0x1D — declared 10-slot ref array into row #10. Real usage: 96% of records populate at
	/// most slot 0, 3/72 populate none; zero real records use more than 1 slot.
	/// </summary>
	public short[] SequenceRefs { get; set; } = new short[10];
}
