using System.Numerics;
using Herculan.Engine;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Herculan.Engine.Render;
using Herculan.Engine.Scene;
using Herculan.Engine.Sim;
using Herculan.Engine.World;
using Silk.NET.Input;
using Silk.NET.OpenGL;

// The thin front-end host from docs/engine/planning.md's "Engine internal architecture" section:
// it locates an install, asks the engine to build a scene from a real mission, and runs a real-time
// loop over it. Everything it does is wiring — no simulation rules, no rendering rules, no file
// formats — so a second host (the mission editor that decision exists to keep possible) can differ
// here and reuse everything below it unchanged.

string? installRoot = GameInstall.Locate(args.Length > 0 ? args[0] : null);
if (installRoot == null) {
	Console.Error.WriteLine(
		"Could not find an Earthsiege 2 installation.\n" +
		$"Pass its path as the first argument, or set {GameInstall.PathVariable}.\n" +
		$"The path should be the folder containing the '{GameInstall.ArchiveFolderName}' directory.");
	return 1;
}

// The mission handoff VSHELL writes and DBSIM reads. It states its own zone and theater, so nothing
// else here needs configuring. Any of the save-slot snapshots in SAV\ works as an alternative.
string scriptPath = args.Length > 1 ? args[1] : MissionLoader.DefaultScriptPath(installRoot);
if (!File.Exists(scriptPath)) {
	Console.Error.WriteLine(
		$"No mission at {scriptPath}.\n" +
		$"Pass one as the second argument — {MissionLoader.ScriptFileName} from the install's " +
		$"{MissionLoader.DataFolderName} folder, or any of the SAV\\script*.dat snapshots.");
	return 1;
}

Console.WriteLine($"HERCULAN Engine — loading {scriptPath} from {installRoot}");

var content = GameContent.Mount(GameInstall.ArchiveDirectory(installRoot));
Console.WriteLine($"Mounted archives: {string.Join(", ", content.MountedArchives)}");

var scene = MissionScene.Load(content, scriptPath);
var mission = scene.Mission;
var terrain = scene.World.Terrain;

Console.WriteLine(
	$"Mission: zone {mission.Header.ZoneIndex}, theater {mission.Header.TheaterIndex}" +
	$" variant {mission.Header.TheaterVariant}.");
Console.WriteLine(
	$"Zone {mission.Header.ZoneIndex}: {terrain.Width}x{terrain.Height} cells, {terrain.CellSize} units per cell, " +
	$"height scale {terrain.HeightScale}, peak {terrain.MaxWorldHeight} units.");
Console.WriteLine(
	$"Placed {scene.Objects.Count} objects — {mission.CountOf(MissionUnitKind.Mech)} mechs, " +
	$"{mission.CountOf(MissionUnitKind.Flyer)} flyers, {mission.CountOf(MissionUnitKind.Base)} structures — " +
	$"from {scene.Models.Count} distinct models.");

if (scene.UnmodelledCount > 0) {
	Console.WriteLine(
		$"{scene.UnmodelledCount} of them have no model (missing install files or an out-of-range " +
		"index); they are simulated and positioned but not drawn.");
}

Console.WriteLine(mission.Player is { } player
	? $"Player flies {player.TypeName} at {player.Position}."
	: "No player.mec beside the mission — camera starts at the first placed object.");

foreach (var group in scene.Objects
		.GroupBy(o => (o.Placement.Kind, o.Placement.TypeName ?? $"base type {o.Placement.TypeIndex}"))
		.OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.Item2)) {
	var model = group.First().Model;
	string art = model == null
		? "no model"
		: model.Atlas is { } atlas
			? $"{model.Mesh.Length / 3} tris, {atlas.FrameCount} frames in {atlas.Width}x{atlas.Height}"
			: $"{model.Mesh.Length / 3} tris, untextured";
	Console.WriteLine($"  {group.Count()}x {group.Key.Item2} ({art})");
}

Console.WriteLine($"Theater {mission.Header.TheaterIndex} ({scene.Theater.PaletteName}): " + (scene.TerrainBank is { } bank
	? $"terrain bank {bank.BankName}, {bank.Atlas.FrameCount} frames in a {bank.Atlas.Width}x{bank.Atlas.Height} atlas, "
	  + $"{scene.TerrainMesh.Length / 3} triangles."
	: $"terrain bank could not be loaded — drawing {scene.TerrainMesh.Length / 3} triangles flat-shaded."));
Console.WriteLine("W/A/S/D move, R/F rise and fall, arrow keys look, Shift boosts, Esc quits.");

using var window = new EngineWindow($"HERCULAN Engine — zone {mission.Header.ZoneIndex}");

SceneRenderer? renderer = null;
GpuMesh? terrainMesh = null;
GpuTexture? terrainTexture = null;
var modelMeshes = new Dictionary<string, GpuMesh>();
var modelTextures = new Dictionary<string, GpuTexture>();
var disposables = new List<IDisposable>();
SceneItem[]? items = null;
IKeyboard? keyboard = null;
var camera = new Camera();

// Fixed-timestep accumulator: the simulation always advances in whole ticks of the same length, so
// the ported fixed-point integration stays reproducible no matter how the frame rate varies.
double tickAccumulator = 0;
const double SecondsPerTick = 1.0 / SimWorld.TicksPerSecond;
const double MaxAccumulatedSeconds = 0.25;

window.Load += (gl, input) => {
	renderer = new SceneRenderer(gl);

	terrainMesh = new GpuMesh(gl, scene.TerrainMesh);
	terrainTexture = scene.TerrainBank != null ? new GpuTexture(gl, scene.TerrainBank.Atlas) : null;

	// One upload per distinct model, however many objects share it — a mission routinely fields
	// several of the same machine and a row of identical structures.
	foreach (var model in scene.Models) {
		modelMeshes[model.Key] = new GpuMesh(gl, model.Mesh);
		if (model.Atlas != null) {
			modelTextures[model.Key] = new GpuTexture(gl, model.Atlas);
		}
	}

	disposables.AddRange(modelMeshes.Values);
	disposables.AddRange(modelTextures.Values);

	var built = new List<SceneItem> {
		new(terrainMesh, Matrix4x4.Identity, terrainTexture?.Handle)
	};

	foreach (var sceneObject in scene.Objects) {
		if (sceneObject.Model is not { } model || !modelMeshes.TryGetValue(model.Key, out var mesh)) {
			continue;
		}

		built.Add(new SceneItem(mesh, MissionScene.TransformOf(sceneObject),
			modelTextures.TryGetValue(model.Key, out var texture) ? texture.Handle : null));
	}

	items = built.ToArray();
	keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;

	// No backface culling. DTS geometry is not reliably wound — the WinForms model viewer reached
	// the same conclusion and never culls either — so culling would punch holes in the mech rather
	// than save fill rate. The shader shades two-sided to match.
	gl.Disable(EnableCap.CullFace);
};

window.Update += deltaSeconds => {
	scene.Camera.Input = ReadInput(keyboard);

	if (keyboard?.IsKeyPressed(Key.Escape) == true) {
		window.Close();
		return;
	}

	// Clamping the accumulator stops a long stall (a breakpoint, a window drag) from turning into
	// a burst of catch-up ticks that would teleport everything.
	tickAccumulator = Math.Min(tickAccumulator + deltaSeconds, MaxAccumulatedSeconds);
	while (tickAccumulator >= SecondsPerTick) {
		scene.World.Tick();
		tickAccumulator -= SecondsPerTick;
	}

	scene.Camera.ApplyTo(camera);
};

window.Render += (_, _) => {
	if (renderer == null || items == null) {
		return;
	}

	var size = window.FramebufferSize;
	renderer.Render(camera, items, size.X, size.Y);
};

window.Closing += () => {
	renderer?.Dispose();
	terrainMesh?.Dispose();
	terrainTexture?.Dispose();
	foreach (var disposable in disposables) {
		disposable.Dispose();
	}
};

window.Run();

return 0;

static CameraInput ReadInput(IKeyboard? keyboard) {
	if (keyboard == null) {
		return default;
	}

	return new CameraInput {
		Forward = Axis(keyboard, Key.W, Key.S),
		Strafe = Axis(keyboard, Key.D, Key.A),
		Vertical = Axis(keyboard, Key.R, Key.F),
		Yaw = Axis(keyboard, Key.Right, Key.Left),
		Pitch = Axis(keyboard, Key.Up, Key.Down),
		Boost = keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight),
	};
}

static int Axis(IKeyboard keyboard, Key positive, Key negative) =>
	(keyboard.IsKeyPressed(positive) ? 1 : 0) - (keyboard.IsKeyPressed(negative) ? 1 : 0);
