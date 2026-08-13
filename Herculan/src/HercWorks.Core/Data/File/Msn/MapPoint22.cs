namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #6 (22 bytes/record) — the file's central spatial-reference table: a flat `{GUID, X, Y, Z}`
/// world-position record. Every other spatially-aware row (#8's nested entries, #9's link/reward
/// refs, #10's LUT ref, #15, #16, #17) points into this row by GUID. Confirmed int32, not float32
/// (reinterpreting as IEEE-754 gives uniformly near-zero denormalized garbage across all 2,661 real
/// instances). See docs/formats/msn-mission-file.md, "Row #6 field decode".
/// </summary>
public class MapPoint22 : MapObject {
	/// <summary>0x02 — condition ref; always -1 in all real data.</summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — template/inherit index. The "copy X/Y/Z from record N" mechanism is real, working
	/// code, but always -1 (never triggered) in every shipped mission.
	/// </summary>
	public short InheritIndex { get; set; }

	/// <summary>0x06 — always -1; not read or written anywhere in this record's load loop.</summary>
	public short Unk06 { get; set; }

	/// <summary>
	/// 0x08 — "sum" flag. The vector-addition-from-two-refs mechanism is real, working code, but
	/// always 0 (never triggered) in every shipped mission.
	/// </summary>
	public short SumFlag { get; set; }

	/// <summary>0x0A — world X; real range 77,591 to 3,825,420.</summary>
	public int X { get; set; }

	/// <summary>0x0E — world Y; real range 17,968 to 3,800,672.</summary>
	public int Y { get; set; }

	/// <summary>0x12 — world Z/altitude; real range 0 to 35,400, two orders of magnitude smaller than X/Y.</summary>
	public int Z { get; set; }
}
