using System.Drawing;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The aiming reticle's screen position — new, no Java equivalent implemented (the Java doc
/// comment named the exact offset, `"1136- RETICLE"`, but never implemented or verified it).
/// Confirmed 2026-08-09: unlike every other widget in this file, this one is stored as a single
/// (X,Y) point, not a 4-int corner rect — <see cref="WidgetBase.Size"/> is unused/always (0,0)
/// here. X is a constant 160 across all 9 real files (exactly the horizontal center of the file's
/// own 320-wide <see cref="GAUFile.HudScreenSize"/>, matching a user's description of the reticle
/// as "horizontally centered"), while Y varies 95-115 for 8 of 9 hercs (a bit above the screen's
/// vertical center of 120, also matching the user's description) — RAZOR is the usual exception
/// (146, below center), consistent with its already-documented unique HUD layout elsewhere in this
/// file. Confirmed decisively by rendering the candidate point over real `APOCA.HB0` cockpit art:
/// it lands exactly centered in the transparent viewport gap between the cockpit struts, where a
/// targeting reticle belongs.
/// </summary>
public class HReticle : WidgetBase {
}
