using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/WEAPONS.DAT — mostly-unidentified fields (total weapons: 33 stock, 6 cut),
/// ammo counts, and per-weapon projectile offset/fire-rate sequence. See Java source for the full
/// byte-offset notes. No fields modeled beyond Total in the original.
///
/// Investigated further against real retail data (2026-08-08) while cross-referencing this file
/// against every other "weapon"-named file in SIMVOL0/SHELL0 (see project memory for the full
/// writeup): real WEAPONS.DAT has <c>Total = 33</c>, matching SHELL0/GAM/WEAPONS.DAT's catalog
/// count exactly — none of the "6 cut/deleted" weapons the Java doc comment mentions are actually
/// present in retail data, so this file never has more than 33 records despite the byte-offset
/// notes hinting at a possibly-larger layout. The per-weapon header fields and the SEQ
/// offset/fire-rate structure past Total remain NOT confidently decoded — candidate values were
/// found in plausible ranges (small integers that could be projectile-table indices, in the same
/// range as PROJ.DAT's 27 entries) but couldn't be anchored to a confirmed record boundary/size in
/// the time available, and the Java source's own doc comment already flags several of these fields
/// as crashing the engine if changed — not a file worth guessing at further without stronger
/// evidence, given that risk. <see cref="Data.Struct.WeaponLUT"/> id 16 was corrected from the
/// guessed "MSLR" to the real catalog name "FLYMSL" during this pass.
/// Ported from org.hercworks.core.data.file.dat.sim.Weapons.
/// </summary>
public class Weapons : DataFile {
	public short Total { get; set; }

	public Weapons() { }

	public Weapons(short total) {
		Total = total;
	}
}
