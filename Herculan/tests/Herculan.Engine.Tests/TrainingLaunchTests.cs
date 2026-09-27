using System.Security.Cryptography;
using Herculan.Engine.Content;
using Herculan.Engine.Numerics;
using Herculan.Engine.Shell;
using HercWorks.Core.Io.Transform.Common;
using Xunit;

namespace Herculan.Engine.Tests;

/// <summary>
/// The practice screen's <c>Begin Mission</c> path and the mission load under it, against a real
/// install. Skips silently without one, as the rest of the suite does.
///
/// <para>The retail case is a TRAIN5 handoff the retail shell wrote to save slot 11 —
/// <c>script11.dat</c>, <c>missn11.str</c> and <c>player11.mec</c> — with the generator seeded 19,
/// <c>Herc Type</c> on Colossus, <c>Mission Difficulty</c> on 2 and <c>Time of Day</c> on Night. The
/// capture is game output and stays out of the repository; the test holds only the SHA-256 of each
/// file's content, so it runs from the install alone. The retail files keep stale bytes past their
/// content, since neither writer truncates, so each digest covers the file up to the length this
/// path writes. The match covers the mission load, the order of every random draw, the roster, and
/// the squad build down to the last wingman staying behind. See
/// docs/shell/screen-layout.md#starting-a-practice-mission.</para>
/// </summary>
public class TrainingLaunchTests {
	private const int RetailSeed = 19;
	private const int StrikeTrainingRow = 4;
	private const byte Colossus = 4;
	private const byte Veteran = 2;
	private const byte Night = 1;
	private const int DifficultyOption = 0x27;
	private const int TimeOfDayOption = 0x29;

	private const string RetailScript = "b2112b6c2f618c29d8e5d9965c72a1ca87c6a93bd0c755e2255e05143bcd2103";
	private const string RetailText = "c590290ae60f88cde0757d1978a80bb26c3085430ea59dd9f779aed7c46f496b";
	private const string RetailSquad = "cfe8aff207e51d271d36cffd1bceff9e55041bb7bb2f89608fec8afcb9a0080a";

	[Fact]
	public void ReproducesTheRetailTrainingHandoff() {
		if (GameInstall.Locate(null) is not { } root) {
			return;
		}

		var content = GameContent.Mount(GameInstall.ArchiveDirectory(root), ShellArt.Archives);
		var options = SimulatorPreferences.Defaults();
		options.Set(DifficultyOption, Veteran);
		options.Set(ShellPracticeScreen.HercTypeOption, Colossus);
		options.Set(TimeOfDayOption, Night);
		var random = new SimRandom();
		for (int step = 0; step < RetailSeed; step++) {
			random.Next();
		}

		string directory = Path.Combine(Path.GetTempPath(), "herculan-tests", nameof(ReproducesTheRetailTrainingHandoff));
		var handoff = ShellTrainingLaunch.Write(directory, content, options, StrikeTrainingRow, random,
			new short[MissionGenerator.ClearListLength], out string? failure);

		Assert.True(handoff != null, failure);
		Assert.Equal(@"MSN\TRAIN5.MSN", handoff.MissionPath);

		Assert.Equal(RetailScript, Digest(handoff.ScriptPath));
		Assert.Equal(RetailText, Digest(Path.Combine(directory, "mission.str")));
		Assert.Equal(RetailSquad, Digest(Path.Combine(directory, "player.mec")));

		// Three positions, but the second wingman is never put on strength.
		Assert.Equal(3, handoff.SquadPositions);
		Assert.Equal(2, handoff.Hangar.MachinesOnStrength);
	}

	[Fact]
	public void LoadsEveryRetailMission() {
		if (GameInstall.Locate(null) is not { } root) {
			return;
		}

		var content = GameContent.Mount(GameInstall.ArchiveDirectory(root), ShellArt.Archives);
		var missions = content.ListFolder("MSN").Where(name => name.EndsWith(".MSN", StringComparison.OrdinalIgnoreCase)).ToList();
		if (missions.Count == 0) {
			return;
		}

		foreach (string name in missions) {
			byte[] msn = content.ReadRequired("MSN", name);
			byte[]? text = content.Read("MSN", Path.ChangeExtension(name, ".ENG"));
			var mission = MissionGenerator.Load(msn, text, new short[MissionGenerator.CampaignFlagCount],
				new short[MissionGenerator.ClearListLength], _ => 0);

			byte[] script = mission.WriteScriptDat();
			var transformer = new ScriptDatTransformer();
			var parsed = transformer.Parse(script);
			Assert.True(parsed != null, name);
			Assert.True(script.AsSpan().SequenceEqual(transformer.Write(parsed)), name);
		}
	}

	private static string Digest(string path) =>
		Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
