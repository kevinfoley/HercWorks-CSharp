using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Bnd;

/// <summary>
/// FILE - DBSIM\BND\MECH.BND — first 8 record bytes confirmed, rest (most of the file) unmapped.
///
/// Every .BND file shares a 9-byte envelope before the per-subsystem record — see the corrected
/// framing in <see cref="Bnd.Cam"/>'s doc comment and docs/formats/bnd-notes.md. The Java author's
/// "offset 0" below is absolute file offset 9 (the record's first byte, right after the envelope).
/// Real retail MECH.BND is 404 bytes (9-byte envelope + 395-byte record) — the Java notes below
/// only cover the first 16 record bytes, of which the first 8 match the real retail file exactly
/// (242, 164, 51, 49, 12, 0, 42, 0); bytes 8-15 do NOT match the real file (notes say all-zero,
/// retail has 48, 117, 0, 0, 100, 0, 100, 0) — almost certainly because the author's copy was an
/// earlier build with fewer defined mech types in what looks like a per-mech-type array starting
/// around record offset 8 (matching the long-standing "MECH_TYPE_DATA[]" string hint from
/// project_es2_exe_recon memory), not a transcription error like Cam's single mismatched byte.
///
/// 	0- UINT8 - ? - 242
///  1- UINT8 - ? - 164
///  2- UINT8 - ? - 51
///  3- UINT8 - ? - 49
///  4- UINT8 - ? - 12
///  5- UINT8 - ? - 0
///  6- UINT8 - ? - 42
///  7- UINT8 - ? - 0
///  8- UINT8 - ? - 0 (Java author's build; retail = 48 — see class doc comment)
///  9- UINT8 - ? - 0 (retail = 117)
///  10- UINT8 - ? - 0 (retail = 0)
///  11- UINT8 - ? - 0 (retail = 0)
///  12- UINT8 - ? - 0 (retail = 100)
///  13- UINT8 - ? - 0 (retail = 0)
///  14- UINT8 - ? - 0 (retail = 100)
///  15- UINT8 - ? - 0 (retail = 0)
///
/// Ported from org.hercworks.core.data.file.bnd.Mech. Empty placeholder in the original — not
/// yet reverse-engineered beyond the byte layout notes in the Java source.
/// </summary>
public class Mech : DataFile {
}
