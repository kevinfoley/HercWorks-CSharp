using HercWorks.Core.Data.Struct.Vshell.Hercs;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/ARM_WEAP.DAT — configures image render position offset in
/// /UI/ARMING/Weapon_panel. See Java source for full byte layout.
/// Ported from org.hercworks.core.data.file.dat.shell.ArmWeap.
/// </summary>
public class ArmWeap {
	public short TotalWeapons { get; set; }
	public short TotalSecondList { get; set; }

	public UiHardpointGraphic[]? Entries { get; set; }
	public UiHardpointGraphic[]? Secondary { get; set; }

	public ArmWeap() { }

	public ArmWeap(short totalWeapons) {
		TotalWeapons = totalWeapons;
		Entries = new UiHardpointGraphic[totalWeapons];
	}
}
