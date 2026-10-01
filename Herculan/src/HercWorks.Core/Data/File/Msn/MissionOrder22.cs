namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #15 (22 bytes/record) — one order a mission group works through, <c>script.dat</c> block 10:
/// a verb, the route it walks, what it is about, and optionally an action that ends it. Row #16 names
/// up to ten and runs them in slot order. See docs/formats/msn-mission-file.md, "Row #15 field
/// decode", and docs/simulation/ai-goals.md.
/// </summary>
public class MissionOrder22 : MapObject {
	/// <summary>0x02 — condition ref.</summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — variant key: unless <c>-1</c>, 0x08-0x15 are copied from a randomly picked variant
	/// (docs/formats/msn-mission-file.md#variants). Unused in retail.
	/// </summary>
	public short VariantKey { get; set; }

	/// <summary>0x06 — compound-condition partner: 1 or -99, set only alongside <see cref="ConditionRef"/>.</summary>
	public short CompoundConditionPartner { get; set; }

	/// <summary>0x08 — the verb, 0-6: search/destroy, ram, guard, patrol, sleep, travel, follow.</summary>
	public short Verb { get; set; }

	/// <summary>0x0A — the formation VSHELL's briefing map stands the squad in; DBSIM copies it and never reads it.</summary>
	public short FormationId { get; set; }

	/// <summary>0x0C — ref into row #6 (<see cref="MapPoint22"/>); DBSIM resolves it and never reads it.</summary>
	public short PointRef { get; set; }

	/// <summary>0x0E — ref into row #8 (<see cref="WaypointGroup"/>): the route. Only a group's first order's is used.</summary>
	public short RouteRef { get; set; }

	/// <summary>0x10 — what <see cref="SubjectRef"/> names: -1 nothing, 0 a group (row #16), 1 a mech (#12), 2 a flyer (#13), 3 a base (#14).</summary>
	public short SubjectKind { get; set; }

	/// <summary>0x12 — the order's subject, per <see cref="SubjectKind"/>: what to hunt, guard or follow.</summary>
	public short SubjectRef { get; set; }

	/// <summary>0x14 — ref into row #10 (<see cref="MissionAction82"/>): when it fires, the group moves to its next order.</summary>
	public short ActionRef { get; set; }
}
