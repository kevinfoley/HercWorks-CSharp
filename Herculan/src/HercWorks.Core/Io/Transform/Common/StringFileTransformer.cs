using HercWorks.Core.Data.File;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Reads .STR string tables into <see cref="StringFile"/> — the first group only, with each entry's
/// attribute bytes kept raw as its trailer and found by scanning for the next well-formed entry
/// rather than read through the attribute count. Read-only: <see cref="Write"/> returns null.
/// </summary>
public class StringFileTransformer : ByteTransformer<StringFile> {
	/// <summary>How far past an entry's null terminator to search for the next well-formed entry.</summary>
	private const int MaxTrailerScan = 64;

	public override StringFile? Parse(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		var file = new StringFile {
			ContentLength = IndexIntLE(),
		};

		int count = IndexShortLE();
		var entries = new StringFile.StringEntry[count];

		for (int i = 0; i < count; i++) {
			if (Index + 2 > inputArray.Length) {
				break; // truncated/corrupt file — return what parsed cleanly so far.
			}

			int len = IndexShortLE();
			if (len <= 0 || Index + len > inputArray.Length) {
				break;
			}

			string text = IndexString(len - 1); // len includes the null terminator.
			Skip(1); // the null terminator itself.

			bool isLast = i == count - 1;
			int trailerLen = isLast ? inputArray.Length - Index : FindNextEntryOffset(inputArray);

			entries[i] = new StringFile.StringEntry {
				Text = text,
				Trailer = IndexSegment(trailerLen)
			};
		}

		file.Entries = entries;
		return file;
	}

	/// <summary>
	/// Finds the next entry after this one's attribute bytes: scans
	/// forward from the current position for the nearest offset where a UINT16 length field is
	/// immediately followed by that many bytes ending in a null terminator, with the preceding
	/// bytes mostly printable ASCII. Falls back to 0 (no trailer) if nothing plausible is found
	/// within <see cref="MaxTrailerScan"/> bytes, rather than desyncing the rest of the file.
	/// </summary>
	private int FindNextEntryOffset(byte[] data) {
		for (int t = 0; t <= MaxTrailerScan; t++) {
			int candidate = Index + t;
			if (candidate + 2 > data.Length) {
				break;
			}

			int len = data[candidate] | (data[candidate + 1] << 8);
			if (len <= 0) {
				continue;
			}

			int strEnd = candidate + 2 + len;
			if (strEnd > data.Length || data[strEnd - 1] != 0x00) {
				continue;
			}

			int printable = 0;
			for (int k = candidate + 2; k < strEnd - 1; k++) {
				if (data[k] is >= 0x20 and <= 0x7E) {
					printable++;
				}
			}

			int textLen = len - 1;
			if (textLen == 0 || printable / (double)textLen > 0.9) {
				return t;
			}
		}

		return 0;
	}

	public override byte[]? Write(StringFile? source) {
		return null;
	}
}
