using System.Drawing;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// FILE - /SIMVOL0/GAU/(herc).GAU — the GUI config file for HUDs. Every widget below is stored as
/// a 16-byte (4x INT32) rectangle — (X1,Y1,X2,Y2), i.e. top-left/bottom-right, not origin+size —
/// which <see cref="Io.Transform.Dbsim.GauFileTransformer"/> converts into each widget's inherited
/// WidgetBase.Origin/Size.
///   0 - Point (2x INT32) - HUD origin, always (0,0) in real data.
///   8 - Size (2x INT32) - HUD screen size, e.g. (320,400).
///   16 - INT32 - weapon list total (how many of the 10 slots below are actually in use).
///   20 - 10x 16-byte rects - weapon hardpoint button slots (5 left column + 5 right column in
///     real data). Slots beyond the weapon list total are filled with a confirmed sentinel rect —
///     the original Java doc comment's own noted "null weapon" bytes, decoded as INT32:
///     (100,140,155,146) — rather than zeros.
///   180 - confirmed always-zero padding, 288 bytes, up to offset 468 (matches the original doc's
///     "180-467(287) Null data, consistent across all herc.gau files" almost exactly — off by one
///     byte from the doc's own count, not worth chasing further).
///   468 - a rect, always (0,0,0,0) in every real file checked — likely a container/root panel
///     with no explicit box of its own; not modeled as a separate field since it carries no real
///     information every time.
///   484 - rect - Weapon Chain-select button.
///   500 - rect - Weapon Link button.
///   516 - rect - Auto-track toggle button.
///   532, 548 - confirmed always-zero (2 more "null widget" slots per the original doc).
///   564 - rect - Energy meter.
///   580, 596, 612 - confirmed always-zero (3 more "null widget" slots per the original doc).
///   628 onward - NOT decoded. The original Java doc comment guesses at further named widgets
///     (shield front/rear labels, an MFD panel, throttle sliders, a navbar, a reticle) starting
///     around here, but real byte offsets stopped lining up with those guesses past this point —
///     preserved as <see cref="Remainder"/> rather than force-fitting a guessed layout.
/// Ported from org.hercworks.core.data.file.gau.GAUFile; the byte layout above was confirmed
/// against real retail data 2026-08-08 (offset 0-627) — the original Java doc comment had the
/// right shape but was never verified against real files, and its offsets past 628 don't hold up.
/// </summary>
public class GAUFile : DataFile {
	public Point HudOrigin { get; set; }
	public Size HudScreenSize { get; set; }

	public int WeaponListTotal { get; set; }
	public HWeaponPanelItem[]? Weapons { get; set; }

	public HButtonBasic? ChainButton { get; set; }
	public HButtonBasic? LinkButton { get; set; }
	public HButtonBasic? AutoTrackButton { get; set; }
	public HMeter? EnergyMeter { get; set; }

	/// <summary>Undecoded bytes from content offset 628 to end of file — see class doc comment.</summary>
	public byte[]? Remainder { get; set; }
}
