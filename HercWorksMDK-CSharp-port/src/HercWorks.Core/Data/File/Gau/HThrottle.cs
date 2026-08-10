using System.Drawing;

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
/// Followed immediately by 4 (X,Y) points, all confirmed (structurally, across all 9 real files —
/// each point falls within its own file's Track bounds) to sit along the track's length:
/// <see cref="DetentPoints"/>[0] near the top (forward limit), [1] and [2] close together near the
/// middle (matches the manual's "Centered is stopped" — likely a small neutral zone rather than a
/// single point), and [3] near the bottom (reverse limit, or full stop for RAZOR, which the manual
/// says has forward-only throttle). The X value across the 4 points is not always identical — 6 of
/// 9 real files alternate between two closely-spaced X values rather than one constant — preserved
/// as-is rather than forced uniform, since the cause isn't understood (plausibly two slightly
/// different sprite-anchor conventions for different detent types).
/// </summary>
public class HThrottle : WidgetBase {
	public Point[] DetentPoints { get; set; } = new Point[4];
}
