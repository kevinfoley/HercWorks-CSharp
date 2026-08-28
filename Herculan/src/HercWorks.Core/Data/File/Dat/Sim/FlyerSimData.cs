namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - dat\[flyer].dat — a much smaller sibling of <see cref="HercSimDat"/>'s dat\[herc].dat,
/// for flyer-type sim units (no legs/torso/walk-animation-set fields at all). Reverse-engineered
/// from the only known real-world sample, SKIMMER.DAT (9-byte VOL file prefix + 46-byte payload +
/// 1 trailing byte = 56 bytes on disk). The payload is 9 little-endian shorts (18 bytes) followed
/// by a 28-byte null-padded ASCII name ("Landskimmer" in the sample).
///
/// The first 7 shorts line up byte-for-byte with HercSimDat's opening fields (SpeedTurn ..
/// AnimId_Walk) — DecelTurning matches both the position AND the exact value (150) seen on every
/// known Herc, CameraBoneId matches position with a small plausible bone id, and AnimId_Walk
/// matches position with the exact -1 "no walk animation" value RAZOR.DAT (the one Herc that's
/// also a flyer) uses. The pattern breaks after that — the next value (14000) is nothing like an
/// AnimId — so this is treated as its own distinct, shorter layout rather than a truncated
/// HercSimDat, and the two trailing shorts are left as Unk. Revisit all of this if a second flyer
/// .DAT sample turns up.
/// </summary>
public class FlyerSimData {
	public short SpeedTurn { get; set; }
	public short SpeedReverse { get; set; }
	public short SpeedForward { get; set; }
	public short SpeedAccelDecel { get; set; }

	/// <summary>150 in the one known sample — same constant HercSimDat.DecelTurning holds on every known Herc, at the same byte offset.</summary>
	public short DecelTurning { get; set; }

	/// <summary>4 in the one known sample — same byte offset as HercSimDat.CameraBoneId.</summary>
	public short CameraBoneId { get; set; }

	/// <summary>-1 in the one known sample — same byte offset and value as RAZOR.DAT's HercSimDat.AnimId_Walk.</summary>
	public short AnimId_Walk { get; set; }

	public short Unk14_val { get; set; }
	public short Unk16_val { get; set; }

	/// <summary>Payload offset 0x12-0x2D (18, 28 bytes) — null-padded ASCII name, e.g. "Landskimmer".</summary>
	public byte[]? NameBytes { get; set; }
}
