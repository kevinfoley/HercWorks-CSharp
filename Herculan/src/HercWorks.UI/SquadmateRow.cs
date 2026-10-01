using HercWorks.Core.Data.Struct.Vshell.Sav;

namespace HercWorks.UI;

/// <summary>
/// Editable grid-row shape for one PilotEntry — used both for the 36 Squadmates and, as a single
/// extra row, the Player Pilot (distinguished by IsPlayer/Role, since PilotEntry itself doesn't
/// carry that distinction — PlayerSaveTransform tracks it only by which slot it was read from).
/// Every field applies to both: the player's record is byte-identical in shape to a squadmate's,
/// roster id included.
///
/// <para>Skill and Rank are two separate 0-3 ladders in the file, so they get a column each.</para>
/// </summary>
public class SquadmateRow {
	public bool IsPlayer { get; set; }
	public string Role => IsPlayer ? "Player" : "Squadmate";
	public short SquadmateId { get; set; }
	public short NameIndex { get; set; }
	public string Name { get; set; } = string.Empty;
	public short BayId { get; set; }
	public byte Active { get; set; }
	public string SkillLabel { get; set; } = PilotSkill.Rookie.Label;
	public short CrewRowNum { get; set; }
	public string RankLabel { get; set; } = PilotRank.Lieutenant.Label;
	public short ProbablyHealth { get; set; }
	public short KillsHercs { get; set; }
	public short KillsFlyers { get; set; }
	public short KillsBuilding { get; set; }
	public short TotalKillHerc { get; set; }
	public short TotalKillFlyer { get; set; }
	public short TotalKillBldng { get; set; }
	public short MissionCount { get; set; }

	public static SquadmateRow FromEntry(PilotEntry entry, bool isPlayer) => new() {
		IsPlayer = isPlayer,
		SquadmateId = entry.RosterId,
		NameIndex = entry.NameIndex,
		Name = entry.Name ?? string.Empty,
		BayId = entry.Bay,
		Active = entry.OnStrength,
		SkillLabel = entry.Skill?.Label ?? PilotSkill.Rookie.Label,
		CrewRowNum = entry.SquadPosition,
		RankLabel = entry.Rank?.Label ?? PilotRank.Lieutenant.Label,
		ProbablyHealth = entry.Condition,
		KillsHercs = entry.HercKills,
		KillsFlyers = entry.FlyerKills,
		KillsBuilding = entry.BaseKills,
		TotalKillHerc = entry.TotalHercKills,
		TotalKillFlyer = entry.TotalFlyerKills,
		TotalKillBldng = entry.TotalBaseKills,
		MissionCount = entry.MissionsFlown
	};

	public void ApplyTo(PilotEntry entry) {
		entry.RosterId = SquadmateId;
		entry.NameIndex = NameIndex;
		entry.Name = Name;
		entry.Bay = BayId;
		entry.OnStrength = Active;
		entry.Skill = PilotSkill.GetByName(SkillLabel);
		entry.SquadPosition = CrewRowNum;
		entry.Rank = PilotRank.GetByName(RankLabel);
		entry.Condition = ProbablyHealth;
		entry.HercKills = KillsHercs;
		entry.FlyerKills = KillsFlyers;
		entry.BaseKills = KillsBuilding;
		entry.TotalHercKills = TotalKillHerc;
		entry.TotalFlyerKills = TotalKillFlyer;
		entry.TotalBaseKills = TotalKillBldng;
		entry.MissionsFlown = MissionCount;
	}
}
