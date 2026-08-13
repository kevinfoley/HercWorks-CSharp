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

	public EngineWindow(string title = "HERCULAN Engine", int width = 1280, int height = 720) {
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

	/// <summary>Window title, so a host can show live diagnostics without owning the window type.</summary>
	public string Title {
		get => _window.Title;
		set => _window.Title = value;
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
		_input?.Dispose();
		_input = null;
	}

	public void Dispose() {
		_input?.Dispose();
		_gl?.Dispose();
		_window.Dispose();
	}
}
