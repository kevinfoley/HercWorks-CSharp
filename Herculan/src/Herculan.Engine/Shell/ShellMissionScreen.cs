using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.File.Sav;
using Herculan.Engine.Content;

namespace Herculan.Engine.Shell;

/// <summary>The four buttons along the foot of the mission tab, in the order the builder constructs them.</summary>
public enum ShellMissionButton {
	Briefing,
	Objectives,
	Intelligence,
	RockAndRoll,
}

/// <summary>
/// The mission tab's eight arrow buttons: six down the right of the <c>Mission Map</c> panel, named for
/// the art they draw in the reference capture, then the <c>Mission Summary</c> panel's two page buttons.
/// </summary>
public enum ShellMissionArrow {
	MapUp,
	MapDown,
	MapLeft,
	MapRight,
	MapInward,
	MapOutward,
	PageUp,
	PageDown,
}

/// <summary>
/// The career's three mission texts as <c>Career_BuildBriefingText</c> (<c>00412f97</c>) assembles them:
/// each the concatenation of the <c>mission.str</c> lines one of the career block's arrays names, in
/// array order, skipping <c>-1</c>. The shell reads <c>data\mission.str</c>, which
/// <c>Career_LoadSlot</c> copies from the slot's <c>sav\missn%d.str</c>; this engine reads the slot's
/// own file where it lies, which is the same bytes.
/// </summary>
public sealed record ShellMissionTexts(string Briefing, string Objectives, string Intelligence) {
	public static readonly ShellMissionTexts Empty = new(string.Empty, string.Empty, string.Empty);

	/// <summary>The texts of <paramref name="save"/>, loaded from slot <paramref name="slot"/>, or <see cref="Empty"/>.</summary>
	public static ShellMissionTexts Load(string installRoot, int slot, PlayerSave? save) {
		string path = Path.Combine(ShellSaveSlots.Directory(installRoot), $"missn{slot}.str");
		if (save == null || !File.Exists(path) || SimStringTable.Parse(File.ReadAllBytes(path)) is not { GroupCount: > 0 } table) {
			return Empty;
		}

		// The string-table reader takes its count from the first group's header, so only group 0 is read;
		// a slot file can carry a stale tail past its content length, which the table never reaches.
		var lines = table.Group(0);
		string Assemble((short Count, short[] Lines) array) =>
			string.Concat(array.Lines.Where(line => line >= 0 && line < lines.Count).Select(line => lines[line].Text));

		return new ShellMissionTexts(Assemble(save.CareerBriefing), Assemble(save.CareerObjectives),
			Assemble(save.CareerIntelligence));
	}
}

/// <summary>
/// <c>esreport.cpp</c>'s paginated text box: a 0x36-byte object, not a widget, that word-wraps a string
/// into one <c>Text</c> child of its parent per line and shows one page of them at a time. Built by
/// <c>TextBox_Ctor</c> (<c>0040cc0c</c>), filled by <c>TextBox_SetText</c> (<c>0040cc81</c>), and paged by
/// <c>TextBox_PageUp</c> (<c>0040d237</c>) and <c>TextBox_PageDown</c> (<c>0040d258</c>). See
/// docs/shell/screen-layout.md, "The summary text box".
/// </summary>
public sealed class ShellTextBox {
	private List<string> _lines = new();

	/// <param name="rect">The box's rect in its parent, which every line's rect is cut from.</param>
	public ShellTextBox(ShellRect rect) => Rect = rect;

	public ShellRect Rect { get; }

	/// <summary>The wrapped lines, <c>+0x2a</c>.</summary>
	public IReadOnlyList<string> Lines => _lines;

	/// <summary><c>+0x22</c>: the box's height over the font's cell height.</summary>
	public int LinesPerPage { get; private set; }

	/// <summary><c>+0x20</c>, as <c>TextBox_SetText</c> computes it.</summary>
	public int PageCount { get; private set; }

	/// <summary><c>+0x1e</c>, the page shown.</summary>
	public int Page { get; private set; }

	/// <summary><c>+0x18</c>: whether the box shows a page at all.</summary>
	public bool Visible { get; set; }

	/// <summary>
	/// <c>TextBox_SetText</c>: clears the box, which hides it and puts it back on page 0, wraps the text to
	/// the box's width and counts its pages. The page count is <c>lines / perPage</c>, plus one unless
	/// <c>lines + 1 == perPage</c> — so a text exactly filling its pages gains an empty one, and a text one
	/// line short of a page has none, which leaves the page buttons inert on it.
	/// </summary>
	public void SetText(string? text, HudFont? font) {
		Visible = false;
		Page = 0;
		if (font == null || font.CellHeight <= 0) {
			_lines = new List<string>();
			LinesPerPage = 0;
			PageCount = 0;
			return;
		}

		LinesPerPage = (Rect.Y1 - Rect.Y0) / font.CellHeight;
		_lines = Wrap(text ?? string.Empty, font, Rect.X1 - Rect.X0);
		PageCount = LinesPerPage > 0 ? _lines.Count / LinesPerPage + (_lines.Count + 1 != LinesPerPage ? 1 : 0) : 0;
	}

	/// <summary><c>TextBox_PageUp</c>: back a page while the box is visible and not on the first.</summary>
	public bool PageUp() {
		if (!Visible || Page <= 0) {
			return false;
		}

		Page--;
		return true;
	}

	/// <summary><c>TextBox_PageDown</c>: on a page while the box is visible and not on the last.</summary>
	public bool PageDown() {
		if (!Visible || Page >= PageCount - 1) {
			return false;
		}

		Page++;
		return true;
	}

	/// <summary>
	/// <c>TextBox_ShowPage</c> (<c>0040cee2</c>) and the lines' own paint: line <c>i</c> sits in row
	/// <c>i % perPage</c>, one cell tall, left-aligned in <c>0x28</c> over a backing it clears to
	/// <c>0x10</c>, and only the current page's are shown.
	/// </summary>
	public void Paint(ShellSurface surface, ShellRect parent, HudFont? font) {
		if (!Visible || font == null || LinesPerPage <= 0) {
			return;
		}

		int cell = font.CellHeight;
		for (int i = Page * LinesPerPage; i < _lines.Count && i / LinesPerPage == Page; i++) {
			int top = parent.Y0 + Rect.Y0 + i % LinesPerPage * cell;
			ShellChrome.PaintText(surface, new ShellRect(parent.X0 + Rect.X0, top, parent.X0 + Rect.X1, top + cell), font,
				_lines[i], ShellTextAlign.Left, LineColor, ShellChrome.InteriorColor);
		}
	}

	/// <summary>
	/// <c>TextBox_Wrap</c> (<c>0040d0d6</c>), over a copy of the string as the original works in place: a
	/// newline ends a line; any other control character becomes a space, without being tested as one;
	/// and at each space the line so far is measured, and when it is wider than the box it is broken at
	/// the last break, which becomes the new line's start. The break is then left where it was rather
	/// than moved to this space, and a word the break leaves at the start of a line is not measured
	/// again until the next space. After the last character the final line gets one more such test.
	/// </summary>
	internal static List<string> Wrap(string text, HudFont font, int width) {
		char[] buffer = text.ToCharArray();
		var starts = new List<int> { 0 };
		int lastBreak = 0;

		for (int p = 0; p < buffer.Length; p++) {
			char c = buffer[p];
			if (c == '\n') {
				buffer[p] = '\0';
				starts.Add(p + 1);
				lastBreak = p;
			} else if (c < ' ') {
				buffer[p] = ' ';
			} else if (c == ' ') {
				buffer[p] = '\0';
				int next = p;
				if (width < font.Measure(LineAt(buffer, starts[^1]))) {
					if (buffer[lastBreak] == ' ') {
						buffer[lastBreak] = '\0';
					}

					starts.Add(lastBreak + 1);
					next = lastBreak;
				}

				lastBreak = next;
				buffer[p] = ' ';
			}
		}

		if (width < font.Measure(LineAt(buffer, starts[^1]))) {
			buffer[lastBreak] = '\0';
			starts.Add(lastBreak + 1);
		}

		return starts.ConvertAll(start => LineAt(buffer, start));
	}

	/// <summary>The string a line pointer names: from <paramref name="start"/> to the next terminator.</summary>
	private static string LineAt(char[] buffer, int start) {
		int end = start;
		while (end < buffer.Length && buffer[end] != '\0') {
			end++;
		}

		return start < buffer.Length ? new string(buffer, start, end - start) : string.Empty;
	}

	private const byte LineColor = 0x28;
}

/// <summary>The mission tab's two banks: the arrow faces and the <c>TERRA DEFENSE</c> plate.</summary>
public sealed class ShellMissionArt {
	private readonly DynamixBitmap[]? _arrows;

	private ShellMissionArt(DynamixBitmap[]? arrows, DynamixBitmap? plate) {
		_arrows = arrows;
		Plate = plate;
	}

	/// <summary><c>dba\terradef.dba</c> frame 0, which <c>Mission_LoadPictures</c> (<c>00443f33</c>) puts in the Telecomm picture.</summary>
	public DynamixBitmap? Plate { get; }

	/// <summary><c>dba\miss_arw.dba</c> frame <paramref name="index"/>, or null.</summary>
	public DynamixBitmap? Arrow(int index) => _arrows is { } frames && index >= 0 && index < frames.Length ? frames[index] : null;

	public static ShellMissionArt Load(GameContent content) =>
		new(ShellArt.ReadBankFrames(content, "MISS_ARW"),
			ShellArt.ReadBankFrames(content, "TERRADEF") is { Length: > 0 } plate ? plate[0] : null);
}

/// <summary>
/// Tab 7, <c>MISSION</c>, in its briefing view: the <c>Telecomm</c> picture, the <c>Mission Map</c> with
/// its six map buttons, the <c>Mission Summary</c> text with its two page buttons, and the button bar.
/// Built once by <c>Mission_BuildScreen</c> (<c>00442534</c>), put up in the view the tab asks for by
/// <c>Mission_Show</c> (<c>004441e3</c>) and taken down by <c>Mission_Leave</c> (<c>00444a05</c>).
/// See docs/shell/screen-layout.md, "The mission screen".
///
/// <para><b>Every rect here is a literal in the executable</b>, kept parent-relative as the builder
/// writes them: the four panels in the canvas, everything else in the panel holding it.</para>
///
/// <para><b>Only the briefing view is ported, and only its text.</b> The map inside the
/// <c>Mission Map</c> panel is drawn by the shell's map object and is not here, so the panel's body
/// stays black and its six buttons fire nothing; the Telecomm movie is not played; and
/// <c>Rock &amp; Roll &gt;</c> launches nothing. The three text buttons and the page buttons work.</para>
/// </summary>
public sealed class ShellMissionScreen {
	/// <summary>The four panels, in the canvas, each parented to the top-level window.</summary>
	public static readonly ShellRect TelecommRect = new(7, 0x2b, 0x113, 0x12b);
	public static readonly ShellRect SummaryRect = new(7, 0x133, 0x278, 0x1a7);
	public static readonly ShellRect ButtonBarRect = new(7, 0x1b1, 0x278, 0x1d9);

	/// <summary>
	/// The map panel. The builder starts it on row <c>0x2b</c> in a window and on <c>0x2a</c> when the
	/// shell runs full-screen (<c>DAT_00481e68</c>); this engine's shell is a window.
	/// </summary>
	public static readonly ShellRect MapRect = new(0x117, 0x2b, 0x278, 0x12b);

	/// <summary>The Telecomm picture, in its panel.</summary>
	private static readonly ShellRect PlateRect = new(10, 0x14, 0xf9, 0x100);

	/// <summary>
	/// The arrows, in their panels: the map's six in a column from <c>x = 0x137</c>, each 31 pixels
	/// square, and the summary's two from <c>x = 0x247</c>. Each draws its first frame unlit and its
	/// second lit.
	/// </summary>
	private static readonly (int Top, int Unlit, int Lit)[] Arrows = {
		(0x18, 1, 0), (0x3e, 3, 2), (0x64, 10, 8), (0x8a, 11, 9), (0xb5, 7, 6), (0xdb, 5, 4),
		(0x18, 1, 0), (0x51, 3, 2),
	};

	private const int MapArrowLeft = 0x137;
	private const int PageArrowLeft = 0x247;
	private const int ArrowSize = 0x1e;

	/// <summary>The four buttons, in the button bar.</summary>
	private static readonly ShellRect[] ButtonRects = {
		new(0xe, 0x10, 0x96, 0x23), new(0x9d, 0x10, 0x125, 0x23), new(0x12d, 0x10, 0x1b5, 0x23),
		new(0x1ea, 0x10, 0x263, 0x23),
	};

	/// <summary>The briefing, objectives and intelligence text boxes, all at one rect in the summary panel.</summary>
	private static readonly ShellRect TextRect = new(10, 0x15, 0x23f, 0x73);

	private readonly ShellMissionArt? _art;
	private readonly ShellTextBox[] _boxes = { new(TextRect), new(TextRect), new(TextRect) };

	public ShellMissionScreen(ShellMissionArt? art = null) => _art = art;

	/// <summary>
	/// <c>DAT_004780a4</c>, which text is up: the briefing's, the objectives' or the intelligence
	/// report's. Each is also the button lit in <see cref="ViewLitColor"/>.
	/// </summary>
	public ShellMissionButton ShownText { get; private set; } = ShellMissionButton.Briefing;

	/// <summary>The text box <paramref name="text"/> fills.</summary>
	public ShellTextBox Box(ShellMissionButton text) => _boxes[(int)text];

	/// <summary>
	/// <c>Mission_Show(1)</c>: fills the three text boxes, which puts each back on its first page,
	/// lights <c>Mission Briefing</c> and shows the briefing.
	/// </summary>
	public void EnterBriefing(ShellMissionTexts texts, HudFont? font) {
		Box(ShellMissionButton.Briefing).SetText(texts.Briefing, font);
		Box(ShellMissionButton.Objectives).SetText(texts.Objectives, font);
		Box(ShellMissionButton.Intelligence).SetText(texts.Intelligence, font);
		ShowText(ShellMissionButton.Briefing);
	}

	/// <summary>
	/// A text button — <c>Mission_OnBriefing</c> (<c>004453c1</c>), <c>Mission_OnObjectives</c>
	/// (<c>00445437</c>) or <c>Mission_OnIntelligence</c> (<c>004454a0</c>) — through
	/// <c>Mission_LightViewButton</c> (<c>00444c49</c>) and <c>Mission_ShowTextBox</c> (<c>00444cb3</c>):
	/// the box that was up is hidden and this one shown on the page it was left on.
	/// </summary>
	public void ShowText(ShellMissionButton text) {
		if (text == ShellMissionButton.RockAndRoll) {
			return;
		}

		Box(ShownText).Visible = false;
		Box(text).Visible = true;
		ShownText = text;
	}

	/// <summary><c>Mission_OnPageUp</c> (<c>004455e9</c>) and <c>Mission_OnPageDown</c> (<c>0044569a</c>), on the box that is up.</summary>
	public bool Page(bool down) => down ? Box(ShownText).PageDown() : Box(ShownText).PageUp();

	/// <summary>One button's rect, in the canvas.</summary>
	public static ShellRect ButtonRect(ShellMissionButton button) => Inside(ButtonBarRect, ButtonRects[(int)button]);

	/// <summary>One arrow's rect, in the canvas.</summary>
	public static ShellRect ArrowRect(ShellMissionArrow arrow) {
		int index = (int)arrow;
		bool page = arrow >= ShellMissionArrow.PageUp;
		int left = page ? PageArrowLeft : MapArrowLeft;
		int top = Arrows[index].Top;
		return Inside(page ? SummaryRect : MapRect, new ShellRect(left, top, left + ArrowSize, top + ArrowSize));
	}

	/// <summary>
	/// What the pointer hits: a button or an arrow. The four panels are built disabled —
	/// <c>TitledPanel_Ctor</c> clears <c>+0x49</c> and the builder clears the button bar's — and the
	/// Telecomm picture's handler (<c>FUN_00444e28</c>) returns at once, so a click anywhere else is
	/// swallowed, which here is the same as hitting nothing.
	/// </summary>
	public ShellHit? HitAt(float canvasX, float canvasY) {
		foreach (var button in Enum.GetValues<ShellMissionButton>()) {
			var rect = ButtonRect(button);
			if (rect.Contains(canvasX, canvasY)) {
				return ShellHit.Button(new ShellWidget(ShellWidgetKind.MissionButton, (int)button), rect, canvasX, canvasY);
			}
		}

		foreach (var arrow in Enum.GetValues<ShellMissionArrow>()) {
			if (ArrowRect(arrow).Contains(canvasX, canvasY)) {
				return new ShellHit(new ShellWidget(ShellWidgetKind.MissionArrow, (int)arrow), ShellHandler.RepeatButtonIcon, 0);
			}
		}

		return null;
	}

	/// <summary>Draws the briefing view into <paramref name="surface"/>, with <paramref name="lit"/> drawn pressed. The caller clears it first.</summary>
	public void Paint(ShellSurface surface, ShellText? text, HudSpriteSheet? sprites, ShellWidget? lit = null) {
		var font = sprites?.Font(ShellArt.ScreenFont);

		// Telecomm: a flat header over a filled body, and the plate in it as a bare bitmap.
		PaintPanel(surface, TelecommRect, font, text?.Text(TelecommText), PanelFace, headerChrome: false, 0, 0);
		ShellChrome.PaintImagePanel(surface, Inside(TelecommRect, PlateRect), _art?.Plate, 0, 0, Border, border: false);

		// The map panel, titled Mission Map in this view, with the hatch and a plate the builder writes.
		PaintPanel(surface, MapRect, font, text?.Text(MapTitleText), MapFace, headerChrome: true, MapPlateFirst,
			MapPlateLast);

		PaintPanel(surface, SummaryRect, font, text?.Text(SummaryText), PanelFace, headerChrome: false, 0, 0);
		Box(ShownText).Paint(surface, SummaryRect, font);

		foreach (var arrow in Enum.GetValues<ShellMissionArrow>()) {
			var (_, unlit, litFrame) = Arrows[(int)arrow];
			bool pressed = lit is { Kind: ShellWidgetKind.MissionArrow } widget && widget.Index == (int)arrow;
			var rect = ArrowRect(arrow);
			var clip = surface.PushClip(rect);
			if (_art?.Arrow(pressed ? litFrame : unlit) is { } face) {
				surface.Blit(face, rect.X0, rect.Y0);
			}

			surface.PopClip(clip);
		}

		ShellChrome.PaintPanel(surface, ButtonBarRect, ButtonBorder, fill: true);
		foreach (var button in Enum.GetValues<ShellMissionButton>()) {
			PaintButton(surface, font, ButtonRect(button), text?.Text(FirstButtonText + (int)button + (button > 0 ? 1 : 0)),
				button == ShownText ? ViewLitColor : ButtonBorder);
		}
	}

	private static void PaintPanel(ShellSurface surface, ShellRect rect, HudFont? font, string? title, byte face,
			bool headerChrome, int plateFirst, int plateLast) {
		ShellChrome.PaintTitledPanel(surface, rect, Border, face, ShellChrome.InteriorColor, TitleHeight, headerChrome,
			plateFirst, plateLast, fill: true);
		ShellChrome.PaintText(surface, new ShellRect(rect.X0, rect.Y0, rect.X1, rect.Y0 + TitleHeight), font, title,
			ShellTextAlign.Center, ShellChrome.FontInkColor);
	}

	/// <summary>One button: its double-bordered box in <paramref name="border"/> and its caption, centred in <c>0x29</c>.</summary>
	private static void PaintButton(ShellSurface surface, HudFont? font, ShellRect rect, string? caption, byte border) {
		ShellChrome.PaintButton(surface, rect, border);
		ShellChrome.PaintText(surface, new ShellRect(rect.X0 + 1, rect.Y0, rect.X1, rect.Y1), font, caption,
			ShellTextAlign.Center, ShellChrome.FontInkColor);
	}

	private static ShellRect Inside(ShellRect parent, ShellRect child) =>
		new(parent.X0 + child.X0, parent.Y0 + child.Y0, parent.X0 + child.X1, parent.Y0 + child.Y1);

	private const byte Border = 0x27;
	private const int TitleHeight = 0x13;

	/// <summary><c>TitledPanel_Ctor</c>'s header face, which the builder leaves on Telecomm and the summary and writes over on the map panel.</summary>
	private const byte PanelFace = 0x24;
	private const byte MapFace = 0x25;
	private const int MapPlateFirst = 0x26;
	private const int MapPlateLast = 0x13b;

	private const byte ButtonBorder = 0x22;

	/// <summary>The border <c>Mission_LightViewButton</c> writes on the button of the text that is up.</summary>
	private const byte ViewLitColor = 0x20;

	/// <summary><c>estext.bin</c> indices the screen prints. The buttons are <c>0xb4</c> and <c>0xb6</c>-<c>0xb8</c>.</summary>
	private const int SummaryText = 0xaf;
	private const int TelecommText = 0xb0;
	private const int MapTitleText = 0xb3;
	private const int FirstButtonText = 0xb4;
}
