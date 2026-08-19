using System.Text.Json;
using System.Text.Json.Serialization;

namespace HercWorks.UI;

/// <summary>
/// Persistent user settings for the app itself (not for any game file), stored as JSON at
/// <c>%APPDATA%\HercWorks\settings.json</c>.
///
/// <para>Per-user AppData rather than a file next to the executable: the app is routinely run from
/// a build output folder that gets wiped, and an install under Program Files isn't writable by a
/// standard user. JSON rather than INI/registry: it round-trips through System.Text.Json with no
/// extra dependency and stays readable/hand-editable as more settings are added.</para>
///
/// <para>Loaded once on first access to <see cref="Current"/> and written back explicitly by
/// <see cref="Save"/>; there's no file watching, so a hand edit made while the app is running is
/// overwritten by the next save.</para>
/// </summary>
public sealed class AppSettings {
	private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

	private static AppSettings? _current;

	/// <summary>The single loaded settings instance for this process.</summary>
	public static AppSettings Current => _current ??= Load();

	[JsonIgnore]
	public static string SettingsPath { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HercWorks", "settings.json");

	/// <summary>
	/// The Earthsiege 2 install directory — the folder holding ES.EXE. Null until the user has
	/// pointed the app at their install; see <see cref="GamePaths"/> for everything resolved
	/// relative to it.
	/// </summary>
	public string? GameDirectory { get; set; }

	private static AppSettings Load() {
		try {
			if (File.Exists(SettingsPath)) {
				return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
			}
		} catch (Exception) {
			// A missing, unreadable or corrupt settings file must never block startup — fall back to
			// defaults and let the next Save replace it.
		}

		return new AppSettings();
	}

	public void Save() {
		try {
			Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
			File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, SerializerOptions));
		} catch (Exception ex) {
			MessageBox.Show($"Settings could not be saved to\n{SettingsPath}\n\n{ex.Message}",
				"Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}
}
