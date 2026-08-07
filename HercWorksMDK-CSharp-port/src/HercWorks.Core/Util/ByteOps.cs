namespace HercWorks.Core.Util;

/// <summary>
/// Ported from org.hercworks.core.util.ByteOps.
///
/// The Java original used at.favre.lib.bytes.Bytes with explicit .byteOrder(...) tags. Verified
/// against that library's actual source (not just its javadoc) before porting this file:
///   - Bytes.from(int/short/char) produces a big-endian byte representation (MSB first).
///   - .byteOrder(ByteOrder) only changes how later .toInt()/.toShort()/.toChar() calls
///     INTERPRET the stored bytes — it does NOT physically reorder the array. .array() always
///     returns the bytes exactly as stored.
///   - Only .reverse() physically flips the stored byte order.
/// That distinction changes the result of a naive re-reading of this code, so the methods below
/// were re-derived from that confirmed behavior rather than from the Java source's surface reading.
/// </summary>
public static class ByteOps {
	/// <summary>
	/// Original: Bytes.from(i).byteOrder(LE).array()[3].
	/// Since .array() ignores the byteOrder tag, index 3 of the big-endian 4-byte representation
	/// of i is simply its least-significant byte — equivalent to a plain narrowing (byte) cast.
	/// </summary>
	public static byte Int4ToByteLittleEndian(int i) {
		return unchecked((byte)i);
	}

	/// <summary>
	/// Original: Bytes.from( Bytes.from(i).byteOrder(LE).array()[3] )
	///              .append( Bytes.from(i).byteOrder(LE).array()[2] )
	///              .toChar()
	/// array()[3] is i's least-significant byte (bits 0-7); array()[2] is bits 8-15. Appending
	/// them in that order and reading as a big-endian char is a byte-swap of i's low 16 bits.
	/// </summary>
	public static char Int4ToCharLittleEndian(int i) {
		return (char)(((i & 0xFF) << 8) | ((i >> 8) & 0xFF));
	}

	/// <summary>
	/// Original: Bytes.from(b.array()[0], b.array()[1]).toChar() — i.e. reads the first two bytes
	/// of the given array and interprets them big-endian (array[0] as the high byte).
	///
	/// NOTE: despite the name, this does NOT do a little-endian interpretation — it's a direct,
	/// literal port of what the Java code does. If 'data' holds two raw little-endian bytes read
	/// from a file (low byte first), this will produce a byte-swapped value versus what you'd
	/// normally call "converting little-endian bytes to an int". This looks like it may have been
	/// a bug in the original; kept as-is for fidelity. Flag if real files show this misreading data.
	/// </summary>
	public static int Bytes2LEToInt(byte[] data) {
		return (data[0] << 8) | data[1];
	}

	/// <summary>Original: Bytes.from(b).toChar() on a single byte — zero-extends to an int.</summary>
	public static int ByteLEToInt(byte b) {
		return b & 0xFF;
	}

	/// <summary>
	/// Original: byte[] s = Bytes.from(v).byteOrder(LE).array(); arr[index]=s[0]; arr[index+1]=s[1];
	/// Same reasoning as Int4ToByteLittleEndian: .array() ignores the LE tag, so 's' is actually
	/// the big-endian 2-byte representation of v (MSB first) — despite the method's name, this
	/// writes v in BIG-ENDIAN order into arr. Kept as a literal port; flagged for the same reason
	/// as Bytes2LEToInt above.
	/// </summary>
	public static void ShortLEToByteArr(byte[] arr, int index, short v) {
		arr[index] = (byte)((v >> 8) & 0xFF);
		arr[index + 1] = (byte)(v & 0xFF);
	}
}
