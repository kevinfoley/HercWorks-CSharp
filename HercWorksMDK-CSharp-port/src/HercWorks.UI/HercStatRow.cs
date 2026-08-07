using HercWorks.Core.Data.Struct.Herc;

namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape for a single HERC_INF.DAT entry (HercInfEntry). Kept separate from the
/// Core data class so the grid can bind directly without touching the raw byte-transform model.
/// </summary>
public class HercStatRow {
	public short HercId { get; set; }
	public short Weight { get; set; }
	public short Speed { get; set; }
	public short HardpointTotal { get; set; }
	public short SalvageReq { get; set; }
	public short UnknownFlag { get; set; }
	public short BuildMissionCount { get; set; }
	public short FlagCampaignStart { get; set; }

	/// <summary>Read-only convenience label resolved from HercLUT — not part of the file format itself.</summary>
	public string HercName => HercLUT.GetById(HercId)?.Name ?? "(unknown)";
}
