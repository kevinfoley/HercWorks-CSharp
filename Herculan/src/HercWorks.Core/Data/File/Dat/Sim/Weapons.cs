namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/WEAPONS.DAT — DBSIM's own runtime weapon-mount-template table (distinct from
/// SHELL0/GAM/WEAPONS.DAT, the UI-facing weapon catalog ported as <see cref="Shell.WeaponsDat"/>).
///
/// Cracked 2026-08-11 by decompiling the real loader in DBSIM.EXE (see
/// docs/formats/weapons-dat-sim.md for the full writeup and open semantic gaps) — a resource
/// literally named "weapons", opened by <c>Weapons_LoadResourceTables</c> (0x0040fc8c). Structure
/// confirmed byte-exact: parsing this exact record shape consumes the real retail file's full 3790
/// content bytes across its 33 records with zero remainder. Every byte is preserved (even where the
/// semantic meaning isn't decoded yet), so this round-trips byte-exact despite several fields being
/// undecoded.
///
/// Records are variable-length, NOT a fixed stride — each is built out of the exact same low-level
/// record readers <c>.DMG</c>/<c>.COL</c> use (<c>HercPiece_ReadRecord</c>,
/// <c>Collision_LoadSubSphereFlag</c>/<c>Collision_LoadSubMeshIndices</c>), reused generically by
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
		/// Read via the reused Collision_LoadSubSphereFlag call. Constant 0x13 (19) in EVERY real
		/// record seen, including id0/NONE — not the boolean flag its origin function's name
		/// suggests; semantics unknown in this context.
		/// </summary>
		public short SubSphereFlagRaw { get; set; }

		/// <summary>
		/// Read via the reused Collision_LoadSubMeshIndices call. The real entry count is this value
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

		/// <summary>Trailing 48 raw bytes (0x30) — mostly undecoded, but see <see cref="ProjDatIndex"/>
		/// for the one field within it that's now fully resolved.</summary>
		public byte[] Tail { get; set; } = new byte[0x30];

		/// <summary>
		/// Tail-relative offset 0x1c (absolute in-memory offset 0x3e) — SOLVED 2026-08-11 by tracing
		/// DBSIM's <c>Mech_ConfigureLoadout</c> (0x004175dc) -&gt; weapon-mount factory (0x0040fff8,
		/// which reads this exact field into a local it then branches on). This is the mechanism
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
		///   (<c>Proj_LookupRecord</c>) instead of a direct index; the secondary key comes from a
		///   different per-hardpoint table this record doesn't contain, so which of PROJ.DAT's 7
		///   remaining Missile/Rocket entries (indices 7-13) a given mount resolves to isn't fully
		///   traceable from WEAPONS.DAT alone. Seen only for MSL6/MSL8/MSL10/FLYMSL.</item>
		///   <item>otherwise — a direct flat array index into PROJ.DAT
		///   (<c>ProjDat_RecordTable[value]</c>), confirmed byte-exact against all 21 other real
		///   catalog weapons (e.g. ATC20/35/50/75/100 -&gt; indices 0/1/2/23/24 in a clean armor-damage
		///   progression; PLAS -&gt; index 22, the same MissileId==9 splash-Bullet record already
		///   independently identified as the Plasma cannon by mechanism alone).</item>
		///   </list>
		/// </summary>
		public short ProjDatIndex => BitConverter.ToInt16(Tail, 0x1c);
	}
}
