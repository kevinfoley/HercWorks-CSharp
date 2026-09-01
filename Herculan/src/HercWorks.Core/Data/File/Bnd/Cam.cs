
namespace HercWorks.Core.Data.File.Bnd;

/// <summary>
/// FILE - SIMVOL0\BND\CAM.BND — fully byte-mapped; no field's real-world meaning is confirmed.
///
/// <para>The per-subsystem record starts at absolute file offset 9, after the 9-byte envelope every
/// <c>.BND</c> file shares, and is <see cref="RecordTag"/> (1 byte) plus the envelope's stated
/// payload length (24 bytes here). The envelope's own layout, the field-by-field match against the
/// Java author's sample values, and the finding that <c>.BND</c> is a build-time-only source format
/// DBSIM.EXE never opens at runtime are in docs/formats/bnd-notes.md.</para>
///
/// Ported from org.hercworks.core.data.file.bnd.Cam.
/// </summary>
public class Cam {
	/// <summary>
	/// The file's own bytes, kept so <see cref="Io.Transform.Bnd.CamTransformer.Write"/> can copy
	/// the 9-byte .BND envelope (type marker, payload length, reserved, build stamp) back out
	/// verbatim instead of reconstructing it — the envelope is never decoded, only preserved.
	/// This is the one place a parsed model needs its source bytes, so it is declared here rather
	/// than inherited.
	/// </summary>
	public byte[]? RawBytes { get; set; }

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
	/// field in this record that doesn't match exactly; see docs/formats/bnd-notes.md.
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
	/// The file's final byte, not covered by the Java author's original notes. 31 (0x1f) in the
	/// real file.
	/// </summary>
	public byte TrailingByte { get; set; }
}
