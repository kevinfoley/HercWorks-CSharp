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

var positional = new List<string>();
string? screenshotPath = null;
for (int i = 0; i < args.Length; i++) {
	if (args[i] == "--screenshot" && i + 1 < args.Length) {
		screenshotPath = args[++i];
	} else {
		positional.Add(args[i]);
	}
}

string? installRoot = GameInstall.Locate(positional.Count > 0 ? positional[0] : null);
if (installRoot == null) {
	Console.Error.WriteLine(
		"Could not find an Earthsiege 2 installation.\n" +
		$"Pass its path as the first argument, or set {GameInstall.PathVariable}.\n" +
		$"The path should be the folder containing the '{GameInstall.ArchiveFolderName}' directory.");
	return 1;
}

// The mission handoff VSHELL writes and DBSIM reads. It states its own zone and theater, so nothing
// else here needs configuring. Any of the save-slot snapshots in SAV\ works as an alternative.
string scriptPath = positional.Count > 1 ? positional[1] : MissionLoader.DefaultScriptPath(installRoot);
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

// Milestone 8: the player's own cockpit canopy art + HUD, drawn as three simultaneous panels
// (front/left/right) rather than the original's single keyboard-panned view — see
// docs/engine/planning.md's Milestone 8 section and docs/formats/cockpit-hud.md for why. Falls back
// to the old single full-window 3D view when there's no player.mec or its cockpit assets are missing
// (e.g. a raw script.dat with no accompanying player.mec).
var cockpitArt = mission.Player?.TypeName is { } pilotHerc ? CockpitArt.Load(content, pilotHerc) : null;
Console.WriteLine(cockpitArt != null
	? $"Cockpit art loaded for {mission.Player!.TypeName} — drawing the three-panel cockpit view."
	: "No cockpit art available — drawing a single full-window 3D view.");

Console.WriteLine("W/A/S/D move, R/F rise and fall, arrow keys look, Shift boosts, Esc quits.");

using var window = new EngineWindow($"HERCULAN Engine — zone {mission.Header.ZoneIndex}");

SceneRenderer? renderer = null;
Overlay2DRenderer? overlay = null;
GpuMesh? terrainMesh = null;
GpuTexture? terrainTexture = null;
GpuTexture? cockpitFrontTexture = null;
GpuTexture? cockpitSideTexture = null;
var modelMeshes = new Dictionary<string, GpuMesh>();
var modelTextures = new Dictionary<string, GpuTexture>();
var disposables = new List<IDisposable>();
SceneItem[]? items = null;
IKeyboard? keyboard = null;
var camera = new Camera();
int framesRendered = 0;
bool screenshotTaken = false;

// Fixed-timestep accumulator: the simulation always advances in whole ticks of the same length, so
// the ported fixed-point integration stays reproducible no matter how the frame rate varies.
double tickAccumulator = 0;
const double SecondsPerTick = 1.0 / SimWorld.TicksPerSecond;
const double MaxAccumulatedSeconds = 0.25;

window.Load += (gl, input) => {
	renderer = new SceneRenderer(gl);
	overlay = new Overlay2DRenderer(gl);

	terrainMesh = new GpuMesh(gl, scene.TerrainMesh);
	terrainTexture = scene.TerrainBank != null ? new GpuTexture(gl, scene.TerrainBank.Atlas) : null;

	if (cockpitArt != null) {
		cockpitFrontTexture = new GpuTexture(gl, cockpitArt.Front.Pixels, cockpitArt.Front.Width, cockpitArt.Front.Height);
		cockpitSideTexture = new GpuTexture(gl, cockpitArt.Side.Pixels, cockpitArt.Side.Width, cockpitArt.Side.Height);
	}

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

window.Render += (_, gl) => {
	if (renderer == null || overlay == null || items == null) {
		return;
	}

	var size = window.FramebufferSize;
	renderer.Clear();

	if (cockpitArt != null && cockpitFrontTexture != null && cockpitSideTexture != null) {
		DrawThreePanelCockpitView(gl, size.X, size.Y);
	} else {
		renderer.Render(camera, items, 0, 0, size.X, size.Y);
	}

	framesRendered++;
	if (screenshotPath != null && !screenshotTaken && framesRendered >= 30) {
		screenshotTaken = true;
		CaptureScreenshot(gl, size.X, size.Y, screenshotPath);
		window.Close();
	}
};

window.Closing += () => {
	renderer?.Dispose();
	overlay?.Dispose();
	terrainMesh?.Dispose();
	terrainTexture?.Dispose();
	cockpitFrontTexture?.Dispose();
	cockpitSideTexture?.Dispose();
	foreach (var disposable in disposables) {
		disposable.Dispose();
	}
};

window.Run();

return 0;

// Draws the front/left/right panels side by side, each sized by its own cockpit-art image's native
// aspect ratio fit to the full window height — not an equal three-way split of the window — so the
// quads butt together edge-to-edge with no seam or overlap regardless of how the front and side art's
// proportions differ from each other. The resulting three-panel composite is anchored to the window's
// horizontal center as one unit: a narrower window crops its outer edges symmetrically, a wider one
// leaves equal empty margins, and no panel is ever stretched. See CockpitViewLayout for the separate
// yaw-offset math that keeps the *3D scene* (as opposed to the cockpit art) tiling seamlessly across
// whatever aspect ratio each panel ends up with.
void DrawThreePanelCockpitView(GL gl, int totalWidth, int totalHeight) {
	int centerWidth = PanelWidthForHeight(cockpitArt!.Front, totalHeight);
	int sideWidth = PanelWidthForHeight(cockpitArt.Side, totalHeight);
	int leftWidth = sideWidth;
	int rightWidth = sideWidth;

	int totalContentWidth = leftWidth + centerWidth + rightWidth;
	int leftX = (totalWidth - totalContentWidth) / 2;
	int centerX = leftX + leftWidth;
	int rightX = centerX + centerWidth;

	float centerAspect = (float)centerWidth / Math.Max(totalHeight, 1);
	float leftAspect = (float)leftWidth / Math.Max(totalHeight, 1);
	float rightAspect = (float)rightWidth / Math.Max(totalHeight, 1);

	int leftYawOffset = CockpitViewLayout.SideYawOffset(camera.FieldOfView, centerAspect, leftAspect);
	int rightYawOffset = CockpitViewLayout.SideYawOffset(camera.FieldOfView, centerAspect, rightAspect);

	var centerCamera = ClonePanelCamera(camera, 0);
	var leftCamera = ClonePanelCamera(camera, -leftYawOffset);
	var rightCamera = ClonePanelCamera(camera, rightYawOffset);

	renderer!.Render(leftCamera, items!, leftX, 0, leftWidth, totalHeight);
	overlay!.Draw(leftX, 0, leftWidth, totalHeight, cockpitSideTexture!,
		cockpitArt!.Side.Width, cockpitArt.Side.Height, mirrorHorizontally: true, widgets: null);

	renderer.Render(centerCamera, items!, centerX, 0, centerWidth, totalHeight);
	overlay.Draw(centerX, 0, centerWidth, totalHeight, cockpitFrontTexture!,
		cockpitArt.Front.Width, cockpitArt.Front.Height, mirrorHorizontally: false, widgets: cockpitArt.Gau);

	renderer.Render(rightCamera, items!, rightX, 0, rightWidth, totalHeight);
	overlay.Draw(rightX, 0, rightWidth, totalHeight, cockpitSideTexture!,
		cockpitArt.Side.Width, cockpitArt.Side.Height, mirrorHorizontally: false, widgets: null);
}

// The pixel width a panel's cockpit-art quad occupies when fit to the given window height while
// preserving its own native aspect ratio — matches Overlay2DRenderer.Draw's own fit-by-height math, so
// a panel sized to this width shows its art edge-to-edge with no letterboxed gap on either side.
static int PanelWidthForHeight(CockpitFrame frame, int height) =>
	Math.Max(1, (int)Math.Round(frame.Width * (height / (float)frame.Height)));

static Camera ClonePanelCamera(Camera source, int yawOffset) => new() {
	Position = source.Position,
	Yaw = source.Yaw + yawOffset,
	Pitch = source.Pitch,
	FieldOfView = source.FieldOfView,
	NearPlane = source.NearPlane,
	FarPlane = source.FarPlane,
};

// Dependency-free 24bpp BMP writer — no System.Drawing/ImageSharp, per Herculan.Engine's
// no-imaging-dependency precedent (see docs/engine/planning.md's Milestone 1 notes). Reads straight
// from the framebuffer via glReadPixels; BMP's standard bottom-up row order matches GL's own
// bottom-left-origin convention, so no row flip is needed.
static void CaptureScreenshot(GL gl, int width, int height, string path) {
	int rowSize = width * 3;
	int rowPadding = (4 - rowSize % 4) % 4;
	int paddedRowSize = rowSize + rowPadding;
	int pixelDataSize = paddedRowSize * height;

	var pixels = new byte[width * height * 3];
	gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Bgr, PixelType.UnsignedByte, pixels.AsSpan());

	using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
	using var writer = new BinaryWriter(file);

	int fileSize = 14 + 40 + pixelDataSize;
	writer.Write((byte)'B'); writer.Write((byte)'M');
	writer.Write(fileSize);
	writer.Write(0); // reserved
	writer.Write(14 + 40); // pixel data offset

	writer.Write(40); // DIB header size (BITMAPINFOHEADER)
	writer.Write(width);
	writer.Write(height); // positive = bottom-up row order
	writer.Write((short)1); // planes
	writer.Write((short)24); // bits per pixel
	writer.Write(0); // no compression
	writer.Write(pixelDataSize);
	writer.Write(2835); // ~72 DPI
	writer.Write(2835);
	writer.Write(0); // colors used
	writer.Write(0); // important colors

	var padding = new byte[rowPadding];
	for (int row = 0; row < height; row++) {
		writer.Write(pixels, row * rowSize, rowSize);
		if (rowPadding > 0) {
			writer.Write(padding);
		}
	}

	Console.WriteLine($"Wrote screenshot to {path} ({width}x{height}).");
}

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
