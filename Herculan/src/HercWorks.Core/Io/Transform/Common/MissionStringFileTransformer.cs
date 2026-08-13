using HercWorks.Core.Data.File.Msn;
using HercWorks.Vol;
using System.Diagnostics;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>Ported from org.hercworks.core.io.transform.common.MissionStringFileTransformer.</summary>
public class MissionStringFileTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		SetBytes(inputArray!);

		var str = new MissionStringFile();

		var entries = new MissionStringFile.StringEntry[IndexShortLE()];

		for (int i = 0; i < entries.Length; i++) {
			short guid = IndexShortLE();
			short rval = IndexShortLE();
			short rflag = IndexShortLE();
			short len = IndexShortLE();

			// Bytes.from(indexSegment(len)).toCharArray() is the same zero-extend-per-byte
			// conversion as IndexString(len).
			var ent = str.CreateEntry(guid, rval, rflag, len, IndexString(len));

			Debug.WriteLine($"Created string entry {ent}");
			entries[i] = ent;
		}
		str.Strings = entries;

		return str;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		// TODO (carried over from Java): not implemented in the original
		return null;
	}
}
