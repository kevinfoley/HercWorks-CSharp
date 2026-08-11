using HercWorks.Core.Data.Struct;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/PROJ.DAT — entries are in Weapon ID order.
///   0 - UINT16 - total weapons
///   SEQ0 (36 bytes/segment): unknown, ID (BULLETS or ROCKETS), DMG/SHIELD, DMG/ARMOR, ?, SPEED
///   (fixed point, 5000 -> 500.0), IMPACT/SHIELD[0-3], IMPACT/GROUND[0-3], IMPACT/ARMOR[0-3].
///
/// Cross-referenced against real retail data (2026-08-08): real PROJ.DAT has exactly 27 entries.
/// An initial pass guessed this was simply SHELL0/GAM/WEAPONS.DAT's own catalog order with 5
/// non-projectile ids skipped (32 - 5 = 27) — that count matched by coincidence, but the ordering
/// guess was wrong; see the per-index mapping below, SOLVED by disassembly rather than pattern
/// matching.
///
/// **The real index-to-weapon mapping — SOLVED 2026-08-11 by tracing DBSIM's mech-loadout
/// weapon-mount factory (0x0040fff8, called from <c>Mech_ConfigureLoadout</c>).** Each real
/// catalog weapon's own <c>Sim.Weapons.WeaponMountTemplate.ProjDatIndex</c> field (see that class'
/// doc comment for the full field semantics) is either a direct flat array index into this table,
/// a sentinel meaning "no PROJ.DAT record" (only <c>ECM</c>), or — for <c>MSL6</c>/<c>MSL8</c>/
/// <c>MSL10</c>/<c>FLYMSL</c> only — resolved through a secondary per-hardpoint key this project
/// doesn't have visibility into. Confirmed byte-exact via a throwaway console probe cross-joining
/// the real retail <c>WEAPONS.DAT</c> (sim), <c>WEAPONS.DAT</c> (SHELL0 catalog, for real names),
/// and this file: 21 of 32 real catalog weapons resolve to a distinct PROJ.DAT index directly (2
/// more, <c>PLAS</c> and <c>MFAC</c>, share one index), 6 catalog ids (<c>NONE</c>, <c>LAEW</c>,
/// <c>MINE</c>, <c>TARG</c>, <c>SHLD</c>, <c>TURB</c>, <c>ENRG</c>) carry an all-zero placeholder
/// template whose mount constructors never actually consume the (coincidentally "valid") index 0
/// it reads, and the remaining 7 PROJ.DAT entries (indices 7-13 — exactly the 3 <c>Rocket</c> +
/// the other 4 <c>Missile</c> entries not already claimed by <c>BMSL</c>) are reached only through
/// <c>MSL6</c>/<c>MSL8</c>/<c>MSL10</c>/<c>FLYMSL</c>'s secondary-key path. Full index table:
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
/// This also retroactively confirms several manual-fiction matches by real weapon name rather than
/// just shape: EMPC and BEMP (both shield≫armor) really are the EMP cannons ("disrupts the shield
/// matrix"); PLAS really is the Plasma cannon, matching the MissileId==9 splash-Bullet mechanism
/// already independently identified two sessions earlier; ELFW really is Electron Flux, matching
/// the "unusually low-damage Beam entry" flagged as a plausible ELF candidate a session earlier.
///
/// <see cref="Projectile.MissileId"/> combined with
/// <see cref="Projectile.Type"/> indexes into MissileDatFile (BULLETS.DAT for Bullet/Beam types,
/// ROCKETS.DAT for Rocket/Missile types per that file's own doc comment) — observed MissileId
/// values stay within each target file's real entry count (0-11 for BULLETS.DAT's 12 entries,
/// 0-4 for ROCKETS.DAT's 5), which is consistent with (but not independent proof of) that link.
///
/// Independently confirmed against DBSIM.EXE disassembly (2026-08-09, see
/// docs/simulation/dbsim-physics-notes.md): DBSIM keys this same table by (category, subtypeId)
/// via a linear search (its own copy, loaded at runtime from a resource opened by the literal
/// name "proj" — matches this file's own name), 36 bytes/record, and reads exactly this record's
/// <see cref="Projectile.DamageShield"/>/<see cref="Projectile.DamageArmor"/> at the same byte
/// offsets this parser already used, independently of this Java-ported doc comment. A shot's raw
/// power level (Q8, effectively the shot's charge fraction) is multiplied against DamageShield
/// before it reaches shield absorption, and against DamageArmor before it reaches the
/// direct-fire/explosion damage-application step — i.e. these fields are genuinely the weapon's
/// own base damage-vs-shields and damage-vs-armor stats, not abstract multipliers. Real values
/// line up with the manual's weapon-effectiveness fiction: entries with DamageShield &gt;&gt;
/// DamageArmor (e.g. 2000/400, 8000/2000) match "EMP disrupts the shield matrix"; entries with
/// DamageArmor &gt;&gt; DamageShield (e.g. 400/1600) match ordinary Autocannon-style projectiles;
/// several DamageShield&gt;DamageArmor entries with speed=0 (no travel time) match beam weapons.
///
/// Follow-up (2026-08-09, same session): traced every caller of DBSIM's PROJ.DAT lookup and
/// confirmed <see cref="Projectile.Type"/>'s 4 values are a firing-mechanism selector, not a
/// cosmetic tag — see <see cref="ProjectileType"/>'s own doc comment for the full mechanical
/// breakdown (each value builds via a genuinely different C++ class). Concretely: every real
/// <c>Beam</c>-typed (4) record has <see cref="Projectile.Speed"/>==0, with no exceptions, and
/// resolves its hit synchronously at the moment of firing rather than via a travelling instance —
/// this is DBSIM's actual beam/hitscan mechanism (previously suspected not to exist as a distinct
/// code path; it turned out to just be the already-known "bullet" function, misclassified because
/// an unrelated shot-record flag happened to share the word "category"). <c>Bullet</c> (2) covers
/// both the ATC20/35/50-shaped progression and the EMP-shaped high-shield entries — both
/// single-target, <see cref="Projectile.SplashFactor"/>==0 throughout, but with genuine
/// flight-time physics (a real travelling object, just non-guided, non-splash) unlike Beam.
/// <c>Missile</c> (0, 5 entries) and <c>Rocket</c> (3, 3 entries) are the game's ordinary,
/// wholesale-splash-capable Missile weapons (<c>Rocket</c>'s constructor is confirmed to reuse
/// this project's guided/homing lead-prediction physics) — not, as an earlier pass through this
/// investigation guessed, a disguised Plasma cannon; see the next paragraph for where Plasma
/// actually is.
///
/// Second follow-up (2026-08-09, same session): **the Plasma cannon is a specific record, found
/// concretely rather than guessed from shape** — the one <c>Bullet</c>-typed (2) record that
/// breaks the "single-target, SplashFactor==0" pattern (<see cref="Projectile.MissileId"/>==9,
/// DamageShield==DamageArmor==3000, SplashFactor==1000). DBSIM's <c>Bullet</c>-class per-tick
/// method (found via its vtable) has a special <c>MissileId==9</c> branch that calls the
/// explosion formula directly instead of the ordinary single-target hit path — already suspected,
/// two sessions ago, as "very plausibly the Plasma cannon" from taxonomy shape alone, and now tied
/// to this exact record: mechanically a <c>Bullet</c> (real flight time, unlike true <c>Beam</c>s)
/// that explodes with splash on impact (unlike every other <c>Bullet</c>) — exactly "an energy
/// weapon that fires a slow-moving projectile, does splash."
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

		/// <summary>
		/// Was <c>Unk2_val</c> — resolved 2026-08-09 via DBSIM.EXE disassembly
		/// (<c>FUN_004188c8</c>, see docs/simulation/dbsim-physics-notes.md). A Q8 fraction of
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
