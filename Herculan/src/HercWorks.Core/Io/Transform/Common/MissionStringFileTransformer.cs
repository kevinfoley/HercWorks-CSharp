using HercWorks.Core.Data.File.Msn;
using System.Diagnostics;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>Reads a mission's <c>.ENG</c> text; see <see cref="MissionStringFile"/>.</summary>
public class MissionStringFileTransformer : ByteTransformer<MissionStringFile> {
	public override MissionStringFile? Parse(byte[]? inputArray) {
		SetBytes(inputArray!);

		var str = new MissionStringFile();

		var entries = new MissionStringFile.StringEntry[IndexShortLE()];

		for (int i = 0; i < entries.Length; i++) {
			short guid = IndexShortLE();
			short conditionRef = IndexShortLE();
			short parentRef = IndexShortLE();
			short len = IndexShortLE();

			var ent = str.CreateEntry(guid, conditionRef, parentRef, len, IndexString(len));

			Debug.WriteLine($"Created string entry {ent}");
			entries[i] = ent;
		}
		str.Strings = entries;

		return str;
	}

	public override byte[]? Write(MissionStringFile? source) {
		// Not implemented.
		return null;
	}
}
