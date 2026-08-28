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
			SceneObject? playerObject, BeamAppearance? beams,
			IReadOnlyDictionary<int, SceneModel> bulletModels,
			IReadOnlyDictionary<int, SceneModel> explosionModels,
			IReadOnlyDictionary<int, IReadOnlyList<SceneModel>> rocketModels, Atmosphere atmosphere) {
		Atmosphere = atmosphere;
		Beams = beams;
		BulletModels = bulletModels;
		ExplosionModels = explosionModels;
		RocketModels = rocketModels;
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

	/// <summary>How far this zone is visible and what it fades into — see <see cref="Scene.Atmosphere"/>.</summary>
	public Atmosphere Atmosphere { get; }

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

	/// <summary>
	/// Beam widths, colours and the shared cross-section, or null when either resource is missing —
	/// in which case beams still fire and still do damage, they just are not drawn. Loaded here
	/// because its colours are palette indices and the theater owns the palette.
	/// </summary>
	public BeamAppearance? Beams { get; }

	/// <summary>
	/// The shape each travelling shot is drawn as, keyed by the <c>PROJ.DAT</c> subtype id that
	/// spawned it — the same id <see cref="SimWorld.Bullets"/> is indexed by. Built up front, from
	/// every record in that table, because a shot that appears mid-flight has nowhere to load a model
	/// from; the original loads the same nine shapes once at startup for the same reason.
	/// </summary>
	public IReadOnlyDictionary<int, SceneModel> BulletModels { get; }

	/// <summary>
	/// The shape each impact effect is drawn as, keyed by the <c>EXPLOS.DAT</c> shape index its type
	/// row names — one root of <c>dts\EXPLOS.DTS</c> each, textured from whichever
	/// <c>dba\EXPLO&lt;n&gt;.DBA</c> that row's own second field selects. Built up front for the same
	/// reason <see cref="BulletModels"/> is: an effect appears at the instant of impact and has
	/// nowhere to load anything from, and the original loads all twenty once at startup.
	/// </summary>
	public IReadOnlyDictionary<int, SceneModel> ExplosionModels { get; }

	/// <summary>
	/// The shapes each launcher round is drawn as, keyed by the <c>PROJ.DAT</c> subtype id that fired
	/// it — the same arrangement <see cref="BulletModels"/> has, over a separate table and a separate
	/// shape file. The two key spaces overlap (both start at subtype 0) and mean different things, so
	/// they are deliberately not one dictionary.
	///
	/// <para>The value is a <b>list</b> because a rocket's flipbook is geometry: entry <c>i</c> is the
	/// shape with its exhaust flame on cell <c>i</c>, and a round in flight picks by
	/// <see cref="Rocket.AnimationFrame"/>. See <see cref="SceneModelLibrary.Rocket"/>.</para>
	/// </summary>
	public IReadOnlyDictionary<int, IReadOnlyList<SceneModel>> RocketModels { get; }

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

		// The travelling-projectile table, loaded once at startup as FUN_0040ade0 loads it, and given
		// to the world because a shot in flight is simulation state before it is anything visual.
		var bullets = BulletCatalog.Load(content.Read(BulletCatalog.ResourceFolder, BulletCatalog.TableResource));

		// The impact-effect table, loaded once at startup as FUN_00407b54 loads it. Its shapes are
		// built below, and the frame counts they yield go back into the catalog — an effect's life is
		// one pass of its own flipbook, so the simulation cannot time it without them.
		var explosions = ExplosionCatalog.Load(
			content.Read(ExplosionCatalog.ResourceFolder, ExplosionCatalog.TableResource));

		// And the launcher table, loaded once at startup as Rocket_LoadTypeTable_Unguided loads it.
		var rockets = RocketCatalog.Load(
			content.Read(RocketCatalog.ResourceFolder, RocketCatalog.TableResource));

		var world = new SimWorld(terrain, bullets, explosions, rockets, mission.Header.ZoneIndex);
		var models = new SceneModelLibrary(content, theater);
		var baseTypes = BaseTypeTable.Load(content);

		// The structure hit-sphere table, read straight after the type table as Bases_LoadTypeTable
		// reads it, and sized by it: BASECOL.DAT carries no count of its own.
		var baseCollision = BaseCollisionTable.Load(content, baseTypes.Count);

		// The simulator loads both weapon tables once, at startup, not per machine — see WeaponCatalog.
		var weapons = WeaponCatalog.Load(
			content.Read(WeaponCatalog.ResourceFolder, WeaponCatalog.TemplateResource),
			content.Read(WeaponCatalog.ResourceFolder, WeaponCatalog.ProjectileResource));

		var objects = new List<SceneObject>(mission.Placements.Count);
		SceneObject? playerObject = null;
		foreach (var placement in mission.Placements) {
			var spawned = Spawn(placement, models, baseTypes, baseCollision, weapons, world.Random);
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

		// Terrain is lit once, here, the way the original lights it once at zone load — see
		// TerrainMeshBuilder. The same theater ramp that colours a flat solid face supplies the
		// brightness curve the baked shade bytes are read through.
		var terrainMesh = TerrainMeshBuilder.Build(terrain, terrainBank, ShadeBrightness.Build(models.Shading));

		var bulletModels = new Dictionary<int, SceneModel>();
		for (int subtype = 0; bullets != null && subtype < bullets.Count; subtype++) {
			if (bullets.Record(subtype) is { } record && models.Bullet(record.ModelId) is { } model) {
				bulletModels[subtype] = model;
			}
		}

		var rocketModels = new Dictionary<int, IReadOnlyList<SceneModel>>();
		for (int subtype = 0; rockets != null && subtype < rockets.Count; subtype++) {
			if (rockets.Record(subtype) is { } record && models.Rocket(record.ModelId) is { Count: > 0 } cells) {
				rocketModels[subtype] = cells;
			}
		}

		var explosionModels = new Dictionary<int, SceneModel>();
		var explosionFrames = new List<int>();
		for (int shapeIndex = 0; explosions != null && shapeIndex < explosions.ShapeCount; shapeIndex++) {
			var shape = explosions.Shape(shapeIndex);
			var model = shape != null ? models.Explosion(shapeIndex, shape.TextureBankIndex) : null;

			if (model != null) {
				explosionModels[shapeIndex] = model;
			}

			explosionFrames.Add(model?.Sprites.Length ?? 0);
		}

		explosions?.BindFrameCounts(explosionFrames);

		return new MissionScene(mission, world, camera, objects, models.Models.ToArray(),
			terrainMesh, theater, terrainBank, playerObject,
			BeamAppearance.Load(content, theater.PaletteName), bulletModels, explosionModels,
			rocketModels, Atmosphere.From(terrain, models.Shading));
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
	/// <summary>
	/// Where one node of an animating machine's shape stands, in render space: the node's own posed
	/// transform followed by the machine's shape-to-world one. A caller draws each
	/// <see cref="MeshSegment"/> with the matrix for its own transform id, which is what makes the
	/// legs move.
	///
	/// <para>Two things differ from <see cref="TransformOf"/>, and both are the simulation being let
	/// through rather than approximated. The machine's own transform is
	/// <see cref="MechObject.WorldTransform"/>, so its lean over sloping ground comes with it, where
	/// the rigid path has only a heading rotation. And there is no
	/// <see cref="SceneModel.BaseOffset"/> lift: a HERC's shape origin is already its ground contact
	/// point and the sim puts that at terrain height plus the type's own ride height (the one retail
	/// machine whose model dips below zero, COLOSSUS, is also the one with a nonzero ride height, and
	/// by exactly the same 400 units — see <see cref="WorldScale.WorldUnitsPerDtsUnit"/>), so lifting
	/// by the bounding box on top of that counted the correction twice.</para>
	/// </summary>
	public static Matrix4x4 PosedTransformOf(MechObject mech, int transformId) {
		var world = WorldScale.ToRenderMatrix(mech.WorldTransform);
		return transformId < 0
			? world
			: WorldScale.ToRenderMatrix(mech.NodeTransform(transformId)) * world;
	}

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
			BaseTypeTable baseTypes, BaseCollisionTable baseCollision, WeaponCatalog? weapons,
			SimRandom random) {
		var (simObject, model) = Create(placement, models, baseTypes, baseCollision, weapons, random);
		if (simObject == null) {
			return null;
		}

		simObject.Position = placement.Position;
		simObject.Heading = placement.Heading;
		simObject.AwaitingDeployment = placement.AwaitingDeployment;

		// The original's hover-height substitution, applied at spawn because that is where it
		// happens in FUN_00421ee8 — see FlyerObject.DefaultHoverHeight.
		if (simObject is FlyerObject && placement.Position.Z == 0) {
			simObject.Position = new Vec3i(
				placement.Position.X, placement.Position.Y, FlyerObject.DefaultHoverHeight);
		}

		return new SceneObject(simObject, model, placement);
	}

	/// <summary>
	/// One object's own component health, or null when the install ships no <c>.DMG</c> for its type.
	/// The record is shared per type and the damage is per object, which is the split the original
	/// has: the maxima live in the loaded file and the three arrays are allocated in the constructor.
	/// </summary>
	private static ComponentDamage? ComponentDamageFor(SceneModelLibrary models, string typeName,
			int componentCount, int dependentCount, SimRandom random) =>
		models.DamageData(typeName) is { } data
			? new ComponentDamage(data, componentCount, dependentCount, random)
			: null;

	private static (SimObject? Object, SceneModel? Model) Create(MissionPlacement placement,
			SceneModelLibrary models, BaseTypeTable baseTypes, BaseCollisionTable baseCollision,
			WeaponCatalog? weapons, SimRandom random) {
		switch (placement.Kind) {
			case MissionUnitKind.Mech: {
				if (placement.TypeName == null || models.MechData(placement.TypeName) is not { } simData) {
					return (null, null);
				}

				var model = models.Mech(placement.TypeName);
				return (
					new MechObject(placement.TypeName, simData, model?.RadiusWorldUnits ?? 0,
						new MechLoadout(
							placement.WeaponRefs.Select(id => (int)id).ToArray(),
							placement.WeaponSecondary),
						models.MechAnimation(placement.TypeName),
						models.MechHardpoints(placement.TypeName),
						weapons,
						models.Collision(placement.TypeName),
						ComponentDamageFor(models, placement.TypeName,
							ComponentDamage.MechComponentCount, ComponentDamage.MechDependentCount, random)),
					model);
			}

			case MissionUnitKind.Flyer: {
				if (placement.TypeName == null) {
					return (null, null);
				}

				var model = models.Flyer(placement.TypeName);
				return (
					new FlyerObject(placement.TypeName, models.FlyerData(placement.TypeName),
						model?.RadiusWorldUnits ?? 0,
						models.Collision(placement.TypeName),
						ComponentDamageFor(models, placement.TypeName,
							ComponentDamage.FlyerComponentCount, ComponentDamage.FlyerDependentCount, random)),
					model);
			}

			case MissionUnitKind.Base: {
				if (baseTypes[placement.TypeIndex] is not { } type) {
					return (null, null);
				}

				var model = models.Base(type);
				var (boundingRadius, volume) = models.BaseShapeCollision(type);
				return (
					new BaseObject(type, volume, baseCollision[type.Index], boundingRadius),
					model);
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
