namespace HercWorks.Core.Data.Ref.Constants;

/// <summary>
/// Data convenience class to reference hercs quickly.
/// Ported from org.hercworks.core.data.ref.constants.HercDataRef.
///
/// NOTE: the Java original stored 2-byte fields as favre `Bytes` and called `.toInt()` on them
/// directly. That method is normally documented for 4-byte (int-sized) values; its exact behavior
/// on a 2-byte array wasn't verified against real data, so the big-endian interpretation used here
/// for IntId/HardpointCount is the most likely reading (consistent with this library's confirmed
/// default-BIG_ENDIAN behavior — see HercWorks.Core.Util.ByteOps) but unconfirmed against real files.
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
