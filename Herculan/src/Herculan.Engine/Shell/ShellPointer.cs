namespace Herculan.Engine.Shell;

/// <summary>
/// The two buttons VSHELL's mouse events tell apart — <c>Mouse_OnButtonState</c> (<c>00408b03</c>)'s
/// sub-codes 2 and 1 for the left going down and up, 4 and 3 for the right.
/// </summary>
public enum ShellMouseButton {
	Left,
	Right,
}

/// <summary>
/// Which event handler a widget's class runs. They disagree about which button acts, and on which
/// edge, so a widget's class decides how a click on it behaves
/// (docs/shell/screen-layout.md#which-widget-a-click-reaches).
/// </summary>
public enum ShellHandler {
	/// <summary>
	/// <c>Control_HandleEvent</c> (<c>004097da</c>) — panels, framed and titled panels, grids, and
	/// <c>Button</c> through <c>Button_HandleEvent</c> — and <c>HatchedDivider_HandleEvent</c>
	/// (<c>0040c3b5</c>), the crew rows' copy of it. Either button: a press lights the widget, a
	/// release while it is lit fires, and a leave puts it out.
	/// </summary>
	Control,

	/// <summary>
	/// <c>ButtonIcon_HandleEvent</c> (<c>00409df2</c>), the tab strip's class. The left button fires on
	/// the press; the right goes through <c>Control_HandleEvent</c> and fires on its release. A leave
	/// never puts it out.
	/// </summary>
	ButtonIcon,

	/// <summary>
	/// <c>ImagePanel_HandleEvent</c> (<c>0040b6da</c>), the crew portraits. Fires on any left release
	/// that reaches it, wherever the press was, and ignores the right button.
	/// </summary>
	ImagePanel,

	/// <summary>
	/// <c>EditField_HandleEvent</c> (<c>0040beaf</c>), the save list's rows. Fires on the left press
	/// and takes the pointer; the right button does nothing.
	/// </summary>
	EditField,
}

/// <summary>What a widget is, for telling one from another — the kind and which one of that kind.</summary>
public enum ShellWidgetKind {
	StripButton,
	SaveRow,
	SaveButton,
	RepairRow,
	RepairHotspot,
	RepairButton,
	SquadRow,
	CrewRow,
	CrewRowPortrait,
	CrewSquadPortrait,
	CrewClear,
	BuildChassisRow,
	BuildButton,
	WeaponsRow,
	WeaponsButton,
}

/// <summary>One widget. <see cref="Sub"/> is a second index where one kind needs two, as the repair lists' <c>(column, row)</c> do.</summary>
public readonly record struct ShellWidget(ShellWidgetKind Kind, int Index, int Sub = 0);

/// <summary>
/// What the pointer is over: the widget a mouse event is delivered to, its class, and which of its
/// <c>Text</c> children the point is on, or <c>-1</c> for none.
///
/// <para><b>The child is part of the hit.</b> The pump hit-tests down to the innermost widget and
/// sends a leave whenever that changes. A <c>Text</c> takes no mouse events, so its leave climbs to its
/// parent, which puts the parent out: moving from one of a row's text columns to the next cancels a
/// press on that row, just as leaving it would.</para>
/// </summary>
public readonly record struct ShellHit(ShellWidget Widget, ShellHandler Handler, int Leaf = -1) {
	/// <summary>
	/// Which of <paramref name="children"/>, each in <paramref name="widget"/>'s own coordinates, a
	/// canvas point is on, or <c>-1</c>. Siblings are hit newest first, so where two share an edge the
	/// one later in the list — built later — answers.
	/// </summary>
	public static int LeafAt(ShellRect widget, ReadOnlySpan<ShellRect> children, float canvasX, float canvasY) {
		for (int i = children.Length - 1; i >= 0; i--) {
			var child = children[i];
			if (new ShellRect(widget.X0 + child.X0, widget.Y0 + child.Y0, widget.X0 + child.X1,
					widget.Y0 + child.Y1).Contains(canvasX, canvasY)) {
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// A <c>Button</c> at <paramref name="rect"/>. Its caption is the <c>Text</c> child
	/// <c>Button_Ctor</c> builds at <c>{1, 0, w, h}</c>, so the button's own left column is the one part
	/// of it the caption does not cover.
	/// </summary>
	public static ShellHit Button(ShellWidget widget, ShellRect rect, float canvasX, float canvasY) =>
		new(widget, ShellHandler.Control,
			LeafAt(rect, [new ShellRect(1, 0, rect.Width - 1, rect.Height - 1)], canvasX, canvasY));

	/// <summary>
	/// A list row at <paramref name="rect"/> whose four text columns <c>ListRow_AddColumns</c>
	/// (<c>0040a310</c>) cut at <paramref name="cut1"/>, <paramref name="cut2"/> and
	/// <paramref name="cut3"/>: each column is the row's full height, the first starts at 2 and the last
	/// ends one inside the row's right edge, so only the two columns at the left and the right edge
	/// itself are the row's own.
	/// </summary>
	public static ShellHit ListRow(ShellWidget widget, ShellRect rect, int cut1, int cut2, int cut3,
			float canvasX, float canvasY) {
		int bottom = rect.Height - 1;
		return new(widget, ShellHandler.Control, LeafAt(rect, [
			new ShellRect(2, 0, cut1, bottom),
			new ShellRect(cut1, 0, cut2, bottom),
			new ShellRect(cut2, 0, cut3, bottom),
			new ShellRect(cut3, 0, rect.Width - 2, bottom),
		], canvasX, canvasY));
	}
}

/// <summary>
/// VSHELL's pointer: which widget the last move left under it, and the per-widget state its handlers
/// keep between a press and a release. Every mouse event is delivered to that one widget and its
/// class decides what happens (<see cref="ShellHandler"/>); nothing is handed on to a parent.
///
/// <para>The state is the original's: the pointer's target, its lock (<c>+0x1f</c> of the pointer
/// state at <c>DAT_005ddbd0</c>), the lit flag <c>+0x45</c> of whichever content widget a press lit,
/// and an edit field's focus flag <c>+0xa7</c>. The strip's lit flags live on its
/// <see cref="ShellButton"/>s, because they are also the tab latch.</para>
///
/// <para><c>Control_HandleEvent</c> and <c>ButtonIcon_HandleEvent</c> also ignore mouse events while a
/// movie is playing or the movie queue is running (<c>Avi_Playing</c>, <c>MovieQueue_Running</c>).
/// This engine plays no movies, so there is nothing here for that test to read.</para>
/// </summary>
public sealed class ShellPointer {
	private readonly ShellScreen _strip;
	private ShellHit? _underPointer;
	private ShellWidget? _lit;
	private ShellWidget? _focused;

	public ShellPointer(ShellScreen strip) => _strip = strip;

	/// <summary>The widget mouse events go to, or null.</summary>
	public ShellHit? Target { get; private set; }

	/// <summary>
	/// Whether an edit field has taken the pointer. While it has, a move changes nothing and every
	/// button event goes to that field.
	/// </summary>
	public bool Locked { get; private set; }

	/// <summary>The content widget a press has lit and a release would fire, or null.</summary>
	public ShellWidget? Lit => _lit;

	/// <summary>
	/// A move to wherever <paramref name="hit"/> is. When the hit differs from the target, the target is
	/// sent a leave and the new hit becomes the target, as <c>EventQueue_Pump</c> (<c>00469ba4</c>)
	/// does; none of these classes acts on the enter that follows. While the pointer is locked the pump
	/// skips the hit test and nothing changes, but the position is kept for when it is released.
	/// </summary>
	public void Move(ShellHit? hit) {
		_underPointer = hit;
		if (!Locked && hit != Target) {
			Leave();
			Target = hit;
		}
	}

	/// <summary>A button going down, delivered to the target. <paramref name="fire"/> runs the widget's handler.</summary>
	public void Press(ShellMouseButton button, Action<ShellWidget> fire) {
		if (Target is not { } target) {
			return;
		}

		switch (target.Handler) {
			case ShellHandler.Control:
				_lit = target.Widget;
				break;

			case ShellHandler.ButtonIcon when _strip.Button(target.Widget.Index) is { Enabled: true } strip:
				strip.Lit = true;
				strip.Repaint();
				if (button == ShellMouseButton.Left) {
					fire(target.Widget);
				}

				break;

			case ShellHandler.EditField when button == ShellMouseButton.Left:
				PressEditField(target.Widget, fire);
				break;
		}
	}

	/// <summary>A button coming up, delivered to the target.</summary>
	public void Release(ShellMouseButton button, Action<ShellWidget> fire) {
		if (Target is not { } target) {
			return;
		}

		switch (target.Handler) {
			case ShellHandler.Control when _lit == target.Widget:
				fire(target.Widget);
				_lit = null;
				break;

			// The left release puts the flag out without repainting, so a tab that has just latched
			// stays drawn lit. The right release fires first and then puts it out and repaints — after
			// the handler has latched the tab, so a tab picked with the right button ends up unlit.
			case ShellHandler.ButtonIcon when _strip.Button(target.Widget.Index) is { Enabled: true } strip:
				if (button == ShellMouseButton.Left) {
					strip.Lit = false;
				} else if (strip.Lit) {
					fire(target.Widget);
					strip.Lit = false;
					strip.Repaint();
				}

				break;

			case ShellHandler.ImagePanel when button == ShellMouseButton.Left:
				fire(target.Widget);
				break;
		}
	}

	/// <summary>
	/// The leave the target is sent. Only <see cref="ShellHandler.Control"/> acts on it; the strip's
	/// class tests <c>+0x5d</c>, which <c>ButtonIcon_Ctor</c> sets, before putting itself out.
	/// </summary>
	private void Leave() {
		if (Target is { Handler: ShellHandler.Control } target && _lit == target.Widget) {
			_lit = null;
		}
	}

	/// <summary>
	/// <c>EditField_HandleEvent</c>'s press. An unfocused field takes the focus and the pointer and
	/// fires. A focused one — the pointer is locked onto it, so every press comes here — gives both up,
	/// hit-tests again, and posts the press over, so it lands on whatever is under the pointer now.
	/// </summary>
	private void PressEditField(ShellWidget field, Action<ShellWidget> fire) {
		if (_focused != field) {
			_focused = field;
			Locked = true;
			fire(field);
			return;
		}

		_focused = null;
		Locked = false;
		Move(_underPointer);
		Press(ShellMouseButton.Left, fire);
	}
}
