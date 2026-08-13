using HercWorks.Core.Data.File;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Transforms byte[] data to and from .STR game files (subtitle/voice-line string tables — see
/// <see cref="StringFile"/> for the format writeup). New: no Java equivalent existed for this
/// format specifically (org.hercworks.core.data.file.StringFile had no matching transformer in
/// the Java source at all — the class was modeled but never wired to an I/O path).
///
/// Read-only: entry trailer bytes are undecoded, so there's no reliable way to reconstruct a
/// byte-exact write. Write-back isn't a current priority (see the codebase-wide "read and display"
/// scope for this pass), so ObjectToBytes is left unimplemented like several other transformers
/// in this codebase (e.g. MissionStringFileTransformer).
/// </summary>
public class StringFileTransformer : ThreeSpaceByteTransformer {
	/// <summary>How far past an entry's null terminator to search for the next well-formed entry when resyncing across an undecoded trailer.</summary>
	private const int MaxTrailerScan = 64;

	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		var file = new StringFile {
			RawBytes = inputArray,
			Ext = FileType.Str,

			TotalSize = IndexIntLE(),
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
	/// Resyncs to the next entry after an undecoded, variable-length per-file trailer: scans
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

	public override byte[]? ObjectToBytes(DataFile? source) {
		// TODO: not implemented — see class doc comment (trailer bytes are undecoded, so a
		// byte-exact round-trip isn't currently achievable).
		return null;
	}
}
