using HercWorks.Core.Data.File.Dyn;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Transforms byte[] data to and from DynamixBitmap game files.
/// Ported from org.hercworks.core.io.transform.common.DynamixBitmapTransformer.
/// </summary>
public class DynamixBitmapTransformer : ByteTransformer<DynamixBitmap> {
	public override DynamixBitmap? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO (carried over from Java): log null
			return null;
		}
		SetBytes(inputArray);

		Skip(4); // magic header — the write path emits DynamixBitmap.HeaderMagic, so it isn't retained.
		Skip(4); // on-disk size — the write path recomputes it from ImageData, so it isn't retained.

		var dbm = new DynamixBitmap {
			Rows = IndexShortLE(),
			Cols = IndexShortLE(),
			BitDepth = IndexShortLE(),
			UnkSpacer1 = IndexByte(),
			ImageDataLen = IndexIntLE()
		};
		dbm.UnkSpacer2 = IndexShortLE();
		dbm.ImageData = IndexSegment(dbm.ImageDataLen);

		return dbm;
	}

	public override byte[]? Write(DynamixBitmap dbm) {
		using var objectBytes = new MemoryStream();

		objectBytes.Write(DynamixBitmap.HeaderMagic, 0, DynamixBitmap.HeaderMagic.Length);

		int size = dbm.ImageData!.Length + 13;

		var sizeBytes = WriteIntLE(size);
		objectBytes.Write(sizeBytes, 0, sizeBytes.Length);

		var rowsBytes = WriteShortLE(dbm.Rows);
		objectBytes.Write(rowsBytes, 0, rowsBytes.Length);

		var colsBytes = WriteShortLE(dbm.Cols);
		objectBytes.Write(colsBytes, 0, colsBytes.Length);

		var bitDepthBytes = WriteShortLE(dbm.BitDepth);
		objectBytes.Write(bitDepthBytes, 0, bitDepthBytes.Length);

		objectBytes.WriteByte(dbm.UnkSpacer1);

		var imgLenBytes = WriteIntLE(dbm.ImageDataLen);
		objectBytes.Write(imgLenBytes, 0, imgLenBytes.Length);

		var unkSpacer2Bytes = WriteShortLE(dbm.UnkSpacer2);
		objectBytes.Write(unkSpacer2Bytes, 0, unkSpacer2Bytes.Length);

		objectBytes.Write(dbm.ImageData, 0, dbm.ImageData.Length);

		return objectBytes.ToArray();
	}
}
