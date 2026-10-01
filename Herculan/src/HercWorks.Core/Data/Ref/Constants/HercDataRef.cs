namespace HercWorks.Core.Data.Ref.Constants;

/// <summary>
/// A HERC's id, hardpoint count and name, the id and count held as raw two-byte fields.
/// <see cref="IntId"/> and <see cref="HardpointCount"/> decode them big-endian, unlike the
/// little-endian files the game ships; which file, if any, carries them in that order is not
/// established.
/// </summary>
public class HercDataRef {
	public byte[] IdBytes { get; set; } = new byte[2];
	public byte[] HardpointCountBytes { get; set; } = new byte[2];
	public byte[] NameStrId { get; set; } = new byte[2];

	public int IntId { get; set; }
	public int HardpointCount { get; set; }

	public string? Name { get; set; }

	public HercDataRef() { }

	public HercDataRef(byte[] id, byte[] hardpoints, string name) {
		IdBytes = id;
		HardpointCountBytes = hardpoints;

		IntId = (id[0] << 8) | id[1];
		HardpointCount = (hardpoints[0] << 8) | hardpoints[1];
		NameStrId = IdBytes;
		Name = name;
	}
}
