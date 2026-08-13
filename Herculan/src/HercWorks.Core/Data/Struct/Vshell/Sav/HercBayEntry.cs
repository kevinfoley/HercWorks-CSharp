using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Core.Data.Struct.Vshell.Hercs;

namespace HercWorks.Core.Data.Struct.Vshell.Sav;

/// <summary>
/// Specifically bound to PlayerSave — a segment of save data is an array of herc bay entries.
/// Ported from org.hercworks.core.data.struct.vshell.sav.HercBayEntry.
/// </summary>
public class HercBayEntry {
	public HercLUT? Id { get; set; }
	public short NameId { get; set; }
	public Dictionary<HercExternals, ShellHercPart>? HealthExternals { get; set; }
	public Dictionary<HercInternals, ShellHercPart>? HealthInternals { get; set; }
	public ShellHercPart[] HealthHardpoints { get; set; } = new ShellHercPart[10];
	public short BuildPercent { get; set; }
	public short BuildStepNum { get; set; }
	public short HardpointMax { get; set; }
	public short ActiveSockets { get; set; }
	public Dictionary<short, ShellWeaponEntry> Weapons { get; set; } = new();
}
