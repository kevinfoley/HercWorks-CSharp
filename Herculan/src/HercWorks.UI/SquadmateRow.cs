using HercWorks.Core.Data.Struct.Vshell.Sav;

namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape for one PilotEntry — used both for the 36 Squadmates and, as a single
/// extra row, the Player Pilot (distinguished by IsPlayer/Role, since PilotEntry itself doesn't
/// carry that distinction — PlayerSaveTransform tracks it only by which write path is called).
/// SquadmateId and Unk5Uint16 don't apply to the player pilot (PlayerSaveTransform.WritePilotData
/// skips both when isPlayer=true) but are still shown/carried through harmlessly for that row.
/// </summary>
public class SquadmateRow {
	public bool IsPlayer { get; set; }
	public string Role => IsPlayer ? "Player" : "Squadmate";
	public short SquadmateId { get; set; }
	public string Name { get; set; } = string.Empty;
	public short BayId { get; set; }
	public byte Active { get; set; }
	public string RankLabel { get; set; } = PilotRank.Rookie.Label;
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

	public static SquadmateRow FromEntry(PilotEntry entry, bool isPlayer) => new() {
		IsPlayer = isPlayer,
		SquadmateId = entry.SquadmateId,
		Name = entry.Name ?? string.Empty,
		BayId = entry.BayId,
		Active = entry.Active,
		RankLabel = entry.Rank?.Label ?? PilotRank.Rookie.Label,
		CrewRowNum = entry.CrewRowNum,
		Unk2Uint16 = entry.Unk2Uint16,
		ProbablyHealth = entry.ProbablyHealth,
		KillsHercs = entry.KillsHercs,
		KillsFlyers = entry.KillsFlyers,
		KillsBuilding = entry.KillsBuilding,
		TotalKillHerc = entry.TotalKillHerc,
		TotalKillFlyer = entry.TotalKillFlyer,
		TotalKillBldng = entry.TotalKillBldng,
		MissionCount = entry.MissionCount,
		Unk5Uint16 = entry.Unk5Uint16
	};

	public void ApplyTo(PilotEntry entry) {
		entry.SquadmateId = SquadmateId;
		entry.Name = Name;
		entry.BayId = BayId;
		entry.Active = Active;
		entry.Rank = PilotRank.GetByName(RankLabel);
		entry.CrewRowNum = CrewRowNum;
		entry.Unk2Uint16 = Unk2Uint16;
		entry.ProbablyHealth = ProbablyHealth;
		entry.KillsHercs = KillsHercs;
		entry.KillsFlyers = KillsFlyers;
		entry.KillsBuilding = KillsBuilding;
		entry.TotalKillHerc = TotalKillHerc;
		entry.TotalKillFlyer = TotalKillFlyer;
		entry.TotalKillBldng = TotalKillBldng;
		entry.MissionCount = MissionCount;
		entry.Unk5Uint16 = Unk5Uint16;
	}
}
