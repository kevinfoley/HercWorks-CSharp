using System.Numerics;
using HercWorks.Core.Data.File.Dts;
using HercWorks.Core.Data.File.Dts.Anim;
using HercWorks.Core.Data.File.Dts.Bsp;
using HercWorks.Core.Data.File.Dts.Part;
using HercWorks.Core.Data.File.Dts.Poly;
using HercWorks.Core.Data.File.Dyn;
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
/// into the mech's <c>.DBA</c> bank, and the four UV corners are the frame's own rect. Pass a
/// <see cref="TextureAtlas"/> to get that; without one they fall back to a flat placeholder, which is
/// honest about "no bank loaded" rather than quietly wrong.</para>
/// </summary>
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
		public Vector3 Color { get; }
		public Vector2 UvA { get; }
		public Vector2 UvB { get; }
		public Vector2 UvC { get; }

		/// <summary>Which twin of a coincident pair wins — see <see cref="DropCoincidentTwins"/>.</summary>
		public int Rank { get; }

		public Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 color, int rank,
				Vector2 uvA = default, Vector2 uvB = default, Vector2 uvC = default) {
			A = a;
			B = b;
			C = c;
			Color = color;
			Rank = rank;
			UvA = uvA;
			UvB = uvB;
			UvC = uvC;
		}
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
	public static MeshVertex[] BuildAll(DynamixThreeSpaceModel model, TextureAtlas? atlas = null) {
		var triangles = new List<Triangle>();
		if (model.Meshes != null) {
			foreach (var root in model.Meshes) {
				Collect(root, null, triangles, atlas);
			}
		}
		return Emit(triangles);
	}

	/// <summary>Builds one top-level root at its highest detail level.</summary>
	public static MeshVertex[] BuildRoot(TSObject root, TextureAtlas? atlas = null) {
		var triangles = new List<Triangle>();
		Collect(root, null, triangles, atlas);
		return Emit(triangles);
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

	private static MeshVertex[] Emit(List<Triangle> triangles) {
		var kept = DropCoincidentTwins(triangles);
		var vertices = new MeshVertex[kept.Count * 3];

		for (int i = 0; i < kept.Count; i++) {
			var triangle = kept[i];

			Vector3 normal = Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A);
			normal = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.UnitY;

			// Only a triangle that actually resolved to an atlas frame samples the texture; the rest
			// keep their colour, which is what makes the placeholder colour on an unresolved texture
			// poly visible instead of it sampling whatever sits at the atlas origin.
			bool textured = triangle.Rank == Ranks.Textured;

			vertices[i * 3] = new MeshVertex(triangle.A, normal, triangle.Color, triangle.UvA, textured);
			vertices[i * 3 + 1] = new MeshVertex(triangle.B, normal, triangle.Color, triangle.UvB, textured);
			vertices[i * 3 + 2] = new MeshVertex(triangle.C, normal, triangle.Color, triangle.UvC, textured);
		}

		return vertices;
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
	private static void Collect(TSObject? node, ANAnimList? animList, List<Triangle> triangles, TextureAtlas? atlas) {
		switch (node) {
			case null:
				return;

			case ANShape shape:
				// An ANShape brings its own animation list into scope for everything beneath it.
				CollectParts(shape.Parts, shape.AnimationList ?? animList, triangles, atlas);
				break;

			case TSDetailPart detailPart:
				CollectHighestDetail(detailPart, animList, triangles, atlas);
				break;

			case TSCellAnimPart cellAnimPart:
				// Consecutive frames of one moving sub-part (a rotating dish, say). Walking all of
				// them stacks every frame of the motion on top of itself, so take the rest pose.
				if (cellAnimPart.Parts is { Length: > 0 } frames) {
					Collect(frames[0], animList, triangles, atlas);
				}
				break;

			case TSBSPGroup bspGroup:
				AppendGroup(bspGroup, animList, triangles, atlas);
				break;

			case TSGroup group:
				AppendGroup(group, animList, triangles, atlas);
				break;

			case TSPartList partList:
				CollectParts(partList.Parts, animList, triangles, atlas);
				break;
		}
	}

	private static void CollectParts(TSObject[]? parts, ANAnimList? animList, List<Triangle> triangles, TextureAtlas? atlas) {
		if (parts == null) {
			return;
		}

		foreach (var part in parts) {
			Collect(part, animList, triangles, atlas);
		}
	}

	/// <summary>
	/// A <see cref="TSDetailPart"/> holds several complete alternate representations of the same
	/// sub-structure, paired 1:1 with ascending on-screen-size thresholds in <c>Details</c>. Picks
	/// the one paired with the largest threshold rather than simply the last entry, since nothing
	/// guarantees a file keeps them in ascending order.
	/// </summary>
	private static void CollectHighestDetail(TSDetailPart detailPart, ANAnimList? animList, List<Triangle> triangles, TextureAtlas? atlas) {
		if (detailPart.Parts is not { Length: > 0 } parts) {
			return;
		}

		int best = parts.Length - 1;
		if (detailPart.Details is { Length: > 0 } details) {
			int count = System.Math.Min(details.Length, parts.Length);
			best = 0;
			for (int i = 1; i < count; i++) {
				if (details[i] > details[best]) {
					best = i;
				}
			}
		}

		Collect(parts[best], animList, triangles, atlas);
	}

	private static void AppendGroup(TSGroup group, ANAnimList? animList, List<Triangle> triangles, TextureAtlas? atlas) {
		if (group.Points == null || group.Indexes == null || group.Polys == null) {
			return;
		}

		Vector3 offset = ResolveGroupOffset(group, animList);

		// Point shorts go straight through as world coordinates — see WorldScale.WorldUnitsPerDtsUnit
		// for the measurements behind that.
		var points = new Vector3[group.Points.Length];
		for (int i = 0; i < group.Points.Length; i++) {
			var point = group.Points[i];
			points[i] = WorldScale.DtsToRender(
				point.X + offset.X,
				point.Y + offset.Y,
				point.Z + offset.Z);
		}

		Vector3[]? surfaceColors = null;
		if (group.Surfaces != null) {
			surfaceColors = new Vector3[group.Surfaces.Length];
			for (int i = 0; i < group.Surfaces.Length; i++) {
				double[] rgb = DefaultShapeColors.Color(group.Surfaces[i].FrontColor).Rgb();
				surfaceColors[i] = new Vector3(
					(float)System.Math.Clamp(rgb[0], 0.0, 1.0),
					(float)System.Math.Clamp(rgb[1], 0.0, 1.0),
					(float)System.Math.Clamp(rgb[2], 0.0, 1.0));
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

			Vector3 color = rank == Ranks.UnresolvedTexture ? TextureFallbackColor : ResolveColor(poly, surfaceColors);
			Vector3 first = points[firstIndex];

			// Polys are convex fans, so a triangle fan from the first vertex reproduces them.
			for (int i = 0; i < poly.VertexCount - 2; i++) {
				int i1 = group.Indexes[listStart + 1 + i];
				int i2 = group.Indexes[listStart + 2 + i];
				if (i1 < 0 || i1 >= points.Length || i2 < 0 || i2 >= points.Length) {
					continue;
				}

				if (rect is { } frame) {
					triangles.Add(new Triangle(first, points[i1], points[i2], color, rank,
						UvAt(frame, 0), UvAt(frame, i + 1), UvAt(frame, i + 2)));
				} else {
					triangles.Add(new Triangle(first, points[i1], points[i2], color, rank));
				}
			}
		}
	}

	/// <summary>
	/// Walks a group's transform-id parent chain summing translations.
	///
	/// <para>Rotation is deliberately left unapplied. The independent <c>convert_dts.py</c>
	/// reference this chain was verified against does the same and calls its own rotation handling
	/// "untested/probably wrong"; applying an unverified rotation would scramble parts rather than
	/// place them. Mech models render correctly with translations alone, so this is a known
	/// limitation to revisit alongside animation, not a bug to paper over now.</para>
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
}
