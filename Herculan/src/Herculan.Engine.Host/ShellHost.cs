using Herculan.Engine.Content;
using Herculan.Engine.Shell;
using Silk.NET.Input;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Host;

/// <summary>
/// Runs the front end instead of a mission — <c>--shell</c>. The same thin-host arrangement the
/// mission loop uses (docs/engine/planning.md, "Engine internal architecture"): everything here is
/// wiring, and every rule about what the shell looks like and where its widgets are lives in
/// <c>Herculan.Engine.Shell</c>.
///
/// <para>It is a separate entry point rather than a mode of the mission loop because the two share
/// nothing: the shell mounts different archives, loads no zone, runs no simulation and needs no fixed
/// timestep. In the retail game they are two executables for the same reason
/// (docs/shell/campaign-loop.md).</para>
///
/// <para>The pointer is polled once per update rather than queued from the device's own events, which
/// is the opposite of what the cockpit does. The cockpit's queue is reproducing DBSIM's own deferred
/// mouse handling (docs/formats/cockpit-input.md); nothing has been read yet that says VSHELL does
/// the same, so this stays the simple thing until it is.</para>
/// </summary>
static class ShellHost {
	/// <summary>
	/// Frames to let pass before <c>--screenshot</c> fires. The shell has nothing to settle — no
	/// simulation, no streaming — but the window manager can hand back a stale or part-sized
	/// framebuffer for the first frame or two, so the capture waits the same short beat the mission
	/// host waits.
	/// </summary>
	private const int ScreenshotFrame = 5;

	public static int Run(string installRoot, string? paletteName, string? screenshotPath = null) {
		var content = GameContent.Mount(GameInstall.ArchiveDirectory(installRoot), ShellArt.Archives);
		Console.WriteLine($"Mounted archives: {string.Join(", ", content.MountedArchives)}");

		var art = ShellArt.Load(content, paletteName);
		if (art == null) {
			Console.Error.WriteLine(
				$"Could not load the shell's art from {GameInstall.ArchiveDirectory(installRoot)}.\n" +
				$"It needs {string.Join(" and ", ShellArt.Archives)}, a "
				+ $"dpl\\{paletteName ?? ShellArt.DefaultPaletteName}.DPL palette and "
				+ $"dbm\\{ShellArt.BackdropName}.DBM.");
			return 1;
		}

		Console.WriteLine(
			$"Shell art loaded — {ShellArt.BackdropName} backdrop {art.Backdrop.Width}x{art.Backdrop.Height}, "
			+ $"palette {paletteName ?? ShellArt.DefaultPaletteName}. "
			+ (art.Sprites is { } sheet
				? $"Banks and fonts: {string.Join(", ", sheet.BankNames)} in a "
				  + $"{sheet.Atlas.Width}x{sheet.Atlas.Height} atlas."
				: "No sprite banks or fonts could be loaded — backdrop only."));

		var screen = ShellScreen.CreateFrame(art.Text);
		Console.WriteLine(art.Text != null
			? $"Tabs: {string.Join(", ", screen.Buttons.Where(b => b.Caption != null).Select(b => b.Caption))}"
			: "No estext.bin — the tabs draw their plates and no captions.");
		Console.WriteLine("The tab strip is the whole of the shell so far; no tab has a screen behind it "
			+ "yet. Click one to latch it. Close the window to quit.");

		using var window = new EngineWindow("HERCULAN Engine — shell");

		ShellRenderer? renderer = null;
		IMouse? mouse = null;
		bool pointerHeld = false;
		int framesRendered = 0;

		window.Load += (gl, input) => {
			renderer = new ShellRenderer(gl, art);
			mouse = input.Mice.Count > 0 ? input.Mice[0] : null;
		};

		window.Update += _ => {
			if (mouse == null) {
				return;
			}

			// The pointer reports window-client pixels while the canvas is placed in framebuffer pixels,
			// which differ on a scaled display — the same correction the mission host makes.
			var client = window.ClientSize;
			var framebuffer = window.FramebufferSize;
			float windowX = mouse.Position.X * framebuffer.X / Math.Max(client.X, 1);
			float windowY = mouse.Position.Y * framebuffer.Y / Math.Max(client.Y, 1);

			var layout = ShellScreenLayout.Create(framebuffer.X, framebuffer.Y);
			var (canvasX, canvasY) = layout.WindowToCanvas(windowX, windowY);

			bool held = mouse.IsButtonPressed(MouseButton.Left);
			if (held && !pointerHeld) {
				screen.PointerDown(canvasX, canvasY);
			} else if (!held && pointerHeld) {
				if (screen.PointerUp(canvasX, canvasY) is { } activated) {
					Activate(activated);
				}
			} else {
				screen.PointerMoved(canvasX, canvasY);
			}

			pointerHeld = held;
		};

		window.Render += (_, gl) => {
			// Black behind the canvas: the shell is a fixed 640x480 layout scaled to the window, so a
			// window that is not 4:3 has margin left over and the original has nothing to put in it.
			gl.ClearColor(0f, 0f, 0f, 1f);
			gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			var framebuffer = window.FramebufferSize;
			renderer?.Draw(ShellScreenLayout.Create(framebuffer.X, framebuffer.Y), screen);

			framesRendered++;
			if (screenshotPath != null && framesRendered == ScreenshotFrame) {
				Screenshot.Capture(gl, framebuffer.X, framebuffer.Y, screenshotPath);
				window.Close();
			}
		};

		window.Closing += () => {
			renderer?.Dispose();
			renderer = null;
		};

		window.Run();
		return 0;

		void Activate(int id) {
			if (id == ShellScreen.MenuButtonId) {
				Console.WriteLine("Menu button — no screen behind it yet.");
				return;
			}

			screen.SelectTab(id);
			Console.WriteLine($"Tab {id}"
				+ (screen.Button(id)?.Caption is { } caption ? $" ({caption})" : string.Empty)
				+ " — no screen behind it yet.");
		}
	}
}
