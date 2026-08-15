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

	public WireframeRenderer(GL gl) {
		_gl = gl;
		_shader = new ShaderProgram(gl, VertexShaderSource, FragmentShaderSource);
		_unitCube = GpuLineMesh.CreateUnitCube(gl);
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

	public void Dispose() {
		_shader.Dispose();
		_unitCube.Dispose();
	}
}
