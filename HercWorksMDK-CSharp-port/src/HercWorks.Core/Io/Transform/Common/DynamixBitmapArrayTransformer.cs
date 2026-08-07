using HercWorks.Core.Data.File.Dyn;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Ported from org.hercworks.core.io.transform.common.DynamixBitmapArrayTransformer.
///
/// NOTE: on write, this reverses dba.FileSize before writing it — a genuine, correct byte
/// reversal (`.reverse()` was called in the original, not just `.byteOrder()`). Since FileSize is
/// stored on read via IndexSegmentLE (a no-op alias of IndexSegment, see ByteOps notes), the
/// stored bytes are raw on-disk order; reversing them on write means the size ends up written in
/// the opposite byte order from how it was read. Ported literally either way.
/// </summary>
public class DynamixBitmapArrayTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null) {
			// TODO (carried over from Java): log null
			return null;
		}

		SetBytes(inputArray);

		var dba = new DynamixBitmapArray {
			RawBytes = (byte[])inputArray.Clone(),
			Ext = FileType.Dba,
			Dir = FileType.Dba,

			Header = IndexSegment(4),
			FileSize = IndexSegmentLE(4),
			ArrayRow = IndexShortLE(),
			ArrayCols = IndexShortLE() // TODO (carried over from Java): could this actually be an INT32 for total images?
		};

		var images = new DynamixBitmap[dba.ArrayRow];

		int imageCount = 0;

		var dynbitmapTransform = new DynamixBitmapTransformer();

		int actualBytes = Bytes!.Length - Index;
		while (Index < actualBytes) {
			Skip(4);

			int fileLength = IndexIntLE(); // slight read-ahead to get file size first

			Index -= 8; // transformer assumes a clean read from byte[0], so reset head read.
			byte[] dbaItem = IndexSegment(fileLength + 8); // +8 for the 4-byte DBM header and INT32 file size.

			dynbitmapTransform.ResetIndex();
			var dbm = (DynamixBitmap)dynbitmapTransform.BytesToObject(dbaItem)!;

			dbm.FileName = "_" + imageCount;

			images[imageCount] = dbm;
			imageCount++;

			if (Index + 1 < actualBytes) {
				byte space = IndexByte();
				if (space != 0x00) {
					Index -= 1;
				}
			}
		}
		dba.Images = images;

		return dba;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			// TODO (carried over from Java): log null
			return null;
		}

		var dba = (DynamixBitmapArray)source;

		using var objectBytes = new MemoryStream();

		objectBytes.Write(DynamixBitmapArray.HeaderMagic, 0, DynamixBitmapArray.HeaderMagic.Length);

		var reversedSize = (byte[])dba.FileSize!.Clone();
		Array.Reverse(reversedSize);
		objectBytes.Write(reversedSize, 0, reversedSize.Length);

		var rowBytes = WriteShortLE(dba.ArrayRow);
		objectBytes.Write(rowBytes, 0, rowBytes.Length);

		var colBytes = WriteShortLE(dba.ArrayCols);
		objectBytes.Write(colBytes, 0, colBytes.Length);

		var dbmConvert = new DynamixBitmapTransformer();
		foreach (var dbm in dba.Images!) {
			dbmConvert.ResetIndex();
			byte[]? data = dbmConvert.ObjectToBytes(dbm);
			if (data != null) {
				objectBytes.Write(data, 0, data.Length);
				objectBytes.WriteByte(0x00);
			}
		}

		return objectBytes.ToArray();
	}
}
