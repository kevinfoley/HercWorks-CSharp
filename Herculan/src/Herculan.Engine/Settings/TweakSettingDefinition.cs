namespace Herculan.Engine.Settings;

public interface ISettingDefinition<T> {
	public string ID { get; }
	public string DisplayNameKey { get; }
	public string DescriptionKey { get; }
	public T DefaultValue { get; }
	public bool Hidden { get; }
}

public enum TweakCategory {
	Cosmetic, AI, Functional, Input
}

public interface ITweakSettingDefinition<T> : ISettingDefinition<T> {
	public TweakCategory Category { get; }
}

public class SettingDefinition<T> : ISettingDefinition<T> {
	public SettingDefinition(string id, T defaultValue, bool hidden = false) {
		ID = id;
		DefaultValue = defaultValue;
		Hidden = hidden;
	}

	/// <summary>
	/// Unique identifier, used for saving and loading the setting, and
	/// also as a prefix for localization keys.
	/// </summary>
	public string ID { get; }

	/// <summary>
	/// Key for the display name in localized strings.
	/// </summary>
	public string DisplayNameKey => $"{ID}.display_name";

	/// <summary>
	/// Key for the description in localized strings.
	/// </summary>
	public string DescriptionKey => $"{ID}.description";

	public T DefaultValue { get; }

	/// <summary>
	/// If true, this setting is not rendered in the settings UI. Use for
	/// future settings that have not been implemented in code yet.
	/// </summary>
	public bool Hidden { get; }
}

/// <summary>
/// The schema for one tweak setting — see <see cref="TweakSettingDefinitions"/> for the
/// full catalog and <see cref="TweakSettings"/> for where the chosen values live. A definition
/// carries no state of its own beyond its identity and default, so a single static instance is shared
/// by every <see cref="TweakSettings"/> instance and doubles as the dictionary key those
/// instances store their value under.
/// </summary>
public class TweakSettingDefinition<T> : SettingDefinition<T>, ITweakSettingDefinition<T> {
	public TweakSettingDefinition(string iD, TweakCategory category, T defaultValue, bool hidden = false) : base(iD, defaultValue, hidden) {
		Category = category;
	}

	public TweakCategory Category { get; }
}
