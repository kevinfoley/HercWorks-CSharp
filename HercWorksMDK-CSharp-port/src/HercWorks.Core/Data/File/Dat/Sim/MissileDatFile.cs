using HercWorks.Core.Data.Struct.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /SIMVOL0/DAT/BULLETS.DAT, /SIMVOL0/DAT/ROCKETS.DAT — shared struct.
///   0 - UINT16 - total count
///   SEQ0 (14-byte segments): model ID, projectile timer (ms), projectile speed, unknown flag
///   (EMP proj IDs have 256, all else 0), SFX/BULLET/FIRE, ?, SFX/ROCKET/FIRE.
/// Ported from org.hercworks.core.data.file.dat.sim.MissileDatFile.
/// </summary>
public class MissileDatFile : DataFile {
	public short Total { get; set; }
	public ProjMissileDatEntry[]? Entries { get; set; }

	public MissileDatFile() { }

	public MissileDatFile(short total) {
		Total = total;
		Entries = new ProjMissileDatEntry[total];
	}
}
