using Herculan.Engine.Content;

namespace Herculan.Engine.Shell;

/// <summary>
/// The five buttons on the save screen, in the order its builder constructs them. The first two sit
/// inside the slot list and the last three in a column beside it.
/// </summary>
public enum ShellSaveButton {
	/// <summary>Abandons a slot rename. Live only while a row is being typed into.</summary>
	Cancel = 0,

	/// <summary>Commits a slot rename. Live only while a row is being typed into.</summary>
	Accept = 1,

	/// <summary>Writes the current game into the selected slot.</summary>
	Save = 2,

	/// <summary>Loads the selected slot. Live only for a slot that holds a save.</summary>
	Restore = 3,

	/// <summary>Leaves the screen — back to the tab strip, or to the main menu.</summary>
	Exit = 4,
}

/// <summary>
/// Tab 1, <c>SAVED GAMES</c> — the slot list, the five buttons and the summary of whichever slot is
/// selected. Built by <c>FUN_004385b0</c>, entered by <c>FUN_00439b0c</c>, its selection moved by
/// <c>FUN_0043795f</c> and its summary panel refilled by <c>FUN_0043712c</c>.
///
/// <para><b>Every rect here is a literal in the executable</b>, written as four immediates onto the
/// builder's own stack, and they are kept parent-relative exactly as the builder writes them — the
/// panel is placed in the canvas, the list and the button column in the panel, and the two inner
/// buttons in the list. Composing them the same way the widget tree does is what makes each number
/// checkable against the decompilation instead of a pre-added absolute nobody can trace back. See
/// docs/shell/screen-layout.md.</para>
///
/// <para><b>The screen names itself.</b> Its title, all five button captions, every field label and
/// the words for skill, rank and sector are <c>estext.bin</c> entries fetched by index, so nothing
/// here hardcodes an English string. The three that are formatted rather than looked up — the kill
/// counts, the salvage figure and the mission number — are formatted the way the original's
/// <c>sprintf</c> calls are.</para>
///
/// <para><b>Ten rows, twelve slots.</b> Every loop on this screen bounds at ten: slots 10 and 11 are
/// the campaign and training autosaves, which are one slot from the caller's side and are reached by
/// the resume path rather than by a row.</para>
/// </summary>
public sealed class ShellSaveScreen {
	/// <summary>How many slots the list shows.</summary>
	public const int RowCount = 10;

	/// <summary>
	/// The content panel, in the canvas: <c>{0x8e, 0x7f, 0x1f2, 0x1d4}</c>. Horizontally centred, and
	/// low enough to leave the backdrop showing between it and the tab strip.
	/// </summary>
	public static readonly ShellRect PanelRect = new(0x8e, 0x7f, 0x1f2, 0x1d4);

	private static readonly ShellRect ListRect = new(9, 0x1c, 0x15b, 0xc6);
	private static readonly ShellRect SummaryRect = new(0x74, 0xcc, 0x15b, 0x150);
	private static readonly ShellRect CancelRect = new(0x43, 0x93, 0xa5, 0xa2);
	private static readonly ShellRect AcceptRect = new(0xb0, 0x93, 0x112, 0xa2);
	private static readonly ShellRect SaveRect = new(9, 0xe5, 0x6b, 0xf4);
	private static readonly ShellRect RestoreRect = new(9, 0xfb, 0x6b, 0x10a);
	private static readonly ShellRect ExitRect = new(9, 0x111, 0x6b, 0x120);

	/// <summary>
	/// A sixth panel the builder constructs over the summary panel's top two thirds,
	/// <c>{0x74, 0xcc, 0x15b, 0x136}</c>, with no children of its own — an empty box where the summary
	/// would be. It is not painted here, and that is the original's arrangement rather than a guess:
	/// the entry routine calls <c>FUN_0041f2e6</c> on the summary panel and <c>FUN_0041f469</c> on this
	/// one, which are the show and the hide respectively (docs/shell/screen-layout.md, "Showing and
	/// hiding a widget"), so the summary is what tab 1 puts in front.
	/// </summary>
	public static readonly ShellRect StubPanelRect = new(0x74, 0xcc, 0x15b, 0x136);

	private readonly List<ShellSaveSlot> _slots = new();
	private int _selected;

	public ShellSaveScreen(IReadOnlyList<ShellSaveSlot>? slots = null, bool canSave = true) {
		if (slots != null) {
			_slots.AddRange(slots);
		}

		CanSave = canSave;
	}

	/// <summary>
	/// Which slot the list has selected. The original parks it at 10 — the autosave, past every row —
	/// on leaving the screen, so an out-of-range value is a normal state and not an error: it means no
	/// row is highlighted and both SAVE and RESTORE are dead.
	/// </summary>
	public int SelectedSlot => _selected;

	/// <summary>
	/// Whether there is a game in progress to write out — <c>DAT_0048260a</c>, which SAVE is gated on
	/// alongside the selection being a real row.
	/// </summary>
	public bool CanSave { get; set; }

	/// <summary>The slots, as <c>sav\GAMEFILE.STR</c> lists them. Empty when there is no directory file.</summary>
	public IReadOnlyList<ShellSaveSlot> Slots => _slots;

	/// <summary>The slot the row at a canvas point belongs to, or null when the point is on no row.</summary>
	public int? RowAt(float canvasX, float canvasY) {
		for (int slot = 0; slot < RowCount; slot++) {
			if (RowRect(slot).Contains(canvasX, canvasY)) {
				return slot;
			}
		}

		return null;
	}

	/// <summary>The button at a canvas point, or null. A disabled button does not answer.</summary>
	public ShellSaveButton? ButtonAt(float canvasX, float canvasY) {
		foreach (var button in Enum.GetValues<ShellSaveButton>()) {
			if (IsEnabled(button) && ButtonRect(button).Contains(canvasX, canvasY)) {
				return button;
			}
		}

		return null;
	}

	/// <summary>
	/// Moves the selection, as <c>FUN_0043795f</c> does. A click on the row already selected is a
	/// no-op, the same early return the tab handlers make, and a row past what the directory lists
	/// still takes the selection — the original tests only the bound of ten.
	/// </summary>
	public void SelectSlot(int slot) {
		if (slot == _selected) {
			return;
		}

		_selected = slot;
	}

	/// <summary>
	/// Whether a button answers a click, which is the same test that greys its caption.
	///
	/// <para>SAVE needs a real row selected and a game to save; RESTORE needs a real row that holds
	/// one. EXIT is never gated. CANCEL and ACCEPT belong to the slot rename — the rows carry a
	/// permitted-character set and are genuinely editable in retail — and stay dead until that is
	/// ported, which is what the original's entry routine leaves them as.</para>
	/// </summary>
	public bool IsEnabled(ShellSaveButton button) => button switch {
		ShellSaveButton.Save => _selected < RowCount && CanSave,
		ShellSaveButton.Restore => _selected < RowCount && SlotAt(_selected)?.InUse == true,
		ShellSaveButton.Exit => true,
		_ => false,
	};

	/// <summary>One slot row's rect, in the canvas.</summary>
	public ShellRect RowRect(int slot) {
		var list = Inside(PanelRect, ListRect);

		// The rows are 13 tall on a 12-pixel pitch, so each overlaps its neighbour's border row, and the
		// first and last are inset two pixels further from the left edge than the eight between them.
		int y0 = slot * RowPitch + FirstRowY;
		int left = slot == 0 || slot == RowCount - 1 ? EndRowInset : RowInset;
		return new ShellRect(list.X0 + left, list.Y0 + y0,
			list.X0 + list.Width - 2, list.Y0 + y0 + RowHeight - 1);
	}

	private const int RowPitch = 12;
	private const int RowHeight = 13;
	private const int FirstRowY = 0x13;
	private const int RowInset = 10;
	private const int EndRowInset = 12;

	/// <summary>One button's rect, in the canvas. Two are placed in the list panel, three in the content panel.</summary>
	public ShellRect ButtonRect(ShellSaveButton button) => button switch {
		ShellSaveButton.Cancel => Inside(Inside(PanelRect, ListRect), CancelRect),
		ShellSaveButton.Accept => Inside(Inside(PanelRect, ListRect), AcceptRect),
		ShellSaveButton.Save => Inside(PanelRect, SaveRect),
		ShellSaveButton.Restore => Inside(PanelRect, RestoreRect),
		_ => Inside(PanelRect, ExitRect),
	};

	/// <summary>
	/// Draws the whole screen into <paramref name="surface"/>. The caller clears it first; what this
	/// leaves untouched is what the shell's backdrop shows through, which the content panel's dithered
	/// body relies on.
	/// </summary>
	public void Paint(ShellSurface surface, ShellText? text, HudSpriteSheet? sprites) {
		var font = sprites?.Font(ShellArt.ScreenFont);

		ShellChrome.PaintTitledPanel(surface, PanelRect, PanelBorder, PanelFace, PanelBodyDither,
			TitleHeight, headerChrome: true, TitlePlateFirst, TitlePlateLast, fill: false);
		ShellChrome.PaintText(surface, TitleRect(), font, text?.Text(TitleText),
			ShellTextAlign.Center, ShellChrome.FontInkColor);

		var list = Inside(PanelRect, ListRect);
		ShellChrome.PaintFramedPanel(surface, list, FrameBorder, FrameFace, fill: true);
		ShellChrome.PaintFramedPanel(surface, Inside(PanelRect, SummaryRect), FrameBorder, FrameFace,
			fill: true);

		for (int slot = 0; slot < RowCount; slot++) {
			ShellChrome.PaintEditField(surface, RowRect(slot), font, SlotAt(slot)?.Label,
				slot == _selected ? SelectedRowColor : RowColor);
		}

		foreach (var button in Enum.GetValues<ShellSaveButton>()) {
			PaintButton(surface, font, text, button);
		}

		PaintSummary(surface, font, text);
	}

	/// <summary>
	/// One button: a filled, chamfer-bordered box with its caption centred on it. Both the border and
	/// the caption grey together on the enable test, which is the trio the original writes at every
	/// gated button — the border colour, the caption colour and the enable flag itself.
	/// </summary>
	private void PaintButton(ShellSurface surface, HudFont? font, ShellText? text,
			ShellSaveButton button) {
		bool enabled = IsEnabled(button);
		var rect = ButtonRect(button);
		ShellChrome.PaintPanel(surface, rect, enabled ? ButtonBorder : DisabledColor, fill: true);
		ShellChrome.PaintText(surface, rect, font, text?.Text(CaptionText(button)),
			ShellTextAlign.Center, enabled ? ShellChrome.FontInkColor : DisabledColor);
	}

	/// <summary>
	/// The summary panel's labels and values — <c>FUN_0043712c</c>. Every value field clears its own
	/// rect before drawing, so a refresh overwrites the last slot's figures rather than layering over
	/// them; the labels do not, and draw straight onto the panel.
	///
	/// <para>A slot with no save reads its values from string-table entry 0, which is empty — so the
	/// labels stay and the figures go blank rather than showing zeroes.</para>
	/// </summary>
	private void PaintSummary(ShellSurface surface, HudFont? font, ShellText? text) {
		var panel = Inside(PanelRect, SummaryRect);
		var summary = _selected < RowCount ? SlotAt(_selected)?.Summary : null;

		void Label(ShellRect rect, int index, ShellTextAlign align = ShellTextAlign.Right) =>
			ShellChrome.PaintText(surface, Inside(panel, rect), font, text?.Text(index), align,
				LabelColor);

		void Value(ShellRect rect, string? value, ShellTextAlign align) =>
			ShellChrome.PaintText(surface, Inside(panel, rect), font, value, align,
				ShellChrome.FontInkColor, ShellChrome.InteriorColor);

		Label(new ShellRect(0x0e, 0x06, 0x36, 0x12), NameLabel);
		Value(new ShellRect(0x3b, 0x06, 0xd8, 0x12), summary?.PilotName, ShellTextAlign.Left);

		Label(new ShellRect(0x0e, 0x12, 0x36, 0x1e), SkillLabel);
		Value(new ShellRect(0x3b, 0x12, 0x71, 0x1e),
			summary == null ? null : text?.Text(FirstSkillWord + summary.Skill), ShellTextAlign.Left);

		Label(new ShellRect(0x76, 0x12, 0x9a, 0x1e), RankLabel);
		Value(new ShellRect(0x9f, 0x12, 0xe5, 0x1e),
			summary == null ? null : text?.Text(FirstRankWord + summary.Rank), ShellTextAlign.Left);

		// The two column headers over the kill grid, both centred over their own column of numbers.
		Label(new ShellRect(0x5a, 0x24, 0x8e, 0x30), CurrentColumnLabel, ShellTextAlign.Center);
		Label(new ShellRect(0x91, 0x24, 0xc5, 0x30), TotalColumnLabel, ShellTextAlign.Center);

		PaintKillRow(0x30, HercKillsLabel, summary?.HercKills, summary?.TotalHercKills);
		PaintKillRow(0x3c, FlyerKillsLabel, summary?.FlyerKills, summary?.TotalFlyerKills);
		PaintKillRow(0x48, BaseKillsLabel, summary?.BaseKills, summary?.TotalBaseKills);

		// Salvage is stored in kilograms and quoted in tons, which is the shell's split throughout —
		// integer division, so a part-ton is dropped rather than rounded. See docs/shell/armory.md.
		Label(new ShellRect(0x0e, 0x5a, 0x54, 0x66), SalvageLabel);
		Value(new ShellRect(0x5a, 0x5a, 0xc5, 0x66),
			summary == null ? null
				: $"{summary.SalvageKilograms / KilogramsPerTon} {text?.Text(TonsWord)}",
			ShellTextAlign.Left);

		Label(new ShellRect(0x0e, 0x66, 0x54, 0x72), SectorLabel);
		Value(new ShellRect(0x5a, 0x66, 0xc5, 0x72),
			summary == null ? null : text?.Text(FirstSectorWord + summary.Sector),
			ShellTextAlign.Left);

		// The mission counter is stored from zero and printed from one, the same off-by-one the stage
		// counter takes everywhere in the shell.
		Label(new ShellRect(0x0e, 0x72, 0x54, 0x7e), MissionLabel);
		Value(new ShellRect(0x5a, 0x72, 0xc5, 0x7e),
			summary == null ? null : (summary.Mission + 1).ToString(), ShellTextAlign.Left);

		void PaintKillRow(int y, int label, int? current, int? total) {
			Label(new ShellRect(0x0e, y, 0x54, y + 0x0c), label);
			Value(new ShellRect(0x5a, y, 0x8e, y + 0x0c), current?.ToString(), ShellTextAlign.Center);
			Value(new ShellRect(0x91, y, 0xc5, y + 0x0c), total?.ToString(), ShellTextAlign.Center);
		}
	}

	/// <summary>
	/// The title's own rect: the panel's full width by its header height, which is what
	/// <c>TitledPanel_Ctor</c> builds for the caption it is handed rather than anything the screen
	/// chooses.
	/// </summary>
	private static ShellRect TitleRect() =>
		new(PanelRect.X0, PanelRect.Y0, PanelRect.X0 + PanelRect.Width - 1,
			PanelRect.Y0 + TitleHeight);

	private ShellSaveSlot? SlotAt(int slot) => slot >= 0 && slot < _slots.Count ? _slots[slot] : null;

	private static ShellRect Inside(ShellRect parent, ShellRect child) =>
		new(parent.X0 + child.X0, parent.Y0 + child.Y0,
			parent.X0 + child.X1, parent.Y0 + child.Y1);

	private static int CaptionText(ShellSaveButton button) => button switch {
		ShellSaveButton.Cancel => 0x33,
		ShellSaveButton.Accept => 0x34,
		ShellSaveButton.Save => 0x1d,
		ShellSaveButton.Restore => 0x1e,
		_ => 0x1f,
	};

	/// <summary>The content panel's colour fields, written by the builder over the class defaults.</summary>
	private const byte PanelBorder = 0x27;
	private const byte PanelFace = 0x25;
	private const byte PanelBodyDither = 0x10;
	private const int TitleHeight = 0x13;
	private const int TitlePlateFirst = 0x70;
	private const int TitlePlateLast = 0xf3;

	/// <summary>The three framed sub-panels', which the builder flattens to the interior colour.</summary>
	private const byte FrameBorder = 0x15;
	private const byte FrameFace = ShellChrome.InteriorColor;

	private const byte ButtonBorder = 0x22;

	/// <summary>
	/// The colour a gated control greys to — the caption and the border alike, and the same value at
	/// every gated button in the shell.
	/// </summary>
	private const byte DisabledColor = 0x26;

	private const byte RowColor = 0x27;
	private const byte SelectedRowColor = 0x29;
	private const byte LabelColor = 0x1a;

	/// <summary><c>estext.bin</c> indices the screen prints, in the order they appear on it.</summary>
	private const int TitleText = 0x1b;
	private const int NameLabel = 0x24;
	private const int SkillLabel = 0x25;
	private const int RankLabel = 0x26;
	private const int HercKillsLabel = 0x27;
	private const int FlyerKillsLabel = 0x28;
	private const int BaseKillsLabel = 0x29;
	private const int CurrentColumnLabel = 0x2a;
	private const int TotalColumnLabel = 0x2b;
	private const int SalvageLabel = 0x2c;
	private const int MissionLabel = 0x2d;
	private const int SectorLabel = 0x2e;
	private const int TonsWord = 0x2f;

	/// <summary>
	/// Three runs of consecutive words the screen indexes into, each by a field's own value.
	///
	/// <para>The sector run is reached as <c>sector + 0x76</c> and <c>0x76</c> is <c>Razor</c>, a
	/// chassis name — the run of five sector names starts at <c>0x77</c>. That is not an off-by-one
	/// here: the campaign stage counts from one at runtime where the save stores it from zero, so
	/// stage 1 lands on the first name. Three other tables in the shell say the same thing.</para>
	/// </summary>
	private const int FirstSkillWord = 0x35;
	private const int FirstRankWord = 0x39;
	private const int FirstSectorWord = 0x76;

	/// <summary>The shell's kilograms-to-tons divisor, in the salvage figure's own <c>sprintf</c>.</summary>
	private const int KilogramsPerTon = 1000;
}
