namespace HercWorks.Core.Data.File.Sav;

/// <summary>
/// <c>ES2\DATA\player.mec</c> — the player's own squad, written by VSHELL alongside
/// <c>data\script.dat</c> and read by DBSIM at world init. Where <c>script.dat</c>'s block 7 is the
/// mission's roster of AI mechs, this is the roster of the ones the player brought: the machine the
/// player pilots plus any wingmen, each with the loadout configured in the shell's HERC bay.
///
/// <para>Decoded from <c>DBSim_LoadScriptDat</c> (<c>00424308</c>), which opens this file
/// immediately before <c>script.dat</c>, and from <c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>), DBSIM's world-spawn pass,
/// which appends these entries to the end of the mission's mech list and hands each one's two
/// <see cref="MecEntry.WeaponRefs"/>/<see cref="MecEntry.WeaponAmmoTypes"/> arrays to the same
/// <c>Mech_ConfigureLoadout</c> that <c>script.dat</c>'s own records feed. The squad spawns at the
/// position carried by <c>script.dat</c> block 11's <b>record 0</b>, which exists purely to place
/// it — DBSIM overwrites that record's member list with these entries.</para>
///
/// <para>The 35 bytes past the last entry in the retail 263-byte sample are not slack: VSHELL's
/// <c>FUN_00412253</c> closes the export with <c>int16 33</c> and then 33 bytes, one per weapon
/// catalog id, carrying that weapon's <c>weapons.dat</c> <c>+0x16</c> unlock flag. They are
/// <see cref="WeaponFlags"/>. See <c>docs/shell/campaign-loop.md</c>.</para>
///
/// <para>Replaces a never-implemented stub that guessed this file held a single VSHELL
/// <c>ShellHercPart</c>.</para>
/// </summary>
public class MecFile {
	/// <summary>
	/// Which <see cref="Entries"/> slot the player themself pilots; the rest are wingmen. DBSIM
	/// compares this against the entry index as it spawns them and flags the match as the camera's
	/// and the input's owner.
	/// </summary>
	public short PlayerEntryIndex { get; set; }

	/// <summary>The squad, in the order DBSIM appends them to the mission's mech list.</summary>
	public MecEntry[] Entries { get; set; } = [];

	/// <summary>
	/// The weapon-unlock table VSHELL writes after the last entry — one byte per weapon catalog id,
	/// 33 of them in every retail file, each that weapon's <c>weapons.dat</c> <c>+0x16</c> field:
	/// whether the campaign has unlocked that weapon for purchase.
	///
	/// <para>Duplicated state, and nothing traced reads it back: VSHELL reopens this file only in
	/// its map screen and takes just the two leading shorts, and DBSIM's reader stops at the last
	/// entry. The save slot is where these flags are authoritative — this file is regenerated from
	/// it at every mission launch — so the table is preserved for byte-fidelity, not because the
	/// game depends on it.</para>
	///
	/// <para>Empty when the source file carried no table, which is the case for files written
	/// before it was decoded; retail accepts those. An empty table is written back as no table at
	/// all rather than as zeroes, because the flags cannot be reconstructed from anything else in
	/// this file and inventing them would state something false about the player's armory.</para>
	/// </summary>
	public byte[] WeaponFlags { get; set; } = [];
}

/// <summary>
/// One machine in the player's squad. DBSIM reads the two leading fields but never uses them along
/// the paths traced so far; they carry the entry's pilot, and the three trailing spans are copied
/// wholesale into the mech's in-memory record, so they round-trip raw rather than being guessed at.
/// </summary>
public class MecEntry {
	/// <summary>
	/// The pilot's name index into <c>esnames.bin</c> — pilot record <c>+0x02</c> on the shell side.
	/// See <c>docs/shell/campaign-loop.md</c> for the writer, VSHELL's <c>FUN_004106b7</c>.
	/// </summary>
	public short Unk00 { get; set; }

	/// <summary>The pilot's skill tier, 0-3 — pilot record <c>+0x25</c> on the shell side.</summary>
	public short Unk02 { get; set; }

	/// <summary>The mech type, an index into <c>nam\MECHS.NAM</c>'s name list — the same numbering
	/// <see cref="Msn.Script.ScriptSpawnRecordExport.SmallDiscrete"/> uses.</summary>
	public short MechType { get; set; }

	/// <summary>
	/// How many weapon slots this entry declares. Both arrays below are this long on disk, which is
	/// what makes the record variable-length.
	/// </summary>
	public short SlotCount { get; set; }

	/// <summary>Per-slot weapon ids; <c>0</c> for an empty slot.</summary>
	public short[] WeaponRefs { get; set; } = [];

	/// <summary>
	/// Per-slot second value, paired with <see cref="WeaponRefs"/> by DBSIM's loadout call — the
	/// ammunition type each missile launcher is loaded with.
	///
	/// <para>Resolved from <c>MechLoadout_ConstructWeaponMounts</c> (<c>0040fff8</c>),
	/// which takes this array's entry for a hardpoint through
	/// <c>Proj_LookupRecord(Missile, key)</c> whenever the weapon's template carries the launcher
	/// sentinel, and from <c>FUN_0040e18c</c>, which then prints that record's own subtype as the
	/// mount's name. That is why the retail player's <c>MSL10</c> hardpoint reads <c>ARH</c> in the
	/// cockpit rather than <c>MSL10</c>. Non-launcher slots carry a filler 5, which the factory
	/// rewrites to 0 before looking it up.</para>
	/// </summary>
	public short[] WeaponAmmoTypes { get; set; } = [];

	public short Unk3A { get; set; }

	/// <summary>
	/// 26 bytes copied to the mech record at <c>+0x3c</c>. Still undecoded on the shell side too.
	/// </summary>
	public byte[] BlockA { get; set; } = new byte[26];

	/// <summary>
	/// 20 bytes copied to the mech record at <c>+0x56</c> — ten <c>int16</c> condition values on the
	/// shell side, of which index 9 is the machine's overall condition: the value the debrief reads
	/// to set its pilot's, and resets to 100 for a machine it does not scrap. Retail data holds
	/// 0-100 throughout.
	/// </summary>
	public byte[] BlockB { get; set; } = new byte[20];

	/// <summary>
	/// 20 bytes copied to the mech record at <c>+0x6a</c> — ten <c>int16</c> per-hardpoint condition
	/// values, one per weapon slot, in the same slot order as <see cref="MecEntry.WeaponRefs"/>. The
	/// shell destroys a mount whose value reaches 0.
	/// </summary>
	public byte[] BlockC { get; set; } = new byte[20];
}
