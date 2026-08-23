namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The front-window HUD's **heading tape** rect — the first sub-widget rect read by the "roving
/// gunsight" HUD-overlay complex's constructor (`Gau_RovingGunsightWidget`, `0043c7d8`, the same
/// function that later reads <see cref="HReticle"/>'s point at offset 1136), and passed to
/// `HudHeadingTape_Ctor` (`0043b57c`), which loads the bank named `"hudhtick"`.
///
/// The class name is kept for compatibility with the file layout this type is read through; it is
/// **not** the torso-twist indicator the Java doc comment's `"1104- INDICATOR\TORSO_TWIST"` guess
/// named. That indicator — the manual's Rotation Indicator — has no rect in the file at all: the
/// gunsight constructor derives it from this one with literals (`+15, -10`, 90x4). See
/// docs/formats/cockpit-hud.md's gunsight-complex section and
/// `Herculan.Engine.Content.RotationIndicator`.
///
/// Normal X1,Y1,X2,Y2 rect (read via the same <see cref="Io.Transform.Dbsim.GauFileTransformer.ReadRect{T}"/>
/// helper as most widgets in this file). Verified against all 9 real files: X1=100/X2=220 (width
/// 120, constant) are identical in every file, centering the widget exactly on the HUD's horizontal
/// center (160, half of <see cref="GAUFile.HudScreenSize"/>'s 320 width). Height (Y2-Y1) is exactly
/// 17px in every one of the 9 files, including RAZOR, despite RAZOR's Y-position itself being a clear
/// outlier (80 vs 14 for most hercs, consistent with RAZOR's already-documented divergent HUD layout
/// elsewhere in this file — see <see cref="HShieldDisplay"/>'s doc comment). APOCA is a smaller
/// outlier (Y1=23 instead of 14).
/// </summary>
public class HTorsoTwist : WidgetBase {
}
