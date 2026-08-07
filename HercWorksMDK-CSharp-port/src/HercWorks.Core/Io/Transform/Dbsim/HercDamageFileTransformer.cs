using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Ported from org.hercworks.core.io.transform.dbsim.HercDamageFileTransformer.
///
/// TWO ISSUES FOUND HERE, flagged in KNOWN_ISSUES.md:
/// 1) `ComponentData` is allocated with length `totalComponents` (read from the file), but the
///    parse loop always runs exactly 29 times regardless of that value — if a real file reports
///    fewer than 29 components, this throws an IndexOutOfRangeException (matching the Java
///    original's equivalent ArrayIndexOutOfBoundsException).
/// 2) The write path multiplies each `CritChance` by 100 before writing; the read path assigns
///    the raw read value directly with no corresponding division. Round-tripping would scale
///    CritChance up by 100x every time it's written after being read.
/// </summary>
public class HercDamageFileTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		Index = 0;

		if (inputArray == null || inputArray.Length <= 0) {
			// TODO (carried over from Java): null input
			return null;
		}

		var data = new HercSimDamage {
			RawBytes = inputArray,
			Ext = FileType.Dmg,
			Dir = FileType.Dmg
		};

		SetBytes(inputArray);

		var internals = new HercSimDamage.InternalsHealth[IndexShortLE()];
		data.InternalsTotal = (short)internals.Length;

		for (int i = 0; i < data.InternalsTotal; i++) {
			short val = IndexShortLE();

			if (i < 10) {
				var system = data.NewInternalsHealth();
				system.Id = (short)i;
				system.Armor = val;
				if (val != 0) {
					system.Name = HercInternals.GetById((short)i);
				}

				internals[i] = system;
			}
		}
		data.Internals = internals;

		for (int i = 0; i < 22 - data.Internals.Length; i++) {
			Skip(2);
		}

		short totalComponents = IndexShortLE();

		// non-skimmer, non-spider hercs have 29 — see class doc re: the fixed loop below
		data.ComponentData = new HercSimDamage.HercPiece[totalComponents];
		for (int i = 0; i < 29; i++) {
			data.ComponentData[i] = ParseHercPiece(data);
		}

		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		using var outStream = new MemoryStream();

		var data = (HercSimDamage)source!;

		int diff = 0;
		if (data.FileName!.ToLowerInvariant().Contains(HercLUT.Skimmer.AbbrevDat.ToLowerInvariant())) {
			Write(outStream, WriteShortLE(1));
		} else {
			Write(outStream, WriteShortLE(22));
			diff = 22 - data.Internals!.Length;
		}

		for (int i = 0; i < data.InternalsTotal; i++) {
			Write(outStream, WriteShortLE(data.Internals![i].Armor));
		}

		for (int i = 0; i < diff; i++) {
			Write(outStream, WriteShortLE(0));
		}

		Write(outStream, WriteShortLE((short)data.ComponentData!.Length));

		foreach (var piece in data.ComponentData) {
			Write(outStream, WriteShortLE(piece.Armor));
			Write(outStream, WriteShortLE(piece.DebrisFlags));
			outStream.WriteByte(piece.BoneId);
			outStream.WriteByte(piece.Unk_val);
			Write(outStream, WriteShortLE((short)piece.MappedInternals!.Length));

			foreach (var t in piece.MappedInternals) {
				short chance = (short)(t.CritChance * 100); // see class doc
				Write(outStream, WriteShortLE(chance));
				Write(outStream, WriteShortLE(t.InternalsId!.Id));
			}
		}

		return outStream.ToArray();
	}

	private HercSimDamage.HercPiece ParseHercPiece(HercSimDamage data) {
		var piece = data.NewHercPiece();
		piece.Armor = IndexShortLE();
		piece.DebrisFlags = IndexShortLE();
		piece.BoneId = IndexByte();
		piece.Unk_val = IndexByte();

		piece.MappedInternals = new HercSimDamage.InternalsTarget[IndexShortLE()];
		for (int i = 0; i < piece.MappedInternals.Length; i++) {
			var internalComp = data.NewInternalsTarget();
			internalComp.CritChance = IndexShortLE();
			internalComp.InternalsId = HercInternals.GetById(IndexShortLE());
			piece.MappedInternals[i] = internalComp;
		}
		return piece;
	}

	private static void Write(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
