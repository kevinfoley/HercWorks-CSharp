using System.Numerics;
using Herculan.Engine;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Herculan.Engine.Host.Debugging;
using Herculan.Engine.Input;
using Herculan.Engine.Numerics;
using Herculan.Engine.Render;
using Herculan.Engine.Scene;
using Herculan.Engine.Sim;
using Herculan.Engine.Sim.Anim;
using Herculan.Engine.World;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;

// The thin front-end host from docs/engine/planning.md's "Engine internal architecture" section:
// it locates an install, asks the engine to build a scene from a real mission, and runs a real-time
// loop over it. Everything it does is wiring — no simulation rules, no rendering rules, no file
// formats — so a second host (the mission editor that decision exists to keep possible) can differ
// here and reuse everything below it unchanged.

var positional = new List<string>();
string? screenshotPath = null;
MfdMode? initialMfdMode = null;
bool startOnHeadsDown = false;
HddPage initialHddPage = CockpitHudState.Default.Hdd;
HddDamageView initialHddDamageView = CockpitHudState.Default.HddDamage;
short initialThrottle = 0;
bool startExternal = false;
short heldTwist = 0;
short heldPitch = 0;
int? initialWeaponRow = null;
bool initialLink = false;
for (int i = 0; i < args.Length; i++) {
	if (args[i] == "--screenshot" && i + 1 < args.Length) {
		screenshotPath = args[++i];
	} else if (args[i] == "--mfd" && i + 1 < args.Length && int.TryParse(args[++i], out int mfdIndex)
			&& mfdIndex >= 0 && mfdIndex <= 5) {
		// Which MFD screen to power up on. F1-F6 switch it live; this exists so a --screenshot run,
		// which never sees a keystroke, can be pointed at a specific screen.
		initialMfdMode = (MfdMode)mfdIndex;
	} else if (args[i] == "--hdd") {
		// Power up already panned down to the Heads-Down Display, for the same reason as --mfd: a
		// --screenshot run never sees a keystroke. An optional 0 or 1 picks which of its two screens
		// to land on — the command display or the damage detail, as [F7] and [F8] do live.
		startOnHeadsDown = true;
		if (i + 1 < args.Length && int.TryParse(args[i + 1], out int hddIndex)
			&& hddIndex >= 0 && hddIndex <= 1) {
			initialHddPage = (HddPage)hddIndex;
			i++;
		}
	} else if (args[i] == "--throttle" && i + 1 < args.Length
			&& int.TryParse(args[++i], out int throttleSetting)) {
		// Power up with the throttle already open, for the same reason as --mfd and --hdd: a
		// --screenshot run never sees a keystroke, and a walking machine is the only way to see the
		// gait, the cockpit bob or the slider anywhere but its centre. ±1024 is full travel.
		initialThrottle = (short)Math.Clamp(throttleSetting, -ThrottleTrack.Full, ThrottleTrack.Full);
	} else if (args[i] == "--turret" && i + 2 < args.Length
			&& int.TryParse(args[i + 1], out int twistAxis) && int.TryParse(args[i + 2], out int pitchAxis)) {
		// Hold the two turret axes for the whole run, for the same reason as --throttle: a
		// --screenshot run never sees a keystroke, and the turret only moves while a key is held.
		// ±256 is full deflection on each.
		heldTwist = (short)Math.Clamp(twistAxis, -MechControls.AxisFull, MechControls.AxisFull);
		heldPitch = (short)Math.Clamp(pitchAxis, -MechControls.AxisFull, MechControls.AxisFull);
		i += 2;
	} else if (args[i] == "--weapon" && i + 1 < args.Length
			&& int.TryParse(args[++i], out int weaponRow) && weaponRow >= 1 && weaponRow <= 10) {
		// Arm a weapon panel row at power-up, 1-based as the row prints it, and optionally link it.
		// Same reason as --mfd and --throttle: a --screenshot run never sees a keystroke, and the
		// armed row and a linked pair are only visible once something has selected one.
		initialWeaponRow = weaponRow - 1;
	} else if (args[i] == "--link") {
		initialLink = true;
	} else if (args[i] == "--external") {
		// Power up in the external chase view, for the same reason as --mfd and --throttle: the
		// player's own machine is the one thing the cockpit view never shows, so a --screenshot run
		// has no other way to see its own legs move.
		startExternal = true;
	} else if (args[i] == "--hdd-damage" && i + 1 < args.Length
			&& int.TryParse(args[++i], out int damageIndex) && damageIndex >= 0 && damageIndex <= 2) {
		// Which component category the damage screen powers up listing. [S], [I] and [W] switch it
		// live; this is the same reason --mfd and --hdd exist.
		initialHddDamageView = (HddDamageView)damageIndex;
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
// The theater's palette is the live palette — all 256 slots — with only this herc's own 24-entry
// cockpit colour scheme installed over slots 42-65. See CockpitPalette.
var cockpitArt = mission.Player?.TypeName is { } pilotHerc
	? CockpitArt.Load(content, pilotHerc, scene.Theater.PaletteName)
	: null;
if (cockpitArt != null) {
	Console.WriteLine(
		$"Cockpit art loaded for {mission.Player!.TypeName} — drawing the three-panel cockpit view. "
		+ (cockpitArt.Sprites is { } hud
			? $"HUD sprites: {string.Join(", ", hud.BankNames)} in a {hud.Atlas.Width}x{hud.Atlas.Height} atlas."
			: "No HUD sprite banks could be loaded — canopy art only."));
	Console.WriteLine(cockpitArt.ColorSchemeIndex >= 0
		? $"Cockpit colour scheme {cockpitArt.ColorSchemeIndex} — palette slots "
		  + $"{CockpitPalette.CockpitSchemeFirstSlot}-"
		  + $"{CockpitPalette.CockpitSchemeFirstSlot + CockpitPalette.CockpitSchemeLength - 1}"
		  + $" from COCKPIT.DPL entries {CockpitPalette.SchemeFirstEntry(cockpitArt.ColorSchemeIndex)}+."
		: $"No cockpit colour scheme — {mission.Player!.TypeName}.DAT unreadable, so slots "
		  + $"{CockpitPalette.CockpitSchemeFirstSlot}+ keep the theater's filler colour.");
	if (!cockpitArt.ClipRegionsLoaded) {
		Console.WriteLine(
			"Viewport cutout fell back to inferring the hole from black pixels — at least one of the "
			+ "herc's .HD0/.HD2 region files could not be read.");
	}
} else {
	Console.WriteLine("No cockpit art available — drawing a single full-window 3D view.");
}

// How far the display window travels down the cockpit canvas to reach the Heads-Down Display, read
// from the herc's own vue\<HERC>.VUE rather than assumed. Every retail file says 237 authored rows
// (474 device), but reading it is what makes the pan the file's statement instead of this host's.
var viewGeometry = cockpitArt?.ViewGeometry;
var cockpitPan = new CockpitPan(
	viewGeometry?.HeadsDownTravelY ?? CockpitViewGeometry.DefaultHeadsDownOriginY);
if (startOnHeadsDown) {
	cockpitPan.Request(headsDown: true);
	cockpitPan.Advance(CockpitPan.DurationSeconds);
}

if (cockpitArt?.HeadsDown != null) {
	Console.WriteLine(
		$"Heads-Down Display art loaded — pan travel {cockpitPan.TravelRows} device rows, "
		+ $"{CockpitPan.DurationSeconds:0.00}s"
		+ (viewGeometry == null ? " (no .VUE; using the retail default travel)." : "."));
	Console.WriteLine(cockpitArt.HeadsDownLayout is { } hddLayout
		? $"Heads-Down widgets from the herc's own .GAU — screen {hddLayout.Screen}, "
		  + $"arrow frame set {hddLayout.UnlitFrame(HddLayout.Widget.ArrowUp)}+."
		: "No Heads-Down widget block in this herc's .GAU — drawing its art only.");
} else if (cockpitArt != null) {
	Console.WriteLine("No .HB1 for this herc — the Heads-Down Display is unavailable.");
}

// The cockpit readouts' live values. The hardpoint names come from the shell weapon catalog keyed by
// player.mec's own hardpoint ids; speed, throttle, turret, the shield numbers and the energy bar are
// taken off the piloted machine each frame. What is left sits at the power-up defaults in
// CockpitHudState.Default until the sim carries the state behind it.
var hudState = CockpitHudState.Default with { Hdd = initialHddPage, HddDamage = initialHddDamageView };
if (initialMfdMode is { } startMfdMode) {
	hudState = hudState with { Mfd = startMfdMode };
}

// The weapon panel. Both lists come off the piloted machine's own mounts, which are already built:
// the rows in cockpit-row order, and the Heads-Down Display's list in hardpoint order. See
// WeaponRowState.
if (cockpitArt?.Gau is { } weaponGau && scene.PlayerMech is { } armedMech) {
	hudState = hudState with {
		Weapons = WeaponRowState.Build(armedMech.Weapons, weaponGau.WeaponListTotal, cockpitArt.Strings),
		HardpointNames = armedMech.Weapons.Mounts.Select(m => m.Name).ToList(),
	};

	if (initialWeaponRow is { } startRow) {
		armedMech.Weapons.SelectBySlot(startRow);
		if (initialLink) {
			armedMech.Weapons.ToggleLink();
		}
	}

	Console.WriteLine("Hardpoints: " + string.Join(", ", hudState.Weapons
		.Select((row, i) => $"{i + 1} {row.Name}")
		.Where(entry => entry.Length > 2)));
}

// Milestone 9: the player walks. A HERC has no velocity vector — the walk and run animations' root
// motion is what moves it — so piloting is the arrow keys on the throttle and the stick, and the
// machine covers whatever ground its own gait covers. See docs/simulation/mech-locomotion.md.
//
// Retail's own bindings, from the manual's keyboard table and its throttle section: left and right
// arrows steer, up and down arrows open and close the throttle, and keypad [5] is all stop. The
// manual says the numeric keypad with NUM LOCK off, which on a real keyboard is the same key as the
// arrow cluster; both are accepted here since a host window has no NUM LOCK to read.
var pilotMech = scene.PlayerMech;
bool piloting = pilotMech != null;
bool allStopKeyDown = false;
bool shieldRearKeyDown = false;
bool shieldFrontKeyDown = false;

// The weapon panel's row keys, in row order: [1] is row 1 and [0] is row 10, which is the same
// wrap-around the row's own printed digit uses ((slot + 1) % 10).
Key[] weaponRowKeys = {
	Key.Number1, Key.Number2, Key.Number3, Key.Number4, Key.Number5,
	Key.Number6, Key.Number7, Key.Number8, Key.Number9, Key.Number0,
};
bool[] weaponRowKeyDown = new bool[weaponRowKeys.Length];
bool cycleWeaponKeyDown = false;
bool linkKeyDown = false;

// [V], the external view: the camera parked behind the machine with the cockpit not drawn. Both the
// geometry and this binding are placeholders — see ExternalCamera for what has not been RE'd. The
// manual's own [V] cycles through several external cameras; this is one fixed chase view and a
// toggle.
bool externalView = startExternal;
bool externalViewKeyDown = false;

// The debug panel, on [Esc] — which therefore no longer quits; close the window for that. It owns
// its own view options and readouts; see DebugPanel for what it shows and why it is ImGui rather
// than the game's own HUD font.
var debugPanel = new DebugPanel();

string debugFontPath = Path.Combine(AppContext.BaseDirectory,
	"Assets", "Fonts", "Open_Sans", "static", "OpenSans-Regular.ttf");

// The console's throttle slider and the machine's throttle setting are two-way bound, so the gauge's
// own value is state in its own right: it is what the machine reads on any frame the machine did not
// itself move the throttle. See MechObject.ExchangeCockpitThrottle.
var throttleTrack = cockpitArt != null ? ThrottleTrack.From(cockpitArt) : null;
short throttleGauge = initialThrottle;
if (pilotMech != null && initialThrottle != 0) {
	pilotMech.Throttle = initialThrottle;
}

if (pilotMech != null) {
	Console.WriteLine(
		$"Piloting {pilotMech.Name}: top speed {pilotMech.Type.DisplaySpeedKph(pilotMech.Type.MaxForward)} km/h, "
		+ $"walk/run threshold at {pilotMech.Type.DisplaySpeedKph(pilotMech.Type.GaitThreshold)} km/h"
		+ (pilotMech.Thread == null ? " — no animation data, so it cannot walk." : "."));
	Console.WriteLine("Up/Down arrows throttle — hold Down through zero for reverse — Left/Right "
		+ "arrows turn, keypad 5 all stop, C switches to the free camera, V to the external view.");
	Console.WriteLine("J/K twist the turret, I/M pitch it, Backspace re-centres it — the manual's own "
		+ "keyboard turret set. The cockpit view looks where the turret points.");
	Console.WriteLine(throttleTrack != null
		? "Drag the console's throttle slider with the mouse to set it; it tracks the keys either way."
		: "No throttle slider in this herc's .GAU — keyboard throttle only.");
}

Console.WriteLine("Free camera: W/A/S/D move, R/F rise and fall, arrow keys look, Shift boosts.");
Console.WriteLine("Esc opens the debug panel (skeleton view, animation readouts) — it no longer quits; "
	+ "close the window for that.");
Console.WriteLine("F1-F6 switch the MFD screen: STATUS, FLASH COMM, NAV MAP, SCANNER, TARGET, MISSILE CAM.");
Console.WriteLine("F7/F8 pan down to the Heads-Down Display's command and damage screens; "
	+ "F1-F6 pan back up.");
Console.WriteLine("On the damage screen, S/I/W switch between structural, internal and weapon systems.");
Console.WriteLine("1-0 arm a weapon row (left-click the row does the same), W and Alt+W step through "
	+ "the firing chain, Alt+1-0 or a right-click add and remove a row from it.");
Console.WriteLine("L or the LINK button links the armed weapon to its opposite hardpoint, when that "
	+ "hardpoint carries the same weapon; both rows then light together.");

using var window = new EngineWindow($"HERCULAN Engine — zone {mission.Header.ZoneIndex}");

SceneRenderer? renderer = null;
Overlay2DRenderer? overlay = null;
WireframeRenderer? wireframe = null;
ImGuiController? imgui = null;
GpuMesh? terrainMesh = null;
GpuTexture? terrainTexture = null;
GpuTexture? cockpitFrontTexture = null;
GpuTexture? cockpitSideTexture = null;
GpuTexture? cockpitHeadsDownTexture = null;
GpuTexture? hudSpriteTexture = null;
var modelMeshes = new Dictionary<string, GpuMesh>();
var modelTextures = new Dictionary<string, GpuTexture>();
var disposables = new List<IDisposable>();
SceneItem[]? items = null;
SceneItem[]? pilotedItems = null;
var movers = new List<(SceneObject Object, SceneItem Item)>();

// A machine whose shape animates is drawn a node at a time, so each entry here is one geometry
// segment riding one transform of one mech — see MissionScene.PosedTransformOf.
var posedParts = new List<(MechObject Mech, int TransformId, SceneItem Item)>();
var segmentMeshes = new Dictionary<string, GpuMesh[]>();
IKeyboard? keyboard = null;
bool cameraKeyDown = false;
var cockpitInput = new CockpitInput();
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
	wireframe = new WireframeRenderer(gl);
	imgui = new ImGuiController(gl, window.View, input, new ImGuiFontConfig(debugFontPath, 16));

	terrainMesh = new GpuMesh(gl, scene.TerrainMesh);
	terrainTexture = scene.TerrainBank != null ? new GpuTexture(gl, scene.TerrainBank.Atlas) : null;

	if (cockpitArt != null) {
		cockpitFrontTexture = new GpuTexture(gl, cockpitArt.Front.Pixels, cockpitArt.Front.Width, cockpitArt.Front.Height);
		cockpitSideTexture = new GpuTexture(gl, cockpitArt.Side.Pixels, cockpitArt.Side.Width, cockpitArt.Side.Height);

		if (cockpitArt.HeadsDown is { } headsDownFrame) {
			cockpitHeadsDownTexture = new GpuTexture(gl, headsDownFrame.Pixels, headsDownFrame.Width, headsDownFrame.Height);
		}

		if (cockpitArt.Sprites is { } hudSprites) {
			hudSpriteTexture = new GpuTexture(gl, hudSprites.Atlas);
		}
	}

	// Which models are actually going to be drawn a node at a time: one whose segments exist *and*
	// whose object has an animation thread to pose them with. A machine whose shape carries no
	// ANAnimList has neither, and has to keep the flat mesh, which has the rest pose baked in — its
	// segments alone would put every part at the shape's origin.
	var animatedKeys = scene.Objects
		.Where(o => o.Object is MechObject { Thread: not null } && o.Model is { } m && m.Segments.Length > 0)
		.Select(o => o.Model!.Key)
		.ToHashSet(StringComparer.OrdinalIgnoreCase);

	// One upload per distinct model, however many objects share it — a mission routinely fields
	// several of the same machine and a row of identical structures. A model that is drawn posed
	// uploads its segments instead of its flat mesh: the two are the same triangles, and only one of
	// them is ever drawn.
	foreach (var model in scene.Models) {
		if (animatedKeys.Contains(model.Key)) {
			segmentMeshes[model.Key] = model.Segments.Select(segment => new GpuMesh(gl, segment.Vertices)).ToArray();
			disposables.AddRange(segmentMeshes[model.Key]);
		} else {
			modelMeshes[model.Key] = new GpuMesh(gl, model.Mesh);
		}

		if (model.Atlas != null) {
			modelTextures[model.Key] = new GpuTexture(gl, model.Atlas);
		}
	}

	disposables.AddRange(modelMeshes.Values);
	disposables.AddRange(modelTextures.Values);

	var built = new List<SceneItem> {
		new(terrainMesh, Matrix4x4.Identity, terrainTexture?.Handle)
	};

	// The player's own machine, kept aside so the cockpit view can leave it out — see below. A
	// segmented machine contributes one item per node, so this is a set rather than one item.
	var playerItems = new HashSet<SceneItem>();

	foreach (var sceneObject in scene.Objects) {
		if (sceneObject.Model is not { } model) {
			continue;
		}

		uint? texture = modelTextures.TryGetValue(model.Key, out var bound) ? bound.Handle : null;
		bool isPlayer = ReferenceEquals(sceneObject, scene.PlayerObject);

		if (sceneObject.Object is MechObject mech && segmentMeshes.TryGetValue(model.Key, out var segments)) {
			for (int i = 0; i < segments.Length; i++) {
				int transformId = model.Segments[i].TransformId;
				var part = new SceneItem(segments[i],
					MissionScene.PosedTransformOf(mech, transformId), texture);

				built.Add(part);
				posedParts.Add((mech, transformId, part));
				if (isPlayer) {
					playerItems.Add(part);
				}
			}

			continue;
		}

		if (!modelMeshes.TryGetValue(model.Key, out var mesh)) {
			continue;
		}

		var item = new SceneItem(mesh, MissionScene.TransformOf(sceneObject), texture);
		built.Add(item);

		if (isPlayer) {
			playerItems.Add(item);
		}

		// Anything that can move needs its transform refreshed every frame. Structures never do, so
		// they stay on the one built here.
		if (sceneObject.Object is MechObject) {
			movers.Add((sceneObject, item));
		}
	}

	items = built.ToArray();

	// Piloting means sitting inside the machine, and its own geometry is all around the eye — the
	// cockpit node the camera rides is well inside the torso, so drawing it fills the canopy and
	// hides the world. Both lists share the same SceneItem objects, so the per-frame transform
	// refresh reaches whichever one is being drawn.
	pilotedItems = playerItems.Count > 0
		? built.Where(entry => !playerItems.Contains(entry)).ToArray()
		: items;
	keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;

	// Mouse events are queued here and nowhere else: everything that decides what a click means runs
	// once per frame in Update, out of CockpitInput.Drain. That is the original's own split — its
	// listener callback pushes a record and returns, and CockpitMouse_ProcessQueue does the work a
	// frame later (docs/formats/cockpit-input.md §3-4).
	if (input.Mice.Count > 0) {
		var mouse = input.Mice[0];

		// The pointer reports window-client pixels while the cockpit is placed in framebuffer pixels,
		// which differ on a high-DPI display. Rescaling here is the same correction the original makes
		// for the same reason — Mouse_RecomputeScale (0048078c) converts client coordinates into the
		// game's own space before any listener sees them (§2).
		void Queue(IMouse m, CockpitMouseButtons buttons) {
			var client = window.ClientSize;
			var framebuffer = window.FramebufferSize;
			cockpitInput.Enqueue(
				m.Position.X * framebuffer.X / Math.Max(client.X, 1),
				m.Position.Y * framebuffer.Y / Math.Max(client.Y, 1),
				buttons);
		}

		// The mask is built from the event's own button rather than read back off the device, so it
		// does not depend on whether Silk.NET updates the held state before or after it raises.
		mouse.MouseMove += (m, _) => Queue(m, ButtonsHeld(m));
		mouse.MouseDown += (m, button) => Queue(m, ButtonsHeld(m) | ButtonFlag(button));
		mouse.MouseUp += (m, button) => Queue(m, ButtonsHeld(m) & ~ButtonFlag(button));
	}

	// No backface culling. DTS geometry is not reliably wound — the WinForms model viewer reached
	// the same conclusion and never culls either — so culling would punch holes in the mech rather
	// than save fill rate. The shader shades two-sided to match.
	gl.Disable(EnableCap.CullFace);
};

window.Update += deltaSeconds => {
	imgui?.Update((float)deltaSeconds);

	// [Esc] opens and closes the debug panel. Read before the capture gate below, so the key that
	// opens the panel is also the key that closes it however ImGui feels about focus.
	debugPanel.ReadToggleKey(keyboard);

	// Everything below reads `controls` rather than the device itself: while the panel has keyboard
	// focus it is null, so piloting and camera keys go dead instead of the panel and the machine both
	// acting on the same keystroke.
	var controls = imgui != null && ImGui.GetIO().WantCaptureKeyboard ? null : keyboard;

	// [C] swaps between flying the observer camera and piloting the machine, on the key's own edge
	// so holding it does not flicker between the two.
	if (pilotMech != null && controls != null) {
		bool cameraKey = controls.IsKeyPressed(Key.C);
		if (cameraKey && !cameraKeyDown) {
			piloting = !piloting;
		}
		cameraKeyDown = cameraKey;

		// [V] swaps between sitting in the cockpit and watching the machine from behind it, on its own
		// edge for the same reason. The cockpit is not drawn in the external view, and the machine —
		// left out of the cockpit view because its geometry wraps the eye — is.
		bool externalViewKey = controls.IsKeyPressed(Key.V);
		if (externalViewKey && !externalViewKeyDown) {
			externalView = !externalView;
		}
		externalViewKeyDown = externalViewKey;
	}

	if (piloting && pilotMech != null && controls != null) {
		// Keypad [5], all stop: zero the throttle and let the gauge follow the machine this frame
		// rather than putting the old setting straight back. On its own edge, so holding it does not
		// fight a throttle the player is trying to open again.
		bool allStopKey = controls.IsKeyPressed(Key.Keypad5);
		if (allStopKey && !allStopKeyDown) {
			pilotMech.AllStop();
		}
		allStopKeyDown = allStopKey;

		// [[] and []], the manual's shield-balance keys — rear and forward. Both fire on their own
		// edge: the original clears the gauge's flag byte after acting on it, so a held key nudges
		// once, not once a tick. Nothing is spent moving the balance; it changes where the next
		// recharge tick puts the charge it is already holding.
		bool shieldRearKey = controls.IsKeyPressed(Key.LeftBracket);
		bool shieldFrontKey = controls.IsKeyPressed(Key.RightBracket);
		if (shieldRearKey && !shieldRearKeyDown) {
			pilotMech.Shields.AdjustBalance(towardFront: false);
		}
		if (shieldFrontKey && !shieldFrontKeyDown) {
			pilotMech.Shields.AdjustBalance(towardFront: true);
		}
		shieldRearKeyDown = shieldRearKey;
		shieldFrontKeyDown = shieldFrontKey;

		ApplyWeaponKeys(controls, pilotMech.Weapons);

		// Stick sign convention is the device's, not the game's: forward and left are negative. No
		// throttle lever, so the throttle's range spans both directions and holding [Down] takes the
		// machine through zero into reverse — see MechControls.ThrottleLever.
		//
		// [I]/[M]/[J]/[K] aim the turret and [Backspace] re-centres it, which is the manual's own
		// keyboard turret set. The turret's axes are rates, so holding a key sweeps it rather than
		// putting it somewhere; [Backspace] latches until either axis is touched again.
		//
		// [\] is the other half of that pair, Center Body: it walks the legs round under the turret
		// instead of bringing the turret back, taking the steering and the twist axis until they line
		// up. It latches on the keypress, and [Backspace] cancels it.
		pilotMech.Controls = new MechControls(
			(short)(Axis(controls, Key.Right, Key.Left, Key.Keypad6, Key.Keypad4) * MechControls.AxisFull),
			(short)(Axis(controls, Key.Down, Key.Up, Key.Keypad2, Key.Keypad8) * MechControls.AxisFull),
			ThrottleLever: 0,
			TorsoTwist: TurretAxis(Axis(controls, Key.K, Key.J), heldTwist),
			TorsoPitch: TurretAxis(Axis(controls, Key.I, Key.M), heldPitch),
			CenterTorso: controls.IsKeyPressed(Key.Backspace),
			CenterBody: controls.IsKeyPressed(Key.BackSlash));
	} else {
		scene.Camera.Input = ReadInput(controls);
		if (pilotMech != null) {
			pilotMech.Controls = MechControls.Neutral;
		}
	}

	// F1-F6 pick the MFD screen, the same keys and the same order as the original's own mode buttons
	// — button i of the display's F-key column dispatches SetMode(i), and this sets the same value.
	// Selecting one also pans back up to the cockpit, which is the manual's own rule for leaving the
	// Heads-Down Display ("select an MFD screen [F1]-[F6], press [Esc], or click the top of the
	// screen") and matches view command 1, the "up" half of the pair at 0042a3f4.
	if (controls != null && ReadMfdMode(controls) is { } requestedMfdMode) {
		hudState = hudState with { Mfd = requestedMfdMode };
		cockpitPan.Request(headsDown: false);
	}

	// F7 (Command Display) and F8 (Damage Detail) are the two HDD functions, and per the manual
	// either one opens the display — so each both pans down and selects its own screen, which is
	// what the display's own two page buttons dispatch (FUN_0044a5e4 with the button's index).
	if (cockpitHeadsDownTexture != null && controls != null) {
		if (controls.IsKeyPressed(Key.F7)) {
			hudState = hudState with { Hdd = HddPage.CommandDisplay };
			cockpitPan.Request(headsDown: true);
		} else if (controls.IsKeyPressed(Key.F8)) {
			hudState = hudState with { Hdd = HddPage.DamageDetail };
			cockpitPan.Request(headsDown: true);
		}
	}

	// The damage detail's three component categories, on the manual's own [S]/[I]/[W] bindings — the
	// same three the display's up/down arrow buttons step through. Only while that screen is actually
	// down: [S] and [W] are also two thirds of this host's camera movement, and the original has no
	// such clash because its own [S]/[I]/[W] only mean anything on this screen either.
	if (controls != null && cockpitPan.AtHeadsDown && hudState.Hdd == HddPage.DamageDetail
		&& ReadHddDamageView(controls) is { } damageView) {
		hudState = hudState with { HddDamage = damageView };
	}

	// Clicks are drained before the pan advances and before the sim ticks, so a click and the tick
	// that reacts to it keep a fixed order every frame — the point of queueing them in the first
	// place. The layout is rebuilt from this frame's pan position, which is the same one Render will
	// use, so what the player is clicking is what they are looking at.
	// Nothing to click while the cockpit is off screen, so the whole click path sits out the external
	// view rather than hit-testing a console the player cannot see — and likewise while the pointer is
	// over the debug panel, so a click on a checkbox is not also a click on the console behind it.
	if (cockpitArt != null && !ExternalViewActive()
			&& (imgui == null || !ImGui.GetIO().WantCaptureMouse)) {
		var framebuffer = window.FramebufferSize;
		var inputLayout = CockpitScreenLayout.Create(framebuffer.X, framebuffer.Y, cockpitArt,
			cockpitPan.OffsetRows, cockpitPan.TravelRows);

		foreach (var click in cockpitInput.Drain(deltaSeconds, inputLayout, cockpitArt, hudState)) {
			ApplyCockpitClick(click);
		}

		// The one draggable control. Dragging the slider sets the gauge, and the machine picks that up
		// on this frame's exchange unless its own input moved the throttle first.
		foreach (var drag in cockpitInput.Drags) {
			if (drag.Id.Kind == CockpitWidgetKind.Throttle && throttleTrack is { } track) {
				throttleGauge = track.ThrottleAt(drag.ArtY);

			}
		}

		// Held buttons draw depressed, and pop back up if the pointer slides off them still held.
		hudState = hudState with { PressedWidget = cockpitInput.Depressed };
	}

	cockpitPan.Advance(deltaSeconds);

	// Clamping the accumulator stops a long stall (a breakpoint, a window drag) from turning into
	// a burst of catch-up ticks that would teleport everything.
	tickAccumulator = Math.Min(tickAccumulator + deltaSeconds, MaxAccumulatedSeconds);
	while (tickAccumulator >= SecondsPerTick) {
		scene.World.Tick();
		tickAccumulator -= SecondsPerTick;
	}

	foreach (var (sceneObject, item) in movers) {
		item.Transform = MissionScene.TransformOf(sceneObject);
	}

	// Each node of an animating machine is re-read here, alongside the whole-object transforms above.
	// Reading more often than the simulation ticks costs nothing and gains nothing: the thread's
	// intra-frame fraction only moves in Advance, so consecutive reads between ticks return the same
	// pose. That is the original's cadence too — see mech-locomotion.md's "Evaluation cadence".
	foreach (var (mech, transformId, item) in posedParts) {
		item.Transform = MissionScene.PosedTransformOf(mech, transformId);
	}

	if (piloting && pilotMech != null) {
		if (externalView) {
			// Fixed chase view, ~10 m behind the machine. Placeholder geometry — see ExternalCamera.
			ExternalCamera.Place(camera, pilotMech, terrain);
		} else {
			// The eye rides the model node the type record names, so the walk cycle's bob comes with it —
			// see MechObject.EyePosition. Camera yaw runs opposite to a simulation heading; see
			// MissionScene.TransformOf.
			//
			// The debug panel's "steady eye" pins the eye's *height* to whatever it was the moment the
			// toggle went on and leaves everything else — the machine's own travel, its lean, the eye's
			// fore/aft swing — alone. That isolates the vertical bob from the ride without touching the
			// animation that produces either, which is the A/B for "is it the eye or the machine?".
			var eyeFrame = pilotMech.EyeTransform;
			var eye = debugPanel.PinEyeHeight(
				new Vec3i(eyeFrame.X, eyeFrame.Y, eyeFrame.Z), pilotMech.Position);

			// Orientation comes off the eye node too, not off the machine's heading: the camera node
			// hangs below the two nodes the torso sequences drive, so twisting and pitching the turret
			// turns the view without anything here having to add the angles in. On a walking machine
			// with the turret centred this is exactly the old heading-only camera — the walk cycle
			// moves the eye but does not rotate it, measured at zero swing across the fleet.
			var look = eyeFrame.ToEuler();
			camera.Position = eye;
			camera.Yaw = -look.Z & 0xffff;
			camera.Pitch = look.X;
		}

		// Keep the observer camera on the machine, so switching to it lands where the player was
		// rather than wherever it was parked at mission start.
		scene.Camera.Position = camera.Position;
		scene.Camera.Heading = camera.Yaw;
	} else {
		scene.Camera.ApplyTo(camera);
	}

	// What the debug panel reports about the walk — see DebugPanel.Sample for why it is measured
	// every frame rather than only while the panel is up.
	debugPanel.Sample(pilotMech);

	if (cockpitArt != null && pilotMech != null) {
		// Player_PerFrameCockpitUpdate's own order: the weapon manager's pass, then the gauge and the
		// machine settle which of them moved this frame, then the readouts are taken from the machine.
		pilotMech.Weapons.PerFrameUpdate();
		throttleGauge = pilotMech.ExchangeCockpitThrottle(throttleGauge);
		hudState = hudState with {
			SpeedKph = pilotMech.DisplaySpeedKph,
			Throttle = throttleGauge,
			TorsoTwist = pilotMech.TorsoTwistAngle,
			ShieldFront = pilotMech.Shields.FrontReadout,
			ShieldRear = pilotMech.Shields.RearReadout,
			EnergyFraction = pilotMech.EnergyPoolFraction,
			Weapons = WeaponRowState.Build(pilotMech.Weapons,
				cockpitArt.Gau.WeaponListTotal, cockpitArt.Strings),
			ChainGroup = pilotMech.Weapons.Group,
			AutoTrack = pilotMech.Weapons.AutoTrack,
		};
	}
};

window.Render += (_, gl) => {
	if (renderer == null || overlay == null || items == null) {
		return;
	}

	var size = window.FramebufferSize;
	renderer.Clear();

	// The shield meter's rings are canopy pixels, not HUD geometry, and the original relights them by
	// rewriting six palette slots every frame. Decoding the art at load baked that palette in, so the
	// live version repaints those pixels and re-uploads — which UpdateShieldRings only asks for on the
	// frames where a ring's colour actually changed, so a settled array costs one comparison.
	if (cockpitArt != null && pilotMech != null && cockpitFrontTexture != null) {
		var shieldRings = pilotMech.Shields;
		bool repainted = cockpitArt.UpdateShieldRings(
			CockpitPalette.ShieldFacingCharge(shieldRings.Front, shieldRings.BaseMax),
			CockpitPalette.ShieldFacingCharge(shieldRings.Rear, shieldRings.BaseMax));

		if (repainted) {
			var frontFrame = cockpitArt.Front;
			cockpitFrontTexture.Update(frontFrame.Pixels, frontFrame.Width, frontFrame.Height);
			cockpitSideTexture?.Update(cockpitArt.Side.Pixels, cockpitArt.Side.Width, cockpitArt.Side.Height);
			if (cockpitArt.HeadsDown is { } headsDownFrame && cockpitHeadsDownTexture != null) {
				cockpitHeadsDownTexture.Update(headsDownFrame.Pixels, headsDownFrame.Width, headsDownFrame.Height);
			}
		}
	}

	// The external view is drawn as one full-window 3D view with no canopy over it — there is no
	// cockpit to see from outside the machine.
	if (cockpitArt != null && cockpitFrontTexture != null && cockpitSideTexture != null
			&& !ExternalViewActive()) {
		DrawThreePanelCockpitView(gl, size.X, size.Y);
	} else {
		renderer.Render(camera, VisibleItems(), 0, 0, size.X, size.Y);
		DrawSkeleton(camera, size.X, size.Y);
	}

	// The panel goes over whatever view is up. The cockpit path above draws through three sub-window
	// viewports and leaves the last one set, so the full-window viewport is restored first —
	// otherwise the panel is squeezed into the right-hand cockpit panel's rectangle and mostly
	// scissored away, which is why it only ever appeared in the external view.
	gl.Viewport(0, 0, (uint)Math.Max(size.X, 1), (uint)Math.Max(size.Y, 1));
	debugPanel.Draw(
		new DebugPanelContext(piloting, externalView, pilotMech,
			scene.PlayerObject?.Model?.Segments.Length ?? 0, terrain),
		size.Y);

	imgui?.Render();

	framesRendered++;
	if (screenshotPath != null && !screenshotTaken && framesRendered >= 30) {
		screenshotTaken = true;
		CaptureScreenshot(gl, size.X, size.Y, screenshotPath);
		window.Close();
	}
};

window.Closing += () => {
	imgui?.Dispose();
	renderer?.Dispose();
	overlay?.Dispose();
	wireframe?.Dispose();
	terrainMesh?.Dispose();
	terrainTexture?.Dispose();
	cockpitFrontTexture?.Dispose();
	cockpitSideTexture?.Dispose();
	cockpitHeadsDownTexture?.Dispose();
	hudSpriteTexture?.Dispose();
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
//
// The whole composite also slides vertically with the Heads-Down Display pan. The original's cockpit
// is a canvas twice the screen's height with the forward view's art at canvas row 0 and the HDD's at
// row 474, both blitted during mission bring-up, and switching between them is a scroll of the
// display window over that canvas — never a redraw (see CockpitViewGeometry, and CockpitPan for the
// transition's own machinery). This reproduces the same geometry with two quads at fixed canvas
// offsets and a moving window: the three cockpit panels are offset up by the pan distance, the HDD
// panel sits one travel-distance below them, and the 3D viewports ride along with their panels so
// the world scrolls out of frame exactly as the art does. The two views' art overlaps by six rows on
// the canvas — HB1 starts at row 474 and HB0 runs to 479 — and the original resolves that by blitting
// view 1 before view 0, so the draw order below does the same.
void DrawThreePanelCockpitView(GL gl, int totalWidth, int totalHeight) {
	// One placement for the whole frame, shared with the input path so a widget's click region cannot
	// drift from the art it was drawn over — see CockpitScreenLayout.
	var layout = CockpitScreenLayout.Create(totalWidth, totalHeight, cockpitArt!,
		cockpitPan.OffsetRows, cockpitPan.TravelRows);

	// Both side panels are the same width, so one offset serves them mirrored about the centre.
	float centerAspect = (float)layout.Center.Viewport.Width / Math.Max(totalHeight, 1);
	float sideAspect = (float)layout.Left.Viewport.Width / Math.Max(totalHeight, 1);
	int sideYawOffset = CockpitViewLayout.SideYawOffset(camera.FieldOfView, centerAspect, sideAspect);

	// First, so the six-row overlap where the two views' art meets on the canvas resolves the way the
	// original's VRAM does. Sim_InitMissionSession (004614fc) blits view 1 and then view 0, so HB0's
	// bottom rows win over HB1's top rows and no sliver of the HDD shows under the dashboard at rest.
	if (cockpitHeadsDownTexture != null && layout.HeadsDown is { } headsDown) {
		overlay!.DrawHeadsDown(headsDown.Viewport.X, headsDown.Viewport.Y,
			headsDown.Viewport.Width, headsDown.Viewport.Height,
			cockpitHeadsDownTexture, headsDown.ArtWidth, headsDown.ArtHeight,
			cockpitArt, hudSpriteTexture, hudState);
	}

	// GL's viewport origin is bottom-left, so a positive y offset moves a panel up the screen — which
	// is the direction the cockpit travels as the view pans down the canvas.
	DrawPanel(layout.Left, -sideYawOffset, cockpitSideTexture!, mirrorHorizontally: true, hud: null);
	DrawPanel(layout.Center, 0, cockpitFrontTexture!, mirrorHorizontally: false, hud: cockpitArt);
	DrawPanel(layout.Right, sideYawOffset, cockpitSideTexture!, mirrorHorizontally: false, hud: null);

	void DrawPanel(CockpitScreenLayout.PlacedSurface surface, int yawOffset, GpuTexture texture,
			bool mirrorHorizontally, CockpitArt? hud) {
		var viewport = surface.Viewport;
		var panelCamera = ClonePanelCamera(camera, yawOffset);
		renderer!.Render(panelCamera, VisibleItems(),
			viewport.X, viewport.Y, viewport.Width, viewport.Height);

		// Before the canopy goes over it, so the skeleton is clipped by the viewport hole like the rest
		// of the world. Mostly of use with the machine's own model hidden, but it costs one draw call.
		DrawSkeleton(panelCamera, viewport.Width, viewport.Height);
		overlay!.Draw(viewport.X, viewport.Y, viewport.Width, viewport.Height, texture,
			surface.ArtWidth, surface.ArtHeight, mirrorHorizontally, hud,
			spriteTexture: hudSpriteTexture, hudState: hudState);
	}
}

// The animating skeleton, drawn over whatever was just rendered into the current viewport.
//
// This is the only view of the animation system there is. The mesh is baked at the shape's default
// pose and drawn with one matrix per object (see DtsMeshBuilder.ResolveGroupOffset), so a playing
// walk cycle moves nothing on screen; before this, the only observable output of the whole thread
// was where the player's eye ended up. Bones are drawn through solid geometry on purpose — the
// skeleton is inside the model it belongs to.
void DrawSkeleton(Camera view, int viewportWidth, int viewportHeight) {
	if (!debugPanel.DrawSkeleton || wireframe == null || pilotMech == null) {
		return;
	}

	var joints = SkeletonPose.Build(pilotMech);
	debugPanel.SkeletonJointCount = joints.Length;
	if (joints.Length == 0) {
		return;
	}

	float aspect = (float)viewportWidth / Math.Max(viewportHeight, 1);
	wireframe.DrawLines(view, SkeletonWireframe.Build(joints), new Vector3(0.2f, 1f, 0.85f), aspect);

	// The node the eye rides, flagged in its own colour: it is the one joint whose motion the player
	// actually feels, so it wants to be findable among the rest.
	int cameraNode = SkeletonPose.CameraTransformId(pilotMech);
	if (cameraNode >= 0 && cameraNode < joints.Length) {
		wireframe.DrawLines(view,
			SkeletonWireframe.Marker(joints[cameraNode].World, SkeletonWireframe.CameraCrossMeters),
			new Vector3(1f, 0.85f, 0.1f), aspect);
	}
}

// A completed click, routed to the same state changes the corresponding key already makes — the
// original's buttons and its keyboard bindings dispatch the same calls, so the two agree here by
// construction rather than by two parallel implementations.
//
// The buttons with nothing behind them yet are deliberately silent rather than stubbed: the MFD's
// SELECT/RANGE/TARGET/XMIT/PASS/ACTIVE, the Heads-Down Display's map arrows and zoom, its comm boxes
// and XMIT/CANCEL all need squad, target or map state the engine does not have. They still hit-test
// and will still light on press; they simply do nothing on release.
void ApplyCockpitClick(CockpitClick click) {
	switch (click.Id.Kind) {
		case CockpitWidgetKind.MfdButton when click.Id.Index < MfdLayout.ModeCount:
			// Button i of the F-key column dispatches SetMode(i), and picking a screen pans back up —
			// the manual's own rule for leaving the Heads-Down Display.
			hudState = hudState with { Mfd = (MfdMode)click.Id.Index };
			cockpitPan.Request(headsDown: false);
			break;

		case CockpitWidgetKind.HddWidget:
			ApplyHddClick(click.Id.AsHddWidget!.Value);
			break;

		// A weapon row: the left button arms it, the right button adds or removes it from the current
		// fire chain. Both are the row gadget's one click handler (FUN_00440ef0 / FUN_004414b4)
		// branching on the mouse-button bit its GetValue slot returns.
		case CockpitWidgetKind.WeaponRow when pilotMech != null:
			if (click.Button.HasFlag(CockpitMouseButtons.Right)) {
				pilotMech.Weapons.ToggleChain(click.Id.Index);
			} else {
				pilotMech.Weapons.SelectBySlot(click.Id.Index);
			}

			break;

		case CockpitWidgetKind.ConsoleButton when pilotMech != null:
			ApplyConsoleClick(click.Id.AsConsoleButton!.Value);
			break;
	}
}

// The three console buttons, from FUN_0044212c's own child switch. TRACK's flag is latched here
// because that is what makes the button look right; nothing reads it — automatic turret tracking is
// not ported.
void ApplyConsoleClick(ConsoleButton button) {
	if (pilotMech == null) {
		return;
	}

	switch (button) {
		case ConsoleButton.Chain:
			pilotMech.Weapons.SetGroup((pilotMech.Weapons.Group + 1) % WeaponMounts.GroupCount);
			break;

		case ConsoleButton.Link:
			pilotMech.Weapons.ToggleLink();
			break;

		case ConsoleButton.Track:
			pilotMech.Weapons.AutoTrack = !pilotMech.Weapons.AutoTrack;
			break;
	}
}

void ApplyHddClick(HddLayout.Widget widget) {
	switch (widget) {
		// The two page buttons dispatch FUN_0044a5e4 with their own index, and either one opens the
		// display — the same pairing F7 and F8 have.
		case HddLayout.Widget.PageButton0:
			hudState = hudState with { Hdd = HddPage.CommandDisplay };
			cockpitPan.Request(headsDown: true);
			break;

		case HddLayout.Widget.PageButton1:
			hudState = hudState with { Hdd = HddPage.DamageDetail };
			cockpitPan.Request(headsDown: true);
			break;

		// On the damage screen the up and down arrows step the component category, which is the same
		// three [S]/[I]/[W] select. They wrap, so the pair walks the list either way without dead ends.
		// On the command display the same two arrows scroll the map, which has no rasterizer yet.
		case HddLayout.Widget.ArrowUp or HddLayout.Widget.ArrowDown
			when hudState.Hdd == HddPage.DamageDetail:
			const int views = 3;
			int step = widget == HddLayout.Widget.ArrowUp ? views - 1 : 1;
			hudState = hudState with {
				HddDamage = (HddDamageView)(((int)hudState.HddDamage + step) % views),
			};
			break;
	}
}

// What this frame's cameras draw. The player's own machine is left out while looking out of its
// cockpit: the cockpit node the eye rides sits well inside the torso, so its geometry would wrap the
// camera and fill the canopy. The observer camera and the external view both put it back, which is
// the only way to see the machine you are flying.
SceneItem[] VisibleItems() =>
	(piloting && !externalView ? pilotedItems : items) ?? Array.Empty<SceneItem>();

// Whether this frame is being drawn from the external camera. Only meaningful while piloting — the
// free camera already draws the whole scene with no cockpit over it.
bool ExternalViewActive() => externalView && piloting && pilotMech != null;

// The weapon panel's keyboard set, on the manual's own bindings. Every one of these reaches exactly
// the same call the corresponding mouse action does — the original routes them together too, through
// the cockpit's ten-gauge array (CockpitViewInstance+0x70) and the console button panel.
//
//   [1]..[0]        arm that row                       -> FUN_004110ac's sibling, FUN_004106ac
//   [Alt]+[1]..[0]  add/remove that row from the chain -> FUN_004110ac
//   [W] / [Alt]+[W] step the armed weapon forward/back -> FUN_0041074c
//   [L]             toggle link fire on the armed pair -> FUN_00410f14
//
// All fire on their own key-down edge: they are toggles and steps, not held states.
void ApplyWeaponKeys(IKeyboard keyboard, WeaponMounts mounts) {
	bool alt = keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight);

	for (int slot = 0; slot < weaponRowKeys.Length; slot++) {
		bool down = keyboard.IsKeyPressed(weaponRowKeys[slot]);
		if (down && !weaponRowKeyDown[slot]) {
			if (alt) {
				mounts.ToggleChain(slot);
			} else {
				mounts.SelectBySlot(slot);
			}
		}

		weaponRowKeyDown[slot] = down;
	}

	bool cycleKey = keyboard.IsKeyPressed(Key.W);
	if (cycleKey && !cycleWeaponKeyDown) {
		mounts.CycleSelection(alt ? -1 : 1);
	}

	cycleWeaponKeyDown = cycleKey;

	bool linkKey = keyboard.IsKeyPressed(Key.L);
	if (linkKey && !linkKeyDown) {
		mounts.ToggleLink();
	}

	linkKeyDown = linkKey;
}

/// <summary>Every mouse button currently held, as the cockpit's own flag pair.</summary>
static CockpitMouseButtons ButtonsHeld(IMouse mouse) =>
	(mouse.IsButtonPressed(MouseButton.Left) ? CockpitMouseButtons.Left : CockpitMouseButtons.None)
	| (mouse.IsButtonPressed(MouseButton.Right) ? CockpitMouseButtons.Right : CockpitMouseButtons.None);

/// <summary>One button as its flag. Anything but left and right is <see cref="CockpitMouseButtons.None"/> — the original watches only those two.</summary>
static CockpitMouseButtons ButtonFlag(MouseButton button) => button switch {
	MouseButton.Left => CockpitMouseButtons.Left,
	MouseButton.Right => CockpitMouseButtons.Right,
	_ => CockpitMouseButtons.None,
};

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

// The MFD screen the function keys are asking for, or null when none of them is down — returning null
// rather than a default keeps the display on whatever screen it was already showing.
static MfdMode? ReadMfdMode(IKeyboard keyboard) {
	Key[] keys = { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6 };
	for (int i = 0; i < keys.Length; i++) {
		if (keyboard.IsKeyPressed(keys[i])) {
			return (MfdMode)i;
		}
	}

	return null;
}

// The Heads-Down damage screen's component category, or null when none of its three keys is down —
// same rule as ReadMfdMode: returning null leaves the screen on whatever it was already showing.
static HddDamageView? ReadHddDamageView(IKeyboard keyboard) {
	if (keyboard.IsKeyPressed(Key.S)) {
		return HddDamageView.Structural;
	}

	if (keyboard.IsKeyPressed(Key.I)) {
		return HddDamageView.Internal;
	}

	return keyboard.IsKeyPressed(Key.W) ? HddDamageView.Weapons : null;
}

// One signed axis from a pair of keys, plus optional aliases for each direction — the arrow cluster
// and the numeric keypad are the same key on the hardware the manual is describing, and a host window
// sees them as two.
static int Axis(IKeyboard keyboard, Key positive, Key negative,
		Key? positiveAlias = null, Key? negativeAlias = null) {
	bool up = keyboard.IsKeyPressed(positive) || (positiveAlias is { } p && keyboard.IsKeyPressed(p));
	bool down = keyboard.IsKeyPressed(negative) || (negativeAlias is { } n && keyboard.IsKeyPressed(n));
	return (up ? 1 : 0) - (down ? 1 : 0);
}

/// <summary>
/// One turret axis: the key pair, or whatever <c>--turret</c> is holding when no key is down. Never
/// past full deflection, so holding a key during a <c>--turret</c> run cannot ask for more rate than
/// a stick can.
/// </summary>
static short TurretAxis(int keys, short held) =>
	keys != 0 ? (short)(keys * MechControls.AxisFull) : held;
