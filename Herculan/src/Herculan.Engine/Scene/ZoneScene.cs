using System.Numerics;
using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Io.Transform.Dbsim;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Herculan.Engine.Numerics;
using Herculan.Engine.Render;
using Herculan.Engine.Sim;
using Herculan.Engine.Terrain;

namespace Herculan.Engine.Scene;

/// <summary>
/// Assembles the first milestone's scene from real game data: one zone's terrain, one mech standing
/// on it, and a free-flying camera — see docs/engine/planning.md, "First milestone".
///
/// <para>Everything here is CPU-side on purpose. It loads files, builds a <see cref="SimWorld"/> and
/// produces vertex arrays, but touches no GL, so a host uploads the meshes once it has a context and
/// a headless caller (a test, a future editor's data pass) can build the same scene with no window
/// at all.</para>
/// </summary>
public sealed class ZoneScene {
	private ZoneScene(SimWorld world, MechObject mech, FlyCameraObject camera,
			MeshVertex[] terrainMesh, MeshVertex[] mechMesh, float mechBaseOffset) {
		World = world;
		Mech = mech;
		Camera = camera;
		TerrainMesh = terrainMesh;
		MechMesh = mechMesh;
		MechBaseOffset = mechBaseOffset;
	}

	public SimWorld World { get; }

	/// <summary>The single spawned mech.</summary>
	public MechObject Mech { get; }

	/// <summary>The observer camera, itself a simulation object (see <see cref="FlyCameraObject"/>).</summary>
	public FlyCameraObject Camera { get; }

	/// <summary>Terrain triangles in render space, ready to upload.</summary>
	public MeshVertex[] TerrainMesh { get; }

	/// <summary>Mech triangles in model space, ready to upload.</summary>
	public MeshVertex[] MechMesh { get; }

	/// <summary>
	/// How far up the mech model has to be lifted for its lowest point to touch the ground, in
	/// render units. DTS model space puts the origin at the rig pivot rather than at the feet.
	/// </summary>
	public float MechBaseOffset { get; }

	/// <summary>
	/// Loads zone <paramref name="zoneIndex"/> and spawns <paramref name="mechName"/> at the middle
	/// of it, with the camera placed a short distance back and above so the mech is in view on the
	/// first frame.
	/// </summary>
	public static ZoneScene Load(GameContent content, int zoneIndex, string mechName) {
		var materials = TerrainMaterialTable.Load(content);

		// Terrain material assignment is a randomised load-time pass in the original; the seed used
		// here is the engine's own, since DBSIM's generator state hasn't been recovered (see
		// SimRandom). It selects detail textures only, which nothing renders yet.
		var random = new SimRandom(zoneIndex);
		var terrain = TerrainZoneLoader.Load(content, zoneIndex, materials, random);

		var world = new SimWorld(terrain);

		var (mechMesh, mechBaseOffset, mechRadius) = LoadMechModel(content, mechName);
		var simData = LoadMechSimData(content, mechName);

		var mech = new MechObject(mechName, simData, mechRadius, MechLoadout.Stubbed) {
			Position = CenterOfZone(terrain),
		};
		mech.Tick(world);
		world.Add(mech);

		var camera = new FlyCameraObject {
			Position = CameraStart(mech.Position, terrain),
		};
		world.Add(camera);

		var terrainMesh = TerrainMeshBuilder.Build(terrain);

		return new ZoneScene(world, mech, camera, terrainMesh, mechMesh, mechBaseOffset);
	}

	/// <summary>
	/// Model-to-world transform for the mech, in render space: heading rotation, the lift that puts
	/// its feet on the ground, then its world position.
	/// </summary>
	public Matrix4x4 MechTransform() =>
		Matrix4x4.CreateRotationY(-BinaryAngle.ToRadians(Mech.Heading))
		* Matrix4x4.CreateTranslation(WorldScale.ToRender(Mech.Position) + new Vector3(0f, MechBaseOffset, 0f));

	private static Vec3i CenterOfZone(HeightGrid terrain) =>
		new((int)(terrain.WorldWidth / 2), (int)(terrain.WorldHeight / 2), 0);

	private static Vec3i CameraStart(Vec3i mechPosition, HeightGrid terrain) {
		// A little south of and above the mech, so it is in frame without having to fly to it.
		var eye = new Vec3i(mechPosition.X, mechPosition.Y - 6000, 0);
		return new Vec3i(eye.X, eye.Y, terrain.HeightAtWorld(eye.X, eye.Y) + 3000);
	}

	private static (MeshVertex[] Mesh, float BaseOffset, int Radius) LoadMechModel(GameContent content, string mechName) {
		byte[] bytes = content.ReadRequired("dts", mechName + ".DTS");

		var model = new DTSModelTransformer().BytesToObject(bytes) as DynamixThreeSpaceModel
			?? throw new InvalidDataException($"dts\\{mechName}.DTS did not parse as a DTS model.");

		if (model.Meshes is not { Count: > 0 } roots) {
			throw new InvalidDataException($"dts\\{mechName}.DTS contains no top-level shapes.");
		}

		// A mech file's roots are LOD variants of the same machine (unlike, say, BASES_AN.DTS, whose
		// roots are unrelated objects), so exactly one is wanted. Root 0 is taken as the primary; the
		// file carries no flag identifying which root is the full-detail one, and picking it properly
		// is part of the LOD/visibility work a later milestone brings.
		var mesh = DtsMeshBuilder.BuildRoot(roots[0]);
		var (min, max) = DtsMeshBuilder.Bounds(mesh);

		Vector3 extent = max - min;
		float radiusInRenderUnits = MathF.Max(extent.X, extent.Z) * 0.5f;

		return (mesh, -min.Y, (int)(radiusInRenderUnits * WorldScale.WorldUnitsPerMeter));
	}

	private static HercSimDat LoadMechSimData(GameContent content, string mechName) {
		byte[] bytes = content.ReadRequired("dat", mechName + ".DAT");

		return new HercSimDataTransformer().BytesToObject(bytes) as HercSimDat
			?? throw new InvalidDataException($"dat\\{mechName}.DAT did not parse as mech sim data.");
	}
}
