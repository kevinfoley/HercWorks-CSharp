namespace HercWorks.Core.Data.Struct.Vshell.Hercs;

/// <summary>
/// Common struct, observed in 2 places so far: Hercs and InitHerc (both in
/// data/file/dat/shell). Generally used in sequence:
///   S0_0 - UINT16 - Herc Id
///   S0_2 - UINT16 - health ratio?
///   S0_4 - UINT16 - build completeness, 00 = complete, > 0 = remaining missions to build.
///   S0_6 - UINT16 - hardpoint count
///     SEQ1 - hardpoint count
///       S1_0 - UINT - hardpoint ID
///       S1_2 - UINT - item ID
///       S1_4 - UINT - health percentage
///       S1_6 - UINT - missile enum, 05 = no missile type.
/// Ported from org.hercworks.core.data.struct.vshell.hercs.ShellHercData.
/// </summary>
public class ShellHercData {
	public short HercId { get; set; }
	public short HealthRatio { get; set; }
	public short BuildCompleteLevel { get; set; }
	public Dictionary<short, UiWeaponEntry>? Hardpoints { get; set; }
}
