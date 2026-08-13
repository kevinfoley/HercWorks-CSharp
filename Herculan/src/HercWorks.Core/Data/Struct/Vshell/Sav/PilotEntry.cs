namespace HercWorks.Core.Data.Struct.Vshell.Sav;

/// <summary>
/// Bound to PlayerSave — a chunk of save data dealing just with squadmate state, and player
/// state. 36-byte segments.
///   0 - UINT16 - name length
///   2 - String - null-terminated string
///   0+X - UINT16 - Herc Bay Id assignment - FF FF is 'empty'
///   X+2 - UINT8 - some kind of flag
///   X+3 - UINT16 - Rank - see PilotRank
///   X+5 - UINT16 - Crew row number - FF FF is 'empty'
///   X+7 - UINT16 - unk2_uint16
///   X+9 - UINT16 - unk3_uint16_hp - probably pilot health, which would increase chance of KIA.
/// Ported from org.hercworks.core.data.struct.vshell.sav.PilotEntry.
/// </summary>
public class PilotEntry {
	public short SquadmateId { get; set; }
	public string? Name { get; set; }
	public short BayId { get; set; }
	public byte Active { get; set; }
	public PilotRank? Rank { get; set; }
	public short CrewRowNum { get; set; }
	public short Unk2Uint16 { get; set; }
	public short ProbablyHealth { get; set; }
	public short KillsHercs { get; set; }
	public short KillsFlyers { get; set; }
	public short KillsBuilding { get; set; }
	public short TotalKillHerc { get; set; }
	public short TotalKillFlyer { get; set; }
	public short TotalKillBldng { get; set; }
	public short MissionCount { get; set; }
	public short Unk5Uint16 { get; set; }
}
