namespace Herculan.Engine.Shell;

/// <summary>
/// Which campaign the shell is running — <c>DAT_0048260c</c>, the flag that also picks
/// <c>GAME_R.SAV</c> over <c>GAME_T.SAV</c> (docs/formats/save-games.md). It is what gates three of
/// the eight tabs; see <see cref="ShellScreen.ApplyTabGate"/>.
/// </summary>
public enum ShellCampaignMode {
	/// <summary>The training campaign, which has no salvage economy and so no repair, build or armory.</summary>
	Training = 0,

	/// <summary>The real campaign, where every tab is live.</summary>
	Campaign = 1,
}

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
/// <para><b>Three tabs are gated in the training campaign.</b> The builder clears <c>+0x49</c> on tab
/// 5 as it constructs it, and the strip's refresh (<c>0043b0c8</c>) rewrites that flag on tabs 2 to 6
/// from <c>DAT_0048260c</c> — REPAIR, BUILD and ARMORY off in training, everything on in the
/// campaign. See <see cref="ApplyTabGate"/>.</para>
///
/// <para><b>Tabs 0 and 1 never latch.</b> Every handler starts by clearing the lit flag on all nine
/// strip buttons (<c>00439dcb</c>); the six from WEAPONS on then write their own back to 1, and the
/// main menu's and the save screen's do not — <see cref="SelectedTab"/> is <c>DAT_0047581c</c>, which
/// screen is up, and the latch is a separate thing that only six of the eight ever take. Those two
/// hide the strip outright instead, and <see cref="StripVisible"/> carries that; see
/// <see cref="SelectTab"/>.</para>
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

	/// <summary>Which tab is up, 0-7, or <see cref="NoTab"/> after <see cref="ReturnToFrame"/>.</summary>
	public int SelectedTab { get; private set; }

	/// <summary>
	/// <c>DAT_0047581c</c>'s parked value, <c>0xffff</c>: no tab is up, so whichever is clicked next is
	/// never mistaken for the one already showing.
	/// </summary>
	public const int NoTab = -1;

	/// <summary>
	/// Whether the strip is drawn and answers clicks — the hidden bit of the full-screen panel every
	/// strip button is parented to (<c>DAT_0048d448</c>).
	/// </summary>
	public bool StripVisible { get; private set; } = true;

	/// <summary>The button under the pointer, or null when it is over none.</summary>
	public int? HoverId => _hoverId;

	/// <summary>The button the pointer is holding down, or null.</summary>
	public int? PressedId => _pressedId;

	/// <summary>
	/// Builds the shell frame: the tab strip, captioned from <paramref name="text"/>, with
	/// <paramref name="selectedTab"/> current and <paramref name="mode"/>'s tabs gated.
	/// </summary>
	public static ShellScreen CreateFrame(ShellText? text, int selectedTab = 0,
			ShellCampaignMode mode = ShellCampaignMode.Campaign) {
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

		screen.ApplyTabGate(mode);
		screen.SelectTab(selectedTab);
		return screen;
	}

	/// <summary>
	/// The strip refresh, <c>0043b0c8</c>: REPAIR, BUILD and ARMORY answer only in the campaign, while
	/// WEAPONS and CREW answer in both. It writes those five and no others — the two leftmost tabs,
	/// MISSION and the square button are never gated. The rest of the refresh — showing the strip and
	/// parking <c>DAT_0047581c</c> — is <see cref="ReturnToFrame"/>'s.
	///
	/// <para><b>The flag is <c>+0x49</c>.</b> Two things say it is the enable flag: which tabs it
	/// selects here — the three the training campaign has no salvage economy for — and the repair
	/// panel, which writes it alongside two greying colour fields on a test of whether the player can
	/// afford the button. What actually stops a cleared widget responding is in the base class's click
	/// dispatch and has not been traced; the button's own paint reads the flag only to decide whether
	/// the caption takes the pressed nudge.</para>
	/// </summary>
	public void ApplyTabGate(ShellCampaignMode mode) {
		bool economy = mode == ShellCampaignMode.Campaign;
		SetTabEnabled(WeaponsTab, true);
		SetTabEnabled(RepairTab, economy);
		SetTabEnabled(BuildTab, economy);
		SetTabEnabled(ArmoryTab, economy);
		SetTabEnabled(CrewTab, true);
	}

	private void SetTabEnabled(int tab, bool enabled) {
		if (Button(tab) is { } button) {
			button.Enabled = enabled;
		}
	}

	/// <summary>
	/// The tabs the gate and the palette switch name. The captions they carry are
	/// <c>estext.bin</c>'s and are fetched, not hardcoded — these are only the indices.
	/// </summary>
	public const int MainMenuTab = 0;
	public const int SaveTab = 1;
	public const int WeaponsTab = 2;
	public const int RepairTab = 3;
	public const int BuildTab = 4;
	public const int ArmoryTab = 5;
	public const int CrewTab = 6;
	public const int MissionTab = 7;

	/// <summary>
	/// The two tabs whose handlers leave the whole strip unlit — see the class remarks. Everything from
	/// here up writes its own lit flag back after the clear.
	/// </summary>
	public const int FirstLatchingTab = WeaponsTab;

	/// <summary>
	/// The tab plate's two faces in <see cref="ShellArt.ButtonBank"/>. The builder hands the class a
	/// third pointer, frame 3, which neither of its paints reads — see <see cref="ShellButton"/>.
	/// </summary>
	private const int UnlitFrame = 1;
	private const int LitFrame = 2;

	/// <summary>
	/// Makes one tab the screen that is up, and latches it if it is one of the six that latch. Every
	/// other plate is released either way, which is the clear all nine handlers start with. Out-of-range
	/// indices are ignored.
	///
	/// <para><b>The save tab hides the strip.</b> Its handler calls <c>0043b23d</c>, which hides the
	/// strip's parent panel, so the save screen stands alone and its own EXIT and RESTORE are the only
	/// way off it; both end in <see cref="ReturnToFrame"/>. The main menu's handler hides it the same
	/// way, and here it does not: that tab has no content ported, so hiding the strip would leave
	/// nothing on screen to click. That is this engine's choice, not the original's.</para>
	/// </summary>
	public void SelectTab(int index) {
		if (index < 0 || index >= ShellLayout.TabCount) {
			return;
		}

		SelectedTab = index;
		StripVisible = index != SaveTab;
		foreach (var button in _buttons) {
			if (button.Id < ShellLayout.TabCount) {
				button.Selected = button.Id == index && index >= FirstLatchingTab;
			}
		}
	}

	/// <summary>
	/// Puts the bare frame back up with no tab current — the pair the save screen's EXIT and RESTORE
	/// both end with: <c>0043b162(8)</c>, which shows the frame's root, then the strip refresh
	/// <c>0043b0c8</c>, which shows the strip's panel, regates it and parks <c>DAT_0047581c</c> at
	/// <c>0xffff</c>. Nothing is latched, because the tab handler that brought the screen up cleared
	/// all nine and latched none.
	/// </summary>
	public void ReturnToFrame(ShellCampaignMode mode) {
		StripVisible = true;
		ApplyTabGate(mode);
		SelectedTab = NoTab;
		foreach (var button in _buttons) {
			if (button.Id < ShellLayout.TabCount) {
				button.Selected = false;
			}
		}
	}

	/// <summary>The button at a canvas point, or null. Disabled buttons, and a hidden strip, do not answer.</summary>
	public ShellButton? ButtonAt(float canvasX, float canvasY) {
		if (!StripVisible) {
			return null;
		}

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
