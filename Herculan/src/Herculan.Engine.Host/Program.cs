using System.Numerics;
using Herculan.Engine;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Herculan.Engine.Render;
using Herculan.Engine.Scene;
using Herculan.Engine.Sim;
using Silk.NET.Input;
using Silk.NET.OpenGL;

// The thin front-end host from docs/engine/planning.md's "Engine internal architecture" section:
// it locates an install, asks the engine to build a scene, and runs a real-time loop over it.
// Everything it does is wiring — no simulation rules, no rendering rules, no file formats — so a
// second host (the mission editor that decision exists to keep possible) can differ here and reuse
// everything below it unchanged.

const int DefaultZone = 504;
const string DefaultMech = "SAMSON";

string? installRoot = GameInstall.Locate(args.Length > 0 ? args[0] : null);
if (installRoot == null) {
	Console.Error.WriteLine(
		"Could not find an Earthsiege 2 installation.\n" +
		$"Pass its path as the first argument, or set {GameInstall.PathVariable}.\n" +
		$"The path should be the folder containing the '{GameInstall.ArchiveFolderName}' directory.");
	return 1;
}

int zoneIndex = args.Length > 1 && int.TryParse(args[1], out int parsedZone) ? parsedZone : DefaultZone;
string mechName = args.Length > 2 ? args[2].ToUpperInvariant() : DefaultMech;

Console.WriteLine($"HERCULAN Engine — loading zone {zoneIndex} with {mechName} from {installRoot}");

var content = GameContent.Mount(GameInstall.ArchiveDirectory(installRoot));
Console.WriteLine($"Mounted archives: {string.Join(", ", content.MountedArchives)}");

var scene = ZoneScene.Load(content, zoneIndex, mechName);
var terrain = scene.World.Terrain;
Console.WriteLine(
	$"Zone {zoneIndex}: {terrain.Width}x{terrain.Height} cells, {terrain.CellSize} units per cell, " +
	$"height scale {terrain.HeightScale}, peak {terrain.MaxWorldHeight} units.");
Console.WriteLine(
	$"{mechName}: {scene.MechMesh.Length / 3} triangles, terrain {scene.TerrainMesh.Length / 3} triangles.");
Console.WriteLine("W/A/S/D move, R/F rise and fall, arrow keys look, Shift boosts, Esc quits.");

using var window = new EngineWindow($"HERCULAN Engine — zone {zoneIndex}");

SceneRenderer? renderer = null;
GpuMesh? terrainMesh = null;
GpuMesh? mechMesh = null;
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
	mechMesh = new GpuMesh(gl, scene.MechMesh);
	items = new[] {
		new SceneItem(terrainMesh, Matrix4x4.Identity),
		new SceneItem(mechMesh, scene.MechTransform()),
	};

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
	mechMesh?.Dispose();

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
