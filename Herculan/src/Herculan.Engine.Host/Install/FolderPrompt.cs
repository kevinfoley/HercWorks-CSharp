using System.Numerics;
using Herculan.Engine.Host.Localization;
using ImGuiNET;

namespace Herculan.Engine.Host.Install;

/// <summary>
/// The body of a "choose a folder" prompt: a message, a path field with the platform's own folder picker
/// beside it (<see cref="NativeFolderPicker"/>), the reason the last choice was refused, and an accept and a
/// dismiss button at the foot. It draws into whatever ImGui window is current, so <see cref="InstallPrompt"/>
/// shows it as a window of its own and <see cref="Settings.SettingsWindow"/> as a modal.
/// </summary>
sealed class FolderPrompt {
	/// <summary>What one frame of the prompt left it at.</summary>
	public enum Outcome {
		Open,
		Accepted,
		Dismissed,
	}

	private readonly LocalizationTable _localization;
	private readonly string _keyPrefix;
	private readonly string _dismissKey;
	private readonly Func<string, string?> _refusal;
	private readonly string _messageKey;
	private string _path;
	private string? _error;
	private Task<string?>? _picking;

	/// <param name="keyPrefix">
	/// The prompt's strings: <c>message</c>, <c>path_hint</c>, <c>picker_title</c>, <c>use_folder</c> and
	/// <c>no_path</c> under it, beside the shared <c>install_prompt.browse</c>, <c>picker_open</c> and
	/// <c>picker_failed</c>.
	/// </param>
	/// <param name="dismissKey">The dismiss button's string.</param>
	/// <param name="refusal">Why a typed or picked folder cannot be taken, or null when it can.</param>
	/// <param name="initialPath">What the path field starts with.</param>
	/// <param name="messageKey">The message's string, in place of the prefix's own <c>message</c>.</param>
	public FolderPrompt(LocalizationTable localization, string keyPrefix, string dismissKey,
			Func<string, string?> refusal, string initialPath = "", string? messageKey = null) {
		_localization = localization;
		_keyPrefix = keyPrefix;
		_dismissKey = dismissKey;
		_refusal = refusal;
		_path = initialPath;
		_messageKey = messageKey ?? keyPrefix + ".message";
	}

	/// <summary>The folder accepted, a full path; set once <see cref="Draw"/> has returned <see cref="Outcome.Accepted"/>.</summary>
	public string? Chosen { get; private set; }

	/// <summary>
	/// Draws the prompt into the current window, filling it to its foot. <paramref name="owner"/> is the
	/// native window the picker belongs to. <paramref name="messageArguments"/> fill the message's slots.
	/// </summary>
	public Outcome Draw(nint owner, params object[] messageArguments) {
		var outcome = Outcome.Open;
		if (_picking is { IsCompleted: true } picked) {
			_picking = null;
			if (picked.IsCompletedSuccessfully && picked.Result is { } folder) {
				_path = folder;
				if (Accept()) {
					outcome = Outcome.Accepted;
				}
			} else if (picked.IsFaulted) {
				Console.Error.WriteLine($"The folder picker failed: {picked.Exception?.InnerException?.Message}");
				_error = Text("install_prompt.picker_failed");
			}
		}

		ImGui.TextWrapped(string.Format(Text(_messageKey), messageArguments));
		ImGui.Spacing();

		bool picking = _picking != null;
		ImGui.BeginDisabled(picking);

		string browseLabel = Text("install_prompt.browse");
		float spacing = ImGui.GetStyle().ItemSpacing.X;
		bool canBrowse = NativeFolderPicker.IsAvailable;
		float browseWidth = canBrowse
			? ImGui.CalcTextSize(browseLabel).X + ImGui.GetStyle().FramePadding.X * 2
			: 0f;

		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - (canBrowse ? browseWidth + spacing : 0f));
		if (ImGui.InputTextWithHint("##path", Text(_keyPrefix + ".path_hint"), ref _path, 1024,
				ImGuiInputTextFlags.EnterReturnsTrue) && Accept()) {
			outcome = Outcome.Accepted;
		}

		if (canBrowse) {
			ImGui.SameLine();
			if (ImGui.Button(browseLabel)) {
				_error = null;
				_picking = NativeFolderPicker.PickAsync(Text(_keyPrefix + ".picker_title"), _path, owner);
			}
		}

		if (_error != null) {
			ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.45f, 0.4f, 1f));
			ImGui.TextWrapped(_error);
			ImGui.PopStyleColor();
		} else if (picking) {
			ImGui.TextDisabled(Text("install_prompt.picker_open"));
		}

		// The two buttons sit at the foot of the window, half its width each.
		var buttonSize = new Vector2((ImGui.GetContentRegionAvail().X - spacing) * 0.5f, 0f);
		ImGui.SetCursorPosY(ImGui.GetWindowHeight() - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y);
		if (ImGui.Button(Text(_keyPrefix + ".use_folder"), buttonSize) && Accept()) {
			outcome = Outcome.Accepted;
		}
		ImGui.SameLine();
		if (ImGui.Button(Text(_dismissKey), buttonSize)) {
			outcome = Outcome.Dismissed;
		}

		ImGui.EndDisabled();
		return outcome;
	}

	/// <summary>Takes the path field's folder if <c>refusal</c> has nothing against it; otherwise says why not.</summary>
	private bool Accept() {
		string path = _path.Trim().Trim('"');
		_error = path.Length == 0 ? Text(_keyPrefix + ".no_path") : _refusal(path);
		if (_error != null) {
			return false;
		}

		Chosen = Path.GetFullPath(path);
		return true;
	}

	private string Text(string key) => _localization.GetString(key) ?? key;
}
