using System.Numerics;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Herculan.Engine.Numerics;
using Herculan.Engine.Render;
using Herculan.Engine.Sim;
using Herculan.Engine.Terrain;
using Herculan.Engine.World;

namespace Herculan.Engine.Scene;

/// <summary>One placed object, paired with the shared model it draws with.</summary>
/// <param name="Object">The simulation object, which owns the authoritative position and heading.</param>
/// <param name="Model">Its shared model, or null when its type has none the engine can build yet.</param>
/// <param name="Placement">The mission record it came from, kept for diagnostics and tooling.</param>
public sealed record SceneObject(SimObject Object, SceneModel? Model, MissionPlacement Placement);

/// <summary>
/// Assembles a playable scene from a real mission: the zone and theater the mission names, its
/// terrain, and one simulation object per unit the mission places — see
/// docs/engine/planning.md, "Milestone 4".
///
/// <para>Nothing here is configured by hand. The zone, the theater and its variant come out of
/// <c>script.dat</c>'s header; the units, their types, positions and headings come out of its
/// rosters and activation records (see <see cref="MissionLoader"/> for the rule and the RE behind
/// it); the player's own lance comes out of <c>player.mec</c>. The previous milestone's single
/// hardcoded mech at the middle of the zone is gone.</para>
///
/// <para>Everything is CPU-side on purpose. It loads files, builds a <see cref="SimWorld"/> and
/// produces vertex arrays, but touches no GL, so a host uploads the meshes once it has a context and
/// a headless caller (a test, a future editor's data pass) can build the same scene with no window
/// at all.</para>
/// </summary>
public sealed class MissionScene {
	private MissionScene(Mission mission, SimWorld world, FlyCameraObject camera,
			IReadOnlyList<SceneObject> objects, IReadOnlyList<SceneModel> models,
			MeshVertex[] terrainMesh, TheaterDescriptor theater, TerrainTextureBank? terrainBank,
			SceneObject? playerObject) {
		Mission = mission;
		World = world;
		Camera = camera;
		Objects = objects;
		PlayerObject = playerObject;
		Models = models;
		TerrainMesh = terrainMesh;
		Theater = theater;
		TerrainBank = terrainBank;
	}

	/// <summary>The mission this scene was built from.</summary>
	public Mission Mission { get; }

	public SimWorld World { get; }

	/// <summary>The observer camera, itself a simulation object (see <see cref="FlyCameraObject"/>).</summary>
	public FlyCameraObject Camera { get; }

	/// <summary>Every placed object, in spawn order.</summary>
	public IReadOnlyList<SceneObject> Objects { get; }

	/// <summary>
	/// The machine the player pilots, or null when the mission has no <c>player.mec</c> beside it.
	/// It is an ordinary placed object; the only thing that distinguishes it is that the host feeds
	/// it <see cref="MechObject.Controls"/>.
	/// </summary>
	public SceneObject? PlayerObject { get; }

	/// <summary>The player's HERC, or null when there is no player or it has no mech model.</summary>
	public MechObject? PlayerMech => PlayerObject?.Object as MechObject;

	/// <summary>The distinct models the scene draws with — upload each of these once.</summary>
	public IReadOnlyList<SceneModel> Models { get; }

	/// <summary>Terrain triangles in render space, ready to upload.</summary>
	public MeshVertex[] TerrainMesh { get; }

	/// <summary>The theater descriptor this scene was built against — it names the terrain bank and the palette.</summary>
	public TheaterDescriptor Theater { get; }

	/// <summary>
	/// The terrain's packed texture bank, or null when the theater's <c>.DBA</c> could not be loaded
	/// — in which case <see cref="TerrainMesh"/>'s vertices are all flagged untextured and fall back
	/// to the height/slope ramp.
	/// </summary>
	public TerrainTextureBank? TerrainBank { get; }

	/// <summary>How many placed objects have no model the engine can build yet.</summary>
	public int UnmodelledCount => Objects.Count(o => o.Model == null);

	/// <summary>
	/// Loads the mission at <paramref name="scriptPath"/> and everything it needs.
	/// </summary>
	public static MissionScene Load(GameContent content, string scriptPath) {
		var mission = MissionLoader.Load(content, scriptPath);

		var materials = TerrainMaterialTable.Load(content);
		var theater = TheaterDescriptor.Load(content, mission.Header.TheaterIndex, mission.Header.TheaterVariant);

		// Terrain material assignment is a randomised load-time pass in the original; the seed used
		// here is the engine's own, since DBSIM's generator state hasn't been recovered (see
		// SimRandom). It selects detail textures only, which nothing renders yet.
		var random = new SimRandom(mission.Header.ZoneIndex);
		var terrain = TerrainZoneLoader.Load(content, mission.Header.ZoneIndex, materials, random);

		var world = new SimWorld(terrain);
		var models = new SceneModelLibrary(content, theater);
		var baseTypes = BaseTypeTable.Load(content);

		var objects = new List<SceneObject>(mission.Placements.Count);
		SceneObject? playerObject = null;
		foreach (var placement in mission.Placements) {
			var spawned = Spawn(placement, models, baseTypes);
			if (spawned == null) {
				continue;
			}

			if (ReferenceEquals(placement, mission.Player)) {
				playerObject = spawned;
				if (spawned.Object is MechObject playerMech) {
					playerMech.IsPlayer = true;
				}
			}

			// Settling an object onto the terrain before it joins the world is a placement step, not
			// a simulation step -- a mech's own tick now walks it, which is not what spawning wants.
			spawned.Object.Position = new Vec3i(spawned.Object.Position.X, spawned.Object.Position.Y,
				spawned.Object switch {
					FlyerObject => spawned.Object.Position.Z,
					MechObject mech => terrain.HeightAtWorld(
						spawned.Object.Position.X, spawned.Object.Position.Y) + mech.Type.RideHeight,
					_ => terrain.HeightAtWorld(spawned.Object.Position.X, spawned.Object.Position.Y)
				});

			world.Add(spawned.Object);
			objects.Add(spawned);
		}

		var camera = new FlyCameraObject { Position = CameraStart(mission, terrain) };
		world.Add(camera);

		var terrainBank = TerrainTextureBank.Load(content, theater, materials);
		var terrainMesh = TerrainMeshBuilder.Build(terrain, terrainBank);

		return new MissionScene(mission, world, camera, objects, models.Models.ToArray(),
			terrainMesh, theater, terrainBank, playerObject);
	}

	/// <summary>
	/// Model-to-world transform for one placed object, in render space: heading rotation, the lift
	/// that puts it on the ground, then its world position.
	///
	/// <para>The rotation sign is the simulation's, not the camera's. A HERC's forward vector is
	/// <c>(-sin h, cos h)</c> in world XY — that falls out of <c>BuildEulerRotationMatrixQ14</c>'s
	/// Z-only matrix and the row-vector transform, and it is the same sense
	/// <see cref="MissionLoader"/>'s formation spread rotates in. <see cref="Camera"/>'s yaw runs
	/// the other way, so anything attaching a camera to an object's heading negates it; this
	/// transform must not.</para>
	/// </summary>
	public static Matrix4x4 TransformOf(SceneObject sceneObject) {
		float lift = sceneObject.Model?.BaseOffset ?? 0f;

		return Matrix4x4.CreateRotationY(BinaryAngle.ToRadians(sceneObject.Object.Heading))
			* Matrix4x4.CreateTranslation(
				WorldScale.ToRender(sceneObject.Object.Position) + new Vector3(0f, lift, 0f));
	}

	/// <summary>
	/// Builds and positions the simulation object for one placement. Returns null when the placement
	/// names a type the install has nothing at all for — an out-of-range index in a hand-edited
	/// mission — since an object with no identity has nothing to stand in for. A known type whose
	/// <i>model</i> cannot be built still spawns: it is really there, and the scene reports it.
	/// </summary>
	private static SceneObject? Spawn(MissionPlacement placement, SceneModelLibrary models,
			BaseTypeTable baseTypes) {
		var (simObject, model) = Create(placement, models, baseTypes);
		if (simObject == null) {
			return null;
		}

		simObject.Position = placement.Position;
		simObject.Heading = placement.Heading;

		// The original's hover-height substitution, applied at spawn because that is where it
		// happens in FUN_00421ee8 — see FlyerObject.DefaultHoverHeight.
		if (simObject is FlyerObject && placement.Position.Z == 0) {
			simObject.Position = new Vec3i(
				placement.Position.X, placement.Position.Y, FlyerObject.DefaultHoverHeight);
		}

		return new SceneObject(simObject, model, placement);
	}

	private static (SimObject? Object, SceneModel? Model) Create(MissionPlacement placement,
			SceneModelLibrary models, BaseTypeTable baseTypes) {
		switch (placement.Kind) {
			case MissionUnitKind.Mech: {
				if (placement.TypeName == null || models.MechData(placement.TypeName) is not { } simData) {
					return (null, null);
				}

				var model = models.Mech(placement.TypeName);
				return (
					new MechObject(placement.TypeName, simData, model?.RadiusWorldUnits ?? 0,
						new MechLoadout(placement.WeaponRefs.Select(id => (int)id).ToArray()),
						models.MechAnimation(placement.TypeName)),
					model);
			}

			case MissionUnitKind.Flyer: {
				if (placement.TypeName == null) {
					return (null, null);
				}

				var model = models.Flyer(placement.TypeName);
				return (
					new FlyerObject(placement.TypeName, models.FlyerData(placement.TypeName),
						model?.RadiusWorldUnits ?? 0),
					model);
			}

			case MissionUnitKind.Base: {
				if (baseTypes[placement.TypeIndex] is not { } type) {
					return (null, null);
				}

				var model = models.Base(type);
				return (new BaseObject(type, model?.RadiusWorldUnits ?? 0), model);
			}

			default:
				return (null, null);
		}
	}

	/// <summary>
	/// Puts the camera where the mission starts: a short distance behind and above the machine the
	/// player pilots, so the opening view is the one the mission was authored around. With no
	/// <c>player.mec</c> to say what that is, it falls back to the first placed object, and to the
	/// middle of the zone if the mission places nothing at all.
	/// </summary>
	private static Vec3i CameraStart(Mission mission, HeightGrid terrain) {
		var target = mission.Player?.Position
			?? mission.Placements.FirstOrDefault()?.Position
			?? new Vec3i((int)(terrain.WorldWidth / 2), (int)(terrain.WorldHeight / 2), 0);

		var eye = new Vec3i(target.X, target.Y - 6000, 0);
		return new Vec3i(eye.X, eye.Y, terrain.HeightAtWorld(eye.X, eye.Y) + 3000);
	}
}
