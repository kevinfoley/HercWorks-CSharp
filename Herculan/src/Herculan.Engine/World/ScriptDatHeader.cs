using System.Buffers.Binary;

namespace Herculan.Engine.World;

/// <summary>
/// The 20-byte header of <c>data\script.dat</c>, the mission handoff VSHELL writes and DBSIM reads
/// (see docs/formats/script-dat.md for the 13 record blocks that follow it).
///
/// <para>Two of its fields are what a scene needs before anything else, and both are decoded
/// from <c>DBSim_LoadScriptDat</c> (<c>00424308</c>), which reads the header into one global and then
/// uses it twice: it hands the whole 20 bytes to <c>maybe_World_LoadTheater</c> (which takes the
/// short at 0 and the short at 18 as <c>world&lt;index * 2 + variant&gt;</c>) and passes the short at
/// 2 straight to <c>Terrain_LoadZone</c>.</para>
///
/// <para>Verified against the ten real files in the retail install (<c>ES2\DATA\script.dat</c> plus
/// the <c>ES2\SAV\script*.dat</c> snapshots): every <see cref="ZoneIndex"/> is a zone that actually
/// ships (555, 123, 22, 234, 3333 — all present as <c>dat\zoneNNNN.dat</c>), and every
/// <see cref="TheaterIndex"/> is 0, 1 or 2. This resolves script-dat.md's open question about the
/// header field at offset 2, which that doc guessed might be a mission id or checksum.</para>
/// </summary>
public readonly struct ScriptDatHeader {
	/// <summary>Bytes the header occupies; the original reads exactly this many in one call.</summary>
	public const int Size = 20;

	/// <summary>How many difficulty levels there are; see <see cref="Difficulty"/>.</summary>
	public const int DifficultyLevels = 4;

	/// <summary>
	/// The value <see cref="UnlimitedAmmunition"/> and <see cref="PlayerInvulnerable"/> are tested
	/// against. Their readers compare for equality with 1, so a 2 in a hand-edited file is off.
	/// </summary>
	public const short CheatEnabled = 1;

	private ScriptDatHeader(int theaterIndex, int zoneIndex, int objectiveType, int theaterVariant,
			int difficulty, bool unlimitedAmmunition, bool playerInvulnerable) {
		TheaterIndex = theaterIndex;
		ZoneIndex = zoneIndex;
		ObjectiveType = objectiveType;
		TheaterVariant = theaterVariant;
		Difficulty = difficulty;
		UnlimitedAmmunition = unlimitedAmmunition;
		PlayerInvulnerable = playerInvulnerable;
	}

	/// <summary>Theater to load, 0-4 — see <see cref="TheaterDescriptor"/>.</summary>
	public int TheaterIndex { get; }

	/// <summary>Which <c>zoneNNNN</c> the mission plays in.</summary>
	public int ZoneIndex { get; }

	/// <summary>
	/// Offset 6 — <c>DAT_004a9ed8</c>, the <b>mission objective type</b>. It selects which arm of the
	/// player's own think watches for progress: 0 the order target coming into range, 5 closing on
	/// the goal position, 3 or 7 the data-link sequence. Type 3 also takes the data-link subject out
	/// of the AI's candidate set, so the player's squad does not shoot the thing they came to read.
	/// See <see cref="Sim.MechObject.PlayerThink"/> and
	/// <see cref="Sim.Ai.AiTargeting.IsTargetable"/>.
	///
	/// <para>Every one of the ten files in the retail install carries 0, which is what a save-slot
	/// snapshot of a conventional mission would; the other three arms are reached from the
	/// campaign's own missions.</para>
	/// </summary>
	public int ObjectiveType { get; }

	/// <summary>
	/// Selects between a theater's two descriptors: it is <b>time of day</b>, written by the shell's
	/// single-mission setup screen from a <c>Day</c> / <c>Night</c> row. Every retail file carries 0.
	/// </summary>
	public int TheaterVariant { get; }

	/// <summary>
	/// Offset 14 — <c>DAT_004a9ee0</c>, the <b>mission difficulty</b>, <c>0</c>-<c>3</c>. The shell
	/// writes the player pilot's own skill here in a campaign and the single-mission screen's setting
	/// outside one, which is why every retail file carries 2 (<c>VETERAN</c>). Four things in the
	/// original index a four-entry table with it, of which three are ported — see
	/// <see cref="Sim.SimWorld.Difficulty"/> and docs/simulation/difficulty.md.
	///
	/// <para><b>Clamped on the way in.</b> The original indexes those tables with whatever the file
	/// says and would read past them; a hand-edited file is held to the four levels here instead.</para>
	/// </summary>
	public int Difficulty { get; }

	/// <summary>
	/// Offset 10 — <c>DAT_004a9edc</c>, <b>unlimited ammunition and energy</b> when the file says
	/// exactly 1. The shell's single-mission screen sets it; a campaign forces it to 0. What it does
	/// is two things, both for the player's machine alone and both in
	/// <see cref="Sim.WeaponMounts"/>: a shot spends no ammunition, and the mounts hand the Master
	/// Energy Pool back everything they drew this tick. See docs/simulation/difficulty.md.
	/// </summary>
	public bool UnlimitedAmmunition { get; }

	/// <summary>
	/// Offset 12 — <c>DAT_004a9ede</c>, <b>player invulnerable</b> when the file says exactly 1. It
	/// gates the whole of the damage write for the locally piloted machine, so its components take
	/// nothing; its shields still absorb and still drain, because that happens before the write.
	/// </summary>
	public bool PlayerInvulnerable { get; }

	/// <summary>
	/// Reads the header from the start of a <c>script.dat</c>'s bytes. The remaining fields are left
	/// undecoded rather than exposed as raw numbers — <c>DBSim_LoadScriptDat</c> zeroes the one at
	/// offset 4 before use, and offsets 8 and 16 are unread. <see cref="ObjectiveType"/> is not the
	/// theater's: it is the mission layer's, and its reader is <c>Mech_BehaviourPlayerThink</c>.
	///
	/// <para>The two cheat flags are read as <c>== 1</c> rather than as "nonzero", which is how both
	/// of their readers spell the test.</para>
	/// </summary>
	public static ScriptDatHeader Read(ReadOnlySpan<byte> scriptDat) {
		if (scriptDat.Length < Size) {
			throw new InvalidDataException(
				$"script.dat is {scriptDat.Length} bytes; its header alone is {Size}.");
		}

		return new ScriptDatHeader(
			BinaryPrimitives.ReadInt16LittleEndian(scriptDat),
			BinaryPrimitives.ReadInt16LittleEndian(scriptDat[2..]),
			BinaryPrimitives.ReadInt16LittleEndian(scriptDat[6..]),
			BinaryPrimitives.ReadInt16LittleEndian(scriptDat[18..]),
			System.Math.Clamp(
				(int)BinaryPrimitives.ReadInt16LittleEndian(scriptDat[14..]), 0, DifficultyLevels - 1),
			BinaryPrimitives.ReadInt16LittleEndian(scriptDat[10..]) == CheatEnabled,
			BinaryPrimitives.ReadInt16LittleEndian(scriptDat[12..]) == CheatEnabled);
	}
}
