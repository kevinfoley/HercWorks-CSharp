using HercWorks.Core.Data.Struct;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/PROJ.DAT — entries are in Weapon ID order.
///   0 - UINT16 - total weapons
///   SEQ0 (36 bytes/segment): unknown, ID (BULLETS or ROCKETS), DMG/SHIELD, DMG/ARMOR, ?, SPEED
///   (fixed point, 5000 -> 500.0), IMPACT/SHIELD[0-3], IMPACT/GROUND[0-3], IMPACT/ARMOR[0-3].
///
/// Cross-referenced against real retail data (2026-08-08): real PROJ.DAT has exactly 27 entries,
/// which lines up cleanly with "Weapon ID order" if the order is SHELL0/GAM/WEAPONS.DAT's own
/// catalog order (see WeaponsDat/WeaponLUT) with the 5 non-projectile support/utility weapon ids
/// skipped — ECM(18), TARG(29), SHLD(30), TURB(31), ENRG(32) — leaving exactly 27 of the 32
/// non-NONE catalog entries (32 - 5 = 27, confirmed by count). Per-entry damage values were
/// spot-checked against this ordering and mostly form a plausible per-weapon-tier progression
/// (e.g. the first 3 BULLET-type entries have steadily increasing shield/armor damage matching
/// ATC20/ATC35/ATC50) but not every entry lines up cleanly enough to call the full 27-entry
/// mapping confirmed — treat the *count* match (27) as solid, the *exact index-to-weapon-name*
/// mapping as a strong but unverified hypothesis. <see cref="Projectile.MissileId"/> combined with
/// <see cref="Projectile.Type"/> indexes into MissileDatFile (BULLETS.DAT for Bullet/Beam types,
/// ROCKETS.DAT for Rocket/Missile types per that file's own doc comment) — observed MissileId
/// values stay within each target file's real entry count (0-11 for BULLETS.DAT's 12 entries,
/// 0-4 for ROCKETS.DAT's 5), which is consistent with (but not independent proof of) that link.
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
