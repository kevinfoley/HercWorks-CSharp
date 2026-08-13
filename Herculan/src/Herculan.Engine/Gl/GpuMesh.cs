using Silk.NET.OpenGL;

namespace Herculan.Engine.Gl;

/// <summary>
/// A static, non-indexed triangle mesh living on the GPU — one vertex array object over one vertex
/// buffer of <see cref="MeshVertex"/>. Non-indexed because everything drawn so far is flat-shaded:
/// adjacent triangles need distinct normals at shared corners anyway, so an index buffer would
/// share almost nothing.
/// </summary>
public sealed class GpuMesh : IDisposable {
	private readonly GL _gl;
	private readonly uint _vertexArray;
	private readonly uint _vertexBuffer;

	public GpuMesh(GL gl, ReadOnlySpan<MeshVertex> vertices) {
		_gl = gl;
		VertexCount = vertices.Length;

		_vertexArray = _gl.GenVertexArray();
		_gl.BindVertexArray(_vertexArray);

		_vertexBuffer = _gl.GenBuffer();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
		_gl.BufferData(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StaticDraw);

		unsafe {
			_gl.EnableVertexAttribArray(0);
			_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, MeshVertex.SizeInBytes, (void*)0);

			_gl.EnableVertexAttribArray(1);
			_gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, MeshVertex.SizeInBytes, (void*)(3 * sizeof(float)));

			_gl.EnableVertexAttribArray(2);
			_gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, MeshVertex.SizeInBytes, (void*)(6 * sizeof(float)));

			_gl.EnableVertexAttribArray(3);
			_gl.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, MeshVertex.SizeInBytes, (void*)(9 * sizeof(float)));

			_gl.EnableVertexAttribArray(4);
			_gl.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, MeshVertex.SizeInBytes, (void*)(11 * sizeof(float)));
		}

		_gl.BindVertexArray(0);
	}

	/// <summary>Number of vertices uploaded; triangle count is a third of this.</summary>
	public int VertexCount { get; }

	public void Draw() {
		if (VertexCount == 0) {
			return;
		}

		_gl.BindVertexArray(_vertexArray);
		_gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)VertexCount);
		_gl.BindVertexArray(0);
	}

	public void Dispose() {
		_gl.DeleteBuffer(_vertexBuffer);
		_gl.DeleteVertexArray(_vertexArray);
	}
}
