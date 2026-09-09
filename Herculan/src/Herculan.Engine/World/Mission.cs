using Herculan.Engine.Numerics;

namespace Herculan.Engine.World;

/// <summary>What class of thing a <see cref="MissionPlacement"/> is, and hence which roster it came from.</summary>
public enum MissionUnitKind {
	/// <summary>A HERC — <c>script.dat</c> block 7, typed by <c>nam\MECHS.NAM</c>.</summary>
	Mech,

	/// <summary>A flyer or ground vehicle — block 8, typed by <c>nam\FLYERS.NAM</c>.</summary>
	Flyer,

	/// <summary>A structure — block 9, typed by <c>dat\BASES.DAT</c>.</summary>
	Base
}

/// <summary>
/// Which side of the war an object belongs to — <c>script.dat</c> block 11's <c>0x6e</c>, which
/// <c>DBSim_BuildGroupRecord</c> (<c>00423b34</c>) copies into the in-memory group record's
/// <c>+0x12</c>.
///
/// <para>Every "is this one of ours" test in the simulation is a comparison of that one byte
/// between two objects' group records — the target filter, the detection sweep and the contact
/// share all read it. It is a property of the <i>group</i>, so every member of a group is on the
/// same side.</para>
/// </summary>
public enum MissionSide {
	/// <summary>Human. The player's own squad, and everything that fights alongside it.</summary>
	Human = 0,

	/// <summary>Cybrid. The only side the detection sweep scans <i>for</i> — see <c>Detection</c>.</summary>
	Cybrid = 1
}

/// <summary>
/// One object the mission puts in the world: what it is, where it stands and which way it faces.
/// </summary>
/// <param name="Kind">Which roster it came from.</param>
/// <param name="TypeIndex">Its index within that roster's type list.</param>
/// <param name="TypeName">
/// The resolved resource base name (<c>HYPERION</c>, <c>SKIMMER</c>) for mechs and flyers, or null
/// for bases, which are named by table index rather than by string.
/// </param>
/// <param name="SlotIndex">Its record index within its <c>script.dat</c> block, for diagnostics.</param>
/// <param name="GroupIndex">The block-11 record that activated and placed it.</param>
/// <param name="Position">Spawn position in world units. Z is left at zero — the ground under a
/// spawn point is a terrain query the scene does once the zone is loaded, exactly as DBSIM does.</param>
/// <param name="Heading">Facing as a binary angle, already converted from the file's degrees.</param>
/// <param name="WeaponRefs">
/// The mech's weapon fit, one entry per fit slot and holes left in — empty for anything else. The
/// slot positions are load-bearing: the chassis' <c>.GL</c> hardpoint list indexes this array, so it
/// is carried as the file states it rather than compacted.
/// </param>
/// <param name="WeaponSecondary">
/// The parallel second array the same loadout call takes — the ammunition type per slot. See
/// <see cref="Herculan.Engine.Sim.MechLoadout.SecondaryKeys"/>.
/// </param>
/// <param name="IsPlayerLance">
/// Whether this came from <c>player.mec</c> rather than the mission's own roster.
/// </param>
/// <param name="Side">
/// Whose side the group that placed it is on — see <see cref="MissionSide"/>. Carried per placement
/// rather than per group because that is the form everything downstream wants: the simulation reads
/// it off the object, not off a group record it does not have.
/// </param>
/// <param name="AiCruiseSpeed">
/// Block 7 <c>+0x02</c> — the speed this machine's AI walks at, or 0 for the AI's own default.
/// </param>
/// <param name="AiRadarActive">Block 7 <c>+0x00</c> — this machine's standing radar setting, PASSIVE or ACTIVE.</param>
/// <param name="EngagementActionRef">
/// The roster record's <c>0x80</c> — the mission action this object fires when an enemy that
/// already sees it closes to engagement range, or <c>-1</c>. See
/// <see cref="Herculan.Engine.Sim.SimObject.EngagementAction"/>.
/// </param>
/// <param name="DefeatActionRef">
/// The roster record's <c>0x82</c> — the mission action this object fires when it is defeated, or
/// <c>-1</c>. See <see cref="Herculan.Engine.Sim.SimObject.DefeatAction"/>.
/// </param>
/// <param name="FormationOffset">
/// This member's unrotated spread offset out of <c>MFORMS.DAT</c>, or null for the group's slot 0
/// and for a formation that names none. Resolved here because it is wanted twice: once to place the
/// machine, and again every tick a follower holds formation on its leader.
/// </param>
public sealed record MissionPlacement(
	MissionUnitKind Kind,
	int TypeIndex,
	string? TypeName,
	int SlotIndex,
	int GroupIndex,
	Vec3i Position,
	int Heading,
	IReadOnlyList<short> WeaponRefs,
	IReadOnlyList<short> WeaponSecondary,
	bool IsPlayerLance = false,
	MissionSide Side = MissionSide.Human,
	short AiCruiseSpeed = 0,
	bool AiRadarActive = false,
	(int X, int Y)? FormationOffset = null,
	int EngagementActionRef = -1,
	int DefeatActionRef = -1);

/// <summary>
/// One patch of ground a base group paints with its formation's own material — the concrete pad a
/// retail base stands on. Produced for a group whose block-11 record sets its <c>BinaryFlag</c> and
/// whose formation declares a layout; see
/// <see cref="Herculan.Engine.Terrain.HeightGrid.PaintFormationPad"/> for what is done with it.
/// </summary>
/// <param name="Anchor">
/// The position the painted tile is chosen from: the group's first-attached member, which is where
/// the original reads it. Only which tile it falls in matters, not where within that tile.
/// </param>
/// <param name="Layout">The formation's material index and occupancy map.</param>
public sealed record MissionBasePad(Vec3i Anchor, BaseFormationLayout Layout);

/// <summary>
/// A mission ready to be turned into a scene: which zone and theater it plays in, and every object
/// it places. This is the output of <see cref="MissionLoader"/> and carries no file-format detail —
/// a scene builder, a mission editor or a headless test all consume the same shape.
/// </summary>
public sealed class Mission {
	public Mission(string sourcePath, ScriptDatHeader header, IReadOnlyList<MissionPlacement> placements,
			MissionPlacement? player, IReadOnlyList<MissionBasePad> basePads,
			IReadOnlyList<Vec3i> coordinates, IReadOnlyList<Vec3i> playerRoute,
			IReadOnlyList<IReadOnlyList<MissionOrder?>> groupOrders,
			IReadOnlyList<MissionAction> actions,
			IReadOnlyList<MissionActionTimer> actionTimers,
			IReadOnlyList<int> groupDeploymentActions,
			IReadOnlyList<MissionUnitKind> groupKinds,
			IReadOnlyList<MissionSide> groupSides) {
		SourcePath = sourcePath;
		Header = header;
		Placements = placements;
		Player = player;
		BasePads = basePads;
		Coordinates = coordinates;
		PlayerRoute = playerRoute;
		GroupOrders = groupOrders;
		Actions = actions;
		ActionTimers = actionTimers;
		GroupDeploymentActions = groupDeploymentActions;
		GroupKinds = groupKinds;
		GroupSides = groupSides;
	}

	/// <summary>Where the <c>script.dat</c> was read from.</summary>
	public string SourcePath { get; }

	/// <summary>The zone, theater and theater variant to load.</summary>
	public ScriptDatHeader Header { get; }

	/// <summary>Every object the mission places, in spawn order.</summary>
	public IReadOnlyList<MissionPlacement> Placements { get; }

	/// <summary>
	/// The machine the player pilots, if <c>player.mec</c> was available — the natural place to put a
	/// camera, since it is where the mission actually starts.
	/// </summary>
	public MissionPlacement? Player { get; }

	/// <summary>Every patch of ground a base group repaints, in group order.</summary>
	public IReadOnlyList<MissionBasePad> BasePads { get; }

	/// <summary>
	/// <c>script.dat</c> block 1 in file order — every point the mission names, which is what its
	/// spawn points, waypoints and route legs all reference. Kept whole because the block's
	/// <i>extent</i> is a fact in its own right: <c>DBSim_LoadScriptDat</c> accumulates the bounding
	/// box as it reads and the Heads-Down Display's map is framed by it end to end. See
	/// <see cref="Herculan.Engine.Content.HddMapBounds"/>.
	/// </summary>
	public IReadOnlyList<Vec3i> Coordinates { get; }

	/// <summary>
	/// The route the player's own squad group carries, as block-1 points. The command display draws
	/// its first nine legs past the start as numbered waypoint markers.
	/// </summary>
	public IReadOnlyList<Vec3i> PlayerRoute { get; }

	/// <summary>
	/// Every group's order list, indexed by block-11 record index and ten slots wide with the unset
	/// ones left null — what the group works through, and where its AI machines get a state to be in.
	/// See <see cref="MissionOrder"/> and <see cref="Herculan.Engine.Sim.MissionGroup"/>.
	/// </summary>
	public IReadOnlyList<IReadOnlyList<MissionOrder?>> GroupOrders { get; }

	/// <summary>
	/// Block 5 in file order — every mission action, with its trigger areas resolved. See
	/// <see cref="MissionAction"/>; <see cref="Herculan.Engine.Sim.MissionTriggers"/> runs them.
	/// </summary>
	public IReadOnlyList<MissionAction> Actions { get; }

	/// <summary>
	/// Block 6 in file order — the mission's timers. See <see cref="MissionActionTimer"/>.
	/// </summary>
	public IReadOnlyList<MissionActionTimer> ActionTimers { get; }

	/// <summary>
	/// Which action each group is waiting on, by block-11 record index, with <c>-1</c> for a group
	/// that is in the mission from the start — the record's <c>0x70</c>, which becomes the group
	/// record's <c>+0x14</c> gate. See
	/// <see cref="Herculan.Engine.Sim.MissionGroup.AwaitingDeployment"/>.
	/// </summary>
	public IReadOnlyList<int> GroupDeploymentActions { get; }

	/// <summary>
	/// Each group's roster discriminator, by block-11 record index. A group with no live members
	/// still has one, which is why it is carried on the mission and not derived from a placement.
	/// </summary>
	public IReadOnlyList<MissionUnitKind> GroupKinds { get; }

	/// <summary>And each group's side, on the same terms.</summary>
	public IReadOnlyList<MissionSide> GroupSides { get; }

	/// <summary>How many placed objects of one kind the mission has.</summary>
	public int CountOf(MissionUnitKind kind) => Placements.Count(p => p.Kind == kind);
}
