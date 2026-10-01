namespace HercWorks.Core.Data.File.Msn.Script;

/// <summary>
/// <c>data\script.dat</c> — the mission as VSHELL hands it to DBSIM, and as VSHELL's briefing map
/// (<c>ShellMap</c>) reads it. Written by the mission load
/// (<see cref="Io.Transform.Common.MissionGenerator.WriteScriptDat"/>) after it applies the
/// <c>.MSN</c>'s conditions and variants: each block is one <see cref="MissionFile"/> row's surviving
/// records, refs renumbered to block indices, some fields dropped. See docs/formats/script-dat.md.
///
/// <b>DBSIM reads this file twice.</b> <c>DBSim_LoadScriptDat</c> counts live objects and sizes its
/// pools, keeping little more than each roster record's type field; <c>DBSim_SpawnMissionObjects</c>
/// then re-opens the file and walks blocks 7-13 again to actually build the world, and that is the
/// pass that reads positions, headings and loadouts. Judging a block by what the first pass keeps
/// gives the wrong answer for most of them — see the format doc's "The two-pass read".
///
/// Bytes past block 13's end are a longer earlier mission's leftovers, since the writer does not
/// truncate (docs/formats/script-dat.md#fixed-size-file-structure). This model round-trips through
/// block 13 only.
/// </summary>
public class ScriptDat {
	/// <summary>
	/// The 20-byte header, ten shorts, kept raw so the two fields nothing reads (offsets 4 and 16)
	/// round-trip as loaded; the named properties below are views over it. See
	/// docs/formats/script-dat.md#header-format.
	/// </summary>
	public byte[] HeaderBytes { get; set; } = new byte[HeaderSize];

	/// <summary>Bytes the header occupies.</summary>
	public const int HeaderSize = 20;

	/// <summary>Header offset 0 — the theater, 0-4.</summary>
	public short TheaterIndex { get => ReadHeader(0); set => WriteHeader(0, value); }

	/// <summary>Header offset 2 — the zone, passed to <c>Terrain_LoadZone</c>.</summary>
	public short ZoneIndex { get => ReadHeader(2); set => WriteHeader(2, value); }

	/// <summary>Header offset 6 — the mission objective type the player's think watches for.</summary>
	public short ObjectiveType { get => ReadHeader(6); set => WriteHeader(6, value); }

	/// <summary>Header offset 8 — the training mission number, 0 for anything that is not one.</summary>
	public short TrainingMissionNumber { get => ReadHeader(8); set => WriteHeader(8, value); }

	/// <summary>Header offset 10 — unlimited ammunition and energy when exactly 1.</summary>
	public short UnlimitedAmmunition { get => ReadHeader(10); set => WriteHeader(10, value); }

	/// <summary>Header offset 12 — player invulnerable when exactly 1.</summary>
	public short PlayerInvulnerable { get => ReadHeader(12); set => WriteHeader(12, value); }

	/// <summary>Header offset 14 — the mission difficulty, 0-3.</summary>
	public short Difficulty { get => ReadHeader(14); set => WriteHeader(14, value); }

	/// <summary>Header offset 18 — the theater variant, 0 day or 1 night.</summary>
	public short TheaterVariant { get => ReadHeader(18); set => WriteHeader(18, value); }

	private short ReadHeader(int offset) =>
		HeaderBytes.Length >= offset + 2 ? BitConverter.ToInt16(HeaderBytes, offset) : (short)0;

	private void WriteHeader(int offset, short value) {
		if (HeaderBytes.Length < offset + 2) {
			throw new InvalidOperationException("This script.dat's header is too short to hold that field.");
		}

		BitConverter.GetBytes(value).CopyTo(HeaderBytes, offset);
	}

	/// <summary>Block 1 — row #6 (<see cref="MapPoint22"/>) export: X/Y/Z world positions only.</summary>
	public ScriptCoordinate[] Coordinates { get; set; } = [];

	/// <summary>Block 2 — row #7 (<see cref="Heading10"/>) export: headings in degrees, which DBSIM multiplies by 182 to reach BAM.</summary>
	public ScriptHeading[] Headings { get; set; } = [];

	/// <summary>Block 3 — row #8 (<see cref="WaypointGroup"/>) export: the waypoint ref list only (no GUID/condition).</summary>
	public ScriptWaypointGroup[] WaypointGroups { get; set; } = [];

	/// <summary>Block 4 — row #9 (<see cref="TriggerArea12"/>) export: trigger areas.</summary>
	public ScriptTriggerArea[] TriggerAreas { get; set; } = [];

	/// <summary>Block 5 — row #10 (<see cref="MissionAction82"/>) export: mission actions.</summary>
	public ScriptAction[] Actions { get; set; } = [];

	/// <summary>Block 6 — row #11 (<see cref="ActionTimer30"/>) export: mission timers.</summary>
	public ScriptActionTimer[] ActionTimers { get; set; } = [];

	/// <summary>
	/// Block 7 — row #12 (<see cref="MechRosterEntry144"/>) export, 134 bytes/record: <b>the mech
	/// roster</b>. DBSIM builds one live mech per record a block-11 group activates.
	/// </summary>
	public ScriptMechRecord[] Mechs { get; set; } = [];

	/// <summary>
	/// Block 8 — row #13 (<see cref="FlyerRosterEntry102"/>) export, 92 bytes/record: <b>the flyer
	/// roster</b>, the same arrangement as <see cref="Mechs"/>.
	/// </summary>
	public ScriptFlyerRecord[] Flyers { get; set; } = [];

	/// <summary>
	/// Block 9 — row #14 (<see cref="BaseRosterEntry62"/>) export, 52 bytes/record: <b>the base
	/// roster</b> — structures, turrets and the rest of the static furniture.
	/// </summary>
	public ScriptBaseRecord[] Bases { get; set; } = [];

	/// <summary>
	/// Block 10 — row #15 (<see cref="MissionOrder22"/>) export, 14 bytes/record: <b>mission-group
	/// orders</b>. A group works through the ones its block-11 record names, and a group with no
	/// spawn point of its own starts at the first waypoint of its first order's
	/// <see cref="ScriptOrder.RouteRef"/>.
	/// </summary>
	public ScriptOrder[] Orders { get; set; } = [];

	/// <summary>
	/// Block 11 — row #16 (<see cref="MissionGroup164"/>) export, 156 bytes/record: <b>the groups</b>,
	/// and the reason anything is anywhere. Each record past the first activates roster slots —
	/// <see cref="ScriptGroup.MemberKind"/> picks the roster, <see cref="ScriptGroup.MemberRefs"/>
	/// the slots — and carries the spawn point, heading, formation and orders its members take.
	/// <b>Record 0 is special</b>: it activates nothing and holds the player squad's spawn point,
	/// whose members DBSIM fills from <see cref="Sav.MecFile"/>.
	/// </summary>
	public ScriptGroup[] Groups { get; set; } = [];

	/// <summary>
	/// Block 12 — row #17 (<see cref="MissionObjective58"/>) export, 54 bytes/record, unfiltered (row
	/// #17 has no GUID) — <b>the mission's objectives</b>, the conditions the simulation tests.
	/// </summary>
	public ScriptObjective[] Objectives { get; set; } = [];

	/// <summary>
	/// Block 13 — the flat tail: <b>the mission's objective list as the player is shown it</b>, one
	/// index per line into <c>data\mission.str</c>. <c>DBSim_LoadScriptDat</c> (<c>00424308</c>)
	/// reads the count into <c>DAT_004a9ec8</c> and the shorts into <c>DAT_004a9ecc</c>, and the
	/// in-mission objectives panel (<c>obj_alrt</c>, <c>ObjectivesPanel_Ctor</c> (<c>0045751c</c>)) is the only reader: it
	/// prints one label per entry.
	///
	/// <para><b>It is separate data from block 12</b>, which holds the conditions the simulation
	/// actually tests. Nothing reconciles the two, so a mission can list an objective it does not
	/// test and test one it does not list.</para>
	/// </summary>
	public short[] ObjectiveTextRefs { get; set; } = [];
}

/// <summary>Block 1 entry — 12 bytes (int32 X/Y/Z), a positions-only export of row #6.</summary>
public class ScriptCoordinate {
	public int X { get; set; }
	public int Y { get; set; }
	public int Z { get; set; }
}

/// <summary>Block 2 entry — 2 bytes, row #7's heading in degrees.</summary>
public class ScriptHeading {
	public short Value { get; set; }
}

/// <summary>Block 3 entry — a count, then that many waypoints as block-1 indices.</summary>
public class ScriptWaypointGroup {
	public short[] Waypoints { get; set; } = [];
}

/// <summary>Block 4 entry — 6 bytes, a trigger area; the fields are <see cref="TriggerArea12"/>'s.</summary>
public class ScriptTriggerArea {
	/// <inheritdoc cref="TriggerArea12.Shape"/>
	public short Shape { get; set; }

	/// <summary>Index into <see cref="ScriptDat.Coordinates"/>: the box's first corner or the circle's centre.</summary>
	public short PointRef { get; set; }

	/// <inheritdoc cref="TriggerArea12.SecondPointOrRadius"/>
	public short SecondPointOrRadius { get; set; }
}

/// <summary>
/// Block 5 entry — 74 bytes, row #10 (<see cref="MissionAction82"/>) less its GUID, condition, 0x04
/// and 0x1A, with its counter pairs split into <see cref="CounterRefs"/> then <see cref="CounterOps"/>.
/// What DBSIM keeps of it is docs/formats/script-dat.md#block-5-in-memory--58-bytes-0x3a.
/// </summary>
public class ScriptAction {
	/// <inheritdoc cref="MissionAction82.Type"/>
	public short Type { get; set; }

	/// <inheritdoc cref="MissionAction82.Verb"/>
	public short Verb { get; set; }

	/// <summary>Indices into <see cref="ScriptDat.TriggerAreas"/>, <c>-1</c> for none.</summary>
	public short[] AreaRefs { get; set; } = new short[8];

	/// <summary>The mission counters the action writes when it activates, <c>-1</c> for an unused slot.</summary>
	public short[] CounterRefs { get; set; } = new short[10];

	/// <summary>The operation for each of <see cref="CounterRefs"/>: 6 increments the counter, 5 clears it.</summary>
	public short[] CounterOps { get; set; } = new short[10];

	/// <summary><c>data\mission.str</c> line indices; DBSIM reads and drops them.</summary>
	public short[] TextRefs { get; set; } = new short[5];

	/// <inheritdoc cref="MissionAction82.MessageId"/>
	public short MessageId { get; set; }

	/// <inheritdoc cref="MissionAction82.Target"/>
	public short Target { get; set; }
}

/// <summary>Block 6 entry — 24 bytes, row #11 (<see cref="ActionTimer30"/>) less its GUID, condition and 0x04.</summary>
public class ScriptActionTimer {
	/// <summary>Index into <see cref="ScriptDat.Actions"/>: the action that arms the timer, or <c>-1</c> to run from mission start.</summary>
	public short PrimaryActionRef { get; set; }

	/// <inheritdoc cref="ActionTimer30.Delay"/>
	public short Delay { get; set; }

	/// <summary>Indices into <see cref="ScriptDat.Actions"/>: fired when the delay runs out.</summary>
	public short[] SequenceRefs { get; set; } = new short[10];
}

/// <summary>
/// Block 7 entry — 134 bytes, row #12 (<see cref="MechRosterEntry144"/>) less its GUID, condition,
/// variant key, compound-condition partner and pair count. <see cref="HeadBytes"/> is source
/// 0x08-0x2F and <see cref="TailBytes"/> source 0x4C-0x8F with the counter pairs split; both are kept
/// raw for a byte-exact round trip, and the named properties are views over them.
///
/// <para><c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>) builds one mech per live slot from
/// this record. <c>DBSim_LoadScriptDat</c>'s first pass keeps only <see cref="TypeIndex"/>, to count
/// and allocate (docs/formats/script-dat.md#the-two-pass-read--and-what-it-means-for-dbsim-keeps).</para>
/// </summary>
public class ScriptMechRecord {
	public byte[] HeadBytes { get; set; } = new byte[40];

	/// <summary>
	/// Source offset 0x08, the first field of <see cref="HeadBytes"/> — the machine's standing AI
	/// radar setting. <c>DBSim_SpawnMissionObjects</c> copies it to <c>mech+0x97</c>, which is the
	/// PASSIVE/ACTIVE an AI machine walks its route on. 0/1 in every retail record.
	/// </summary>
	public short AiRadarActive { get => ReadHead(0); set => ScriptActionRefs.Write(HeadBytes, 0, value); }

	/// <summary>
	/// Source offset 0x0a — the speed the machine's AI walks at, copied to <c>mech+0x252</c>. Zero,
	/// which is 91% of retail records, means the AI's own default. See
	/// <c>docs/simulation/ai-navigation.md</c>.
	/// </summary>
	public short AiCruiseSpeed { get => ReadHead(2); set => ScriptActionRefs.Write(HeadBytes, 2, value); }

	private short ReadHead(int offset) =>
		HeadBytes.Length >= offset + 2 ? BitConverter.ToInt16(HeadBytes, offset) : (short)0;

	/// <summary>Source offset 0x30 — the mech type, an index into <c>nam\MECHS.NAM</c>'s name list.</summary>
	public short TypeIndex { get; set; }

	/// <summary>
	/// Source offsets 0x32-0x45 — the mech's weapon fit, passed straight to DBSIM's
	/// <c>Mech_ConfigureLoadout</c> alongside <see cref="WeaponSecondary"/>. Unused slots are <c>-1</c>.
	/// </summary>
	public short[] WeaponRefs { get; set; } = new short[10];

	/// <summary>
	/// Source offset 0x46 — index into <see cref="ScriptDat.Coordinates"/>, or <c>-1</c>, in which
	/// case the mech takes its spawn point from the block-11 group that activates it (see
	/// <see cref="ScriptGroup.PositionRef"/>).
	/// </summary>
	public short PositionRef { get; set; }

	/// <summary>Source offset 0x48 — index into <see cref="ScriptDat.Headings"/>, or <c>-1</c>.</summary>
	public short HeadingRef { get; set; }

	public byte[] TailBytes { get; set; } = new byte[68];

	/// <summary>
	/// Exported offset <c>0x42</c> — the machine's ten mission-counter refs, <c>-1</c> for an unused
	/// slot. <c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>) copies them to <c>mech+0x1ba</c>
	/// through <c>SimObject_SetOutOfActionCounters</c> (<c>00411b90</c>), and they are written when
	/// the machine goes out of the fight. See
	/// docs/simulation/mission-deployment.md#the-out-of-action-report.
	/// </summary>
	public short[] CounterRefs => ScriptActionRefs.ReadSlots(TailBytes, ScriptActionRefs.CounterRefs);

	/// <summary>Exported offset <c>0x56</c> — the operation for each of <see cref="CounterRefs"/>' counters.</summary>
	public short[] CounterOps => ScriptActionRefs.ReadSlots(TailBytes, ScriptActionRefs.CounterOps);

	/// <summary>
	/// Source offset 0x74 — the second of the two parallel per-slot arrays
	/// <c>Mech_ConfigureLoadout</c> takes, alongside <see cref="WeaponRefs"/>. It is the ammunition
	/// type each missile launcher is loaded with, the value a launcher's mount resolves through
	/// <c>Proj_LookupRecord(Missile, key)</c> and then prints as its name; non-launcher slots carry a
	/// filler 5.
	///
	/// <para>Located by the two stack locals <c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>) hands
	/// the loadout call, which sit exactly 64 bytes apart in a frame holding one record — placing the
	/// second array 64 bytes past <see cref="WeaponRefs"/> in the exported record, source 0x74 since the
	/// writer drops 0x4a between them; VSHELL's squad build reads it there too. Confirmed against the retail
	/// mission: every slot whose <see cref="WeaponRefs"/> entry is a launcher (<c>MSL10</c>, id 15)
	/// reads 1 here and every other slot reads 5.</para>
	///
	/// <para>A view over <see cref="TailBytes"/> rather than a field of its own, so the record still
	/// round-trips byte-exact through <see cref="Io.Transform.Common.ScriptDatTransformer"/>.</para>
	/// </summary>
	public short[] WeaponSecondary => HasWeaponSecondary
		? Enumerable.Range(0, SlotCount)
			.Select(i => BitConverter.ToInt16(TailBytes, SecondaryOffset + i * 2))
			.ToArray()
		: [];

	/// <summary>
	/// Whether <see cref="TailBytes"/> is long enough to hold the second array — false only for a
	/// record built with a short tail rather than parsed from a real file.
	/// </summary>
	public bool HasWeaponSecondary => TailBytes.Length >= SecondaryOffset + SlotCount * 2;

	/// <summary>
	/// Writes one slot of <see cref="WeaponSecondary"/> back into <see cref="TailBytes"/>.
	/// <see cref="WeaponSecondary"/> hands back a copy, so assigning into what it returns changes
	/// nothing — this is the only way to edit a slot's ammunition type.
	/// </summary>
	public void SetWeaponSecondary(int slot, short value) {
		if (slot < 0 || slot >= SlotCount) {
			throw new ArgumentOutOfRangeException(nameof(slot), slot, $"A loadout has {SlotCount} slots.");
		}

		if (!HasWeaponSecondary) {
			throw new InvalidOperationException("This record's tail is too short to hold the ammunition array.");
		}

		BitConverter.GetBytes(value).CopyTo(TailBytes, SecondaryOffset + slot * 2);
	}

	/// <summary>
	/// Exported offset <c>0x80</c> — the mission action this machine fires when it is <b>engaged</b>:
	/// <c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>) resolves it into <c>mech+0x1b2</c>, and
	/// <c>Detection_Sweep</c> (<c>004128f8</c>) fires it once a hostile that already has contact on
	/// this machine closes to 50,000 units. <c>-1</c> for a record that names none.
	/// </summary>
	public short EngagementActionRef {
		get => ReadTail(EngagementActionOffset);
		set => ScriptActionRefs.Write(TailBytes, EngagementActionOffset, value);
	}

	/// <summary>
	/// Exported offset <c>0x82</c> — the mission action this machine fires when it is
	/// <b>defeated</b>, resolved into <c>mech+0x1b6</c>. A machine fires it on death
	/// (<c>Mech_ComponentDamageWrite</c>, <c>00417de4</c>) and again on running out of working
	/// weapons; it is not a death action alone.
	///
	/// <para><b>This is how a retail mission chains its reinforcements.</b> The shipped
	/// <c>script.dat</c> has five of its ten mech records naming one, which is what brings each wave
	/// in as the last is beaten — see docs/simulation/mission-deployment.md.</para>
	/// </summary>
	public short DefeatActionRef {
		get => ReadTail(DefeatActionOffset);
		set => ScriptActionRefs.Write(TailBytes, DefeatActionOffset, value);
	}

	/// <summary>
	/// Exported offset <c>0x84</c> — the machine's <b>starting condition, as a percentage</b>. 100 is
	/// pristine; anything under 80 has <c>DBSim_SpawnMissionObjects</c> pre-damage the machine
	/// through <c>Mech_ApplyStartingCondition</c> (<c>004178e8</c>) before it ever takes a shot, in
	/// four widening bands at 80 / 60 / 40 / 20. Below 20 the machine is placed as a <b>wreck</b>:
	/// a leg destroyed outright, immobilised and collapsed where it stands. See
	/// docs/simulation/component-damage.md#starting-condition--mech_applystartingcondition-004178e8.
	/// </summary>
	public short StartingCondition {
		get => ReadTail(StartingConditionOffset);
		set => ScriptActionRefs.Write(TailBytes, StartingConditionOffset, value);
	}

	private short ReadTail(int offset) =>
		TailBytes.Length >= offset + 2 ? BitConverter.ToInt16(TailBytes, offset) : (short)-1;

	/// <summary>Where <see cref="EngagementActionRef"/> sits in <see cref="TailBytes"/> (0x80 less 0x42).</summary>
	private const int EngagementActionOffset = 62;

	/// <summary>And <see cref="DefeatActionRef"/> (0x82 less 0x42).</summary>
	private const int DefeatActionOffset = 64;

	/// <summary>And <see cref="StartingCondition"/> (0x84 less 0x42).</summary>
	private const int StartingConditionOffset = 66;

	/// <summary>Where <see cref="WeaponSecondary"/> starts inside <see cref="TailBytes"/> (source 0x74 less 0x4c).</summary>
	private const int SecondaryOffset = 40;

	/// <summary>Slots in both loadout arrays.</summary>
	private const int SlotCount = 10;
}

/// <summary>
/// Block 8 entry — 92 bytes, row #13 (<see cref="FlyerRosterEntry102"/>) less its GUID, condition,
/// variant key, 0x06 and pair count. <see cref="HeadBytes"/> is source 0x08-0x2F (the flag span) and
/// <see cref="TailBytes"/> source 0x38-0x65 with the counter pairs split.
///
/// <para>DBSIM's world-spawn pass (<c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>)) builds one flyer/vehicle per live slot from
/// this record, taking its type from <see cref="TypeIndex"/> and its placement from the two refs
/// below.</para>
/// </summary>
public class ScriptFlyerRecord {
	public byte[] HeadBytes { get; set; } = new byte[40];

	/// <summary>Source offset 0x30 — index into <see cref="ScriptDat.Coordinates"/>, or <c>-1</c>.</summary>
	public short PositionRef { get; set; }

	/// <summary>Source offset 0x32 — index into <see cref="ScriptDat.Headings"/>, or <c>-1</c>.</summary>
	public short HeadingRef { get; set; }

	/// <summary>Source offset 0x34 — the flyer type, an index into <c>nam\FLYERS.NAM</c>'s name list.</summary>
	public short TypeIndex { get; set; }

	public byte[] TailBytes { get; set; } = new byte[46];

	/// <inheritdoc cref="ScriptMechRecord.CounterRefs" />
	/// <remarks>Exported offset <c>0x2e</c>.</remarks>
	public short[] CounterRefs => ScriptActionRefs.ReadSlots(TailBytes, ScriptActionRefs.CounterRefs);

	/// <inheritdoc cref="ScriptMechRecord.CounterOps" />
	/// <remarks>Exported offset <c>0x42</c>.</remarks>
	public short[] CounterOps => ScriptActionRefs.ReadSlots(TailBytes, ScriptActionRefs.CounterOps);

	/// <inheritdoc cref="ScriptMechRecord.EngagementActionRef" />
	/// <remarks>Exported offset <c>0x56</c>; the flyer's own <c>+0x1b2</c>.</remarks>
	public short EngagementActionRef {
		get => ScriptActionRefs.Read(TailBytes, ScriptActionRefs.SmallEngagement);
		set => ScriptActionRefs.Write(TailBytes, ScriptActionRefs.SmallEngagement, value);
	}

	/// <inheritdoc cref="ScriptMechRecord.DefeatActionRef" />
	/// <remarks>
	/// Exported offset <c>0x58</c>; the flyer's own <c>+0x1b6</c>, fired by
	/// <c>Flyer_ComponentDamageWrite</c> (<c>00421bb4</c>).
	/// </remarks>
	public short DefeatActionRef {
		get => ScriptActionRefs.Read(TailBytes, ScriptActionRefs.SmallDestruction);
		set => ScriptActionRefs.Write(TailBytes, ScriptActionRefs.SmallDestruction, value);
	}
}

/// <summary>
/// Where the two per-object mission-action refs sit inside a flyer's or a structure's exported
/// tail. Both records place them at the same distance into <see cref="ScriptFlyerRecord.TailBytes"/>/
/// <see cref="ScriptBaseRecord.TailBytes"/>, four bytes before the trailing field; a mech's
/// tail starts further back and carries its own offsets.
/// </summary>
internal static class ScriptActionRefs {
	/// <summary>The engaged-action ref's index into either tail.</summary>
	public const int SmallEngagement = 40;

	/// <summary>And the destroyed-action ref's.</summary>
	public const int SmallDestruction = 42;

	/// <summary>
	/// Where the ten mission-counter refs start in any of the three roster tails. Every tail begins
	/// with them, and the ten operations follow at <see cref="CounterOps"/>.
	/// </summary>
	public const int CounterRefs = 0;

	/// <inheritdoc cref="CounterRefs"/>
	public const int CounterOps = 20;

	/// <summary>Mission-counter slots per record.</summary>
	private const int CounterSlots = 10;

	/// <summary>Reads one, answering <c>-1</c> for a tail too short to hold it.</summary>
	public static short Read(byte[] tail, int offset) =>
		tail.Length >= offset + 2 ? BitConverter.ToInt16(tail, offset) : (short)-1;

	/// <summary>
	/// Reads ten shorts starting at <paramref name="offset"/>, answering an empty array for a tail too
	/// short to hold them.
	/// </summary>
	public static short[] ReadSlots(byte[] tail, int offset) {
		if (tail.Length < offset + CounterSlots * 2) {
			return [];
		}

		var slots = new short[CounterSlots];
		for (int i = 0; i < slots.Length; i++) {
			slots[i] = BitConverter.ToInt16(tail, offset + i * 2);
		}

		return slots;
	}

	/// <summary>
	/// Writes a short into a record's raw span — how the named views over <c>HeadBytes</c>/
	/// <c>TailBytes</c> are edited without giving up the byte-exact round trip.
	/// </summary>
	public static void Write(byte[] bytes, int offset, short value) {
		if (bytes.Length < offset + 2) {
			throw new InvalidOperationException("This record's raw span is too short to hold that field.");
		}

		BitConverter.GetBytes(value).CopyTo(bytes, offset);
	}
}

/// <summary>
/// Block 9 entry — 52 bytes, row #14 (<see cref="BaseRosterEntry62"/>) less its GUID, condition,
/// variant key, 0x06 and pair count. <see cref="TailBytes"/> is source 0x10-0x3D with the counter
/// pairs split.
///
/// <para>DBSIM's world-spawn pass (<c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>)) builds one base/structure per live slot
/// from this record.</para>
/// </summary>
public class ScriptBaseRecord {
	/// <summary>
	/// Source offset 0x08 — the base type, an index into the 65-entry table in
	/// <c>dat\BASES.DAT</c> (which in turn names the model and its texture bank).
	/// </summary>
	public short TypeIndex { get; set; }

	/// <summary>Source offset 0x0A — index into <see cref="ScriptDat.Coordinates"/>, or <c>-1</c>.</summary>
	public short PositionRef { get; set; }

	/// <summary>Source offset 0x0C — index into <see cref="ScriptDat.Headings"/>, or <c>-1</c>.</summary>
	public short HeadingRef { get; set; }

	public byte[] TailBytes { get; set; } = new byte[46];

	/// <inheritdoc cref="ScriptMechRecord.CounterRefs" />
	/// <remarks>Exported offset <c>0x06</c>.</remarks>
	public short[] CounterRefs => ScriptActionRefs.ReadSlots(TailBytes, ScriptActionRefs.CounterRefs);

	/// <inheritdoc cref="ScriptMechRecord.CounterOps" />
	/// <remarks>Exported offset <c>0x1a</c>.</remarks>
	public short[] CounterOps => ScriptActionRefs.ReadSlots(TailBytes, ScriptActionRefs.CounterOps);

	/// <inheritdoc cref="ScriptMechRecord.EngagementActionRef" />
	/// <remarks>Exported offset <c>0x2e</c>; the structure's own <c>+0x1b2</c>.</remarks>
	public short EngagementActionRef {
		get => ScriptActionRefs.Read(TailBytes, ScriptActionRefs.SmallEngagement);
		set => ScriptActionRefs.Write(TailBytes, ScriptActionRefs.SmallEngagement, value);
	}

	/// <inheritdoc cref="ScriptMechRecord.DefeatActionRef" />
	/// <remarks>
	/// Exported offset <c>0x30</c>; the structure's own <c>+0x1b6</c>, fired by
	/// <c>Base_ApplyDamage</c> (<c>00404d70</c>) when the last component goes.
	/// </remarks>
	public short DefeatActionRef {
		get => ScriptActionRefs.Read(TailBytes, ScriptActionRefs.SmallDestruction);
		set => ScriptActionRefs.Write(TailBytes, ScriptActionRefs.SmallDestruction, value);
	}
}

/// <summary>
/// Block 10 entry — 14 bytes, row #15 (<see cref="MissionOrder22"/>)'s 0x08-0x15: <b>one
/// mission-group order</b>, refs as block indices. See docs/formats/script-dat.md#block-10-in-memory--22-bytes-0x16
/// and docs/simulation/ai-goals.md.
/// </summary>
public class ScriptOrder {
	/// <inheritdoc cref="MissionOrder22.Verb"/>
	public short Verb { get; set; }

	/// <inheritdoc cref="MissionOrder22.FormationId"/>
	public short FormationId { get; set; }

	/// <summary>Index into <see cref="ScriptDat.Coordinates"/>; DBSIM resolves it and never reads it.</summary>
	public short PointRef { get; set; }

	/// <summary>Index into <see cref="ScriptDat.WaypointGroups"/>: the route.</summary>
	public short RouteRef { get; set; }

	/// <inheritdoc cref="MissionOrder22.SubjectKind"/>
	public short SubjectKind { get; set; }

	/// <summary>The subject, as a block-11, -7, -8 or -9 index per <see cref="SubjectKind"/>.</summary>
	public short SubjectRef { get; set; }

	/// <summary>Index into <see cref="ScriptDat.Actions"/>: when it fires, the group moves to its next order.</summary>
	public short ActionRef { get; set; }
}

/// <summary>
/// Block 11 entry — 156 bytes, row #16 (<see cref="MissionGroup164"/>) less its GUID, condition,
/// compound-condition partner and pair count, with its counter pairs split into
/// <see cref="CounterRefs"/> then <see cref="CounterOps"/>. The fields are
/// <see cref="MissionGroup164"/>'s, refs as block indices.
/// </summary>
public class ScriptGroup {
	/// <inheritdoc cref="MissionGroup164.PaintsGround"/>
	public short PaintsGround { get; set; }

	/// <inheritdoc cref="MissionGroup164.NearConstant"/>
	public short NearConstant { get; set; }

	/// <inheritdoc cref="MissionGroup164.DeadZone"/>
	public short[] DeadZone { get; set; } = new short[18];

	/// <summary>Which roster <see cref="MemberRefs"/> indexes: 0 <see cref="ScriptDat.Mechs"/>, 1 <see cref="ScriptDat.Flyers"/>, 2 <see cref="ScriptDat.Bases"/>.</summary>
	public short MemberKind { get; set; }

	/// <inheritdoc cref="MissionGroup164.FormationId"/>
	public short FormationId { get; set; }

	/// <summary>Index into <see cref="ScriptDat.Coordinates"/>: the group's spawn point, or <c>-1</c> for its first order's route's first waypoint.</summary>
	public short PositionRef { get; set; }

	/// <summary>Index into <see cref="ScriptDat.Headings"/>, or <c>-1</c> for the bearing of the route's first leg.</summary>
	public short HeadingRef { get; set; }

	/// <summary>Index into <see cref="ScriptDat.WaypointGroups"/>; the group's own route.</summary>
	public short RouteRef { get; set; }

	/// <inheritdoc cref="MissionGroup164.MemberRefs"/>
	public short[] MemberRefs { get; set; } = new short[20];

	/// <summary>Indices into <see cref="ScriptDat.Orders"/>: the group's orders, in slot order.</summary>
	public short[] OrderRefs { get; set; } = new short[10];

	/// <inheritdoc cref="MissionGroup164.Side"/>
	public short Side { get; set; }

	/// <inheritdoc cref="MissionGroup164.DeploymentActionRef"/>
	public short DeploymentActionRef { get; set; }

	/// <summary>
	/// Exported offset <c>0x72</c> — the group's ten mission-counter refs, <c>-1</c> for an unused
	/// slot. <c>DBSim_BuildGroupRecord</c> (<c>00423b34</c>) copies them to <c>group+0x1c</c>; they
	/// are written when every member of the group is out of the fight. See
	/// docs/simulation/mission-deployment.md#the-out-of-action-report.
	/// </summary>
	public short[] CounterRefs { get; set; } = new short[10];

	/// <summary>
	/// Exported offset <c>0x86</c> — the operation for each of <see cref="CounterRefs"/>' counters,
	/// copied to <c>group+0x30</c>.
	/// </summary>
	public short[] CounterOps { get; set; } = new short[10];

	/// <inheritdoc cref="MissionGroup164.MapShown"/>
	public short MapShown { get; set; }
}

/// <summary>
/// Block 12 entry — 54 bytes, row #17 (<see cref="MissionObjective58"/>) less its condition and pair
/// count, with its counter pairs split into <see cref="CounterRefs"/> then <see cref="CounterOps"/>.
/// What DBSIM builds from it is docs/formats/script-dat.md#block-12-in-memory--76-bytes-0x4c.
/// </summary>
public class ScriptObjective {
	/// <inheritdoc cref="MissionObjective58.Required"/>
	public short Required { get; set; }

	/// <inheritdoc cref="MissionObjective58.ConditionCode"/>
	public short ConditionCode { get; set; }

	/// <inheritdoc cref="MissionObjective58.SubjectKind"/>
	public short SubjectKind { get; set; }

	/// <summary>The subject, as a block-11, -7, -8 or -9 index per <see cref="SubjectKind"/>.</summary>
	public short SubjectRef { get; set; }

	/// <summary>Index into <see cref="ScriptDat.Coordinates"/>; no condition reads it.</summary>
	public short PointRef { get; set; }

	/// <summary>Index into <see cref="ScriptDat.WaypointGroups"/>: the waypoint group condition 0 asks about.</summary>
	public short RouteRef { get; set; }

	/// <summary>The failure text, a <c>data\mission.str</c> line index: the first of three consecutive lines.</summary>
	public short TextRef { get; set; }

	/// <summary>The mission counters the objective writes, <c>-1</c> for an unused slot.</summary>
	public short[] CounterRefs { get; set; } = new short[10];

	/// <summary>The operation for each of <see cref="CounterRefs"/>.</summary>
	public short[] CounterOps { get; set; } = new short[10];
}
