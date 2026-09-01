using HercWorks.Core.Data.File.Dat.Sim;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// One shot in flight through the hit query — the stack-built record <c>Bullet_FireBurst</c>
/// (<c>0040bf74</c>) assembles and hands to <c>Sim_RaycastObjectList</c> (<c>00426528</c>), which
/// passes it on to every candidate object's own hit test.
///
/// <para>It is two structures nested in the original: an outer record holding the damage figures,
/// and an inner one at its first field holding the ray. The inner record is <b>written back into</b>
/// during the query — the raycast shortens <see cref="Distance"/> as objects are struck, and caches
/// the world-to-muzzle transform in it for the hit tests to use — which is why this is a class
/// rather than a readonly struct.</para>
///
/// <para><b>Two things build one of these.</b> A beam is the whole of its own lifetime:
/// <c>ProjectileType.Beam</c> records all carry <c>Speed == 0</c>, the ray is the weapon's full
/// range, and the hit is resolved synchronously at the instant of firing. A travelling
/// <see cref="Projectile"/> builds a fresh one every tick instead, with the ray no longer than the
/// step it is about to take, so that its flight is a chain of short sweeps rather than a moving
/// point — same record, same query, same per-object hit test.</para>
/// </summary>
public sealed class WeaponShot {
	/// <summary>
	/// The slack the range check allows past the weapon's own range — <c>Bullet_FireBurst</c>'s
	/// literal 200 at the ray record's <c>+8</c>, which <c>Mech_DirectFireHitTest</c> adds to the
	/// range and the target's radius before it rejects a candidate outright.
	/// </summary>
	public const int MuzzleClearance = 200;

	/// <summary>
	/// The ray record's <c>+0x08</c> for this shot. A beam gets the literal above; a travelling
	/// <see cref="Projectile"/> gets its <c>BULLETS.DAT</c> record's <c>ClipRadius</c> instead, which
	/// is what makes the big EMP round (200) forgiving where an autocannon round (100) is not.
	///
	/// <para>The ground walk is handed it as a radius and never reads it, so it only ever widens the
	/// object test.</para>
	/// </summary>
	public int Clearance { get; }

	/// <summary>
	/// How far a hit has to be for the query to keep looking past it — <c>Sim_RaycastObjectList</c>'s
	/// own <c>while (499 &lt; hit)</c>. A hit inside that distance ends the sweep; anything further
	/// out only shortens the ray, so a nearer object found later still wins.
	/// </summary>
	public const int MinimumScanDistance = 500;

	/// <param name="muzzle">Where the shot starts and which way it points — see <see cref="Muzzle"/>.</param>
	/// <param name="range">The firing weapon's range — see <see cref="Range"/>.</param>
	/// <param name="projectile">The <c>PROJ.DAT</c> record, for its two damage figures and its splash fraction.</param>
	/// <param name="power">The charge this shot was fired at — see <see cref="WeaponMount.ShotPower"/>.</param>
	/// <param name="owner">The machine that fired, which the query skips.</param>
	/// <param name="clearance">The slack the range check allows — see <see cref="Clearance"/>.</param>
	public WeaponShot(in Transform3 muzzle, int range, ProjectileData.Projectile projectile,
			short power, SimObject? owner, int clearance = MuzzleClearance) {
		Muzzle = muzzle;
		MuzzleInverse = muzzle.Inverted();
		Range = range;
		Distance = range;
		Clearance = clearance;

		// Both damage figures are scaled by the shot's power before anything downstream sees them, so
		// a weapon fired at half charge does half damage on both counts. Q10, against the 1200 the
		// capacitor is scaled to, so a mount holding more than 1024 makes a shot worth slightly more
		// than the record's face value.
		//
		// Zero means "not fired from a capacitor at all" and leaves the record's own figures alone —
		// Bullet_TickUpdate's own `if (power != 0)`, which is what makes an autocannon round, spent
		// out of a magazine rather than a charge, do the damage the record states. The beam dispatch
		// spells the same line without the test, but its power is min(cost, charge) on a mount that
		// only fires above its threshold, so it can never reach here with zero.
		DamageArmor = power == 0
			? projectile.DamageArmor
			: (short)SimMath.Q10Multiply(power, projectile.DamageArmor);
		DamageShield = power == 0
			? projectile.DamageShield
			: (short)SimMath.Q10Multiply(power, projectile.DamageShield);
		SplashFactor = projectile.SplashFactor;
		MissileId = projectile.MissileId;
		Effects = projectile;
		Owner = owner;
	}

	/// <summary>
	/// The shot's frame: the firing hardpoint's world orientation with the muzzle's world position in
	/// the translation. <c>WeaponMount_FireDispatch_GunBeam</c> overwrites the gun transform's
	/// translation with the muzzle point before handing it over, so the ray starts at the muzzle and
	/// runs down the transform's <b>Y</b> axis — model forward. A travelling shot passes its own
	/// frame, which is the same convention: where it is now, pointing the way it is going.
	/// </summary>
	public Transform3 Muzzle { get; }

	/// <summary>
	/// World to muzzle space. The raycast builds this once (<c>Sim_RaycastObjectList</c> caches it in
	/// the ray record at <c>+0x0a</c>) so that every candidate's hit test can work in the ray's own
	/// frame, where the ray is the Y axis and the miss distance is a plain 2D magnitude.
	/// </summary>
	public Transform3 MuzzleInverse { get; }

	/// <summary>
	/// The weapon's range, in world units — the mount template's int32 at <c>0x30</c>. It was the
	/// field <c>WeaponMounts.ToggleChain</c> reads only for its sign; <c>Bullet_FireBurst</c> takes it
	/// as the ray's length, which is what identifies it. Retail values run 75000 (ATC20, 450 m) down
	/// to 15000 (ELF2, 90 m).
	///
	/// <para><b>A travelling shot has no range of its own here</b> — it passes this tick's step
	/// instead, because that is the length of the segment it is asking about. Its real reach is its
	/// <c>BULLETS.DAT</c> lifetime, which is a different limit in a different place.</para>
	///
	/// <para>Kept separate from <see cref="Distance"/> only for reporting: the original has one field
	/// and overwrites it, so nothing in the query itself reads this.</para>
	/// </summary>
	public int Range { get; }

	/// <summary>
	/// The ray's length — the record's <c>+0x04</c>, which every hit test measures against. Starts at
	/// <see cref="Range"/> and the sweep shortens it to each hit as it finds one, so an object behind
	/// another cannot be struck through it.
	/// </summary>
	public int Distance { get; internal set; }

	/// <summary>The record's <c>+0x04</c>: <c>PROJ.DAT</c>'s <c>DamageArmor</c>, scaled by shot power.</summary>
	public short DamageArmor { get; }

	/// <summary>The record's <c>+0x06</c>: <c>PROJ.DAT</c>'s <c>DamageShield</c>, scaled by shot power.</summary>
	public short DamageShield { get; }

	/// <summary>
	/// The record's subtype id. Not part of the shot record in the original — <c>Bullet_FireBurst</c>
	/// keeps it as its own first parameter and hands it to the tracer directly. Carried here because
	/// it is what picks the shot's appearance: <c>BEAM.DAT</c> is indexed by this, not by weapon id.
	/// </summary>
	public short MissileId { get; }

	/// <summary>
	/// The record's <c>+0x08</c>: <c>PROJ.DAT</c>'s <c>SplashFactor</c>, the Q10 fraction of
	/// penetrating damage <c>Mech_ApplyDirectFireDamage</c> diverts into a secondary explosion, read
	/// by <c>MechObject.ApplyDirectFireDamage</c>. Zero for every real beam, so it only bites on
	/// projectile weapons.
	/// </summary>
	public short SplashFactor { get; }

	/// <summary>
	/// The record's <c>+0x0a</c>: a pointer to the firing <c>PROJ.DAT</c> record's three
	/// <c>ImpactFX</c> arrays, which is the only reason a hit test can see the record at all. Each
	/// array holds four effect ids and a struck object picks one at random from whichever array
	/// matches what the shot did — see <see cref="ImpactFxFor"/>.
	/// </summary>
	public ProjectileData.Projectile Effects { get; }

	/// <summary>
	/// One of the four ids in the group <paramref name="group"/> names. The original addresses all
	/// three arrays as one twelve-entry short array off the shot record's <c>+0x0a</c> and indexes it
	/// <c>group * 4 + (rand &amp; 3)</c>, so the groups are ordered exactly as the file writes them —
	/// see <see cref="ImpactFxGroup"/> for which branch reaches each.
	/// </summary>
	public short[] ImpactFx(ImpactFxGroup group) => group switch {
		ImpactFxGroup.Shield => Effects.ImpactFXShield,
		ImpactFxGroup.Ground => Effects.ImpactFXGround,
		_ => Effects.ImpactFXArmor,
	};

	/// <summary>
	/// Which of a <c>PROJ.DAT</c> record's three <c>ImpactFX</c> arrays a hit draws from. Every spawn
	/// site in the original reaches one of these three, and the file's own field order is this order.
	/// </summary>
	public enum ImpactFxGroup {
		/// <summary>
		/// <c>ImpactFXShield</c>. <c>Mech_DirectFireHitTest</c>'s branch for a shot the struck
		/// facing's shields <i>fully</i> absorbed — the flash off a shield rather than off a surface.
		/// </summary>
		Shield = 0,

		/// <summary>
		/// <c>ImpactFXGround</c>, and the name is accurate: it is what
		/// <see cref="SimWorld.Raycast"/> spawns when a shot ends on the terrain. It is <i>also</i>
		/// what <c>Mech_ApplyDirectFireDamage</c> uses for damage that got through armour but left the
		/// struck component in the health band it was already in — one array serving both.
		/// </summary>
		Ground = 1,

		/// <summary>
		/// <c>ImpactFXArmor</c>. The same armour branch when the struck component's health band did
		/// drop, and the only array the non-mech classes' hit test (<c>FUN_00405038</c>) ever uses.
		///
		/// <para>The distinction from <see cref="Ground"/> is a change in the struck component's
		/// health band, which <see cref="MechObject"/> now measures either side of the damage write,
		/// so both branches are reachable. It shows on nothing: <b>all 27 retail records carry
		/// byte-identical <c>ImpactFXGround</c> and <c>ImpactFXArmor</c> arrays</b>, so the two draw
		/// the same effect anyway.</para>
		/// </summary>
		Armor = 2,
	}

	/// <summary>The machine that fired. The sweep skips it, so nothing shoots itself.</summary>
	public SimObject? Owner { get; }

	/// <summary>What the sweep struck, or null if the shot reached its full range. Not in the original — see <see cref="SimWorld.Beams"/>.</summary>
	public SimObject? HitObject { get; internal set; }

	/// <summary>
	/// Where the ray met the ground, if it did — <c>Sim_RaycastTerrain</c>'s own hit point, which the
	/// original stashes in the globals at <c>004aab72</c>. Null when the shot cleared the terrain,
	/// which includes the case where an object was struck nearer than the ground was.
	///
	/// <para>A ground hit and a struck object are not exclusive: the terrain clip runs first, and an
	/// object found inside the shortened ray overwrites <see cref="Distance"/> but not this. What
	/// the shot ended on is therefore <see cref="HitObject"/> if that is set, and the ground
	/// otherwise.</para>
	/// </summary>
	public Vec3i? GroundHit { get; internal set; }
}
