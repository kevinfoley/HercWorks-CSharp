using HercWorks.Core.Data.Struct;
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
///   580, 596 - confirmed always-zero "null widget" slots; 612-615 is 4 more confirmed-zero bytes.
///   616 - <see cref="HShieldDisplay"/>, 80 bytes — the ShieldsGauge block, per DBSIM's own
///     ShieldsGauge_Ctor (004434fc), which Gau_ShieldDisplayWidget (00432454) calls with this
///     offset. A 16-byte header whose first two ints are an origin offset added to the rest
///     (all-zero in every retail file), then four ordinary X1,Y1,X2,Y2 rects at 632/648/664/680: the
///     two shield facings' meter bodies and their two numeric-readout rects. Block ends at 696.
///     RAZOR is a confirmed exception — the manual (line 400) gives it a unique altimeter here — so
///     its bytes parse through the same struct but do not mean the same thing.
///   696 - <see cref="RemainderBeforeMfdPanel"/>, 256 bytes, NOT decoded. Confirmed all-zero across
///     every real file. Parallels the 288-byte gap earlier in the file.
///   952 - <see cref="HMfdPanel"/>, 16 bytes (a single normal X1,Y1,X2,Y2 rect) — the Multi-Function
///     Display screen bounding box, on the console's central screen bezel. Matches the Java doc
///     comment's `"952- PANEL\MFD"`, which was never verified there.
///   968, 984 - confirmed always-zero ("null widget" slots).
///   1000 - the throttle gauge's record, which `ThrottleGauge_Ctor` (`00447b84`) is handed whole.
///     Ints `[0]`/`[1]` at 1000/1004 are the origin the rest is measured from — zero in all 9 retail
///     files, which is why they also read as a null widget slot.
///   1016 - <see cref="HThrottle"/>, 48 bytes: the slider **track** rect at 1016, then the forward
///     fill bar at 1032 and the reverse fill bar at 1048 — three X1,Y1,X2,Y2 rects, not a track plus
///     four detent points. Matches the Java doc comment's `"1016- SLIDER\THROTTLE\"`. See
///     <see cref="HThrottle"/> and docs/formats/cockpit-hud.md, "Throttle gauge".
///   1064 - <see cref="RemainderBeforeTorsoTwist"/>, 40 bytes, NOT modeled as typed fields but
///     understood (see <see cref="HTorsoTwist"/> for the disassembly method):
///     - 1064 (INT32): <see cref="HThrottle.SlideMode"/>, the throttle's `SLIDE_DIR` flag. Always 1
///       across all 9 real files, so the 0 branch is never exercised by retail data.
///     - 1068: unread by that constructor (padding).
///     - 1072 (INT32): <see cref="HThrottle.TickOffsetX"/>, a small per-herc x nudge for the tick
///       sprite beside the track. Measured across all 9 retail files: -2 (APOCA, OGRE), -3
///       (COLOSSUS, MAVERICK, OUTLAW, RAPTOR2), -4 (SAMSON), +14 (TOMAHAWK), +17 (RAZOR).
///     - 1076-1087 (3 INT32s): confirmed all-zero across all 9 real files.
///     - 1088-1103 (X1,Y1,X2,Y2 rect): the bounding container of the whole "roving gunsight"
///       HUD-overlay complex that <see cref="HTorsoTwist"/> and <see cref="HReticle"/> are both
///       children of. X1/Y1/X2 are constant (0,0,320) in every file (320 is the full HUD screen
///       width); only Y2 varies per herc (117-157) as the complex's bottom edge. The Java doc
///       comment's `"1088- PANEL\NAVBAR"` had the offset right and the label wrong: this is not a
///       navbar/compass widget.
///   1104 - <see cref="HTorsoTwist"/>, 16 bytes (a normal X1,Y1,X2,Y2 rect) — the HUD **heading
///     tape**. Neither the Java doc comment's `"1104- INDICATOR\TORSO_TWIST"` nor the field name
///     kept here for compatibility describes what it is. The Rotation Indicator has no rect in the
///     file: the gunsight constructor derives it from this one. See <see cref="HTorsoTwist"/>.
///   1120 - <see cref="RemainderBeforeReticle"/>, 16 bytes: two (X,Y) anchor points at 1120-1127 and
///     1132-1135 for a target-speed text readout ("000 K/H" per the literal format string beside the
///     code that positions it), part of the same gunsight complex as <see cref="HTorsoTwist"/>. Left
///     as raw bytes rather than typed fields — see the note at the end.
///   1136 - <see cref="HReticle"/>, 8 bytes (a single (X,Y) point, not a rect — unlike every other
///     widget in this file), matching the Java doc comment's `"1136- RETICLE"`. X is a constant 160
///     (the screen's horizontal centre) across all 9 files; Y is 95-115 for 8 of 9, RAZOR 146,
///     consistent with its different HUD layout. Lands centred in the transparent viewport gap
///     between the cockpit struts.
///   1144 onward - <see cref="Remainder"/>, 556 bytes. Structurally mapped, not modeled:
///     - 1144: the half-extent of the gunsight complex's waypoint child, taken about the
///       <see cref="HReticle"/> point.
///     - 1148-1163: a rect three of the gunsight complex's children share — <see cref="GunsightArea"/>.
///     - 1164-1195 and 1204-1211 (40 bytes): unaccounted for — no widget constructor reads this span.
///     - 1196-1203: the floating scanner repeater's top-left — <see cref="HudScanner"/>.
///     - 1212-~1588: the file-data footprint of one widget constructor (`FUN_00448cc8`) tied to the
///       `"hddclip"`/`"pilots"`/`"static"` string resources — the Heads-Down Display. Reads rects at
///       1228, 1260 and 1276, an array of 15 more 16-byte rects at 1292-1531, 3 more at 1532-1579,
///       and a 2-byte field at 1588. See docs/formats/heads-down-display.md, "`.GAU` block at 1212".
///     - ~1668-1683: another MFD-related object (`FUN_00435bf0`, an MFDRadar/MFDMissileView-family
///       sub-mode per DBSIM's own class-name strings), read as a 16-byte rect at struct offset 1668
///       by the `.GAU` loader's *caller* (`FUN_00431bf8`), not the loader itself.
///     - The remaining ~10 bytes at the very end (~1690-1699) are unaccounted for.
/// NAVBAR is a **confirmed negative**, not an unexplored gap: it is nowhere in this file. Two DBSIM
/// string-table keyword sweeps (torso/twist/navbar/compass/reticle/hud/panel/gadget/indicator, then
/// heading/bearing/degree/altimeter/mach) found no nav/compass/heading hits, and all 7 top-level
/// widget-offset constructors — 468 (weapon-control button panel), 548 (energy-meter container), 616
/// (shield value labels), 728 (MFD radar/mode switching), 1000 (throttle), 1088 (gunsight complex),
/// 1212 (Heads-Down Display) — were traced and none is navbar-shaped. Re-running the same
/// string/constructor search adds nothing; a new angle would be checking whether a compass/heading
/// bar is baked into the `.HB0` cockpit art as static geometry instead of being a `.GAU` widget.
/// <para>Spans left as raw bytes are left that way deliberately: none showed the "constant except
/// position" signal needed to trust a specific label, and this app has no HUD renderer to check a
/// candidate against the way every modeled widget here was checked (`.HB0` cockpit-art overlay or a
/// screenshot measurement).</para>
/// Ported from org.hercworks.core.data.file.gau.GAUFile; the Java doc comment had the right shape
/// but was never verified against real files, and its offsets past 628 do not hold up.
/// <see cref="Io.Transform.Dbsim.GauFileTransformer.ObjectToBytes"/> round-trips byte-exact against
/// all 9 real herc `.GAU` files despite <see cref="Remainder"/> being undecoded, since it is
/// captured and written back verbatim rather than needing to be understood.
/// </summary>
public class GAUFile {
	public PixelPoint HudOrigin { get; set; }
	public PixelSize HudScreenSize { get; set; }

	public int WeaponListTotal { get; set; }
	public HWeaponPanelItem[]? Weapons { get; set; }

	public HButtonBasic? ChainButton { get; set; }
	public HButtonBasic? LinkButton { get; set; }
	public HButtonBasic? AutoTrackButton { get; set; }
	public HMeter? EnergyMeter { get; set; }
	public HShieldDisplay? ShieldDisplay { get; set; }

	/// <summary>Undecoded bytes from content offset 696 to 951 — see class doc comment.</summary>
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

	/// <summary>
	/// The gunsight complex's target-indicator area at content offset 1148 - see
	/// <see cref="HGunsightArea"/>. Surfaced from <see cref="Remainder"/>, which still carries the
	/// same bytes and is what the write path emits.
	/// </summary>
	public HGunsightArea? GunsightArea { get; set; }

	/// <summary>
	/// The floating scanner repeater's top-left at content offset 1196 - see
	/// <see cref="HHudScanner"/>. Surfaced from <see cref="Remainder"/> the same way
	/// <see cref="GunsightArea"/> is.
	/// </summary>
	public HHudScanner? HudScanner { get; set; }
}
