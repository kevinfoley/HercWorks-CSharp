using HercWorks.Core.Data.File.Msn;
using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>Ported from org.hercworks.core.io.transform.common.MissionFileTransformer.</summary>
public class MissionFileTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO (carried over from Java): null input
			return null;
		}

		var data = new MissionFile {
			RawBytes = inputArray,
			Ext = FileType.Msn,
			Dir = FileType.Msn,
			MarkedObjects = new List<MapObject>()
		};

		SetBytes(inputArray);

		data.UnknownFileId = IndexShortLE();

		var headerEntries = new UnkHeaderEntry[IndexShortLE()];
		for (int i = 0; i < headerEntries.Length; i++) {
			headerEntries[i] = new UnkHeaderEntry(IndexShortLE(), IndexShortLE(), IndexShortLE(),
				IndexShortLE(), IndexShortLE(), IndexShortLE(), IndexShortLE());
			Console.WriteLine(headerEntries[i].ToString());
		}
		data.HeaderEntries = headerEntries;

		data.UnkVal_is2 = IndexShortLE();
		data.UnkFlag_isFFFF = IndexShortLE();
		data.WorldId = IndexShortLE();
		data.ZoneNumber = IndexShortLE();

		// XXX (carried over from Java): hard-coded test for TRAIN5.MSN, this block is variable
		// length in scripts, and the size accounting isn't known yet.
		data.UnkSeg1 = IndexShortLEArray(189); // C1_01
		Console.WriteLine($"\tunk chunk shorts: @{Index - 378}, [{string.Join(", ", data.UnkSeg1)}]");

		data.UnkSTRFileLinks = IndexShortLEArray(13);

		data.Unknown4ByteOr2ByteVals = IndexShortLEArray(20);

		// map coords - total count of 'static map coords', pre-compiled in the script, probably
		// for fast reference.
		var coords = new MapCoord[IndexShortLE()];
		for (int m = 0; m < coords.Length; m++) {
			coords[m] = new MapCoord(IndexShortLE(), IndexShortLE(), IndexShortLE(), IndexShortLE(),
				IndexShortLE(), IndexIntLE(), IndexIntLE(), IndexIntLE());
		}
		data.MapCoordHeader = coords;

		var unk10s = new UnkEntity10Byte[IndexShortLE()];
		for (int u = 0; u < unk10s.Length; u++) {
			var ent = new UnkEntity10Byte {
				GUID = IndexShortLE(),
				Values = IndexShortLEArray(4)
			};

			unk10s[u] = ent;
			data.MarkedObjects.Add(unk10s[u]);
		}
		data.Unk10ByteEnts = unk10s;

		var unk16s = new UnkEntity16Byte[IndexShortLE()];
		for (int u = 0; u < unk16s.Length; u++) {
			var ent = new UnkEntity16Byte {
				GUID = IndexShortLE(),
				Values = IndexShortLEArray(7)
			};

			unk16s[u] = ent;
			data.MarkedObjects.Add(unk16s[u]);
		}
		data.Unk16ByteEnts = unk16s;

		Console.WriteLine($"skipping unknown segment @{Index} 306 bytes"); // TRAIN5
		data.UnkVariableLengthSeg = IndexShortLEArray(393);

		// -------------------------------------------------------------------
		var units = new UnitInfo[IndexShortLE()];
		for (int u = 0; u < units.Length; u++) {
			units[u] = ParseUnitInfo();
			if (units[u].GUID != -1) {
				data.MarkedObjects.Add(units[u]);
			}
		}
		data.MapUnits = units;

		// -------------------------------------------------------------------
		var unkEntity102s = new UnkEntity102Bytes[IndexShortLE()];
		for (int o = 0; o < unkEntity102s.Length; o++) {
			unkEntity102s[o] = ParseUnk102();

			if (unkEntity102s[o].GUID != -1) {
				data.MarkedObjects.Add(unkEntity102s[o]);
			}
		}
		data.Unk102ByteEnts = unkEntity102s;

		// -------------------------------------------------------------------
		var miscEnts = new MiscEntityInfo[IndexShortLE()];
		for (int m = 0; m < miscEnts.Length; m++) {
			miscEnts[m] = ParseMiscEntity();

			if (miscEnts[m].GUID != -1) {
				data.MarkedObjects.Add(miscEnts[m]);
			}
		}
		data.MapMiscEntities = miscEnts;

		// -------------------------------------------------------------------
		var unkEntity22s = new UnkEntity22Byte[IndexShortLE()];
		for (int e = 0; e < unkEntity22s.Length; e++) {
			unkEntity22s[e] = ParseUnkEntity22();

			if (unkEntity22s[e].GUID != -1) {
				data.MarkedObjects.Add(unkEntity22s[e]);
			}
		}
		data.Unk22ByteEnts = unkEntity22s;

		// -------------------------------------------------------------------
		var unkEntity164s = new UnkEntity164Bytes[IndexShortLE()];
		for (int e = 0; e < unkEntity164s.Length; e++) {
			unkEntity164s[e] = ParseUnkEntity164Bytes(data);
			if (unkEntity164s[e].GUID != -1) {
				data.MarkedObjects.Add(unkEntity164s[e]);
			}
		}
		data.Unk164ByteEnts = unkEntity164s;

		// -------------------------------------------------------------------
		var unkEntity58s = new UnkEntity58Byte[IndexShortLE()];
		for (int e = 0; e < unkEntity58s.Length; e++) {
			unkEntity58s[e] = ParseUnkEntity58Byte();
		}
		data.Unk58ByteEnts = unkEntity58s;

		return data;
	}

	private UnitInfo ParseUnitInfo() {
		var unit = new UnitInfo {
			GUID = IndexShortLE(),
			MapCoordId = IndexShortLE()
		};

		for (int i = 0; i < 22; i++) {
			unit.HeaderFlags[i] = IndexShortLE();
		}

		short unitId = IndexShortLE();
		unit.UnitId = unitId == -1 ? null : HercLUT.GetById(unitId);

		for (int i = 0; i < 10; i++) {
			unit.Weapons[i] = IndexShortLE();
		}

		for (int i = 0; i < 36; i++) {
			unit.UnkFlags[i] = IndexShortLE();
		}

		unit.HealthModAdjust = IndexShortLE();

		return unit;
	}

	private UnkEntity102Bytes ParseUnk102() {
		var unk = new UnkEntity102Bytes {
			GUID = IndexShortLE(),
			Flags = IndexShortLEArray(49),
			UnkVal_100 = IndexShortLE()
		};

		return unk;
	}

	private MiscEntityInfo ParseMiscEntity() {
		var misc = new MiscEntityInfo {
			GUID = IndexShortLE(),
			HeaderFlags = IndexShortLEArray(3)
		};

		short miscId = IndexShortLE();
		misc.MiscEntityId = miscId == -1 ? null : MiscEntityLUT.GetById(miscId);

		misc.Spawnflags = IndexShortLEArray(25);
		misc.HealthModAdjust = IndexShortLE();

		return misc;
	}

	private UnkEntity22Byte ParseUnkEntity22() {
		var ent = new UnkEntity22Byte {
			GUID = IndexShortLE(),
			Flags = IndexShortLEArray(10)
		};

		return ent;
	}

	private UnkEntity164Bytes ParseUnkEntity164Bytes(MissionFile msn) {
		var unk = new UnkEntity164Bytes {
			GUID = IndexShortLE(),
			Flags = IndexShortLEArray(22),
			LayoutType = IndexShortLE(),
			LayoutId = IndexShortLE(),
			MapCoord = msn.GetMapCoordById(IndexShortLE()),
			Unk10ByteId = IndexShortLE(),
			Unk16ByteId = IndexShortLE()
		};

		short[] guids = IndexShortLEArray(20);
		var obs = new MapObject[20];
		for (int i = 0; i < guids.Length; i++) {
			obs[i] = msn.FindMarkedObjectById(guids[i])!;
		}

		unk.MapEntities = obs;
		unk.MapEntIds = guids;

		unk.Unk22ByteId = IndexShortLE();

		unk.Values = IndexShortLEArray(33);

		return unk;
	}

	private UnkEntity58Byte ParseUnkEntity58Byte() {
		var unk = new UnkEntity58Byte {
			GUID = IndexShortLE(),
			Flags = IndexShortLEArray(28)
		};

		return unk;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		using var outStream = new MemoryStream();

		var data = (MissionFile)source!;

		Write(outStream, WriteShortLE(5));

		Write(outStream, WriteShortLE((short)data.HeaderEntries!.Length));
		foreach (var hdr in data.HeaderEntries) {
			Write(outStream, WriteShortLE(hdr.IndexId));
			Write(outStream, WriteShortLE(hdr.StartFrameIndexId));
			Write(outStream, WriteShortLE(hdr.UnkValue1));
			Write(outStream, WriteShortLE(hdr.FrameStartTime));
			Write(outStream, WriteShortLE(hdr.FrameEndTime));
			Write(outStream, WriteShortLE(hdr.TotalTime));
			Write(outStream, WriteShortLE(hdr.UnkValue2));
		}

		Write(outStream, WriteShortLE(data.UnkVal_is2));
		Write(outStream, WriteShortLE(data.UnkFlag_isFFFF));
		Write(outStream, WriteShortLE(data.WorldId));
		Write(outStream, WriteShortLE(data.ZoneNumber));

		Write(outStream, WriteShortLESegment(data.UnkSeg1!));
		Write(outStream, WriteShortLESegment(data.UnkSTRFileLinks!));
		Write(outStream, WriteShortLESegment(data.Unknown4ByteOr2ByteVals!));

		Write(outStream, WriteShortLE((short)data.MapCoordHeader!.Length));
		foreach (var mp in data.MapCoordHeader) {
			Write(outStream, WriteShortLE(mp.Id));
			Write(outStream, WriteShortLE(mp.UnkFlag1));
			Write(outStream, WriteShortLE(mp.UnkFlag2));
			Write(outStream, WriteShortLE(mp.UnkFlag3));
			Write(outStream, WriteShortLE(mp.UnkFlag4));
			Write(outStream, WriteIntLE(mp.X));
			Write(outStream, WriteIntLE(mp.Y));
			Write(outStream, WriteIntLE(mp.Z));
		}

		Write(outStream, WriteShortLE((short)data.Unk10ByteEnts!.Length));
		foreach (var ent in data.Unk10ByteEnts) {
			Write(outStream, WriteShortLE(ent.GUID));
			Write(outStream, WriteShortLESegment(ent.Values!));
		}

		Write(outStream, WriteShortLE((short)data.Unk16ByteEnts!.Length));
		foreach (var ent in data.Unk16ByteEnts) {
			Write(outStream, WriteShortLE(ent.GUID));
			Write(outStream, WriteShortLESegment(ent.Values!));
		}

		Write(outStream, WriteShortLESegment(data.UnkVariableLengthSeg!));

		Write(outStream, WriteShortLE((short)data.MapUnits!.Length));
		foreach (var unit in data.MapUnits) {
			Write(outStream, WriteShortLE(unit.GUID));
			Write(outStream, WriteShortLE(unit.MapCoordId));
			Write(outStream, WriteShortLESegment(unit.HeaderFlags));
			Write(outStream, WriteShortLE(unit.UnitId == null ? (short)-1 : unit.UnitId.Id));
			Write(outStream, WriteShortLESegment(unit.Weapons));
			Write(outStream, WriteShortLESegment(unit.UnkFlags));
			Write(outStream, WriteShortLE(unit.HealthModAdjust));
		}

		Write(outStream, WriteShortLE((short)data.Unk102ByteEnts!.Length));
		foreach (var ent in data.Unk102ByteEnts) {
			Write(outStream, WriteShortLE(ent.GUID));
			Write(outStream, WriteShortLESegment(ent.Flags));
			Write(outStream, WriteShortLE(ent.UnkVal_100));
		}

		Write(outStream, WriteShortLE((short)data.MapMiscEntities!.Length));
		foreach (var ent in data.MapMiscEntities) {
			Write(outStream, WriteShortLE(ent.GUID));
			Write(outStream, WriteShortLESegment(ent.HeaderFlags));
			Write(outStream, WriteShortLE(ent.MiscEntityId == null ? (short)-1 : ent.MiscEntityId.Id));
			Write(outStream, WriteShortLESegment(ent.Spawnflags));
			Write(outStream, WriteShortLE(ent.HealthModAdjust));
		}

		Write(outStream, WriteShortLE((short)data.Unk22ByteEnts!.Length));
		foreach (var ent in data.Unk22ByteEnts) {
			Write(outStream, WriteShortLE(ent.GUID));
			Write(outStream, WriteShortLESegment(ent.Flags));
		}

		Write(outStream, WriteShortLE((short)data.Unk164ByteEnts!.Length));
		foreach (var ent in data.Unk164ByteEnts) {
			Write(outStream, WriteShortLE(ent.GUID));
			Write(outStream, WriteShortLESegment(ent.Flags!));
			Write(outStream, WriteShortLE(ent.LayoutType));
			Write(outStream, WriteShortLE(ent.LayoutId));
			Write(outStream, WriteShortLE(ent.MapCoord == null ? (short)-1 : ent.MapCoord.Id));
			Write(outStream, WriteShortLE(ent.UnkEntity10Byte == null ? (short)-1 : ent.UnkEntity10Byte.GUID));
			Write(outStream, WriteShortLE(ent.UnkEntity16Byte == null ? (short)-1 : ent.UnkEntity16Byte.GUID));

			Write(outStream, WriteShortLESegment(ent.MapEntIds!));

			Write(outStream, WriteShortLE(ent.UnkEntity22Byte == null ? (short)-1 : ent.UnkEntity22Byte.GUID));

			Write(outStream, WriteShortLESegment(ent.Values!));
		}

		Write(outStream, WriteShortLE((short)data.Unk58ByteEnts!.Length));
		foreach (var ent in data.Unk58ByteEnts) {
			Write(outStream, WriteShortLE(ent.GUID));
			Write(outStream, WriteShortLESegment(ent.Flags));
		}

		return outStream.ToArray();
	}

	private static void Write(MemoryStream outArr, byte[] data) {
		outArr.Write(data, 0, data.Length);
	}
}
