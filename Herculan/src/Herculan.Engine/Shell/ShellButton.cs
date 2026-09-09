namespace Herculan.Engine.Shell;

/// <summary>One frame of one sprite bank — what a widget constructor is handed for each of its states.</summary>
public readonly record struct ShellSprite(string Bank, int Frame);

/// <summary>
/// One of the shell's framed buttons: a rect, two sprites, and a caption centred on whichever sprite
/// is showing.
///
/// <para><b>Two, though the constructor takes three.</b> <c>ButtonIcon_Ctor</c> (<c>00409d14</c>)
/// stores three frame pointers at <c>+0x51</c>, <c>+0x55</c> and <c>+0x59</c>, and both of the
/// class's paints (<c>0040a05d</c> and its subclass's <c>0040a26d</c>) pick between the first two on
/// the lit flag and never read the third. So a button has an unlit and a lit face and nothing
/// else.</para>
///
/// <para>Each is a bank <i>and</i> a frame rather than an index into one bank, which the tab strip's
/// leftmost button needs: its two faces come from <c>dba\online.dba</c> while the eight tabs' come
/// from <see cref="ShellArt.ButtonBank"/>.</para>
///
/// <para>A button's art is blitted at its rect's top-left at the sprite's own size rather than
/// stretched to the rect — the same rule the cockpit's widgets follow, for the same reason: the rect
/// is the hit and layout box, the sprite is the art.</para>
///
/// <para><see cref="Selected"/> is the tab strip's latch — a tab handler writes <c>1</c> to the
/// widget's own lit flag at <c>+0x45</c> and repaints before building its screen, which is what
/// leaves the active tab drawn lit while the pointer is elsewhere. It is separate from the transient
/// press <see cref="ShellScreen"/> tracks, because a press ends on mouse-up and the latch does
/// not.</para>
/// </summary>
public sealed class ShellButton {
	public ShellButton(int id, ShellRect rect, ShellSprite unlit, ShellSprite lit,
			string? caption = null, string fontName = ShellArt.ButtonFont) {
		Id = id;
		Rect = rect;
		Unlit = unlit;
		Lit = lit;
		Caption = caption;
		FontName = fontName;
	}

	/// <summary>Caller-assigned identity — see <see cref="ShellScreen.MenuButtonId"/> for the frame's own ids.</summary>
	public int Id { get; }

	/// <summary>The button's box in canvas pixels, both corners inclusive.</summary>
	public ShellRect Rect { get; }

	/// <summary>Sprite drawn at rest.</summary>
	public ShellSprite Unlit { get; }

	/// <summary>Sprite drawn while pressed or latched.</summary>
	public ShellSprite Lit { get; }

	/// <summary>The button's text, or null for an icon button that carries none.</summary>
	public string? Caption { get; set; }

	/// <summary>Which font the caption is drawn in — in this format the font is the colour.</summary>
	public string FontName { get; }

	/// <summary>
	/// Whether the button takes clicks. It does not change what is drawn: this class has no disabled
	/// face, so a gated button in the original looks exactly like an idle one and only stops
	/// responding.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>Whether the button is latched down — the tab strip's "this is the screen you are on".</summary>
	public bool Selected { get; set; }

	/// <summary>Which face to draw, given whether the pointer is holding this button down.</summary>
	public ShellSprite Sprite(bool pressed) => pressed || Selected ? Lit : Unlit;
}
