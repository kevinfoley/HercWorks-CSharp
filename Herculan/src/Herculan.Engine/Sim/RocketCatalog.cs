using HercWorks.Core.Data.Struct.Dbsim;
using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Io.Transform.Dbsim;

namespace Herculan.Engine.Sim;

/// <summary>
/// <c>dat\ROCKETS.DAT</c>, the launcher family's own table — what <see cref="BulletCatalog"/> is to
/// a travelling gun round, this is to a <see cref="Rocket"/>.
///
/// <para><c>Rocket_LoadTypeTable_Unguided</c> (<c>0040a818</c>) reads it exactly as
/// <c>Bullet_LoadResources</c> reads its own: a count followed by that many fourteen-byte records,
/// into <c>maybe_RocketTypeTable_Unguided</c>. It then loads <c>dts\ROCKETS.DTS</c> into
/// <c>DAT_004a975c</c> and stops — <b>there is no <c>ROCKETS.DBA</c></b> and no bank is bound to any
/// rocket shape, which is the mechanical reason a rocket is ramp-coloured geometry rather than
/// textured (see <see cref="Scene.SceneModelLibrary.Rocket"/>).</para>
///
/// <para><b>Indexed by the firing <c>PROJ.DAT</c> record's subtype id</b>, like every other appearance
/// table here: <c>Rocket_GetTypeRecord</c> (<c>0040a234</c>) is <c>table + id * 14</c> against the id
/// <c>Missile_Construct</c> stored at the object's <c>+0x41</c>.</para>
///
/// <para><b>The record layout is not <c>BULLETS.DAT</c>'s.</b> The two files share a stride and a
/// first two fields and nothing else — the readers are different functions reading different
/// offsets, and the retail values only make sense under this reading:</para>
///
/// | Offset | Field | Meaning |
/// |---|---|---|
/// | <c>+0x00</c> | <see cref="ProjMissileDatEntry.ModelId"/> | root of <c>ROCKETS.DTS</c> |
/// | <c>+0x02</c> | <see cref="ProjMissileDatEntry.Lifetime"/> | in <b>ticks</b>, not in the bullet's <c>0x200</c> age units |
/// | <c>+0x04</c> | <see cref="ProjMissileDatEntry.ClipRadius"/> | <b>acceleration</b> — see <see cref="Rocket.Tick"/> |
/// | <c>+0x06</c> | <see cref="ProjMissileDatEntry.Unk2Flag"/> | the shot record's slack, which is what a bullet keeps at <c>+0x04</c> |
/// | <c>+0x08</c> | <see cref="ProjMissileDatEntry.SfxFireIdBullets"/> | animation frame interval; 0 = static shape |
/// | <c>+0x0a</c> | <see cref="ProjMissileDatEntry.Unk3Uint16"/> | which of the shape's sequences that interval steps |
/// | <c>+0x0c</c> | <see cref="ProjMissileDatEntry.SfxFireIdMissiles"/> | sound id, played as <c>id + 10</c> — the one field whose old name was right |
///
/// <para>Retail ships five records, one per <c>Missile</c> subtype id, and four of them are the same
/// record:</para>
///
/// | id | Weapon | Shape | Life | Accel | Slack | Anim |
/// |---|---|---|---|---|---|---|
/// | 0 | <c>SARH</c> | 0 | 80 | 250 | 200 | 256 |
/// | 1 | <c>ARH</c> | 0 | 80 | 250 | 200 | 256 |
/// | 2 | <c>ARM</c> | 0 | 80 | 250 | 200 | 256 |
/// | 3 | <c>EO</c> | 0 | 80 | 250 | 200 | 256 |
/// | 4 | <c>BMSL</c> | 1 | 80 | 250 | 300 | 0 |
///
/// <para>So every launcher round flies for 80 ticks (3.2 s at the simulation's rate) and accelerates
/// at the same figure; the big missile is the one with its own shape, a wider hit slack and a shape
/// that does not animate.</para>
/// </summary>
public sealed class RocketCatalog {
	/// <summary>The resource folder and name <c>Rocket_LoadTypeTable_Unguided</c> opens, by the literal name <c>rockets</c>.</summary>
	public const string ResourceFolder = "dat";

	/// <inheritdoc cref="ResourceFolder" />
	public const string TableResource = "ROCKETS.DAT";

	private readonly MissileDatFile _table;

	private RocketCatalog(MissileDatFile table) {
		_table = table;
	}

	/// <summary>How many records were read.</summary>
	public int Count => _table.Entries?.Length ?? 0;

	/// <summary>
	/// Parses the table. Returns null when the resource is missing or unreadable, in which case a
	/// launcher fires nothing — see <see cref="SimWorld.FireRocket"/>.
	/// </summary>
	public static RocketCatalog? Load(byte[]? rocketsDat) =>
		rocketsDat != null
			&& new MissileDatFileTransformer().BytesToObject(rocketsDat) is MissileDatFile { Entries: not null } table
			? new RocketCatalog(table)
			: null;

	/// <summary>
	/// The record for <paramref name="missileId"/>, or null when the id is outside the table — which
	/// no retail <c>Missile</c> record is, but a hand-edited <c>PROJ.DAT</c> could be.
	/// </summary>
	public ProjMissileDatEntry? Record(int missileId) =>
		_table.Entries is { } entries && missileId >= 0 && missileId < entries.Length
			? entries[missileId]
			: null;
}
