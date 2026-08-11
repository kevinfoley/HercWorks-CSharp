using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Msn.Script;

/// <summary>
/// <c>data\script.dat</c> — DBSIM's real gameplay handoff format, written by VSHELL immediately
/// after it parses a <see cref="MissionFile"/> (`.msn`) and read independently by both DBSIM (the
/// actual simulator) and VSHELL's own map-editor UI (`ShellMap`). Every block below is a
/// GUID-filtered, field-subset re-export of one of <see cref="MissionFile"/>'s already-decoded
/// rows — this is not an independently-authored format. See
/// docs/formats/script-dat.md for the full byte-exact writeup (verified against two independently
/// compiled readers plus 10 real sample files).
///
/// The file is a fixed 13,520-byte preallocated buffer in every real sample seen — real content
/// only fills a prefix of it, and any bytes beyond the last block's declared end are stale leftover
/// data from an earlier, larger write (confirmed byte-identical across samples), not part of the
/// format. This model only round-trips the meaningful prefix; nothing here assumes or preserves a
/// fixed total file length.
/// </summary>
public class ScriptDat : DataFile {
	/// <summary>
	/// Fixed 20-byte header, 10 little-endian shorts. Mostly unconfirmed — one field (bytes 2-3)
	/// is real and varies meaningfully across real files, but its exact meaning (mission/chapter
	/// id? a checksum?) wasn't chased down. Round-tripped raw rather than split into named fields.
	/// </summary>
	public byte[] HeaderBytes { get; set; } = new byte[20];

	/// <summary>Block 1 — row #6 (<see cref="MapPoint22"/>) export: X/Y/Z world positions only.</summary>
	public ScriptCoordinate[] Coordinates { get; set; } = [];

	/// <summary>
	/// Block 2 — row #7 (<see cref="Flag10"/>) export: the payload field only. DBSIM multiplies
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
	/// Block 7 — row #12 (<see cref="SpawnRecord144"/>) export, 134 bytes/record. DBSIM only keeps
	/// <see cref="ScriptSpawnRecordExport.SmallDiscrete"/> (the mech-type ref) from this; VSHELL's
	/// own `ShellMap` reader keeps the whole record for UI display. Preserved in full here since a
	/// consumer might need any of it, with only the one confirmed field split out by name.
	/// </summary>
	public ScriptSpawnRecordExport[] SpawnRecords { get; set; } = [];

	/// <summary>
	/// Block 8 — row #13 (<see cref="UnkEntity102Bytes"/>) export, 92 bytes/record. DBSIM only
	/// keeps <see cref="ScriptEntity102Export.BinaryField"/>; discarded entirely otherwise.
	/// </summary>
	public ScriptEntity102Export[] Entities102 { get; set; } = [];

	/// <summary>
	/// Block 9 — row #14 (<see cref="MiscEntityInfo"/>) export, 52 bytes/record. DBSIM only keeps
	/// <see cref="ScriptMiscEntityExport.TypeLikeScalar"/>.
	/// </summary>
	public ScriptMiscEntityExport[] MiscEntities { get; set; } = [];

	/// <summary>
	/// Block 10 — row #15 (<see cref="LinkedRef22"/>) export, 14 bytes/record. DBSIM reads and
	/// fully discards this block; VSHELL's `ShellMap` reader keeps it in full (UI-relevant "what's
	/// this linked to" data).
	/// </summary>
	public ScriptLinkedRef22Export[] LinkedRefs22 { get; set; } = [];

	/// <summary>
	/// Block 11 — row #16 (<see cref="UnkEntity164Bytes"/>) export, 156 bytes/record. This is
	/// DBSIM's actual entity-activation mechanism: <see cref="ScriptEntity164Export.ArrayA"/>/
	/// <see cref="ScriptEntity164Export.ArrayB"/> together are the on-disk interleaving of the
	/// source record's Payload1-4 + DeadZone2 span (even-offset entries in A, odd-offset in B) —
	/// re-derived here from the writer's exact read order, not the `.msn` in-memory field order.
	/// <see cref="ScriptEntity164Export.TrailingDiscriminator"/> (0x78 in the source row) is not
	/// exported at all.
	/// </summary>
	public ScriptEntity164Export[] Entities164 { get; set; } = [];

	/// <summary>
	/// Block 12 — row #17 (<see cref="LinkedRef58"/>) export, 54 bytes/record, unfiltered (row #17
	/// has no GUID to filter on). DBSIM reads and fully discards this block. <see cref="ScriptLinkedRef58Export.PairRefs"/>/
	/// <see cref="ScriptLinkedRef58Export.PairTags"/> are the source record's <c>Pairs[10]</c> array
	/// re-exported as parallel arrays (all 10 refs, then all 10 tags) rather than interleaved pairs —
	/// the writer's own on-disk order. <c>ConditionRef</c> (0x00) and <c>PairCount</c> (0x10) are not exported.
	/// </summary>
	public ScriptLinkedRef58Export[] LinkedRefs58 { get; set; } = [];

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
/// Block 7 entry — 134 bytes, row #12 (<see cref="SpawnRecord144"/>) minus GUID/ConditionRef/
/// InheritIndex/CompoundConditionPartner and minus <c>SmallDiscrete2</c> (skipped by the writer,
/// not exported). <see cref="HeadBytes"/> = source offsets 0x08-0x2F (BinaryFlag+NearConstant+
/// DeadZone), <see cref="SmallDiscrete"/> = source offset 0x30 (the mech-type ref — the one field
/// DBSIM actually reads back out of this block), <see cref="TailBytes"/> = source offsets
/// 0x32-0x8F (UnresolvedRefs, RefRow6, RefRow7, PairedRefs, AlwaysPopulatedBlock+Constant5,
/// Constant2, RefRow10Slot1/2, TrailingField, in that exact on-disk order).
/// </summary>
public class ScriptSpawnRecordExport {
	public byte[] HeadBytes { get; set; } = new byte[40];
	public short SmallDiscrete { get; set; }
	public byte[] TailBytes { get; set; } = new byte[92];
}

/// <summary>
/// Block 8 entry — 92 bytes, row #13 (<see cref="UnkEntity102Bytes"/>) minus GUID/ConditionRef/
/// InheritIndex/Unk06 and minus <c>Unk36</c> (skipped, not exported). <see cref="HeadBytes"/> =
/// source offsets 0x08-0x33 (FlagsA, RefRow6, RefRow7), <see cref="BinaryField"/> = source offset
/// 0x34 (the one field DBSIM keeps), <see cref="TailBytes"/> = source offsets 0x36-0x64 (skips
/// Unk36 itself, then FlagsB, RefRow10Slot1/2, UnkVal_100).
/// </summary>
public class ScriptEntity102Export {
	public byte[] HeadBytes { get; set; } = new byte[44];
	public short BinaryField { get; set; }
	public byte[] TailBytes { get; set; } = new byte[46];
}

/// <summary>
/// Block 9 entry — 52 bytes, row #14 (<see cref="MiscEntityInfo"/>) minus GUID/ConditionRef/
/// InheritIndex/Unk06. <see cref="TypeLikeScalar"/> = source offset 0x08 (the one field DBSIM
/// keeps — it's the very first field exported), <see cref="TailBytes"/> = source offsets
/// 0x0A-0x3D (RefRow6, RefRow7, SparseBlock, RefRow10Slot1/2, TrailingField).
/// </summary>
public class ScriptMiscEntityExport {
	public short TypeLikeScalar { get; set; }
	public byte[] TailBytes { get; set; } = new byte[50];
}

/// <summary>
/// Block 10 entry — 14 bytes, row #15 (<see cref="LinkedRef22"/>)'s 7 payload fields (0x08-0x14)
/// verbatim. DBSIM reads and fully discards every instance of this block.
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
/// Block 11 entry — 156 bytes, row #16 (<see cref="UnkEntity164Bytes"/>) minus GUID/ConditionRef/
/// CompoundConditionPartner and minus <c>TrailingDiscriminator</c> (0x78, skipped, not exported).
/// Field names/offsets otherwise match <see cref="UnkEntity164Bytes"/> exactly (confirmed byte-for-
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
/// Block 12 entry — 54 bytes, row #17 (<see cref="LinkedRef58"/>) minus ConditionRef (0x00) and
/// PairCount (0x10) — both skipped, not exported; row #17 is written unfiltered (no GUID to
/// filter on). <see cref="PairRefs"/>/<see cref="PairTags"/> are <see cref="LinkedRef58.Pairs"/>'s
/// 10 (ref, tag) entries re-exported as parallel arrays (all 10 refs, then all 10 tags) — the
/// writer's own on-disk order, not an array of pair structs. DBSIM reads and fully discards every
/// instance of this block.
/// </summary>
public class ScriptLinkedRef58Export {
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
