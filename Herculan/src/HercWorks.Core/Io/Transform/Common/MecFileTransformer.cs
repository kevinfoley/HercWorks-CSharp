using HercWorks.Core.Data.File.Sav;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Transforms byte[] data to and from <c>ES2\DATA\player.mec</c> — see <see cref="MecFile"/> for the
/// format and the RE it came from. Reads exactly what DBSIM reads and stops there: two leading
/// shorts, then that many variable-length entries. Anything past the last entry is stale buffer
/// content (the retail sample has 35 such bytes) and is neither consumed nor written back.
/// </summary>
public class MecFileTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		var data = new MecFile {
			RawBytes = inputArray,
			Ext = FileType.Sav,
			Dir = FileType.Sav
		};

		SetBytes(inputArray);

		data.PlayerEntryIndex = IndexShortLE();

		var entries = new MecEntry[IndexShortLE()];
		for (int i = 0; i < entries.Length; i++) {
			var entry = new MecEntry {
				Unk00 = IndexShortLE(),
				Unk02 = IndexShortLE(),
				MechType = IndexShortLE(),
				SlotCount = IndexShortLE()
			};

			entry.WeaponRefs = IndexShortLEArray(entry.SlotCount);
			entry.WeaponCounts = IndexShortLEArray(entry.SlotCount);
			entry.Unk3A = IndexShortLE();
			entry.BlockA = IndexSegment(26);
			entry.BlockB = IndexSegment(20);
			entry.BlockC = IndexSegment(20);

			entries[i] = entry;
		}

		data.Entries = entries;
		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		using var outStream = new MemoryStream();
		var data = (MecFile)source!;

		Write(outStream, WriteShortLE(data.PlayerEntryIndex));
		Write(outStream, WriteShortLE((short)data.Entries.Length));

		foreach (var entry in data.Entries) {
			Write(outStream, WriteShortLE(entry.Unk00));
			Write(outStream, WriteShortLE(entry.Unk02));
			Write(outStream, WriteShortLE(entry.MechType));
			Write(outStream, WriteShortLE(entry.SlotCount));
			Write(outStream, WriteShortLESegment(entry.WeaponRefs));
			Write(outStream, WriteShortLESegment(entry.WeaponCounts));
			Write(outStream, WriteShortLE(entry.Unk3A));
			Write(outStream, entry.BlockA);
			Write(outStream, entry.BlockB);
			Write(outStream, entry.BlockC);
		}

		return outStream.ToArray();
	}

	private static void Write(MemoryStream outArr, byte[] data) {
		outArr.Write(data, 0, data.Length);
	}
}
