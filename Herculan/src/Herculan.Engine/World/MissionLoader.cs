using HercWorks.Core.Data.File.Msn.Script;
using HercWorks.Core.Data.File.Sav;
using HercWorks.Core.Io.Transform.Common;
using Herculan.Engine.Content;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.World;

/// <summary>
/// Turns the game's own mission handoff — <c>DATA\script.dat</c>, plus <c>DATA\player.mec</c> when
/// it is there — into a <see cref="Mission"/>.
///
/// <para><b>The two-pass structure below is DBSIM's, not an invention.</b> It is easy to read
/// <c>DBSim_LoadScriptDat</c> (<c>00424308</c>) and conclude the format throws its placement data
/// away: that function reads all 134 bytes of a block-7 record and keeps only the type field, and it
/// reads block 11 into a stack buffer and keeps only which slots the record marks live. It does not
/// place anything because placing is not its job — it counts live objects and allocates the pools.
/// <c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>) then <b>re-opens the same file</b> and walks it again, and that is the pass
/// that builds objects. Anything reading only the first pass will conclude, wrongly, that
/// <c>script.dat</c> carries no positions.</para>
///
/// <para>The placement rule that pass implements, with the RE for each step:</para>
/// <list type="number">
/// <item><b>Which objects exist.</b> Every block-11 record past the first is an activation
/// directive: a 3-way discriminator naming a roster (mech/flyer/base) and up to 20 refs into it.
/// Each ref marks that roster slot live. Records that mark nothing spawn nothing, which is why a
/// mission's roster is routinely larger than its live object count.</item>
/// <item><b>Where they stand.</b> The same block-11 record carries a position ref, a heading ref and
/// a route, and <c>DBSim_BuildGroupRecord</c> (<c>00423b34</c>) builds it into a group record holding all three. Each member is
/// then attached by <c>Mech_AttachToGroup</c> (<c>00417aa8</c>), which fills in the member's position <i>only if the object
/// does not already have one of its own</i> — so a roster record's own position ref wins where it is
/// set, and the group's is the fallback. In retail data the roster refs are always unset, so in
/// practice every object stands at its group's point.</item>
/// <item><b>Groups with no point of their own</b> fall back to their route: the group's row-15 links
/// resolve to a waypoint group, and <c>FUN_00423b0c</c> takes a waypoint from it. Patrol lances are
/// placed this way — the retail mission's three mech lances all are.</item>
/// <item><b>The player's lance</b> is not in <c>script.dat</c>'s roster at all. Block 11's
/// <b>record 0</b> exists only to hold its spawn point: <c>DBSim_LoadScriptDat</c> skips it during
/// activation, and <c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>) overwrites its member list with the entries read from
/// <c>player.mec</c>.</item>
/// </list>
///
/// <para><b>Formation offsets: bases implemented, mechs/flyers still a known gap.</b> Every member of
/// a group is placed on the group's own point by the rule above; the original then spreads
/// non-leader members off that point via a per-kind vtable <c>+0x78</c> call
/// (<c>Mech_ApplyFormationOffset</c> (<c>00417898</c>) for mechs, <c>FUN_00405c04</c> for bases —
/// same slot, same "member index 0 takes no offset" rule). For mechs this waits on an unconfirmed
/// load site: the table <c>Formation_GetSlotOffset</c> (<c>004205cc</c>) reads matches
/// <c>dat\MFORMS.DAT</c>'s shape exactly (142 content bytes = a count plus five 28-byte formations,
/// formation 0 a symmetric 10,000-unit wedge), but the pointer it reads has exactly one reference in
/// all of DBSIM.EXE — that read — and lives in uninitialised data, so nothing links the file to the
/// table; implementing it on the strength of the filename alone would be the kind of plausible guess
/// that has cost this project time before. <b>Bases are different</b>: their table
/// (<see cref="BaseFormationTable"/>, <c>dat\BFORMS.DAT</c>) has a confirmed load site
/// (<c>FUN_00405fac</c> streams it from a file literally named <c>"bforms"</c>) and is implemented
/// below — see that class's doc comment for the byte-exact verification. Flyers remain unfixed
/// either way; retail missions were not seen putting more than one flyer in a group.</para>
/// </summary>
public static class MissionLoader {
	/// <summary>Folder inside an install root holding the loose mission handoff files.</summary>
	public const string DataFolderName = "DATA";

	/// <summary>The mission handoff itself.</summary>
	public const string ScriptFileName = "script.dat";

	/// <summary>The player's lance, written beside it.</summary>
	public const string PlayerFileName = "player.mec";

	/// <summary>
	/// The lance file that goes with a mission file. The live pair in <c>DATA\</c> is
	/// <c>script.dat</c>/<c>player.mec</c>; the save-slot snapshots in <c>SAV\</c> keep the same
	/// pairing with a slot number on both halves (<c>script3.dat</c>/<c>player3.mec</c>).
	/// </summary>
	public static string PlayerPathFor(string scriptPath) {
		string directory = Path.GetDirectoryName(scriptPath) ?? ".";
		string slot = Path.GetFileNameWithoutExtension(scriptPath);

		slot = slot.StartsWith("script", StringComparison.OrdinalIgnoreCase)
			? slot["script".Length..]
			: string.Empty;

		return Path.Combine(directory, $"player{slot}.mec");
	}

	/// <summary>
	/// Degrees-to-binary-angle constant. DBSIM applies this to every block-2 value as it reads them
	/// (<c>*(short *)local_7c = local_102[0] * 0xb6</c>), which is what identifies that block as
	/// headings in degrees rather than an opaque discrete field.
	/// </summary>
	private const int DegreesToBinaryAngle = 0xb6;

	/// <summary>The block-11 record reserved for the player's lance; it activates nothing.</summary>
	private const int PlayerGroupIndex = 0;

	/// <summary>Members a block-11 record can name.</summary>
	private const int GroupMemberSlots = 20;

	/// <summary>Row-15 links a block-11 record can name.</summary>
	private const int GroupRouteSlots = 10;

	/// <summary>The conventional path of a mission handoff inside an install root.</summary>
	public static string DefaultScriptPath(string installRoot) =>
		Path.Combine(installRoot, DataFolderName, ScriptFileName);

	/// <summary>
	/// Loads the mission at <paramref name="scriptPath"/>. <paramref name="content"/> supplies the
	/// type-name lists, which live in the archives rather than beside the mission.
	///
	/// <para><c>player.mec</c> is looked for next to the script and is optional: without it the
	/// mission still loads, just with no <see cref="Mission.Player"/>.</para>
	/// </summary>
	public static Mission Load(GameContent content, string scriptPath) {
		byte[] scriptBytes = File.ReadAllBytes(scriptPath);
		var script = new ScriptDatTransformer().BytesToObject(scriptBytes) as ScriptDat
			?? throw new InvalidDataException($"{scriptPath} did not parse as a script.dat.");

		var header = ScriptDatHeader.Read(scriptBytes);
		var mechNames = UnitTypeNames.LoadMechs(content);
		var flyerNames = UnitTypeNames.LoadFlyers(content);
		var baseFormations = BaseFormationTable.Load(content);

		var groups = ResolveGroups(script);
		var claims = ClaimSlots(script, groups);

		var placements = new List<MissionPlacement>();
		AddRoster(script, claims, mechNames, flyerNames, baseFormations, placements);

		var player = LoadPlayerLance(scriptPath, groups, mechNames, placements);

		return new Mission(scriptPath, header, placements, player);
	}

	/// <summary>A block-11 record reduced to what placement needs.</summary>
	private readonly record struct Group(int Index, MissionUnitKind Kind, Vec3i Position, int Heading,
		int FormationId);

	/// <summary>
	/// A roster slot's claim: which group activated it, and the slot's index within that group's
	/// <c>DiscriminatedRefs</c> array — the "member index" <see cref="BaseFormationTable.OffsetFor"/>
	/// needs, which is the array position, not a compacted count of live members (see
	/// <see cref="ClaimSlots"/>).
	/// </summary>
	private readonly record struct Claim(Group Group, int MemberIndex);

	private static Group[] ResolveGroups(ScriptDat script) {
		var groups = new Group[script.Entities164.Length];

		for (int i = 0; i < groups.Length; i++) {
			var record = script.Entities164[i];

			var position = Coordinate(script, record.RefRow6)
				?? RouteStart(script, record)
				?? Vec3i.Zero;

			groups[i] = new Group(
				i,
				KindOf(record.Discriminator),
				position,
				Heading(script, record.RefRow7) ?? 0,
				record.SmallDiscrete);
		}

		return groups;
	}

	/// <summary>
	/// First-claim map, one entry per roster: slot index to the group that places it, and the slot's
	/// position within that group's member-ref array. Groups are walked in file order and the first
	/// to name a slot wins, matching <c>Mech_AttachToGroup</c> (<c>00417aa8</c>)'s "only if it has no
	/// position yet" test. The player's group is skipped, since it activates nothing.
	///
	/// <para>The member index recorded here is the raw array position (0-19), <b>not</b> a count of
	/// live members seen so far — <c>DBSim_BuildGroupRecord</c> (<c>00423b34</c>) passes the loop
	/// index itself as the attach-time slot, gaps and all, and that is what <c>BaseFormationTable</c>
	/// indexes by.</para>
	/// </summary>
	private static Dictionary<MissionUnitKind, Dictionary<int, Claim>> ClaimSlots(
			ScriptDat script, Group[] groups) {
		var claims = new Dictionary<MissionUnitKind, Dictionary<int, Claim>> {
			[MissionUnitKind.Mech] = new(),
			[MissionUnitKind.Flyer] = new(),
			[MissionUnitKind.Base] = new()
		};

		for (int i = PlayerGroupIndex + 1; i < groups.Length; i++) {
			var group = groups[i];
			if (!claims.TryGetValue(group.Kind, out var bySlot)) {
				continue;
			}

			var refs = script.Entities164[i].DiscriminatedRefs;
			for (int slot = 0; slot < GroupMemberSlots && slot < refs.Length; slot++) {
				short member = refs[slot];
				if (member >= 0) {
					bySlot.TryAdd(member, new Claim(group, slot));
				}
			}
		}

		return claims;
	}

	/// <summary>
	/// Walks the three rosters in the order DBSIM's spawn pass does, emitting one placement per live
	/// slot.
	/// </summary>
	private static void AddRoster(ScriptDat script,
			Dictionary<MissionUnitKind, Dictionary<int, Claim>> claims,
			UnitTypeNames mechNames, UnitTypeNames flyerNames, BaseFormationTable baseFormations,
			List<MissionPlacement> placements) {
		var mechClaims = claims[MissionUnitKind.Mech];
		for (int slot = 0; slot < script.SpawnRecords.Length; slot++) {
			if (!mechClaims.TryGetValue(slot, out var claim)) {
				continue;
			}

			var group = claim.Group;
			var record = script.SpawnRecords[slot];
			placements.Add(new MissionPlacement(
				MissionUnitKind.Mech,
				record.SmallDiscrete,
				mechNames[record.SmallDiscrete],
				slot,
				group.Index,
				Coordinate(script, record.PositionRef) ?? group.Position,
				Heading(script, record.HeadingRef) ?? group.Heading,
				record.WeaponRefs.Where(id => id >= 0).ToArray()));
		}

		var flyerClaims = claims[MissionUnitKind.Flyer];
		for (int slot = 0; slot < script.Entities102.Length; slot++) {
			if (!flyerClaims.TryGetValue(slot, out var claim)) {
				continue;
			}

			var group = claim.Group;
			var record = script.Entities102[slot];
			placements.Add(new MissionPlacement(
				MissionUnitKind.Flyer,
				record.BinaryField,
				flyerNames[record.BinaryField],
				slot,
				group.Index,
				Coordinate(script, record.PositionRef) ?? group.Position,
				Heading(script, record.HeadingRef) ?? group.Heading,
				Array.Empty<short>()));
		}

		var baseClaims = claims[MissionUnitKind.Base];
		for (int slot = 0; slot < script.MiscEntities.Length; slot++) {
			if (!baseClaims.TryGetValue(slot, out var claim)) {
				continue;
			}

			var group = claim.Group;
			var record = script.MiscEntities[slot];
			var position = Coordinate(script, record.PositionRef)
				?? OffsetFromGroup(group, baseFormations, claim.MemberIndex);

			placements.Add(new MissionPlacement(
				MissionUnitKind.Base,
				record.TypeLikeScalar,
				null,
				slot,
				group.Index,
				position,
				Heading(script, record.HeadingRef) ?? group.Heading,
				Array.Empty<short>()));
		}
	}

	/// <summary>
	/// A base's spawn point when it carries no coordinate of its own: the group's point, spread by
	/// its <see cref="BaseFormationTable"/> entry for this member's slot (member 0, the group's
	/// first-claimed slot, always takes no offset — see that class's doc comment) and rotated by the
	/// group's own heading, mirroring <c>Formation_RotateAndAddOffset</c> (<c>00411d64</c>).
	/// </summary>
	private static Vec3i OffsetFromGroup(Group group, BaseFormationTable baseFormations, int memberIndex) {
		if (baseFormations.OffsetFor(group.FormationId, memberIndex) is not { } offset) {
			return group.Position;
		}

		short cos = BinaryAngle.Cos(group.Heading);
		short sin = BinaryAngle.Sin(group.Heading);
		int worldDx = (int)(((long)offset.X * cos - (long)offset.Y * sin + 0x2000) >> 14);
		int worldDy = (int)(((long)offset.X * sin + (long)offset.Y * cos + 0x2000) >> 14);

		return new Vec3i(group.Position.X + worldDx, group.Position.Y + worldDy, group.Position.Z);
	}

	/// <summary>
	/// Reads the player's lance and places it at block 11's reserved record-0 point. Returns the
	/// entry the player themself pilots, which is the one worth putting a camera on.
	/// </summary>
	private static MissionPlacement? LoadPlayerLance(string scriptPath, Group[] groups,
			UnitTypeNames mechNames, List<MissionPlacement> placements) {
		string playerPath = PlayerPathFor(scriptPath);

		if (groups.Length == 0 || !File.Exists(playerPath)) {
			return null;
		}

		if (new MecFileTransformer().BytesToObject(File.ReadAllBytes(playerPath)) is not MecFile lance) {
			return null;
		}

		var spawn = groups[PlayerGroupIndex];
		MissionPlacement? player = null;

		for (int i = 0; i < lance.Entries.Length; i++) {
			var entry = lance.Entries[i];
			var placement = new MissionPlacement(
				MissionUnitKind.Mech,
				entry.MechType,
				mechNames[entry.MechType],
				i,
				PlayerGroupIndex,
				spawn.Position,
				spawn.Heading,
				entry.WeaponRefs.Where(id => id > 0).ToArray(),
				IsPlayerLance: true);

			placements.Add(placement);
			if (i == lance.PlayerEntryIndex) {
				player = placement;
			}
		}

		return player;
	}

	private static MissionUnitKind KindOf(short discriminator) => discriminator switch {
		0 => MissionUnitKind.Mech,
		1 => MissionUnitKind.Flyer,
		2 => MissionUnitKind.Base,
		// Retail data only ever carries 0/1/2. An unknown discriminator names no roster, so the
		// record claims nothing and its members simply never spawn — which is what DBSIM's own
		// if/else-if chain does with it too.
		_ => (MissionUnitKind)(-1)
	};

	private static Vec3i? Coordinate(ScriptDat script, short reference) =>
		reference >= 0 && reference < script.Coordinates.Length
			? new Vec3i(
				script.Coordinates[reference].X,
				script.Coordinates[reference].Y,
				script.Coordinates[reference].Z)
			: null;

	private static int? Heading(ScriptDat script, short reference) =>
		reference >= 0 && reference < script.Headings.Length
			? script.Headings[reference].Value * DegreesToBinaryAngle
			: null;

	/// <summary>
	/// A group's route start: its first row-15 link resolves to a waypoint group, whose first
	/// waypoint is a block-1 coordinate. This is how every patrolling lance in the retail mission is
	/// positioned, since those groups carry no point of their own.
	/// </summary>
	private static Vec3i? RouteStart(ScriptDat script, ScriptEntity164Export record) {
		for (int i = 0; i < GroupRouteSlots && i < record.Row15Refs.Length; i++) {
			short linkRef = record.Row15Refs[i];
			if (linkRef < 0 || linkRef >= script.LinkedRefs22.Length) {
				continue;
			}

			short groupRef = script.LinkedRefs22[linkRef].RefRow8;
			if (groupRef < 0 || groupRef >= script.WaypointGroups.Length) {
				continue;
			}

			var waypoints = script.WaypointGroups[groupRef].Waypoints;
			if (waypoints.Length > 0 && Coordinate(script, waypoints[0]) is { } start) {
				return start;
			}
		}

		return null;
	}
}
