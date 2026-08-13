namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #2 (82 bytes/record) — a one-shot campaign-override/patch application. VSHELL reads each
/// record into a reused scratch buffer and applies its effects immediately rather than storing a
/// persistent array, so there is no confirmed field-level meaning to decode. Modeled here purely
/// as opaque, round-trippable data — see docs/formats/msn-mission-file.md's row #2 entry.
/// </summary>
public class CampaignOverridePatch82 {
	/// <summary>Raw 41-short (82-byte) record, preserved verbatim for round-trip fidelity.</summary>
	public short[] Data { get; set; } = new short[41];
}
