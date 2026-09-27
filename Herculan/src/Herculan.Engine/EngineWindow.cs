using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Herculan.Engine;

/// <summary>
/// A Silk.NET window with an OpenGL context and an input context — the engine's platform surface,
/// meant to be driven by a thin front-end host rather than assuming it is the only thing running a
/// loop (see docs/engine/planning.md, "Engine internal architecture").
///
/// <para>It owns no scene state and does no drawing of its own: it raises <see cref="Load"/> once
/// the GL and input contexts exist, <see cref="Update"/> for simulation, and <see cref="Render"/>
/// for drawing, and leaves clearing and depth state to whatever renders (see
/// <see cref="Render.SceneRenderer"/>). That separation is what lets a second host — a mission
/// editor, a screenshot tool — reuse the same engine libraries without inheriting a game loop's
/// assumptions.</para>
/// </summary>
public sealed class EngineWindow : IDisposable {
	private readonly IWindow _window;
	private GL? _gl;
	private IInputContext? _input;

	/// <summary>Raised once, after the GL and input contexts are created.</summary>
	public event Action<GL, IInputContext>? Load;

	/// <summary>Raised each frame before <see cref="Render"/>, with the elapsed seconds.</summary>
	public event Action<double>? Update;

	/// <summary>Raised each frame to draw, with the elapsed seconds and the GL context.</summary>
	public event Action<double, GL>? Render;

	/// <summary>Raised when the window is closing, while the GL context is still active.</summary>
	public event Action? Closing;

	public EngineWindow(string title = "HERCULAN Engine", int width = 1280, int height = 960) {
		var options = WindowOptions.Default with {
			Size = new Vector2D<int>(width, height),
			Title = title,
			// Asked for explicitly rather than relying on the default, since a context without a
			// depth buffer fails silently: depth testing simply does nothing and the scene renders
			// as whatever was drawn last, which is a confusing symptom to chase.
			PreferredDepthBufferBits = 24,
		};

		_window = Window.Create(options);
		_window.Load += OnLoad;
		_window.Update += OnUpdate;
		_window.Render += OnRender;
		_window.Closing += OnClosing;
	}

	/// <summary>Current framebuffer size in pixels — the viewport a renderer should draw into.</summary>
	public Vector2D<int> FramebufferSize => _window.FramebufferSize;

	/// <summary>
	/// Current client size in window coordinates, which is the space the pointer reports in. Equal to
	/// <see cref="FramebufferSize"/> except on a scaled display, where input has to be converted
	/// between the two.
	/// </summary>
	public Vector2D<int> ClientSize => _window.Size;

	/// <summary>
	/// The underlying view, for integrations that need it directly — e.g. ImGui's
	/// <c>ImGuiController</c>, which hooks resize itself rather than going through
	/// <see cref="Render"/>/<see cref="Update"/>.
	/// </summary>
	public IView View => _window;

	/// <summary>Window title, so a host can show live diagnostics without owning the window type.</summary>
	public string Title {
		get => _window.Title;
		set => _window.Title = value;
	}

	/// <summary>Whether <see cref="ToggleFullScreen"/> has the window covering its monitor.</summary>
	public bool FullScreen { get; private set; }

	private Vector2D<int> _windowedSize;

	/// <summary>
	/// Takes the window to full screen or back. Full screen is <c>glfwSetWindowMonitor</c> on the monitor
	/// under the window's centre at that monitor's current video mode — GLFW's own windowed full screen,
	/// which changes no display mode: the window covers the monitor at its own resolution and Windows
	/// treats it as full screen, taskbar included. GLFW minimises a full-screen window that loses the
	/// focus. Back is the size the window had before, centred on the same monitor. Called directly rather
	/// than through Silk's <see cref="WindowState.Fullscreen"/>, which makes the same call but always on
	/// the primary monitor. Does nothing on a backend other than GLFW.
	/// </summary>
	public unsafe void ToggleFullScreen() {
		if (_window.Native?.Glfw is not { } native) {
			return;
		}

		var glfw = Glfw.GetApi();
		var handle = (WindowHandle*)native;

		if (!FullScreen) {
			var monitor = MonitorUnderWindow(glfw, handle);
			if (monitor == null) {
				return;
			}

			var mode = glfw.GetVideoMode(monitor);
			_windowedSize = _window.Size;
			_fullScreenMonitor = (nint)monitor;
			glfw.SetWindowMonitor(handle, monitor, 0, 0, mode->Width, mode->Height, mode->RefreshRate);
			FullScreen = true;
			return;
		}

		var from = (Silk.NET.GLFW.Monitor*)_fullScreenMonitor;
		glfw.GetMonitorPos(from, out int monitorX, out int monitorY);
		var desktop = glfw.GetVideoMode(from);
		glfw.SetWindowMonitor(handle, null,
			monitorX + (desktop->Width - _windowedSize.X) / 2, monitorY + (desktop->Height - _windowedSize.Y) / 2,
			_windowedSize.X, _windowedSize.Y, Glfw.DontCare);
		FullScreen = false;
	}

	private nint _fullScreenMonitor;

	/// <summary>The monitor whose area holds the window's centre, or the primary one when none does.</summary>
	private static unsafe Silk.NET.GLFW.Monitor* MonitorUnderWindow(Glfw glfw, WindowHandle* handle) {
		glfw.GetWindowPos(handle, out int x, out int y);
		glfw.GetWindowSize(handle, out int width, out int height);
		int centreX = x + width / 2;
		int centreY = y + height / 2;

		var monitors = glfw.GetMonitors(out int count);
		for (int i = 0; i < count; i++) {
			glfw.GetMonitorPos(monitors[i], out int monitorX, out int monitorY);
			var mode = glfw.GetVideoMode(monitors[i]);
			if (centreX >= monitorX && centreX < monitorX + mode->Width
					&& centreY >= monitorY && centreY < monitorY + mode->Height) {
				return monitors[i];
			}
		}

		return glfw.GetPrimaryMonitor();
	}

	public void Run() => _window.Run();

	public void Close() => _window.Close();

	private void OnLoad() {
		_gl = _window.CreateOpenGL();
		_input = _window.CreateInput();
		Load?.Invoke(_gl, _input);
	}

	private void OnUpdate(double deltaSeconds) => Update?.Invoke(deltaSeconds);

	private void OnRender(double deltaSeconds) {
		if (_gl != null) {
			Render?.Invoke(deltaSeconds, _gl);
		}
	}

	private void OnClosing() {
		Closing?.Invoke();
		_input?.Dispose();
		_input = null;
	}

	public void Dispose() {
		_input?.Dispose();
		_gl?.Dispose();
		_window.Dispose();
	}
}
