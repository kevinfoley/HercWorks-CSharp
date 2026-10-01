namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/WEAPONS.DAT — DBSIM's own runtime weapon-mount-template table (distinct from
/// SHELL0/GAM/WEAPONS.DAT, the UI-facing weapon catalog, <see cref="Shell.WeaponsDat"/>). Loaded by
/// <c>Weapons_LoadResourceTables</c> (<c>0040fc8c</c>) from a resource named "weapons"; one record per
/// weapon id, 33 in retail.
///
/// <para>Records are variable-length, not a fixed stride: each is built from the same low-level
/// record readers <c>.DMG</c>/<c>.COL</c> use (<c>HercPiece_ReadRecord</c>,
/// <c>Collision_ReadCluster</c>/<c>Collision_ReadSphereArray</c>) — see
/// <see cref="WeaponMountTemplate"/>. Fields whose meaning is open are kept raw so the file
/// round-trips byte-exact. Layout: docs/formats/weapons-dat-sim.md.</para>
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
	/// One weapon's mount-template record. See docs/formats/weapons-dat-sim.md for the field-by-field
	/// evidence; the fields before <see cref="Tail"/> are modeled raw, and their meaning is open.
	/// </summary>
	public class WeaponMountTemplate {
		/// <summary>0 for NONE; one of 1500, 2000, 2500, 15000 for every real weapon. Meaning unknown — not the range.</summary>
		public short Field0 { get; set; }

		/// <summary>0 for NONE; -1 for every real weapon. Meaning unknown.</summary>
		public short Field1 { get; set; }

		/// <summary>0 for NONE; 0x01FF (511) for every real weapon. Meaning unknown.</summary>
		public short Field2 { get; set; }

		/// <summary>
		/// <c>HercPiece_ReadRecord</c>'s dependent sub-component list, reused here. (20, 12) on every
		/// real weapon and empty for NONE; meaning unknown.
		/// </summary>
		public short[] DependentRaw { get; set; } = [];

		/// <summary>
		/// Read through <c>Collision_ReadCluster</c>, where it is a component index. 0x13 (19) in every
		/// record including NONE; meaning here unknown.
		/// </summary>
		public short SubSphereFlagRaw { get; set; }

		/// <summary>
		/// Read through <c>Collision_ReadSphereArray</c>: the low 13 bits are the entry count of
		/// <see cref="FiringSequence"/> (see <see cref="FiringSequenceCount"/>); the top three are that
		/// format's flag bits, never set here.
		/// </summary>
		public short SubMeshCountRaw { get; set; }

		public int FiringSequenceCount => SubMeshCountRaw & 0x1FFF;

		/// <summary>
		/// <see cref="FiringSequenceCount"/> entries of four raw int16s each. Meaning unknown; kept raw.
		/// </summary>
		public short[][] FiringSequence { get; set; } = [];

		/// <summary>
		/// The 48-byte tail, record offset <c>0x22</c>-<c>0x51</c>, kept raw. The doc's table gives the
		/// decoded fields by in-memory offset; tail-relative is that minus <c>0x22</c>: the four model
		/// shapes at <c>0x00</c>-<c>0x06</c>, minimum range (int32) <c>0x0a</c>, range (int32)
		/// <c>0x0e</c>, AI shot-value penalty <c>0x12</c>, energy thresholds <c>0x14</c>/<c>0x16</c>,
		/// magazine size <c>0x18</c>, barrel count <c>0x1a</c>, <see cref="ProjDatIndex"/> <c>0x1c</c>,
		/// muzzle offset <c>0x1e</c>-<c>0x22</c>, side offsets <c>0x24</c>/<c>0x28</c>, refire delay
		/// <c>0x2a</c>, <see cref="DamageIconIndex"/> <c>0x2e</c>. See
		/// docs/formats/weapons-dat-sim.md#decoded-tail-fields.
		/// </summary>
		public byte[] Tail { get; set; } = new byte[0x30];

		/// <summary>
		/// Tail-relative <c>0x1c</c> (in-memory <c>0x3e</c>) — how the weapon reaches its
		/// <see cref="ProjectileData"/> record: <c>0x21</c> (33) for none (<c>ECM</c>), <c>0x22</c> (34)
		/// for a <c>Missile</c> record chosen by the hardpoint's ammunition type
		/// (<c>MSL6</c>/<c>MSL8</c>/<c>MSL10</c>/<c>FLYMSL</c>), otherwise a direct PROJ.DAT index. The
		/// non-firing entries carry 0, which their mount constructors never consume. See
		/// docs/formats/weapons-dat-sim.md#projdatindex--tail-relative-offset-0x1c-absolute-offset-0x3e.
		/// </summary>
		public short ProjDatIndex => BitConverter.ToInt16(Tail, 0x1c);

		/// <summary>
		/// Tail-relative <c>0x2e</c> (in-memory <c>0x50</c>): which icon of the <c>WEAPONS</c> bank the
		/// Heads-Down Display's damage detail draws for this weapon, before the <c>.PDG</c>
		/// hardpoint's own frame offset is added. -1 draws none. See
		/// docs/formats/weapons-dat-sim.md.
		/// </summary>
		public short DamageIconIndex => BitConverter.ToInt16(Tail, 0x2e);

		/// <summary>
		/// Which shape of <c>dts\MECHWPNS.DTS</c> this weapon is drawn as when it is fitted to a
		/// hardpoint whose mounting code (<c>.GL +6</c>,
		/// <see cref="Dbsim.GunLayout.HardpointEntry.MountingCode"/>) is
		/// <paramref name="mountingCode"/> — four shorts at tail-relative <c>0x00</c>-<c>0x06</c>,
		/// one per code, read by <c>WeaponMount_ShapeForMountingCode</c> (<c>0040fab0</c>) as <c>template[0x22 + code * 2]</c>.
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
