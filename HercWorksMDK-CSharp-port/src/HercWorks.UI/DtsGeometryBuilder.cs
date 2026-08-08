using System.Drawing;
using System.Numerics;
using HercWorks.Core.Data.File.Dts;
using HercWorks.Core.Data.File.Dts.Anim;
using HercWorks.Core.Data.File.Dts.Bsp;
using HercWorks.Core.Data.File.Dts.Part;
using HercWorks.Core.Data.File.Dts.Poly;
using HercWorks.Core.Data.File.Dyn;

namespace HercWorks.UI;

/// <summary>A single triangle in world space, already colored — ready for the rasterizer.</summary>
public readonly struct DtsTriangle {
	public Vector3 A { get; }
	public Vector3 B { get; }
	public Vector3 C { get; }
	public Color Color { get; }

	public DtsTriangle(Vector3 a, Vector3 b, Vector3 c, Color color) {
		A = a;
		B = b;
		C = c;
		Color = color;
	}
}

/// <summary>One top-level entry from DynamixThreeSpaceModel.Meshes, flattened to triangles.</summary>
public sealed class DtsRootMesh {
	public string Label { get; }
	public List<DtsTriangle> Triangles { get; }

	public DtsRootMesh(string label, List<DtsTriangle> triangles) {
		Label = label;
		Triangles = triangles;
	}
}

/// <summary>
/// Extracts renderable triangle geometry from a parsed DTS model tree. Lives in HercWorks.UI
/// rather than HercWorks.Core — same reasoning as DynamixImageRenderer: turning parsed file data
/// into renderer-ready floats/colors is a rendering concern, not a file-format concern.
///
/// DTS texture binding was never resolved (see TSBitmapPart's and TSSurfaceEntry's doc comments
/// in HercWorks.Core) — TSTexture4Poly polys render with a fixed placeholder color instead of an
/// actual texture. Multi-part placement uses the translation-only transform chain verified against
/// the independent convert_dts.py reference (rotation is left unapplied there too, since that
/// script's own comments call it "untested/probably wrong").
///
/// Each entry in DynamixThreeSpaceModel.Meshes (one DtsRootMesh here) is a fully independent
/// top-level object — confirmed against convert_dts.py's own export loop, which treats every
/// top-level ANShape as its own separate .3ds file with no grouping logic at all. There is no
/// in-file signal for whether a given file's roots are alternate LOD levels of one conceptual
/// object (e.g. SAMSON.DTS) or several unrelated objects bundled together for storage (e.g.
/// BASES_AN.DTS, MECHWPNS.DTS) — that relationship lives in the game engine, not the file. An
/// earlier version of this class tried to guess via bounding-sphere overlap; it was removed after
/// confirming it produced false positives (unrelated nearby objects misread as one LOD chain).
/// Callers should treat every root as independently selectable, never merge multiple at once.
///
/// That said, DTS does have a *real* in-file LOD mechanism — it just lives one level down, inside
/// each root's own tree, via TSDetailPart (see CollectDetailLevel's doc comment). Build() always
/// picks the highest detail level per root; BuildRoot()/GetDetailLevelCount() let a caller offer
/// the other levels for whichever single root is currently selected.
/// </summary>
public static class DtsGeometryBuilder {
	// TSGroup point/translation shorts are fixed-point tenths (see Vec3Short's FormatFixedPoint).
	private const float Unit = 1f / 10f;
	private const int MaxTransformChainSteps = 64;
	private static readonly Color TextureFallbackColor = Color.FromArgb(255, 120, 150, 190);

	public static List<DtsRootMesh> Build(DynamixThreeSpaceModel model) {
		var roots = new List<DtsRootMesh>();
		if (model.Meshes == null) {
			return roots;
		}

		int index = 0;
		foreach (var mesh in model.Meshes) {
			string label = $"{mesh.Header?.Id() ?? mesh.GetType().Name} #{index}";
			roots.Add(BuildRootInternal(mesh, label, null));
			index++;
		}

		return roots;
	}

	/// <summary>
	/// Rebuilds a single top-level root at a specific requested detail level, clamped per
	/// TSDetailPart node to its own available range — used when the user picks a different Detail
	/// Level for the currently-selected Part (see Model3DViewerForm.OnDetailLevelChanged).
	/// </summary>
	public static DtsRootMesh BuildRoot(TSObject root, string label, int detailLevelIndex) =>
		BuildRootInternal(root, label, detailLevelIndex);

	/// <summary>
	/// Highest TSDetailPart.Parts.Length found anywhere in this root's tree (0 if none) — how many
	/// Detail Level choices to offer for this Part. TSCellAnimPart is only descended into at its
	/// first frame, matching what actually gets rendered (see CollectFirstFrame).
	/// </summary>
	public static int GetDetailLevelCount(TSObject root) {
		int max = 0;
		CountDetailLevels(root, ref max);
		return max;
	}

	private static void CountDetailLevels(TSObject? node, ref int max) {
		switch (node) {
			case null:
				return;

			case TSDetailPart detailPart:
				if (detailPart.Parts != null) {
					max = Math.Max(max, detailPart.Parts.Length);
					foreach (var part in detailPart.Parts) {
						CountDetailLevels(part, ref max);
					}
				}
				break;

			case TSCellAnimPart cellAnimPart:
				if (cellAnimPart.Parts is { Length: > 0 } frames) {
					CountDetailLevels(frames[0], ref max);
				}
				break;

			case TSPartList partList:
				if (partList.Parts != null) {
					foreach (var part in partList.Parts) {
						CountDetailLevels(part, ref max);
					}
				}
				break;
		}
	}

	private static DtsRootMesh BuildRootInternal(TSObject root, string label, int? detailLevelIndex) {
		var triangles = new List<DtsTriangle>();
		CollectGroups(root, null, triangles, detailLevelIndex);
		triangles = DeduplicateCoincidentTriangles(triangles);
		return new DtsRootMesh(label, triangles);
	}

	/// <summary>
	/// Real DTS meshes can carry two triangles occupying the exact same surface — a textured poly
	/// (TSTexture4Poly, always rendered here as a flat placeholder color since texture binding is
	/// unresolved, see class doc comment) stacked precisely on a flat-shaded twin. Confirmed
	/// against SAMSON.DTS's root 0: 186 such pairs, centroid distance and normal both exactly
	/// identical. Left alone, both get rasterized at the exact same depth, and the Z-buffer tie
	/// between them flips from floating-point noise as the camera moves a fraction of a degree —
	/// visible as the surface flickering between the placeholder color and the shaded one. Since
	/// only one of the pair can ever look right without real texturing, keep a single triangle per
	/// coincident group, preferring whichever one isn't the texture placeholder.
	/// </summary>
	private static List<DtsTriangle> DeduplicateCoincidentTriangles(List<DtsTriangle> triangles) {
		var buckets = new Dictionary<(int, int, int, int, int, int), List<int>>();

		for (int i = 0; i < triangles.Count; i++) {
			var t = triangles[i];
			Vector3 centroid = (t.A + t.B + t.C) / 3f;
			Vector3 normal = Vector3.Cross(t.B - t.A, t.C - t.A);
			if (normal.LengthSquared() > 1e-8f) {
				normal = Vector3.Normalize(normal);
			}

			// Abs() on the normal so opposite-winding duplicates of the same surface still land in
			// the same bucket; coarse rounding tolerates minor float noise from the transform chain
			// without merging genuinely distinct, closely-spaced triangles.
			var key = (
				(int)MathF.Round(centroid.X * 4f), (int)MathF.Round(centroid.Y * 4f), (int)MathF.Round(centroid.Z * 4f),
				(int)MathF.Round(MathF.Abs(normal.X) * 100f), (int)MathF.Round(MathF.Abs(normal.Y) * 100f),
				(int)MathF.Round(MathF.Abs(normal.Z) * 100f));

			if (!buckets.TryGetValue(key, out var bucket)) {
				bucket = new List<int>();
				buckets[key] = bucket;
			}
			bucket.Add(i);
		}

		var toSkip = new HashSet<int>();
		foreach (var bucket in buckets.Values) {
			if (bucket.Count < 2) {
				continue;
			}

			int keepIndex = bucket[0];
			foreach (int i in bucket) {
				if (triangles[i].Color != TextureFallbackColor) {
					keepIndex = i;
					break;
				}
			}

			foreach (int i in bucket) {
				if (i != keepIndex) {
					toSkip.Add(i);
				}
			}
		}

		if (toSkip.Count == 0) {
			return triangles;
		}

		var result = new List<DtsTriangle>(triangles.Count - toSkip.Count);
		for (int i = 0; i < triangles.Count; i++) {
			if (!toSkip.Contains(i)) {
				result.Add(triangles[i]);
			}
		}
		return result;
	}

	/// <summary>
	/// DTS model space is Z-up (this engine's ThreeSpace/Torque lineage), with X/Y roughly
	/// symmetric about the origin (width/depth) and Z carrying the larger, rig-pivot-offset extent
	/// (height) — confirmed against real files (e.g. SAMSON.DTS: X and Y both ~symmetric about 0,
	/// Z spans -205..+391). The renderer's camera/ground-grid code assumes Y-up, so remap here via
	/// a proper rotation (determinant +1, so winding/handedness is preserved) rather than a raw
	/// axis swap (which would be a mirror reflection instead).
	/// </summary>
	private static Vector3 ToRenderSpace(Vector3 dtsSpace) => new(dtsSpace.X, dtsSpace.Z, -dtsSpace.Y);

	public static (Vector3 Center, float Radius) ComputeBounds(IEnumerable<DtsTriangle> triangles) {
		Vector3 min = new(float.MaxValue);
		Vector3 max = new(float.MinValue);
		bool any = false;

		foreach (var tri in triangles) {
			min = Vector3.Min(min, Vector3.Min(tri.A, Vector3.Min(tri.B, tri.C)));
			max = Vector3.Max(max, Vector3.Max(tri.A, Vector3.Max(tri.B, tri.C)));
			any = true;
		}

		if (!any) {
			return (Vector3.Zero, 1f);
		}

		Vector3 center = (min + max) * 0.5f;
		float radius = Vector3.Distance(min, max) * 0.5f;
		return (center, MathF.Max(radius, 1f));
	}

	/// <summary>
	/// Recursively walks the TSObject tree looking for geometry-bearing TSGroup/TSBSPGroup nodes.
	/// Container types (TSPartList and its subtypes TSShape/ANShape/TSBSPPart/TSDetailPart/
	/// TSCellAnimPart) are walked into via Parts; ANShape additionally swaps in its own
	/// AnimationList for everything beneath it. TSBitmapPart has no geometry and is skipped.
	/// </summary>
	private static void CollectGroups(TSObject? node, ANAnimList? animList, List<DtsTriangle> triangles, int? detailLevelIndex) {
		switch (node) {
			case null:
				return;

			case ANShape anShape:
				CollectFromParts(anShape.Parts, anShape.AnimationList ?? animList, triangles, detailLevelIndex);
				break;

			case TSDetailPart detailPart:
				CollectDetailLevel(detailPart, animList, triangles, detailLevelIndex);
				break;

			case TSCellAnimPart cellAnimPart:
				CollectFirstFrame(cellAnimPart, animList, triangles, detailLevelIndex);
				break;

			case TSBSPGroup bspGroup:
				AppendGroupTriangles(bspGroup, animList, triangles);
				break;

			case TSGroup group:
				AppendGroupTriangles(group, animList, triangles);
				break;

			case TSPartList partList:
				CollectFromParts(partList.Parts, animList, triangles, detailLevelIndex);
				break;
		}
	}

	private static void CollectFromParts(TSObject[]? parts, ANAnimList? animList, List<DtsTriangle> triangles, int? detailLevelIndex) {
		if (parts == null) {
			return;
		}

		foreach (var part in parts) {
			CollectGroups(part, animList, triangles, detailLevelIndex);
		}
	}

	/// <summary>
	/// TSDetailPart.Parts holds several complete alternate representations of the same
	/// sub-structure, one per level of detail, paired 1:1 with TSDetailPart.Details — ascending
	/// on-screen-size thresholds in the classic Torque/ThreeSpace "detail size" convention (always
	/// ending in 255 in files checked; geometry complexity increases alongside the paired value).
	/// Confirmed by dumping BASES_AN.DTS's full tree: every root has exactly one TSDetailPart with
	/// 5 siblings whose point/poly counts climb in step with their Details entry.
	///
	/// detailLevelIndex is null by default (Build()'s normal path) meaning "always the highest
	/// detail" — the one paired with the largest Details value, not just the last array entry, in
	/// case a file doesn't keep them ascending. A caller asking for a specific Detail Level (see
	/// Model3DViewerForm's Detail Level combo) passes a concrete index instead, clamped to this
	/// particular TSDetailPart's own range since different roots — or even different TSDetailPart
	/// nodes within one root — aren't guaranteed to have the same number of levels.
	/// </summary>
	private static void CollectDetailLevel(TSDetailPart detailPart, ANAnimList? animList, List<DtsTriangle> triangles, int? detailLevelIndex) {
		if (detailPart.Parts is not { Length: > 0 } parts) {
			return;
		}

		int chosenIndex = detailLevelIndex is int requested
			? Math.Clamp(requested, 0, parts.Length - 1)
			: HighestDetailIndex(detailPart, parts);

		CollectGroups(parts[chosenIndex], animList, triangles, detailLevelIndex);
	}

	private static int HighestDetailIndex(TSDetailPart detailPart, TSObject[] parts) {
		if (detailPart.Details is not { Length: > 0 } details) {
			return parts.Length - 1;
		}

		int count = Math.Min(details.Length, parts.Length);
		int bestIndex = 0;
		for (int i = 1; i < count; i++) {
			if (details[i] > details[bestIndex]) {
				bestIndex = i;
			}
		}
		return bestIndex;
	}

	/// <summary>
	/// TSCellAnimPart.Parts holds consecutive animation frames of one moving sub-part (e.g. a
	/// rotating radar dish) — confirmed by their near-identical point/poly counts and radii in a
	/// real tree dump, unlike TSDetailPart siblings which visibly grow in complexity. Walking into
	/// all of them (the old generic-TSPartList behavior) rendered every frame of the motion stacked
	/// on top of each other. This viewer doesn't play animations, so it always shows just the first
	/// frame (rest pose) regardless of the requested detail level.
	/// </summary>
	private static void CollectFirstFrame(TSCellAnimPart cellAnimPart, ANAnimList? animList, List<DtsTriangle> triangles, int? detailLevelIndex) {
		if (cellAnimPart.Parts is { Length: > 0 } parts) {
			CollectGroups(parts[0], animList, triangles, detailLevelIndex);
		}
	}

	private static void AppendGroupTriangles(TSGroup group, ANAnimList? animList, List<DtsTriangle> triangles) {
		if (group.Points == null || group.Indexes == null || group.Polys == null) {
			return;
		}

		Vector3 offset = ResolveGroupOffset(group, animList);

		var worldPoints = new Vector3[group.Points.Length];
		for (int i = 0; i < group.Points.Length; i++) {
			var p = group.Points[i];
			worldPoints[i] = ToRenderSpace(new Vector3(p.X, p.Y, p.Z) * Unit + offset);
		}

		Color[]? surfaceColors = null;
		if (group.Surfaces != null) {
			surfaceColors = new Color[group.Surfaces.Length];
			for (int i = 0; i < group.Surfaces.Length; i++) {
				surfaceColors[i] = ToColor(DefaultShapeColors.Color(group.Surfaces[i].FrontColor).Rgb());
			}
		}

		foreach (var polyObj in group.Polys) {
			if (polyObj is not TSPoly poly || poly.VertexCount < 3) {
				continue;
			}

			int listStart = poly.VertexList;
			if (listStart < 0 || listStart + poly.VertexCount > group.Indexes.Length) {
				continue;
			}

			int v0Index = group.Indexes[listStart];
			if (v0Index < 0 || v0Index >= worldPoints.Length) {
				continue;
			}
			Vector3 v0 = worldPoints[v0Index];

			Color color = ResolveColor(poly, surfaceColors);

			for (int i = 0; i < poly.VertexCount - 2; i++) {
				int i1 = group.Indexes[listStart + 1 + i];
				int i2 = group.Indexes[listStart + 2 + i];
				if (i1 < 0 || i1 >= worldPoints.Length || i2 < 0 || i2 >= worldPoints.Length) {
					continue;
				}

				triangles.Add(new DtsTriangle(v0, worldPoints[i1], worldPoints[i2], color));
			}
		}
	}

	/// <summary>
	/// Walks the transform-id parent chain (group.Transform -> ANAnimList.Relations, a list of
	/// (parent, child) pairs) summing translations, mirroring convert_dts.py's TSGroup.modelOut.
	/// Rotation is intentionally not applied — see class doc comment.
	/// </summary>
	private static Vector3 ResolveGroupOffset(TSGroup group, ANAnimList? animList) {
		if (animList?.Relations == null || animList.Transforms == null || animList.DefaultTransforms == null) {
			return Vector3.Zero;
		}

		var parentOf = new Dictionary<int, int>();
		foreach (var rel in animList.Relations) {
			parentOf[rel.Y] = rel.X;
		}

		Vector3 offset = Vector3.Zero;
		int tid = group.Transform;
		int steps = 0;

		while (tid != -1 && steps < MaxTransformChainSteps) {
			if (tid < 0 || tid >= animList.DefaultTransforms.Length) {
				break;
			}

			int transformIndex = animList.DefaultTransforms[tid];
			if (transformIndex < 0 || transformIndex >= animList.Transforms.Length) {
				break;
			}

			var translation = animList.Transforms[transformIndex].Translation;
			if (translation != null) {
				offset += new Vector3(translation.X, translation.Y, translation.Z) * Unit;
			}

			if (!parentOf.TryGetValue(tid, out int parentId)) {
				break;
			}
			tid = parentId;
			steps++;
		}

		return offset;
	}

	private static Color ResolveColor(TSPoly poly, Color[]? surfaceColors) {
		if (poly is TSTexture4Poly) {
			return TextureFallbackColor;
		}

		if (poly is TSSolidPoly solidPoly && surfaceColors != null
			&& solidPoly.ColorIndexId >= 0 && solidPoly.ColorIndexId < surfaceColors.Length) {
			return surfaceColors[solidPoly.ColorIndexId];
		}

		return Color.Gainsboro;
	}

	private static Color ToColor(double[] rgb) {
		return Color.FromArgb(255, ClampByte(rgb[0]), ClampByte(rgb[1]), ClampByte(rgb[2]));
	}

	private static byte ClampByte(double v) => (byte)Math.Clamp(v * 255.0, 0, 255);
}
