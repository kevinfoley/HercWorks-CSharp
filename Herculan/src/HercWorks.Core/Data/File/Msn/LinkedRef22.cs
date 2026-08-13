namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #15 (22 bytes/record) — a "linked reference" record whose real payload is overwhelmingly a
/// pointer into row #8 (<see cref="WaypointGroup"/>, 94% of records), optionally annotated with a
/// world position (row #6, 7%), an action (row #10, 2%), and/or a polymorphic entity ref chosen by
/// <see cref="DiscriminatorType"/> (rows #12/#13/#14/#16, ~6% combined — the 0 -> row #16 case is
/// only resolvable in a second pass, since row #16 hasn't loaded yet when row #15 itself is
/// parsed). Reads most naturally as "attach this patrol route/waypoint group to (optionally) a
/// position, an action, and/or a specific entity" — plausibly a patrol-assignment or
/// escort/guard-route record. Same 22-byte size as row #6, but a structurally unrelated "typed
/// link" shape, not a flat position record. See docs/formats/msn-mission-file.md, "Row #15 field
/// decode".
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

	/// <summary>0x08 — small int, real range 0-6. Weak, inconclusive correlation with whether <see cref="RefRow6"/> is populated.</summary>
	public short SmallInt1 { get; set; }

	/// <summary>0x0A — small int, almost always 0; otherwise 3 or 1. No confirmed meaning.</summary>
	public short SmallInt2 { get; set; }

	/// <summary>0x0C — ref into row #6 (<see cref="MapPoint22"/>); sparse, 7% real.</summary>
	public short RefRow6 { get; set; }

	/// <summary>0x0E — ref into row #8 (<see cref="WaypointGroup"/>); the record's dominant payload, 94% real.</summary>
	public short RefRow8 { get; set; }

	/// <summary>
	/// 0x10 — discriminator selecting the target row type for <see cref="DiscriminatedRef"/>: -1 =
	/// no ref (always paired with -1 there), 0 = row #16, 1 = row #12, 3 = row #14. Code 2 is a
	/// valid switch arm but never occurs in retail data.
	/// </summary>
	public short DiscriminatorType { get; set; }

	/// <summary>0x12 — discriminated ref, per <see cref="DiscriminatorType"/>; see field doc above.</summary>
	public short DiscriminatedRef { get; set; }

	/// <summary>0x14 — ref into row #10 (<see cref="Action82"/>); sparse, 2% real.</summary>
	public short RefRow10 { get; set; }
}
