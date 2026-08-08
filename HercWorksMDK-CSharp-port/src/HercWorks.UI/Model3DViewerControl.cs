using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HercWorks.UI;

/// <summary>One loaded root mesh plus a user-toggleable visibility flag.</summary>
public sealed class ViewerMesh {
	public DtsRootMesh Mesh { get; }
	public bool Visible { get; set; } = true;

	public ViewerMesh(DtsRootMesh mesh) {
		Mesh = mesh;
	}
}

public enum Model3DRenderMode {
	Shaded,
	Wireframe,
	ShadedWireframe
}

/// <summary>
/// Software-rasterized orbit-camera viewer for parsed DTS geometry (see DtsGeometryBuilder). No
/// GPU/extra dependency needed — plenty fast in plain C# at ES2's low poly counts.
///
/// Fill uses a real per-pixel depth (Z-)buffer, not a triangle-level painter's-algorithm sort: an
/// earlier version sorted whole triangles by average depth, which looked fine for a single
/// isolated convex shape but flickered wildly on real meshes, since a single global order can't be
/// correct for triangles that overlap or interpenetrate in screen space — any such pair has pixels
/// where either order is wrong, and which one "wins" the sort flips with tiny camera changes. A
/// depth buffer sidesteps that entirely by testing per pixel instead of per triangle. The ground
/// grid and wireframe edge overlay still go through GDI+ directly, since they're not competing for
/// overlapping fill pixels the way triangle interiors are.
///
/// Deliberately does NOT backface-cull: the source data's winding convention (CW vs CCW as seen
/// from outside) was never verified, and culling on a wrong guess would render the model
/// inside-out. The depth buffer makes this cheap either way — every candidate pixel still gets a
/// correct nearest-wins test regardless of which side of each triangle is "front."
/// </summary>
public sealed class Model3DViewerControl : Control {
	private readonly List<ViewerMesh> _roots = new();
	private Vector3 _baseCenter = Vector3.Zero;
	private float _boundingRadius = 1f;
	private Vector3 _panOffset = Vector3.Zero;
	private float _yaw;
	private float _pitch;
	private float _distance = 5f;

	private bool _leftDown;
	private bool _middleDown;
	private Point _lastMouse;

	private const float RotateSpeed = 0.008f;
	private const float MaxPitch = MathF.PI * 0.49f;
	private const float PanClampFactor = 2f;
	private const float MinZoomFactor = 0.15f;
	private const float MaxZoomFactor = 20f;
	private const float DefaultYaw = -MathF.PI * 0.25f;
	private const float DefaultPitch = MathF.PI * 0.18f;

	public Model3DRenderMode RenderMode { get; set; } = Model3DRenderMode.ShadedWireframe;

	public IReadOnlyList<ViewerMesh> Roots => _roots;

	public Model3DViewerControl() {
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
			ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
		TabStop = true;
		BackColor = Color.FromArgb(30, 30, 34);
	}

	public void LoadMeshes(IEnumerable<DtsRootMesh> meshes) {
		_roots.Clear();
		foreach (var mesh in meshes) {
			_roots.Add(new ViewerMesh(mesh));
		}

		var allTriangles = _roots.SelectMany(r => r.Mesh.Triangles);
		(_baseCenter, _boundingRadius) = DtsGeometryBuilder.ComputeBounds(allTriangles);

		ResetView();
	}

	public void SetRootVisible(int index, bool visible) {
		if (index >= 0 && index < _roots.Count) {
			_roots[index].Visible = visible;
			Invalidate();
		}
	}

	/// <summary>
	/// Swaps in newly-rebuilt geometry for one root in place — e.g. after the user picks a
	/// different Detail Level for the currently-selected part. Preserves that root's visibility
	/// flag and deliberately does not recompute the camera's bounding sphere/framing, so switching
	/// detail levels (which shouldn't change the object's overall size much) doesn't jar the view.
	/// </summary>
	public void ReplaceRoot(int index, DtsRootMesh newMesh) {
		if (index < 0 || index >= _roots.Count) {
			return;
		}

		_roots[index] = new ViewerMesh(newMesh) { Visible = _roots[index].Visible };
		Invalidate();
	}

	public void ResetView() {
		_yaw = DefaultYaw;
		_pitch = DefaultPitch;
		_distance = _boundingRadius * 2.5f;
		_panOffset = Vector3.Zero;
		Invalidate();
	}

	protected override void OnMouseEnter(EventArgs e) {
		base.OnMouseEnter(e);
		// MouseWheel only reaches the focused control by default — grab focus on hover so
		// scrolling works without requiring a click first.
		if (CanFocus) {
			Focus();
		}
	}

	protected override void OnMouseDown(MouseEventArgs e) {
		base.OnMouseDown(e);
		_lastMouse = e.Location;
		if (e.Button == MouseButtons.Left) {
			_leftDown = true;
		} else if (e.Button == MouseButtons.Middle) {
			_middleDown = true;
		}
	}

	protected override void OnMouseUp(MouseEventArgs e) {
		base.OnMouseUp(e);
		if (e.Button == MouseButtons.Left) {
			_leftDown = false;
		} else if (e.Button == MouseButtons.Middle) {
			_middleDown = false;
		}
	}

	protected override void OnMouseMove(MouseEventArgs e) {
		base.OnMouseMove(e);
		int dx = e.X - _lastMouse.X;
		int dy = e.Y - _lastMouse.Y;
		_lastMouse = e.Location;

		if (_leftDown) {
			// Horizontal drag = yaw (the primary ask). Vertical drag = pitch — an added bonus on
			// top of yaw-only rotation, clamped short of the poles to avoid gimbal flip.
			_yaw -= dx * RotateSpeed;
			_pitch = Math.Clamp(_pitch - dy * RotateSpeed, -MaxPitch, MaxPitch);
			Invalidate();
		} else if (_middleDown) {
			Pan(dx, dy);
			Invalidate();
		}
	}

	protected override void OnMouseWheel(MouseEventArgs e) {
		base.OnMouseWheel(e);
		float factor = MathF.Pow(1.0012f, -e.Delta);
		_distance = Math.Clamp(_distance * factor, _boundingRadius * MinZoomFactor, _boundingRadius * MaxZoomFactor);
		Invalidate();
	}

	protected override void OnDoubleClick(EventArgs e) {
		base.OnDoubleClick(e);
		ResetView();
	}

	private Vector3 OrbitDirection() {
		float cp = MathF.Cos(_pitch);
		return new Vector3(cp * MathF.Sin(_yaw), MathF.Sin(_pitch), cp * MathF.Cos(_yaw));
	}

	private void Pan(int dx, int dy) {
		Vector3 dir = OrbitDirection();
		Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, dir));
		if (float.IsNaN(right.X)) {
			right = Vector3.UnitX;
		}
		Vector3 up = Vector3.Normalize(Vector3.Cross(dir, right));

		float panScale = _distance * 0.0025f;
		Vector3 candidate = _panOffset + (-dx * right + dy * up) * panScale;

		float maxPan = _boundingRadius * PanClampFactor;
		if (candidate.Length() > maxPan) {
			candidate = Vector3.Normalize(candidate) * maxPan;
		}

		_panOffset = candidate;
	}

	protected override void OnPaint(PaintEventArgs e) {
		var g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.Clear(BackColor);

		int width = Width, height = Height;
		if (width <= 0 || height <= 0 || _roots.Count == 0) {
			return;
		}

		Vector3 target = _baseCenter + _panOffset;
		Vector3 dir = OrbitDirection();
		Vector3 eye = target + dir * _distance;
		Matrix4x4 view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);

		float near = MathF.Max(_boundingRadius * 0.01f, 0.01f);
		float far = _boundingRadius * 40f + _distance * 2f;
		float aspect = MathF.Max((float)width / height, 0.01f);
		Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, aspect, near, far);

		Vector3 lightDir = Vector3.Normalize(eye - target);

		DrawGroundGrid(g, view, proj, near);

		bool drawFill = RenderMode != Model3DRenderMode.Wireframe;
		bool drawEdges = RenderMode != Model3DRenderMode.Shaded;

		// depthBuffer holds 1/w per pixel (larger = closer; matches TryProject's invW) — 0 means
		// "nothing drawn here yet", since every surviving vertex has a strictly positive w.
		float[]? depthBuffer = null;

		if (drawFill) {
			var pixels = new int[width * height];
			depthBuffer = new float[width * height];

			foreach (var root in _roots) {
				if (!root.Visible) {
					continue;
				}

				foreach (var tri in root.Mesh.Triangles) {
					RasterizeTriangle(tri, view, proj, lightDir, near, pixels, depthBuffer, width, height);
				}
			}

			using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
			bmp.UnlockBits(bmpData);

			g.DrawImageUnscaled(bmp, 0, 0);
		}

		if (drawEdges) {
			DrawWireframeOverlay(g, view, proj, near, width, height, depthBuffer);
		}
	}

	/// <summary>
	/// Scan-converts one triangle directly into the pixel/depth buffers via a standard
	/// edge-function rasterizer, testing every candidate pixel's interpolated 1/w against the
	/// buffer instead of relying on a whole-triangle draw order.
	/// </summary>
	private static void RasterizeTriangle(DtsTriangle tri, Matrix4x4 view, Matrix4x4 proj, Vector3 lightDir,
			float near, int[] pixels, float[] depthBuffer, int width, int height) {
		Vector3 ca = Vector3.Transform(tri.A, view);
		Vector3 cb = Vector3.Transform(tri.B, view);
		Vector3 cc = Vector3.Transform(tri.C, view);

		if (ca.Z >= -near || cb.Z >= -near || cc.Z >= -near) {
			return;
		}

		Vector4 pa = Vector4.Transform(ca, proj);
		Vector4 pb = Vector4.Transform(cb, proj);
		Vector4 pc = Vector4.Transform(cc, proj);
		if (pa.W <= 1e-6f || pb.W <= 1e-6f || pc.W <= 1e-6f) {
			return;
		}

		float invWa = 1f / pa.W, invWb = 1f / pb.W, invWc = 1f / pc.W;

		float sxA = (pa.X * invWa * 0.5f + 0.5f) * width, syA = (1f - (pa.Y * invWa * 0.5f + 0.5f)) * height;
		float sxB = (pb.X * invWb * 0.5f + 0.5f) * width, syB = (1f - (pb.Y * invWb * 0.5f + 0.5f)) * height;
		float sxC = (pc.X * invWc * 0.5f + 0.5f) * width, syC = (1f - (pc.Y * invWc * 0.5f + 0.5f)) * height;

		int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(sxA, MathF.Min(sxB, sxC))));
		int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(sxA, MathF.Max(sxB, sxC))));
		int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(syA, MathF.Min(syB, syC))));
		int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(syA, MathF.Max(syB, syC))));
		if (minX > maxX || minY > maxY) {
			return;
		}

		float area = EdgeFunction(sxA, syA, sxB, syB, sxC, syC);
		if (MathF.Abs(area) < 1e-6f) {
			return;
		}
		float invArea = 1f / area;

		Vector3 faceNormal = Vector3.Cross(tri.B - tri.A, tri.C - tri.A);
		float normalLength = faceNormal.Length();
		float intensity = 0.65f;
		if (normalLength > 1e-6f) {
			float nDotL = MathF.Abs(Vector3.Dot(faceNormal / normalLength, lightDir));
			intensity = 0.35f + 0.65f * nDotL;
		}
		int argb = Scale(tri.Color, intensity).ToArgb();

		for (int y = minY; y <= maxY; y++) {
			float py = y + 0.5f;
			int rowOffset = y * width;

			for (int x = minX; x <= maxX; x++) {
				float px = x + 0.5f;

				float w0 = EdgeFunction(sxB, syB, sxC, syC, px, py) * invArea;
				float w1 = EdgeFunction(sxC, syC, sxA, syA, px, py) * invArea;
				float w2 = EdgeFunction(sxA, syA, sxB, syB, px, py) * invArea;

				// Winding is unknown (see class doc comment), so accept either all-positive or
				// all-negative barycentric weights as "inside".
				bool inside = (w0 >= 0f && w1 >= 0f && w2 >= 0f) || (w0 <= 0f && w1 <= 0f && w2 <= 0f);
				if (!inside) {
					continue;
				}

				float pixelInvW = w0 * invWa + w1 * invWb + w2 * invWc;
				int idx = rowOffset + x;
				if (pixelInvW > depthBuffer[idx]) {
					depthBuffer[idx] = pixelInvW;
					pixels[idx] = argb;
				}
			}
		}
	}

	private static float EdgeFunction(float ax, float ay, float bx, float by, float cx, float cy) =>
		(cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

	/// <summary>
	/// Draws triangle edges via GDI+ on top of the rasterized fill. When a depth buffer is
	/// available (Shaded+Wireframe mode), skips edges for triangles whose centroid is clearly
	/// behind whatever's already in the depth buffer there, so hidden geometry's edges don't bleed
	/// through the visible surface. Pure Wireframe mode has no depth buffer (fill is skipped
	/// entirely for it) and intentionally draws every edge — an "X-ray" view of the whole mesh.
	/// </summary>
	private void DrawWireframeOverlay(Graphics g, Matrix4x4 view, Matrix4x4 proj, float near,
			int width, int height, float[]? depthBuffer) {
		using var edgePen = new Pen(Color.FromArgb(160, 0, 0, 0), 1f);
		using var wireframePen = new Pen(Color.FromArgb(220, 200, 220, 255), 1f);
		Pen pen = depthBuffer != null ? edgePen : wireframePen;

		foreach (var root in _roots) {
			if (!root.Visible) {
				continue;
			}

			foreach (var tri in root.Mesh.Triangles) {
				Vector3 ca = Vector3.Transform(tri.A, view);
				Vector3 cb = Vector3.Transform(tri.B, view);
				Vector3 cc = Vector3.Transform(tri.C, view);

				if (ca.Z >= -near || cb.Z >= -near || cc.Z >= -near) {
					continue;
				}

				if (!TryProject(ca, proj, out var pa) ||
					!TryProject(cb, proj, out var pb) ||
					!TryProject(cc, proj, out var pc)) {
					continue;
				}

				if (depthBuffer != null) {
					float centroidInvW = (1f / Vector4.Transform(ca, proj).W +
						1f / Vector4.Transform(cb, proj).W + 1f / Vector4.Transform(cc, proj).W) / 3f;
					int cx = Math.Clamp((int)((pa.X + pb.X + pc.X) / 3f), 0, width - 1);
					int cy = Math.Clamp((int)((pa.Y + pb.Y + pc.Y) / 3f), 0, height - 1);
					if (centroidInvW < depthBuffer[cy * width + cx] * 0.98f) {
						continue;
					}
				}

				g.DrawPolygon(pen, new[] { pa, pb, pc });
			}
		}
	}

	private void DrawGroundGrid(Graphics g, Matrix4x4 view, Matrix4x4 proj, float near) {
		float half = _boundingRadius * 1.5f;
		float y = _baseCenter.Y - _boundingRadius;
		const int divisions = 10;

		using var gridPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1f);

		for (int i = 0; i <= divisions; i++) {
			float t = -half + 2 * half * i / divisions;

			DrawGridLine(g, view, proj, near, gridPen, new Vector3(t, y, -half), new Vector3(t, y, half));
			DrawGridLine(g, view, proj, near, gridPen, new Vector3(-half, y, t), new Vector3(half, y, t));
		}
	}

	private void DrawGridLine(Graphics g, Matrix4x4 view, Matrix4x4 proj, float near, Pen pen, Vector3 worldA, Vector3 worldB) {
		Vector3 ca = Vector3.Transform(worldA, view);
		Vector3 cb = Vector3.Transform(worldB, view);
		if (ca.Z >= -near || cb.Z >= -near) {
			return;
		}

		if (TryProject(ca, proj, out var pa) && TryProject(cb, proj, out var pb)) {
			g.DrawLine(pen, pa, pb);
		}
	}

	private bool TryProject(Vector3 camSpace, Matrix4x4 proj, out PointF screenPoint) {
		Vector4 clip = Vector4.Transform(camSpace, proj);
		if (clip.W <= 1e-6f) {
			screenPoint = default;
			return false;
		}

		float invW = 1f / clip.W;
		float ndcX = clip.X * invW;
		float ndcY = clip.Y * invW;

		screenPoint = new PointF(
			(ndcX * 0.5f + 0.5f) * Width,
			(1f - (ndcY * 0.5f + 0.5f)) * Height);
		return true;
	}

	private static Color Scale(Color c, float intensity) {
		intensity = Math.Clamp(intensity, 0f, 1.15f);
		return Color.FromArgb(255,
			(int)Math.Clamp(c.R * intensity, 0, 255),
			(int)Math.Clamp(c.G * intensity, 0, 255),
			(int)Math.Clamp(c.B * intensity, 0, 255));
	}
}
