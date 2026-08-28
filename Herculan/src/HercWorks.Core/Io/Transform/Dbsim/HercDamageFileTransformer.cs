using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Ported from org.hercworks.core.io.transform.dbsim.HercDamageFileTransformer.
///
/// FIXED — verified against real .DMG files from a retail install
/// (ES2\VOL\simvol0\dmg\{SKIMMER,SPIDER,OUTLAW}.DMG). Three issues:
/// 1) The component parse loop always ran exactly 29 times regardless of the actual
///    `totalComponents` read from the file. SPIDER.DMG and OUTLAW.DMG both genuinely have 29
///    components (so the old hardcoded loop happened to work for them), but SKIMMER.DMG has
///    only 1 — decoding it by hand confirmed the fixed-29 loop would write past the end of a
///    1-element array, an immediate crash on real data. Fixed to loop `totalComponents` times.
/// 2) The internals-padding skip (`22 - internals.Length` shorts) is only correct for
///    "normal" hercs, which always store all 22 internals slots (so the skip amount is
///    genuinely 0 in every real file checked — the padding was never actually exercised for
///    them). SKIMMER.DMG stores only 1 internal, and unconditionally skipping `(22-1)*2 = 42`
///    bytes overruns its tiny 18-byte content — decoding confirmed the correct behavior mirrors
///    what the write path already does (skip 0 padding for a 1-internal/Skimmer-shaped record):
///    with 0 padding, the remaining bytes decode perfectly into one well-formed component.
///    Fixed to only apply the 22-slot padding skip when there's more than 1 internal.
/// 3) The write path multiplied `CritChance` by 100 before writing; read assigned the raw value
///    directly. Real files settle this: `CritChance` reads as exactly `20` for the large majority
///    of components across all three files (matching this class's own "0x14 in every known
///    example" doc comment) — not 2000, which the old write-then-read-back round trip would have
///    produced. The write path's `* 100` was the actual bug; fixed to write the raw value.
/// </summary>
public class HercDamageFileTransformer : ByteTransformer<HercSimDamage> {
	public override HercSimDamage? Parse(byte[]? inputArray) {
		Index = 0;

		if (inputArray == null || inputArray.Length <= 0) {
			// TODO (carried over from Java): null input
			return null;
		}

		var data = new HercSimDamage();

		SetBytes(inputArray);

		var internals = new HercSimDamage.InternalsHealth[IndexShortLE()];
		data.InternalsTotal = (short)internals.Length;

		for (int i = 0; i < data.InternalsTotal; i++) {
			// Every slot is kept, not just the first ten. DBSIM reads this array flat and indexes it
			// by the dependent index a component's own record names, and two of those indices are
			// past ten: slots 10 and 11 are the rear leg servos of a four-legged chassis, which
			// Mech_ComponentDamageWrite reads by literal offset alongside slots 0 and 1. Dropping
			// them lost PITBULL's rear legs, and left the write path dereferencing nulls for every
			// 22-slot file it round-tripped.
			var system = data.NewInternalsHealth();
			system.Id = (short)i;
			system.Armor = IndexShortLE();
			if (system.Armor != 0) {
				system.Name = HercInternals.GetById((short)i);
			}

			internals[i] = system;
		}
		data.Internals = internals;

		if (data.Internals.Length > 1) {
			for (int i = 0; i < 22 - data.Internals.Length; i++) {
				Skip(2);
			}
		}

		short totalComponents = IndexShortLE();

		data.ComponentData = new HercSimDamage.HercPiece[totalComponents];
		for (int i = 0; i < totalComponents; i++) {
			data.ComponentData[i] = ParseHercPiece(data);
		}

		return data;
	}

	public override byte[]? Write(HercSimDamage data) {
		using var outStream = new MemoryStream();

		int diff = 0;
		if (data.FileName!.ToLowerInvariant().Contains(HercLUT.Skimmer.AbbrevDat.ToLowerInvariant())) {
			Emit(outStream, WriteShortLE(1));
		} else {
			Emit(outStream, WriteShortLE(22));
			diff = 22 - data.Internals!.Length;
		}

		for (int i = 0; i < data.InternalsTotal; i++) {
			Emit(outStream, WriteShortLE(data.Internals![i].Armor));
		}

		for (int i = 0; i < diff; i++) {
			Emit(outStream, WriteShortLE(0));
		}

		Emit(outStream, WriteShortLE((short)data.ComponentData!.Length));

		foreach (var piece in data.ComponentData) {
			Emit(outStream, WriteShortLE(piece.Armor));
			Emit(outStream, WriteShortLE(piece.DebrisFlags));
			outStream.WriteByte(piece.BoneId);
			outStream.WriteByte(piece.DestructionFlags);
			Emit(outStream, WriteShortLE((short)piece.MappedInternals!.Length));

			foreach (var t in piece.MappedInternals) {
				Emit(outStream, WriteShortLE(t.CritChance));
				Emit(outStream, WriteShortLE(t.InternalsId!.Id));
			}
		}

		return outStream.ToArray();
	}

	private HercSimDamage.HercPiece ParseHercPiece(HercSimDamage data) {
		var piece = data.NewHercPiece();
		piece.Armor = IndexShortLE();
		piece.DebrisFlags = IndexShortLE();
		piece.BoneId = IndexByte();
		piece.DestructionFlags = IndexByte();

		piece.MappedInternals = new HercSimDamage.InternalsTarget[IndexShortLE()];
		for (int i = 0; i < piece.MappedInternals.Length; i++) {
			var internalComp = data.NewInternalsTarget();
			internalComp.CritChance = IndexShortLE();
			internalComp.InternalsId = HercInternals.GetById(IndexShortLE());
			piece.MappedInternals[i] = internalComp;
		}
		return piece;
	}

	private static void Emit(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
