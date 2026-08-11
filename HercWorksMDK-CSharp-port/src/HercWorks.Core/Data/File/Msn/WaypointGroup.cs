namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #8 (10 fixed bytes/record + nested-count x 2 bytes) — a named, ordered list of row #6
/// (<see cref="MapPoint22"/>) world positions: functionally a patrol route/waypoint chain,
/// sometimes closed into a loop (24% of multi-entry records have their last nested entry equal
/// their first), occasionally conditional (in which case it deliberately has GUID -1, since a
/// record that might not exist in a given playthrough can't safely be referenced by GUID anyway),
/// or aliased from an existing group via inheritance (only the nested list itself is copied, not
/// the rest of the header).
///
/// On disk each nested entry is only 2 bytes (a ref into row #6's GUID space) — NOT the 6-byte
/// in-memory slot VSHELL allocates for it (the other 4 bytes are zero-initialized locally, never
/// read from the file). See docs/formats/msn-mission-file.md, "Row #8 field decode" and its
/// on-disk-width correction note in the main record-array table.
/// </summary>
public class WaypointGroup : MapObject {
	/// <summary>
	/// 0x02 — condition ref; real usage 47/470 (10%), the highest of any row decoded. The 47 real
	/// instances are exactly the same 47 records that have GUID -1 (see class doc comment).
	/// </summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — parent/inherit index, nested-list only: copies the referenced parent's resolved
	/// nested waypoint list wholesale (not any other header field). Real usage 11/470 (2%); every
	/// one of those 11 has on-disk nested count 0 (the list only exists at runtime, via the copy).
	/// </summary>
	public short InheritIndex { get; set; }

	/// <summary>0x06 — always -1 in all real data; same dead-field shape as elsewhere in this file.</summary>
	public short Unk06 { get; set; }

	/// <summary>
	/// Nested refs into row #6's GUID space, in order. Real range 0-9 slots, mean 3.2; consecutive
	/// entries are measurably closer together (spatial coherence) than a random same-file pair.
	/// </summary>
	public short[] Waypoints { get; set; } = Array.Empty<short>();
}
