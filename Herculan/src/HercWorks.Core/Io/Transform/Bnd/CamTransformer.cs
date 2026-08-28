using HercWorks.Core.Data.File.Bnd;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Bnd;

/// <summary>
/// Transforms byte[] data to and from CAM.BND (see <see cref="Cam"/> for the format writeup and
/// field-confidence notes). New: the Java source had a data-model doc comment with sample values
/// but no transformer at all — this implements and verifies that layout against the real retail
/// CAM.BND. Only matches this one specific file by name (see <see cref="TransformerRegistry"/>) —
/// every other .BND file has its own unrelated record shape, confirmed different from CAM's both
/// in length and in the Java author's own per-file notes for the handful of other .BND files that
/// have any.
/// </summary>
public class CamTransformer : ByteTransformer<Cam> {
	/// <summary>Header shared by every .BND file: [0]=0x02, [1-2]=payload length (LE), [3-4]=0x0000, [5-8]=build stamp.</summary>
	private const int EnvelopeLength = 9;

	public override Cam? Parse(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);
		JumpTo(EnvelopeLength);

		var cam = new Cam {
			RawBytes = inputArray,

			RecordTag = IndexByte(),
			Unknown1 = IndexByte(),
			Unknown2 = IndexByte(),
			Unknown3 = IndexByte(),
			Distance1 = IndexShortLE(),
			Distance2 = IndexShortLE(),
			Blank1 = IndexByte(),
			Unknown4 = IndexByte(),
			Unknown5 = IndexByte(),
			Blank2 = IndexByte(),
			Blank3 = IndexByte(),
			Unknown6 = IndexByte(),
			Unknown7 = IndexByte(),
			Blank4 = IndexByte(),
			Blank5 = IndexByte(),
			Unknown8 = IndexByte(),
			Unknown9 = IndexByte(),
			Unknown10 = IndexByte(),
			Value3 = IndexShortLE(),
			Value4 = IndexShortLE(),
			TrailingByte = IndexByte(),
		};

		return cam;
	}

	public override byte[]? Write(Cam cam) {
		using var outStream = new MemoryStream();

		void Emit(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);
		void WriteByte(byte b) => outStream.WriteByte(b);

		// The 9-byte envelope (type marker, payload length, reserved, build stamp) is preserved
		// verbatim from the source file rather than reconstructed — RawBytes always holds it.
		Emit(cam.RawBytes![..EnvelopeLength]);

		WriteByte(cam.RecordTag);
		WriteByte(cam.Unknown1);
		WriteByte(cam.Unknown2);
		WriteByte(cam.Unknown3);
		Emit(WriteShortLE(cam.Distance1));
		Emit(WriteShortLE(cam.Distance2));
		WriteByte(cam.Blank1);
		WriteByte(cam.Unknown4);
		WriteByte(cam.Unknown5);
		WriteByte(cam.Blank2);
		WriteByte(cam.Blank3);
		WriteByte(cam.Unknown6);
		WriteByte(cam.Unknown7);
		WriteByte(cam.Blank4);
		WriteByte(cam.Blank5);
		WriteByte(cam.Unknown8);
		WriteByte(cam.Unknown9);
		WriteByte(cam.Unknown10);
		Emit(WriteShortLE(cam.Value3));
		Emit(WriteShortLE(cam.Value4));
		WriteByte(cam.TrailingByte);

		return outStream.ToArray();
	}
}
