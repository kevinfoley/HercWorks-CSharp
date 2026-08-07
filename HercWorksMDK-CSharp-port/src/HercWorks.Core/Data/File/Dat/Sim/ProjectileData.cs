using HercWorks.Core.Data.Struct;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/PROJ.DAT — entries are in Weapon ID order.
///   0 - UINT16 - total weapons
///   SEQ0 (36 bytes/segment): unknown, ID (BULLETS or ROCKETS), DMG/SHIELD, DMG/ARMOR, ?, SPEED
///   (fixed point, 5000 -> 500.0), IMPACT/SHIELD[0-3], IMPACT/GROUND[0-3], IMPACT/ARMOR[0-3].
/// Ported from org.hercworks.core.data.file.dat.sim.ProjectileData.
/// </summary>
public class ProjectileData : DataFile {
	public short Total { get; set; }
	public Projectile[]? Data { get; set; }

	public Projectile NewProjectile() => new();

	public class Projectile {
		/// <summary>TODO (carried over from Java): possible projectile type bitflag — 0x04 == beams, 0x02 == bullets?</summary>
		public ProjectileType? Type { get; set; }

		public short MissileId { get; set; }
		public short DamageShield { get; set; }
		public short DamageArmor { get; set; }
		public short Unk2_val { get; set; }
		public short Speed { get; set; }
		public short[] ImpactFXShield { get; set; } = new short[4];
		public short[] ImpactFXArmor { get; set; } = new short[4];
		public short[] ImpactFXGround { get; set; } = new short[4];
	}
}
