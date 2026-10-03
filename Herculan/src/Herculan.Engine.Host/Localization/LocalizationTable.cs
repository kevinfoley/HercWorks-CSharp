namespace Herculan.Engine.Host.Localization;

/// <summary>
/// Loads HERCULAN's own interface strings from <c>Localization/&lt;locale&gt;.lang</c> next to
/// the executable. That folder ships as loose files rather than embedded resources specifically so a
/// player can add or edit a <c>.lang</c> file after install — see the <c>CopyToOutputDirectory</c> item
/// in <c>Herculan.Engine.Host.csproj</c>.
///
/// <para>Each line is a key, a space and the string. A file names its own language under
/// <see cref="LanguageNameKey"/>, in that language, for the Settings menu's list. A key a translation
/// lacks reads from <see cref="DefaultLocale"/>'s file, so a partial translation shows English for the
/// rest.</para>
/// </summary>
public class LocalizationTable {
	public const string LocalizationFileExtension = ".lang";
	public const string DefaultLocale = "en";

	/// <summary>The key under which a <c>.lang</c> file gives its language's name.</summary>
	public const string LanguageNameKey = "language.name";

	private static readonly string LocaleFolder = Path.Combine(AppContext.BaseDirectory, "Localization");

	/// <summary>Where the player's chosen locale is remembered — just the one string, so a whole
	/// settings file would be more ceremony than the value needs.</summary>
	private static readonly string SelectedLocaleFile = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
			Environment.SpecialFolderOption.DoNotVerify),
		"Herculan", "selected-locale.txt");

	private Dictionary<string, string> _keyValuePairs = new();
	private Dictionary<string, string>? _fallback;
	private string _selectedLocale = DefaultLocale;

	/// <summary>
	/// The locale whose strings are shown. Setting one that has no <c>.lang</c> file, or whose file cannot be
	/// read, keeps the current locale and logs why.
	/// </summary>
	public string SelectedLocale {
		get => _selectedLocale;
		set => Apply(value, persist: true);
	}

	/// <summary>
	/// Loads the locale last chosen, or <see cref="DefaultLocale"/> when none was or it no longer loads. A
	/// default that fails to load throws: there would be nothing to show the player in any language.
	/// </summary>
	public LocalizationTable() {
		if (LoadSelectedLocale() is { } saved && saved != DefaultLocale && Apply(saved, persist: false)) {
			return;
		}

		if (!Apply(DefaultLocale, persist: false)) {
			throw new InvalidOperationException(
				$"Default localization file '{PathFor(DefaultLocale)}' is missing or unreadable — there would be no strings to show at all.");
		}
	}

	// Switches to locale, with the default's strings behind it; false, with nothing changed, when its file is
	// missing or unreadable.
	private bool Apply(string locale, bool persist) {
		if (!GetLocales().Contains(locale)) {
			Console.Error.WriteLine($"No localization file for '{locale}'; keeping '{_selectedLocale}'.");
			return false;
		}

		if (Read(PathFor(locale)) is not { } strings) {
			return false;
		}

		_keyValuePairs = strings;
		_fallback = locale == DefaultLocale ? null : Read(PathFor(DefaultLocale));
		_selectedLocale = locale;
		if (persist) {
			SaveSelectedLocale(locale);
		}

		return true;
	}

	private static string PathFor(string locale) => Path.Combine(LocaleFolder, locale + LocalizationFileExtension);

	// The file's strings by key, or null when it cannot be read.
	private static Dictionary<string, string>? Read(string path) {
		try {
			string[] lines = File.ReadAllLines(path);
			var loaded = new Dictionary<string, string>(lines.Length);
			foreach (string line in lines) {
				int spaceIndex = line.IndexOf(' ');
				if (spaceIndex > 0) {
					string key = line[..spaceIndex];
					string value = line[(spaceIndex + 1)..].Trim();
					if (loaded.ContainsKey(key)) {
						Console.Error.WriteLine($"Warning: key {key} appears multiple times in localization file {path}. " +
							"Only the last entry will be kept.");
					}

					loaded[key] = value;
				}
			}

			return loaded;
		} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
			// Not shown with ImGui: this runs from the constructor, before the host has an ImGui frame
			// (or even a GL context) open to draw one into. A caller with a frame loop to hook into —
			// once one exists — can check a failed load some other way and raise its own panel; this
			// class only logs.
			Console.Error.WriteLine($"Could not read {path} ({ex.Message}).");
			return null;
		}
	}

	/// <summary>The string for <paramref name="key"/> in the selected locale, else in the default's; null when neither has it.</summary>
	public string? GetString(string key) => _keyValuePairs.GetValueOrDefault(key) ?? _fallback?.GetValueOrDefault(key);

	/// <summary>
	/// Every locale <see cref="GetLocales"/> finds, each with the name its own file gives under
	/// <see cref="LanguageNameKey"/>, or the locale itself when the file gives none. Reads every file, so a caller
	/// drawing the list each frame holds on to it.
	/// </summary>
	public IReadOnlyList<(string Locale, string Name)> GetLanguages() =>
		GetLocales()
			.Order(StringComparer.OrdinalIgnoreCase)
			.Select(locale => (locale, Read(PathFor(locale))?.GetValueOrDefault(LanguageNameKey) ?? locale))
			.ToList();

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
