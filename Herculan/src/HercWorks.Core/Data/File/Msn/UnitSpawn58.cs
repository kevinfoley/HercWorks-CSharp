namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #17 (58 bytes/record) — structurally unusual: no leading GUID field at all (this record is
/// never referenced by anything else in the file, so it has no need for the standard
/// insert-or-merge-by-GUID machinery — the load loop has no dedup step either, surviving records
/// are just unconditionally appended). Best read as "attach a specific herc/unit type"
/// (<see cref="LutRef"/>, the dominant real field) "to this waypoint group"
/// (<see cref="RefRow8"/>, secondary), with an optional polymorphic entity tag and rarely a
/// trigger condition — plausibly a unit-spawn or unit-assignment record tied to a patrol route.
///
/// Unlike row #8's nested array (which is genuinely variable-length on disk — only
/// count x 2 bytes are present), this record's whole 58 bytes are always present, including the
/// tail: <see cref="Pairs"/> is a fixed 10-slot on-disk array (unused slots padded -1), prefixed by
/// its own real pair count. See docs/formats/msn-mission-file.md, "Row #17 field decode" and
/// "The tail (0x10-0x39) — resolved".
/// </summary>
public class UnitSpawn58 {
	/// <summary>0x00 — condition ref; rare, 98% are -1, only 2 real records use it.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x02 — not read anywhere in this record's load loop; real binary distribution 1 (72%) / 0 (28%).</summary>
	public short Unk02 { get; set; }

	/// <summary>0x04 — not read in this loop; real distribution across 7 values (0-7), skewed toward 1 (53%).</summary>
	public short Unk04 { get; set; }

	/// <summary>
	/// 0x06 — discriminator selecting the target row type for <see cref="DiscriminatedRef"/>: 0 =
	/// row #16, 1 = row #12, 3 = row #14. Code 2 is a valid switch arm but never occurs in retail data.
	/// </summary>
	public short Discriminator { get; set; }

	/// <summary>0x08 — discriminated ref, per <see cref="Discriminator"/>; the standard 4-way polymorphic-entity motif.</summary>
	public short DiscriminatedRef { get; set; }

	/// <summary>0x0A — ref into row #6 (<see cref="MapPoint22"/>); declared in the load code but never populated in any real record.</summary>
	public short RefRow6 { get; set; }

	/// <summary>0x0C — ref into row #8 (<see cref="WaypointGroup"/>); populated in 23% of real records.</summary>
	public short RefRow8 { get; set; }

	/// <summary>0x0E — ref into the shared herc/unit-type LUT; the record's dominant field, 93% real.</summary>
	public short LutRef { get; set; }

	/// <summary>
	/// 0x10 — count of populated entries in <see cref="Pairs"/> (0-2 in all real data). Not part of
	/// a generic dead span — a genuine pair-count discriminator for the fixed 10-slot tail array.
	/// </summary>
	public short PairCount { get; set; }

	/// <summary>
	/// 0x12-0x39 — fixed 10-slot array of (ref, tag) pairs; only the first <see cref="PairCount"/>
	/// slots are ever populated in real data (max observed: 2), the rest are -1/-1. The first
	/// element of each pair ranges 20-360 (plausibly still LUT/GUID-shaped, not confirmed by any
	/// resolver call); the second is only ever 6 or 7.
	/// </summary>
	public UnitSpawn58Pair[] Pairs { get; set; } = new UnitSpawn58Pair[10];
}

/// <summary>One (ref, tag) entry of <see cref="UnitSpawn58.Pairs"/> — 4 bytes on disk.</summary>
public class UnitSpawn58Pair {
	public short Ref { get; set; } = -1;
	public short Tag { get; set; } = -1;
}
