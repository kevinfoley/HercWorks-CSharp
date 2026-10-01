namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #7 (10 bytes/record) — a heading, <c>script.dat</c> block 2. See
/// docs/formats/msn-mission-file.md, "Row #7 field decode".
/// </summary>
public class Heading10 : MapObject {
	/// <summary>0x02 — condition ref.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — variant key; a variant copies the heading. Unused in retail.</summary>
	public short VariantKey { get; set; }

	/// <summary>0x06 — always -1 in retail; the load does not read it.</summary>
	public short Unk06 { get; set; }

	/// <summary>0x08 — the heading in degrees; DBSIM multiplies it by 182 to reach BAM.</summary>
	public short Degrees { get; set; }
}
