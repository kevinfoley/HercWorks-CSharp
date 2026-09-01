using HercWorks.Core.Data.File.Dyn;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Ported from org.hercworks.core.io.transform.common.DynamixBitmapArrayTransformer.
///
/// <para><b>Trap:</b> <c>IndexSegmentLE</c> is a no-op alias of <c>IndexSegment</c> despite its
/// name, so <c>FileSize</c> is stored in raw on-disk order. Do not byte-swap it on write.</para>
/// </summary>
public class DynamixBitmapArrayTransformer : ByteTransformer<DynamixBitmapArray> {
	public override DynamixBitmapArray? Parse(byte[]? inputArray) {
		if (inputArray == null) {
			// TODO (carried over from Java): log null
			return null;
		}

		SetBytes(inputArray);

		Skip(4); // magic header — the write path emits DynamixBitmapArray.HeaderMagic, so it isn't retained.

		var dba = new DynamixBitmapArray {
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
			var dbm = dynbitmapTransform.Parse(dbaItem)!;

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

	public override byte[]? Write(DynamixBitmapArray dba) {
		using var objectBytes = new MemoryStream();

		objectBytes.Write(DynamixBitmapArray.HeaderMagic, 0, DynamixBitmapArray.HeaderMagic.Length);

		// Written as stored — see the IndexSegmentLE trap on this class.
		objectBytes.Write(dba.FileSize!, 0, dba.FileSize!.Length);

		var rowBytes = WriteShortLE(dba.ArrayRow);
		objectBytes.Write(rowBytes, 0, rowBytes.Length);

		var colBytes = WriteShortLE(dba.ArrayCols);
		objectBytes.Write(colBytes, 0, colBytes.Length);

		var dbmConvert = new DynamixBitmapTransformer();
		foreach (var dbm in dba.Images!) {
			dbmConvert.ResetIndex();
			byte[]? data = dbmConvert.Write(dbm);
			if (data != null) {
				objectBytes.Write(data, 0, data.Length);
				objectBytes.WriteByte(0x00);
			}
		}

		return objectBytes.ToArray();
	}
}
