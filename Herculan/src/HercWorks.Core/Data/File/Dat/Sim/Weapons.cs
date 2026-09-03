namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/WEAPONS.DAT — DBSIM's own runtime weapon-mount-template table (distinct from
/// SHELL0/GAM/WEAPONS.DAT, the UI-facing weapon catalog ported as <see cref="Shell.WeaponsDat"/>).
///
/// Cracked by decompiling the real loader in DBSIM.EXE (see
/// docs/formats/weapons-dat-sim.md for the full writeup and open semantic gaps) — a resource
/// literally named "weapons", opened by <c>Weapons_LoadResourceTables</c> (0x0040fc8c). Structure
/// confirmed byte-exact: parsing this exact record shape consumes the real retail file's full 3790
/// content bytes across its 33 records with zero remainder. Every byte is preserved (even where the
/// semantic meaning isn't decoded yet), so this round-trips byte-exact despite several fields being
/// undecoded.
///
/// Records are variable-length, NOT a fixed stride — each is built out of the exact same low-level
/// record readers <c>.DMG</c>/<c>.COL</c> use (<c>HercPiece_ReadRecord</c>,
/// <c>Collision_ReadCluster</c>/<c>Collision_ReadSphereArray</c>), reused generically by
/// DBSIM rather than a bespoke weapon-record reader — see <see cref="WeaponMountTemplate"/>.
/// Ported from org.hercworks.core.data.file.dat.sim.Weapons (extended — the original Java class
/// only ever modeled the Total field).
/// </summary>
public class Weapons {
	public short Total { get; set; }
	public WeaponMountTemplate[]? Templates { get; set; }

	public Weapons() { }

	public Weapons(short total) {
		Total = total;
	}

	public WeaponMountTemplate NewWeaponMountTemplate() => new();

	/// <summary>
	/// One weapon's mount-template record. See docs/formats/weapons-dat-sim.md for the full
	/// field-by-field evidence — most fields here are confirmed present/sized but NOT semantically
	/// decoded; they're modeled and round-tripped raw rather than dropped.
	/// </summary>
	public class WeaponMountTemplate {
		/// <summary>0 for id0/NONE; one of {1500, 2000, 2500, 15000} for every real weapon seen —
		/// too few distinct values to be a per-weapon-unique stat, plausibly a range/tier bucket.</summary>
		public short Field0 { get; set; }

		/// <summary>0 for NONE; exactly -1 (0xFFFF) for every real weapon seen.</summary>
		public short Field1 { get; set; }

		/// <summary>0 for NONE; exactly 0x01FF (511) for every real weapon seen.</summary>
		public short Field2 { get; set; }

		/// <summary>
		/// HercPiece_ReadRecord's "dependent sub-component list," reused generically here. Always
		/// exactly (20, 12) in every real weapon record seen (empty for NONE) — never observed to
		/// vary, despite the mechanism supporting an arbitrary-length list. Raw 16-bit values,
		/// semantics unknown.
		/// </summary>
		public short[] DependentRaw { get; set; } = [];

		/// <summary>
		/// Read via the reused Collision_ReadCluster call. Constant 0x13 (19) in EVERY real
		/// record seen, including id0/NONE — not the boolean flag its origin function's name
		/// suggests; semantics unknown in this context.
		/// </summary>
		public short SubSphereFlagRaw { get; set; }

		/// <summary>
		/// Read via the reused Collision_ReadSphereArray call. The real entry count is this value
		/// masked with 0x1FFF (top 3 bits reserved for flags in the original collision-record
		/// format; never observed set in real weapon data) — see <see cref="FiringSequenceCount"/>.
		/// </summary>
		public short SubMeshCountRaw { get; set; }

		public int FiringSequenceCount => SubMeshCountRaw & 0x1FFF;

		/// <summary>
		/// FiringSequenceCount entries, each 4 raw int16s. Real values resemble the original Java
		/// doc comment's guessed per-shot muzzle-offset/fire-rate "SEQ" array for multi-shot/chain
		/// weapons — plausible but NOT confirmed field-by-field. Kept raw.
		/// </summary>
		public short[][] FiringSequence { get; set; } = [];

		/// <summary>
		/// Trailing 48 raw bytes (0x30). Kept raw, but much of it is decoded and read directly by
		/// <c>Herculan.Engine.Sim.WeaponMount</c> at these tail-relative offsets: <c>0x00</c> range,
		/// <c>0x06</c>/<c>0x08</c> the readiness threshold pair, <c>0x0a</c> magazine size,
		/// <c>0x0c</c> barrel count, <c>0x10</c>-<c>0x14</c> the muzzle offset triple,
		/// <c>0x16</c>/<c>0x1a</c> the side offsets, <c>0x1c</c> <see cref="ProjDatIndex"/>, and
		/// <c>0x1e</c> the refire interval. See docs/formats/weapons-dat-sim.md.
		/// </summary>
		public byte[] Tail { get; set; } = new byte[0x30];

		/// <summary>
		/// Tail-relative offset 0x1c (absolute in-memory offset 0x3e). Read by DBSIM's
		/// <c>Mech_ConfigureLoadout</c> (0x004175dc) -&gt; weapon-mount factory (0x0040fff8), which
		/// branches on it. This is the mechanism
		/// behind <see cref="ProjectileData"/>'s weapon-id-to-record mapping — see
		/// docs/simulation/damage-system.md and docs/formats/weapons-dat-sim.md for the full
		/// writeup, and <c>ProjectileData</c>'s own doc comment for the resulting confirmed mapping.
		/// Three cases, confirmed against every real weapon in the retail catalog:
		///   <list type="bullet">
		///   <item>0x21 (33) — no PROJ.DAT lookup at all. Only ECM has this literal sentinel; the six
		///   other non-firing catalog entries (NONE, LAEW, MINE, TARG, SHLD, TURB, ENRG) instead carry
		///   an all-zero/blank template (this field reads 0, a coincidentally "valid" index that their
		///   mount constructors never actually consume — confirmed by reading the mount-factory's
		///   per-case argument lists, not all of which pass the resolved PROJ.DAT pointer through).</item>
		///   <item>0x22 (34) — resolved via a (category=Missile, secondary-key) search
		///   (<c>Proj_LookupRecord</c>) instead of a direct index. The secondary key is the mission's
		///   ammunition-type array (<c>MecEntry.WeaponAmmoTypes</c>, or <c>script.dat</c> block 7
		///   offset <c>0x72</c>), which is what selects among PROJ.DAT's remaining Missile/Rocket
		///   entries (indices 7-13); <c>Herculan.Engine.Sim.WeaponCatalog</c> resolves it. Seen only
		///   for MSL6/MSL8/MSL10/FLYMSL.</item>
		///   <item>otherwise — a direct flat array index into PROJ.DAT
		///   (<c>ProjDat_RecordTable[value]</c>), confirmed byte-exact against all 21 other real
		///   catalog weapons (e.g. ATC20/35/50/75/100 -&gt; indices 0/1/2/23/24 in a clean armor-damage
		///   progression; PLAS -&gt; index 22, the same MissileId==9 splash-Bullet record already
		///   independently identified as the Plasma cannon by mechanism alone).</item>
		///   </list>
		/// </summary>
		public short ProjDatIndex => BitConverter.ToInt16(Tail, 0x1c);

		/// <summary>
		/// Which shape of <c>dts\MECHWPNS.DTS</c> this weapon is drawn as when it is fitted to a
		/// hardpoint whose mounting code (<c>.GL +6</c>,
		/// <see cref="Dbsim.GunLayout.HardpointEntry.AngleDirOption"/>) is
		/// <paramref name="mountingCode"/> — four shorts at tail-relative <c>0x00</c>-<c>0x06</c>,
		/// one per code, read by <c>FUN_0040fab0</c> as <c>template[0x22 + code * 2]</c>.
		///
		/// <para>The four are the same gun modelled for the four ways it can hang off a chassis, so
		/// an autocannon reads four different shapes and a shoulder-mounted launcher reads the same
		/// one four times. <b>The shape's flipbook is the muzzle flash</b> — see
		/// <c>Herculan.Engine.Sim.WeaponMount.FlashCell</c>.</para>
		///
		/// <para>Code 4 is the invisible mounting and has no entry: nothing is drawn for it and the
		/// base mount constructor loads no shape at all.</para>
		/// </summary>
		public short ModelShapeIndex(int mountingCode) =>
			mountingCode >= 0 && mountingCode < 4 ? BitConverter.ToInt16(Tail, mountingCode * 2) : (short)-1;
	}
}
