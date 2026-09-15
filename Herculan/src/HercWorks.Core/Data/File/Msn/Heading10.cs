namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #7 (10 bytes/record) — the simplest record type in the file: a minimal `{GUID, payload}`
/// entry. Nothing else in the file references it. The payload is a <b>heading in degrees</b>, which
/// DBSIM multiplies by 182 to reach BAM at load; the three values seen are 0, 1 and 10.
/// See docs/formats/msn-mission-file.md, "Row #7 field decode".
/// </summary>
public class Heading10 : MapObject {
	/// <summary>0x02 — condition ref; always -1 in all real data.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — parent/inherit index; always -1 in all real data (unlike row #8's version of this field).</summary>
	public short InheritIndex { get; set; }

	/// <summary>0x06 — always -1; same dead-field shape as elsewhere in this file.</summary>
	public short Unk06 { get; set; }

	/// <summary>0x08 — heading in degrees, x182 to BAM at load; real values 0 (62%), 1 (34%), 10 (4%).</summary>
	public short Payload { get; set; }
}
