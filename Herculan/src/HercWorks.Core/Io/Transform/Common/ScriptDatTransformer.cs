using HercWorks.Core.Data.File.Msn.Script;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Transforms byte[] data to and from <c>data\script.dat</c> — see docs/formats/script-dat.md for
/// the full byte-exact-verified format writeup this follows: a fixed 20-byte header, then 13
/// count-prefixed record blocks in exact order, each a GUID-filtered field-subset re-export of one
/// of <see cref="MissionFile"/>'s already-decoded rows. Confirmed against two independently
/// compiled real readers (DBSIM's own loader and VSHELL's `ShellMap` map-editor reader) plus all 10
/// real sample files found in the installed game (`ES2\DATA\script.dat` + 9 distinct
/// `ES2\SAV\scriptN.dat` save-slot snapshots).
///
/// The real file is a fixed 13,520-byte preallocated buffer with stale leftover bytes past the
/// real content's end in most real samples (confirmed byte-identical to the one sample whose real
/// content happens to fill the whole buffer) — this transformer reads only the 13 declared blocks
/// and stops there; it does not attempt to consume or preserve anything past block 13, and does not
/// pad the write-back to any fixed total length. Replaces a stale, never-implemented stub that
/// guessed at an unrelated 20-field/coordinate-array layout with no basis in the real format.
/// </summary>
public class ScriptDatTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		var data = new ScriptDat {
			RawBytes = inputArray,
			Ext = FileType.Dat,
			Dir = FileType.Dat
		};

		SetBytes(inputArray);

		data.HeaderBytes = IndexSegment(20);

		data.Coordinates = ReadArray(ParseCoordinate);
		data.Headings = ReadArray(ParseHeading);
		data.WaypointGroups = ReadArray(ParseWaypointGroup);
		data.LinksOrRewards = ReadArray(ParseLinkOrReward);
		data.Actions = ReadArray(ParseAction);
		data.ActionPairs = ReadArray(ParseActionPair);
		data.SpawnRecords = ReadArray(ParseSpawnRecordExport);
		data.Entities102 = ReadArray(ParseEntity102Export);
		data.MiscEntities = ReadArray(ParseMiscEntityExport);
		data.LinkedRefs22 = ReadArray(ParseLinkedRef22Export);
		data.Entities164 = ReadArray(ParseEntity164Export);
		data.LinkedRefs58 = ReadArray(ParseLinkedRef58Export);

		int lutCount = IndexShortLE();
		data.UnlockedLutRefs = IndexShortLEArray(lutCount);

		return data;
	}

	private T[] ReadArray<T>(Func<T> parseOne) {
		var arr = new T[IndexShortLE()];
		for (int i = 0; i < arr.Length; i++) {
			arr[i] = parseOne();
		}
		return arr;
	}

	// ---- Block 1: ScriptCoordinate (12 bytes) -----------------------------------------------

	private ScriptCoordinate ParseCoordinate() => new() {
		X = IndexIntLE(),
		Y = IndexIntLE(),
		Z = IndexIntLE()
	};

	// ---- Block 2: ScriptHeading (2 bytes) ----------------------------------------------------

	private ScriptHeading ParseHeading() => new() { Value = IndexShortLE() };

	// ---- Block 3: ScriptWaypointGroup (variable) ---------------------------------------------

	private ScriptWaypointGroup ParseWaypointGroup() {
		int count = IndexShortLE();
		return new ScriptWaypointGroup { Waypoints = IndexShortLEArray(count) };
	}

	// ---- Block 4: ScriptLinkOrReward (6 bytes) -----------------------------------------------

	private ScriptLinkOrReward ParseLinkOrReward() => new() {
		TypeFlag = IndexShortLE(),
		RefA = IndexShortLE(),
		RefBOrLiteral = IndexShortLE()
	};

	// ---- Block 5: ScriptAction (74 bytes) ------------------------------------------------------

	private ScriptAction ParseAction() => new() {
		Type = IndexShortLE(),
		Verb = IndexShortLE(),
		RefsRow9 = IndexShortLEArray(8),
		ArrayA = IndexShortLEArray(10),
		ArrayB = IndexShortLEArray(10),
		LutRefs = IndexShortLEArray(5),
		SecondaryValue = IndexShortLE(),
		Target = IndexShortLE()
	};

	// ---- Block 6: ScriptActionPair (24 bytes) --------------------------------------------------

	private ScriptActionPair ParseActionPair() => new() {
		PrimaryActionRef = IndexShortLE(),
		TimerValue = IndexShortLE(),
		SequenceRefs = IndexShortLEArray(10)
	};

	// ---- Block 7: ScriptSpawnRecordExport (134 bytes) ------------------------------------------

	private ScriptSpawnRecordExport ParseSpawnRecordExport() => new() {
		HeadBytes = IndexSegment(40),
		SmallDiscrete = IndexShortLE(),
		TailBytes = IndexSegment(92)
	};

	// ---- Block 8: ScriptEntity102Export (92 bytes) ---------------------------------------------

	private ScriptEntity102Export ParseEntity102Export() => new() {
		HeadBytes = IndexSegment(44),
		BinaryField = IndexShortLE(),
		TailBytes = IndexSegment(46)
	};

	// ---- Block 9: ScriptMiscEntityExport (52 bytes) --------------------------------------------

	private ScriptMiscEntityExport ParseMiscEntityExport() => new() {
		TypeLikeScalar = IndexShortLE(),
		TailBytes = IndexSegment(50)
	};

	// ---- Block 10: ScriptLinkedRef22Export (14 bytes) ------------------------------------------

	private ScriptLinkedRef22Export ParseLinkedRef22Export() => new() {
		SmallInt1 = IndexShortLE(),
		SmallInt2 = IndexShortLE(),
		RefRow6 = IndexShortLE(),
		RefRow8 = IndexShortLE(),
		DiscriminatorType = IndexShortLE(),
		DiscriminatedRef = IndexShortLE(),
		RefRow10 = IndexShortLE()
	};

	// ---- Block 11: ScriptEntity164Export (156 bytes) -------------------------------------------

	private ScriptEntity164Export ParseEntity164Export() => new() {
		BinaryFlag = IndexShortLE(),
		NearConstant = IndexShortLE(),
		DeadZone = IndexShortLEArray(18),
		Discriminator = IndexShortLE(),
		SmallDiscrete = IndexShortLE(),
		RefRow6 = IndexShortLE(),
		RefRow7 = IndexShortLE(),
		RefRow8 = IndexShortLE(),
		DiscriminatedRefs = IndexShortLEArray(20),
		Row15Refs = IndexShortLEArray(10),
		TriStateFlag = IndexShortLE(),
		RefRow10 = IndexShortLE(),
		ArrayA = IndexShortLEArray(10),
		ArrayB = IndexShortLEArray(10),
		TrailingFlag = IndexShortLE()
	};

	// ---- Block 12: ScriptLinkedRef58Export (54 bytes) ------------------------------------------

	private ScriptLinkedRef58Export ParseLinkedRef58Export() => new() {
		Unk02 = IndexShortLE(),
		Unk04 = IndexShortLE(),
		Discriminator = IndexShortLE(),
		DiscriminatedRef = IndexShortLE(),
		RefRow6 = IndexShortLE(),
		RefRow8 = IndexShortLE(),
		LutRef = IndexShortLE(),
		PairRefs = IndexShortLEArray(10),
		PairTags = IndexShortLEArray(10)
	};

	// ==============================================================================================
	// Write path
	// ==============================================================================================

	public override byte[]? ObjectToBytes(DataFile? source) {
		using var outStream = new MemoryStream();
		var data = (ScriptDat)source!;

		Write(outStream, data.HeaderBytes);

		WriteArray(outStream, data.Coordinates, WriteCoordinate);
		WriteArray(outStream, data.Headings, WriteHeading);
		WriteArray(outStream, data.WaypointGroups, WriteWaypointGroup);
		WriteArray(outStream, data.LinksOrRewards, WriteLinkOrReward);
		WriteArray(outStream, data.Actions, WriteAction);
		WriteArray(outStream, data.ActionPairs, WriteActionPair);
		WriteArray(outStream, data.SpawnRecords, WriteSpawnRecordExport);
		WriteArray(outStream, data.Entities102, WriteEntity102Export);
		WriteArray(outStream, data.MiscEntities, WriteMiscEntityExport);
		WriteArray(outStream, data.LinkedRefs22, WriteLinkedRef22Export);
		WriteArray(outStream, data.Entities164, WriteEntity164Export);
		WriteArray(outStream, data.LinkedRefs58, WriteLinkedRef58Export);

		Write(outStream, WriteShortLE((short)data.UnlockedLutRefs.Length));
		Write(outStream, WriteShortLESegment(data.UnlockedLutRefs));

		return outStream.ToArray();
	}

	private void WriteArray<T>(MemoryStream outStream, T[] items, Action<MemoryStream, T> writeOne) {
		Write(outStream, WriteShortLE((short)items.Length));
		foreach (var item in items) {
			writeOne(outStream, item);
		}
	}

	private void WriteCoordinate(MemoryStream o, ScriptCoordinate e) {
		Write(o, WriteIntLE(e.X));
		Write(o, WriteIntLE(e.Y));
		Write(o, WriteIntLE(e.Z));
	}

	private void WriteHeading(MemoryStream o, ScriptHeading e) {
		Write(o, WriteShortLE(e.Value));
	}

	private void WriteWaypointGroup(MemoryStream o, ScriptWaypointGroup e) {
		Write(o, WriteShortLE((short)e.Waypoints.Length));
		Write(o, WriteShortLESegment(e.Waypoints));
	}

	private void WriteLinkOrReward(MemoryStream o, ScriptLinkOrReward e) {
		Write(o, WriteShortLE(e.TypeFlag));
		Write(o, WriteShortLE(e.RefA));
		Write(o, WriteShortLE(e.RefBOrLiteral));
	}

	private void WriteAction(MemoryStream o, ScriptAction e) {
		Write(o, WriteShortLE(e.Type));
		Write(o, WriteShortLE(e.Verb));
		Write(o, WriteShortLESegment(e.RefsRow9));
		Write(o, WriteShortLESegment(e.ArrayA));
		Write(o, WriteShortLESegment(e.ArrayB));
		Write(o, WriteShortLESegment(e.LutRefs));
		Write(o, WriteShortLE(e.SecondaryValue));
		Write(o, WriteShortLE(e.Target));
	}

	private void WriteActionPair(MemoryStream o, ScriptActionPair e) {
		Write(o, WriteShortLE(e.PrimaryActionRef));
		Write(o, WriteShortLE(e.TimerValue));
		Write(o, WriteShortLESegment(e.SequenceRefs));
	}

	private void WriteSpawnRecordExport(MemoryStream o, ScriptSpawnRecordExport e) {
		Write(o, e.HeadBytes);
		Write(o, WriteShortLE(e.SmallDiscrete));
		Write(o, e.TailBytes);
	}

	private void WriteEntity102Export(MemoryStream o, ScriptEntity102Export e) {
		Write(o, e.HeadBytes);
		Write(o, WriteShortLE(e.BinaryField));
		Write(o, e.TailBytes);
	}

	private void WriteMiscEntityExport(MemoryStream o, ScriptMiscEntityExport e) {
		Write(o, WriteShortLE(e.TypeLikeScalar));
		Write(o, e.TailBytes);
	}

	private void WriteLinkedRef22Export(MemoryStream o, ScriptLinkedRef22Export e) {
		Write(o, WriteShortLE(e.SmallInt1));
		Write(o, WriteShortLE(e.SmallInt2));
		Write(o, WriteShortLE(e.RefRow6));
		Write(o, WriteShortLE(e.RefRow8));
		Write(o, WriteShortLE(e.DiscriminatorType));
		Write(o, WriteShortLE(e.DiscriminatedRef));
		Write(o, WriteShortLE(e.RefRow10));
	}

	private void WriteEntity164Export(MemoryStream o, ScriptEntity164Export e) {
		Write(o, WriteShortLE(e.BinaryFlag));
		Write(o, WriteShortLE(e.NearConstant));
		Write(o, WriteShortLESegment(e.DeadZone));
		Write(o, WriteShortLE(e.Discriminator));
		Write(o, WriteShortLE(e.SmallDiscrete));
		Write(o, WriteShortLE(e.RefRow6));
		Write(o, WriteShortLE(e.RefRow7));
		Write(o, WriteShortLE(e.RefRow8));
		Write(o, WriteShortLESegment(e.DiscriminatedRefs));
		Write(o, WriteShortLESegment(e.Row15Refs));
		Write(o, WriteShortLE(e.TriStateFlag));
		Write(o, WriteShortLE(e.RefRow10));
		Write(o, WriteShortLESegment(e.ArrayA));
		Write(o, WriteShortLESegment(e.ArrayB));
		Write(o, WriteShortLE(e.TrailingFlag));
	}

	private void WriteLinkedRef58Export(MemoryStream o, ScriptLinkedRef58Export e) {
		Write(o, WriteShortLE(e.Unk02));
		Write(o, WriteShortLE(e.Unk04));
		Write(o, WriteShortLE(e.Discriminator));
		Write(o, WriteShortLE(e.DiscriminatedRef));
		Write(o, WriteShortLE(e.RefRow6));
		Write(o, WriteShortLE(e.RefRow8));
		Write(o, WriteShortLE(e.LutRef));
		Write(o, WriteShortLESegment(e.PairRefs));
		Write(o, WriteShortLESegment(e.PairTags));
	}

	private static void Write(MemoryStream outArr, byte[] data) {
		outArr.Write(data, 0, data.Length);
	}
}
