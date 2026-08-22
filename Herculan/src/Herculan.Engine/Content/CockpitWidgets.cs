using Herculan.Engine.Render;

namespace Herculan.Engine.Content;

/// <summary>Which family a clickable cockpit widget belongs to, and so which table its index means.</summary>
public enum CockpitWidgetKind {
	/// <summary>An index into <see cref="MfdLayout.Buttons"/>.</summary>
	MfdButton = 0,

	/// <summary>A <see cref="HddLayout.Widget"/>.</summary>
	HddWidget = 1,

	/// <summary>The console's throttle slider. One of a kind, so its index is always zero.</summary>
	Throttle = 2,
}

/// <summary>One clickable widget's identity — the pair a click reports back and an action handler switches on.</summary>
/// <param name="Kind">Which family, and so how to read <paramref name="Index"/>.</param>
/// <param name="Index">The widget's index within its own family's table.</param>
public readonly record struct CockpitWidgetId(CockpitWidgetKind Kind, int Index) {
	/// <summary>Multi-function display button <paramref name="index"/>.</summary>
	public static CockpitWidgetId Mfd(int index) => new(CockpitWidgetKind.MfdButton, index);

	/// <summary>Heads-Down Display widget <paramref name="widget"/>.</summary>
	public static CockpitWidgetId Hdd(HddLayout.Widget widget) => new(CockpitWidgetKind.HddWidget, (int)widget);

	/// <summary>The throttle slider.</summary>
	public static CockpitWidgetId Throttle { get; } = new(CockpitWidgetKind.Throttle, 0);

	/// <summary>This id as an <see cref="HddLayout.Widget"/>, or null when it is not one.</summary>
	public HddLayout.Widget? AsHddWidget =>
		Kind == CockpitWidgetKind.HddWidget ? (HddLayout.Widget)Index : null;

	/// <summary>This id as an MFD button index, or null when it is not one.</summary>
	public int? AsMfdButton => Kind == CockpitWidgetKind.MfdButton ? Index : null;
}

/// <summary>
/// One clickable widget as it stands this frame: which one it is, which surface it lives on, and the
/// rect it occupies in that surface's art pixels.
/// </summary>
/// <param name="Id">Which widget.</param>
/// <param name="Surface">Which cockpit surface its rect is measured on.</param>
/// <param name="X0">Left edge, art device pixels, inclusive.</param>
/// <param name="Y0">Top edge, inclusive.</param>
/// <param name="X1">Right edge, inclusive — the last pixel the widget covers, not one past it.</param>
/// <param name="Y1">Bottom edge, inclusive.</param>
/// <param name="Lit">
/// Whether the widget draws its lit frame this frame — <b>and which input decides that depends on the
/// button</b>. A latching button (<see cref="MfdLayout.IsLatching"/>,
/// <see cref="HddLayout.IsLatching"/>) is lit by being the chosen one and is unaffected by a press; a
/// momentary one is lit only while it is held. The original does this by having the two kinds read
/// different fields — the selection flag <c>+0x40</c> versus the shared press byte <c>+0x1b</c> — so
/// the F-key columns have no pressed state at all, while SELECT and the arrows have nothing else.
/// </param>
/// <param name="Selected">
/// Whether the widget is the persistently chosen one — the MFD's current screen, the Heads-Down
/// Display's current page. Only latching buttons are ever selected, and only they re-font their
/// caption; a momentary button holds its construction font however long it is held.
/// </param>
/// <param name="Draggable">
/// Whether pressing this widget captures the pointer — the original's per-widget <c>+0x1d</c> flag,
/// which <c>Widget_OnMouseDown</c> (<c>004527a0</c>) tests before latching the global drag capture
/// and dispatching the widget's own drag handler. Every button class leaves it clear; the slider
/// base (<c>004524a8</c>) sets it, which is the whole of what makes the throttle draggable.
/// </param>
public readonly record struct CockpitWidget(CockpitWidgetId Id, CockpitSurface Surface,
		int X0, int Y0, int X1, int Y1, bool Lit, bool Selected = false, bool Draggable = false) {

	/// <summary>Inclusive width in art pixels.</summary>
	public int Width => X1 - X0 + 1;

	/// <summary>Inclusive height in art pixels.</summary>
	public int Height => Y1 - Y0 + 1;

	/// <summary>
	/// Whether an art-space point falls on this widget — <c>Widget_HitTest</c>'s rectangular case
	/// (<c>00452388</c>), inclusive on all four edges as the original's is.
	///
	/// <para>The original's other case, a Manhattan-distance circular test selected by a per-widget
	/// flag, is not implemented: no widget in the MFD, the Heads-Down Display, the console buttons or
	/// the shield facings uses it. See docs/formats/cockpit-input.md §6.</para>
	/// </summary>
	public bool Contains(float artX, float artY) =>
		artX >= X0 && artY >= Y0 && artX <= X1 && artY <= Y1;
}

/// <summary>
/// Every clickable widget in the cockpit, in one flat list, positioned in art pixels.
///
/// <para>This is the shared definition the renderer draws from and the input layer hit-tests against,
/// so a button's click region cannot drift from the art it was drawn over. It pairs with
/// <see cref="CockpitScreenLayout"/>: that type gets a click from the window down to a surface's art
/// pixel, this one says which widget is under it.</para>
///
/// <para><b>Faithful to the original in the part that matters.</b> DBSIM keeps one flat, cockpit-wide
/// clickable list rather than a per-panel tree — every widget anywhere in the cockpit is appended to
/// the same array by <c>Widget_RegisterClickable</c> (<c>00452c44</c>), and
/// <c>Widget_HitTestChildren</c> (<c>00452a00</c>) linear-scans the whole thing. Nothing is removed
/// when a panel stops showing; a hidden widget is skipped because its state byte says so
/// (docs/formats/cockpit-input.md §5). What is reproduced here is that <i>rule</i> — hidden means not
/// hit — via the layouts' own visibility tables, which are the decoded form of the same state byte.
/// The flat array itself is not reproduced as a mutable registry: widgets are enumerated on demand
/// from the layout tables, since nothing in Herculan needs to register or unregister one at
/// runtime.</para>
///
/// <para><b>Clickable is not the same as drawable.</b> Every widget listed here is hit-testable,
/// including ones with no sprite of their own — the Heads-Down Display's title box and its dead slot
/// are in the original's clickable list too. Renderers keep their own "does this have a frame" check;
/// this type does not filter on it.</para>
/// </summary>
public static class CockpitWidgets {
	/// <summary>
	/// Every widget that is currently visible, and so currently clickable, for the given cockpit and
	/// HUD state. Widgets whose panel is not showing are omitted rather than reported hidden — the
	/// original's hit test skips them just as completely.
	/// </summary>
	public static IEnumerable<CockpitWidget> Visible(CockpitArt art, CockpitHudState state) {
		ArgumentNullException.ThrowIfNull(art);

		foreach (var widget in VisibleMfdButtons(art, state)) {
			yield return widget;
		}

		foreach (var widget in VisibleHddWidgets(art, state)) {
			yield return widget;
		}

		if (VisibleThrottle(art) is { } throttle) {
			yield return throttle;
		}
	}

	/// <summary>
	/// The throttle slider's track, or null when this herc's <c>.GAU</c> has no throttle block. It is
	/// the one cockpit control the pointer can drag, and it is always showing: the console it sits on
	/// has no modes.
	/// </summary>
	public static CockpitWidget? VisibleThrottle(CockpitArt art) {
		ArgumentNullException.ThrowIfNull(art);
		if (ThrottleTrack.From(art) is not { } track) {
			return null;
		}

		return new CockpitWidget(CockpitWidgetId.Throttle, CockpitSurface.Forward,
			track.Left, track.Top, track.Right, track.Bottom, Lit: false, Selected: false,
			Draggable: true);
	}

	/// <summary>
	/// The topmost visible widget under an art-space point on one surface, or null when the point is
	/// over bare art.
	///
	/// <para>Later entries win, which is the opposite of the original's first-hit-wins linear scan
	/// (<c>Widget_HitTestChildren</c>). The two agree for every retail cockpit because no two visible
	/// widget rects overlap — the MFD's buttons 7 and 10 share a rect, but they are the same physical
	/// button under two captions and no mode shows both. Later-wins is chosen anyway so that if an
	/// overlap ever does appear, the widget drawn on top is the one that takes the click.</para>
	/// </summary>
	public static CockpitWidget? HitTest(CockpitArt art, CockpitHudState state,
			CockpitSurface surface, float artX, float artY) {
		CockpitWidget? hit = null;
		foreach (var widget in Visible(art, state)) {
			if (widget.Surface == surface && widget.Contains(artX, artY)) {
				hit = widget;
			}
		}

		return hit;
	}

	/// <summary>
	/// The visible multi-function display buttons, in <c>MfdDisplay_Ctor</c>'s own index order.
	///
	/// <para>Rects come from <see cref="MfdLayout.Buttons"/>, which is authored in GAU units relative
	/// to the display's inset origin, so each is offset by that origin and scaled by
	/// <see cref="CockpitArt.GauToPixelScale"/> into the art's device pixels. The inclusive right and
	/// bottom edges land on the last device pixel the button's sprite covers: a GAU rect inclusive of
	/// <c>X1</c> covers <c>(X1 - X0 + 1)</c> GAU units, hence twice that many device pixels.</para>
	/// </summary>
	public static IEnumerable<CockpitWidget> VisibleMfdButtons(CockpitArt art, CockpitHudState state) {
		ArgumentNullException.ThrowIfNull(art);
		if (MfdLayout.InsetOrigin(art.Gau) is not { } inset) {
			yield break;
		}

		const int scale = (int)CockpitArt.GauToPixelScale;

		for (int i = 0; i < MfdLayout.ButtonCount; i++) {
			if (!MfdLayout.ButtonVisible(state.Mfd, i)) {
				continue;
			}

			var button = MfdLayout.Buttons[i];
			var id = CockpitWidgetId.Mfd(i);
			bool selected = i < MfdLayout.ModeCount && i == (int)state.Mfd;
			yield return new CockpitWidget(id, CockpitSurface.Forward,
				X0: (inset.X + button.X0) * scale,
				Y0: (inset.Y + button.Y0) * scale,
				X1: (inset.X + button.X1) * scale + scale - 1,
				Y1: (inset.Y + button.Y1) * scale + scale - 1,
				Lit: MfdLayout.IsLatching(i) ? selected : state.PressedWidget == id,
				Selected: selected);
		}
	}

	/// <summary>
	/// The visible Heads-Down Display widgets. Their rects are already device pixels relative to the
	/// <c>.HB1</c> art's top-left — the space <see cref="HddLayout"/> reports in — so they need no
	/// conversion, only the herc's own <c>.GAU</c> block to have loaded.
	/// </summary>
	public static IEnumerable<CockpitWidget> VisibleHddWidgets(CockpitArt art, CockpitHudState state) {
		ArgumentNullException.ThrowIfNull(art);
		if (art.HeadsDownLayout is not { } layout) {
			yield break;
		}

		var litWidget = state.Hdd == HddPage.CommandDisplay
			? HddLayout.Widget.PageButton0
			: HddLayout.Widget.PageButton1;

		for (int i = 0; i < HddLayout.WidgetCount; i++) {
			var widget = (HddLayout.Widget)i;
			if (!HddLayout.WidgetVisible(state.Hdd, widget)) {
				continue;
			}

			var rect = layout[widget];
			var id = CockpitWidgetId.Hdd(widget);
			bool selected = widget == litWidget;
			yield return new CockpitWidget(id, CockpitSurface.HeadsDown,
				rect.X0, rect.Y0, rect.X1, rect.Y1,
				Lit: HddLayout.IsLatching(widget) ? selected : state.PressedWidget == id,
				Selected: selected);
		}
	}
}
