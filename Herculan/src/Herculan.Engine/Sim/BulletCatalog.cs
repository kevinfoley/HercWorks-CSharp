using HercWorks.Core.Data.Struct.Dbsim;
using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Io.Transform.Dbsim;

namespace Herculan.Engine.Sim;

/// <summary>
/// <c>dat\BULLETS.DAT</c>, the travelling projectile's own table — the counterpart of
/// <see cref="Content.BeamAppearance"/>'s <c>BEAM.DAT</c> for everything that is not a beam.
///
/// <para><c>FUN_0040ade0</c> — the bullet module's init, named by the <c>BULLET.CPP</c> string at
/// <c>00498601</c> — loads three resources once at startup and this is the first: a count followed
/// by that many fourteen-byte records, into the table at <c>DAT_004a977c</c>. The other two are
/// <c>dts\BULLETS.DTS</c> (the shapes the records' first field indexes, into <c>DAT_004a9784</c>)
/// and <c>dba\BULLETS.DBA</c> (the bank every one of those shapes is textured from) — see
/// <see cref="Scene.SceneModelLibrary.Bullet"/>.</para>
///
/// <para><b>Indexed by the firing <c>PROJ.DAT</c> record's subtype id</b>, not by weapon id, exactly
/// as <c>BEAM.DAT</c> is: <c>Bullet_Construct</c> stores that id at the object's <c>+0x41</c> and
/// <c>FUN_0040adc0</c> is nothing but <c>table + id * 14</c>.</para>
///
/// <para>Retail ships twelve records for the seven subtype ids real <c>Bullet</c> records use:</para>
///
/// | id | Weapons | Shape | Life | Radius | Spread |
/// |---|---|---|---|---|---|
/// | 0 | ATC20 | 0 | 20 | 100 | 63 |
/// | 1 | ATC35, ATC75 | 4 | 18 | 100 | 63 |
/// | 2 | ATC50, ATC100 | 5 | 16 | 100 | 63 |
/// | 6 | EMPC | 2 | 30 | 100 | 0 |
/// | 7 | BEMP | 3 | 30 | 200 | 0 |
/// | 8 | EMP2 | 2 | 30 | 100 | 0 |
/// | 9 | PLAS, MFAC | 8 | 40 | 100 | 0 |
///
/// <para>Ids 3, 4, 5, 10 and 11 are present but unreachable: no retail <c>Bullet</c> record carries
/// them. (Those subtype ids do exist on <c>Beam</c> records, which read <c>BEAM.DAT</c> instead.)</para>
/// </summary>
public sealed class BulletCatalog {
	/// <summary>The resource folder and name <c>FUN_0040ade0</c> opens, by the literal name <c>bullets</c>.</summary>
	public const string ResourceFolder = "dat";

	/// <inheritdoc cref="ResourceFolder" />
	public const string TableResource = "BULLETS.DAT";

	/// <summary>
	/// The rate the age counter climbs at, and the unit <see cref="ProjMissileDatEntry.Lifetime"/>
	/// is measured against — <c>Bullet_TickUpdate</c>'s literal <c>Math_IntegrateRateOverTick(0x200)</c>
	/// and its <c>lifetime * 0x200 &lt; age</c> test. Since <c>0x200</c> is the rate per 125 ms, a
	/// record's lifetime is directly a count of 125 ms intervals: ATC20's 20 is 2.5 seconds.
	/// </summary>
	public const short AgeRate = 0x200;

	private readonly MissileDatFile _table;

	private BulletCatalog(MissileDatFile table) {
		_table = table;
	}

	/// <summary>How many records were read.</summary>
	public int Count => _table.Entries?.Length ?? 0;

	/// <summary>
	/// Parses the table. Returns null when the resource is missing or unreadable, in which case
	/// nothing that fires a travelling projectile can build one — see
	/// <see cref="SimWorld.FireBullet"/>.
	/// </summary>
	public static BulletCatalog? Load(byte[]? bulletsDat) =>
		bulletsDat != null
			&& new MissileDatFileTransformer().BytesToObject(bulletsDat) is MissileDatFile { Entries: not null } table
			? new BulletCatalog(table)
			: null;

	/// <summary>
	/// The record for <paramref name="missileId"/>, or null when the id is outside the table — which
	/// no retail <c>Bullet</c> record is, but a hand-edited <c>PROJ.DAT</c> could be.
	/// </summary>
	public ProjMissileDatEntry? Record(int missileId) =>
		_table.Entries is { } entries && missileId >= 0 && missileId < entries.Length
			? entries[missileId]
			: null;
}
