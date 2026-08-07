using HercWorks.Core.Util;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform;

/// <summary>
/// Transformer abstract class for all ByteTransformers. Transformers are responsible for
/// converting between byte[] and types of DataFile — they essentially wrap a byte array with a
/// custom index cursor and a set of convenience operations for reading/writing chunks of it.
/// Ported from org.hercworks.core.io.transform.ThreeSpaceByteTransformer.
///
/// IMPORTANT — verified against the real at.favre.lib.bytes source before porting (see the notes
/// in HercWorks.Core.Util.ByteOps): `.byteOrder(...)` only changes how `.toShort()/.toInt()`
/// INTERPRET stored bytes; it does NOT reorder them, and `.array()` always ignores that tag. Only
/// `.reverse()` physically flips byte order. That distinction matters a lot here:
///   - indexShortLE/indexShort/indexIntLE (which call .toShort()/.toInt()) are genuinely correct
///     little/big-endian reads — the tag does what it says for those.
///   - indexSegmentLE, however, only calls `.byteOrder(LE).array()` — since .array() ignores the
///     tag, this is a NO-OP and returns bytes in the exact same order as indexSegment(). Despite
///     the name, it does NOT reverse anything. Ported literally (bug-compatible) below.
///   - writeIntLE/writeShortLE call `.reverse()` (not just `.byteOrder()`), so those ARE genuine,
///     correct little-endian writes.
/// </summary>
public abstract class ThreeSpaceByteTransformer {
	protected byte[]? Bytes;
	protected int Index;

	public abstract DataFile? BytesToObject(byte[]? inputArray);

	public abstract byte[]? ObjectToBytes(DataFile? source);

	protected void SetBytes(byte[] src) {
		Bytes = src;
	}

	protected byte IndexByte() {
		byte b = Bytes![Index];
		Index += 1;
		return b;
	}

	protected short IndexShortLE() {
		short s = EndianOps.ToShort(Bytes!, Index, ByteOrder.LittleEndian);
		Index += 2;
		return s;
	}

	protected short IndexShort() {
		short s = EndianOps.ToShort(Bytes!, Index, ByteOrder.BigEndian);
		Index += 2;
		return s;
	}

	protected int IndexIntLE() {
		int i = EndianOps.ToInt(Bytes!, Index, ByteOrder.LittleEndian);
		Index += 4;
		return i;
	}

	protected int IndexInt() {
		// Original had no explicit byteOrder() call, so it used the library's default: BIG_ENDIAN.
		int i = EndianOps.ToInt(Bytes!, Index, ByteOrder.BigEndian);
		Index += 4;
		return i;
	}

	protected byte[] IndexSegment(int len) {
		var arr = Slice(Bytes!, Index, len);
		Index += len;
		return arr;
	}

	/// <summary>
	/// NOTE: despite the name, this is byte-identical to IndexSegment(len) — see the class-level
	/// doc comment. Kept as a literal, bug-compatible port of the Java original.
	/// </summary>
	protected byte[] IndexSegmentLE(int len) {
		return IndexSegment(len);
	}

	protected string IndexString(int len) {
		byte[] segment = IndexSegment(len);
		var chars = new char[segment.Length];
		for (int i = 0; i < segment.Length; i++) {
			chars[i] = (char)segment[i];
		}
		return new string(chars);
	}

	protected short[] IndexShortLEArray(int len) {
		var arr = new short[len];
		for (int i = 0; i < len; i++) {
			arr[i] = IndexShortLE();
		}
		return arr;
	}

	protected int[] IndexIntLEArray(int len) {
		var arr = new int[len];
		for (int i = 0; i < len; i++) {
			arr[i] = IndexIntLE();
		}
		return arr;
	}

	protected byte[] WriteInt(int i) => EndianOps.GetIntBEBytes(i);

	protected byte[] WriteIntLE(int i) => EndianOps.GetIntLEBytes(i);

	protected byte[] WriteShort(short s) => EndianOps.GetShortBEBytes(s);

	protected byte[] WriteShortLE(short s) => EndianOps.GetShortLEBytes(s);

	protected byte[] WriteShortLESegment(short[] s) {
		var arr = new byte[s.Length * 2];

		int t = 0;
		for (int b = 0; b < s.Length; b++) {
			byte[] shortVal = WriteShortLE(s[b]);
			arr[t] = shortVal[0];
			arr[t + 1] = shortVal[1];
			t += 2;
		}

		return arr;
	}

	public void JumpTo(int jump) {
		if (jump < Bytes!.Length) {
			Index = jump;
		}
	}

	public void Skip(int skip) {
		if (Index + skip < Bytes!.Length) {
			Index += skip;
		}
	}

	/// <summary>
	/// NOTE: despite the name, this does not read/peek a byte value — it just returns a computed
	/// offset (index + at) without dereferencing Bytes. Kept as a literal port; looks like it may
	/// be unused/unfinished in the original.
	/// </summary>
	public int PeekAt(int at) => Index + at;

	public void ResetIndex() => Index = 0;

	public byte[] GetBytes() => Bytes!;

	private static byte[] Slice(byte[] data, int offset, int length) {
		var result = new byte[length];
		Array.Copy(data, offset, result, 0, length);
		return result;
	}
}
