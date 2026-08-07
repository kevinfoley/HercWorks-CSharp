using HercWorks.Core.Data.File.Dyn;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Transforms byte[] data to and from DynamixBitmap game files.
/// Ported from org.hercworks.core.io.transform.common.DynamixBitmapTransformer.
/// </summary>
public class DynamixBitmapTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO (carried over from Java): log null
			return null;
		}
		SetBytes(inputArray);

		var dbm = new DynamixBitmap {
			RawBytes = inputArray,
			Ext = FileType.Dbm,
			Dir = FileType.Dbm,

			Header = IndexSegment(4),
			FileSize = IndexSegmentLE(4),
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

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			// TODO (carried over from Java): null file check
			return null;
		}

		var dbm = (DynamixBitmap)source;

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
