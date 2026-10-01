namespace HercWorks.Core.Data.Struct.Dbsim;

/// <summary>
/// One 14-byte record of BULLETS.DAT or ROCKETS.DAT. The two files share the stride and their first
/// two fields; DBSIM reads the rest at different offsets for each, so the property names past
/// <see cref="Lifetime"/> are BULLETS.DAT's except <see cref="SfxFireIdMissiles"/>, and each comment
/// gives the ROCKETS.DAT meaning too. See docs/simulation/projectiles.md and
/// docs/simulation/rockets.md.
/// </summary>
public class ProjMissileDatEntry {
	/// <summary><c>+0x00</c> — which root of the sibling <c>.DTS</c> the projectile is drawn as.</summary>
	public short ModelId { get; set; }

	/// <summary>
	/// <c>+0x02</c> — how long the projectile lives. A bullet's is in 125 ms units (it is dropped once
	/// its age passes <c>Lifetime * 0x200</c>); a rocket's is in ticks. Either way it expires with no
	/// impact.
	/// </summary>
	public short Lifetime { get; set; }

	/// <summary>
	/// <c>+0x04</c> — for a bullet, the slack the shot record allows the hit test, in place of a beam's
	/// literal 200. For a rocket, its acceleration per 125 ms.
	/// </summary>
	public short ClipRadius { get; set; }

	/// <summary>
	/// <c>+0x06</c> — for a bullet, the animation frame interval: the countdown reloaded each time it
	/// expires to step the drawn shape's frame. Zero means a static shape; only the three EMP records
	/// set it, all to 256. For a rocket, the shot record's hit-test slack.
	/// </summary>
	public short FrameInterval { get; set; }

	/// <summary>
	/// <c>+0x08</c> — for a bullet, the fire sound id, played as <c>id + 10</c>. For a rocket, the
	/// animation frame interval.
	/// </summary>
	public short SfxFireIdBullets { get; set; }

	/// <summary>
	/// <c>+0x0a</c> — for a bullet, the firing scatter in binary-angle units: the spawn displaces two
	/// of the projectile's three euler angles by <c>(value * 2 &amp; random) - value</c>. 63 on every
	/// autocannon, zero on the EMP and plasma records. For a rocket, which of the shape's sequences
	/// the frame interval steps.
	/// </summary>
	public short Scatter { get; set; }

	/// <summary>
	/// <c>+0x0c</c> — for a rocket, the fire sound id, played as <c>id + 10</c>. For a bullet, nonzero
	/// arms a per-lifetime rate at the object's <c>+0x61</c> whose reader is open
	/// (docs/simulation/projectiles.md#open); 1 on the three autocannon records and zero elsewhere.
	/// </summary>
	public short SfxFireIdMissiles { get; set; }
}
