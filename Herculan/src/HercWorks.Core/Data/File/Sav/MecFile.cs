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
/// <para>Like <c>script.dat</c>, the real file is longer than its content: the retail sample is 263
/// bytes and its two entries account for 228, with the rest stale. Nothing here reads past the last
/// declared entry, matching DBSIM.</para>
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
}

/// <summary>
/// One machine in the player's squad. The two leading fields have no confirmed meaning yet — they
/// are read but never used along the paths traced so far — and the three trailing spans are copied
/// wholesale into the mech's in-memory record, so they round-trip raw rather than being guessed at.
/// </summary>
public class MecEntry {
	public short Unk00 { get; set; }

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

	/// <summary>26 bytes copied to the mech record at <c>+0x3c</c>.</summary>
	public byte[] BlockA { get; set; } = new byte[26];

	/// <summary>20 bytes copied to the mech record at <c>+0x56</c>.</summary>
	public byte[] BlockB { get; set; } = new byte[20];

	/// <summary>20 bytes copied to the mech record at <c>+0x6a</c>.</summary>
	public byte[] BlockC { get; set; } = new byte[20];
}
