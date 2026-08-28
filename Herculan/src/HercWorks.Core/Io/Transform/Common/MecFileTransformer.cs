using HercWorks.Core.Data.File.Sav;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Transforms byte[] data to and from <c>ES2\DATA\player.mec</c> — see <see cref="MecFile"/> for the
/// format and the RE it came from. Reads exactly what DBSIM reads and stops there: two leading
/// shorts, then that many variable-length entries. Anything past the last entry is stale buffer
/// content (the retail sample has 35 such bytes) and is neither consumed nor written back.
/// </summary>
public class MecFileTransformer : ByteTransformer<MecFile> {
	public override MecFile? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		var data = new MecFile();

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
			entry.WeaponAmmoTypes = IndexShortLEArray(entry.SlotCount);
			entry.Unk3A = IndexShortLE();
			entry.BlockA = IndexSegment(26);
			entry.BlockB = IndexSegment(20);
			entry.BlockC = IndexSegment(20);

			entries[i] = entry;
		}

		data.Entries = entries;
		return data;
	}

	public override byte[]? Write(MecFile? data) {
		if (data == null) {
			return null;
		}

		using var outStream = new MemoryStream();

		Emit(outStream, WriteShortLE(data.PlayerEntryIndex));
		Emit(outStream, WriteShortLE((short)data.Entries.Length));

		foreach (var entry in data.Entries) {
			Emit(outStream, WriteShortLE(entry.Unk00));
			Emit(outStream, WriteShortLE(entry.Unk02));
			Emit(outStream, WriteShortLE(entry.MechType));
			Emit(outStream, WriteShortLE(entry.SlotCount));
			Emit(outStream, WriteShortLESegment(entry.WeaponRefs));
			Emit(outStream, WriteShortLESegment(entry.WeaponAmmoTypes));
			Emit(outStream, WriteShortLE(entry.Unk3A));
			Emit(outStream, entry.BlockA);
			Emit(outStream, entry.BlockB);
			Emit(outStream, entry.BlockC);
		}

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) {
		outArr.Write(data, 0, data.Length);
	}
}
