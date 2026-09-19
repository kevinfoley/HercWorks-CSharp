using System.Numerics;
using Herculan.Engine.Host.Localization;
using Herculan.Engine.Settings;
using ImGuiNET;

namespace Herculan.Engine.Host.Settings;

/// <summary>
/// The floating "Tweaks" window opened from the menu bar.
///
/// <para>Edits land on the live <see cref="TweakSettings"/> immediately, so a checkbox shows
/// its effect in the viewport while the panel is still open. Cancel undoes them by re-reading the
/// settings file — safe because nothing outside this panel writes to <see cref="TweakSettings"/>
/// at runtime, so the in-memory values never diverge from disk except through edits made here. Save
/// writes the current values to disk and closes, Cancel reloads from disk (discarding anything not
/// yet saved) and closes, and so does closing the window by its title-bar button or [Esc]; see
/// Program.cs for the latter.</para>
/// </summary>
public sealed class TweaksMenu {
	private const float PanelWidth = 300f;

	private readonly TweakSettings _settings;
	private readonly LocalizationTable _localization;

	/// <summary>Whether the panel is currently open. Set by the menu bar; see Program.cs.</summary>
	public bool IsOpen { get; set; }

	public TweaksMenu(TweakSettings settings, LocalizationTable localization) {
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		_localization = localization ?? throw new ArgumentNullException(nameof(localization));
	}

	/// <summary>Draws the panel, if it is open. Call once per frame, inside the ImGui frame.</summary>
	public void Draw() {
		if (!IsOpen) {
			return;
		}

		// A zero component means "fit the content", so the panel keeps a fixed width and grows to
		// whatever height its settings need.
		ImGui.SetNextWindowSize(new Vector2(PanelWidth, 0f));
		ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

		bool stayOpen = true;
		if (ImGui.Begin("Tweaks", ref stayOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize)) {
			foreach (var group in TweakSettingDefinitions.All.Where(d => !d.Hidden).GroupBy(d => d.Category)) {
				ImGui.SeparatorText(group.Key.ToString());
				foreach (var definition in group) {
					bool value = _settings.GetSettingValue(definition);
					string displayName = _localization.GetString(definition.DisplayNameKey) ?? definition.ID;
					if (ImGui.Checkbox(displayName, ref value)) {
						_settings.SetSettingValue(definition, value);
					}

					if (ImGui.IsItemHovered() && _localization.GetString(definition.DescriptionKey) is { } description) {
						ImGui.SetTooltip(description);
					}
				}
			}

			ImGui.Separator();

			float spacing = ImGui.GetStyle().ItemSpacing.X;
			float buttonWidth = (ImGui.GetContentRegionAvail().X - spacing) * 0.5f;
			var buttonSize = new Vector2(buttonWidth, 0f);

			if (ImGui.Button("Save", buttonSize)) {
				_settings.SaveToDisk();
				IsOpen = false;
			}

			ImGui.SameLine();

			if (ImGui.Button("Cancel", buttonSize)) {
				Cancel();
			}
		}

		ImGui.End();

		if (!stayOpen && IsOpen) {
			Cancel();
		}
	}

	/// <summary>Discards any edit not yet saved by reloading <see cref="TweakSettings"/> from disk,
	/// and closes the panel. Also reachable from outside — [Esc] backs out of an open Tweaks the same
	/// way the title-bar close and the in-panel Cancel button do; see Program.cs.</summary>
	public void Cancel() {
		_settings.LoadFromDisk();
		IsOpen = false;
	}
}
