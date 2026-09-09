namespace HercWorks.Core.Data.Struct.Vshell.Sav;

/// <summary>
/// Found in .sav files. This and <see cref="Hercs.UiWeaponEntry"/> are the two on-disk forms of one
/// ten-byte in-memory record, so both are needed: the <c>gam\*.dat</c> catalogs store six bytes
/// (id, condition, missile type) and the save stores all five shorts. The two fields the short form
/// omits are filled at load — the id-derived index below, and a value the constructor sets to 100
/// and no traced path reads back from a file.
///
/// <para>Field order is id, that derived index, then the two health shorts, then the missile type.
/// The derived index is a position in a thirty-entry table covering every weapon id except the
/// three Bull weapons, which therefore resolve to -1.</para>
///
/// Ported from org.hercworks.core.data.struct.vshell.sav.ShellWeaponEntry.
/// See <c>docs/formats/herc-catalogs.md</c> for the record and both its file forms.
/// </summary>
public class ShellWeaponEntry {
	public WeaponLUT? Id { get; set; }
	public short NameId { get; set; }
	public short HealthArmor { get; set; }
	public short HealthInteral { get; set; }
	public MissileType? MissileType { get; set; }
}
