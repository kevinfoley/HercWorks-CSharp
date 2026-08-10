namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The torso-twist deviation indicator — new, no Java equivalent implemented (the Java doc comment
/// named the exact offset, `"1104- INDICATOR\TORSO_TWIST"`, but never implemented or verified it;
/// an earlier session's exhaustive plain-corner-rect sliding-window search of the whole undecoded
/// remainder came up empty because it searched for a shape close to a user's rough visual estimate
/// — ~92x6 pixels — which turned out to be off enough from the real shape to fall outside the
/// search's tolerance).
///
/// Confirmed 2026-08-10 via DBSIM.EXE disassembly (Ghidra), not black-box byte search: this is the
/// first sub-widget rect read by the "roving gunsight" HUD-overlay complex's own constructor
/// (`FUN_0043c7d8` in a 2026-08-10 Ghidra project, the same function that later reads
/// <see cref="HReticle"/>'s point at offset 1136 as another one of its children) and is passed to a
/// sub-gadget constructor that loads a bitmap resource literally named `"hudhtick"` ("HUD
/// H[orizontal]-tick") — a tick-mark graphic, exactly what a torso-twist deviation gauge would use
/// to render its left/right tick scale.
///
/// Normal X1,Y1,X2,Y2 rect (read via the same <see cref="Io.Transform.Dbsim.GauFileTransformer.ReadRect{T}"/>
/// helper as most widgets in this file). Verified against all 9 real files: X1=100/X2=220 (width
/// 120, constant) are identical in every file, centering the widget exactly on the HUD's horizontal
/// center (160, half of <see cref="GAUFile.HudScreenSize"/>'s 320 width) — matching a user's visual
/// description of the indicator as horizontally centered. Height (Y2-Y1) is exactly 17px in every
/// one of the 9 files, including RAZOR, despite RAZOR's Y-position itself being a clear outlier (80
/// vs 14 for most hercs, consistent with RAZOR's already-documented divergent HUD layout elsewhere
/// in this file — see <see cref="HShieldDisplay"/>'s doc comment). APOCA is a smaller outlier (Y1=23
/// instead of 14). The near-top vertical position (Y≈14-23 for 8 of 9 hercs) matches a user's
/// description of the indicator sitting "near the top of the screen, just above the compass."
/// </summary>
public class HTorsoTwist : WidgetBase {
}
