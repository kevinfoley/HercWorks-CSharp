using Herculan.Engine.Host.Localization;

namespace Herculan.Engine.Host;

/// <summary>
/// What outlives one turn of the shell or one mission: the install, which the Settings menu can change for
/// the next turn, the player's interface strings and the font every ImGui window is drawn in.
/// </summary>
sealed class HostSession(string installRoot, LocalizationTable localization, string imguiFontPath) {
	/// <summary>The install the next shell turn and mission read; <see cref="Settings.SettingsWindow"/> changes it.</summary>
	public string InstallRoot { get; set; } = installRoot;

	public LocalizationTable Localization { get; } = localization;

	public string ImGuiFontPath { get; } = imguiFontPath;
}
