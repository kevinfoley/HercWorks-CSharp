namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #7 (10 bytes/record) — the simplest record type in the file: a minimal `{GUID, payload}`
/// entry. Nothing else in the file references it. Real payload is a narrow 3-valued discrete flag
/// (0/1/10) — plausibly a per-entity toggle or difficulty-tier marker, not confirmed further.
/// See docs/formats/msn-mission-file.md, "Row #7 field decode".
/// </summary>
public class Heading10 : MapObject {
	/// <summary>0x02 — condition ref; always -1 in all real data.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — parent/inherit index; always -1 in all real data (unlike row #8's version of this field).</summary>
	public short InheritIndex { get; set; }

	/// <summary>0x06 — always -1; same dead-field shape as elsewhere in this file.</summary>
	public short Unk06 { get; set; }

	/// <summary>0x08 — small discrete payload; real values 0 (62%), 1 (34%), or 10 (4%).</summary>
	public short Payload { get; set; }
}
