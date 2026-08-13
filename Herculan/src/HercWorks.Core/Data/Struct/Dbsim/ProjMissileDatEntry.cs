namespace HercWorks.Core.Data.Struct.Dbsim;

/// <summary>
/// This struct seems shared by both BULLETS.DAT and ROCKETS.DAT — for some reason BULLETS.DAT
/// uses SfxFireIdBullets and ROCKETS.DAT uses SfxFireIdMissiles.
/// Ported from org.hercworks.core.data.struct.dbsim.ProjMissileDatEntry.
/// </summary>
public class ProjMissileDatEntry {
	public short ModelId { get; set; }
	public short Lifetime { get; set; }
	public short ClipRadius { get; set; }
	public short Unk2Flag { get; set; }
	public short SfxFireIdBullets { get; set; }
	public short Unk3Uint16 { get; set; }
	public short SfxFireIdMissiles { get; set; }
}
