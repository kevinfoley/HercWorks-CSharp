namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The front-window HUD's **ATT legend** — content offset 1164, the rect the "roving gunsight"
/// complex's constructor (`Gau_RovingGunsightWidget`, `0043c7d8`) gives the first of its two text
/// labels. While Automatic Turret Tracking is on, the complex's paint puts a plate and the word
/// `ATT` in it. Every retail file makes it 24x7 at the top left of the HUD (`68,0 - 92,7` on most
/// hercs, RAZOR `60,67 - 84,74`).
///
/// The 16 bytes after it, at 1180, are the second label's rect, which neither gunsight paint writes
/// to; it is not surfaced.
///
/// Read out of <see cref="GAUFile.Remainder"/> rather than carved out of it, exactly as
/// <see cref="HGunsightArea"/> is: surfaced here and still written back verbatim, so the byte-exact
/// round-trip is untouched. See docs/formats/cockpit-gunsight-hud.md.
/// </summary>
public class HAutoTrackLegend : WidgetBase {
}
