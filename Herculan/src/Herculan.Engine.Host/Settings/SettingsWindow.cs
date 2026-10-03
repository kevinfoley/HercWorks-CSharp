using System.Numerics;
using HercWorks.Core.Data.File.Cfg;
using Herculan.Engine.Content;
using Herculan.Engine.Host.Install;
using ImGuiNET;

namespace Herculan.Engine.Host.Settings;

/// <summary>
/// The floating "Settings" window opened from the menu bar: where the install is, which folder
/// <c>data\drive.cfg</c> names as the disc, and the two languages. Retail has no such screen: its installer
/// wrote <c>drive.cfg</c> and <c>language.cfg</c>, and nothing changed them after.
///
/// <para>The two folders are read once per shell turn — the archives are mounted from them as the turn
/// starts (<see cref="GameContent.MountShell"/>) — so changing either brings the shell back up through
/// <c>restartShell</c>. A mission has none to give, and there both are greyed. The install is asked for with
/// the startup's own prompt (<see cref="InstallPrompt"/>); the disc's choice is written into the install's
/// <c>drive.cfg</c> (<see cref="GameInstall.WriteDiscDirectory"/>), where the original game reads it too.</para>
///
/// <para>The language rows show the current choices, greyed until the language can be changed
/// (docs/engine/handoff-language-selection.md).</para>
/// </summary>
sealed class SettingsWindow {
	private const float PanelWidth = 520f;
	private static readonly Vector2 PromptSize = new(640f, 220f);
	private const string PromptId = "##folder_prompt";

	private readonly HostSession _session;
	private readonly Action? _restartShell;
	private FolderPrompt? _prompt;
	private bool _promptIsDisc;
	private string? _error;

	/// <param name="restartShell">Brings the shell back up on the new folders; null in a mission. It is called
	/// between this window's <c>Begin</c> and <c>End</c>, so it must not tear down the ImGui context there.</param>
	public SettingsWindow(HostSession session, Action? restartShell) {
		_session = session;
		_restartShell = restartShell;
	}

	/// <summary>Whether the window is up. Set by the menu bar.</summary>
	public bool IsOpen { get; set; }

	/// <summary>Takes the window down, and the folder prompt over it with nothing changed.</summary>
	public void Close() {
		_prompt = null;
		_error = null;
		IsOpen = false;
	}

	/// <summary>Draws the window, if it is up. <paramref name="owner"/> is the native window a folder picker belongs to.</summary>
	public void Draw(nint owner) {
		if (!IsOpen) {
			return;
		}

		ImGui.SetNextWindowSize(new Vector2(PanelWidth, 0f));
		ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

		bool stayOpen = true;
		if (ImGui.Begin(Text("settings.title") + "###settings", ref stayOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize)) {
			ImGui.SeparatorText(Text("settings.folders"));
			string installRoot = _session.InstallRoot;
			if (FolderRow(Text("settings.install_folder"), installRoot, "##change_install")) {
				OpenPrompt(InstallPrompt.Create(_session.Localization, "settings.cancel", installRoot,
					"settings.install_message"), disc: false);
			}

			string? disc = GameInstall.DiscDirectory(installRoot);
			if (FolderRow(Text("settings.disc_folder"), disc ?? Text("settings.no_disc"), "##change_disc")) {
				OpenPrompt(new FolderPrompt(_session.Localization, "disc_prompt", "settings.cancel", DiscRefusal, disc ?? ""),
					disc: true);
			}

			if (_error != null) {
				ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.45f, 0.4f, 1f));
				ImGui.TextWrapped(_error);
				ImGui.PopStyleColor();
			}

			ImGui.SeparatorText(Text("settings.languages"));
			LanguageRow(Text("settings.game_language"), OnlineManual.Language(installRoot).Folder);
			LanguageRow(Text("settings.interface_language"), _session.Localization.SelectedLocale);

			ImGui.Separator();
			if (ImGui.Button(Text("settings.close"), new Vector2(ImGui.GetContentRegionAvail().X, 0f))) {
				stayOpen = false;
			}

			DrawPrompt(owner);
		}

		ImGui.End();

		if (!stayOpen) {
			Close();
		}
	}

	// A folder's label, its path and a Change button, which only a shell turn offers.
	private bool FolderRow(string label, string path, string buttonId) {
		ImGui.TextUnformatted(label);
		ImGui.TextDisabled(path);
		ImGui.BeginDisabled(_restartShell == null);
		bool clicked = ImGui.Button(Text("settings.change") + buttonId);
		ImGui.EndDisabled();
		if (_restartShell == null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
			ImGui.SetTooltip(Text("settings.in_mission"));
		}

		return clicked;
	}

	private void LanguageRow(string label, string current) {
		ImGui.BeginDisabled();
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.5f);
		if (ImGui.BeginCombo(label, current)) {
			ImGui.EndCombo();
		}
		ImGui.EndDisabled();
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
			ImGui.SetTooltip(Text("settings.language_unavailable"));
		}
	}

	private void OpenPrompt(FolderPrompt prompt, bool disc) {
		_prompt = prompt;
		_promptIsDisc = disc;
		_error = null;
		ImGui.OpenPopup(PromptId);
	}

	// The prompt as a modal over the window, the size of the startup's own.
	private void DrawPrompt(nint owner) {
		if (_prompt == null) {
			return;
		}

		ImGui.SetNextWindowSize(PromptSize);
		ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
		if (!ImGui.BeginPopupModal(PromptId, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove)) {
			return;
		}

		var outcome = _promptIsDisc
			? _prompt.Draw(owner, Drive.FileName)
			: _prompt.Draw(owner, GameInstall.ArchiveFolderName);
		if (outcome != FolderPrompt.Outcome.Open) {
			ImGui.CloseCurrentPopup();
		}

		ImGui.EndPopup();

		if (outcome == FolderPrompt.Outcome.Accepted && _prompt.Chosen is { } chosen) {
			_prompt = null;
			Apply(chosen);
		} else if (outcome == FolderPrompt.Outcome.Dismissed) {
			_prompt = null;
		}
	}

	// The install becomes the next turn's and is remembered for the next launch; the disc is written into the
	// install's drive.cfg. Either way the shell comes back up on it.
	private void Apply(string folder) {
		if (_promptIsDisc) {
			try {
				GameInstall.WriteDiscDirectory(_session.InstallRoot, folder);
			} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
				_error = string.Format(Text("settings.write_failed"), Drive.FileName, ex.Message);
				return;
			}
		} else {
			_session.InstallRoot = folder;
			GameInstall.Remember(folder);
		}

		Close();
		_restartShell?.Invoke();
	}

	private string? DiscRefusal(string path) => GameInstall.CheckDiscDirectory(path) switch {
		null => null,
		GameInstall.DiscDirectoryProblem.Missing => string.Format(Text("disc_prompt.missing"), path),
		GameInstall.DiscDirectoryProblem.Whitespace => string.Format(Text("disc_prompt.whitespace"), path, Drive.FileName),
		_ => string.Format(Text("disc_prompt.not_latin1"), path, Drive.FileName),
	};

	private string Text(string key) => _session.Localization.GetString(key) ?? key;
}
