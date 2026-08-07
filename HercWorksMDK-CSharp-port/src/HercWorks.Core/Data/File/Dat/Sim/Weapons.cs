using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/WEAPONS.DAT — mostly-unidentified fields (total weapons: 33 stock, 6 cut),
/// ammo counts, and per-weapon projectile offset/fire-rate sequence. See Java source for the full
/// byte-offset notes. No fields modeled beyond Total in the original.
/// Ported from org.hercworks.core.data.file.dat.sim.Weapons.
/// </summary>
public class Weapons : DataFile {
	public short Total { get; set; }

	public Weapons() { }

	public Weapons(short total) {
		Total = total;
	}
}
