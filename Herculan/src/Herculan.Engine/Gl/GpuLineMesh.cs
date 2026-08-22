using System.Numerics;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Gl;

/// <summary>
/// A static, position-only line-list mesh on the GPU — the wireframe counterpart to
/// <see cref="GpuMesh"/>, which is triangles-only. Used for editor/debug gizmos rather than
/// anything drawn by <see cref="Render.SceneRenderer"/>.
/// </summary>
public sealed class GpuLineMesh : IDisposable {
	private readonly GL _gl;
	private readonly uint _vertexArray;
	private readonly uint _vertexBuffer;
	private readonly bool _dynamic;
	private int _vertexCount;

	public GpuLineMesh(GL gl, ReadOnlySpan<Vector3> vertices, bool dynamic = false) {
		_gl = gl;
		_vertexCount = vertices.Length;
		_dynamic = dynamic;

		_vertexArray = _gl.GenVertexArray();
		_gl.BindVertexArray(_vertexArray);

		_vertexBuffer = _gl.GenBuffer();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
		_gl.BufferData(BufferTargetARB.ArrayBuffer, vertices,
			dynamic ? BufferUsageARB.DynamicDraw : BufferUsageARB.StaticDraw);

		unsafe {
			_gl.EnableVertexAttribArray(0);
			_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vector3), (void*)0);
		}

		_gl.BindVertexArray(0);
	}

	/// <summary>A unit cube's 12 edges, centered on the origin and spanning -1..1 on each axis.</summary>
	public static GpuLineMesh CreateUnitCube(GL gl) {
		Vector3[] corners = {
			new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
			new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1),
		};

		(int, int)[] edges = {
			(0, 1), (1, 2), (2, 3), (3, 0), // bottom face
			(4, 5), (5, 6), (6, 7), (7, 4), // top face
			(0, 4), (1, 5), (2, 6), (3, 7), // verticals
		};

		var vertices = new Vector3[edges.Length * 2];
		for (int i = 0; i < edges.Length; i++) {
			vertices[i * 2] = corners[edges[i].Item1];
			vertices[i * 2 + 1] = corners[edges[i].Item2];
		}

		return new GpuLineMesh(gl, vertices);
	}

	/// <summary>
	/// Replaces the whole vertex list. Only valid on a mesh created with <c>dynamic: true</c>: a
	/// debug gizmo whose geometry is rebuilt every frame reuses one buffer rather than churning a
	/// GL object per frame.
	/// </summary>
	public void SetVertices(ReadOnlySpan<Vector3> vertices) {
		if (!_dynamic) {
			throw new InvalidOperationException("This line mesh was created static; it cannot be updated.");
		}

		_vertexCount = vertices.Length;
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
		_gl.BufferData(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.DynamicDraw);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
	}

	public void Draw() {
		if (_vertexCount == 0) {
			return;
		}

		_gl.BindVertexArray(_vertexArray);
		_gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_vertexCount);
		_gl.BindVertexArray(0);
	}

	public void Dispose() {
		_gl.DeleteBuffer(_vertexBuffer);
		_gl.DeleteVertexArray(_vertexArray);
	}
}
