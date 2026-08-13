using HercWorks.Core.Data.Struct.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /SIMVOL0/DAT/BULLETS.DAT, /SIMVOL0/DAT/ROCKETS.DAT — shared struct.
///   0 - UINT16 - total count
///   SEQ0 (14-byte segments): model ID, projectile timer (ms), projectile speed, unknown flag
///   (EMP proj IDs have 256, all else 0), SFX/BULLET/FIRE, ?, SFX/ROCKET/FIRE.
///
/// Cross-referenced against real retail data (2026-08-08): real BULLETS.DAT has 12 entries with
/// <see cref="ProjMissileDatEntry.ModelId"/> values 0-8 (all 9 values used, several repeated
/// across entries), and real ROCKETS.DAT has 5 entries with ModelId values 0-1 — both ranges
/// match exactly the real root-mesh counts of simvol0/dts/BULLETS.DTS (9 roots) and
/// simvol0/dts/ROCKETS.DTS (2 roots) respectively, strongly suggesting ModelId indexes directly
/// into the sibling .DTS file's top-level mesh list (i.e. which projectile 3D model to render).
/// Not proven beyond the range/count match — DTS top-level roots carry no name field to confirm
/// against (see the DTS LOD-investigation notes in project memory).
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
