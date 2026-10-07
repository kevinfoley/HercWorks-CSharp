using ImGuiNET;
using System.Numerics;

namespace Herculan.Engine;

public static class ImGuiHelpers {
	public static void DrawButtonRow(params (string label, Action callback)[] buttons) {
		float spacing = ImGui.GetStyle().ItemSpacing.X;
		int count = buttons.Length;
		float buttonWidth = (ImGui.GetContentRegionAvail().X - spacing * (count - 1)) / count;
		var buttonSize = new Vector2(buttonWidth, 0f);
		for (int i = 0; i < buttons.Length; i++) {
			var button = buttons[i];
			if (ImGui.Button(button.label, buttonSize)) {
				button.callback.Invoke();
			}
			if (i < buttons.Length - 1) {
				ImGui.SameLine();
			}
		}
	}
}
