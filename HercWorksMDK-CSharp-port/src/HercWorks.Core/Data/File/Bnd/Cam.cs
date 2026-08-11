using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Bnd;

/// <summary>
/// FILE - SIMVOL0\BND\CAM.BND — fully byte-mapped (2026-08-11), field meanings mostly unconfirmed.
///
/// Every real .BND file (83 total in ES2/VOL/simvol0/bnd/) shares a common 9-byte envelope before
/// the per-subsystem record: <c>[0]=0x02</c> (constant format marker), <c>[1-2]=uint16 LE</c>
/// (payload length, always exactly <c>fileSize-10</c>, verified against all 83 real files with no
/// exceptions), <c>[3-4]=0x0000</c> (reserved), <c>[5-8]</c> = a 4-byte value that clusters
/// per apparent build batch (see docs/formats/bnd-notes.md) — not consumed here. The actual
/// per-subsystem record starts at absolute file offset 9 and is <c>RecordTag</c> (1 byte) followed
/// by <c>payloadLength</c> more bytes (24 for CAM.BND).
///
/// Ported from org.hercworks.core.data.file.bnd.Cam, whose doc comment already listed sample
/// values for every field at this same byte alignment (confirmed by cross-checking every value
/// against the real retail CAM.BND, byte for byte — 21 of 22 numeric fields match exactly; the
/// remaining one, <see cref="Unknown7"/>, differs by author's-notes "50" vs retail "80", almost
/// certainly the Java author reading a hex digit string as decimal rather than a real data
/// difference). The Java notes did not account for the file's very last byte — exposed here as
/// <see cref="TrailingByte"/>.
///
/// No field's real-world meaning is confirmed. <see cref="Distance1"/>/<see cref="Distance2"/>/
/// <see cref="Value3"/>/<see cref="Value4"/> are the four UINT16 fields (2500, 30000, 500, 8000 in
/// the real file) — plausible camera clip/zoom distances given the file name, not independently
/// verified. Note: .BND is confirmed (2026-08-11, see docs/formats/bnd-notes.md) to be a
/// build-time-only source format — DBSIM.EXE never opens .bnd files at runtime; other .BND files'
/// values (ROCKET.BND, PWEAPONS.BND) were found baked directly into DBSIM.EXE as literal code
/// constants, so there's no runtime loader to check CAM.BND's fields against, only the compiled
/// code's own use of camera-related values if that's ever traced.
/// </summary>
public class Cam : DataFile {
	/// <summary>Per-subsystem record tag, absolute file offset 9. 54 (0x36) in the real CAM.BND.</summary>
	public byte RecordTag { get; set; }

	/// <summary>Unknown. 208 in the real file.</summary>
	public byte Unknown1 { get; set; }

	/// <summary>Unknown. 52 in the real file.</summary>
	public byte Unknown2 { get; set; }

	/// <summary>
	/// Unknown. 49 (0x31, ASCII '1') in the real file — CAM/MECH/MECHSYS all share this exact
	/// value at the same relative offset, plausibly a shared format sub-version, not confirmed.
	/// </summary>
	public byte Unknown3 { get; set; }

	/// <summary>Unknown UINT16. 2500 in the real file — plausibly a near/min camera distance.</summary>
	public short Distance1 { get; set; }

	/// <summary>Unknown UINT16. 30000 in the real file — plausibly a far/max camera distance.</summary>
	public short Distance2 { get; set; }

	/// <summary>Always 0 in the real file (Java notes: "blank").</summary>
	public byte Blank1 { get; set; }

	/// <summary>Unknown. 8 in the real file.</summary>
	public byte Unknown4 { get; set; }

	/// <summary>Unknown. 192 in the real file.</summary>
	public byte Unknown5 { get; set; }

	/// <summary>Always 0 in the real file (Java notes: "blank").</summary>
	public byte Blank2 { get; set; }

	/// <summary>Always 0 in the real file (Java notes: "blank").</summary>
	public byte Blank3 { get; set; }

	/// <summary>Unknown. 4 in the real file.</summary>
	public byte Unknown6 { get; set; }

	/// <summary>
	/// Unknown. 80 (0x50) in the real retail file — the Java author's notes say "50", the one
	/// field in this record that doesn't match exactly; see class doc comment.
	/// </summary>
	public byte Unknown7 { get; set; }

	/// <summary>Always 0 in the real file (Java notes: "blank").</summary>
	public byte Blank4 { get; set; }

	/// <summary>Always 0 in the real file (Java notes: "blank").</summary>
	public byte Blank5 { get; set; }

	/// <summary>Unknown. 48 in the real file.</summary>
	public byte Unknown8 { get; set; }

	/// <summary>Unknown. 38 in the real file — Java notes flag this as possibly paired with the next byte.</summary>
	public byte Unknown9 { get; set; }

	/// <summary>Unknown, possibly paired with <see cref="Unknown9"/>. 2 in the real file.</summary>
	public byte Unknown10 { get; set; }

	/// <summary>Unknown UINT16. 500 in the real file.</summary>
	public short Value3 { get; set; }

	/// <summary>Unknown UINT16. 8000 in the real file.</summary>
	public short Value4 { get; set; }

	/// <summary>
	/// The file's final byte, not covered by the Java author's original notes (their field list
	/// ends one byte early). 31 (0x1f) in the real file.
	/// </summary>
	public byte TrailingByte { get; set; }
}
