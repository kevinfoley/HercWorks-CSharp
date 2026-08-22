using Herculan.Engine.Content;
using Herculan.Engine.Numerics;
using Herculan.Engine.Scene;
using Herculan.Engine.Sim;
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

	[Fact]
	public void OtherMachinesStayPutWithNoPilot() {
		if (Load() is not { } scene) {
			return;
		}

		var others = scene.Objects
			.Where(o => o.Object is MechObject mech && !mech.IsPlayer)
			.Select(o => (Object: (MechObject)o.Object, Start: o.Object.Position))
			.ToList();

		if (others.Count == 0) {
			return;
		}

		for (int i = 0; i < 100; i++) {
			scene.World.Tick();
		}

		foreach (var (mech, position) in others) {
			Assert.Equal(position.X, mech.Position.X);
			Assert.Equal(position.Y, mech.Position.Y);
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
