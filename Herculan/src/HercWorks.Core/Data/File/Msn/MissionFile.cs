namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// FILE - /ZONES.VOL/MSN/MSN_FILE.MSN — root file for all data for a mission in Earthsiege 2.
///
/// Rewritten from a real, byte-exact-verified decode of VSHELL's raw `.msn` parser (see
/// docs/formats/msn-mission-file.md) — replaces a prior version that was a literal, unverified
/// port of the old Java project's own guesswork, hardcoded against a single file (TRAIN5.MSN) and
/// known to be structurally wrong (it invented a fixed 189-short block that doesn't exist anywhere
/// in the real format).
///
/// The file is a 2-byte revision field (always 5) followed by 17 array/skip "rows" in a fixed
/// order: a `[uint16 count] -> array of fixed-size records` shape for most rows, one campaign-patch
/// scratch pass (row #2, not persisted) and one skip-only pass (row #5, never read at all) among
/// them. Cross-references between rows are stored on disk as GUIDs into other rows' own record
/// arrays, not array indices — VSHELL resolves them to runtime indices at load time; this port
/// keeps the raw GUID on read and resolves lazily via the lookup helpers below, rather than
/// replicating VSHELL's load-time mutation/compaction/campaign-state filtering.
/// Ported from org.hercworks.core.data.file.msn.MissionFile.
/// </summary>
public class MissionFile {
	/// <summary>2-byte revision field, right after file start; always 5 in real data.</summary>
	public short Revision { get; set; }

	/// <summary>Row #1 — the shared campaign flag/condition-trigger store.</summary>
	public UnkHeaderEntry[]? TriggerEntries { get; set; }

	/// <summary>Row #2 — one-shot campaign-override/patch scratch records; not persisted by VSHELL, kept only for round-trip fidelity.</summary>
	public CampaignOverridePatch82[]? OverridePatches { get; set; }

	/// <summary>Row #3 — condition-gated campaign-variant value lookup.</summary>
	public VariantValue8[]? Variants { get; set; }

	/// <summary>Row #4 — mission-level reward/unlock package(s). Real data has at most one per mission.</summary>
	public RewardPackage144[]? RewardPackages { get; set; }

	/// <summary>Row #5 — skip-only span (count * 64 bytes), genuinely unread/unmodeled by VSHELL at this load path; preserved raw for round-trip.</summary>
	public byte[]? SkippedBytes { get; set; }

	/// <summary>Row #6 — the file's central spatial-reference table.</summary>
	public MapPoint22[]? Points { get; set; }

	/// <summary>Row #7 — minimal per-entity flag records.</summary>
	public Heading10[]? Flags { get; set; }

	/// <summary>Row #8 — named, ordered waypoint/patrol-route lists.</summary>
	public WaypointGroup[]? WaypointGroups { get; set; }

	/// <summary>Row #9 — link/reward dual-purpose records.</summary>
	public LinkOrReward12[]? LinksOrRewards { get; set; }

	/// <summary>Row #10 — mission action/objective records.</summary>
	public Action82[]? Actions { get; set; }

	/// <summary>Row #11 — action-to-action pairings.</summary>
	public ActionPair30[]? ActionPairs { get; set; }

	/// <summary>Row #12 — entity/spawn-style records; second, distinct 144-byte type from row #4.</summary>
	public EntityTemplate144[]? SpawnRecords { get; set; }

	/// <summary>Row #13 — a per-item boolean flag set with mostly-dead cross-refs.</summary>
	public UnkEntity102Bytes[]? Entities102 { get; set; }

	/// <summary>Row #14 — misc entity info (buildings/vehicles).</summary>
	public MiscEntityInfo[]? MiscEntities { get; set; }

	/// <summary>Row #15 — "linked reference" records, primarily pointing into row #8.</summary>
	public LinkedRef22[]? LinkedRefs22 { get; set; }

	/// <summary>Row #16 — the richest record type in the file: position/flag/route/action quartet plus polymorphic ref arrays.</summary>
	public EntitySpawn164[]? Entities164 { get; set; }

	/// <summary>
	/// Row #17 — the file's final row: unit/herc-type assignment tied to a waypoint group. No
	/// GUID; never referenced by anything else. An entry is null only for the one known real-world
	/// truncation case (DEMO2.MSN) — see <see cref="TruncatedRow17Tail"/>.
	/// </summary>
	public UnitSpawn58?[]? LinkedRefs58 { get; set; }

	/// <summary>
	/// Raw leftover bytes when row #17's last declared record didn't have enough file left to read
	/// in full (the one known case: DEMO2.MSN, 16 bytes remaining where 58 were expected).
	/// Null for every other real file. Preserved so a write-back reproduces the source file exactly,
	/// truncation included, instead of fabricating or dropping data.
	/// </summary>
	public byte[]? TruncatedRow17Tail { get; set; }

	public VariantValue8? GetVariant(short guid) => FindByGuid(Variants, guid);
	public MapPoint22? GetPoint(short guid) => FindByGuid(Points, guid);
	public Heading10? GetFlag(short guid) => FindByGuid(Flags, guid);
	public WaypointGroup? GetWaypointGroup(short guid) => FindByGuid(WaypointGroups, guid);
	public LinkOrReward12? GetLinkOrReward(short guid) => FindByGuid(LinksOrRewards, guid);
	public Action82? GetAction(short guid) => FindByGuid(Actions, guid);
	public ActionPair30? GetActionPair(short guid) => FindByGuid(ActionPairs, guid);
	public EntityTemplate144? GetSpawnRecord(short guid) => FindByGuid(SpawnRecords, guid);
	public UnkEntity102Bytes? GetEntity102(short guid) => FindByGuid(Entities102, guid);
	public MiscEntityInfo? GetMiscEntity(short guid) => FindByGuid(MiscEntities, guid);
	public LinkedRef22? GetLinkedRef22(short guid) => FindByGuid(LinkedRefs22, guid);
	public EntitySpawn164? GetEntity164(short guid) => FindByGuid(Entities164, guid);

	/// <summary>
	/// Generalizes the old FindMarkedObjectById/GetMapCoordById lookups into one typed, per-row
	/// GUID search. Every row's cross-references are into a specific other row's own array, not a
	/// shared global object space, so this is intentionally per-row rather than one flat registry.
	/// </summary>
	private static T? FindByGuid<T>(T[]? rows, short guid) where T : MapObject =>
		guid == -1 ? null : rows?.FirstOrDefault(r => r.GUID == guid);
}
