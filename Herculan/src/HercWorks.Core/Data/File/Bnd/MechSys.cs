namespace HercWorks.Core.Data.File.Bnd;

/// <summary>
/// FILE - DBSIM\BND\MECHSYS.BND — offsets 0-4 and a repeating "value + 3 zero bytes" stride
/// confirmed against the real retail file (2026-08-11); semantics still unknown.
///
/// Every .BND file shares a 9-byte envelope before the per-subsystem record — see the corrected
/// framing in <see cref="Cam"/>'s doc comment and docs/formats/bnd-notes.md. The Java author's
/// "offset 0" below is absolute file offset 9. Real retail MECHSYS.BND is 48 bytes (9-byte
/// envelope + 39-byte record). Extending the Java author's partial/TODO notes against the real
/// file: after the initial 5 bytes (offsets 0-4), the record is a repeating 4-byte stride of
/// [UINT8 value][3 zero bytes] — offsets 4, 8, 12, 16, 20, 24, 28 hold 75, 60, 45, 25, 18, 12, 6, a
/// **monotonically decreasing sequence** (not evenly-spaced), strongly suggestive of a distance/LOD
/// tier or priority-falloff table. The stride breaks after offset 28: offset 32 is 0 (breaking the
/// expected next-in-sequence value), followed by a differently-shaped 4-byte trailer at offsets
/// 33-35 (4, 59, 1) before three final zero bytes — plausibly a count/terminator rather than one
/// more table entry, not confirmed.
///
/// 	0- UINT8 - ? - 241
///  1- UINT8 - ? - 184
///  2- UINT8 - ? - 35
///  3- UINT8 - ? - 49
///  4- UINT8 - ? - 75
///  [5-7 zero]
///  8- UINT8 - ? - 60
///  [9-11 zero]
///  12- UINT8 - ? - 45
///  [13-15 zero]
///  16- UINT8 - ? - 25
///  [17-19 zero]
///  20- UINT8 - ? - 18
///  [21-23 zero]
///  24- UINT8 - ? - 12 (new, beyond the Java author's "TODO - finish" cutoff)
///  [25-27 zero]
///  28- UINT8 - ? - 6
///  [29-31 zero]
///  32- UINT8 - ? - 0
///  33- UINT8 - ? - 4
///  34- UINT8 - ? - 59
///  35- UINT8 - ? - 1
///  [36-38 zero]
///
/// Ported from org.hercworks.core.data.file.bnd.MechSys. Empty placeholder in the original.
/// </summary>
public class MechSys {
}
