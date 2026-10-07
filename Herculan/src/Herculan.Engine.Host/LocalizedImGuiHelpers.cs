using Herculan.Engine.Host.Localization;

namespace Herculan.Engine.Host; 
public static class LocalizedImGuiHelpers {
	/// <summary>
	/// Draw a row of buttons with localized labels.
	/// </summary>
	/// <param name="table"></param>
	/// <param name="buttons">Button localization keys and callbacks</param>
	public static void DrawButtonRow(LocalizationTable table, params (string label, Action callback)[] buttons) {
		for (int i = 0; i < buttons.Length; i++) {
			(string label, Action callback) button = buttons[i];
			button.label = table.GetStringOrKey(button.label);
			buttons[i] = button;
		}
		ImGuiHelpers.DrawButtonRow(buttons);
	}
}
