namespace HercWorks.Core.Data.File.Bnd;

/// <summary>
/// FILE - SIMVOL0\BND\MECHVIEW.BND — not yet reverse-engineered; envelope framing corrected.
///
/// Every .BND file shares a 9-byte envelope before the per-subsystem record — see the corrected
/// framing in <see cref="Cam"/>'s doc comment and docs/formats/bnd-notes.md. The Java author's
/// "offset 0" below is absolute file offset 9 (confirmed elsewhere against Cam/Mech/MechSys/
/// AppInput; not independently re-verified for this specific file since the original notes give no
/// sample values to check against). Real retail MECHVIEW.BND is 48 bytes (9-byte envelope + 39-byte
/// record) — entirely unmapped beyond the envelope.
///
/// 	0- UINT8 - ?
///  1- UINT8 - ?
///
/// Ported from org.hercworks.core.data.file.bnd.MechView. Empty placeholder in the original.
/// </summary>
public class MechView {
}
