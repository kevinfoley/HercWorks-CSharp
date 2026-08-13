namespace HercWorks.Core.Data.File.Bnd;

/// <summary>
/// FILE - DBSIM\BND\APPINPUT.BND — offset 0 confirmed, rest unmapped.
///
/// Every .BND file shares a 9-byte envelope before this offset-0 byte — see the corrected framing
/// in <see cref="Cam"/>'s doc comment and docs/formats/bnd-notes.md. The Java author's "offset 0"
/// is absolute file offset 9 (the per-subsystem record's first byte, right after the envelope),
/// confirmed against the real retail APPINPUT.BND: file[9] = 0x54 = 84, matching exactly.
///
///   0 - UINT8 - ? - 84
///
/// Real file is 32 bytes total (9-byte envelope + 23-byte record); only this first byte has a
/// note from the original author, the remaining 22 bytes are completely unmapped.
/// Ported from org.hercworks.core.data.file.bnd.AppInput. Empty placeholder in the original —
/// not yet reverse-engineered beyond the byte note above.
/// </summary>
public class AppInput {
}
