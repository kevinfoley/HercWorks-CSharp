namespace HercWorks.Core.Data.Struct.Vshell.Hercs;

/// <summary>
/// Utility class, abstracted out from HercInf — a separate class makes working with this data
/// easier. Ported from org.hercworks.core.data.struct.vshell.hercs.HercInfEntry.
/// </summary>
public class HercInfEntry {
	public short HercId { get; set; }
	public short Weight { get; set; }
	public short Speed { get; set; }
	public short HardpointTotal { get; set; }
	public short SalvageReq { get; set; }
	public short UnknownFlag { get; set; }
	public short BuildMissionCount { get; set; }
	public short FlagCampaignStart { get; set; }
}
