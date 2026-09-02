using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/PROJ.DAT — 27 records of 36 bytes, in weapon-id order, behind a
/// <c>UINT16</c> count. Each record is: unknown, <see cref="Projectile.MissileId"/> (into
/// BULLETS.DAT or ROCKETS.DAT), DamageShield, DamageArmor, ?, Speed (fixed point, 5000 -> 500.0),
/// then ImpactShield[0-3], ImpactGround[0-3], ImpactArmor[0-3].
///
/// <para><b>How a weapon reaches a record.</b> A catalog weapon's
/// <see cref="Weapons.WeaponMountTemplate.ProjDatIndex"/> is either a flat index into this table, a
/// sentinel meaning "no record" (<c>ECM</c> only), or — for <c>MSL6</c>/<c>MSL8</c>/<c>MSL10</c>/
/// <c>FLYMSL</c> — resolved through the mission's second loadout array, the ammunition type
/// (<c>MecEntry.WeaponAmmoTypes</c>, or <c>script.dat</c> block 7 offset <c>0x72</c>), which is what
/// reaches indices 7-13. Seven catalog ids (<c>NONE</c>, <c>LAEW</c>, <c>MINE</c>, <c>TARG</c>,
/// <c>SHLD</c>, <c>TURB</c>, <c>ENRG</c>) carry an all-zero placeholder template whose mount
/// constructors never consume the index 0 it reads. See docs/simulation/weapon-mounts.md.</para>
///
/// <para>Retail index table:</para>
///
/// | idx | Weapon | Type | MissileId | DmgShield | DmgArmor | Splash | Speed |
/// |---|---|---|---|---|---|---|---|
/// | 0 | ATC20 | Bullet | 0 | 60 | 360 | 0 | 5000 |
/// | 1 | ATC35 | Bullet | 1 | 120 | 480 | 0 | 5000 |
/// | 2 | ATC50 | Bullet | 2 | 180 | 600 | 0 | 5000 |
/// | 3 | L100 | Beam | 3 | 1500 | 600 | 0 | 0 |
/// | 4 | L200 | Beam | 4 | 1800 | 960 | 0 | 0 |
/// | 5 | L300 | Beam | 5 | 2000 | 1200 | 0 | 0 |
/// | 6 | EMPC | Bullet | 6 | 2000 | 400 | 0 | 2000 |
/// | 7-9 | (unclaimed) | Rocket | 0-2 | 1000 | 1000 | 500-1000 | 1000 |
/// | 10-13 | (unclaimed) | Missile | 0-3 | 400 | 1600 | 500 | 6000 |
/// | 14 | PBW | Beam | 0 | 1000 | 1000 | 0 | 0 |
/// | 15 | ELFW | Beam | 1 | 150 | 200 | 0 | 0 |
/// | 16 | BEMP | Bullet | 7 | 8000 | 2000 | 0 | 2000 |
/// | 17 | BPBW | Beam | 2 | 4000 | 4000 | 0 | 0 |
/// | 18 | BMSL | Missile | 4 | 3000 | 7200 | 500 | 6000 |
/// | 19 | EMP2 | Bullet | 8 | 2000 | 400 | 0 | 2000 |
/// | 20 | PBW2 | Beam | 6 | 1400 | 1400 | 0 | 0 |
/// | 21 | ELF2 | Beam | 7 | 200 | 300 | 0 | 0 |
/// | 22 | PLAS, MFAC | Bullet | 9 | 3000 | 3000 | 1000 | 1000 |
/// | 23 | ATC75 | Bullet | 1 | 220 | 700 | 0 | 5000 |
/// | 24 | ATC100 | Bullet | 2 | 260 | 800 | 0 | 5000 |
/// | 25 | L400 | Beam | 4 | 3000 | 1920 | 0 | 0 |
/// | 26 | L500 | Beam | 5 | 3000 | 2000 | 0 | 0 |
///
/// <para><b>Damage scaling.</b> A shot's power level — the capacitor charge it was fired at,
/// <c>min(template+0x38, mount+0x7d)</c> — is Q10-multiplied against DamageShield before shield
/// absorption, and against DamageArmor before the damage-application step;
/// <see cref="Projectile.SplashFactor"/>'s own multiplier one step further down is Q10 as well.
/// DamageShield/DamageArmor are the weapon's own base stats, not abstract multipliers. See
/// docs/simulation/weapon-firing.md and docs/simulation/damage-system.md.</para>
///
/// <para><b><see cref="Projectile.Type"/> is a firing-mechanism selector</b>, not a cosmetic tag —
/// each value builds a different class; see <see cref="ProjectileType"/>. Every <c>Beam</c> (4)
/// record has <see cref="Projectile.Speed"/> 0 and resolves its hit synchronously at fire time
/// rather than as a travelling instance. <c>Bullet</c> (2) covers both the ATC progression and the
/// EMP-shaped high-shield entries: real flight time, and <see cref="Projectile.SplashFactor"/> 0
/// throughout — except one. <c>Missile</c> (0) and <c>Rocket</c> (3) are the splash-capable guided
/// weapons.</para>
///
/// <para>Weapon names above are the shell catalog's (<see cref="WeaponLUT"/>), not DBSIM's own —
/// index 22's two claimants are catalog ids 25 (<c>PLAS</c>) and 28 (<c>MFAC</c>, which the
/// simulator's name table calls <c>MAGN</c>).</para>
///
/// <para><b>The Plasma cannon is index 22</b>, the single <c>Bullet</c> record that breaks the
/// no-splash rule (<see cref="Projectile.MissileId"/> 9, 3000/3000, SplashFactor 1000). DBSIM's
/// <c>Bullet</c> per-tick method has a <c>MissileId == 9</c> branch calling the explosion formula
/// directly instead of the single-target hit path — a bullet with real flight time that explodes
/// with splash on impact.</para>
///
/// <para>Ported from org.hercworks.core.data.file.dat.sim.ProjectileData.</para>
/// </summary>
public class ProjectileData {
	public short Total { get; set; }
	public Projectile[]? Data { get; set; }

	public Projectile NewProjectile() => new();

	public class Projectile {
		/// <summary>TODO (carried over from Java): possible projectile type bitflag — 0x04 == beams, 0x02 == bullets?</summary>
		public ProjectileType? Type { get; set; }

		public short MissileId { get; set; }
		public short DamageShield { get; set; }
		public short DamageArmor { get; set; }

		/// <summary>
		/// Was <c>Unk2_val</c> — resolved via DBSIM.EXE disassembly
		/// (<c>FUN_004188c8</c>, see docs/simulation/damage-system.md). A Q8 fraction of
		/// this hit's (already shield-absorbed) armor/structure damage that gets diverted into a
		/// secondary small-radius explosion — reusing the same blast-sweep formula explosive
		/// weapons use — instead of going straight to the struck component's health. Zero (the
		/// common case in real data) means no secondary explosion: the full armor-damage amount
		/// applies directly. Nonzero for every <see cref="ProjectileType.Rocket"/> (Type 3) entry
		/// (uniform DamageShield==DamageArmor, unlike every other type) and every
		/// <see cref="ProjectileType.Missile"/> (Type 0) entry — i.e. the two splash-capable
		/// types, consistent with this being that splash's actual damage-delivery mechanism.
		/// Zero for every real <see cref="ProjectileType.Beam"/> entry with no exceptions, and
		/// zero for all but one <see cref="ProjectileType.Bullet"/> entry — the sole exception
		/// (<see cref="Projectile.MissileId"/>==9, DamageShield==DamageArmor==3000) is the Plasma
		/// cannon, confirmed via DBSIM's Bullet-class per-tick method having a dedicated
		/// MissileId==9 explosion branch (see the class doc comment above) — so "Bullet never
		/// splashes" is a strong pattern with one identified, explained exception, not a
		/// coincidental outlier. Real per-weapon values seen: 0, 500, or 1000.
		/// </summary>
		public short SplashFactor { get; set; }

		public short Speed { get; set; }
		public short[] ImpactFXShield { get; set; } = new short[4];
		public short[] ImpactFXArmor { get; set; } = new short[4];
		public short[] ImpactFXGround { get; set; } = new short[4];
	}
}
