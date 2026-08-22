using System.Numerics;
using Herculan.Engine.Gl;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Render;

/// <summary>
/// Draws flat-coloured line geometry — currently just the editor's selection box — with its own
/// minimal shader rather than reusing <see cref="SceneRenderer"/>'s, which expects a normal/UV
/// vertex layout this has no use for.
/// </summary>
public sealed class WireframeRenderer : IDisposable {
	private const string VertexShaderSource = """
		#version 330 core
		layout (location = 0) in vec3 aPosition;

		uniform mat4 uModel;
		uniform mat4 uView;
		uniform mat4 uProjection;

		void main() {
			gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
		}
		""";

	private const string FragmentShaderSource = """
		#version 330 core
		uniform vec3 uColor;
		out vec4 FragColor;

		void main() {
			FragColor = vec4(uColor, 1.0);
		}
		""";

	private readonly GL _gl;
	private readonly ShaderProgram _shader;
	private readonly GpuLineMesh _unitCube;
	private readonly GpuLineMesh _scratch;

	public WireframeRenderer(GL gl) {
		_gl = gl;
		_shader = new ShaderProgram(gl, VertexShaderSource, FragmentShaderSource);
		_unitCube = GpuLineMesh.CreateUnitCube(gl);
		_scratch = new GpuLineMesh(gl, ReadOnlySpan<Vector3>.Empty, dynamic: true);
	}

	/// <summary>
	/// Draws a cube centered at <paramref name="centerRender"/> with the given half-extent, both in
	/// render units.
	/// </summary>
	public void DrawBox(Camera camera, Vector3 centerRender, float halfExtentRender, Vector3 color,
			float aspectRatio) {
		_shader.Use();
		_shader.SetMatrix("uModel",
			Matrix4x4.CreateScale(halfExtentRender) * Matrix4x4.CreateTranslation(centerRender));
		_shader.SetMatrix("uView", camera.ViewMatrix);
		_shader.SetMatrix("uProjection", camera.ProjectionMatrix(aspectRatio));
		_shader.SetVector3("uColor", color);

		_unitCube.Draw();
	}

	/// <summary>
	/// Draws a line list already in world (render) space — <paramref name="segments"/> is read two
	/// vertices at a time, so an odd trailing vertex is ignored. Rebuilt geometry goes through one
	/// shared buffer, so a caller drawing several colours issues several calls rather than holding
	/// several meshes.
	/// </summary>
	/// <param name="throughGeometry">
	/// Draw with depth testing off, so the lines show through solid geometry. On by default because
	/// a skeleton sits inside the model it belongs to and would otherwise be invisible from outside
	/// it. <see cref="SceneRenderer.Render"/> turns depth testing back on for the next thing drawn.
	/// </param>
	public void DrawLines(Camera camera, ReadOnlySpan<Vector3> segments, Vector3 color,
			float aspectRatio, bool throughGeometry = true) {
		if (segments.Length < 2) {
			return;
		}

		if (throughGeometry) {
			_gl.Disable(EnableCap.DepthTest);
		}

		_scratch.SetVertices(segments);

		_shader.Use();
		_shader.SetMatrix("uModel", Matrix4x4.Identity);
		_shader.SetMatrix("uView", camera.ViewMatrix);
		_shader.SetMatrix("uProjection", camera.ProjectionMatrix(aspectRatio));
		_shader.SetVector3("uColor", color);

		_scratch.Draw();

		if (throughGeometry) {
			_gl.Enable(EnableCap.DepthTest);
		}
	}

	public void Dispose() {
		_shader.Dispose();
		_unitCube.Dispose();
		_scratch.Dispose();
	}
}
