using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// FILE - /ZONES.VOL/MSN/MSN_FILE.MSN — root file for all data for a mission in Earthsiege 2.
/// The script.dat file in /DATA/ seems to be a parsed or excised snippet of the MSN file.
///
/// Discovered so far: zone heightmap file id, world id (sector, then day/night); a global table
/// of map coords (unclear how they're mapped to entities yet); spawn data for hercs, buildings
/// (including misc stuff like ruins).
///
/// What's expected: a file-global object/entry UUID for entities linking to each other; nav
/// point entities (likely more than special map coords); entity definitions for objectives;
/// pointer IDs for strings within .STR files (intel section, objectives section); possible
/// campaign flags or a flag system to track campaign events (check/compare blocks — e.g. the
/// mission that unlocks the Raptor II, presumably one of 10 vehicle-unlock slots and one of ~33
/// weapon-unlock slots); trigger logic and callbacks (probably not as concise as Doom's or
/// Quake's, but something close).
/// Ported from org.hercworks.core.data.file.msn.MissionFile.
/// </summary>
public class MissionFile : DataFile {
	public short UnknownFileId { get; set; }
	public UnkHeaderEntry[]? HeaderEntries { get; set; }

	public short UnkVal_is2 { get; set; }
	public short UnkFlag_isFFFF { get; set; }
	public short WorldId { get; set; }
	public short ZoneNumber { get; set; }

	public short[]? UnkSeg1 { get; set; }
	public short[]? UnkSTRFileLinks { get; set; }
	public short[]? Unknown4ByteOr2ByteVals { get; set; }

	public MapCoord[]? MapCoordHeader { get; set; }

	public UnkEntity10Byte[]? Unk10ByteEnts { get; set; }
	public UnkEntity16Byte[]? Unk16ByteEnts { get; set; }

	public List<MapObject>? MarkedObjects { get; set; }

	// Some unknown, variable-length block of data.
	public short[]? UnkVariableLengthSeg { get; set; }

	public UnitInfo[]? MapUnits { get; set; }
	public UnkEntity102Bytes[]? Unk102ByteEnts { get; set; }
	public MiscEntityInfo[]? MapMiscEntities { get; set; }
	public UnkEntity22Byte[]? Unk22ByteEnts { get; set; }
	public UnkEntity164Bytes[]? Unk164ByteEnts { get; set; }
	public UnkEntity58Byte[]? Unk58ByteEnts { get; set; }

	public MapObject? FindMarkedObjectById(short guid) =>
		MarkedObjects?.FirstOrDefault(t => t.GUID == guid);

	public MapCoord? GetMapCoordById(short id) {
		if (id == -1) {
			return null;
		}
		return MapCoordHeader?[id];
	}
}
