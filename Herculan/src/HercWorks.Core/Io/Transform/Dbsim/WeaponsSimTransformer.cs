using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from simvol0/dat/WEAPONS.DAT (see <see cref="Weapons"/> and
/// docs/formats/weapons-dat-sim.md for the full field-by-field writeup). No Java equivalent existed
/// beyond a bare Total field — cracked 2026-08-11 from DBSIM.EXE disassembly. Records are
/// variable-length; every byte is preserved even where semantics aren't decoded yet, so this
/// round-trips byte-exact — verified against the real retail file.
/// </summary>
public class WeaponsSimTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var data = new Weapons {
			Ext = FileType.Dat,
			Dir = FileType.Dat,
			RawBytes = inputArray
		};

		data.Total = IndexShortLE();
		data.Templates = new Weapons.WeaponMountTemplate[data.Total];

		for (int i = 0; i < data.Total; i++) {
			var t = data.NewWeaponMountTemplate();

			t.Field0 = IndexShortLE();
			t.Field1 = IndexShortLE();
			t.Field2 = IndexShortLE();
			short depCount = IndexShortLE();
			t.DependentRaw = IndexShortLEArray(depCount * 2);

			t.SubSphereFlagRaw = IndexShortLE();
			t.SubMeshCountRaw = IndexShortLE();

			t.FiringSequence = new short[t.FiringSequenceCount][];
			for (int s = 0; s < t.FiringSequenceCount; s++) {
				t.FiringSequence[s] = IndexShortLEArray(4);
			}

			t.Tail = IndexSegment(0x30);

			data.Templates[i] = t;
		}

		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var data = (Weapons)source!;

		using var outStream = new MemoryStream();

		void WriteShort(short s) {
			var b = WriteShortLE(s);
			outStream.Write(b, 0, b.Length);
		}

		WriteShort(data.Total);

		for (int i = 0; i < data.Templates!.Length; i++) {
			var t = data.Templates[i];

			WriteShort(t.Field0);
			WriteShort(t.Field1);
			WriteShort(t.Field2);
			WriteShort((short)(t.DependentRaw.Length / 2));
			foreach (short v in t.DependentRaw) {
				WriteShort(v);
			}

			WriteShort(t.SubSphereFlagRaw);
			WriteShort(t.SubMeshCountRaw);
			foreach (var seq in t.FiringSequence) {
				foreach (short v in seq) {
					WriteShort(v);
				}
			}

			outStream.Write(t.Tail, 0, t.Tail.Length);
		}

		return outStream.ToArray();
	}
}
