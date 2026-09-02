namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The Multi-Function Display screen bounding box — the console panel that shows Radar/Scanner by
/// default and switches between Status/FlashComm/Nav Map/Radar/Target Status/Missile Cam via the
/// F1-F6 keys (manual line 355, 450-451). Confirmed the same way as
/// <see cref="HShieldDisplay"/> and <see cref="HThrottle"/> — a user screenshot measurement matched
/// real `.GAU` bytes, then confirmed decisively by overlaying the candidate rect on real
/// `(herc).HB0` cockpit texture art: it lands exactly on the console's central screen bezel,
/// flanked by the F1-F4 button column.
///
/// Unlike <see cref="HShieldDisplay"/>, this one is a single plain rect (normal X1,Y1,X2,Y2 order,
/// read via the same <see cref="Io.Transform.Dbsim.GauFileTransformer.ReadRect{T}"/> helper as
/// every other simple widget) — no extra fields, since the surrounding bytes are confirmed
/// always-zero padding across all 9 real files rather than more MFD-specific data.
///
/// Also notable: this offset (952) and <see cref="HThrottle"/>'s offset (1016) both exactly match
/// offsets given in the original Java `GAUFile.java` doc comment (`"952- PANEL\MFD"`,
/// `"1016- SLIDER\THROTTLE\"`) — the Java author had already correctly identified both the concept
/// and the byte offset, just never implemented or verified it against real data. Worth checking
/// that doc comment first for any future `.GAU` work — it also names offset 1064
/// (`"SLIDER\THROTTLE\SLIDE_DIR"`, exactly where <see cref="GAUFile.Remainder"/> now starts),
/// 1088 (`"PANEL\NAVBAR"`), 1104 (`"INDICATOR\TORSO_TWIST"`), and 1136 (`"RETICLE"`) as further
/// unverified leads within the still-undecoded remainder.
/// </summary>
public class HMfdPanel : WidgetBase {
}
