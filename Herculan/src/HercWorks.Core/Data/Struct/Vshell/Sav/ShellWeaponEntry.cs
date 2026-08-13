namespace HercWorks.Core.Data.Struct.Vshell.Sav;

/// <summary>
/// Found in .sav files — for some reason this duplicates the information in UiWeaponEntry.
/// Abstracted out to its own class, but it's unclear if both are actually needed, or whether
/// the two should eventually resolve to a single class.
/// Ported from org.hercworks.core.data.struct.vshell.sav.ShellWeaponEntry.
/// </summary>
public class ShellWeaponEntry {
	public WeaponLUT? Id { get; set; }
	public short NameId { get; set; }
	public short HealthArmor { get; set; }
	public short HealthInteral { get; set; }
	public MissileType? MissileType { get; set; }
}
