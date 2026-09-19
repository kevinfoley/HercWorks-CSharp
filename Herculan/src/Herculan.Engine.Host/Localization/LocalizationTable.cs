namespace Herculan.Engine.Host.Localization;

/// <summary>
/// Loads the Tweaks menu's localized strings from <c>Localization/&lt;locale&gt;.lang</c> next to
/// the executable. That folder ships as loose files rather than embedded resources specifically so a
/// player can add or edit a <c>.lang</c> file after install — see the <c>CopyToOutputDirectory</c> item
/// in <c>Herculan.Engine.Host.csproj</c>.
/// </summary>
public class LocalizationTable {
	public const string LocalizationFileExtension = ".lang";
	public const string DefaultLocale = "en";

	private static readonly string LocaleFolder = Path.Combine(AppContext.BaseDirectory, "Localization");

	/// <summary>Where the player's chosen locale is remembered — just the one string, so a whole
	/// settings file would be more ceremony than the value needs.</summary>
	private static readonly string SelectedLocaleFile = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
			Environment.SpecialFolderOption.DoNotVerify),
		"Herculan", "selected-locale.txt");

	private Dictionary<string, string> _keyValuePairs = new();
	private string _selectedLocale = DefaultLocale;

	public string SelectedLocale {
		get => _selectedLocale;
		set => Apply(value, persist: true);
	}

	public LocalizationTable() {
		Apply(LoadSelectedLocale() ?? DefaultLocale, persist: false);
	}

	/// <summary>
	/// Switches to <paramref name="locale"/>, falling back to <see cref="DefaultLocale"/> (with a
	/// warning) if there is no <c>.lang</c> file for it. A default that fails to load is a harder
	/// error — there would be nothing to show the player in any language — so that case throws instead.
	/// </summary>
	private void Apply(string locale, bool persist) {
		if (!GetLocales().Contains(locale)) {
			Console.Error.WriteLine($"No localization file for '{locale}'; falling back to '{DefaultLocale}'.");
			locale = DefaultLocale;
		}

		if (!LoadFromDisk(PathFor(locale)) && locale == DefaultLocale) {
			throw new InvalidOperationException(
				$"Default localization file '{PathFor(DefaultLocale)}' is missing or unreadable — there would be no strings to show at all.");
		}

		_selectedLocale = locale;
		if (persist) {
			SaveSelectedLocale(locale);
		}
	}

	private static string PathFor(string locale) => Path.Combine(LocaleFolder, locale + LocalizationFileExtension);

	private bool LoadFromDisk(string path) {
		try {
			string[] lines = File.ReadAllLines(path);
			var loaded = new Dictionary<string, string>(lines.Length);
			foreach (string line in lines) {
				int spaceIndex = line.IndexOf(' ');
				if (spaceIndex > 0) {
					string key = line[..spaceIndex];
					string value = line[(spaceIndex + 1)..];
					if (loaded.ContainsKey(key)) {
						Console.Error.WriteLine($"Warning: key {key} appears multiple times in localization file {path}. " +
							"Only the last entry will be kept.");
					}

					loaded[key] = value;
				}
			}

			_keyValuePairs = loaded;
			return true;
		} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
			// Not shown with ImGui: this runs from the constructor, before the host has an ImGui frame
			// (or even a GL context) open to draw one into. A caller with a frame loop to hook into —
			// once one exists — can check a failed load some other way and raise its own panel; this
			// class only logs.
			Console.Error.WriteLine($"Could not read {path} ({ex.Message}).");
			return false;
		}
	}

	public string? GetString(string key) => _keyValuePairs.GetValueOrDefault(key);

	/// <summary>Every locale with a <c>.lang</c> file in <see cref="LocaleFolder"/>, e.g. <c>"en"</c>
	/// for <c>Localization/en.lang</c>.</summary>
	public string[] GetLocales() {
		if (!Directory.Exists(LocaleFolder)) {
			return Array.Empty<string>();
		}

		return Directory.GetFiles(LocaleFolder, "*" + LocalizationFileExtension)
			.Select(Path.GetFileNameWithoutExtension)
			.ToArray()!;
	}

	private static string? LoadSelectedLocale() {
		try {
			return File.Exists(SelectedLocaleFile) ? File.ReadAllText(SelectedLocaleFile).Trim() : null;
		} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
			Console.Error.WriteLine($"Could not read {SelectedLocaleFile} ({ex.Message}); using default locale.");
			return null;
		}
	}

	private static void SaveSelectedLocale(string locale) {
		try {
			Directory.CreateDirectory(Path.GetDirectoryName(SelectedLocaleFile)!);
			File.WriteAllText(SelectedLocaleFile, locale);
		} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
			Console.Error.WriteLine($"Could not write {SelectedLocaleFile}: {ex.Message}");
		}
	}
}
