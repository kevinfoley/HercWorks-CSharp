using HercWorks.Core.Data.File.Dyn;
using Herculan.Engine.Content;

namespace Herculan.Engine.Shell;

/// <summary>
/// The two portrait banks the crew screen loads in its builder: <c>dba\c_pilots.dba</c>, one frame
/// per roster id, and <c>dba\star.dba</c>, whose single frame stands for the player.
/// </summary>
public sealed record ShellCrewPortraits(DynamixBitmap[]? Pilots, DynamixBitmap[]? Star) {
	/// <summary>Reads both banks. A missing one leaves its portraits blank rather than failing the screen.</summary>
	public static ShellCrewPortraits Load(GameContent content) =>
		new(ShellArt.ReadBankFrames(content, "C_PILOTS"), ShellArt.ReadBankFrames(content, "STAR"));

	/// <summary>A pilot's portrait, the <c>c_pilots</c> frame their roster id names.</summary>
	public DynamixBitmap? Pilot(int rosterId) =>
		Pilots != null && rosterId >= 0 && rosterId < Pilots.Length ? Pilots[rosterId] : null;

	/// <summary>The player's portrait, which is always the star.</summary>
	public DynamixBitmap? Player => Star is { Length: > 0 } ? Star[0] : null;
}

/// <summary>
/// Tab 6, <c>CREW</c> — the <c>PILOT ASSIGNMENT</c> panel: the three squad members as portraits across
/// the top, and four rows below them, the player's and one per squad position, each with a portrait
/// and the pilot's name, skill and machine. Built by <c>Crew_BuildScreen</c> (<c>00440eb8</c>,
/// <c>wcrewi.cpp</c>), entered by <c>maybe_Crew_Show</c> (<c>00441a01</c>), its rows coloured by
/// <c>Crew_ColourRows</c> (<c>00441857</c>), matched to pilots by <c>Crew_MatchRowPilots</c> (<c>00441b08</c>), selected by <c>Crew_SelectRow</c> (<c>00441b85</c>) and
/// filled by <c>Crew_FillRows</c> (<c>00441c4f</c>). See docs/shell/screen-layout.md, "The crew screen".
///
/// <para><b>Every rect here is a literal in the executable</b> and they are kept parent-relative as
/// the builder writes them: the content panel in the canvas, the portraits, the label and CLEAR in the
/// panel, the four rows in the panel, and each row's portrait and six texts in that row.</para>
///
/// <para><b>The left of the canvas is the squad panel</b> — the bay picture, the pilot readout and the
/// <c>Squad Inventory</c> roster, which <see cref="ShellSquadPanel"/> draws for this tab and three
/// others. This screen decides which bay it shows.</para>
///
/// <para>Nothing here answers a click yet: choosing a row or a portrait, assigning a pilot through the
/// roster, and CLEAR are not ported. The screen shows the state its entry leaves.</para>
/// </summary>
public sealed class ShellCrewScreen {
	/// <summary>The four rows: the player's, then squad positions 1-3.</summary>
	public const int RowCount = 4;

	/// <summary>The squad members offered across the top.</summary>
	public const int AvailablePilotCount = 3;

	/// <summary>The content panel, in the canvas — the same rect as the repair screen's.</summary>
	public static readonly ShellRect PanelRect = new(0xf1, 0x2b, 0x278, 0x1d9);

	/// <summary>The three squad members' portraits, in the panel, each an image panel with its bitmap one pixel in.</summary>
	private static readonly ShellRect[] AvailablePortraitRects = {
		new(0xbc, 0x19, 0xf7, 0x50),
		new(0xfc, 0x19, 0x137, 0x50),
		new(0x13c, 0x19, 0x177, 0x50),
	};

	private static readonly ShellRect AvailableLabelRect = new(10, 0x1d, 0xb4, 0x29);
	private static readonly ShellRect ClearRect = new(0x99, 0x188, 0xdf, 0x197);

	/// <summary>The rows, in the panel: 71 tall on a 74-pixel pitch.</summary>
	private const int RowLeft = 0xd;
	private const int RowRight = 0x178;
	private const int FirstRowTop = 0x58;
	private const int RowPitch = 0x4a;
	private const int RowBottomOffset = 0x46;

	/// <summary>A row's portrait, in the row. Its bitmap sits at the corner, under the border.</summary>
	private static readonly ShellRect RowPortraitRect = new(0xb, 9, 0x43, 0x3d);

	/// <summary>The three labels, right-aligned, and the three values, left-aligned, in the row.</summary>
	private static readonly ShellRect[] LabelRects = {
		new(100, 0xb, 0xec, 0x18),
		new(100, 0x18, 0xec, 0x25),
		new(100, 0x25, 0xec, 0x32),
	};

	private static readonly ShellRect[] ValueRects = {
		new(0xef, 0xb, 0x164, 0x18),
		new(0xef, 0x18, 0x164, 0x25),
		new(0xef, 0x25, 0x164, 0x32),
	};

	private readonly ShellHangar _hangar;
	private readonly ShellBayPictures? _pictures;
	private readonly ShellCrewPortraits? _portraits;

	/// <summary>
	/// Enters the screen with <paramref name="bay"/> selected, the way the previous tab left
	/// <c>DAT_00482ae5</c>; the entry then moves it — see <see cref="SelectedBay"/>.
	/// </summary>
	public ShellCrewScreen(ShellHangar? hangar = null, int bay = -1, ShellBayPictures? pictures = null,
			ShellCrewPortraits? portraits = null) {
		_hangar = hangar ?? ShellHangar.From(null);
		_pictures = pictures;
		_portraits = portraits;
		SelectedBay = bay;
		Enter();
	}

	/// <summary>
	/// <c>DAT_004776dc</c> — which of the four rows is selected. It is 0 in the image and every entry
	/// leaves it at 0, so with no clicks ported it is always the player's row.
	/// </summary>
	public int SelectedRow { get; private set; }

	/// <summary>
	/// <c>DAT_00482ae5</c>, the bay the squad panel shows. <b>Entering the screen leaves it on the last
	/// bay that holds a finished machine</b>: after selecting the player's row, the entry offers every
	/// occupied bay in turn to <c>Squad_SelectBay</c> (<c>0043d64d</c>), and the crew arm accepts each finished one — so the
	/// readout is not the player's until the player's row is clicked. See docs/shell/screen-layout.md,
	/// "The crew screen".
	/// </summary>
	public int SelectedBay { get; private set; }

	/// <summary>
	/// The pilot a row shows — the player on row 0 (the record embedded in the player structure), and
	/// on rows 1-3 the squad member whose position is that row (<c>Crew_MatchRowPilots</c>, <c>00441b08</c>), or null.
	/// </summary>
	public ShellBayPilot? RowPilot(int row) => row == 0 ? _hangar.Player : _hangar.SquadMemberAt(row);

	/// <summary>
	/// <c>maybe_Crew_Show</c>. The tab handler sets <c>DAT_0047581c</c> to 6 before calling it — the one
	/// tab handler that does so before its builder rather than after — so every bay move below takes
	/// <c>Squad_SelectBay</c> (<c>0043d64d</c>)'s crew arm.
	/// </summary>
	private void Enter() {
		for (int row = 0; row < RowCount; row++) {
			SelectRow(row);
		}

		SelectRow(0);

		for (int bay = 0; bay < ShellHangar.BayCount; bay++) {
			if (_hangar.Bay(bay) != null) {
				SelectBay(bay);
			}
		}
	}

	/// <summary>
	/// <c>Crew_SelectRow</c> (<c>00441b85</c>) without its repaints: moves the bay to the row's pilot's — the player's own
	/// bay for row 0, none for a row with no pilot — and only then makes the row the selected one, so
	/// the Razor test inside the move sees the row being left.
	/// </summary>
	private void SelectRow(int row) {
		SelectBay(RowPilot(row)?.Bay ?? -1);
		SelectedRow = row;
	}

	/// <summary><c>Squad_SelectBay</c> (<c>0043d64d</c>)'s crew arm, with the assignment it makes on a roster click left out.</summary>
	private void SelectBay(int bay) {
		if (bay != SelectedBay && ShellSquadPanel.CanSelectForCrew(_hangar, bay, SelectedRow)) {
			SelectedBay = bay;
		}
	}

	/// <summary>One row's rect, in the canvas.</summary>
	public static ShellRect RowRect(int row) =>
		new(PanelRect.X0 + RowLeft, PanelRect.Y0 + row * RowPitch + FirstRowTop,
			PanelRect.X0 + RowRight, PanelRect.Y0 + row * RowPitch + FirstRowTop + RowBottomOffset);

	/// <summary>
	/// Draws the whole screen into <paramref name="surface"/>, the squad panel included. The caller
	/// clears it first; the content panel's body is filled, as the repair screen's is.
	/// </summary>
	public void Paint(ShellSurface surface, ShellText? text, HudSpriteSheet? sprites) {
		var font = sprites?.Font(ShellArt.ScreenFont);

		ShellSquadPanel.Paint(surface, font, text, _hangar, SelectedBay, _pictures);

		ShellChrome.PaintTitledPanel(surface, PanelRect, PanelBorder, PanelFace, ShellChrome.InteriorColor,
			TitleHeight, headerChrome: true, TitlePlateFirst, TitlePlateLast, fill: true);
		ShellChrome.PaintText(surface,
			new ShellRect(PanelRect.X0, PanelRect.Y0, PanelRect.X1, PanelRect.Y0 + TitleHeight), font,
			text?.Text(TitleText), ShellTextAlign.Center, ShellChrome.FontInkColor);

		// Crew_BuildScreen gives the three portraits border 0x21 and nothing on entry changes it; only a
		// click on one lights it.
		var squad = _hangar.SquadMembers;
		for (int i = 0; i < AvailablePilotCount; i++) {
			ShellChrome.PaintImagePanel(surface, Inside(PanelRect, AvailablePortraitRects[i]),
				i < squad.Count ? _portraits?.Pilot(squad[i].RosterId) : null, 1, 1, UnlitBorder);
		}

		PaintClearButton(surface, font, text);
		ShellChrome.PaintText(surface, Inside(PanelRect, AvailableLabelRect), font,
			text?.Text(AvailablePilotsText), ShellTextAlign.Right, ShellChrome.FontInkColor);

		for (int row = 0; row < RowCount; row++) {
			PaintRow(surface, font, text, row);
		}
	}

	/// <summary>
	/// One row: a line-filled box with an inner border, lit when it is the selected row, holding a
	/// portrait and three label/value pairs. Rows inside the squad's positions (<c>Crew_ColourRows</c>, <c>00441857</c>)
	/// take a <c>0x25</c> body, greyer labels and value backing to match; the rest keep the shell's
	/// background. The values are drawn in <c>0x29</c> whichever: <c>Crew_ColourRows</c> writes them
	/// <c>0x16</c> or <c>0x17</c>, and <c>Crew_FillRows</c> (<c>00441c4f</c>)'s <c>Text_SetString</c> then overwrites that
	/// with its own <c>0x29</c>.
	/// </summary>
	private void PaintRow(ShellSurface surface, HudFont? font, ShellText? text, int row) {
		var rect = RowRect(row);
		bool inPlay = row < _hangar.SquadPositions;
		byte border = row == SelectedRow ? LitBorder : UnlitBorder;
		byte body = inPlay ? InPlayBody : ShellChrome.InteriorColor;
		byte labelColor = inPlay ? InPlayLabelColor : LabelColor;

		ShellChrome.PaintHatchedDivider(surface, rect, border, body, firstLine: 0, innerBorder: true);

		var pilot = RowPilot(row);
		var portrait = row == 0 ? _portraits?.Player : pilot != null ? _portraits?.Pilot(pilot.RosterId) : null;
		ShellChrome.PaintImagePanel(surface, Inside(rect, RowPortraitRect), portrait, 0, 0, border);

		// Crew_FillRows (00441c4f): a row with no pilot has all three values set to estext.bin entry 0, the empty
		// string, and a pilot with no machine reads None.
		string?[] values = pilot == null
			? new string?[] { null, null, null }
			: new[] {
				pilot.Name,
				text?.Text(FirstSkillText + pilot.Skill),
				text?.Text(_hangar.Bay(pilot.Bay) is { } machine
					? FirstChassisNameText + machine.ChassisType : NoHercText),
			};

		for (int i = 0; i < LabelRects.Length; i++) {
			ShellChrome.PaintText(surface, Inside(rect, LabelRects[i]), font, text?.Text(FirstLabelText + i),
				ShellTextAlign.Right, labelColor);
			ShellChrome.PaintText(surface, Inside(rect, ValueRects[i]), font, values[i], ShellTextAlign.Left,
				ShellChrome.FontInkColor, body);
		}
	}

	/// <summary>
	/// CLEAR, a live button no code greys. Its caption is the <c>Text</c> child <c>Button_Ctor</c>
	/// builds at <c>{1, 0, w, h}</c> in the button, so it is centred one pixel right of the button's
	/// own rect.
	/// </summary>
	private static void PaintClearButton(ShellSurface surface, HudFont? font, ShellText? text) {
		var rect = Inside(PanelRect, ClearRect);
		ShellChrome.PaintPanel(surface, rect, ButtonBorder, fill: true);
		ShellChrome.PaintText(surface, new ShellRect(rect.X0 + 1, rect.Y0, rect.X1, rect.Y1), font,
			text?.Text(ClearText), ShellTextAlign.Center, ShellChrome.FontInkColor);
	}

	private static ShellRect Inside(ShellRect parent, ShellRect child) =>
		new(parent.X0 + child.X0, parent.Y0 + child.Y0, parent.X0 + child.X1, parent.Y0 + child.Y1);

	/// <summary>The content panel's fields, written by the builder over the class defaults.</summary>
	private const byte PanelBorder = 0x27;
	private const byte PanelFace = 0x25;
	private const int TitleHeight = 0x13;
	private const int TitlePlateFirst = 0x7e;
	private const int TitlePlateLast = 0x108;

	/// <summary>The border every row and portrait takes, and the one the selected row and its portrait take.</summary>
	private const byte UnlitBorder = 0x21;
	private const byte LitBorder = 0x29;

	private const byte ButtonBorder = 0x22;

	/// <summary><c>Crew_ColourRows</c> (<c>00441857</c>)'s two sets: a row inside the squad's positions, and one outside them.</summary>
	private const byte InPlayBody = 0x25;
	private const byte InPlayLabelColor = 0x19;
	private const byte LabelColor = 0x1a;

	/// <summary><c>estext.bin</c> indices the screen prints.</summary>
	private const int FirstSkillText = 0x35;
	private const int FirstChassisNameText = 0x6e;
	private const int NoHercText = 0x7e;
	private const int TitleText = 0xa8;
	private const int ClearText = 0xa9;
	private const int AvailablePilotsText = 0xab;

	/// <summary><c>Name:</c>, <c>Skill:</c> and <c>Herc:</c>, consecutive from here.</summary>
	private const int FirstLabelText = 0xac;
}
