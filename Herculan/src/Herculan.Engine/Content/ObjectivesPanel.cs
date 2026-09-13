namespace Herculan.Engine.Content;

/// <summary>
/// The in-mission objectives panel — <c>obj_alrt</c> (<c>FUN_0045751c</c>), what [F11] puts up over
/// the cockpit. It lists <c>script.dat</c> block 13, which is the mission's objective text as the
/// player is shown it; see docs/simulation/mission-objectives.md.
///
/// <para>This type is the panel's data and its state. Its geometry is in
/// <see cref="ObjectivesPanelLayout"/> and its pixels are drawn by
/// <see cref="Render.Overlay2DRenderer.DrawObjectivesPanel"/>, out of the same sprite atlas and
/// <c>.HFN</c> fonts the rest of the cockpit draws from.</para>
///
/// <para><b>It is modal, and the simulation does not tick behind it.</b> The original runs its own
/// event loop (<c>FUN_00457ae4</c>) that polls input, repaints the widgets it owns and presents —
/// and never calls the sim tick. Entering also pauses both message ports
/// (<c>MessagePort_Pause</c>) and saves the framebuffer, so the frozen cockpit and whatever the
/// computer was saying are still there when the panel comes down.</para>
/// </summary>
public sealed class ObjectivesPanel {
	/// <summary>The panel's own string table: its title and its one button's caption.</summary>
	public const string StringsFileName = "OBJ_ALRT.STR";

	/// <summary>The panel background's sprite bank — one frame, the whole plate.</summary>
	public const string BackgroundBank = "OBJ_ALRT";

	/// <summary>Group 0 of <see cref="StringsFileName"/> — <c>OBJECTIVES</c>.</summary>
	private const int TitleGroup = 0;

	/// <summary>Group 1 — <c>RETURN</c>.</summary>
	private const int ButtonGroup = 1;

	private ObjectivesPanel(string title, string buttonCaption, IReadOnlyList<string> lines) {
		Title = title;
		ButtonCaption = buttonCaption;
		Lines = lines;
	}

	/// <summary>The panel's title, drawn centred in the plate's title bar.</summary>
	public string Title { get; }

	/// <summary>The one button's caption.</summary>
	public string ButtonCaption { get; }

	/// <summary>
	/// The objective lines, already resolved from block 13's <c>data\mission.str</c> refs and already
	/// trimmed to what the panel can show — see <see cref="Build"/>.
	/// </summary>
	public IReadOnlyList<string> Lines { get; }

	/// <summary>Whether the panel is up. The simulation does not tick while it is.</summary>
	public bool IsOpen { get; private set; }

	/// <summary>
	/// Whether the button is currently held down — it paints its second plate and captions itself in
	/// <c>PUSHED</c> rather than <c>ACTIVE</c> while it is (<c>FUN_00454ff8</c>).
	/// </summary>
	public bool ButtonPressed { get; private set; }

	/// <summary>
	/// Reads the panel's own captions out of the mounted archives and resolves a mission's block-13
	/// refs against its text. Returns null when the string table is missing, since a panel with no
	/// title and no button caption is not worth putting up.
	/// </summary>
	/// <param name="briefingLines">
	/// <c>Mission.BriefingLines</c> — block 13, one <c>data\mission.str</c> line index per entry.
	/// </param>
	/// <param name="textAt">Resolves one of those indices to its line.</param>
	public static ObjectivesPanel? Build(GameContent content, IReadOnlyList<int> briefingLines,
			Func<int, string> textAt) {
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(briefingLines);
		ArgumentNullException.ThrowIfNull(textAt);

		if (SimStringTable.Load(content, StringsFileName) is not { } strings) {
			return null;
		}

		// The paint skips an empty line rather than leaving a blank row for it, and stops once seven
		// labels have text — the panel builds seven and no more, so an eighth entry is dropped.
		var lines = new List<string>(ObjectivesPanelLayout.LineCount);
		foreach (int reference in briefingLines) {
			string text = textAt(reference);
			if (text.Length == 0) {
				continue;
			}

			lines.Add(text);
			if (lines.Count == ObjectivesPanelLayout.LineCount) {
				break;
			}
		}

		return new ObjectivesPanel(
			strings.Text(TitleGroup, 0) ?? string.Empty,
			strings.Text(ButtonGroup, 0) ?? string.Empty,
			lines);
	}

	/// <summary>
	/// Puts the panel up. [F11] is scancode <c>0x57</c>, which
	/// <c>CockpitWidgets_HandleCommand</c> answers by constructing the panel and running its modal
	/// loop; nothing in that loop answers <c>0x57</c> again, so pressing [F11] a second time does not
	/// take the panel back down.
	/// </summary>
	public void Open() {
		IsOpen = true;
		ButtonPressed = false;
	}

	/// <summary>Takes the panel down, however it was dismissed.</summary>
	public void Close() {
		IsOpen = false;
		ButtonPressed = false;
	}

	/// <summary>
	/// A key the panel's own handler (<c>FUN_00454e10</c>) answers. [Return] presses the focused
	/// widget and [Esc] presses the panel's cancel widget; the panel sets both to its one button, so
	/// either closes it. [Tab] and [Shift+Tab] walk the focus, which with one widget is a no-op, and
	/// are not modelled.
	/// </summary>
	/// <returns>True when the key was the panel's to answer.</returns>
	public bool HandleKey(bool enter, bool escape) {
		if (!IsOpen || (!enter && !escape)) {
			return false;
		}

		Close();
		return true;
	}

	/// <summary>
	/// A mouse press inside the panel, in the panel's own device pixels. Arms the button when the
	/// press lands on it, the way <c>Widget_OnMouseDown</c> does.
	/// </summary>
	public void PointerDown(float panelX, float panelY) {
		if (IsOpen) {
			ButtonPressed = ObjectivesPanelLayout.Button.Contains(panelX, panelY);
		}
	}

	/// <summary>
	/// A mouse release, in the same space. Closes the panel when press and release both landed on the
	/// button — <c>Widget_OnMouseUp</c> re-hit-tests before it calls the click, so a press dragged off
	/// its widget never fires.
	/// </summary>
	public void PointerUp(float panelX, float panelY) {
		if (!IsOpen) {
			return;
		}

		bool onButton = ButtonPressed && ObjectivesPanelLayout.Button.Contains(panelX, panelY);
		ButtonPressed = false;
		if (onButton) {
			Close();
		}
	}
}
