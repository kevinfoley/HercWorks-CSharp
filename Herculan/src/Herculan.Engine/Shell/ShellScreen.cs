namespace Herculan.Engine.Shell;

/// <summary>
/// One shell screen: the widgets on it, which one the pointer is over, and which one it is holding
/// down.
///
/// <para>What is built here is the <b>frame</b> — the backdrop-textured root, the square button at the
/// far left of the strip, and the eight tabs. Every tab screen in VSHELL builds its own copy of that
/// strip at the same coordinates and then adds its own content beneath it, so the frame is the part
/// that is the same on all eight and the part worth having first. The content panels are not built
/// yet; see Herculan/ROADMAP.md.</para>
///
/// <para><b>The tabs name themselves.</b> Each caption is an <c>estext.bin</c> entry, consecutive from
/// <see cref="ShellLayout.FirstTabCaption"/>, exactly as the builder fetches them — so nothing here
/// hardcodes what the tabs are called or has to be corrected when the string table is read properly.
/// A tab with no caption available draws its plate and no text.</para>
///
/// <para><b>No tab is gated here, and retail gates several.</b> The builder clears <c>+0x49</c> on
/// tab 5 as it constructs it, and the strip's refresh (<c>0043b0c8</c>) rewrites that flag on tabs 2
/// to 6 from campaign state held in <c>DAT_0048260c</c>. <c>Control_Ctor</c> defaults it to 1, and
/// the button's own paint reads it for one thing only — whether the caption takes the pressed nudge —
/// so whatever stops a gated tab responding is elsewhere and is not read. The campaign state behind
/// it is not read either, so <see cref="ShellButton.Enabled"/> is left true throughout and the gate is
/// left for the pass that reads both.</para>
/// </summary>
public sealed class ShellScreen {
	/// <summary>Id of the square button at the left of the strip, past the eight tab ids.</summary>
	public const int MenuButtonId = ShellLayout.TabCount;

	private readonly List<ShellButton> _buttons = new();
	private int? _hoverId;
	private int? _pressedId;

	private ShellScreen() { }

	/// <summary>Every button on the screen, in the order the builder constructs them.</summary>
	public IReadOnlyList<ShellButton> Buttons => _buttons;

	/// <summary>Which tab is latched down, 0-7.</summary>
	public int SelectedTab { get; private set; }

	/// <summary>The button under the pointer, or null when it is over none.</summary>
	public int? HoverId => _hoverId;

	/// <summary>The button the pointer is holding down, or null.</summary>
	public int? PressedId => _pressedId;

	/// <summary>
	/// Builds the shell frame: the tab strip, captioned from <paramref name="text"/>, with
	/// <paramref name="selectedTab"/> latched.
	/// </summary>
	public static ShellScreen CreateFrame(ShellText? text, int selectedTab = 0) {
		var screen = new ShellScreen();

		// The strip's leftmost button, whose two faces come from the ONLINE bank rather than the tab
		// plate's — which is why it is built separately rather than as a tab.
		screen._buttons.Add(new ShellButton(MenuButtonId, ShellLayout.MenuButton,
			unlit: new ShellSprite(ShellArt.MenuButtonBank, 0),
			lit: new ShellSprite(ShellArt.MenuButtonBank, 1)));

		for (int i = 0; i < ShellLayout.TabCount; i++) {
			screen._buttons.Add(new ShellButton(i, ShellLayout.Tab(i),
				unlit: new ShellSprite(ShellArt.ButtonBank, UnlitFrame),
				lit: new ShellSprite(ShellArt.ButtonBank, LitFrame),
				caption: text?.Text(ShellLayout.FirstTabCaption + i)));
		}

		screen.SelectTab(selectedTab);
		return screen;
	}

	/// <summary>
	/// The tab plate's two faces in <see cref="ShellArt.ButtonBank"/>. The builder hands the class a
	/// third pointer, frame 3, which neither of its paints reads — see <see cref="ShellButton"/>.
	/// </summary>
	private const int UnlitFrame = 1;
	private const int LitFrame = 2;

	/// <summary>Latches one tab down and releases the rest. Out-of-range indices are ignored.</summary>
	public void SelectTab(int index) {
		if (index < 0 || index >= ShellLayout.TabCount) {
			return;
		}

		SelectedTab = index;
		foreach (var button in _buttons) {
			if (button.Id < ShellLayout.TabCount) {
				button.Selected = button.Id == index;
			}
		}
	}

	/// <summary>The button at a canvas point, or null. Disabled buttons do not answer.</summary>
	public ShellButton? ButtonAt(float canvasX, float canvasY) {
		foreach (var button in _buttons) {
			if (button.Enabled && button.Rect.Contains(canvasX, canvasY)) {
				return button;
			}
		}

		return null;
	}

	/// <summary>The button with this id, or null.</summary>
	public ShellButton? Button(int id) {
		foreach (var button in _buttons) {
			if (button.Id == id) {
				return button;
			}
		}

		return null;
	}

	/// <summary>Tracks the pointer. Canvas pixels; pass anything off-canvas and nothing is hovered.</summary>
	public void PointerMoved(float canvasX, float canvasY) => _hoverId = ButtonAt(canvasX, canvasY)?.Id;

	/// <summary>Arms the button under the pointer, if there is one.</summary>
	public void PointerDown(float canvasX, float canvasY) {
		PointerMoved(canvasX, canvasY);
		_pressedId = _hoverId;
	}

	/// <summary>
	/// Releases the pointer, and reports the button that was activated — the armed one, and only if
	/// the pointer is still on it. Null otherwise, including for a press dragged off its button, which
	/// is the cancel every pointer UI gives for free.
	/// </summary>
	public int? PointerUp(float canvasX, float canvasY) {
		PointerMoved(canvasX, canvasY);
		int? activated = _pressedId is { } pressed && _hoverId == pressed ? pressed : null;
		_pressedId = null;
		return activated;
	}

	/// <summary>Drops any hover and press state — for when the pointer leaves the window.</summary>
	public void PointerLeft() {
		_hoverId = null;
		_pressedId = null;
	}
}
