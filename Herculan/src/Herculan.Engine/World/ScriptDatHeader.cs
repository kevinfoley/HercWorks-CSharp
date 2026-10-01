using HercWorks.Core.Data.File.Msn.Script;

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
/// <para>The offsets are <see cref="ScriptDat"/>'s; this is the engine's interpretation of them.</para>
/// </summary>
public readonly struct ScriptDatHeader {
	/// <summary>Bytes the header occupies; the original reads exactly this many in one call.</summary>
	public const int Size = ScriptDat.HeaderSize;

	/// <summary>How many difficulty levels there are; see <see cref="Difficulty"/>.</summary>
	public const int DifficultyLevels = 4;

	/// <summary>
	/// The value <see cref="UnlimitedAmmunition"/> and <see cref="PlayerInvulnerable"/> are tested
	/// against. Their readers compare for equality with 1, so a 2 in a hand-edited file is off.
	/// </summary>
	public const short CheatEnabled = 1;

	private ScriptDatHeader(int theaterIndex, int zoneIndex, int objectiveType, int theaterVariant,
			int difficulty, bool unlimitedAmmunition, bool playerInvulnerable,
			int trainingMissionNumber) {
		TheaterIndex = theaterIndex;
		ZoneIndex = zoneIndex;
		ObjectiveType = objectiveType;
		TheaterVariant = theaterVariant;
		Difficulty = difficulty;
		UnlimitedAmmunition = unlimitedAmmunition;
		PlayerInvulnerable = playerInvulnerable;
		TrainingMissionNumber = trainingMissionNumber;
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
	/// practice missions screen from a <c>Day</c> / <c>Night</c> row. Every retail file carries 0.
	/// </summary>
	public int TheaterVariant { get; }

	/// <summary>
	/// Offset 14 — <c>DAT_004a9ee0</c>, the <b>mission difficulty</b>, <c>0</c>-<c>3</c>. The shell
	/// writes the player pilot's own skill here in a campaign and the practice missions screen's setting
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
	/// exactly 1. The shell's practice missions screen sets it; a campaign forces it to 0. What it does
	/// is two things, both for the player's machine alone and both in
	/// <see cref="Sim.WeaponMounts"/>: a shot spends no ammunition, and the mounts hand the Master
	/// Energy Pool back everything they drew this tick. See docs/simulation/difficulty.md.
	/// </summary>
	public bool UnlimitedAmmunition { get; }

	/// <summary>
	/// Offset 8 — <c>DAT_004a9eda</c>, the <b>training mission number</b>, 0 for anything that is
	/// not one. <c>DBSim_LoadScriptDat</c> only stores it; the copy every reader takes is
	/// <c>DAT_004aa7ac</c>, made at the end of the load (<c>00425321</c>). Three things branch on it,
	/// and all three read the copy:
	///
	/// <list type="bullet">
	/// <item><b>No music.</b> <c>Sim_InitMissionSession</c> sets the CD track only when this is 0, so
	/// a training mission plays none — see <see cref="Audio.SoundDirector.StartMissionMusic"/>.</item>
	/// <item><b>A different pilot and squad port.</b> The cockpit builds a <c>0x4ef</c>-byte instance
	/// at <c>view+0x207</c> instead of the ordinary <c>0x4df</c>-byte one, and moves the box up by its
	/// own height.</item>
	/// <item><b>Its own voice clips.</b> The instructor speaks from the <c>TM&lt;n&gt;_</c> name
	/// template rather than the squad's <c>P&lt;bank&gt;_</c> one, with this number as the digit.</item>
	/// </list>
	///
	/// <para>Every one of the ten files in the retail install carries 0: the training missions reach
	/// DBSIM through the shell, not through a save-slot snapshot.</para>
	/// </summary>
	public int TrainingMissionNumber { get; }

	/// <summary>
	/// Offset 12 — <c>DAT_004a9ede</c>, <b>player invulnerable</b> when the file says exactly 1. It
	/// gates the whole of the damage write for the locally piloted machine, so its components take
	/// nothing; its shields still absorb and still drain, because that happens before the write.
	/// </summary>
	public bool PlayerInvulnerable { get; }

	/// <summary>
	/// Reads the header from the start of a <c>script.dat</c>'s bytes; see <see cref="From"/>.
	/// </summary>
	public static ScriptDatHeader Read(ReadOnlySpan<byte> scriptDat) {
		if (scriptDat.Length < Size) {
			throw new InvalidDataException(
				$"script.dat is {scriptDat.Length} bytes; its header alone is {Size}.");
		}

		return From(new ScriptDat { HeaderBytes = scriptDat[..Size].ToArray() });
	}

	/// <summary>
	/// The engine's reading of a parsed <c>script.dat</c>'s header fields. The fields offsets 4 and
	/// 16 hold are left out — <c>DBSim_LoadScriptDat</c> zeroes the one at 4 before use, and 16 is
	/// unread. <see cref="ObjectiveType"/> is not the theater's: it is the mission layer's, and its
	/// reader is <c>Mech_BehaviourPlayerThink</c>.
	///
	/// <para>The two cheat flags are read as <c>== 1</c> rather than as "nonzero", which is how both
	/// of their readers spell the test.</para>
	/// </summary>
	public static ScriptDatHeader From(ScriptDat script) => new(
		script.TheaterIndex,
		script.ZoneIndex,
		script.ObjectiveType,
		script.TheaterVariant,
		System.Math.Clamp((int)script.Difficulty, 0, DifficultyLevels - 1),
		script.UnlimitedAmmunition == CheatEnabled,
		script.PlayerInvulnerable == CheatEnabled,
		script.TrainingMissionNumber);
}
