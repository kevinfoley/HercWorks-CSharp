namespace HercWorks.Core.Util;

/// <summary>Mirrors java.nio.ByteOrder for the two orders actually used across the ported code.</summary>
public enum ByteOrder {
	BigEndian,
	LittleEndian
}

/// <summary>
/// Explicit endianness-aware primitive readers, replacing the Java code's
/// <c>Bytes.from(arr, off, len).byteOrder(order).toShort()</c> pattern. Unlike the <c>.array()</c>
/// case documented in HercWorks.Core.Util.ByteOps, byteOrder DOES affect these numeric conversions —
/// only <c>.array()</c> ignores the tag.
/// </summary>
public static class EndianOps {
	public static short ToShort(byte[] data, int offset, ByteOrder order) {
		return order == ByteOrder.BigEndian
			? (short)((data[offset] << 8) | (data[offset + 1] & 0xFF))
			: (short)((data[offset + 1] << 8) | (data[offset] & 0xFF));
	}

	public static int ToInt(byte[] data, int offset, ByteOrder order) {
		return order == ByteOrder.BigEndian
			? (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]
			: (data[offset + 3] << 24) | (data[offset + 2] << 16) | (data[offset + 1] << 8) | data[offset];
	}

	public static byte[] GetShortBEBytes(short s) => new byte[] { (byte)((s >> 8) & 0xFF), (byte)(s & 0xFF) };
	public static byte[] GetShortLEBytes(short s) => new byte[] { (byte)(s & 0xFF), (byte)((s >> 8) & 0xFF) };

	public static byte[] GetIntBEBytes(int i) => new byte[]
	{
		(byte)((i >> 24) & 0xFF), (byte)((i >> 16) & 0xFF), (byte)((i >> 8) & 0xFF), (byte)(i & 0xFF)
	};

	public static byte[] GetIntLEBytes(int i) => new byte[]
	{
		(byte)(i & 0xFF), (byte)((i >> 8) & 0xFF), (byte)((i >> 16) & 0xFF), (byte)((i >> 24) & 0xFF)
	};
}
