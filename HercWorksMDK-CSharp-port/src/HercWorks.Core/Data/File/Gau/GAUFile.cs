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
///   628 - <see cref="HShieldDisplay"/>, 64 bytes (4x 16-byte slots) — the shield-status display.
///     Cross-referenced against `Earthsiege 2 - On-Line Manual.pdf` (repo root, line 353: cockpit
///     console lists "shield display" alongside the already-decoded ChainButton/LinkButton/
///     EnergyMeter) and then confirmed 2026-08-09 against real pixel measurements from a user
///     screenshot (APOCA) and, decisively, by rendering real `(herc).HB0` cockpit texture art with
///     the candidate rects overlaid: they land exactly on the real meter graphic for both a
///     bar-style meter (APOCA) and a completely different circular-gauge-style meter (MAVERICK) —
///     see HShieldDisplay's own doc comment for the full slot breakdown. RAZOR is a confirmed
///     exception: the manual (line 400) says it has a unique altimeter here instead of a shield
///     display, so RAZOR's bytes at this offset still parse via the same struct shape (round-trips
///     fine) but don't mean "shield display" the way every other herc's do.
///   692 - <see cref="RemainderBeforeMfdPanel"/>, 260 bytes, NOT decoded. Confirmed all-zero across
///     every real file except a single leftover byte at offset 692 itself (duplicates
///     ShieldDisplay's last decoded int for reasons still unconfirmed — kept raw rather than
///     guessed). Parallels the confirmed 288-byte gap earlier in the file.
///   952 - <see cref="HMfdPanel"/>, 16 bytes (a single normal X1,Y1,X2,Y2 rect) — the Multi-Function
///     Display screen bounding box. The original Java doc comment already named this exact offset
///     (`"952- PANEL\MFD"`) but it was never implemented or verified against real data. Confirmed
///     2026-08-09 the same way as the shield display and throttle: a user screenshot measurement
///     matched real bytes, then confirmed decisively by overlaying the candidate rect on real
///     `(herc).HB0` cockpit texture art — it lands exactly on the console's central screen bezel.
///   968, 984, 1000 - confirmed always-zero (3 more "null widget" slots, matching the Java doc's
///     own "968/984- null/empty widget" notes, off by one slot — it only listed 2).
///   1016 - <see cref="HThrottle"/>, 48 bytes (a normal X1,Y1,X2,Y2 track rect + 4 detent points) —
///     the throttle slider. The original Java doc comment already named this exact offset too
///     (`"1016- SLIDER\THROTTLE\"`). Confirmed 2026-08-09 the same way as the shield display: a user
///     screenshot measurement matched real bytes, then confirmed decisively by overlaying the
///     candidate track/points on real `(herc).HB0` cockpit texture art — they land exactly on the
///     physical slider-track graphic. See HThrottle's own doc comment for the detent-point details.
///   1064 - <see cref="RemainderBeforeTorsoTwist"/>, 40 bytes, NOT fully decoded, but partially
///     understood via 2026-08-10 DBSIM.EXE disassembly (see <see cref="HTorsoTwist"/>'s doc comment
///     for the method). Offset 1064 (INT32) is <see cref="HThrottle"/>'s own "slide direction" mode
///     flag (confirmed by disassembly of the throttle's own constructor reading this exact offset;
///     matches the Java doc's `"1064- SLIDER\THROTTLE\SLIDE_DIR"` guess) — always 1 across all 9 real
///     files, so the alternate (0) code path was never exercised by retail data and isn't confirmed.
///     Offset 1068 is unread by that constructor (padding). Offset 1072 (INT32) is a small per-herc
///     offset value also read by the throttle constructor (-2 to -4 for most hercs, +14/+17 for
///     RAZOR/TOMAHAWK) and left unexplained. Offset 1076-1087 (3 INT32s) is confirmed all-zero across
///     all 9 real files. Offset 1088-1103 (a normal X1,Y1,X2,Y2 rect) is the bounding container of
///     the whole "roving gunsight" HUD-overlay complex that <see cref="HTorsoTwist"/> and
///     <see cref="HReticle"/> both turned out to be children of — X1/Y1/X2 are constant (0,0,320) in
///     every file (X2=320 is the full HUD screen width), only Y2 varies per herc (117-157) as the
///     complex's bottom edge. This offset is where the Java doc comment guessed `"1088- PANEL\NAVBAR"`
///     — that guess is now known to be wrong (it isn't a navbar/compass widget), though the doc's
///     *offset* itself wasn't a bad guess, just its label. NAVBAR itself remains unlocated.
///   1104 - <see cref="HTorsoTwist"/>, 16 bytes (a normal X1,Y1,X2,Y2 rect) — the torso-twist
///     deviation indicator. Matches the Java doc's `"1104- INDICATOR\TORSO_TWIST"` guess exactly.
///     See <see cref="HTorsoTwist"/>'s own doc comment for the full disassembly-based confirmation
///     (a prior session's black-box byte search had ruled out a plain-rect encoding here, but that
///     search's shape tolerance was too tight for the real widget's actual 120x17 size).
///   1120 - <see cref="RemainderBeforeReticle"/>, 16 bytes, NOT modeled as typed fields (though its
///     purpose is now understood from disassembly): two (X,Y) anchor points — offset 1120-1127 and
///     1132-1135 — for a target-speed text readout ("000 K/H" per a literal format string found next
///     to the code that positions it) that's part of the same gunsight complex as
///     <see cref="HTorsoTwist"/>. Not modeled as a typed field because there's no way to visually
///     confirm it the way the shield display/throttle/MFD panel/reticle/torso-twist all were (this
///     app has no HUD text/font renderer to overlay a candidate label position against, unlike a
///     plain rect that can be checked against `.HB0` cockpit art) — left as raw preserved bytes
///     rather than force-fitting an unverified model.
///   1136 - <see cref="HReticle"/>, 8 bytes (a single (X,Y) point, not a rect — unlike every other
///     widget in this file). The Java doc comment already named this exact offset (`"1136-
///     RETICLE"`). Confirmed 2026-08-09 the same way as the shield display/MFD panel/throttle: a
///     user description ("horizontally centered, a bit above vertical center") matched real bytes —
///     X is a constant 160 (exactly the screen's horizontal center) across all 9 files, Y is 95-115
///     for 8 of 9 (RAZOR is 146, consistent with its already-documented different HUD layout) — then
///     confirmed decisively by rendering the point over real `APOCA.HB0` cockpit art: it lands
///     exactly centered in the transparent viewport gap between the cockpit struts.
///   1144 onward - <see cref="Remainder"/>, 556 bytes. Structurally mapped via 2026-08-10 DBSIM.EXE
///     disassembly (not decoded into typed fields — see below for why), so this is no longer really
///     "undecoded," just not modeled:
///     - 1144-1211 (68 bytes): still genuinely unaccounted for — no widget constructor found that
///       reads this span.
///     - 1212-~1588: the file-data footprint of one widget constructor (`FUN_00448cc8` in the
///       2026-08-10 Ghidra project) tied to `"hddclip"`/`"pilots"`/`"static"` string resources — a
///       pilot-roster/crew-status HDD readout. Reads several more rects at offset 1228, 1260, 1276,
///       an array of 15 more 16-byte rects at 1292-1531, 3 more 16-byte rects at 1532-1579, and a
///       2-byte field at 1588. None of these are modeled as typed fields: unlike the shield
///       display/throttle/MFD panel/reticle/torso-twist, none of this data showed the kind of clean
///       "constant except position" signal needed to trust a specific label, and — more
///       fundamentally — this app has no HUD renderer to visually confirm a guess against, the way
///       every other widget in this file ultimately was confirmed (`.HB0` cockpit-art overlay or a
///       user screenshot measurement). Left as raw preserved bytes rather than force-fitting an
///       unverified model.
///     - ~1668-1683: another MFD-related object (`FUN_00435bf0`, sets up what looks like an
///       MFDRadar/MFDMissileView-family sub-mode, matching the class names found in DBSIM's
///       strings), read as a 16-byte rect at struct offset 1668 by the `.GAU` loader's *caller*
///       (`FUN_00431bf8`), not the loader itself — same "no renderer to confirm against" reasoning,
///       not modeled.
///     - The remaining ~10 bytes at the very end (~1690-1699) are still unaccounted for.
///     NAVBAR (the Java doc's `"1088- PANEL\NAVBAR"` guess, already known wrong for that specific
///     offset — see above) was searched for exhaustively this session and NOT found anywhere in this
///     file: two separate DBSIM string-table keyword sweeps (torso/twist/navbar/compass/reticle/hud/
///     panel/gadget/indicator, then heading/bearing/degree/altimeter/mach) found zero direct hits for
///     anything nav/compass/heading-related, and all 7 of this file's top-level widget-offset
///     constructors (468/548/616/728/1000/1088/1212 — covering the weapon-control-button panel,
///     energy-meter container, shield front/rear value labels, MFD radar/mode-switching logic,
///     throttle, the gunsight complex, and the pilots/HDD readout respectively) were traced and none
///     is navbar-shaped. Treat this as a confirmed negative result, not an unexplored gap — a future
///     session shouldn't re-run the same string/constructor search without a genuinely new angle
///     (e.g. checking whether a compass/heading bar is actually baked into the `.HB0` cockpit texture
///     art as static geometry rather than being a separate dynamic `.GAU` widget at all).
/// Ported from org.hercworks.core.data.file.gau.GAUFile; the byte layout above was confirmed
/// against real retail data 2026-08-08 (offset 0-627) — the original Java doc comment had the
/// right shape but was never verified against real files, and its offsets past 628 don't hold up.
/// <see cref="Io.Transform.Dbsim.GauFileTransformer.ObjectToBytes"/> round-trips byte-exact against
/// all 9 real herc `.GAU` files (2026-08-09) despite Remainder being undecoded, since it's captured
/// and written back verbatim rather than needing to be understood.
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
	public HShieldDisplay? ShieldDisplay { get; set; }

	/// <summary>Undecoded bytes from content offset 692 to 951 — see class doc comment.</summary>
	public byte[]? RemainderBeforeMfdPanel { get; set; }

	public HMfdPanel? MfdPanel { get; set; }

	public HThrottle? Throttle { get; set; }

	/// <summary>Undecoded bytes from content offset 1064 to 1103 — see class doc comment.</summary>
	public byte[]? RemainderBeforeTorsoTwist { get; set; }

	public HTorsoTwist? TorsoTwist { get; set; }

	/// <summary>Undecoded bytes from content offset 1120 to 1135 — see class doc comment.</summary>
	public byte[]? RemainderBeforeReticle { get; set; }

	public HReticle? Reticle { get; set; }

	/// <summary>Undecoded bytes from content offset 1144 to end of file — see class doc comment.</summary>
	public byte[]? Remainder { get; set; }
}
