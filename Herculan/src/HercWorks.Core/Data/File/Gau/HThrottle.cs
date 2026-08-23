using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The HUD throttle slider — new, no Java equivalent (the Java doc comment's "throttle sliders"
/// guess named the concept but never located real bytes for it). Confirmed 2026-08-09 using the
/// same method that cracked <see cref="HShieldDisplay"/>: a user-supplied pixel measurement from
/// real cockpit art, matched against real `.GAU` bytes, then confirmed decisively by overlaying the
/// candidate rect/points on real `(herc).HB0` cockpit texture art — they land exactly on the
/// physical slider-track graphic. Unlike the shield display, this one uses the file's normal
/// X1,Y1,X2,Y2 corner-rect convention with no field-order surprises, so the inherited
/// WidgetBase.Origin/Size (the track) is read via the same <see
/// cref="Io.Transform.Dbsim.GauFileTransformer.ReadRect{T}"/> helper as every other simple widget.
///
/// The 8 ints that follow are <b>two more rects</b>, not four loose points, as
/// <see cref="ForwardBar"/> and <see cref="ReverseBar"/> read them. `ThrottleGauge_Ctor`
/// (`00447b84`) and its slider child (`00447e24`) treat the whole block from offset 1000 as one
/// widget record and hand `[8..11]` and `[12..15]` to the vertical LED bar-graph constructor
/// (`00439344`) with ranges `+0x400` and `-0x400`: they are the forward and reverse throttle fill
/// bars either side of the track's centre. That supersedes the earlier reading of these ints as
/// four detent points, and explains both of the things that reading found odd — the middle two
/// "points" always sit close together because they are the bottom of the upper bar and the top of
/// the lower one, and the x value alternates between two values because those are each bar's left
/// and right edge.
///
/// Neither bar is ever drawn — not by DBSIM either. The slider keeps them as private fields, never
/// registers them with the widget tree, and uses their rects only to widen the region it
/// invalidates; the bar-graph draw routines have no callers in the image. They are a cut feature.
/// </summary>
public class HThrottle : WidgetBase {
	/// <summary>
	/// The 8 ints following the track rect, kept in the file's own order so the transformer can write
	/// them back verbatim. Read them through <see cref="ForwardBar"/> and <see cref="ReverseBar"/>.
	/// </summary>
	public PixelPoint[] DetentPoints { get; set; } = new PixelPoint[4];

	/// <summary>
	/// The forward fill bar, above the track's centre — <c>ThrottleGauge_Ctor</c>'s first LED bar,
	/// constructed with a range of <c>+0x400</c>.
	/// </summary>
	public (PixelPoint TopLeft, PixelPoint BottomRight) ForwardBar =>
		(DetentPoints[0], DetentPoints[1]);

	/// <summary>The reverse fill bar, below the centre — the second LED bar, range <c>-0x400</c>.</summary>
	public (PixelPoint TopLeft, PixelPoint BottomRight) ReverseBar =>
		(DetentPoints[2], DetentPoints[3]);

	/// <summary>
	/// The `SLIDE_DIR` int at file offset 1064, decoded out of
	/// <see cref="GAUFile.RemainderBeforeTorsoTwist"/> rather than parsed as its own field, so the
	/// remainder still round-trips byte-exact. 1 in all 9 real files, selecting the vertical slider
	/// the gauge constructor builds at <c>00447e24</c>; the 0 branch (<c>004483c0</c>, a fixed 12px
	/// knob spanning the track's full height) is never exercised by retail data.
	/// </summary>
	public int SlideMode { get; set; } = 1;

	/// <summary>
	/// The int at file offset 1072, likewise decoded out of the remainder. It is the x nudge the
	/// gauge applies to the small tick sprite it parks beside the track — <c>ThrottleGauge_Ctor</c>
	/// adds it, shifted by the video mode's x coordinate shift, to the knob's own left edge. Small and
	/// per-herc (-4 to +14), which is what a per-art alignment tweak looks like.
	/// </summary>
	public int TickOffsetX { get; set; }
}
