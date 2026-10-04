using HercWorks.Core.Data.File.Sav;
using Herculan.Engine.Content;
using Herculan.Engine.Host.Shell.Tabs;
using Herculan.Engine.Numerics;
using Herculan.Engine.Shell;
using Herculan.Engine.Sim;
using Herculan.Engine.World;

namespace Herculan.Engine.Host.Shell;

/// <summary>
/// The shell's side of the campaign loop (docs/retail/shell/campaign-loop.md): where the game in progress comes from — a
/// slot, a new career, a training launch, the simulator's return — and the missions it hands the simulator.
/// </summary>
sealed class CampaignLoop {
	private readonly string _installRoot;
	private readonly GameContent _content;
	private readonly GameInProgress _game;
	private readonly ShellSaveScreen _saveScreen;
	private readonly HangarTabs _hangar;
	private readonly MissionTabScreens _mission;
	private readonly TabNavigation _navigation;
	private readonly StartupScreen _startup;
	private readonly ShellMovies _movies;
	private readonly ShellCanvas _canvas;
	private readonly ShellDialogs _dialogs;
	private readonly ShellScreen _screen;
	private readonly ShellOutcome _outcome;
	private readonly FrontEndWindow _window;
	private readonly Action _repaint;

	// VSHELL's one generator, seeded once at startup, and the row-2 flag clear list, which the
	// original keeps from one mission load to the next.
	private readonly SimRandom _random = ShellTrainingLaunch.StartupRandom();
	private readonly short[] _clearList = new short[HercWorks.Core.Io.Transform.Common.MissionGenerator.ClearListLength];

	// INSTANT ACTION's InstantAction_Active (0047363c), which nothing clears.
	private bool _instantActionSet;

	public CampaignLoop(string installRoot, GameContent content, GameInProgress game, ShellSaveScreen saveScreen, HangarTabs hangar,
			MissionTabScreens mission, TabNavigation navigation, StartupScreen startup, ShellMovies movies, ShellCanvas canvas,
			ShellDialogs dialogs, ShellScreen screen, ShellOutcome outcome, FrontEndWindow window, WidgetEvents widgets,
			Action repaint) {
		_installRoot = installRoot;
		_content = content;
		_game = game;
		_saveScreen = saveScreen;
		_hangar = hangar;
		_mission = mission;
		_navigation = navigation;
		_startup = startup;
		_movies = movies;
		_canvas = canvas;
		_dialogs = dialogs;
		_screen = screen;
		_outcome = outcome;
		_window = window;
		_repaint = repaint;

		widgets.Handle(ShellWidgetKind.ReplayButton, widget => ClickReplay((ShellReplayButton)widget.Index));
	}

	/// <summary>
	/// The folder the launch handoff is written to. The original writes it over the install's own
	/// <c>data</c> folder; this engine leaves the install's copies as they are and writes a scratch folder
	/// instead, which is this engine's choice.
	/// </summary>
	public static string HandoffDirectory => Path.Combine(Path.GetTempPath(), "herculan-launch");

	/// <summary>
	/// Where a new career's mission load writes the three working files the original writes into the
	/// install's <c>data</c> folder (<see cref="ShellWorkingFiles"/>). A scratch folder for the same reason
	/// as <see cref="HandoffDirectory"/>, which is this engine's choice.
	/// </summary>
	private static string CareerDirectory => Path.Combine(Path.GetTempPath(), "herculan-career");

	/// <summary>Sets InstantAction_Active (0047363c), which the training launches read and nothing clears.</summary>
	public void SetInstantAction() => _instantActionSet = true;

	/// <summary>
	/// Game_LoadSlot (0040e4f2): a slot in use read in whole — the hangar, the career and its mission, and
	/// the game in progress that saving needs. Slot 10 is slot 11 in training, as it is to Game_SaveSlot. The
	/// mission map is rebuilt, here on the briefing's next visit, and the briefing's and debrief's movies play
	/// again (DAT_004778ab and DAT_004778ac cleared). Returns false for a slot not in use, which the original
	/// refuses, or one that cannot be read.
	/// </summary>
	public bool LoadSlot(int slot) {
		if (slot == GameInProgress.CurrentGameSlot && _game.Mode == ShellCampaignMode.Training) {
			slot = GameInProgress.CurrentGameSlot + 1;
		}

		if (_saveScreen.Slots.ElementAtOrDefault(slot) is not { InUse: true } entry
				|| ShellSaveSlots.LoadSave(_installRoot, entry.FileName) is not { } restored) {
			return false;
		}

		Adopt(restored, ShellHangar.From(restored), ShellWorkingFiles.ForSlot(_installRoot, slot));
		Console.WriteLine($"Loaded {entry.FileName}: "
			+ (ShellSaveSummary.From(restored) is { } summary
				? $"{summary.PilotName}, sector {summary.Sector}, mission {summary.Mission + 1}."
				: "no pilot record.")
			+ (_hangar.RepairBay >= 0
				? $" Repair opens on bay {_hangar.RepairBay}."
				: " No built machine in any hangar bay."));
		return true;
	}

	/// <summary>
	/// Shell_BuildScreensAndStart's -X3 and -X4 arm (004012b0): Game_LoadSlot(10) and then
	/// Game_ProcessMissionResults (0040eae7) over the results.dat and mission.var the simulator left beside the
	/// handoff, then wherever the debrief goes next (docs/retail/shell/campaign-loop.md#where-the-debrief-goes-next).
	/// A slot 10 not in use, or no results, cannot come from a mission this shell launched; it is reported,
	/// and the menu comes up.
	/// </summary>
	public void ReturnFromMission() {
		string resultsPath = Path.Combine(HandoffDirectory, MissionResults.FileName);
		string countersPath = Path.Combine(HandoffDirectory, MissionLoader.CountersFileName);
		if (!LoadSlot(GameInProgress.CurrentGameSlot) || _game.LoadedGame == null || !File.Exists(resultsPath) || !File.Exists(countersPath)) {
			Console.WriteLine($"Back from the mission, but slot 10 or {resultsPath} could not be read — main menu.");
			_startup.Begin();
			return;
		}

		var result = ShellDebrief.Process(_game.LoadedGame, _game.Hangar, _content, File.ReadAllBytes(countersPath),
			File.ReadAllBytes(resultsPath), _game.Mode == ShellCampaignMode.Campaign, _game.Options[GameInProgress.RepairOption],
			_game.ManualWeaponBuild(), bound => _random.NextBelow(bound), out string? failure);
		if (result == null) {
			Console.WriteLine($"Debrief: {failure} Main menu.");
			_startup.Begin();
			return;
		}

		Console.WriteLine($"Debrief: {(result.Outcome != 0 ? "success" : "failure")}, {result.SalvageAwarded} kg salvage "
			+ $"and {result.SalvageItems} weapon(s) recovered, {result.MachinesScrapped} machine(s) scrapped, "
			+ $"{result.PilotsLost} pilot(s) lost; game state {result.State?.ToString() ?? "unchanged"}.");
		if (result.Report is { } report) {
			_mission.WriteReport(report, _canvas.Art.Text);
		}

		switch (result.State) {
			case ShellDebrief.CampaignOverState or ShellDebrief.ShellState:
				// ReplayDialog_Show(state) (0044ca57).
				_dialogs.Replay.Open(result.State.Value);
				break;
			case ShellDebrief.CampaignWonState:
				// Game_SaveSlot(10), the two ending movies, palette 1 and the startup sequence.
				_game.AutoSave();
				_movies.Queue.Enqueue(ShellMovieQueue.Victory, ShellMovieQueue.FullRect);
				_movies.Queue.Enqueue(ShellMovieQueue.Credits, ShellMovieQueue.FullRect);
				_movies.Start();
				_canvas.InstallPalette(ShellPalette.ServiceBay);
				_startup.Begin();
				break;
			case ShellDebrief.NextMissionState:
				// MissionScreenView = 4, then Career_StartMissionLoad's Use Default: the next mission's load,
				// whose campaign end puts the frame up and the mission tab in that view.
				_mission.ShowDebrief(result.Debrief is { } text ? ShellMissionTexts.AssembleDebrief(text) : null,
					result.Debrief?.Movie);
				LoadNextCareerMission();
				break;
			default:
				// A training debrief leaves the state alone and shows the startup sequence.
				_startup.Begin();
				break;
		}
	}

	/// <summary>
	/// ACCEPT, Registration_OnAccept (0043c0fb): gam\herc_inf.dat reloaded, the screen hidden, the campaign
	/// map's first-show flag (DAT_004778aa) cleared, Game_NewCareer(name, skill) in
	/// campaign mode, and MissionScreenView from the position — the map, on stage 1 mission 0. The career's
	/// position step posts the developer's mission-name dialog's Use Default click, which the original
	/// delivers once the handler has returned and which runs Career_LoadCurrentMission; this goes straight
	/// there, as LaunchTraining does. Its campaign end rebuilds the map and the texts (here on the adopt),
	/// stages slot 10's summary, which no save row shows, puts the frame up with the strip regated, and
	/// calls Mission_ShowView(MissionScreenView, 1). Nothing is saved: slot 10 is first written by the next
	/// autosave.
	/// </summary>
	public void StartCampaign(string name, int skill) {
		_mission.ClearMapShown();

		int Roll(short bound) => _random.NextBelow(bound);
		if (ShellCampaignLaunch.NewCareer(_content, name, skill, ShellCampaignMode.Campaign, Roll,
				_game.HeldGame(), out string? failure) is not { } game) {
			Console.WriteLine($"Accept: {failure} No career started; main menu.");
			_repaint();
			return;
		}

		var careerHangar = ShellHangar.From(game);
		if (ShellCampaignLaunch.LoadCareerMission(CareerDirectory, _content, game, careerHangar, _clearList, Roll, out failure)
				is not { } mission) {
			Console.WriteLine($"Accept: {failure} No career started; main menu.");
			_repaint();
			return;
		}

		Adopt(game, careerHangar, ShellWorkingFiles.In(CareerDirectory));
		Console.WriteLine($"New campaign for {name}, skill {skill}: {mission.MissionPath}, "
			+ $"{mission.SquadPositions} squad position(s), {game.SalvageTotal} kg salvage; working files in {CareerDirectory}.");
		_screen.ReturnToFrame(_game.Mode);
		_navigation.ShowMissionView();
	}

	/// <summary>
	/// Game_NewCareer("TRAINEE", option 0x27) in training mode on stage 0's mission at row: the career
	/// started, its mission loaded and the handoff written, and the shell closed on exit code 2. The
	/// original gets from the career to the load through the developer's mission-name dialog, which
	/// clicks its own Use Default at once; this goes straight there. The career is the game in progress,
	/// which the loop exit's autosave writes as slot 11 with the handoff's three working files. Returns
	/// whether it launched.
	/// </summary>
	public bool LaunchTraining(int row, string label) {
		var handoff = ShellTrainingLaunch.Write(HandoffDirectory, _content, _game.Options, row, _instantActionSet,
			_random, _clearList, _game.HeldGame(), out string? failure);
		if (handoff == null) {
			Console.WriteLine($"{label}: {failure}");
			return false;
		}

		Adopt(handoff.Game, handoff.Hangar, ShellWorkingFiles.In(HandoffDirectory));

		var squad = Enumerable.Range(0, ShellHangar.BayCount)
			.Select(bay => handoff.Hangar.Bay(bay) is { } machine
				? $"bay {bay} chassis {machine.ChassisType}" + (handoff.Hangar.PilotFor(bay) is { } pilot ? $" ({pilot.Name})" : string.Empty)
				: null)
			.OfType<string>();
		Console.WriteLine($"{label} — {handoff.MissionPath}, {handoff.SquadPositions} squad position(s): "
			+ $"{string.Join(", ", squad)}; {handoff.Hangar.MachinesOnStrength} machine(s) going. "
			+ $"Handoff written to {HandoffDirectory}; launching the mission.");
		_outcome.Launch = new ShellLaunch(handoff.ScriptPath, Path.Combine(_installRoot, MissionLoader.DataFolderName));
		_window.Close();
		return true;
	}

	// Career_LoadCurrentMission's campaign load after a debrief, as StartCampaign runs it for a new career.
	private void LoadNextCareerMission() {
		if (_game.LoadedGame == null) {
			return;
		}

		var game = _game.LoadedGame;
		var hangar = _game.Hangar;
		if (ShellCampaignLaunch.LoadCareerMission(CareerDirectory, _content, game, hangar, _clearList,
				bound => _random.NextBelow(bound), out string? failure) is not { } mission) {
			Console.WriteLine($"Next mission: {failure} Main menu.");
			_mission.DropDebrief();
			_startup.Begin();
			return;
		}

		Adopt(game, hangar, ShellWorkingFiles.In(CareerDirectory));
		Console.WriteLine($"Next mission: {mission.MissionPath}, {mission.SquadPositions} squad position(s); "
			+ $"working files in {CareerDirectory}.");
		_screen.ReturnToFrame(_game.Mode);
		_navigation.ShowMissionView();
	}

	// A REPLAY MISSION? button. No (ReplayDialog_OnNo, 0044cbbd) saves slot 10, takes the dialog down and
	// shows the main menu. Yes (ReplayDialog_OnYes, 0044cb44) takes it down, loads slot 10 again, and ends
	// the shell on exit code 2 — so the simulator flies what the load's Career_LoadSlot copied in: the
	// slot's script.dat, mission.str and player.mec, beside the mission.var the simulator itself last wrote,
	// which no one rewrites.
	private void ClickReplay(ShellReplayButton button) {
		_dialogs.Replay.Close();
		if (button == ShellReplayButton.No) {
			_game.AutoSave();
			_repaint();
			return;
		}

		if (!LoadSlot(GameInProgress.CurrentGameSlot)) {
			Console.WriteLine("Replay: slot 10 could not be read — main menu.");
			_repaint();
			return;
		}

		Directory.CreateDirectory(HandoffDirectory);
		var workingFiles = _game.WorkingFiles;
		var handoff = ShellWorkingFiles.In(HandoffDirectory);
		foreach (var (from, to) in new[] {
				(workingFiles.Script, handoff.Script), (workingFiles.Text, handoff.Text), (workingFiles.Player, handoff.Player) }) {
			if (from != null && to != null && File.Exists(from)) {
				File.Copy(from, to, overwrite: true);
			}
		}

		_game.WorkingFiles = handoff;
		_outcome.Launch = new ShellLaunch(handoff.Script!, Path.Combine(_installRoot, MissionLoader.DataFolderName));
		Console.WriteLine($"Replay: yes — slot 10's mission copied to {HandoffDirectory}; launching it.");
		_window.Close();
	}

	// The game in progress from here on, from a load or a new career — maybe_HasGameInProgress (0048260a) set, the briefing's and
	// debrief's movies to play again (DAT_004778ab and DAT_004778ac cleared), and the mission map rebuilt on
	// the briefing's next visit.
	private void Adopt(PlayerSave game, ShellHangar hangar, ShellWorkingFiles files) {
		_game.Adopt(game, hangar, files);
		_mission.OnAdopted(files, game);
		_hangar.OnAdopted();
	}
}
