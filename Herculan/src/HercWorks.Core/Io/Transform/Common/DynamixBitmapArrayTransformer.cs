using HercWorks.Core.Data.File.Dyn;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Ported from org.hercworks.core.io.transform.common.DynamixBitmapArrayTransformer.
///
/// FIXED — see KNOWN_ISSUES.md history: write used to reverse dba.FileSize before writing it,
/// but FileSize is stored on read via IndexSegmentLE (a no-op alias of IndexSegment, see ByteOps
/// notes), so the stored bytes are raw on-disk order — reversing them on write flipped the byte
/// order relative to how they were read. Write now writes FileSize as stored.
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

		// FIXED — see KNOWN_ISSUES.md history: this used to reverse FileSize before writing, but
		// FileSize is stored on read via IndexSegmentLE, which (despite the name) is a no-op alias
		// of IndexSegment — so the stored bytes are already in raw on-disk order. Reversing them
		// here flipped the byte order relative to how they were read. Now written as stored,
		// matching the read path (the side this project's own features already rely on).
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
