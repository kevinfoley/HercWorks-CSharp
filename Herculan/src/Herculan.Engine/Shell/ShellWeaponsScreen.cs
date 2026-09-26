using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Core.Io.Transform.Shell;
using Herculan.Engine.Content;

namespace Herculan.Engine.Shell;

/// <summary>
/// The WEAPONS screen's buttons, in the order the builder constructs them: the four guidance kinds,
/// the missile rack's own name, and the two hardpoint steppers.
/// </summary>
public enum ShellWeaponsButton {
	Arm,
	Arh,
	Sarh,
	Eo,
	Weapon,
	PreviousHardpoint,
	NextHardpoint,
}

/// <summary>
/// What the WEAPONS screen loads beside the save: <c>gam\arm_weap.dat</c> and <c>dba\arm_weap.dba</c>,
/// the picture of each weapon and each guidance kind, and <c>wpn_desc.bin</c>, three lines of prose
/// per weapon id. Any of them missing leaves its part of the screen blank rather than failing it.
/// </summary>
public sealed class ShellWeaponsArt {
	private readonly Dictionary<int, UiHardpointGraphic> _weapons = new();
	private readonly Dictionary<int, UiHardpointGraphic> _guidance = new();
	private readonly DynamixBitmap[]? _bank;

	private ShellWeaponsArt(ArmWeap? layout, DynamixBitmap[]? bank, ShellText? descriptions) {
		_bank = bank;
		Descriptions = descriptions;
		foreach (var record in layout?.Entries ?? Array.Empty<UiHardpointGraphic>()) {
			_weapons[record.Id] = record;
		}

		foreach (var record in layout?.Secondary ?? Array.Empty<UiHardpointGraphic>()) {
			_guidance[record.Id] = record;
		}
	}

	/// <summary><c>wpn_desc.bin</c>, opened by <c>Arming_BuildScreen</c> into <c>DAT_0048d728</c>.</summary>
	public ShellText? Descriptions { get; }

	/// <summary>How many weapons have a picture — 26 in the retail file.</summary>
	public int WeaponCount => _weapons.Count;

	public static ShellWeaponsArt Load(GameContent content) =>
		new(content.Read(ShellRepairCosts.CatalogFolder, "ARM_WEAP.DAT") is { } bytes
				? new ArmWeapTransformer().Parse(bytes) : null,
			ShellArt.ReadBankFrames(content, "ARM_WEAP"),
			ShellText.Load(content, "WPN_DESC.BIN"));

	/// <summary>A weapon's picture and where it sits in the picture box, or null for an id with no record.</summary>
	public (DynamixBitmap Frame, int X, int Y)? Weapon(int weaponId) => Picture(_weapons, weaponId);

	/// <summary>A guidance kind's picture, by the kind's own id, 0-3.</summary>
	public (DynamixBitmap Frame, int X, int Y)? Guidance(int kind) => Picture(_guidance, kind);

	private (DynamixBitmap Frame, int X, int Y)? Picture(Dictionary<int, UiHardpointGraphic> records, int id) =>
		records.TryGetValue(id, out var record) && _bank is { } bank
			&& record.FrameId >= 0 && record.FrameId < bank.Length
			? (bank[record.FrameId], record.OriginX, record.OriginY) : null;
}

/// <summary>
/// Tab 2, <c>WEAPONS</c> — the arming screen: a picture of the selected weapon over three lines of its
/// description, the <c>Weapons Inventory</c> list of every weapon the armory holds, and the
/// <c>Hard Points</c> steppers. Built once by <c>Arming_BuildScreen</c> (<c>0043e52c</c>,
/// <c>warmingi.cpp</c>), entered by <c>Arming_Enter</c> (<c>0043f548</c>), its row moved by
/// <c>Arming_SelectRow</c> (<c>0043f71c</c>) and its guidance kind by <c>Arming_ShowGuidance</c>
/// (<c>0043fd69</c>). See docs/shell/screen-layout.md, "The weapons screen".
///
/// <para><b>Every rect here is a literal in the executable</b>, kept parent-relative as the builder
/// writes them: the content panel in the canvas; the picture box, the list and the steppers in the
/// content panel; the rows in the list; the pictures, the five buttons and the description in the
/// picture box. A weapon's picture is the exception, placed by its <c>arm_weap.dat</c> record.</para>
///
/// <para><b>The left of the canvas is the squad panel</b>, which <see cref="ShellSquadPanel"/> draws.
/// This tab's arm of <c>Squad_SelectBay</c> (<c>0043d64d</c>) refuses an empty bay and one still being
/// built.</para>
///
/// <para><b>No hardpoint is ever selected here.</b> Every entry and every bay change leaves the
/// selection at <c>-1</c>, and only the steppers and the hotspots on the bay picture move it; neither is
/// ported. With none selected, clicking a row shows the weapon and fits nothing, which is what retail
/// does too.</para>
/// </summary>
public sealed class ShellWeaponsScreen {
	/// <summary>
	/// <c>0x1b</c> rows, one per weapon the table at <c>004769b0</c> lists, in its order: thirteen in the
	/// left column and fourteen in the right, the last of them <c>None</c>.
	/// </summary>
	public static readonly int[] RowWeapons = {
		1, 2, 3, 4, 5, 6, 22, 7, 23, 18, 8, 9, 10,
		11, 12, 13, 14, 15, 16, 17, 24, 25, 29, 30, 31, 32, 0,
	};

	/// <summary>The rows in the left column; the rest are in the right.</summary>
	private const int LeftColumnRows = 0xd;

	/// <summary>The content panel, in the canvas — the repair and crew screens' rect.</summary>
	public static readonly ShellRect PanelRect = new(0xf1, 0x2b, 0x278, 0x1d9);

	/// <summary>The picture box, a line-filled divider, and the list, in the content panel.</summary>
	private static readonly ShellRect PictureBoxRect = new(6, 0x1a, 0x183, 0xc0);
	private static readonly ShellRect ListRect = new(6, 0xc5, 0x183, 400);

	/// <summary>The five buttons down the picture box's right edge, in the box: ARM, ARH, SARH, EO, and the rack's own name.</summary>
	private static readonly ShellRect[] PictureButtonRects = {
		new(0x152, 5, 0x179, 0x16),
		new(0x152, 0x1c, 0x179, 0x2d),
		new(0x152, 0x33, 0x179, 0x44),
		new(0x152, 0x4a, 0x179, 0x5b),
		new(0x101, 0x61, 0x179, 0x72),
	};

	/// <summary>The guidance kind each of the four buttons shows — its <c>wpn_desc.bin</c> block and its <c>arm_weap.dat</c> record.</summary>
	private static readonly int[] ButtonGuidance = { 2, 1, 0, 3 };

	/// <summary>The three description lines' tops and bottoms, in the box; each runs from 1 to the box's own right edge.</summary>
	private static readonly (int Top, int Bottom)[] DescriptionRows = { (0x7a, 0x88), (0x88, 0x94), (0x94, 0xa0) };

	/// <summary><c>Hard Points</c> and its two steppers, in the content panel.</summary>
	private static readonly ShellRect HardPointsLabelRect = new(0xe, 0x195, 0x76, 0x1a1);
	private static readonly ShellRect PreviousHardpointRect = new(0x7d, 0x194, 0x8b, 0x1a3);
	private static readonly ShellRect NextHardpointRect = new(0x91, 0x194, 0x9f, 0x1a3);

	/// <summary>The rows, in the list: 13 tall on a 13-pixel pitch, so they butt without overlapping.</summary>
	private const int LeftRowLeft = 9;
	private const int LeftRowRight = 0xaf;
	private const int RightRowLeft = 0xbf;
	private const int RightRowRight = 0x165;
	private const int FirstRowTop = 0x14;
	private const int RowPitch = 0xd;
	private const int RowBottomOffset = 0xc;

	/// <summary>
	/// Where <c>ListRow_AddColumns</c> is told to cut a row: the name left from <c>2</c> to <c>0x8b</c>,
	/// two one-pixel columns holding a space, and the count right-aligned from <c>0x8d</c> to one inside
	/// the row's right edge.
	/// </summary>
	private const int NameRight = 0x8b;
	private const int SecondRight = 0x8c;
	private const int ThirdRight = 0x8d;

	/// <summary>The weapons that take a guidance kind: the three racks and the Razor's launcher.</summary>
	private const int FirstMissileRack = 0xd;
	private const int LastMissileRack = 0x10;

	/// <summary>Where the guidance kinds' prose starts in <c>wpn_desc.bin</c>, past three lines for each of the 33 weapon ids.</summary>
	private const int FirstGuidanceDescription = 99;
	private const int DescriptionLines = 3;

	private readonly ShellWeaponsArt? _art;
	private readonly ShellBayPictures? _pictures;
	private ShellHangar _hangar;

	/// <summary>
	/// Builds the screen and enters it with <paramref name="bay"/> selected, the bay the previous tab
	/// left in <c>DAT_00482ae5</c>.
	/// </summary>
	public ShellWeaponsScreen(ShellHangar? hangar = null, int bay = -1, ShellWeaponsArt? art = null,
			ShellBayPictures? pictures = null) {
		_hangar = hangar ?? ShellHangar.From(null);
		_art = art;
		_pictures = pictures;
		Enter(_hangar, bay);
	}

	/// <summary><c>DAT_00482ae5</c>, the bay the squad panel shows.</summary>
	public int SelectedBay { get; private set; } = -1;

	/// <summary>
	/// <c>DAT_00476d5a</c>, the lit row, <c>-1</c> in the image. The widgets outlive a visit, so it does too.
	/// </summary>
	public int SelectedRow { get; private set; } = -1;

	/// <summary>
	/// <c>DAT_00476d5c</c>, the guidance kind last put up, <c>-1</c> in the image. Selecting a missile rack
	/// hides its picture and leaves the value, so it is not the same thing as <see cref="ShowingGuidance"/>.
	/// </summary>
	public int ShownGuidance { get; private set; } = -1;

	/// <summary>Whether the picture box shows <see cref="ShownGuidance"/>'s picture rather than the selected weapon's.</summary>
	public bool ShowingGuidance { get; private set; }

	/// <summary><c>DAT_004769e6</c> — whether the five buttons are up. Only a missile rack's row puts them up.</summary>
	public bool GuidanceButtonsShown { get; private set; }

	/// <summary>The guidance button whose border is lit <c>0x20</c>, as an index into <see cref="ButtonGuidance"/>, or <c>-1</c>.</summary>
	private int _litGuidanceButton = -1;

	/// <summary>The first of the three <c>wpn_desc.bin</c> lines under the picture, or <c>-1</c> before any row is selected.</summary>
	private int _description = -1;

	/// <summary>The weapon id a row lists.</summary>
	public static int WeaponOfRow(int row) => row >= 0 && row < RowWeapons.Length ? RowWeapons[row] : -1;

	/// <summary>The selected row's weapon id, or <c>-1</c>.</summary>
	public int SelectedWeapon => WeaponOfRow(SelectedRow);

	/// <summary>
	/// <c>Arming_Enter</c> (<c>0043f548</c>). When the bay it inherits is none, empty or unfinished it
	/// selects the first bay holding a finished machine (<c>Herc_FirstBuiltBay</c>, <c>00410c2a</c>); then it
	/// clears the hardpoint and selects row 0.
	///
	/// <para>The original selects that bay through <c>Squad_SelectBay</c> under the previous tab's rule,
	/// because the tab handler stores 2 in <c>DAT_0047581c</c> only after the entry — a retail bug, recorded
	/// in KNOWN_ISSUES.md. This engine applies the arming tab's rule, which takes any finished machine.</para>
	/// </summary>
	public void Enter(ShellHangar hangar, int bay) {
		_hangar = hangar;
		SelectedBay = hangar.Bay(bay) is { IsBuilt: true } ? bay : hangar.FirstBuiltBay();
		SelectRow(0);
	}

	/// <summary>
	/// <c>Squad_SelectBay</c> (<c>0043d64d</c>)'s arming arm: an empty bay and an unfinished machine are
	/// refused. Otherwise the hardpoint is cleared, row 0 selected, and the bay taken. Returns whether the
	/// bay moved.
	/// </summary>
	public bool ClickRoster(int bay) {
		if (bay == SelectedBay || bay < 0 || bay >= ShellHangar.BayCount || _hangar.Bay(bay) is not { IsBuilt: true }) {
			return false;
		}

		SelectRow(0);
		SelectedBay = bay;
		return true;
	}

	/// <summary>
	/// <c>Arming_SelectRow</c> (<c>0043f71c</c>) with no hardpoint selected, which is a row's handler through
	/// the 27 thunks from <c>00440300</c>. It is a no-op for the row already lit unless a guidance picture has
	/// been put up since. It lights the row and shows its weapon's picture — a blank box for <c>None</c> —
	/// and three lines of <c>wpn_desc.bin</c> from <c>id * 3</c>. A missile rack's row puts up the four
	/// guidance buttons and a fifth captioned with the rack's name; any other row takes all five down and
	/// forgets the guidance kind. Returns whether anything changed.
	/// </summary>
	public bool SelectRow(int row) {
		if (row < 0 || row >= RowWeapons.Length || (row == SelectedRow && ShownGuidance == -1)) {
			return false;
		}

		int weapon = RowWeapons[row];
		SelectedRow = row;
		ShowingGuidance = false;
		_description = weapon * DescriptionLines;

		if (weapon is >= FirstMissileRack and <= LastMissileRack) {
			GuidanceButtonsShown = true;
		} else {
			GuidanceButtonsShown = false;
			_litGuidanceButton = -1;
			ShownGuidance = -1;
		}

		return true;
	}

	/// <summary>
	/// <c>Arming_ShowGuidance</c> (<c>0043fd69</c>) as a guidance button calls it: lights the button, swaps the
	/// weapon's picture for the kind's, and prints the kind's three lines from <c>99 + kind * 3</c>. It
	/// then writes the kind into the selected hardpoint's mount, which with none selected does nothing.
	/// </summary>
	public void ShowGuidance(int button) {
		if (button < 0 || button >= ButtonGuidance.Length) {
			return;
		}

		int kind = ButtonGuidance[button];
		_litGuidanceButton = button;
		ShowingGuidance = true;
		ShownGuidance = kind;
		_description = FirstGuidanceDescription + kind * DescriptionLines;
	}

	/// <summary>
	/// A row's state as <c>Arming_RefreshRows</c> (<c>0043fbc6</c>) leaves it, before the lit row is drawn
	/// over it: a weapon that is still locked is disabled with every column in <c>0x10</c>, the background.
	/// With no hardpoint selected every unlocked weapon is live.
	/// </summary>
	public bool IsRowEnabled(int row) => row == SelectedRow || _hangar.IsWeaponUnlocked(WeaponOfRow(row));

	/// <summary>One row's rect, in the canvas.</summary>
	public static ShellRect RowRect(int row) {
		var list = Inside(PanelRect, ListRect);
		bool left = row < LeftColumnRows;
		int top = (left ? row : row - LeftColumnRows) * RowPitch + FirstRowTop;
		return new ShellRect(list.X0 + (left ? LeftRowLeft : RightRowLeft), list.Y0 + top,
			list.X0 + (left ? LeftRowRight : RightRowRight), list.Y0 + top + RowBottomOffset);
	}

	/// <summary>One button's rect, in the canvas.</summary>
	public static ShellRect ButtonRect(ShellWeaponsButton button) => button switch {
		ShellWeaponsButton.PreviousHardpoint => Inside(PanelRect, PreviousHardpointRect),
		ShellWeaponsButton.NextHardpoint => Inside(PanelRect, NextHardpointRect),
		_ => Inside(Inside(PanelRect, PictureBoxRect), PictureButtonRects[(int)button]),
	};

	private bool IsShown(ShellWeaponsButton button) =>
		button is ShellWeaponsButton.PreviousHardpoint or ShellWeaponsButton.NextHardpoint || GuidanceButtonsShown;

	/// <summary>
	/// What the pointer hits: a live row and which of its text columns, or a button that is up. A locked
	/// row is disabled and swallows a click, and so does the picture box, whose builder clears its enable
	/// flag — here the same as hitting nothing.
	/// </summary>
	public ShellHit? HitAt(float canvasX, float canvasY) {
		foreach (var button in Enum.GetValues<ShellWeaponsButton>()) {
			if (IsShown(button) && ButtonRect(button).Contains(canvasX, canvasY)) {
				return ShellHit.Button(new ShellWidget(ShellWidgetKind.WeaponsButton, (int)button), ButtonRect(button),
					canvasX, canvasY);
			}
		}

		for (int row = 0; row < RowWeapons.Length; row++) {
			var rect = RowRect(row);
			if (rect.Contains(canvasX, canvasY)) {
				return IsRowEnabled(row)
					? ShellHit.ListRow(new ShellWidget(ShellWidgetKind.WeaponsRow, row), rect, NameRight, SecondRight,
						ThirdRight, canvasX, canvasY)
					: null;
			}
		}

		return null;
	}

	/// <summary>Draws the whole screen into <paramref name="surface"/>, the squad panel included. The caller clears it first.</summary>
	public void Paint(ShellSurface surface, ShellText? text, HudSpriteSheet? sprites) {
		var font = sprites?.Font(ShellArt.ScreenFont);

		ShellSquadPanel.Paint(surface, font, text, _hangar, SelectedBay, _pictures);

		ShellChrome.PaintTitledPanel(surface, PanelRect, Border, PanelFace, ShellChrome.InteriorColor, TitleHeight,
			headerChrome: true, TitlePlateFirst, TitlePlateLast, fill: true);
		ShellChrome.PaintText(surface, TitleRect(PanelRect), font, text?.Text(TitleText), ShellTextAlign.Center,
			ShellChrome.FontInkColor);

		PaintList(surface, font, text);
		PaintPictureBox(surface, font, text);

		ShellChrome.PaintText(surface, Inside(PanelRect, HardPointsLabelRect), font, text?.Text(HardPointsText),
			ShellTextAlign.Right, ShellChrome.FontInkColor);
		PaintButton(surface, font, ShellWeaponsButton.PreviousHardpoint, "<", ButtonBorder);
		PaintButton(surface, font, ShellWeaponsButton.NextHardpoint, ">", ButtonBorder);
	}

	/// <summary>
	/// The <c>Weapons Inventory</c> list: a flat header with no hatch over a filled body, and the 27 rows.
	/// A row reads the weapon's name and, right-aligned, how many the armory holds — two spaces for
	/// <c>None</c>. A locked weapon's row is all background; the lit row's border and text are <c>0x29</c>.
	/// </summary>
	private void PaintList(ShellSurface surface, HudFont? font, ShellText? text) {
		var list = Inside(PanelRect, ListRect);
		ShellChrome.PaintTitledPanel(surface, list, Border, ListHeaderFace, ShellChrome.InteriorColor, TitleHeight,
			headerChrome: false, 0, 0, fill: true);
		ShellChrome.PaintText(surface, TitleRect(list), font, text?.Text(ListTitleText), ShellTextAlign.Center,
			ShellChrome.FontInkColor);

		for (int row = 0; row < RowWeapons.Length; row++) {
			int weapon = RowWeapons[row];
			bool selected = row == SelectedRow;
			bool unlocked = _hangar.IsWeaponUnlocked(weapon);
			byte color = selected ? LitColor : unlocked ? RowColor : ShellChrome.InteriorColor;
			string count = !unlocked ? LockedCount : weapon == 0 ? NoneCount : $"{_hangar.WeaponsOwned(weapon)}";

			var rect = RowRect(row);
			ShellChrome.PaintPanel(surface, rect, selected ? LitColor : ShellChrome.InteriorColor, fill: true);
			Column(surface, font, rect, 2, NameRight, text?.Text(FirstWeaponNameText + weapon), ShellTextAlign.Left, color);
			Column(surface, font, rect, NameRight, SecondRight, " ", ShellTextAlign.Left, color);
			Column(surface, font, rect, SecondRight, ThirdRight, " ", ShellTextAlign.Center, color);
			Column(surface, font, rect, ThirdRight, rect.Width - 2, count, ShellTextAlign.Right, color);
		}
	}

	/// <summary>
	/// The picture box: black above row <c>0x76</c> and a solid <c>0x25</c> band below it, the builder's
	/// first line and line colour. The selected weapon's picture — or the guidance kind's — sits in the
	/// black with no border, the three description lines are centred in the band, and a missile rack
	/// puts its five buttons up the right-hand side.
	/// </summary>
	private void PaintPictureBox(ShellSurface surface, HudFont? font, ShellText? text) {
		var box = Inside(PanelRect, PictureBoxRect);
		ShellChrome.PaintHatchedDivider(surface, box, PictureBoxBorder, DescriptionBand, DescriptionFirstLine,
			innerBorder: false);

		var picture = ShowingGuidance ? _art?.Guidance(ShownGuidance)
			: SelectedWeapon > 0 ? _art?.Weapon(SelectedWeapon) : null;
		if (picture is { } shown) {
			var (frame, x, y) = shown;
			var rect = new ShellRect(box.X0 + x, box.Y0 + y, box.X0 + x + frame.Cols, box.Y0 + y + frame.Rows);
			ShellChrome.PaintImagePanel(surface, rect, frame, 0, 0, Border, border: false);
		}

		if (GuidanceButtonsShown) {
			for (int button = 0; button < ButtonGuidance.Length; button++) {
				PaintButton(surface, font, (ShellWeaponsButton)button, text?.Text(FirstGuidanceText + button),
					button == _litGuidanceButton ? LitGuidanceBorder : ButtonBorder);
			}

			PaintButton(surface, font, ShellWeaponsButton.Weapon, text?.Text(FirstWeaponNameText + SelectedWeapon),
				ButtonBorder);
		}

		for (int line = 0; line < DescriptionRows.Length; line++) {
			var (top, bottom) = DescriptionRows[line];
			ShellChrome.PaintText(surface, new ShellRect(box.X0 + 1, box.Y0 + top, box.X1, box.Y0 + bottom), font,
				_description >= 0 ? _art?.Descriptions?.Text(_description + line) : null, ShellTextAlign.Center,
				ShellChrome.FontInkColor, DescriptionBand);
		}
	}

	/// <summary>
	/// One button: its double-bordered box in <paramref name="border"/> and its caption, the <c>Text</c> child
	/// <c>Button_Ctor</c> builds at <c>{1, 0, w, h}</c>, centred in <c>0x29</c>.
	/// </summary>
	private static void PaintButton(ShellSurface surface, HudFont? font, ShellWeaponsButton button, string? caption,
			byte border) {
		var rect = ButtonRect(button);
		ShellChrome.PaintButton(surface, rect, border);
		ShellChrome.PaintText(surface, new ShellRect(rect.X0 + 1, rect.Y0, rect.X1, rect.Y1), font, caption,
			ShellTextAlign.Center, ShellChrome.FontInkColor);
	}

	private static void Column(ShellSurface surface, HudFont? font, ShellRect row, int left, int right,
			string? value, ShellTextAlign align, byte color) =>
		ShellChrome.PaintText(surface,
			new ShellRect(row.X0 + left, row.Y0, row.X0 + right, row.Y0 + row.Height - 1), font, value, align,
			color, ShellChrome.InteriorColor);

	private static ShellRect TitleRect(ShellRect panel) => new(panel.X0, panel.Y0, panel.X1, panel.Y0 + TitleHeight);

	private static ShellRect Inside(ShellRect parent, ShellRect child) =>
		new(parent.X0 + child.X0, parent.Y0 + child.Y0, parent.X0 + child.X1, parent.Y0 + child.Y1);

	private const byte Border = 0x27;
	private const byte PanelFace = 0x25;
	private const int TitleHeight = 0x13;
	private const int TitlePlateFirst = 0x66;
	private const int TitlePlateLast = 0x116;

	/// <summary><c>TitledPanel_Ctor</c>'s own header face, which the builder leaves on the list.</summary>
	private const byte ListHeaderFace = 0x24;

	/// <summary>The picture box's border, and the row its line fill starts on and the colour of those lines.</summary>
	private const byte PictureBoxBorder = 0x15;
	private const int DescriptionFirstLine = 0x76;
	private const byte DescriptionBand = 0x25;

	private const byte RowColor = 0x27;
	private const byte LitColor = 0x29;
	private const byte ButtonBorder = 0x22;
	private const byte LitGuidanceBorder = 0x20;

	/// <summary><c>"%d"</c> of the count at <c>00477428</c>; <c>None</c>'s two spaces at <c>00477425</c>; and the builder's <c>"0"</c>, which a locked row keeps.</summary>
	private const string NoneCount = "  ";
	private const string LockedCount = "0";

	/// <summary><c>estext.bin</c> indices the screen prints.</summary>
	private const int TitleText = 0x9f;
	private const int ListTitleText = 0xa0;
	private const int FirstGuidanceText = 0xa1;
	private const int HardPointsText = 0xa5;

	/// <summary>A weapon's name is <c>estext.bin</c> <c>0x7e + id</c>; <c>0x7e</c> itself is <c>None</c>.</summary>
	private const int FirstWeaponNameText = 0x7e;
}
