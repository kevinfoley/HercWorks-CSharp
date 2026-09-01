namespace HercWorks.Core.Data.File.Msn.Script;

/// <summary>
/// <c>data\script.dat</c> — DBSIM's real gameplay handoff format, written by VSHELL immediately
/// after it parses a <see cref="MissionFile"/> (`.msn`) and read independently by both DBSIM (the
/// actual simulator) and VSHELL's own map-editor UI (`ShellMap`). Every block below is a
/// GUID-filtered, field-subset re-export of one of <see cref="MissionFile"/>'s already-decoded
/// rows — this is not an independently-authored format. See
/// docs/formats/script-dat.md for the full byte-exact writeup (verified against three independently
/// compiled readers plus 10 real sample files).
///
/// <b>DBSIM reads this file twice.</b> <c>DBSim_LoadScriptDat</c> counts live objects and sizes its
/// pools, keeping little more than each roster record's type field; <c>DBSim_SpawnMissionObjects</c>
/// then re-opens the file and walks blocks 7-13 again to actually build the world, and that is the
/// pass that reads positions, headings and loadouts. Judging a block by what the first pass keeps
/// gives the wrong answer for most of them — see the format doc's "The two-pass read".
///
/// The file is a fixed 13,520-byte preallocated buffer in every real sample seen — real content
/// only fills a prefix of it, and any bytes beyond the last block's declared end are stale leftover
/// data from an earlier, larger write (confirmed byte-identical across samples), not part of the
/// format. This model only round-trips the meaningful prefix; nothing here assumes or preserves a
/// fixed total file length.
/// </summary>
public class ScriptDat {
	/// <summary>
	/// Fixed 20-byte header, 10 little-endian shorts. Offset 0 is the theater index, offset 2 the
	/// zone id passed to <c>Terrain_LoadZone</c>, and offset 18 the variant — decoded in
	/// docs/formats/script-dat.md and modelled by <c>Herculan.Engine.World.ScriptDatHeader</c>.
	/// Round-tripped raw here rather than split into named fields.
	/// </summary>
	public byte[] HeaderBytes { get; set; } = new byte[20];

	/// <summary>Block 1 — row #6 (<see cref="MapPoint22"/>) export: X/Y/Z world positions only.</summary>
	public ScriptCoordinate[] Coordinates { get; set; } = [];

	/// <summary>
	/// Block 2 — row #7 (<see cref="Heading10"/>) export: the payload field only. DBSIM multiplies
	/// this by 182 (the confirmed degrees-&gt;BAM constant) at load time, reframing it as a heading
	/// in degrees rather than a generic discrete flag — that transform is DBSIM-side, not part of
	/// the on-disk value stored here.
	/// </summary>
	public ScriptHeading[] Headings { get; set; } = [];

	/// <summary>Block 3 — row #8 (<see cref="WaypointGroup"/>) export: the waypoint ref list only (no GUID/condition).</summary>
	public ScriptWaypointGroup[] WaypointGroups { get; set; } = [];

	/// <summary>Block 4 — row #9 (<see cref="LinkOrReward12"/>) export: type flag + both refs/literal.</summary>
	public ScriptLinkOrReward[] LinksOrRewards { get; set; } = [];

	/// <summary>Block 5 — row #10 (<see cref="Action82"/>) export: every field except GUID/condition/Unk04.</summary>
	public ScriptAction[] Actions { get; set; } = [];

	/// <summary>Block 6 — row #11 (<see cref="ActionPair30"/>) export: the resolved target/type/refs only.</summary>
	public ScriptActionPair[] ActionPairs { get; set; } = [];

	/// <summary>
	/// Block 7 — row #12 (<see cref="EntityTemplate144"/>) export, 134 bytes/record: <b>the mech
	/// roster</b>. One record per mech the mission can field, carrying its type, weapon fit and
	/// (usually unset) placement. DBSIM builds one live mech per record its block-11 activation
	/// marks; VSHELL's own `ShellMap` reader keeps the whole record for UI display.
	/// </summary>
	public ScriptSpawnRecordExport[] SpawnRecords { get; set; } = [];

	/// <summary>
	/// Block 8 — row #13 (<see cref="UnkEntity102Bytes"/>) export, 92 bytes/record: <b>the
	/// flyer/vehicle roster</b>, the same arrangement as <see cref="SpawnRecords"/> one class down.
	/// </summary>
	public ScriptEntity102Export[] Entities102 { get; set; } = [];

	/// <summary>
	/// Block 9 — row #14 (<see cref="MiscEntityInfo"/>) export, 52 bytes/record: <b>the base
	/// roster</b> — structures, turrets and the rest of the static furniture.
	/// </summary>
	public ScriptMiscEntityExport[] MiscEntities { get; set; } = [];

	/// <summary>
	/// Block 10 — row #15 (<see cref="LinkedRef22"/>) export, 14 bytes/record: <b>route links</b>.
	/// DBSIM's first pass discards these, but its spawn pass resolves them — a group with no spawn
	/// point of its own reaches its route through here, and
	/// <see cref="ScriptLinkedRef22Export.RefRow8"/> names the waypoint group whose first waypoint it
	/// starts at. VSHELL's `ShellMap` reader keeps the block in full for the same "what's this linked
	/// to" reason.
	/// </summary>
	public ScriptLinkedRef22Export[] LinkedRefs22 { get; set; } = [];

	/// <summary>
	/// Block 11 — row #16 (<see cref="EntitySpawn164"/>) export, 156 bytes/record: <b>the groups</b>,
	/// and the reason anything is anywhere. Each record past the first activates roster slots — the
	/// discriminator picks which roster, the ref array picks the slots — and carries the spawn point,
	/// heading, formation and route its members take. <b>Record 0 is special</b>: it activates
	/// nothing and exists only to hold the player squad's spawn point, which DBSIM fills from
	/// <see cref="Sav.MecFile"/>.
	///
	/// <para><see cref="ScriptEntity164Export.ArrayA"/>/
	/// <see cref="ScriptEntity164Export.ArrayB"/> together are the on-disk interleaving of the
	/// source record's Payload1-4 + DeadZone2 span (even-offset entries in A, odd-offset in B) —
	/// re-derived here from the writer's exact read order, not the `.msn` in-memory field order.
	/// <see cref="ScriptEntity164Export.TrailingDiscriminator"/> (0x78 in the source row) is not
	/// exported at all.</para>
	/// </summary>
	public ScriptEntity164Export[] Entities164 { get; set; } = [];

	/// <summary>
	/// Block 12 — row #17 (<see cref="UnitSpawn58"/>) export, 54 bytes/record, unfiltered (row #17
	/// has no GUID to filter on). DBSIM reads and fully discards this block. <see cref="ScriptUnitSpawn58Export.PairRefs"/>/
	/// <see cref="ScriptUnitSpawn58Export.PairTags"/> are the source record's <c>Pairs[10]</c> array
	/// re-exported as parallel arrays (all 10 refs, then all 10 tags) rather than interleaved pairs —
	/// the writer's own on-disk order. <c>ConditionRef</c> (0x00) and <c>PairCount</c> (0x10) are not exported.
	/// </summary>
	public ScriptUnitSpawn58Export[] LinkedRefs58 { get; set; } = [];

	/// <summary>
	/// Block 13 — the mission's herc/weapon unlock package: the populated prefix of row #4's
	/// (<see cref="RewardPackage144"/>) <c>LutRefsA</c> sub-array, re-counted from scratch by the
	/// writer (assumes real entries are always front-packed with no gaps, matching
	/// <see cref="MissionFile"/>'s own real-data findings for that field).
	/// </summary>
	public short[] UnlockedLutRefs { get; set; } = [];
}

/// <summary>Block 1 entry — 12 bytes (int32 X/Y/Z), a positions-only export of row #6.</summary>
public class ScriptCoordinate {
	public int X { get; set; }
	public int Y { get; set; }
	public int Z { get; set; }
}

/// <summary>Block 2 entry — 2 bytes, row #7's payload field verbatim (pre-degrees-&gt;BAM conversion).</summary>
public class ScriptHeading {
	public short Value { get; set; }
}

/// <summary>Block 3 entry — variable length, row #8's resolved waypoint ref list (no GUID/condition).</summary>
public class ScriptWaypointGroup {
	public short[] Waypoints { get; set; } = [];
}

/// <summary>Block 4 entry — 6 bytes, row #9's type flag + both refs/literal.</summary>
public class ScriptLinkOrReward {
	public short TypeFlag { get; set; }
	public short RefA { get; set; }
	public short RefBOrLiteral { get; set; }
}

/// <summary>
/// Block 5 entry — 74 bytes, row #10 (<see cref="Action82"/>) minus GUID/ConditionRef/Unk04.
/// <see cref="ArrayA"/>/<see cref="ArrayB"/> are <see cref="Action82.ConstantSpan"/>'s first 40 bytes
/// (0x1A-0x43 of the source row), re-split into two interleaved 10-short arrays by the writer's
/// actual read order (even source offsets in A, odd in B) — not the source row's own 21-short span.
/// </summary>
public class ScriptAction {
	public short Type { get; set; }
	public short Verb { get; set; }
	public short[] RefsRow9 { get; set; } = new short[8];
	public short[] ArrayA { get; set; } = new short[10];
	public short[] ArrayB { get; set; } = new short[10];
	public short[] LutRefs { get; set; } = new short[5];
	public short SecondaryValue { get; set; }
	public short Target { get; set; }
}

/// <summary>Block 6 entry — 24 bytes, row #11's resolved target/type/refs only.</summary>
public class ScriptActionPair {
	public short PrimaryActionRef { get; set; }
	public short TimerValue { get; set; }
	public short[] SequenceRefs { get; set; } = new short[10];
}

/// <summary>
/// Block 7 entry — 134 bytes, row #12 (<see cref="EntityTemplate144"/>) minus GUID/ConditionRef/
/// InheritIndex/CompoundConditionPartner and minus <c>SmallDiscrete2</c> (skipped by the writer,
/// not exported). <see cref="HeadBytes"/> = source offsets 0x08-0x2F (BinaryFlag+NearConstant+
/// DeadZone), <see cref="TailBytes"/> = source offsets 0x4C-0x8F (PairedRefs,
/// AlwaysPopulatedBlock+Constant5, Constant2, RefRow10Slot1/2, TrailingField, in that exact
/// on-disk order).
///
/// <para>The four named fields between them are the ones DBSIM's world-spawn pass
/// (<c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>)) reads back out: it re-opens <c>script.dat</c> after
/// <c>DBSim_LoadScriptDat</c> has marked which slots are live and builds one mech per live slot
/// from this record. Do not be misled by <c>DBSim_LoadScriptDat</c> reading 134 bytes and keeping
/// only <see cref="SmallDiscrete"/> — that first pass exists to count and allocate, not to
/// place.</para>
/// </summary>
public class ScriptSpawnRecordExport {
	public byte[] HeadBytes { get; set; } = new byte[40];

	/// <summary>Source offset 0x30 — the mech type, an index into <c>nam\MECHS.NAM</c>'s name list.</summary>
	public short SmallDiscrete { get; set; }

	/// <summary>
	/// Source offsets 0x32-0x45 — the mech's weapon fit, passed straight to DBSIM's
	/// <c>Mech_ConfigureLoadout</c> alongside <see cref="TailBytes"/>' second array. Unused slots
	/// are <c>-1</c>. This is the 10-slot array <c>msn-mission-file.md</c> row #12 calls the
	/// "unresolved 10-slot array ... domain unknown".
	/// </summary>
	public short[] WeaponRefs { get; set; } = new short[10];

	/// <summary>
	/// Source offset 0x46 — index into <see cref="ScriptDat.Coordinates"/>, or <c>-1</c>. Every
	/// retail record carries <c>-1</c> here, in which case the mech takes its spawn point from the
	/// block-11 group that activates it (see <see cref="ScriptEntity164Export.RefRow6"/>).
	/// </summary>
	public short PositionRef { get; set; }

	/// <summary>Source offset 0x48 — index into <see cref="ScriptDat.Headings"/>, or <c>-1</c>.</summary>
	public short HeadingRef { get; set; }

	public byte[] TailBytes { get; set; } = new byte[68];

	/// <summary>
	/// Source offset 0x72 — the second of the two parallel per-slot arrays
	/// <c>Mech_ConfigureLoadout</c> takes, alongside <see cref="WeaponRefs"/>. It is the ammunition
	/// type each missile launcher is loaded with, the value a launcher's mount resolves through
	/// <c>Proj_LookupRecord(Missile, key)</c> and then prints as its name; non-launcher slots carry a
	/// filler 5.
	///
	/// <para>Located by the two stack locals <c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>) hands
	/// the loadout call, which sit exactly 64 bytes apart in a frame holding one record — placing the
	/// second array 64 bytes past <see cref="WeaponRefs"/>' own 0x32. Confirmed against the retail
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

	/// <summary>Where <see cref="WeaponSecondary"/> starts inside <see cref="TailBytes"/> (source 0x72 less 0x4a).</summary>
	private const int SecondaryOffset = 40;

	/// <summary>Slots in both loadout arrays.</summary>
	private const int SlotCount = 10;
}

/// <summary>
/// Block 8 entry — 92 bytes, row #13 (<see cref="UnkEntity102Bytes"/>) minus GUID/ConditionRef/
/// InheritIndex/Unk06 and minus <c>Unk36</c> (skipped, not exported). <see cref="HeadBytes"/> =
/// source offsets 0x08-0x2F (FlagsA), <see cref="TailBytes"/> = source offsets 0x38-0x64 (FlagsB,
/// RefRow10Slot1/2, UnkVal_100).
///
/// <para>DBSIM's world-spawn pass (<c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>)) builds one flyer/vehicle per live slot from
/// this record, taking its type from <see cref="BinaryField"/> and its placement from the two refs
/// below.</para>
/// </summary>
public class ScriptEntity102Export {
	public byte[] HeadBytes { get; set; } = new byte[40];

	/// <summary>Source offset 0x30 — index into <see cref="ScriptDat.Coordinates"/>, or <c>-1</c>.</summary>
	public short PositionRef { get; set; }

	/// <summary>Source offset 0x32 — index into <see cref="ScriptDat.Headings"/>, or <c>-1</c>.</summary>
	public short HeadingRef { get; set; }

	/// <summary>Source offset 0x34 — the flyer type, an index into <c>nam\FLYERS.NAM</c>'s name list.</summary>
	public short BinaryField { get; set; }

	public byte[] TailBytes { get; set; } = new byte[46];
}

/// <summary>
/// Block 9 entry — 52 bytes, row #14 (<see cref="MiscEntityInfo"/>) minus GUID/ConditionRef/
/// InheritIndex/Unk06. <see cref="TailBytes"/> = source offsets 0x0E-0x3D (SmallDiscrete,
/// SparseBlock, RefRow10Slot1/2, TrailingField).
///
/// <para>DBSIM's world-spawn pass (<c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>)) builds one base/structure per live slot
/// from this record.</para>
/// </summary>
public class ScriptMiscEntityExport {
	/// <summary>
	/// Source offset 0x08 — the base type, an index into the 65-entry table in
	/// <c>dat\BASES.DAT</c> (which in turn names the model and its texture bank).
	/// </summary>
	public short TypeLikeScalar { get; set; }

	/// <summary>Source offset 0x0A — index into <see cref="ScriptDat.Coordinates"/>, or <c>-1</c>.</summary>
	public short PositionRef { get; set; }

	/// <summary>Source offset 0x0C — index into <see cref="ScriptDat.Headings"/>, or <c>-1</c>.</summary>
	public short HeadingRef { get; set; }

	public byte[] TailBytes { get; set; } = new byte[46];
}

/// <summary>
/// Block 10 entry — 14 bytes, row #15 (<see cref="LinkedRef22"/>)'s 7 payload fields (0x08-0x14)
/// verbatim. DBSIM's pass 1 reads and discards it; pass 2 resolves it into the group's route link —
/// a group's route and spawn point come from its slot-0 link's <c>0x08</c>. See
/// docs/formats/script-dat.md.
/// </summary>
public class ScriptLinkedRef22Export {
	public short SmallInt1 { get; set; }
	public short SmallInt2 { get; set; }
	public short RefRow6 { get; set; }
	public short RefRow8 { get; set; }
	public short DiscriminatorType { get; set; }
	public short DiscriminatedRef { get; set; }
	public short RefRow10 { get; set; }
}

/// <summary>
/// Block 11 entry — 156 bytes, row #16 (<see cref="EntitySpawn164"/>) minus GUID/ConditionRef/
/// CompoundConditionPartner and minus <c>TrailingDiscriminator</c> (0x78, skipped, not exported).
/// Field names/offsets otherwise match <see cref="EntitySpawn164"/> exactly (confirmed byte-for-
/// byte against the writer). <see cref="ArrayA"/>/<see cref="ArrayB"/> together are the source
/// row's Payload1-4 (0x7A-0x81) + DeadZone2 (0x82-0xA1) span, re-split into two interleaved
/// 10-short arrays by the writer's actual read order (even source offsets in A, odd in B).
/// </summary>
public class ScriptEntity164Export {
	public short BinaryFlag { get; set; }
	public short NearConstant { get; set; }
	public short[] DeadZone { get; set; } = new short[18];
	public short Discriminator { get; set; }
	public short SmallDiscrete { get; set; }
	public short RefRow6 { get; set; }
	public short RefRow7 { get; set; }
	public short RefRow8 { get; set; }
	public short[] DiscriminatedRefs { get; set; } = new short[20];
	public short[] Row15Refs { get; set; } = new short[10];
	public short TriStateFlag { get; set; }
	public short RefRow10 { get; set; }
	public short[] ArrayA { get; set; } = new short[10];
	public short[] ArrayB { get; set; } = new short[10];
	public short TrailingFlag { get; set; }
}

/// <summary>
/// Block 12 entry — 54 bytes, row #17 (<see cref="UnitSpawn58"/>) minus ConditionRef (0x00) and
/// PairCount (0x10) — both skipped, not exported; row #17 is written unfiltered (no GUID to
/// filter on). <see cref="PairRefs"/>/<see cref="PairTags"/> are <see cref="UnitSpawn58.Pairs"/>'s
/// 10 (ref, tag) entries re-exported as parallel arrays (all 10 refs, then all 10 tags) — the
/// writer's own on-disk order, not an array of pair structs. DBSIM reads and fully discards every
/// instance of this block.
/// </summary>
public class ScriptUnitSpawn58Export {
	public short Unk02 { get; set; }
	public short Unk04 { get; set; }
	public short Discriminator { get; set; }
	public short DiscriminatedRef { get; set; }
	public short RefRow6 { get; set; }
	public short RefRow8 { get; set; }
	public short LutRef { get; set; }
	public short[] PairRefs { get; set; } = new short[10];
	public short[] PairTags { get; set; } = new short[10];
}
