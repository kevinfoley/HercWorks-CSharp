using HercWorks.Core.Data.File.Msn;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Transforms byte[] data to and from .MSN mission files — see docs/formats/msn-mission-file.md
/// for the full, byte-exact-verified format writeup this rewrite follows: a 2-byte revision field
/// (always 5) followed by 17 array/skip rows, in the exact fixed order implemented below (including
/// the skip-only row #5 and row #8's true 2-byte-per-entry on-disk nested-array width). Replaces a
/// prior version hardcoded against a single file (TRAIN5.MSN) that invented a fixed 189-short block
/// with no basis in the real format.
///
/// Row #17 (the file's last row) is the one place a real retail file (DEMO2.MSN) is known to be
/// truncated by 42 bytes mid-record — see <see cref="ParseRow17"/> for how that's handled: read
/// stops cleanly at EOF and the leftover raw bytes are preserved for an exact round-trip, rather
/// than throwing or silently fabricating data.
/// Ported from org.hercworks.core.io.transform.common.MissionFileTransformer.
/// </summary>
public class MissionFileTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		var data = new MissionFile {
			RawBytes = inputArray,
			Ext = FileType.Msn,
			Dir = FileType.Msn
		};

		SetBytes(inputArray);

		data.Revision = IndexShortLE();

		data.TriggerEntries = ReadArray(ParseRow1);
		data.OverridePatches = ReadArray(ParseRow2);
		data.Variants = ReadArray(ParseRow3);
		data.RewardPackages = ReadArray(ParseRow4);

		int skipCount = IndexShortLE();
		data.SkippedBytes = IndexSegment(skipCount * 64);

		data.Points = ReadArray(ParseRow6);
		data.Flags = ReadArray(ParseRow7);
		data.WaypointGroups = ReadArray(ParseRow8);
		data.LinksOrRewards = ReadArray(ParseRow9);
		data.Actions = ReadArray(ParseRow10);
		data.ActionPairs = ReadArray(ParseRow11);
		data.SpawnRecords = ReadArray(ParseRow12);
		data.Entities102 = ReadArray(ParseRow13);
		data.MiscEntities = ReadArray(ParseRow14);
		data.LinkedRefs22 = ReadArray(ParseRow15);
		data.Entities164 = ReadArray(ParseRow16);

		ParseRow17(data);

		return data;
	}

	private T[] ReadArray<T>(Func<T> parseOne) {
		var arr = new T[IndexShortLE()];
		for (int i = 0; i < arr.Length; i++) {
			arr[i] = parseOne();
		}
		return arr;
	}

	// ---- Row #1: UnkHeaderEntry (14 bytes) --------------------------------------------------

	private UnkHeaderEntry ParseRow1() => new(
		IndexShortLE(), IndexShortLE(), IndexShortLE(), IndexShortLE(),
		IndexShortLE(), IndexShortLE(), IndexShortLE());

	// ---- Row #2: CampaignOverridePatch82 (82 bytes, scratch) --------------------------------

	private CampaignOverridePatch82 ParseRow2() => new() { Data = IndexShortLEArray(41) };

	// ---- Row #3: VariantValue8 (8 bytes) ----------------------------------------------------

	private VariantValue8 ParseRow3() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		Unk04 = IndexShortLE(),
		Payload = IndexShortLE()
	};

	// ---- Row #4: RewardPackage144 (144 bytes, no identity field) ---------------------------

	private RewardPackage144 ParseRow4() => new() {
		ConditionRef = IndexShortLE(),
		LutRefsA = IndexShortLEArray(10),
		LutRefsB = IndexShortLEArray(30),
		LutRefsC = IndexShortLEArray(30),
		VariantRef = IndexShortLE()
	};

	// ---- Row #6: MapPoint22 (22 bytes) ------------------------------------------------------

	private MapPoint22 ParseRow6() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		InheritIndex = IndexShortLE(),
		Unk06 = IndexShortLE(),
		SumFlag = IndexShortLE(),
		X = IndexIntLE(),
		Y = IndexIntLE(),
		Z = IndexIntLE()
	};

	// ---- Row #7: Flag10 (10 bytes) ----------------------------------------------------------

	private Flag10 ParseRow7() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		InheritIndex = IndexShortLE(),
		Unk06 = IndexShortLE(),
		Payload = IndexShortLE()
	};

	// ---- Row #8: WaypointGroup (10 fixed bytes + nested-count x 2 bytes) -------------------

	private WaypointGroup ParseRow8() {
		var g = new WaypointGroup {
			GUID = IndexShortLE(),
			ConditionRef = IndexShortLE(),
			InheritIndex = IndexShortLE(),
			Unk06 = IndexShortLE()
		};

		int nestedCount = IndexShortLE();
		g.Waypoints = IndexShortLEArray(nestedCount);

		return g;
	}

	// ---- Row #9: LinkOrReward12 (12 bytes) --------------------------------------------------

	private LinkOrReward12 ParseRow9() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		Unk04 = IndexShortLE(),
		TypeFlag = IndexShortLE(),
		RefA = IndexShortLE(),
		RefBOrLiteral = IndexShortLE()
	};

	// ---- Row #10: Action82 (82 bytes) -------------------------------------------------------

	private Action82 ParseRow10() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		Unk04 = IndexShortLE(),
		Type = IndexShortLE(),
		Verb = IndexShortLE(),
		RefsRow9 = IndexShortLEArray(8),
		ConstantSpan = IndexShortLEArray(21),
		LutRefs = IndexShortLEArray(5),
		SecondaryValue = IndexShortLE(),
		Target = IndexShortLE()
	};

	// ---- Row #11: ActionPair30 (30 bytes) ---------------------------------------------------

	private ActionPair30 ParseRow11() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		Unk04 = IndexShortLE(),
		PrimaryActionRef = IndexShortLE(),
		TimerValue = IndexShortLE(),
		SequenceRefs = IndexShortLEArray(10)
	};

	// ---- Row #12: SpawnRecord144 (144 bytes) ------------------------------------------------

	private SpawnRecord144 ParseRow12() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		InheritIndex = IndexShortLE(),
		CompoundConditionPartner = IndexShortLE(),
		BinaryFlag = IndexShortLE(),
		NearConstant = IndexShortLE(),
		DeadZone = IndexShortLEArray(18),
		SmallDiscrete = IndexShortLE(),
		UnresolvedRefs = IndexShortLEArray(10),
		RefRow6 = IndexShortLE(),
		RefRow7 = IndexShortLE(),
		SmallDiscrete2 = IndexShortLE(),
		PairedRefs = IndexShortLEArray(20),
		AlwaysPopulatedBlock = IndexShortLEArray(9),
		Constant5 = IndexShortLE(),
		Constant2 = IndexShortLE(),
		RefRow10Slot1 = IndexShortLE(),
		RefRow10Slot2 = IndexShortLE(),
		TrailingField = IndexShortLE()
	};

	// ---- Row #13: UnkEntity102Bytes (102 bytes) ---------------------------------------------

	private UnkEntity102Bytes ParseRow13() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		InheritIndex = IndexShortLE(),
		Unk06 = IndexShortLE(),
		FlagsA = IndexShortLEArray(20),
		RefRow6 = IndexShortLE(),
		RefRow7 = IndexShortLE(),
		BinaryField = IndexShortLE(),
		Unk36 = IndexShortLE(),
		FlagsB = IndexShortLEArray(20),
		RefRow10Slot1 = IndexShortLE(),
		RefRow10Slot2 = IndexShortLE(),
		UnkVal_100 = IndexShortLE()
	};

	// ---- Row #14: MiscEntityInfo (62 bytes) -------------------------------------------------

	private MiscEntityInfo ParseRow14() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		InheritIndex = IndexShortLE(),
		Unk06 = IndexShortLE(),
		TypeLikeScalar = IndexShortLE(),
		RefRow6 = IndexShortLE(),
		RefRow7 = IndexShortLE(),
		SmallDiscrete = IndexShortLE(),
		SparseBlock = IndexShortLEArray(20),
		RefRow10Slot1 = IndexShortLE(),
		RefRow10Slot2 = IndexShortLE(),
		TrailingField = IndexShortLE()
	};

	// ---- Row #15: LinkedRef22 (22 bytes) ----------------------------------------------------

	private LinkedRef22 ParseRow15() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		InheritIndex = IndexShortLE(),
		CompoundConditionPartner = IndexShortLE(),
		SmallInt1 = IndexShortLE(),
		SmallInt2 = IndexShortLE(),
		RefRow6 = IndexShortLE(),
		RefRow8 = IndexShortLE(),
		DiscriminatorType = IndexShortLE(),
		DiscriminatedRef = IndexShortLE(),
		RefRow10 = IndexShortLE()
	};

	// ---- Row #16: UnkEntity164Bytes (164 bytes) ---------------------------------------------

	private UnkEntity164Bytes ParseRow16() => new() {
		GUID = IndexShortLE(),
		ConditionRef = IndexShortLE(),
		CompoundConditionPartner = IndexShortLE(),
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
		TrailingDiscriminator = IndexShortLE(),
		Payload1 = IndexShortLE(),
		Payload2 = IndexShortLE(),
		Payload3 = IndexShortLE(),
		Payload4 = IndexShortLE(),
		DeadZone2 = IndexShortLEArray(16),
		TrailingFlag = IndexShortLE()
	};

	// ---- Row #17: LinkedRef58 (58 bytes, no identity field) --------------------------------

	private const int Row17RecordSize = 58;

	/// <summary>
	/// One real retail file (DEMO2.MSN) is truncated 42 bytes short of its final row #17 record
	/// (only 16 of the expected 58 bytes remain at EOF) — a known, isolated data issue, not a
	/// format-table error (see docs/formats/msn-mission-file.md, "How this was verified" under row
	/// #17). Rather than throw or silently fabricate the missing bytes, this stops reading cleanly
	/// at EOF and preserves whatever raw bytes remain so a write-back reproduces the original file
	/// exactly, truncation included.
	/// </summary>
	private void ParseRow17(MissionFile data) {
		int count = IndexShortLE();
		var entries = new LinkedRef58?[count];

		for (int i = 0; i < count; i++) {
			int remaining = GetBytes().Length - Index;
			if (remaining < Row17RecordSize) {
				data.TruncatedRow17Tail = IndexSegment(remaining);
				entries[i] = null;
				break;
			}

			entries[i] = ParseLinkedRef58();
		}

		data.LinkedRefs58 = entries;
	}

	private LinkedRef58 ParseLinkedRef58() {
		var r = new LinkedRef58 {
			ConditionRef = IndexShortLE(),
			Unk02 = IndexShortLE(),
			Unk04 = IndexShortLE(),
			Discriminator = IndexShortLE(),
			DiscriminatedRef = IndexShortLE(),
			RefRow6 = IndexShortLE(),
			RefRow8 = IndexShortLE(),
			LutRef = IndexShortLE(),
			PairCount = IndexShortLE()
		};

		for (int p = 0; p < r.Pairs.Length; p++) {
			r.Pairs[p] = new LinkedRef58Pair {
				Ref = IndexShortLE(),
				Tag = IndexShortLE()
			};
		}

		return r;
	}

	// ==========================================================================================
	// Write path
	// ==========================================================================================

	public override byte[]? ObjectToBytes(DataFile? source) {
		using var outStream = new MemoryStream();
		var data = (MissionFile)source!;

		Write(outStream, WriteShortLE(data.Revision));

		WriteArray(outStream, data.TriggerEntries!, WriteRow1);
		WriteArray(outStream, data.OverridePatches!, WriteRow2);
		WriteArray(outStream, data.Variants!, WriteRow3);
		WriteArray(outStream, data.RewardPackages!, WriteRow4);

		Write(outStream, WriteShortLE((short)(data.SkippedBytes!.Length / 64)));
		Write(outStream, data.SkippedBytes);

		WriteArray(outStream, data.Points!, WriteRow6);
		WriteArray(outStream, data.Flags!, WriteRow7);
		WriteArray(outStream, data.WaypointGroups!, WriteRow8);
		WriteArray(outStream, data.LinksOrRewards!, WriteRow9);
		WriteArray(outStream, data.Actions!, WriteRow10);
		WriteArray(outStream, data.ActionPairs!, WriteRow11);
		WriteArray(outStream, data.SpawnRecords!, WriteRow12);
		WriteArray(outStream, data.Entities102!, WriteRow13);
		WriteArray(outStream, data.MiscEntities!, WriteRow14);
		WriteArray(outStream, data.LinkedRefs22!, WriteRow15);
		WriteArray(outStream, data.Entities164!, WriteRow16);

		WriteRow17(outStream, data);

		return outStream.ToArray();
	}

	private void WriteArray<T>(MemoryStream outStream, T[] items, Action<MemoryStream, T> writeOne) {
		Write(outStream, WriteShortLE((short)items.Length));
		foreach (var item in items) {
			writeOne(outStream, item);
		}
	}

	private void WriteRow1(MemoryStream o, UnkHeaderEntry e) {
		Write(o, WriteShortLE(e.Ordinal));
		Write(o, WriteShortLE(e.ConditionInput));
		Write(o, WriteShortLE(e.TypeDiscriminator));
		Write(o, WriteShortLE(e.FlagIndexOrRangeLower));
		Write(o, WriteShortLE(e.OperatorOrRangeUpperOrResult));
		Write(o, WriteShortLE(e.ComparisonOperand));
		Write(o, WriteShortLE(e.AlwaysZero));
	}

	private void WriteRow2(MemoryStream o, CampaignOverridePatch82 e) {
		Write(o, WriteShortLESegment(e.Data));
	}

	private void WriteRow3(MemoryStream o, VariantValue8 e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.Unk04));
		Write(o, WriteShortLE(e.Payload));
	}

	private void WriteRow4(MemoryStream o, RewardPackage144 e) {
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLESegment(e.LutRefsA));
		Write(o, WriteShortLESegment(e.LutRefsB));
		Write(o, WriteShortLESegment(e.LutRefsC));
		Write(o, WriteShortLE(e.VariantRef));
	}

	private void WriteRow6(MemoryStream o, MapPoint22 e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.InheritIndex));
		Write(o, WriteShortLE(e.Unk06));
		Write(o, WriteShortLE(e.SumFlag));
		Write(o, WriteIntLE(e.X));
		Write(o, WriteIntLE(e.Y));
		Write(o, WriteIntLE(e.Z));
	}

	private void WriteRow7(MemoryStream o, Flag10 e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.InheritIndex));
		Write(o, WriteShortLE(e.Unk06));
		Write(o, WriteShortLE(e.Payload));
	}

	private void WriteRow8(MemoryStream o, WaypointGroup e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.InheritIndex));
		Write(o, WriteShortLE(e.Unk06));
		Write(o, WriteShortLE((short)e.Waypoints.Length));
		Write(o, WriteShortLESegment(e.Waypoints));
	}

	private void WriteRow9(MemoryStream o, LinkOrReward12 e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.Unk04));
		Write(o, WriteShortLE(e.TypeFlag));
		Write(o, WriteShortLE(e.RefA));
		Write(o, WriteShortLE(e.RefBOrLiteral));
	}

	private void WriteRow10(MemoryStream o, Action82 e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.Unk04));
		Write(o, WriteShortLE(e.Type));
		Write(o, WriteShortLE(e.Verb));
		Write(o, WriteShortLESegment(e.RefsRow9));
		Write(o, WriteShortLESegment(e.ConstantSpan));
		Write(o, WriteShortLESegment(e.LutRefs));
		Write(o, WriteShortLE(e.SecondaryValue));
		Write(o, WriteShortLE(e.Target));
	}

	private void WriteRow11(MemoryStream o, ActionPair30 e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.Unk04));
		Write(o, WriteShortLE(e.PrimaryActionRef));
		Write(o, WriteShortLE(e.TimerValue));
		Write(o, WriteShortLESegment(e.SequenceRefs));
	}

	private void WriteRow12(MemoryStream o, SpawnRecord144 e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.InheritIndex));
		Write(o, WriteShortLE(e.CompoundConditionPartner));
		Write(o, WriteShortLE(e.BinaryFlag));
		Write(o, WriteShortLE(e.NearConstant));
		Write(o, WriteShortLESegment(e.DeadZone));
		Write(o, WriteShortLE(e.SmallDiscrete));
		Write(o, WriteShortLESegment(e.UnresolvedRefs));
		Write(o, WriteShortLE(e.RefRow6));
		Write(o, WriteShortLE(e.RefRow7));
		Write(o, WriteShortLE(e.SmallDiscrete2));
		Write(o, WriteShortLESegment(e.PairedRefs));
		Write(o, WriteShortLESegment(e.AlwaysPopulatedBlock));
		Write(o, WriteShortLE(e.Constant5));
		Write(o, WriteShortLE(e.Constant2));
		Write(o, WriteShortLE(e.RefRow10Slot1));
		Write(o, WriteShortLE(e.RefRow10Slot2));
		Write(o, WriteShortLE(e.TrailingField));
	}

	private void WriteRow13(MemoryStream o, UnkEntity102Bytes e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.InheritIndex));
		Write(o, WriteShortLE(e.Unk06));
		Write(o, WriteShortLESegment(e.FlagsA));
		Write(o, WriteShortLE(e.RefRow6));
		Write(o, WriteShortLE(e.RefRow7));
		Write(o, WriteShortLE(e.BinaryField));
		Write(o, WriteShortLE(e.Unk36));
		Write(o, WriteShortLESegment(e.FlagsB));
		Write(o, WriteShortLE(e.RefRow10Slot1));
		Write(o, WriteShortLE(e.RefRow10Slot2));
		Write(o, WriteShortLE(e.UnkVal_100));
	}

	private void WriteRow14(MemoryStream o, MiscEntityInfo e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.InheritIndex));
		Write(o, WriteShortLE(e.Unk06));
		Write(o, WriteShortLE(e.TypeLikeScalar));
		Write(o, WriteShortLE(e.RefRow6));
		Write(o, WriteShortLE(e.RefRow7));
		Write(o, WriteShortLE(e.SmallDiscrete));
		Write(o, WriteShortLESegment(e.SparseBlock));
		Write(o, WriteShortLE(e.RefRow10Slot1));
		Write(o, WriteShortLE(e.RefRow10Slot2));
		Write(o, WriteShortLE(e.TrailingField));
	}

	private void WriteRow15(MemoryStream o, LinkedRef22 e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.InheritIndex));
		Write(o, WriteShortLE(e.CompoundConditionPartner));
		Write(o, WriteShortLE(e.SmallInt1));
		Write(o, WriteShortLE(e.SmallInt2));
		Write(o, WriteShortLE(e.RefRow6));
		Write(o, WriteShortLE(e.RefRow8));
		Write(o, WriteShortLE(e.DiscriminatorType));
		Write(o, WriteShortLE(e.DiscriminatedRef));
		Write(o, WriteShortLE(e.RefRow10));
	}

	private void WriteRow16(MemoryStream o, UnkEntity164Bytes e) {
		Write(o, WriteShortLE(e.GUID));
		Write(o, WriteShortLE(e.ConditionRef));
		Write(o, WriteShortLE(e.CompoundConditionPartner));
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
		Write(o, WriteShortLE(e.TrailingDiscriminator));
		Write(o, WriteShortLE(e.Payload1));
		Write(o, WriteShortLE(e.Payload2));
		Write(o, WriteShortLE(e.Payload3));
		Write(o, WriteShortLE(e.Payload4));
		Write(o, WriteShortLESegment(e.DeadZone2));
		Write(o, WriteShortLE(e.TrailingFlag));
	}

	private void WriteRow17(MemoryStream outStream, MissionFile data) {
		var entries = data.LinkedRefs58!;
		Write(outStream, WriteShortLE((short)entries.Length));

		foreach (var e in entries) {
			if (e == null) {
				// DEMO2.MSN-style truncation: write back whatever raw tail bytes were preserved on
				// read, instead of a full 58-byte record that never existed in the source file.
				if (data.TruncatedRow17Tail != null) {
					Write(outStream, data.TruncatedRow17Tail);
				}
				continue;
			}

			Write(outStream, WriteShortLE(e.ConditionRef));
			Write(outStream, WriteShortLE(e.Unk02));
			Write(outStream, WriteShortLE(e.Unk04));
			Write(outStream, WriteShortLE(e.Discriminator));
			Write(outStream, WriteShortLE(e.DiscriminatedRef));
			Write(outStream, WriteShortLE(e.RefRow6));
			Write(outStream, WriteShortLE(e.RefRow8));
			Write(outStream, WriteShortLE(e.LutRef));
			Write(outStream, WriteShortLE(e.PairCount));

			foreach (var pair in e.Pairs) {
				Write(outStream, WriteShortLE(pair.Ref));
				Write(outStream, WriteShortLE(pair.Tag));
			}
		}
	}

	private static void Write(MemoryStream outArr, byte[] data) {
		outArr.Write(data, 0, data.Length);
	}
}
