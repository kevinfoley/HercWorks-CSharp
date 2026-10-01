using HercWorks.Core.Data.Struct.Dbsim;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /SIMVOL0/DAT/BULLETS.DAT, /SIMVOL0/DAT/ROCKETS.DAT — a <c>UINT16</c> count, then 14-byte
/// <see cref="ProjMissileDatEntry"/> records indexed by a <see cref="ProjectileData"/> record's
/// MissileId. The two files share the stride but not the field meanings past the first two; see
/// docs/simulation/projectiles.md and docs/simulation/rockets.md.
/// </summary>
public class MissileDatFile {
	public short Total { get; set; }
	public ProjMissileDatEntry[]? Entries { get; set; }

	public MissileDatFile() { }

	public MissileDatFile(short total) {
		Total = total;
		Entries = new ProjMissileDatEntry[total];
	}
}
