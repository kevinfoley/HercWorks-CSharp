using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The HUD shield-status display — new, no Java equivalent (the Java doc comment's "shield
/// front/rear" guess named the concept but never located real bytes for it). Confirmed 2026-08-09
/// by hand-decoding real `.GAU` bytes against pixel coordinates measured from a real screenshot
/// (APOCA), then cross-checking against the actual cockpit console art in `(herc).HB0` for both
/// APOCA (bar-style meter) and MAVERICK (circular-gauge-style meter) — see
/// <see cref="Io.Transform.Dbsim.GauFileTransformer"/>.
///
/// Stored as 4 "slots" of 4 raw ints each, read in this order: Unused, Divider, Bounds, Fill. Each
/// slot has the shape (A, X, Y, B) where X/B are the widget's left/right screen edges, but A/Y are
/// its top/bottom edges in NO GUARANTEED ORDER — for some slots A is the smaller (top) value, for
/// others Y is. Rather than risk a lossy sort-on-read, the raw ints are kept verbatim in the *Raw
/// arrays (so <see cref="Io.Transform.Dbsim.GauFileTransformer.ObjectToBytes"/> can always
/// round-trip byte-exact), with sorted Origin/Size-style accessors provided as a derived,
/// read-only view for consumers that just want a normal rectangle.
///
///   Slot 0 (<see cref="Unused"/>) — meaning unconfirmed. Interpreted the same way as the other 3
///     slots, it always lands over empty cockpit-art background in both real HB0 renders checked
///     (APOCA and MAVERICK) — preserved raw rather than modeled as a real rect.
///   Slot 1 (<see cref="DividerOrigin"/>/<see cref="DividerSize"/>) — a thin rect marking the
///     boundary between the front and rear shield halves. Confirmed against real HB0 art: sits on
///     the horizontal midline of the meter graphic in both bar-style and gauge-style designs.
///   Slot 2 (mirrored into the inherited <see cref="WidgetBase.Origin"/>/<see
///     cref="WidgetBase.Size"/>) — the overall widget bounding box. Confirmed against real HB0
///     art: tightly bounds the entire meter graphic (bar-style meter body + its two text strips,
///     or the whole circular gauge for MAVERICK's design).
///   Slot 3 (<see cref="FillOrigin"/>/<see cref="FillSize"/>) — the actual shield-strength fill
///     graphic, the sub-region within Bounds that gets colored in to show front/rear shield
///     percentage. Confirmed against real HB0 art: tightly bounds the recessed/beveled meter body.
/// </summary>
public class HShieldDisplay : WidgetBase {
	public int[] Unused { get; set; } = new int[4];
	public int[] DividerRaw { get; set; } = new int[4];
	public int[] BoundsRaw { get; set; } = new int[4];
	public int[] FillRaw { get; set; } = new int[4];

	public PixelPoint DividerOrigin => SlotOrigin(DividerRaw);
	public PixelSize DividerSize => SlotSize(DividerRaw);
	public PixelPoint FillOrigin => SlotOrigin(FillRaw);
	public PixelSize FillSize => SlotSize(FillRaw);

	private static PixelPoint SlotOrigin(int[] raw) => new(raw[1], Math.Min(raw[0], raw[2]));

	private static PixelSize SlotSize(int[] raw) {
		int top = Math.Min(raw[0], raw[2]);
		int bottom = Math.Max(raw[0], raw[2]);
		return new PixelSize(raw[3] - raw[1], bottom - top);
	}

	public override string ToString() {
		string name = HWidgetId != null ? HWidgetId.Name : GetType().Name;
		return $"{name} [bounds=({Origin.X},{Origin.Y}), size=({Size.Width},{Size.Height}), " +
			$"fill=({FillOrigin.X},{FillOrigin.Y}), fillSize=({FillSize.Width},{FillSize.Height}), " +
			$"divider=({DividerOrigin.X},{DividerOrigin.Y})]";
	}
}
