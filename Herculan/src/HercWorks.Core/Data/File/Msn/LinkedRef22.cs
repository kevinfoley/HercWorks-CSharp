namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #15 (22 bytes/record) — <b>one order a mission group works through</b>: a verb
/// (<see cref="SmallInt1"/>), the route it walks (<see cref="RefRow8"/>, a
/// <see cref="WaypointGroup"/>), what it is about (<see cref="DiscriminatorType"/> and
/// <see cref="DiscriminatedRef"/>, into rows #12/#13/#14/#16 — the 0 -> row #16 case is only
/// resolvable in a second pass, since row #16 hasn't loaded yet when row #15 itself is parsed), and
/// optionally a mission action that ends it (<see cref="RefRow10"/>). Row #16 names up to ten of
/// them and runs them in slot order.
///
/// <para>Same 22-byte size as row #6, but a structurally unrelated "typed link" shape, not a flat
/// position record. See docs/formats/msn-mission-file.md, "Row #15 field decode", and
/// Herculan/docs/simulation/ai-goals.md for what the simulation does with each field.</para>
/// </summary>
public class LinkedRef22 : MapObject {
	/// <summary>
	/// 0x02 — condition ref; 632/637 are -1, but 5 real records use a genuine trigger condition —
	/// the first row decoded where this mechanism is confirmed to actually fire in a shipped mission.
	/// </summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — parent/template index; always -1 in real data. When set, resolves a parent via a
	/// wholesale copy of the 7 remaining short fields (0x08-0x14) — the "proper" version of the
	/// inheritance idiom, unlike row #6's partial one, but never triggered by any shipped mission.
	/// </summary>
	public short InheritIndex { get; set; }

	/// <summary>
	/// 0x06 — compound-condition partner: the same 5 records with a real <see cref="ConditionRef"/>
	/// also have a real value here (1, -99, -99, -99, 1) — 100% correlated. Neither field is read by
	/// any code in this specific loop.
	/// </summary>
	public short CompoundConditionPartner { get; set; }

	/// <summary>
	/// 0x08 — <b>the order's verb</b>, 0-6: search/destroy, ram, guard, patrol, sleep, travel,
	/// follow. It picks both the behaviour state the group's machines are put in and the test that
	/// decides when the order is finished.
	/// </summary>
	public short SmallInt1 { get; set; }

	/// <summary>0x0A — small int, almost always 0; otherwise 3 or 1. Resolved into the runtime order record and never read.</summary>
	public short SmallInt2 { get; set; }

	/// <summary>0x0C — ref into row #6 (<see cref="MapPoint22"/>); sparse, 7% real. Resolved into the runtime order record and never read.</summary>
	public short RefRow6 { get; set; }

	/// <summary>
	/// 0x0E — ref into row #8 (<see cref="WaypointGroup"/>), <b>the route</b>; the record's dominant
	/// payload, 94% real. Only the group's <i>first</i> order's route is ever installed.
	/// </summary>
	public short RefRow8 { get; set; }

	/// <summary>
	/// 0x10 — what <see cref="DiscriminatedRef"/> names: -1 = nothing (always paired with -1 there),
	/// 0 = a group (row #16), 1 = a HERC (row #12), 3 = a structure (row #14). Code 2 (a flyer, row
	/// #13) is a valid switch arm but never occurs in retail data.
	/// </summary>
	public short DiscriminatorType { get; set; }

	/// <summary>0x12 — <b>the order's subject</b>, per <see cref="DiscriminatorType"/>: what to hunt, guard or follow.</summary>
	public short DiscriminatedRef { get; set; }

	/// <summary>
	/// 0x14 — ref into row #10 (<see cref="Action82"/>); sparse, 2% real. When that action fires the
	/// group moves to its next order whether or not this one finished.
	/// </summary>
	public short RefRow10 { get; set; }
}
