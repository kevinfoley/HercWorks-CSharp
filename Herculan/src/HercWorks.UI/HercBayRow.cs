using HercWorks.Core.Data.Struct.Herc;

namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape for one PlayerSave.HercBay entry (keyed by bay id). Flat fields
/// (BayId, Herc, BuildPercent, BuildStepNum, HardpointMax) are edited directly in the grid;
/// ActiveSockets is shown read-only (recalculated from the equipped-weapons count on save, same
/// pattern as CampaignResourcesForm's WorkshopSpace recalculation) since it must stay consistent
/// with whatever the detail editor's Weapons grid ends up holding. Per-part health
/// (externals/internals/hardpoints) and equipped weapons are too deeply nested for a flat grid row
/// — those are edited via HercBayEditorForm, opened per-row.
/// </summary>
public class HercBayRow {
	public short BayId { get; set; }
	public HercLUT? Herc { get; set; }
	public short BuildPercent { get; set; }
	public short BuildStepNum { get; set; }
	public short HardpointMax { get; set; }
	public int ActiveSocketCount { get; set; }

	/// <summary>The live HercBayEntry this row edits in place via HercBayEditorForm.</summary>
	public required Core.Data.Struct.Vshell.Sav.HercBayEntry Entry { get; init; }
}
