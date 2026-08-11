namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #4 (144 bytes/record) — refutes the old, Java-ported `UnitInfo` hypothesis outright: this
/// record has no GUID/identity field at all (nothing else in the file references it), and none of
/// the old model's MapCoordId/UnitId/Weapons[10]/UnkFlags[36]/HealthModAdjust fields exist in the
/// real load code. Every one of the 62 real missions has at most one instance of this row (60 have
/// exactly one; only the two earliest tutorials have none) — a mission-level singleton, not a
/// multi-entity roster.
///
/// Three sub-arrays resolve element-by-element into a shared herc/unit-type LUT — but real values
/// (45-248) fall well outside <c>HercLUT</c>'s declared 22-entry range (0-21), so they're modeled
/// here as raw shorts rather than eagerly resolved; VSHELL's `DAT_00470664` LUT is not the same
/// table this port's `HercLUT` models. Combined with a variant-value ref that fetches a
/// condition-gated bonus quantity from row #3, the best current reading is a per-mission
/// "reward/unlock package," not a spawn record. See docs/formats/msn-mission-file.md, "Row #4
/// field decode".
/// </summary>
public class RewardPackage144 {
	/// <summary>0x00 — condition ref; always -1 in all real data.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x02-0x14 — 10 slots resolved into the shared LUT; real usage 1-4 of 10.</summary>
	public short[] LutRefsA { get; set; } = new short[10];

	/// <summary>0x16-0x50 — 30 slots resolved into the shared LUT; real usage 0-4 of 30.</summary>
	public short[] LutRefsB { get; set; } = new short[30];

	/// <summary>0x52-0x8C — 30 slots resolved into the shared LUT; real usage 0-3 of 30.</summary>
	public short[] LutRefsC { get; set; } = new short[30];

	/// <summary>0x8E — ref into row #3 (<see cref="VariantValue8"/>); dominant field, 87% real.</summary>
	public short VariantRef { get; set; }
}
