namespace HercWorks.Core.Data.Struct.Dbsim;

/// <summary>
/// This struct is shared by both BULLETS.DAT and ROCKETS.DAT — for some reason BULLETS.DAT
/// uses SfxFireIdBullets and ROCKETS.DAT uses SfxFireIdMissiles.
///
/// <para>Three fields were resolved 2026-08-25 from DBSIM's bullet class — see
/// <c>Herculan/docs/simulation/projectiles.md</c>. They are still under their old names because the
/// WinForms editor binds to them; the doc comments say what they are.</para>
///
/// Ported from org.hercworks.core.data.struct.dbsim.ProjMissileDatEntry.
/// </summary>
public class ProjMissileDatEntry {
	/// <summary>Which root of the sibling <c>.DTS</c> the projectile is drawn as.</summary>
	public short ModelId { get; set; }

	/// <summary>
	/// How long the projectile lives, in 125 ms units. <c>Bullet_TickUpdate</c> drops it once its age
	/// passes <c>Lifetime * 0x200</c>, with no impact of any kind.
	/// </summary>
	public short Lifetime { get; set; }

	/// <summary>The slack the shot record allows the hit test, in place of a beam's literal 200.</summary>
	public short ClipRadius { get; set; }

	/// <summary>
	/// <b>Animation frame interval</b>, not a flag — the countdown <c>Bullet_TickUpdate</c> reloads
	/// each time it expires to step the drawn shape's frame on. Zero means a static shape; only the
	/// three EMP records set it, all to 256.
	/// </summary>
	public short Unk2Flag { get; set; }

	/// <summary>Sound id, played by the spawn as <c>id + 10</c>.</summary>
	public short SfxFireIdBullets { get; set; }

	/// <summary>
	/// <b>Firing scatter</b>, in binary-angle units — the spawn displaces two of the projectile's
	/// three euler angles by <c>(value * 2 &amp; random) - value</c>. 63 on every autocannon, zero on
	/// the EMP and plasma records.
	/// </summary>
	public short Unk3Uint16 { get; set; }

	/// <summary>
	/// For BULLETS.DAT, nonzero arms a per-lifetime rate at the object's <c>+0x61</c> whose consumer
	/// has not been traced; 1 on the three autocannon records and zero elsewhere.
	/// </summary>
	public short SfxFireIdMissiles { get; set; }
}
