using HercWorks.Core.Io.Transform.Common;
using Herculan.Engine.Content;
using Herculan.Engine.World;

namespace Herculan.Engine.Shell;

/// <summary>
/// The career block's text fields as <c>Career_SetBriefing</c> (<c>00412ece</c>) fills them from a loaded
/// mission: the objective, briefing and intelligence <c>mission.str</c> lines, <c>-1</c> for an empty slot,
/// and the briefing movie id. See docs/formats/save-games.md#career-block--152-bytes.
/// </summary>
public sealed record ShellCareerBriefing(short[] Objectives, short[] Briefing, short[] Intelligence, short BriefingMovie) {
	/// <summary>The words of row 4's slot 0, as <see cref="MissionGenerator.TextPackage"/> gives them.</summary>
	public static ShellCareerBriefing From(short[] package) =>
		new(package[1..11], package[11..41], package[41..71], package[71]);
}

/// <summary>What a campaign mission load wrote, and what it hands back to the career.</summary>
public sealed record ShellCampaignMission(string ScriptPath, string MissionPath, ShellCareerBriefing? Briefing, int SquadPositions);

/// <summary>
/// The campaign half of <c>MsnGen_LoadMission</c> (<c>0041c73d</c>), the load <c>Career_LoadCurrentMission</c>
/// (<c>0044d4cc</c>) makes for the career position: the flags seeded with the position, the mission loaded
/// (<see cref="MissionGenerator"/>), the header's theater, cheat and difficulty fields set, <c>script.dat</c>
/// and <c>mission.str</c> written, and the career block's text taken from row 4. Unlike the training half
/// (<see cref="ShellTrainingLaunch"/>) it builds no machines: a campaign flies the hangar it has, with its
/// squad positions in play set from the mission's group 0. See docs/shell/campaign-loop.md and
/// docs/formats/script-dat.md#the-training-fields.
/// </summary>
public static class ShellCampaignLaunch {
	/// <summary>
	/// Loads the mission at career position (<paramref name="stage"/>, <paramref name="mission"/>) and writes
	/// its <c>script.dat</c> and <c>mission.str</c> into <paramref name="directory"/>, or returns null with the
	/// reason when the install lacks a file the original would open. <paramref name="flags"/> is the career's
	/// campaign flag array, which the load seeds and the mission's header patch clears in place;
	/// <paramref name="clearList"/> is the row-2 clear list the shell keeps across loads;
	/// <paramref name="skill"/> is the player pilot's skill, the mission's difficulty. <paramref name="roll"/> is
	/// VSHELL's <c>ShellRandom_Below</c> (<c>004659ec</c>), a draw in <c>[0, n)</c>.
	/// </summary>
	public static ShellCampaignMission? Write(string directory, GameContent content, int stage, int mission, short skill,
			short[] flags, short[] clearList, Func<short, int> roll, out string? failure) {
		if (ShellTrainingLaunch.CareerStages(content) is not { } stages || stage < 0 || stage >= stages.Count
				|| ShellTrainingLaunch.MissionPath(content, stage, mission) is not { } missionPath) {
			failure = $"gam\\career.dat or missions.bin has no stage {stage} mission {mission}.";
			return null;
		}

		if (ShellTrainingLaunch.ReadMission(content, missionPath) is not (byte[] msn, var text)) {
			failure = $"{missionPath} is not in any mounted archive.";
			return null;
		}

		// MsnGen_SeedCampaignFlags: a campaign load writes the position into flags 1 and 2.
		flags[1] = (short)stage;
		flags[2] = (short)mission;
		ShellTrainingLaunch.SeedDrawnFlags(flags, roll);

		var loaded = MissionGenerator.Load(msn, text, flags, clearList, roll);
		var header = loaded.Header;
		header[5] = 0;
		header[6] = 0;
		header[7] = skill;
		header[0] = stages[stage].CampaignIndex;

		Directory.CreateDirectory(directory);
		string scriptPath = Path.Combine(directory, MissionLoader.ScriptFileName);
		File.WriteAllBytes(scriptPath, loaded.WriteScriptDat());
		File.WriteAllBytes(Path.Combine(directory, MissionLoader.TextFileName), loaded.WriteMissionText());

		// The original asserts row 4 is there before it copies slot 0 into the career block.
		var briefing = loaded.TextPackage is { } package ? ShellCareerBriefing.From(package) : null;

		failure = null;
		return new ShellCampaignMission(scriptPath, missionPath, briefing, loaded.SquadPositions);
	}

	/// <summary>
	/// The first career position whose <c>missions.bin</c> name has <paramref name="name"/>'s file name — a
	/// bare <c>C1_03</c>, <c>C1_03.MSN</c> or <c>MSN\C1_03.MSN</c> — or null when <c>gam\career.dat</c> has
	/// none. The original only ever loads a mission by position; looking one up by name is this engine's.
	/// </summary>
	public static (int Stage, int Mission)? Find(GameContent content, string name) {
		if (ShellTrainingLaunch.CareerStages(content) is not { } stages || ShellText.Load(content, "MISSIONS.BIN") is not { } names) {
			return null;
		}

		string wanted = Stem(name);
		for (int stage = 0; stage < stages.Count; stage++) {
			for (int mission = 0; mission < stages[stage].Missions.Length; mission++) {
				if (names.Text(stages[stage].Missions[mission]) is { } path
						&& string.Equals(Stem(path), wanted, StringComparison.OrdinalIgnoreCase)) {
					return (stage, mission);
				}
			}
		}

		return null;
	}

	private static string Stem(string path) {
		string name = path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
		int dot = name.IndexOf('.');
		return dot < 0 ? name : name[..dot];
	}
}
