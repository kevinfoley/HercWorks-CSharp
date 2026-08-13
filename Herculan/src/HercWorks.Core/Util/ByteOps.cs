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
	/// FIXED — see KNOWN_ISSUES.md history: despite the name, this used to interpret the first two
	/// bytes big-endian (array[0] as the high byte), a direct literal port of what the Java
	/// original did. Genuinely unused anywhere in this codebase (verified before changing), so
	/// fixed to a real little-endian interpretation (array[0] as the low byte) with no risk of
	/// disturbing already-relied-upon behavior.
	/// </summary>
	public static int Bytes2LEToInt(byte[] data) {
		return (data[1] << 8) | data[0];
	}

	/// <summary>Original: Bytes.from(b).toChar() on a single byte — zero-extends to an int.</summary>
	public static int ByteLEToInt(byte b) {
		return b & 0xFF;
	}

	/// <summary>
	/// FIXED — see KNOWN_ISSUES.md history: despite the name, this used to write v in big-endian
	/// order into arr, a direct literal port of what the Java original did. The only caller
	/// (UiWeaponEntry.ToByte()) has no callers of its own anywhere in this codebase (verified
	/// before changing), so fixed to a real little-endian write with no risk of disturbing
	/// already-relied-upon behavior.
	/// </summary>
	public static void ShortLEToByteArr(byte[] arr, int index, short v) {
		arr[index] = (byte)(v & 0xFF);
		arr[index + 1] = (byte)((v >> 8) & 0xFF);
	}
}
