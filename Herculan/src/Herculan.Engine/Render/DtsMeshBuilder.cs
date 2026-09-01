using System.Numerics;
using HercWorks.Core.Data.File.Dts;
using HercWorks.Core.Data.File.Dts.Anim;
using HercWorks.Core.Data.File.Dts.Bsp;
using HercWorks.Core.Data.File.Dts.Part;
using HercWorks.Core.Data.File.Dts.Poly;
using HercWorks.Core.Data.File.Dyn;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;

namespace Herculan.Engine.Render;

/// <summary>
/// Flattens a parsed DTS model tree into untextured, flat-shaded triangles in render space.
///
/// <para>This is the engine's counterpart to <c>HercWorks.UI.DtsGeometryBuilder</c>, and it is a
/// separate type on purpose rather than shared code: that one produces GDI+ <c>Color</c> values for
/// a software rasterizer inside a Windows-only WinForms tool, while this produces GPU vertices and
/// must stay clear of System.Drawing so the engine keeps building for Linux/macOS (see
/// docs/engine/planning.md's target-platform decision). The tree-walking rules are the same, and
/// each one below is annotated with what the UI builder established — worth keeping the two in sync
/// if either side changes.</para>
///
/// <para><see cref="TSTexture4Poly"/> polys resolve to real texture through the chain established in
/// docs/formats/dts-texture-binding.md: <c>Surfaces[ColorIndexId / 4].FrontColor</c> is a frame index
/// into the mesh's bound <c>.DBA</c> bank, and the four UV corners are the frame's own rect.</para>
///
/// <para><b>The untextured poly types are three separate mechanisms</b>, distinguished by what their
/// <c>Surfaces[ColorIndexId / 4].FrontColor</c> means:</para>
/// <list type="bullet">
/// <item><see cref="TSShadedPoly"/> and <see cref="TSGouraudPoly"/> — a <b>ramp number</b> into the
/// theater palette's shade-ramp table, with the face's light level picking a step along it. The two
/// spend it through different chains; see <see cref="SurfaceShading"/>. Nearly every surface of a
/// HERC or a building is one of these. Resolved by <see cref="ResolveShadeRamp"/>, and the lookup
/// happens per fragment (<see cref="MeshVertex.ShadeRamp"/>, <see cref="SurfaceRampTable"/>) because
/// the shade depends on the face's world normal and one mesh serves every instance of a type.</item>
/// <item>Plain <see cref="TSSolidPoly"/> — a <b>palette index</b>, through the theater ramp at a
/// fixed shade, never lit. <see cref="ResolveSolidColors"/>.</item>
/// </list>
///
/// <para>Pass a <see cref="TextureAtlas"/> and a <see cref="SurfaceShading"/> to resolve all three.
/// Without them, surfaces fall back to <see cref="ResolveSurfaceColor"/> or a flat placeholder.</para>
/// </summary>
/// <summary>
/// One node's share of a shape's geometry: the triangles of every group that hangs from a single
/// transform, in that node's own space rather than the shape's.
///
/// <para>A segment is drawn with the node's posed transform in front of the object's own, so the
/// animation thread moving the node moves the geometry. Its vertices are therefore <i>not</i>
/// interchangeable with <see cref="DtsMeshBuilder.BuildRoot"/>'s flat mesh, which has the rest pose
/// already baked into it.</para>
/// </summary>
/// <param name="TransformId">The node, in the id space <c>AnimationThread.NodeTransform</c> takes.
/// -1 for geometry no node places, which is drawn at the shape's origin.</param>
/// <param name="Vertices">Triangles then outline edges in the node's own space, ready to upload —
/// see <see cref="MeshBuild"/>.</param>
/// <param name="TriangleVertexCount">Where the outline edges start — see <see cref="MeshBuild"/>.</param>
public readonly record struct MeshSegment(int TransformId, MeshVertex[] Vertices, int TriangleVertexCount);

/// <summary>
/// A built mesh: filled triangles first, then the outline edges that are drawn over them as lines,
/// in one array so a single vertex buffer carries both.
///
/// <para>The outline is not decoration. <c>TSSolidPoly_Render</c> (<c>00474db4</c>) resolves
/// <i>two</i> colours for every flat solid face — <c>surface.FrontColor</c> and
/// <c>surface.FrontLineColor</c>, both through the theater ramp at the same fixed shade — and hands
/// both to the polygon fill <c>FUN_0048d518</c>, which fills in the first and then, whenever the two
/// resolve differently, re-draws the same polygon's edge loop in the second. That second pass is
/// this range. See <see cref="DtsMeshBuilder"/>'s <c>ResolveSolidColors</c>.</para>
/// </summary>
/// <param name="Vertices">Triangle corners in <c>[0, TriangleVertexCount)</c>, line-segment
/// endpoint pairs after it.</param>
/// <param name="TriangleVertexCount">Always a multiple of three; the remainder of
/// <paramref name="Vertices"/> is a multiple of two.</param>
public readonly record struct MeshBuild(MeshVertex[] Vertices, int TriangleVertexCount) {
	public static MeshBuild Empty { get; } = new(Array.Empty<MeshVertex>(), 0);

	/// <summary>How many vertices belong to the outline pass.</summary>
	public int OutlineVertexCount => Vertices.Length - TriangleVertexCount;
}

public static class DtsMeshBuilder {
	/// <summary>Safety bound on the transform parent chain, in case a file's relations form a cycle.</summary>
	private const int MaxTransformChainSteps = 64;

	private static readonly Vector3 FallbackColor = new(0.72f, 0.72f, 0.75f);

	/// <summary>
	/// Stand-in colour for a <see cref="TSTexture4Poly"/> whose frame could not be resolved. Distinct
	/// from <see cref="FallbackColor"/> so an unresolved texture poly is identifiable on screen
	/// instead of blending in with genuinely untextured geometry.
	/// </summary>
	private static readonly Vector3 TextureFallbackColor = new(0.47f, 0.59f, 0.75f);

	/// <summary>
	/// Vertex-order UV corners for a textured quad, as fractions of the frame's own rect.
	/// RE-confirmed order (top-left, top-right, bottom-right, bottom-left) — the exe builds
	/// <c>[(F0,F1), (F2,F1), (F2,F3), (F0,F3)]</c> from a per-frame descriptor, see
	/// docs/formats/dts-texture-binding.md's "UV-generation formula — FOUND".
	/// </summary>
	private static readonly Vector2[] QuadCorners = {
		new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f)
	};

	private readonly struct Triangle {
		public Vector3 A { get; }
		public Vector3 B { get; }
		public Vector3 C { get; }

		/// <summary>The same corners before the node's own placement — see <see cref="MeshSegment"/>.</summary>
		public Vector3 LocalA { get; }

		/// <inheritdoc cref="LocalA" />
		public Vector3 LocalB { get; }

		/// <inheritdoc cref="LocalA" />
		public Vector3 LocalC { get; }

		/// <summary>The transform id of the node this triangle's group hangs from, or -1.</summary>
		public int TransformId { get; }

		public Vector3 Color { get; }
		public Vector2 UvA { get; }
		public Vector2 UvB { get; }
		public Vector2 UvC { get; }

		/// <summary>
		/// The three corners' UV weights, or all zero when <see cref="UvA"/>..<see cref="UvC"/> are
		/// plain coordinates — see <see cref="MeshVertex.UvWeight"/>.
		/// </summary>
		public (float A, float B, float C) UvWeights { get; }

		/// <summary>Which twin of a coincident pair wins — see <see cref="DropCoincidentTwins"/>.</summary>
		public int Rank { get; }

		/// <summary>
		/// Which source poly this triangle was fanned out of, unique across the whole build. Only
		/// <see cref="OutlineEdge"/> reads it: an outline belongs to a poly, so it has to disappear
		/// with that poly when <see cref="DropCoincidentTwins"/> discards it.
		/// </summary>
		public int PolyId { get; }

		/// <summary>Whether <see cref="Color"/> is final — see <see cref="MeshVertex.Unlit"/>.</summary>
		public bool Unlit { get; }

		/// <summary>The material ramp this face's surface names, or -1 — see <see cref="MeshVertex.ShadeRamp"/>.</summary>
		public int ShadeRamp { get; }

		/// <summary>
		/// The shape's own per-corner normals, for a <see cref="TSGouraudPoly"/> — see
		/// <see cref="ResolveVertexNormals"/>. Null for every other poly, whose three corners share
		/// <see cref="FaceNormal"/>.
		/// </summary>
		public (Vector3 A, Vector3 B, Vector3 C)? VertexNormals { get; }

		/// <summary>
		/// The source poly's own stored normal — see <see cref="ResolveFaceNormal"/>. Null when the
		/// poly's normal index does not resolve, and <see cref="EmitTriangle"/> falls back to the
		/// winding.
		/// </summary>
		public Vector3? FaceNormal { get; }

		public Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 color, int rank, int polyId,
				Vector3 localA, Vector3 localB, Vector3 localC, int transformId,
				Vector2 uvA = default, Vector2 uvB = default, Vector2 uvC = default,
				bool unlit = false, int shadeRamp = -1,
				(Vector3 A, Vector3 B, Vector3 C)? vertexNormals = null,
				Vector3? faceNormal = null,
				(float A, float B, float C) uvWeights = default) {
			Unlit = unlit;
			ShadeRamp = shadeRamp;
			VertexNormals = vertexNormals;
			FaceNormal = faceNormal;
			PolyId = polyId;
			A = a;
			B = b;
			C = c;
			LocalA = localA;
			LocalB = localB;
			LocalC = localC;
			TransformId = transformId;
			Color = color;
			Rank = rank;
			UvA = uvA;
			UvB = uvB;
			UvC = uvC;
			UvWeights = uvWeights;
		}
	}

	/// <summary>
	/// One edge of a flat solid poly's outline pass, in the same two spaces a <see cref="Triangle"/>
	/// is kept in — see <see cref="MeshBuild"/>.
	/// </summary>
	private readonly struct OutlineEdge {
		public OutlineEdge(Vector3 a, Vector3 b, Vector3 localA, Vector3 localB,
				Vector3 color, int transformId, int polyId) {
			A = a;
			B = b;
			LocalA = localA;
			LocalB = localB;
			Color = color;
			TransformId = transformId;
			PolyId = polyId;
		}

		public Vector3 A { get; }
		public Vector3 B { get; }
		public Vector3 LocalA { get; }
		public Vector3 LocalB { get; }

		/// <summary>The ramped <c>FrontLineColor</c>, already final — an outline is never lit.</summary>
		public Vector3 Color { get; }

		public int TransformId { get; }

		/// <summary>The poly this edge belongs to — see <see cref="Triangle.PolyId"/>.</summary>
		public int PolyId { get; }
	}

	/// <summary>
	/// What the tree walk fills in: the two passes the original draws every flat solid face in, plus
	/// the counter that ties one to the other.
	/// </summary>
	private sealed class Collector {
		public List<Triangle> Triangles { get; } = new();

		public List<OutlineEdge> Outlines { get; } = new();

		private int _nextPolyId;

		/// <summary>Claims the next <see cref="Triangle.PolyId"/>, once per source poly.</summary>
		public int NextPolyId() => _nextPolyId++;
	}

	/// <summary>
	/// How good a triangle is as the survivor of a coincident group, highest wins. The ordering is
	/// the whole point of <see cref="DropCoincidentTwins"/> and is documented there.
	/// </summary>
	private static class Ranks {
		/// <summary>A texture poly with no atlas frame behind it — a placeholder colour, worst option.</summary>
		public const int UnresolvedTexture = 0;

		/// <summary>An ordinary flat-shaded poly, carrying a real surface colour.</summary>
		public const int FlatShaded = 1;

		/// <summary>A texture poly resolved to real atlas pixels — what the original draws.</summary>
		public const int Textured = 2;
	}

	/// <summary>
	/// Builds every top-level root in the model into one mesh, each at its highest detail level.
	///
	/// <para>A DTS file's roots are fully independent objects — <c>SAMSON.DTS</c>'s roots are LOD
	/// variants of one mech, while <c>BASES_AN.DTS</c>'s are unrelated buildings bundled together —
	/// and nothing in the file distinguishes the two cases; that knowledge lives in the game engine.
	/// A caller that wants one specific object should use <see cref="BuildRoot"/> and pick. Merging
	/// all roots is right only when the file is known to hold a single object.</para>
	/// </summary>
	public static MeshBuild BuildAll(DynamixThreeSpaceModel model, TextureAtlas? atlas = null, SurfaceShading? shading = null) {
		var sink = new Collector();
		if (model.Meshes != null) {
			foreach (var root in model.Meshes) {
				Collect(root, null, sink, atlas, shading);
			}
		}
		return Emit(sink);
	}

	/// <summary>
	/// Builds one top-level root at its highest detail level, with every
	/// <see cref="TSCellAnimPart"/> under it showing cell <paramref name="cellFrame"/>.
	/// </summary>
	/// <param name="cellFrame">
	/// Which cell of the shape's flipbook to bake. Zero — the rest pose — for everything the engine
	/// draws statically; a launcher round builds one mesh per cell and picks between them as its own
	/// frame counter moves, see <see cref="CellFrameCount"/>.
	/// </param>
	public static MeshBuild BuildRoot(TSObject root, TextureAtlas? atlas = null,
			SurfaceShading? shading = null, int cellFrame = 0) {
		var sink = new Collector();
		Collect(root, null, sink, atlas, shading, cellFrame);
		return Emit(sink);
	}

	/// <summary>
	/// How many cells the shape's flipbook has — <c>TSShape.SequenceList[0]</c>, which is the
	/// per-sequence frame-count array the original mods its own counter by
	/// (<c>shape+0x20</c>, read by <c>Bullet_TickUpdate</c> and <c>Rocket_TickUpdate</c>).
	///
	/// <para>Sequence zero only: every retail <see cref="TSCellAnimPart"/> in a projectile shape
	/// carries <c>AnimSequence == 0</c>, and so does every <c>ROCKETS.DAT</c> record's own sequence
	/// field. A shape with no flipbook reports one frame, which is the shape itself.</para>
	/// </summary>
	public static int CellFrameCount(TSObject? root) =>
		root is TSShape { SequenceList: { Length: > 0 } sequences } && sequences[0] > 1
			? System.Math.Min((int)sequences[0], MaxCellFrames)
			: 1;

	/// <summary>Guard against a file claiming a flipbook longer than anything could reasonably hold.</summary>
	private const int MaxCellFrames = 64;

	/// <summary>
	/// The same geometry as <see cref="BuildRoot"/>, split by the node each part hangs from and left
	/// in that node's own space — what a shape has to be to animate.
	///
	/// <para>DBSIM draws a shape exactly this way. <c>TSGroup_RenderPolys</c> (<c>004758c8</c>)
	/// begins by calling <c>00476014</c>, which takes the group's own <c>TSBasePart.Transform</c>
	/// (field +4), looks the node's world transform up in the shape instance's per-node array, and
	/// composes it with the current object-to-view transform before a single poly is drawn
	/// (<c>Concat(nodeWorld[transform], objectToView)</c>, then <c>0048c338</c> installs it). Every
	/// group in the shape is placed by its own node, and it is the animation thread that moves those
	/// nodes.</para>
	///
	/// <para>Contrast <see cref="BuildRoot"/>, which bakes each group at the rest pose that
	/// <see cref="ResolveGroupOffset"/> works out and hands back one rigid mesh. That is still what a
	/// structure wants — nothing animates it — but it is why a HERC's legs never moved.</para>
	///
	/// <para>Coincident-twin removal (<see cref="DropCoincidentTwins"/>) runs across the whole shape
	/// first, in the shared rest-pose space, exactly as it does for the flat build: a textured poly
	/// and its flat-shaded twin always belong to the same group, so splitting afterwards keeps the
	/// same survivor either way.</para>
	/// </summary>
	public static MeshSegment[] BuildSegments(TSObject root, TextureAtlas? atlas = null, SurfaceShading? shading = null) {
		var sink = new Collector();
		Collect(root, null, sink, atlas, shading);
		return EmitSegments(sink);
	}

	/// <summary>
	/// Axis-aligned bounds of a built mesh, as (min, max) in render units. Used to sit a model on
	/// the ground and to derive a collision radius until the original's per-type hit-cylinder value
	/// is mapped (see <see cref="Sim.MechObject.HitRadius"/>).
	/// </summary>
	public static (Vector3 Min, Vector3 Max) Bounds(IReadOnlyList<MeshVertex> vertices) {
		if (vertices.Count == 0) {
			return (Vector3.Zero, Vector3.Zero);
		}

		var min = new Vector3(float.MaxValue);
		var max = new Vector3(float.MinValue);
		foreach (var vertex in vertices) {
			min = Vector3.Min(min, vertex.Position);
			max = Vector3.Max(max, vertex.Position);
		}
		return (min, max);
	}

	private static MeshBuild Emit(Collector sink) {
		var kept = DropCoincidentTwins(sink.Triangles);
		var edges = SurvivingOutlines(kept, sink.Outlines);

		int triangleVertices = kept.Count * 3;
		var vertices = new MeshVertex[triangleVertices + edges.Count * 2];

		for (int i = 0; i < kept.Count; i++) {
			EmitTriangle(kept[i], local: false, vertices, i * 3);
		}

		for (int i = 0; i < edges.Count; i++) {
			EmitEdge(edges[i], local: false, vertices, triangleVertices + i * 2);
		}

		return new MeshBuild(vertices, triangleVertices);
	}

	/// <summary>
	/// The outline edges whose poly still has geometry after <see cref="DropCoincidentTwins"/>. An
	/// outline is a second pass over a poly the original has just filled, so it has no business
	/// outliving one that lost its tie.
	/// </summary>
	private static List<OutlineEdge> SurvivingOutlines(List<Triangle> kept, List<OutlineEdge> outlines) {
		if (outlines.Count == 0) {
			return outlines;
		}

		var drawn = new HashSet<int>();
		foreach (var triangle in kept) {
			drawn.Add(triangle.PolyId);
		}

		return outlines.Where(edge => drawn.Contains(edge.PolyId)).ToList();
	}

	/// <summary>
	/// Writes one outline edge's two endpoints. Unlit and untextured by construction — the line
	/// colour came out of the ramp already resolved, exactly as the fill colour did.
	/// </summary>
	private static void EmitEdge(in OutlineEdge edge, bool local, MeshVertex[] vertices, int at) {
		Vector3 a = local ? edge.LocalA : edge.A;
		Vector3 b = local ? edge.LocalB : edge.B;

		vertices[at] = new MeshVertex(a, Vector3.UnitY, edge.Color, unlit: true);
		vertices[at + 1] = new MeshVertex(b, Vector3.UnitY, edge.Color, unlit: true);
	}

	/// <summary>
	/// The surviving triangles grouped by the node that places them, each in that node's own space.
	/// Segments come back in ascending transform id, which is only for stable output — nothing reads
	/// the order.
	/// </summary>
	private static MeshSegment[] EmitSegments(Collector sink) {
		var kept = DropCoincidentTwins(sink.Triangles);
		var edges = SurvivingOutlines(kept, sink.Outlines);

		var byNode = new Dictionary<int, List<Triangle>>();
		foreach (var triangle in kept) {
			if (!byNode.TryGetValue(triangle.TransformId, out var list)) {
				byNode[triangle.TransformId] = list = new List<Triangle>();
			}
			list.Add(triangle);
		}

		// An outline rides the same node its poly does, so it goes into that node's segment. A node
		// that has outlines but no surviving triangles cannot happen — SurvivingOutlines already
		// dropped those — so this never introduces a segment of its own.
		var edgesByNode = new Dictionary<int, List<OutlineEdge>>();
		foreach (var edge in edges) {
			if (!edgesByNode.TryGetValue(edge.TransformId, out var list)) {
				edgesByNode[edge.TransformId] = list = new List<OutlineEdge>();
			}
			list.Add(edge);
		}

		var segments = new MeshSegment[byNode.Count];
		int next = 0;
		foreach (int transformId in byNode.Keys.OrderBy(id => id)) {
			var list = byNode[transformId];
			var nodeEdges = edgesByNode.TryGetValue(transformId, out var found) ? found : null;

			int triangleVertices = list.Count * 3;
			var vertices = new MeshVertex[triangleVertices + (nodeEdges?.Count ?? 0) * 2];
			for (int i = 0; i < list.Count; i++) {
				EmitTriangle(list[i], local: true, vertices, i * 3);
			}

			for (int i = 0; i < (nodeEdges?.Count ?? 0); i++) {
				EmitEdge(nodeEdges![i], local: true, vertices, triangleVertices + i * 2);
			}

			segments[next++] = new MeshSegment(transformId, vertices, triangleVertices);
		}

		return segments;
	}

	/// <summary>
	/// Writes one triangle's three vertices, either at the rest pose <see cref="Collect"/> baked or
	/// in its node's own space. The normal is taken from whichever corners are being written, so a
	/// segment's normals rotate with the node matrix that draws it.
	/// </summary>
	private static void EmitTriangle(in Triangle triangle, bool local, MeshVertex[] vertices, int at) {
		Vector3 a = local ? triangle.LocalA : triangle.A;
		Vector3 b = local ? triangle.LocalB : triangle.B;
		Vector3 c = local ? triangle.LocalC : triangle.C;

		// The poly's OWN stored normal, not one derived from the winding — the two point opposite
		// ways. Measured across every poly of BASES.DGS, BASES_AN.DTS and APOCA.DTS (12,656 of them,
		// no exceptions): dot(normalize(cross(b - a, c - a)), storedNormal) == -1. The files wind
		// their corners the other way round from the normal they carry.
		//
		// This has to be the stored one because the front/back sign the shader derives from it is
		// then applied to the CORNER normals, which come out of the same point list and so share the
		// stored convention (ResolveVertexNormals). Deriving the sign from the winding instead turns
		// every Gouraud poly's light term inside out: lit on the side facing away from the sun.
		// A flat poly is insensitive to the choice — its corner normal is this same vector, so the
		// sign cancels — which is why the mistake was invisible until Gouraud shading went in.
		Vector3 winding = Vector3.Cross(c - a, b - a);
		Vector3 normal = triangle.FaceNormal
			?? (winding.LengthSquared() > 1e-12f ? Vector3.Normalize(winding) : Vector3.UnitY);

		// A Gouraud poly carries the shape's own normal per corner, and interpolating between them is
		// the whole difference between the type and its flat sibling. The corners' normals are
		// direction-only, so they are the same in the node's space and the rest pose's — the offset
		// between those is a translation (see ResolveGroupOffset).
		var (normalA, normalB, normalC) = triangle.VertexNormals ?? (normal, normal, normal);

		// Only a triangle that actually resolved to an atlas frame samples the texture; the rest
		// keep their colour, which is what makes the placeholder colour on an unresolved texture
		// poly visible instead of it sampling whatever sits at the atlas origin.
		bool textured = triangle.Rank == Ranks.Textured;

		vertices[at] = new MeshVertex(a, normalA, triangle.Color, triangle.UvA, textured, triangle.Unlit,
			shadeRamp: triangle.ShadeRamp, faceNormal: normal, uvWeight: triangle.UvWeights.A);
		vertices[at + 1] = new MeshVertex(b, normalB, triangle.Color, triangle.UvB, textured, triangle.Unlit,
			shadeRamp: triangle.ShadeRamp, faceNormal: normal, uvWeight: triangle.UvWeights.B);
		vertices[at + 2] = new MeshVertex(c, normalC, triangle.Color, triangle.UvC, textured, triangle.Unlit,
			shadeRamp: triangle.ShadeRamp, faceNormal: normal, uvWeight: triangle.UvWeights.C);
	}

	/// <summary>
	/// Real DTS meshes stack a textured poly precisely on top of a flat-shaded twin occupying the
	/// exact same surface — 186 such pairs in <c>SAMSON.DTS</c>'s first root alone, with identical
	/// centroid and normal. Both drawn, they land at identical depth, so which one is visible comes
	/// down to draw order rather than anything meaningful. Exactly one survives per coincident group,
	/// picked by <see cref="Ranks"/>.
	///
	/// <para><b>That preference is the inverse of what it was before texturing existed.</b> While
	/// <see cref="TSTexture4Poly"/> could only render as a placeholder colour, the flat-shaded twin
	/// was the only one that could look right and deliberately won every tie. Now that a texture poly
	/// resolves to real atlas pixels it is the one the original actually draws, so it has to win —
	/// leaving the old preference in place would load and pack every texture and then systematically
	/// hide it behind the untextured twin.</para>
	///
	/// <para>A texture poly that did <i>not</i> resolve (no atlas supplied, or a frame index outside
	/// the bank) still loses to the flat-shaded twin, which is why this is a three-way rank rather
	/// than a flipped boolean: the no-bank path keeps behaving exactly as it did before.</para>
	///
	/// <para>Grouping uses a coarsely-rounded centroid plus the absolute normal, so opposite-winding
	/// duplicates of one surface land together while genuinely distinct nearby triangles do not.</para>
	/// </summary>
	private static List<Triangle> DropCoincidentTwins(List<Triangle> triangles) {
		var groups = new Dictionary<(int, int, int, int, int, int), int>();
		var keep = new bool[triangles.Count];
		var order = new List<int>();

		for (int i = 0; i < triangles.Count; i++) {
			var triangle = triangles[i];
			Vector3 centroid = (triangle.A + triangle.B + triangle.C) / 3f;
			Vector3 normal = Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A);
			if (normal.LengthSquared() > 1e-12f) {
				normal = Vector3.Normalize(normal);
			}

			var key = (
				(int)MathF.Round(centroid.X * 40f), (int)MathF.Round(centroid.Y * 40f), (int)MathF.Round(centroid.Z * 40f),
				(int)MathF.Round(MathF.Abs(normal.X) * 100f), (int)MathF.Round(MathF.Abs(normal.Y) * 100f),
				(int)MathF.Round(MathF.Abs(normal.Z) * 100f));

			if (!groups.TryGetValue(key, out int existing)) {
				groups[key] = i;
				keep[i] = true;
				order.Add(i);
				continue;
			}

			// A strictly better-ranked twin replaces the one already kept; ties go to the first seen.
			if (triangle.Rank > triangles[existing].Rank) {
				keep[existing] = false;
				groups[key] = i;
				keep[i] = true;
				order.Add(i);
			}
		}

		var result = new List<Triangle>(order.Count);
		foreach (int index in order) {
			if (keep[index]) {
				result.Add(triangles[index]);
			}
		}
		return result;
	}

	/// <summary>
	/// Walks the node tree looking for geometry-bearing groups. Container nodes are descended into;
	/// <see cref="TSBitmapPart"/> carries no geometry (it is a camera-facing billboard, which needs
	/// per-frame geometry this builder doesn't produce) and is skipped.
	/// </summary>
	private static void Collect(TSObject? node, ANAnimList? animList, Collector sink,
			TextureAtlas? atlas, SurfaceShading? shading, int cellFrame = 0) {
		switch (node) {
			case null:
				return;

			case ANShape shape:
				// An ANShape brings its own animation list into scope for everything beneath it.
				CollectParts(shape.Parts, shape.AnimationList ?? animList, sink, atlas, shading, cellFrame);
				break;

			case TSDetailPart detailPart:
				CollectHighestDetail(detailPart, animList, sink, atlas, shading, cellFrame);
				break;

			case TSCellAnimPart cellAnimPart:
				// Consecutive frames of one moving sub-part — a rocket's exhaust flame, say. Walking
				// all of them stacks every frame of the motion on top of itself, so exactly one cell
				// is taken, the way TSCellAnimPart_Render (004767e4) takes one:
				// children[counter % childCount]. Everything the engine draws statically asks for
				// cell zero, the rest pose.
				if (cellAnimPart.Parts is { Length: > 0 } cells) {
					Collect(cells[((cellFrame % cells.Length) + cells.Length) % cells.Length],
						animList, sink, atlas, shading, cellFrame);
				}
				break;

			case TSBSPGroup bspGroup:
				AppendGroup(bspGroup, animList, sink, atlas, shading);
				break;

			case TSGroup group:
				AppendGroup(group, animList, sink, atlas, shading);
				break;

			case TSPartList partList:
				CollectParts(partList.Parts, animList, sink, atlas, shading, cellFrame);
				break;
		}
	}

	private static void CollectParts(TSObject[]? parts, ANAnimList? animList, Collector sink,
			TextureAtlas? atlas, SurfaceShading? shading, int cellFrame = 0) {
		if (parts == null) {
			return;
		}

		foreach (var part in parts) {
			Collect(part, animList, sink, atlas, shading, cellFrame);
		}
	}

	/// <summary>
	/// A <see cref="TSDetailPart"/> holds several complete alternate representations of the same
	/// sub-structure, paired 1:1 with ascending on-screen-size thresholds in <c>Details</c>. This
	/// takes the <b>last</b> one, which is the level the original selects at maximum detail — the
	/// setting the options screen calls <c>STRUCTURE DETAIL: MAXIMUM</c>, and the engine's only
	/// setting for now.
	///
	/// <para><c>TSDetailPart_Render</c> (<c>004768bc</c>) is the whole of the selection:</para>
	/// <code>
	/// size = (radius &lt;&lt; shift) / max(distance - radius, 1)   // projected size
	/// t    = Q10Multiply(detailScale, size)
	/// i    = detailBias;                                          // the STRUCTURE DETAIL setting
	/// while (i &lt; count - 1 &amp;&amp; details[i] &lt; t) i++;
	/// render(parts[min(i - detailBias, count - 1)]);
	/// </code>
	/// <para>Thresholds are walked in file order, the chosen part is <c>i - detailBias</c>, and a
	/// larger bias shifts the whole scale <i>down</i> — bias zero is maximum detail and lets a close
	/// object reach <c>count - 1</c>. <c>Parts[^1]</c> is that loop's limit.</para>
	///
	/// <para>Picking the part paired with the largest <i>threshold</i> is not the same rule, though
	/// it agrees on every retail shape (all of them end at 255). It would diverge on a file whose
	/// thresholds were not ascending.</para>
	/// </summary>
	private static void CollectHighestDetail(TSDetailPart detailPart, ANAnimList? animList,
			Collector sink, TextureAtlas? atlas, SurfaceShading? shading, int cellFrame = 0) {
		if (detailPart.Parts is not { Length: > 0 } parts) {
			return;
		}

		Collect(parts[^1], animList, sink, atlas, shading, cellFrame);
	}

	private static void AppendGroup(TSGroup group, ANAnimList? animList, Collector sink, TextureAtlas? atlas, SurfaceShading? shading) {
		if (group.Points == null || group.Indexes == null || group.Polys == null) {
			return;
		}

		Vector3 offset = ResolveGroupOffset(group, animList);

		// Point shorts go straight through as world coordinates — see WorldScale.WorldUnitsPerDtsUnit
		// for the measurements behind that. Each point is kept twice: once at the rest pose the flat
		// mesh bakes, and once in the group's own node space, which is where a segment draws from.
		var points = new Vector3[group.Points.Length];
		var localPoints = new Vector3[group.Points.Length];
		for (int i = 0; i < group.Points.Length; i++) {
			var point = group.Points[i];
			localPoints[i] = WorldScale.DtsToRender(point.X, point.Y, point.Z);
			points[i] = WorldScale.DtsToRender(
				point.X + offset.X,
				point.Y + offset.Y,
				point.Z + offset.Z);
		}

		Vector3[]? surfaceColors = null;
		if (group.Surfaces != null) {
			surfaceColors = new Vector3[group.Surfaces.Length];
			for (int i = 0; i < group.Surfaces.Length; i++) {
				surfaceColors[i] = ResolveSurfaceColor(group.Surfaces[i].FrontColor, atlas);
			}
		}

		foreach (var polyObject in group.Polys) {
			if (polyObject is not TSPoly poly || poly.VertexCount < 3) {
				continue;
			}

			int listStart = poly.VertexList;
			if (listStart < 0 || listStart + poly.VertexCount > group.Indexes.Length) {
				continue;
			}

			int firstIndex = group.Indexes[listStart];
			if (firstIndex < 0 || firstIndex >= points.Length) {
				continue;
			}

			// A TSTexture4Poly is a quad by definition; anything else claiming to be one has a layout
			// this UV mapping was never confirmed against, so it falls back rather than guessing.
			AtlasRect? rect = poly is TSTexture4Poly && poly.VertexCount == 4
				? ResolveFrame(poly, group.Surfaces, atlas)
				: null;

			int rank = poly is TSTexture4Poly
				? (rect.HasValue ? Ranks.Textured : Ranks.UnresolvedTexture)
				: Ranks.FlatShaded;

			// A plain TSSolidPoly — the exact type, not one of the three subclasses that inherit its
			// fields — is the one poly kind the original draws through the theater ramp instead of the
			// bound texture bank, and the one it does not light. See ResolveSolidColors.
			SolidColors? solid = polyObject.GetType() == typeof(TSSolidPoly)
				? ResolveSolidColors(poly, group.Surfaces, shading)
				: null;

			// The lit flat types name a material ramp rather than a colour, and the ramp is only
			// half a colour until the face's own light level picks a step along it — which happens
			// per instance, at draw time. See MeshVertex.ShadeRamp.
			int shadeRamp = IsRampShaded(polyObject) && shading is { HasShadeRamps: true }
				? ResolveShadeRamp(poly, group.Surfaces)
				: -1;

			Vector3[]? vertexNormals = ResolveVertexNormals(polyObject, group);
			Vector3? faceNormal = ResolveFaceNormal(poly, group);

			Vector3 color = solid?.Fill
				?? (rank == Ranks.UnresolvedTexture ? TextureFallbackColor : ResolveColor(poly, surfaceColors));
			Vector3 first = points[firstIndex];
			Vector3 localFirst = localPoints[firstIndex];
			int polyId = sink.NextPolyId();

			// A textured quad is mapped as a quad by the original, not as two triangles — see
			// QuadUvWeights.
			float[]? quadWeights = rect.HasValue
				? QuadUvWeights(points, group.Indexes, listStart)
				: null;

			// Polys are convex fans, so a triangle fan from the first vertex reproduces them.
			for (int i = 0; i < poly.VertexCount - 2; i++) {
				int i1 = group.Indexes[listStart + 1 + i];
				int i2 = group.Indexes[listStart + 2 + i];
				if (i1 < 0 || i1 >= points.Length || i2 < 0 || i2 >= points.Length) {
					continue;
				}

				if (rect is { } frame) {
					// With weights the corner's UV goes to the GPU premultiplied by its own weight and
					// is divided back per fragment; without them (a quad too degenerate to solve) the
					// mapping stays affine per triangle, as it was.
					var weights = quadWeights == null
						? (1f, 1f, 1f)
						: (quadWeights[0], quadWeights[i + 1], quadWeights[i + 2]);

					sink.Triangles.Add(new Triangle(first, points[i1], points[i2], color, rank, polyId,
						localFirst, localPoints[i1], localPoints[i2], group.Transform,
						UvAt(frame, 0) * weights.Item1,
						UvAt(frame, i + 1) * weights.Item2,
						UvAt(frame, i + 2) * weights.Item3,
						faceNormal: faceNormal,
						uvWeights: quadWeights == null ? default : weights));
				} else {
					// The fan's corners are vertex-list slots 0, i+1 and i+2, and the normal list is
					// parallel to it, so the same three slots index it.
					var corners = vertexNormals == null
						? ((Vector3, Vector3, Vector3)?)null
						: (vertexNormals[0], vertexNormals[i + 1], vertexNormals[i + 2]);

					sink.Triangles.Add(new Triangle(first, points[i1], points[i2], color, rank, polyId,
						localFirst, localPoints[i1], localPoints[i2], group.Transform,
						unlit: solid.HasValue, shadeRamp: shadeRamp, vertexNormals: corners,
						faceNormal: faceNormal));
				}
			}

			// The original's second pass over the same poly: its whole edge loop, re-drawn in the
			// surface's line colour, whenever that resolves to something other than the fill. See
			// MeshBuild.
			if (solid?.Line is { } lineColor) {
				for (int i = 0; i < poly.VertexCount; i++) {
					int from = group.Indexes[listStart + i];
					int to = group.Indexes[listStart + (i + 1) % poly.VertexCount];
					if (from < 0 || from >= points.Length || to < 0 || to >= points.Length) {
						continue;
					}

					sink.Outlines.Add(new OutlineEdge(points[from], points[to],
						localPoints[from], localPoints[to], lineColor, group.Transform, polyId));
				}
			}
		}
	}

	/// <summary>
	/// Walks a group's transform-id parent chain summing translations.
	///
	/// <para>Rotation is deliberately left unapplied here, and costs nothing: no retail shape's rest
	/// pose carries one. Every node of all 18 HERCs has a zero-rotation default transform, so this
	/// sum and <see cref="BuildSegments"/>'s full composition agree to the last vertex — checked
	/// against the built meshes' own bounds. Rotation is what an animated node acquires, and that
	/// path applies it.</para>
	/// </summary>
	private static Vector3 ResolveGroupOffset(TSGroup group, ANAnimList? animList) {
		if (animList?.Relations == null || animList.Transforms == null || animList.DefaultTransforms == null) {
			return Vector3.Zero;
		}

		var parentOf = new Dictionary<int, int>();
		foreach (var relation in animList.Relations) {
			parentOf[relation.Y] = relation.X;
		}

		Vector3 offset = Vector3.Zero;
		int transformId = group.Transform;

		for (int step = 0; transformId != -1 && step < MaxTransformChainSteps; step++) {
			if (transformId < 0 || transformId >= animList.DefaultTransforms.Length) {
				break;
			}

			int transformIndex = animList.DefaultTransforms[transformId];
			if (transformIndex < 0 || transformIndex >= animList.Transforms.Length) {
				break;
			}

			if (animList.Transforms[transformIndex].Translation is { } translation) {
				offset += new Vector3(translation.X, translation.Y, translation.Z);
			}

			if (!parentOf.TryGetValue(transformId, out int parentId)) {
				break;
			}
			transformId = parentId;
		}

		return offset;
	}

	/// <summary>
	/// The four corners' homogeneous UV weights for a textured quad, or null when the quad is too
	/// degenerate to solve (its diagonals do not cross inside it, or one of them has no length).
	///
	/// <para>With the quad's diagonals crossing at fraction <c>s</c> along <c>p0→p2</c> and <c>t</c>
	/// along <c>p1→p3</c>, the corners take <c>1/(1-s), 1/(1-t), 1/s, 1/t</c>. Why that is the right
	/// mapping, and what it fixes, is docs/formats/dts-texture-binding.md's "Quad mapping on triangle
	/// hardware".</para>
	///
	/// <para>The crossing is solved as a least-squares one rather than a planar intersection because
	/// a DTS quad is not guaranteed to be planar.</para>
	/// </summary>
	private static float[]? QuadUvWeights(Vector3[] points, short[] indexes, int listStart) {
		var corner = new Vector3[4];
		for (int i = 0; i < 4; i++) {
			int index = indexes[listStart + i];
			if (index < 0 || index >= points.Length) {
				return null;
			}
			corner[i] = points[index];
		}

		// p0 + s·d = p1 + (1-t)·(p1 - p3)  rearranged as  s·d + t·e = f.
		Vector3 d = corner[2] - corner[0];
		Vector3 e = corner[1] - corner[3];
		Vector3 f = corner[1] - corner[0];

		float dd = Vector3.Dot(d, d);
		float de = Vector3.Dot(d, e);
		float ee = Vector3.Dot(e, e);
		float determinant = dd * ee - de * de;
		if (MathF.Abs(determinant) < 1e-9f) {
			return null;
		}

		float df = Vector3.Dot(d, f);
		float ef = Vector3.Dot(e, f);
		float s = (df * ee - ef * de) / determinant;
		float t = (dd * ef - de * df) / determinant;

		// Outside that range the diagonals meet beyond the quad — a non-convex or self-crossing poly,
		// which has no projective map onto the rect. Left affine rather than guessed at.
		const float margin = 1e-3f;
		if (s <= margin || s >= 1f - margin || t <= margin || t >= 1f - margin) {
			return null;
		}

		return new[] { 1f / (1f - s), 1f / (1f - t), 1f / s, 1f / t };
	}

	/// <summary>
	/// Maps one of the four RE-confirmed quad corners onto a frame's rect inside the atlas.
	/// </summary>
	private static Vector2 UvAt(AtlasRect frame, int corner) {
		Vector2 unit = QuadCorners[corner];
		return new Vector2(
			frame.U0 + unit.X * (frame.U1 - frame.U0),
			frame.V0 + unit.Y * (frame.V1 - frame.V0));
	}

	/// <summary>
	/// Resolves a textured poly to its frame in the atlas. The frame index is
	/// <c>Surfaces[ColorIndexId / 4].FrontColor</c> — the <c>/ 4</c> because <c>ColorIndexId</c> is
	/// stored as <c>surfaceIndex * 4</c> (confirmed two independent ways, see
	/// <see cref="ResolveColor"/>), and <c>FrontColor</c> because nothing here backface-culls, so the
	/// front face is what gets drawn for every poly regardless of facing.
	/// </summary>
	private static AtlasRect? ResolveFrame(TSPoly poly, TSSurfaceEntry[]? surfaces, TextureAtlas? atlas) {
		if (atlas == null || surfaces == null || poly is not TSSolidPoly solidPoly) {
			return null;
		}

		int surfaceIndex = solidPoly.ColorIndexId / 4;
		if (surfaceIndex < 0 || surfaceIndex >= surfaces.Length) {
			return null;
		}

		return atlas.Frame(surfaces[surfaceIndex].FrontColor);
	}

	/// <summary>
	/// Resolves a poly's flat colour. <c>ColorIndexId</c> is stored on disk as
	/// <c>surfaceIndex * 4</c>, not a plain surface index — confirmed two independent ways, from
	/// VSHELL's own texture-poly render code and from the DTS reader's <c>colorCount / 4</c> read
	/// convention.
	/// </summary>
	private static Vector3 ResolveColor(TSPoly poly, Vector3[]? surfaceColors) {
		if (poly is TSSolidPoly solidPoly && surfaceColors != null) {
			int surfaceIndex = solidPoly.ColorIndexId / 4;
			if (surfaceIndex >= 0 && surfaceIndex < surfaceColors.Length) {
				return surfaceColors[surfaceIndex];
			}
		}

		return FallbackColor;
	}

	/// <summary>
	/// The <b>stand-in</b> colour for a flat-shaded surface, used only when the theater's palette
	/// carries no shade-ramp table: the surface value read as a frame index into the bound atlas,
	/// averaged. See <see cref="ResolveShadeRamp"/> for what the value actually is.
	/// </summary>
	private static Vector3 ResolveSurfaceColor(short frontColor, TextureAtlas? atlas) =>
		atlas?.AverageColor(frontColor) ?? FallbackColor;

	/// <summary>
	/// Whether a poly is one of the two <b>lit</b> flat types, whose surface value names a material
	/// ramp in the theater palette.
	///
	/// <para><see cref="TSGouraudPoly"/> names its ramp in the same field as
	/// <see cref="TSShadedPoly"/>: in <c>BASES.DGS</c> a shape's Gouraud and shaded polys share
	/// surface records and values (shape 5's groups mix both against ramps 0 and 12). They differ in
	/// how the ramp entry is spent — see <see cref="SurfaceShading.GouraudColor"/> — and in per-vertex
	/// versus per-face light, which <see cref="ResolveVertexNormals"/> supplies.</para>
	///
	/// <para>Excluded: <see cref="TSTexture4Poly"/>, also a <c>TSSolidPoly</c> subclass but whose
	/// value is a frame index (<see cref="ResolveFrame"/>), and plain <c>TSSolidPoly</c>, whose value
	/// is a palette index and which is never lit (<see cref="ResolveSolidColors"/>).</para>
	/// </summary>
	private static bool IsRampShaded(TSObject poly) =>
		poly is TSShadedPoly or TSGouraudPoly;

	/// <summary>
	/// A poly's own stored normal, in render space, or null when its index does not resolve.
	///
	/// <para><see cref="TSPoly.Normal"/> is a <b>point index</b>, dereferenced with the same 6-byte
	/// Vec3Short stride as a corner — <c>*(ushort *)(poly + 4) * 6 + DAT_006c696c</c> in every one of
	/// <c>TSSolidPoly_Render</c>, <c>TSShadedPoly_Render</c> and <c>TSTexture4Poly_Render</c>. It is
	/// what <c>TSPoly_FrontBackVisibilityTest</c> (<c>0048c620</c>) tests with, so it is what the
	/// front/back sign has to be derived from here.</para>
	///
	/// <para>It is <i>not</i> interchangeable with the winding — see <see cref="EmitTriangle"/> for
	/// the measurement and for what using the winding instead did to Gouraud polys.</para>
	/// </summary>
	private static Vector3? ResolveFaceNormal(TSPoly poly, TSGroup group) {
		if (group.Points == null || poly.Normal < 0 || poly.Normal >= group.Points.Length) {
			return null;
		}

		var point = group.Points[poly.Normal];
		var rendered = WorldScale.DtsToRender(point.X, point.Y, point.Z);
		return rendered.LengthSquared() > 1e-12f ? Vector3.Normalize(rendered) : null;
	}

	/// <summary>
	/// A <see cref="TSGouraudPoly"/>'s own per-corner normals, in render space, or null for any other
	/// poly — which is what makes it Gouraud rather than flat.
	///
	/// <para><b>The shape stores normals as extra entries in its point list.</b> Every poly carries
	/// <see cref="TSPoly.Normal"/>, a <i>point index</i> that DBSIM's renderers dereference as
	/// <c>points[index]</c> with the 6-byte Vec3Short stride (<c>*(ushort *)(poly + 4) * 6 +
	/// DAT_006c696c</c>, in all three of <c>TSSolidPoly_Render</c>, <c>TSShadedPoly_Render</c> and
	/// <c>TSTexture4Poly_Render</c>). <c>TSGouraudPoly.NormalList</c> is the per-vertex form of the
	/// same thing: an offset into the group's index array running parallel to
	/// <see cref="TSPoly.VertexList"/>, whose entries are point indices of normals rather than of
	/// corners.</para>
	///
	/// <para>Confirmed on <c>BASES.DGS</c> shape 11's eight side panels: every entry the list reaches
	/// has length <b>2048</b> — <see cref="MissionSun.NormalLength"/>, the <c>0x800</c> the shade
	/// calculation is scaled around — and adjacent panels share the normal at the edge between them,
	/// which is what wraps a smooth gradient around the mass instead of stepping it.</para>
	///
	/// <para>Returns null rather than a partial set if any entry is out of range, so a malformed list
	/// falls back to flat shading instead of half-smooth shading.</para>
	/// </summary>
	private static Vector3[]? ResolveVertexNormals(TSObject polyObject, TSGroup group) {
		if (polyObject is not TSGouraudPoly gouraud || group.Indexes == null || group.Points == null) {
			return null;
		}

		var normals = new Vector3[gouraud.VertexCount];
		for (int i = 0; i < normals.Length; i++) {
			int at = gouraud.NormalList + i;
			if (at < 0 || at >= group.Indexes.Length) {
				return null;
			}

			int pointIndex = group.Indexes[at];
			if (pointIndex < 0 || pointIndex >= group.Points.Length) {
				return null;
			}

			var normal = group.Points[pointIndex];
			var rendered = WorldScale.DtsToRender(normal.X, normal.Y, normal.Z);
			if (rendered.LengthSquared() <= 1e-12f) {
				return null;
			}

			normals[i] = Vector3.Normalize(rendered);
		}

		return normals;
	}

	/// <summary>
	/// The material ramp a lit flat surface names, or -1 when it names none.
	///
	/// <para>The value is <c>Surfaces[ColorIndexId / 4].FrontColor</c>, the same field and the same
	/// <c>/ 4</c> every other poly type reads (see <see cref="ResolveColor"/>) — it is what the value
	/// <i>means</i> that differs. <c>TSShadedPoly_Render</c> hands it to
	/// <c>Palette_ShadeRampLookup</c>, which treats it as a slot in the palette's own ramp table; see
	/// <see cref="SurfaceShading.ShadedColor"/>.</para>
	///
	/// <para>Unlike the solid path this does <b>not</b> reject a nonzero <c>FrontFlag</c>: every
	/// shaded surface in the retail files carries flag 1024 on its front pair, and rejecting that
	/// would exclude all of them. The original's own test is narrower — it skips the face only when
	/// <i>both</i> the front and back values put <c>0x14</c> in the top byte of the int32 they share
	/// with their flag, which is flag 5120, and which retail data uses on back faces only. Nothing
	/// here draws back faces, so the test has nothing to reject.</para>
	/// </summary>
	private static int ResolveShadeRamp(TSPoly poly, TSSurfaceEntry[]? surfaces) {
		if (surfaces == null || poly is not TSSolidPoly solid) {
			return -1;
		}

		int index = solid.ColorIndexId / 4;
		if (index < 0 || index >= surfaces.Length) {
			return -1;
		}

		short ramp = surfaces[index].FrontColor;
		if (ramp < 0) {
			return -1;
		}

		// The two lit types spend the ramp differently — TSShadedPoly through the theater .RMP at a
		// fixed row, TSGouraudPoly straight through the palette — so they address different halves of
		// the lookup table. See SurfaceShading.GouraudColor.
		return (ramp & 0xff) + (poly is TSGouraudPoly ? SurfaceRampTable.GouraudRowOffset : 0);
	}

	/// <summary>
	/// The colour of a plain <see cref="TSSolidPoly"/> — <b>a palette index run through the theater's
	/// own ramp</b>, which is a different mechanism from every other poly type's and was previously
	/// conflated with theirs.
	///
	/// <para><c>TSSolidPoly_Render</c> (DBSIM <c>00474db4</c>, reached from the DTS type registry's
	/// tag <c>0x00140002</c> entry, so this is the poly class by construction and not by structural
	/// resemblance) is short enough to quote whole: pick the front or back pair by the visibility
	/// test, bail if both carry the "none" marker, then</para>
	/// <code>
	/// fill = rampRow(0x80)[surface.Front];   line = rampRow(0x80)[surface.FrontLine];
	/// </code>
	/// <para>and hand both to <c>FUN_0048d518</c>, which fills the polygon in <c>fill</c> and then,
	/// when <c>line != fill</c>, re-draws the same polygon's edge loop in <c>line</c>. (That second
	/// pass is the rasterizer's mode 4, which is a line loop over the vertex list — confirmed down to
	/// <c>FUN_00483dac</c>'s own <c>iVar11 == 4</c> branch, which walks consecutive vertex pairs and
	/// closes back to the first.) There is no texture lookup, no frame index and — the part that
	/// shows — <b>no light term</b>: the shade byte is the literal <c>0x80</c>, so a solid face is the
	/// same brightness whichever way it faces. <see cref="Content.ShadeRamp"/> is that table.</para>
	///
	/// <para>The comparison that decides whether there is an outline at all is on the <b>ramped</b>
	/// bytes, not the raw surface values: the original ramps both before <c>FUN_0048d518</c> ever sees
	/// them, so two different palette indices that land on the same ramp output draw no outline.</para>
	///
	/// <para>Its sibling <c>TSShadedPoly</c> (tag <c>0x00140003</c>, <c>0047542c</c>) is the one that
	/// lights: it runs <c>Light_ComputeShadeForFace</c> and puts the result through the palette's own
	/// per-colour shade ramp before the same table. That is what almost every surface of a HERC or a
	/// building is — 1227 of APOCA's 1368 polys, 2049 of BASES_AN's — and it is <b>not</b> changed
	/// here; those keep the atlas-average stand-in and the renderer's own lighting.</para>
	///
	/// <para>Which is why this correction is small and safe as well as right. Retail geometry uses
	/// plain <c>TSSolidPoly</c> almost nowhere: 12 polys in <c>BULLETS.DTS</c>, 57 in
	/// <c>ROCKETS.DTS</c>, and 73 scattered across the whole mech and building fleet. The projectiles
	/// are the case that made it visible — their palette indices are 85, 93, 94, 104 and 246, which
	/// are the fire ramp and near-white, and none of which is a valid frame of the eight-frame
	/// <c>BULLETS.DBA</c> the old reading indexed. An autocannon round is meant to be gold.</para>
	///
	/// <para>Corroborated across the install: of the 1517 plain <c>TSSolidPoly</c> surfaces in every
	/// <c>.DTS</c> the game ships, <b>all 1517</b> carry a zero flag and a value inside 0-255. A frame
	/// index would have to fit each shape's own bank, and no distribution that tight to a byte is a
	/// frame index.</para>
	///
	/// <para>Returns null when there is no ramp loaded, when the surface index is out of range, or
	/// when the entry carries a nonzero flag — the flag occupies the high half of the same int32 the
	/// original indexes with, so a value that has one is not a plain colour and is left to the
	/// existing path. (The original's own test is narrower: a flag of 5120, which puts <c>0x14</c> in
	/// that int32's top byte, means "do not draw this face at all". Nothing in retail data reaches
	/// either case.)</para>
	/// </summary>
	private static SolidColors? ResolveSolidColors(TSPoly poly, TSSurfaceEntry[]? surfaces, SurfaceShading? shading) {
		if (shading == null || surfaces == null || poly is not TSSolidPoly solid) {
			return null;
		}

		int index = solid.ColorIndexId / 4;
		if (index < 0 || index >= surfaces.Length) {
			return null;
		}

		var surface = surfaces[index];
		if (surface.FrontFlag != 0 || surface.FrontColor < 0) {
			return null;
		}

		if (shading.Ramp.Resolve(surface.FrontColor, ShadeRamp.UnlitShade, shading.Palette) is not { } fill) {
			return null;
		}

		// The line colour is guarded exactly as the fill is — a nonzero flag means the entry is not a
		// plain colour, and retail's own "no outline" entries are the flagged -1 pair. Past that, the
		// original's test is on the ramp's output, so this one is too.
		Vector3? line = null;
		if (surface.FrontLineFlag == 0 && surface.FrontLineColor >= 0
			&& shading.Ramp.Lookup(surface.FrontLineColor, ShadeRamp.UnlitShade)
				!= shading.Ramp.Lookup(surface.FrontColor, ShadeRamp.UnlitShade)) {
			line = shading.Ramp.Resolve(surface.FrontLineColor, ShadeRamp.UnlitShade, shading.Palette);
		}

		return new SolidColors(fill, line);
	}

	/// <summary>
	/// The two colours a flat solid surface carries — see <see cref="ResolveSolidColors"/>.
	/// <paramref name="Line"/> is null when the surface draws no outline.
	/// </summary>
	private readonly record struct SolidColors(Vector3 Fill, Vector3? Line);
}
