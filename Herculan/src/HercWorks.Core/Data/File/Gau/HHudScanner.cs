namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// Top-left of the front window's floating scanner repeater — content offset 1196, two more ints of
/// the "roving gunsight" block that <c>Gau_RovingGunsightWidget</c> (<c>0043c7d8</c>) reads straight
/// into the widget at <c>+0x10b</c>/<c>+0x10f</c>, right after the four rects
/// <see cref="HGunsightArea"/> covers.
///
/// Like <see cref="HReticle"/> this is a bare (X,Y) point rather than a corner rect —
/// <see cref="WidgetBase.Size"/> is unused. The repeater's extent is not in the file: its paint
/// (<c>FUN_0043f2b0</c>) squares off <c>0x2e</c> units from this point on both axes.
///
/// Position varies per herc — APOCA <c>40,27</c>, SAMSON <c>51,5</c>, OGRE <c>67,80</c>, RAZOR
/// <c>15,20</c> — in the file's own 320-wide space.
///
/// Read out of <see cref="GAUFile.Remainder"/> rather than carved out of it, exactly as
/// <see cref="HGunsightArea"/> is: surfaced here and still written back verbatim, so the byte-exact
/// round-trip is untouched.
/// </summary>
public class HHudScanner : WidgetBase {
}
