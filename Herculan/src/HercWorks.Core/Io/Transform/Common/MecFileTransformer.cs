using HercWorks.Core.Data.File.Sav;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Transforms byte[] data to and from <c>ES2\DATA\player.mec</c> — see <see cref="MecFile"/> for the
/// format and the RE it came from: two leading shorts, that many variable-length entries, then the
/// armory flag table VSHELL closes every export with (<c>FUN_00412253</c>).
///
/// <para>The flag table is optional on the way in and preserved on the way out, so a retail file
/// round-trips byte for byte while a file written before the table was decoded — which DBSIM
/// accepts — still parses and is written back unchanged in shape. It is read only when the bytes
/// are actually there and self-consistent, since the base reader is unchecked.</para>
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
				PilotNameIndex = IndexShortLE(),
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
		data.WeaponFlags = IndexWeaponFlags();
		return data;
	}

	/// <summary>
	/// Reads the trailing armory flag table, or returns empty if this file has none. Every step is
	/// bounds-checked because the base reader is not, and because a file that ends at its last entry
	/// is legitimate rather than malformed.
	/// </summary>
	private byte[] IndexWeaponFlags() {
		if (Bytes == null || Bytes.Length - Index < 2) {
			return [];
		}

		short count = IndexShortLE();
		if (count <= 0 || Bytes.Length - Index < count) {
			return [];
		}

		return IndexSegment(count);
	}

	public override byte[]? Write(MecFile? data) {
		if (data == null) {
			return null;
		}

		using var outStream = new MemoryStream();

		Emit(outStream, WriteShortLE(data.PlayerEntryIndex));
		Emit(outStream, WriteShortLE((short)data.Entries.Length));

		foreach (var entry in data.Entries) {
			Emit(outStream, WriteShortLE(entry.PilotNameIndex));
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

		if (data.WeaponFlags.Length > 0) {
			Emit(outStream, WriteShortLE((short)data.WeaponFlags.Length));
			Emit(outStream, data.WeaponFlags);
		}

		return outStream.ToArray();
	}

	private static void Emit(MemoryStream outArr, byte[] data) {
		outArr.Write(data, 0, data.Length);
	}
}
