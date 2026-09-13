namespace Herculan.Engine.Content;

/// <summary>
/// What DBSIM's modal alert panels have in common: the screen they centre on, the rect type their
/// widgets are laid out in, and the transform that puts one in this engine's window.
///
/// <para>The family is four panel classes built on one base (<c>AlertPanel_CtorBase</c>,
/// <c>00454174</c>) and one modal loop. This engine draws two of them —
/// <see cref="ObjectivesPanelLayout"/> ([F11]) and <see cref="StatusAlertPanelLayout"/> ([Q] and the
/// mission-end alerts) — and they share their button art, three of their fonts and all of the
/// geometry below. See docs/simulation/mission-objectives.md.</para>
/// </summary>
public static class AlertPanelLayout {
	/// <summary>The screen a panel centres itself on — <c>AlertPanel_CenterRect</c>'s two globals.</summary>
	public const int ScreenWidth = 640;

	/// <inheritdoc cref="ScreenWidth"/>
	public const int ScreenHeight = 480;

	/// <summary>
	/// The bank every alert panel's buttons come from, shared by all four. Frame 0 is the button at
	/// rest and frame 1 the same box in a different border colour, held; both are 124x22 and are
	/// blitted at the widget rect's origin, so they overhang whatever rect the panel gives them.
	/// </summary>
	public const string ButtonBank = "ALERT";

	/// <summary>A panel title's font, <c>DAT_004d1e9c</c>.</summary>
	public const string TitleFont = "TITLE";

	/// <summary>
	/// A panel button's caption font at rest, <c>DAT_004d1ea4</c>. Every panel constructs its button
	/// captions in <c>CPYLW</c> and none is ever drawn in it: <c>PanelButton_Paint</c>
	/// (<c>00454ff8</c>) overwrites the label's font from the button's own four-entry state table
	/// before each draw.
	/// </summary>
	public const string ButtonFont = "ACTIVE";

	/// <summary>And while it is held, <c>DAT_004d1ea8</c>.</summary>
	public const string ButtonPressedFont = "PUSHED";

	/// <summary>Frame of <see cref="ButtonBank"/> for a button in the given state.</summary>
	public static int ButtonFrame(bool pressed) => pressed ? 1 : 0;

	/// <summary>Caption font for a button in the given state.</summary>
	public static string CaptionFont(bool pressed) => pressed ? ButtonPressedFont : ButtonFont;

	/// <summary>A rect in panel-local device pixels, inclusive of its low edges.</summary>
	public readonly record struct Rect(int X0, int Y0, int X1, int Y1) {
		/// <summary>Whether a point is inside — <c>Widget_HitTest</c>'s own rect test.</summary>
		public bool Contains(float x, float y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;
	}

	/// <summary>
	/// Where a panel of a given size lands in the window, and how to get back from a window pixel to
	/// a panel one.
	///
	/// <para>The original blits at a fixed screen origin; this engine's window is any size, so the
	/// panel is scaled by the same art-pixels-to-window factor the cockpit uses (fit the 480-row
	/// screen to the window's height) and centred horizontally, which is how the three-panel cockpit
	/// composite is anchored too.</para>
	/// </summary>
	public readonly record struct Placement(float Scale, float OriginX, float OriginY) {
		/// <summary>Places a panel of <paramref name="panelWidth"/> x <paramref name="panelHeight"/> device pixels.</summary>
		public static Placement Create(int windowWidth, int windowHeight, int panelWidth, int panelHeight) {
			float scale = Math.Max(windowHeight, 1) / (float)ScreenHeight;
			return new Placement(scale,
				(windowWidth - ScreenWidth * scale) / 2f + ScreenLeft(panelWidth) * scale,
				ScreenTop(panelHeight) * scale);
		}

		/// <summary>The window pixel a panel-local pixel lands on.</summary>
		public (float X, float Y) ToWindow(float panelX, float panelY) =>
			(OriginX + panelX * Scale, OriginY + panelY * Scale);

		/// <summary>The panel-local pixel under a window pixel. Not clamped.</summary>
		public (float X, float Y) ToPanel(float windowX, float windowY) =>
			Scale <= 0f
				? (float.NaN, float.NaN)
				: ((windowX - OriginX) / Scale, (windowY - OriginY) / Scale);
	}

	/// <summary>A panel's left edge on the screen — <c>(ScreenWidth - width) / 2</c>.</summary>
	public static int ScreenLeft(int panelWidth) => (ScreenWidth - panelWidth) / 2;

	/// <summary>A panel's top edge on the screen.</summary>
	public static int ScreenTop(int panelHeight) => (ScreenHeight - panelHeight) / 2;
}
