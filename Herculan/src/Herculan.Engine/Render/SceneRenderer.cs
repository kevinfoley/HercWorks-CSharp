using System.Numerics;
using Herculan.Engine.Gl;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Render;

/// <summary>One mesh plus the transform that places it in the world.</summary>
public sealed class SceneItem {
	public SceneItem(GpuMesh mesh, Matrix4x4 transform) {
		Mesh = mesh;
		Transform = transform;
	}

	public GpuMesh Mesh { get; }

	/// <summary>Model-to-world transform, in render space.</summary>
	public Matrix4x4 Transform { get; set; }
}

/// <summary>
/// Draws a list of <see cref="SceneItem"/>s from a <see cref="Camera"/> with one directional light
/// plus ambient. Deliberately minimal — the first milestone's rendering goal is a correct,
/// legible view of real game geometry, not a material system.
/// </summary>
public sealed class SceneRenderer : IDisposable {
	private const string VertexShaderSource = """
		#version 330 core
		layout (location = 0) in vec3 aPosition;
		layout (location = 1) in vec3 aNormal;
		layout (location = 2) in vec3 aColor;

		uniform mat4 uModel;
		uniform mat4 uView;
		uniform mat4 uProjection;

		out vec3 vNormal;
		out vec3 vColor;
		out float vViewDistance;

		void main() {
			vec4 worldPosition = uModel * vec4(aPosition, 1.0);
			vec4 viewPosition = uView * worldPosition;

			// Normals only ever see rotation and uniform scale here, so the plain model matrix is
			// enough; a normal matrix becomes necessary if non-uniform scaling ever appears.
			vNormal = normalize(mat3(uModel) * aNormal);
			vColor = aColor;
			vViewDistance = length(viewPosition.xyz);

			gl_Position = uProjection * viewPosition;
		}
		""";

	private const string FragmentShaderSource = """
		#version 330 core
		in vec3 vNormal;
		in vec3 vColor;
		in float vViewDistance;

		uniform vec3 uLightDirection;
		uniform vec3 uHazeColor;
		uniform float uHazeStart;
		uniform float uHazeEnd;

		out vec4 FragColor;

		void main() {
			// Two-sided lighting: DTS geometry is not reliably wound, and nothing is backface-culled,
			// so shade by the absolute facing rather than letting flipped triangles go black.
			float lambert = abs(dot(normalize(vNormal), normalize(-uLightDirection)));
			vec3 lit = vColor * (0.35 + 0.65 * lambert);

			// Distance haze, so a 10 km zone reads as depth instead of a flat wall of terrain.
			float haze = clamp((vViewDistance - uHazeStart) / max(uHazeEnd - uHazeStart, 0.001), 0.0, 1.0);
			FragColor = vec4(mix(lit, uHazeColor, haze), 1.0);
		}
		""";

	private readonly GL _gl;
	private readonly ShaderProgram _shader;

	public SceneRenderer(GL gl) {
		_gl = gl;
		_shader = new ShaderProgram(gl, VertexShaderSource, FragmentShaderSource);

		_gl.Enable(EnableCap.DepthTest);
		_gl.DepthFunc(DepthFunction.Less);
	}

	/// <summary>Direction the sun's light travels, in render space.</summary>
	public Vector3 LightDirection { get; set; } = Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.45f));

	/// <summary>Horizon/haze colour; also what the frame is cleared to, so distant terrain melts into it.</summary>
	public Vector3 HazeColor { get; set; } = new(0.55f, 0.60f, 0.68f);

	/// <summary>Distance in render units at which haze starts.</summary>
	public float HazeStart { get; set; } = 900f;

	/// <summary>Distance in render units at which haze is total.</summary>
	public float HazeEnd { get; set; } = 9000f;

	public void Render(Camera camera, IEnumerable<SceneItem> items, int viewportWidth, int viewportHeight) {
		_gl.Viewport(0, 0, (uint)System.Math.Max(viewportWidth, 1), (uint)System.Math.Max(viewportHeight, 1));
		_gl.ClearColor(HazeColor.X, HazeColor.Y, HazeColor.Z, 1f);
		_gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

		_shader.Use();
		_shader.SetMatrix("uView", camera.ViewMatrix);
		_shader.SetMatrix("uProjection", camera.ProjectionMatrix((float)viewportWidth / System.Math.Max(viewportHeight, 1)));
		_shader.SetVector3("uLightDirection", LightDirection);
		_shader.SetVector3("uHazeColor", HazeColor);
		_shader.SetFloat("uHazeStart", HazeStart);
		_shader.SetFloat("uHazeEnd", HazeEnd);

		foreach (var item in items) {
			_shader.SetMatrix("uModel", item.Transform);
			item.Mesh.Draw();
		}
	}

	public void Dispose() => _shader.Dispose();
}
