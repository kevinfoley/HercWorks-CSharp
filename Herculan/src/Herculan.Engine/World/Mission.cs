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
/// <param name="AwaitingDeployment">
/// Whether the block-11 record that placed it is waiting on a mission action, in which case the unit
/// is not in the mission yet and <see cref="Position"/> is a placeholder its arrival replaces — see
/// <see cref="Herculan.Engine.Sim.SimObject.AwaitingDeployment"/> and <see cref="MissionLoader"/>.
/// </param>
/// <param name="Side">
/// Whose side the group that placed it is on — see <see cref="MissionSide"/>. Carried per placement
/// rather than per group because that is the form everything downstream wants: the simulation reads
/// it off the object, not off a group record it does not have.
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
	bool AwaitingDeployment = false,
	MissionSide Side = MissionSide.Human);

/// <summary>
/// A mission ready to be turned into a scene: which zone and theater it plays in, and every object
/// it places. This is the output of <see cref="MissionLoader"/> and carries no file-format detail —
/// a scene builder, a mission editor or a headless test all consume the same shape.
/// </summary>
public sealed class Mission {
	public Mission(string sourcePath, ScriptDatHeader header, IReadOnlyList<MissionPlacement> placements,
			MissionPlacement? player) {
		SourcePath = sourcePath;
		Header = header;
		Placements = placements;
		Player = player;
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

	/// <summary>How many placed objects of one kind the mission has.</summary>
	public int CountOf(MissionUnitKind kind) => Placements.Count(p => p.Kind == kind);
}
