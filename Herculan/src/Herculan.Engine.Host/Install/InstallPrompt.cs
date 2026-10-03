using Herculan.Engine.Content;
using Herculan.Engine.Host.Localization;
using ImGuiNET;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace Herculan.Engine.Host.Install;

/// <summary>
/// The window the host opens when <see cref="GameInstall.Locate"/> finds no install: it asks the player for
/// the Earthsiege 2 folder, by the platform's own folder picker or by a typed path, and accepts only a folder
/// <see cref="GameInstall.IsInstallRoot"/> takes. Retail has no equivalent; its installer wrote the path for
/// it. The Settings menu asks the same way (<see cref="Create"/>).
/// </summary>
static class InstallPrompt {
	private const int WindowWidth = 640;
	private const int WindowHeight = 220;

	/// <summary>
	/// The prompt's body: <c>install_prompt</c>'s strings, refusing any folder that is not an install.
	/// <paramref name="dismissKey"/> is the string of the button that gives up, and <paramref name="messageKey"/>
	/// the message's, when not the startup's own.
	/// </summary>
	public static FolderPrompt Create(LocalizationTable localization, string dismissKey, string initialPath = "",
			string? messageKey = null) =>
		new(localization, "install_prompt", dismissKey, path => GameInstall.IsInstallRoot(path)
			? null
			: string.Format(localization.GetString("install_prompt.not_an_install") ?? "install_prompt.not_an_install",
				path, GameInstall.ArchiveFolderName), initialPath, messageKey);

	/// <summary>
	/// Shows the prompt until the player picks an install or gives up. Returns the install root, or null
	/// when the player chose Quit or closed the window. <paramref name="fontPath"/> is the ImGui font.
	/// </summary>
	public static string? Run(LocalizationTable localization, string fontPath) {
		var prompt = Create(localization, "install_prompt.quit");
		using var window = new EngineWindow(localization.GetString("install_prompt.window_title") ?? "install_prompt.window_title",
			WindowWidth, WindowHeight);

		ImGuiController? imgui = null;
		window.Load += (gl, input) => {
			imgui = new ImGuiController(gl, window.View, input, new ImGuiFontConfig(fontPath, 16));
		};

		window.Render += (deltaSeconds, gl) => {
			if (imgui == null) {
				return;
			}

			imgui.Update((float)deltaSeconds);
			gl.ClearColor(0.1f, 0.1f, 0.1f, 1f);
			gl.Clear(ClearBufferMask.ColorBufferBit);

			var viewport = ImGui.GetMainViewport();
			ImGui.SetNextWindowPos(viewport.WorkPos);
			ImGui.SetNextWindowSize(viewport.WorkSize);
			ImGui.Begin("##install", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
				| ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
			var outcome = prompt.Draw(window.View.Native?.Win32?.Hwnd ?? 0, GameInstall.ArchiveFolderName);
			ImGui.End();
			if (outcome != FolderPrompt.Outcome.Open) {
				window.Close();
			}

			imgui.Render();
		};

		window.Closing += () => {
			imgui?.Dispose();
			imgui = null;
		};

		window.Run();
		return prompt.Chosen;
	}
}
