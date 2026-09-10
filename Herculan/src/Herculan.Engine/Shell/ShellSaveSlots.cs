using HercWorks.Core.Data.File.Sav;
using HercWorks.Core.Data.Struct.Vshell.Sav;
using HercWorks.Core.Io.Transform.Common;

namespace Herculan.Engine.Shell;

/// <summary>
/// What the save screen's detail panel prints about one slot — the 67-byte (<c>0x43</c>) staging
/// record VSHELL keeps eleven of, at <c>0048cddc</c>, and fills by opening each save in turn and
/// skipping to the fields it wants (<c>stats.cpp</c>, <c>00430378</c>).
///
/// <para><b>The record is a pilot record plus three fields.</b> Its first 59 bytes are laid out
/// exactly as <see cref="PilotEntry"/>, because the writer (<c>FUN_0043084c</c>) copies them field for
/// field out of the player's own record; the salvage pool, the sector and the mission counter follow
/// at <c>+0x3b</c>, <c>+0x3f</c> and <c>+0x41</c>. That is why the panel can show a pilot's name, skill
/// and rank beside a career position without reading anything else.</para>
///
/// <para>Built here from a parsed <see cref="PlayerSave"/> rather than by reproducing the original's
/// skip-walk: the walk exists because VSHELL wants eleven summaries without eleven full loads, and
/// this engine already has a whole-file reader. The walk is still worth knowing — its skip counts are
/// an independent statement of every block length in <c>docs/formats/save-games.md</c>.</para>
/// </summary>
public sealed record ShellSaveSummary(
	string PilotName,
	int Skill,
	int Rank,
	int HercKills,
	int FlyerKills,
	int BaseKills,
	int TotalHercKills,
	int TotalFlyerKills,
	int TotalBaseKills,
	int SalvageKilograms,
	int Sector,
	int Mission) {

	/// <summary>
	/// The summary of an already-parsed save. The sector and the mission come from the career block's
	/// first two shorts, which is where the original's scanner reads them too — it takes those two and
	/// then skips the remaining 148 bytes of the block.
	/// </summary>
	public static ShellSaveSummary? From(PlayerSave? save) {
		if (save?.PlayerPilot is not { } pilot) {
			return null;
		}

		short[] career = save.Unk4_stateFlags;
		return new ShellSaveSummary(
			pilot.Name ?? string.Empty,
			pilot.Skill?.Id ?? 0,
			pilot.Rank?.Id ?? 0,
			pilot.KillsHercs, pilot.KillsFlyers, pilot.KillsBuilding,
			pilot.TotalKillHerc, pilot.TotalKillFlyer, pilot.TotalKillBldng,
			save.SalvageTotal,
			career.Length > 0 ? career[0] : 0,
			career.Length > 1 ? career[1] : 0);
	}
}

/// <summary>
/// One row of the save screen's slot list: what <c>sav\GAMEFILE.STR</c> says about the slot, plus the
/// summary read out of the save itself when there is one.
/// </summary>
public sealed record ShellSaveSlot(string FileName, string Label, bool InUse, ShellSaveSummary? Summary);

/// <summary>
/// The twelve save slots, as the save screen sees them: the directory file, each slot's summary, and
/// the completion the directory's reader applies to a slot nobody has written yet.
///
/// <para>The screen shows ten of the twelve. Slots 10 and 11 are the campaign and training autosaves
/// and are one slot from the caller's side, reached by the resume path rather than by a row — which is
/// why the slot loop in every save-screen routine bounds at 10 while the summary scan bounds at
/// 11.</para>
/// </summary>
public static class ShellSaveSlots {
	/// <summary>The save subfolder inside an install root, beside <c>VOL</c>.</summary>
	public const string FolderName = "SAV";

	/// <summary>The slot directory's filename.</summary>
	public const string DirectoryFileName = "GAMEFILE.STR";

	/// <summary>
	/// <c>estext.bin</c> entry <c>0x21</c>, <c>EMPTY</c>: what the directory's reader appends to a label
	/// whose fifth character is NUL. A stored <c>" 8. "</c> becomes <c>" 8. EMPTY"</c>, which is why
	/// every retail label already reads that way — once such a slot is written back, the completed label
	/// is in the file. See <see cref="Complete"/>.
	/// </summary>
	public const int EmptyLabelText = 0x21;

	/// <summary>How many characters of a label are the stored <c>"N. "</c> prefix.</summary>
	private const int LabelPrefixLength = 4;

	/// <summary>The save folder inside an install root.</summary>
	public static string Directory(string installRoot) => Path.Combine(installRoot, FolderName);

	/// <summary>
	/// Reads the slot directory and every save it says is in use. Returns an empty list when there is no
	/// <c>GAMEFILE.STR</c> to read — the screen then draws its furniture and no rows, which is the same
	/// thing it does for a missing string table.
	/// </summary>
	public static IReadOnlyList<ShellSaveSlot> Load(string installRoot, ShellText? text = null) {
		string folder = Directory(installRoot);
		string path = Path.Combine(folder, DirectoryFileName);
		if (!File.Exists(path)
			|| new SaveSlotDirectoryTransform().Parse(ReadOrNull(path)) is not { } directory) {
			return Array.Empty<ShellSaveSlot>();
		}

		string? emptyWord = text?.Text(EmptyLabelText);
		var slots = new List<ShellSaveSlot>(directory.Slots.Count);
		foreach (var entry in directory.Slots) {
			slots.Add(new ShellSaveSlot(entry.FileName, Complete(entry.Label, emptyWord), entry.InUse,
				entry.InUse ? ReadSummary(folder, entry.FileName) : null));
		}

		return slots;
	}

	/// <summary>
	/// The reader's label completion: a label whose fifth character is missing is a slot that has never
	/// been written, and the word for that comes from the string table rather than from the file. Done
	/// here rather than in the transformer because the toolkit has no <c>estext.bin</c> to reach.
	/// </summary>
	public static string Complete(string label, string? emptyWord) =>
		label.Length > LabelPrefixLength ? label : label + (emptyWord ?? string.Empty);

	private static ShellSaveSummary? ReadSummary(string folder, string fileName) {
		string path = Path.Combine(folder, fileName);
		if (!File.Exists(path)) {
			return null;
		}

		// A save that will not parse leaves the slot listed with no summary rather than dropping the row:
		// the row's label comes from the directory, which is a separate file and may well still be good.
		try {
			return ShellSaveSummary.From(new PlayerSaveTransform().Parse(ReadOrNull(path)));
		} catch (Exception) {
			return null;
		}
	}

	private static byte[]? ReadOrNull(string path) {
		try {
			return File.ReadAllBytes(path);
		} catch (IOException) {
			return null;
		} catch (UnauthorizedAccessException) {
			return null;
		}
	}
}
