using System.Globalization;

namespace Herculan.Engine.Input;

/// <summary>
/// <c>data\keyjoy.cfg</c> — the four axis-sense switches, and the <b>only</b> part of the retail
/// input configuration that does not live in <c>prefs.cfg</c>.
///
/// <para><c>FUN_0045b78c</c> reads it once during input init with four
/// <c>GetPrivateProfileStringA</c> calls, all against section <c>[Keyjoy]</c>, and each is a plain
/// case-insensitive compare against the word <c>Reverse</c> — anything else, the shipped
/// <c>Default</c> included, leaves the flag clear. There is no writer: retail ships the file with
/// its own explanatory comments and expects the player to edit it by hand.</para>
/// </summary>
public sealed class KeyjoyConfig {
	/// <summary>Where the simulator keeps the file, relative to the game's <c>data</c> folder.</summary>
	public const string FileName = "keyjoy.cfg";

	private const string Section = "keyjoy";
	private const string ReverseWord = "reverse";

	/// <summary>
	/// <c>Tilt</c> (<c>DAT_0049eab8</c>) — inverts the <b>keyboard's</b> turret-pitch axis, the second
	/// axis pair's y. The file's own comment puts it as which way keypad 8 tilts the turret. It is
	/// applied unconditionally, so it reaches a keyboard pilot whether or not a stick is attached.
	/// </summary>
	public bool ReverseTilt { get; init; }

	/// <summary>
	/// <c>Backturn</c> (<c>DAT_0049eabc</c>) — inverts the steering axis while the throttle axis is
	/// positive, which is to say while backing up. The file's comment describes it as which way
	/// keypad 3 turns while reversing. Applied last of all, to the already-combined axes.
	/// </summary>
	public bool ReverseBackturn { get; init; }

	/// <summary>
	/// <c>Missile</c> (<c>DAT_0049eac0</c>) — inverts the pitch axis inside the missile camera
	/// (<c>DAT_004d25aa</c>). This engine has no missile camera, so it is carried and not applied.
	/// </summary>
	public bool ReverseMissile { get; init; }

	/// <summary>
	/// <c>Rudder</c> (<c>DAT_0049eac4</c>) — inverts the joystick's rudder axis, and only when the
	/// device reports having one. The file's comment puts it as which way the left pedal turns.
	/// </summary>
	public bool ReverseRudder { get; init; }

	/// <summary>Every switch at its shipped setting, which is what a missing file also produces.</summary>
	public static readonly KeyjoyConfig Defaults = new();

	/// <summary>
	/// Reads the file, or returns <see cref="Defaults"/> when it is absent or unreadable — which is
	/// what the original does too, <c>GetPrivateProfileStringA</c> falling back to the default string
	/// it is handed rather than failing.
	/// </summary>
	public static KeyjoyConfig Load(string path) {
		Dictionary<string, string> values;
		try {
			values = ReadSection(File.ReadAllLines(path), Section);
		} catch (IOException) {
			return Defaults;
		} catch (UnauthorizedAccessException) {
			return Defaults;
		}

		bool Reverse(string key) =>
			values.TryGetValue(key, out string? value)
			&& string.Equals(value, ReverseWord, StringComparison.OrdinalIgnoreCase);

		return new KeyjoyConfig {
			ReverseTilt = Reverse("tilt"),
			ReverseBackturn = Reverse("backturn"),
			ReverseMissile = Reverse("missile"),
			ReverseRudder = Reverse("rudder"),
		};
	}

	/// <summary>
	/// One <c>[section]</c>'s <c>key = value</c> pairs, lowercased keys, in the shape
	/// <c>GetPrivateProfileString</c> would see them: <c>;</c> starts a comment, whitespace around
	/// both halves is dropped, and a repeated key keeps the first.
	/// </summary>
	internal static Dictionary<string, string> ReadSection(IEnumerable<string> lines, string section) {
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		bool inSection = false;

		foreach (string raw in lines) {
			string line = raw.Trim();
			int comment = line.IndexOf(';');
			if (comment >= 0) {
				line = line[..comment].TrimEnd();
			}

			if (line.Length == 0) {
				continue;
			}

			if (line[0] == '[') {
				int close = line.IndexOf(']');
				inSection = close > 1
					&& string.Equals(line[1..close].Trim(), section, StringComparison.OrdinalIgnoreCase);
				continue;
			}

			if (!inSection) {
				continue;
			}

			int equals = line.IndexOf('=');
			if (equals <= 0) {
				continue;
			}

			string key = line[..equals].Trim();
			if (key.Length > 0 && !values.ContainsKey(key)) {
				values[key] = line[(equals + 1)..].Trim();
			}
		}

		return values;
	}

	/// <summary>An integer setting, or <paramref name="fallback"/> when it is absent or unparseable.</summary>
	internal static int IntValue(Dictionary<string, string> values, string key, int fallback) =>
		values.TryGetValue(key, out string? value)
		&& int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
			? parsed
			: fallback;

	/// <summary>A boolean setting, taking <c>1</c>, <c>yes</c> and <c>on</c> alongside <c>true</c>.</summary>
	internal static bool BoolValue(Dictionary<string, string> values, string key, bool fallback) =>
		values.TryGetValue(key, out string? value)
			? value.Trim().ToLowerInvariant() switch {
				"1" or "true" or "yes" or "on" => true,
				"0" or "false" or "no" or "off" => false,
				_ => fallback,
			}
			: fallback;
}
