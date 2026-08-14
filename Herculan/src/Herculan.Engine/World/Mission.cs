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
/// <param name="WeaponRefs">The mech's weapon fit; empty for anything else.</param>
/// <param name="IsPlayerLance">
/// Whether this came from <c>player.mec</c> rather than the mission's own roster.
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
	bool IsPlayerLance = false);

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
