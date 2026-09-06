using Herculan.Engine.Content;
using Herculan.Engine.Numerics;
using Herculan.Engine.Scene;
using Herculan.Engine.Sim;
using Herculan.Engine.Sim.Ai;
using Herculan.Engine.World;
using Xunit;

namespace Herculan.Engine.Tests;

/// <summary>
/// Walks the player's HERC around a real mission — the whole chain at once: <c>script.dat</c> and
/// <c>player.mec</c> to a scene, the zone's own heightmap under the machine's feet, and the control
/// law driving it over real terrain rather than the flat plate
/// <see cref="MechLocomotionTests"/> uses.
///
/// <para>Skips silently when no Earthsiege 2 install or mission can be found.</para>
/// </summary>
[Collection(SimTimestepCollection.Name)]
public class MissionWalkTests {
	[Fact]
	public void PlayerMechWalksAcrossRealTerrain() {
		if (Load() is not { } scene || scene.PlayerMech is not { Thread: not null } mech) {
			return;
		}

		var start = mech.Position;
		var full = new MechControls(0, -MechControls.AxisFull);

		for (int i = 0; i < 500; i++) {
			mech.Controls = full;
			scene.World.Tick();
		}

		int travelled = SimMath.FastMagnitude2D(
			mech.Position.X - start.X, mech.Position.Y - start.Y);

		// 20 seconds of walking should cover hundreds of metres. The only ways it does not are a
		// collision it cannot back out of or ground too steep to leave, both of which are legitimate
		// but would make this a poor smoke test — so this asserts it moved a long way, not a
		// specific distance.
		Assert.True(travelled > 20000,
			$"{mech.Name} covered only {travelled} world units in 500 ticks");

		// And it must still be standing on the zone, not inside it or floating over it.
		Assert.Equal(scene.World.Terrain.HeightAtWorld(mech.Position.X, mech.Position.Y)
			+ mech.Type.RideHeight, mech.Position.Z);
	}

	/// <summary>
	/// A machine with no pilot is driven by its behaviour state's think, and only the states the
	/// navigation slice ports drive anything — so a mission's AI machines split in two. One under a
	/// movement or guard order walks; one in a state whose think is unported stands where the mission
	/// put it, because nothing calls the control law for it at all.
	/// </summary>
	[Fact]
	public void UnpilotedMachinesMoveOnlyUnderAThinkThatDrivesThem() {
		if (Load() is not { } scene) {
			return;
		}

		var others = scene.Objects
			.Select(o => o.Object)
			.OfType<MechObject>()
			.Where(mech => !mech.IsPlayer && mech.Thread != null)
			.Select(mech => (Mech: mech, Start: mech.Position))
			.ToList();

		if (others.Count == 0) {
			return;
		}

		// Sampled per tick rather than up front: every machine starts in `deciding`, whose think is
		// none, and takes its real state from the reassess on the tick after.
		var driven = new HashSet<MechObject>();

		for (int i = 0; i < 100; i++) {
			scene.World.Tick();

			foreach (var (mech, _) in others) {
				if (mech.Behaviour.State is { Think: not ThinkSlot.None }) {
					driven.Add(mech);
				}
			}
		}

		foreach (var (mech, start) in others.Where(o => !driven.Contains(o.Mech))) {
			Assert.Equal(start.X, mech.Position.X);
			Assert.Equal(start.Y, mech.Position.Y);
		}

		// Not every driven machine has anywhere to be — a guard already on its post and a follower
		// already on station both hold still on purpose — so this asks that the group order layer
		// moved somebody rather than that it moved everybody.
		if (driven.Count != 0) {
			Assert.Contains(others.Where(o => driven.Contains(o.Mech)),
				o => SimMath.FastMagnitude2D(
					o.Mech.Position.X - o.Start.X, o.Mech.Position.Y - o.Start.Y) > 0);
		}
	}

	private static MissionScene? Load() {
		string? root = GameInstall.Locate(null);
		if (root == null) {
			return null;
		}

		string script = MissionLoader.DefaultScriptPath(root);
		if (!File.Exists(script)) {
			return null;
		}

		return MissionScene.Load(GameContent.Mount(GameInstall.ArchiveDirectory(root)), script);
	}
}
