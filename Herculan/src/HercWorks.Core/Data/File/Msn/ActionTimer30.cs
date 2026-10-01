namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #11 (30 bytes/record) — a mission timer, <c>script.dat</c> block 6: an action that arms it, a
/// delay, and up to ten actions fired when the delay runs out. See docs/formats/msn-mission-file.md,
/// "Row #11 field decode", and docs/simulation/mission-deployment.md.
/// </summary>
public class ActionTimer30 : MapObject {
	/// <summary>0x02 — condition ref.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — always -1 in retail; the load does not read it.</summary>
	public short Unk04 { get; set; }

	/// <summary>0x06 — ref into row #10 (<see cref="MissionAction82"/>): the action that arms the timer. Unset means it runs from mission start.</summary>
	public short PrimaryActionRef { get; set; }

	/// <summary>0x08 — the delay in seconds.</summary>
	public short Delay { get; set; }

	/// <summary>0x0A-0x1D — refs into row #10: the actions fired when the delay runs out. DBSIM fires all ten; retail fills at most the first.</summary>
	public short[] SequenceRefs { get; set; } = new short[10];
}
