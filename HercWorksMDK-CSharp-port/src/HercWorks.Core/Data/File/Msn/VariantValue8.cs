namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #3 (8 bytes/record) — a condition-gated campaign-variant value lookup. Several real records
/// share the same GUID but different conditions and different payload values; the standard
/// GUID-based compaction step at load time keeps only whichever variant's condition actually
/// passes (plus any unconditioned instance, presumably a default/fallback). Referenced by row #4's
/// variant ref, which fetches this record's own <see cref="Payload"/> value, not just its index.
/// See docs/formats/msn-mission-file.md, "Row #3 field decode".
/// </summary>
public class VariantValue8 : MapObject {
	/// <summary>0x02 — condition ref; real usage 44%, the second-highest in the file after row #1.</summary>
	public short ConditionRef { get; set; }

	/// <summary>
	/// 0x04 — never read anywhere in this record's own load loop. Exactly two real values, `-1` or
	/// `-99`; the `-99` set is identical to the set of records with a real <see cref="ConditionRef"/>
	/// — reads as an authoring-tool marker rather than a runtime-consumed value.
	/// </summary>
	public short Unk04 { get; set; }

	/// <summary>0x06 — the variant's actual payload value; what row #4's variant ref fetches.</summary>
	public short Payload { get; set; }
}
