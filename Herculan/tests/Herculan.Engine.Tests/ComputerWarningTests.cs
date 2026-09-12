using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Core.Io.Transform.Dbsim;
using Herculan.Engine.Audio;
using Herculan.Engine.Content;
using Herculan.Engine.Numerics;
using Herculan.Engine.Sim;
using Herculan.Engine.Terrain;
using Xunit;

namespace Herculan.Engine.Tests;

/// <summary>
/// The cockpit computer's damage warnings — that each one is said once and that only the machine the
/// player is flying says anything at all. Both are properties of the guards rather than of the
/// damage, so they are checked by driving real damage into a real chassis and reading the message
/// port's input, not by reproducing the guards here.
///
/// <para>The latches these rest on are not the warnings' own bookkeeping: <c>+0xa8</c>/<c>+0xa9</c>
/// are also the two speed penalties and <c>+0xaa</c>/<c>+0xab</c> the reactor's output grades, so a
/// warning that repeated would mean one of those had come unlatched. See
/// docs/simulation/damage-system.md.</para>
///
/// <para>Every test skips silently when no Earthsiege 2 install can be found.</para>
/// </summary>
[Collection(SimTimestepCollection.Name)]
public class ComputerWarningTests {
	/// <summary>
	/// The warnings whose one-shot is a latch byte rather than a difference across one write: the two
	/// leg grades and the reactor. The shield-generator pair and <c>WEAPON DESTROYED</c> are excluded
	/// deliberately — they are differences, and <c>WEAPON DESTROYED</c> is *supposed* to be said again
	/// for a second hardpoint lost in a later write.
	/// </summary>
	private static readonly int[] LatchedWarnings = {
		SystemMessages.InternalDamageLegServos,
		SystemMessages.StructuralFailureImminent,
		SystemMessages.InternalDamageEngine,
	};

	/// <summary>
	/// A machine shelled until it comes apart says each latched warning exactly once, however many
	/// hits land in the same band.
	/// </summary>
	[Fact]
	public void EachLatchedWarningIsSaidOnce() {
		if (Shell(player: true) is not { } log) {
			return;
		}

		// The positive control: this is worthless as a "said once" check if the run never reached the
		// bands at all.
		Assert.NotEmpty(log.Said);

		foreach (int id in LatchedWarnings) {
			Assert.True(1 == log.Said.Count(said => said == id), $"id 0x{id:x} — log [{string.Join(",", log.Said.Select(v => $"0x{v:x}"))}]");
		}
	}

	/// <summary>
	/// And an AI machine says nothing, because every one of these posts is gated on
	/// <c>SimObject.LocallyPiloted</c>. The player's own run above is this one's positive control:
	/// the same shelling on the same chassis is what fills the log when the machine is flown.
	/// </summary>
	[Fact]
	public void AnAiMachineSaysNothing() {
		if (Shell(player: false) is not { } log) {
			return;
		}

		Assert.Empty(log.Said);
	}

	/// <summary>
	/// <c>ENEMY TARGET DESTROYED</c> needs both halves of its predicate: the player's own machine
	/// landed the shot, and the thing that died is what the player had selected. Killing something
	/// the player was not boxing is silent.
	/// </summary>
	[Fact]
	public void TheKillAnnouncementNeedsTheVictimToBeTheSelectedTarget() {
		if (Content() is not { } content
				|| Spawn(content, "OUTLAW") is not { } player
				|| Spawn(content, "OUTLAW") is not { } victim) {
			return;
		}

		player.IsPlayer = true;

		var log = new MessageLog();
		var world = FlatWorld(log, player, victim);

		Shell(world, victim, player);
		Assert.True(victim.Destroyed, "the victim should have been destroyed");
		Assert.DoesNotContain(SystemMessages.EnemyTargetDestroyed, log.Said);

		// Again, with the second machine boxed this time.
		if (Spawn(content, "OUTLAW") is not { } selected) {
			return;
		}

		world.Add(selected);
		selected.Position = victim.Position;
		player.Target = selected;
		log.Said.Clear();

		Shell(world, selected, player);
		Assert.True(selected.Destroyed, "the selected machine should have been destroyed");
		Assert.Equal(1, log.Said.Count(said => said == SystemMessages.EnemyTargetDestroyed));
	}

	/// <summary>
	/// Walks one machine from pristine to wrecked with repeated blasts on its own position, and hands
	/// back everything the computer said on the way. Null when there is no install to read.
	/// </summary>
	private static MessageLog? Shell(bool player) {
		if (Content() is not { } content || Spawn(content, "OUTLAW") is not { } mech) {
			return null;
		}

		mech.IsPlayer = player;

		var log = new MessageLog();
		Shell(FlatWorld(log, mech), mech, attacker: null);
		return log;
	}

	/// <summary>
	/// Small blasts at the machine's own feet until it is destroyed. Each one rolls per component, so
	/// this is many partial writes rather than one flattening hit — which is the point: the bands are
	/// crossed repeatedly and by different parts.
	/// </summary>
	private static void Shell(SimWorld world, MechObject victim, MechObject? attacker) {
		for (int i = 0; i < BlastCount && !victim.Destroyed; i++) {
			victim.ExplosiveDamage(world, BlastDamage, victim.Position, BlastRadius, attacker);
		}
	}

	/// <summary>
	/// Fixed, so a failure is reproducible — though the run does not depend on it: the blasts are
	/// small enough that every band is crossed in order whatever the draws are.
	/// </summary>
	private const int RandomSeed = 1;

	/// <summary>
	/// Small blasts, and a great many of them. The size is what matters: a heavier shell can take a
	/// side of legs from healthy to destroyed inside one write, skipping the <c>0x50</c> band
	/// entirely, and then <c>INTERNAL DAMAGE: LEG SERVOS</c> is never reached — correctly, but the
	/// test would be checking nothing.
	/// </summary>
	private const short BlastDamage = 200;

	/// <inheritdoc cref="BlastDamage"/>
	private const int BlastCount = 8000;

	/// <summary>Wide enough that the falloff never spares a component; the grind is the point.</summary>
	private const int BlastRadius = 40000;

	/// <summary>Records the message ids posted to the port and ignores every other noise.</summary>
	private sealed class MessageLog : ISoundSink {
		public List<int> Said { get; } = new();

		public void Say(int messageId) => Said.Add(messageId);

		public void Play(int id) { }

		public void PlayAt(int id, Vec3i position) { }

		public void Stop(int id) { }

		public void MoveTo(int id, Vec3i position) { }

		public void SetPitch(int id, int rate) { }

		public void SquadSay(int messageId, object speaker) { }

		public void Unsay(int messageId) => Said.Remove(messageId);
	}

	private static SimWorld FlatWorld(ISoundSink sounds, params MechObject[] mechs) {
		const int widthShift = 8;
		const int cellShift = 12;
		const int cellCount = 1 << (widthShift * 2);

		var heights = new byte[cellCount];
		Array.Fill(heights, (byte)40);

		var terrain = new HeightGrid(widthShift, widthShift, cellShift, 16, 10, heights, new byte[cellCount]);
		var world = new SimWorld(terrain) { Sounds = sounds };

		int middle = (1 << (widthShift + cellShift)) / 2;
		foreach (var mech in mechs) {
			mech.Position = new Vec3i(middle, middle, terrain.HeightAtWorld(middle, middle));
			world.Add(mech);
		}

		return world;
	}

	/// <summary>
	/// A bare chassis with its component health — and nothing else, because nothing else is on any
	/// of these paths. The <c>.DMG</c> is not optional here: without it <c>_damage</c> is null, every
	/// damage entry point returns at its first line, and a test that shells the machine would pass
	/// while proving nothing.
	/// </summary>
	private static MechObject? Spawn(GameContent content, string herc) {
		if (content.Read("dat", herc + ".DAT") is not { } datBytes
				|| new HercSimDataTransformer().Parse(datBytes) is not HercSimDat data
				|| content.Read("dmg", herc + ".DMG") is not { } dmgBytes
				|| new HercDamageFileTransformer().Parse(dmgBytes) is not HercSimDamage damageModel) {
			return null;
		}

		var damage = new ComponentDamage(damageModel, ComponentDamage.MechComponentCount,
			ComponentDamage.MechDependentCount, new SimRandom(RandomSeed));

		return new MechObject(herc, data, 0, MechLoadout.None, damage: damage);
	}

	private static GameContent? Content() {
		string? root = GameInstall.Locate(null);
		return root != null ? GameContent.Mount(GameInstall.ArchiveDirectory(root)) : null;
	}
}
