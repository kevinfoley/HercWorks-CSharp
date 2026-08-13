using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using HercWorks.Core.Data.File.Dts;
using HercWorks.Core.Data.File.Dts.Anim;
using HercWorks.Core.Data.File.Dts.Bsp;
using HercWorks.Core.Data.File.Dts.Part;
using HercWorks.Core.Data.File.Dts.Poly;
using HercWorks.Core.Data.File.Dyn;

namespace HercWorks.UI;

/// <summary>
/// A single decoded DBA frame's pixels, pre-unpacked to ARGB for fast per-pixel sampling in the
/// rasterizer (avoids GDI+ GetPixel calls in the hot path).
/// </summary>
public readonly struct DtsTexture {
	public int[] Pixels { get; }
	public int Width { get; }
	public int Height { get; }

	public DtsTexture(int[] pixels, int width, int height) {
		Pixels = pixels;
		Width = width;
		Height = height;
	}
}

/// <summary>
/// A single triangle in world space — either flat-colored, or textured (Texture != null) with a
/// per-vertex UV in [0,1] to be perspective-correct-interpolated by the rasterizer.
/// </summary>
public readonly struct DtsTriangle {
	public Vector3 A { get; }
	public Vector3 B { get; }
	public Vector3 C { get; }
	public Color Color { get; }
	public DtsTexture? Texture { get; }
	public Vector2 UvA { get; }
	public Vector2 UvB { get; }
	public Vector2 UvC { get; }

	public DtsTriangle(Vector3 a, Vector3 b, Vector3 c, Color color) {
		A = a;
		B = b;
		C = c;
		Color = color;
		Texture = null;
		UvA = UvB = UvC = default;
	}

	public DtsTriangle(Vector3 a, Vector3 b, Vector3 c, DtsTexture texture, Vector2 uvA, Vector2 uvB, Vector2 uvC) {
		A = a;
		B = b;
		C = c;
		Color = Color.White;
		Texture = texture;
		UvA = uvA;
		UvB = uvB;
		UvC = uvC;
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
/// TSTexture4Poly texture-frame resolution (2026-08-11 follow-up, settling
/// docs/formats/dts-texture-binding.md's front/back stride question): ColorIndexId is stored on
/// disk as surfaceIndex*4 (confirmed two independent ways — fresh VSHELL.EXE disassembly of
/// TSTexture4Poly_Render, and DTSModelTransformer.cs's own colorCount/4 read convention, unrelated
/// to the exe RE). Frame = group.Surfaces[ColorIndexId/4].FrontColor — this is the same frame index
/// value both of TSTexture4Poly_Render's internal code paths agree on (see that doc's "UV-generation
/// formula" section for why there even are two paths); Images[frame] is the natural C#-side target
/// regardless of which internal path the exe takes to get there.
///
/// UV-corner mapping (2026-08-11 second follow-up, RE-confirmed): decoding TSTexture4Poly_Render's
/// real scanline-rasterizer path (previously misidentified — an earlier pass thought a DIFFERENT,
/// non-texturing fallback branch was "the normal case") found the exe assigns UV corners to a
/// poly's 4 vertices as (left,top)/(right,top)/(right,bottom)/(left,bottom) in vertex order — i.e.
/// exactly the (0,0)/(1,0)/(1,1)/(0,1) topology already used here, now confirmed correct in *order*
/// rather than just assumed. The one remaining unconfirmed piece: this assumes each DBA frame is its
/// own independently-cropped image (matching how Images[] is already parsed, each with its own
/// Cols/Rows) rather than a shared-atlas sub-rect with a nonzero top-left offset — the exe's
/// descriptor-table builder that would settle that with certainty wasn't traced (see that doc's open
/// follow-ups).
///
/// The engine's real front/back visibility test (picking BackColor for back-facing views) is not
/// implemented — this renderer never backface-culls (see Model3DViewerControl's doc comment), so a
/// poly's texture is always resolved once via FrontColor regardless of view angle. Without a texture
/// bank loaded, TSTexture4Poly still falls back to the original fixed placeholder color rather than
/// silently guessing. TSBitmapPart geometry is still not built at all (billboard quads need
/// per-frame camera-facing geometry, a bigger architecture change than this pass — see that doc,
/// mechanism is fully confirmed and simple whenever that's tackled).
///
/// Multi-part placement uses the translation-only transform chain verified against the independent
/// convert_dts.py reference (rotation is left unapplied there too, since that script's own comments
/// call it "untested/probably wrong").
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

	// Vertex-order UV corners for a TSTexture4Poly quad — RE-confirmed order (top-left/top-right/
	// bottom-right/bottom-left), see class doc comment's "UV-corner mapping" note for the one
	// remaining unconfirmed assumption (no shared-atlas frames).
	private static readonly Vector2[] QuadUvCorners = {
		new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f)
	};

	/// <summary>
	/// Decodes and caches DBA frames on demand for one Build() call. Null Bank means "no texture
	/// bank loaded" — callers fall back to the flat placeholder in that case, same as before this
	/// feature existed.
	/// </summary>
	private sealed class TextureContext {
		private readonly DynamixBitmapArray? _bank;
		private readonly DynamixPalette? _palette;
		private readonly Dictionary<int, DtsTexture?> _cache = new();

		public TextureContext(DynamixBitmapArray? bank, DynamixPalette? palette) {
			_bank = bank;
			_palette = palette;
		}

		public DtsTexture? Resolve(int frameIndex) {
			if (_bank?.Images is not { } images || frameIndex < 0 || frameIndex >= images.Length) {
				return null;
			}

			if (_cache.TryGetValue(frameIndex, out var cached)) {
				return cached;
			}

			DtsTexture? texture = DecodeFrame(images[frameIndex], _palette);
			_cache[frameIndex] = texture;
			return texture;
		}

		private static DtsTexture? DecodeFrame(Core.Data.File.Dyn.DynamixBitmap frame, DynamixPalette? palette) {
			if (frame.Cols <= 0 || frame.Rows <= 0) {
				return null;
			}

			using var bmp = DynamixImageRenderer.RenderFrame(frame, palette);
			int width = bmp.Width, height = bmp.Height;
			var pixels = new int[width * height];
			var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);
			bmp.UnlockBits(bmpData);
			return new DtsTexture(pixels, width, height);
		}
	}

	public static List<DtsRootMesh> Build(DynamixThreeSpaceModel model) => Build(model, null, null);

	/// <summary>
	/// Same as Build(model), but resolves TSTexture4Poly polys to real decoded textures from the
	/// given DBA (+ optional palette) instead of the flat placeholder color — see class doc comment.
	/// </summary>
	public static List<DtsRootMesh> Build(DynamixThreeSpaceModel model, DynamixBitmapArray? textureBank, DynamixPalette? palette) {
		var roots = new List<DtsRootMesh>();
		if (model.Meshes == null) {
			return roots;
		}

		var texCtx = textureBank != null ? new TextureContext(textureBank, palette) : null;

		int index = 0;
		foreach (var mesh in model.Meshes) {
			string label = $"{mesh.Header?.Id() ?? mesh.GetType().Name} #{index}";
			roots.Add(BuildRootInternal(mesh, label, null, texCtx));
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
		BuildRoot(root, label, detailLevelIndex, null, null);

	/// <summary>Same as BuildRoot, but with texture resolution — see Build's texture overload.</summary>
	public static DtsRootMesh BuildRoot(TSObject root, string label, int detailLevelIndex,
			DynamixBitmapArray? textureBank, DynamixPalette? palette) =>
		BuildRoot(root, label, (int?)detailLevelIndex, textureBank, palette);

	/// <summary>
	/// Same as BuildRoot, but detailLevelIndex is nullable — null means "always highest detail",
	/// matching Build()'s own default (see HighestDetailIndex), for callers rebuilding an
	/// already-loaded root (e.g. after loading a texture bank) without disturbing whichever detail
	/// level happened to already be selected.
	/// </summary>
	public static DtsRootMesh BuildRoot(TSObject root, string label, int? detailLevelIndex,
			DynamixBitmapArray? textureBank, DynamixPalette? palette) {
		var texCtx = textureBank != null ? new TextureContext(textureBank, palette) : null;
		return BuildRootInternal(root, label, detailLevelIndex, texCtx);
	}

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

	private static DtsRootMesh BuildRootInternal(TSObject root, string label, int? detailLevelIndex, TextureContext? texCtx) {
		var triangles = new List<DtsTriangle>();
		CollectGroups(root, null, triangles, detailLevelIndex, texCtx);
		triangles = DeduplicateCoincidentTriangles(triangles);
		return new DtsRootMesh(label, triangles);
	}

	/// <summary>
	/// Real DTS meshes can carry two triangles occupying the exact same surface — a textured poly
	/// (TSTexture4Poly, always rendered here as a flat placeholder color, see class doc comment)
	/// stacked precisely on a flat-shaded twin. Confirmed against SAMSON.DTS's root 0: 186 such
	/// pairs, centroid distance and normal both exactly identical. Left alone, both get rasterized
	/// at the exact same depth, and the Z-buffer tie between them flips from floating-point noise
	/// as the camera moves a fraction of a degree — visible as the surface flickering between the
	/// placeholder color and the shaded one. Since only one of the pair can ever look right without
	/// real texturing, keep a single triangle per coincident group, preferring whichever one isn't
	/// the texture placeholder.
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
				if (triangles[i].Texture != null || triangles[i].Color != TextureFallbackColor) {
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
	private static void CollectGroups(TSObject? node, ANAnimList? animList, List<DtsTriangle> triangles, int? detailLevelIndex, TextureContext? texCtx) {
		switch (node) {
			case null:
				return;

			case ANShape anShape:
				CollectFromParts(anShape.Parts, anShape.AnimationList ?? animList, triangles, detailLevelIndex, texCtx);
				break;

			case TSDetailPart detailPart:
				CollectDetailLevel(detailPart, animList, triangles, detailLevelIndex, texCtx);
				break;

			case TSCellAnimPart cellAnimPart:
				CollectFirstFrame(cellAnimPart, animList, triangles, detailLevelIndex, texCtx);
				break;

			case TSBSPGroup bspGroup:
				AppendGroupTriangles(bspGroup, animList, triangles, texCtx);
				break;

			case TSGroup group:
				AppendGroupTriangles(group, animList, triangles, texCtx);
				break;

			case TSPartList partList:
				CollectFromParts(partList.Parts, animList, triangles, detailLevelIndex, texCtx);
				break;
		}
	}

	private static void CollectFromParts(TSObject[]? parts, ANAnimList? animList, List<DtsTriangle> triangles, int? detailLevelIndex, TextureContext? texCtx) {
		if (parts == null) {
			return;
		}

		foreach (var part in parts) {
			CollectGroups(part, animList, triangles, detailLevelIndex, texCtx);
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
	private static void CollectDetailLevel(TSDetailPart detailPart, ANAnimList? animList, List<DtsTriangle> triangles, int? detailLevelIndex, TextureContext? texCtx) {
		if (detailPart.Parts is not { Length: > 0 } parts) {
			return;
		}

		int chosenIndex = detailLevelIndex is int requested
			? Math.Clamp(requested, 0, parts.Length - 1)
			: HighestDetailIndex(detailPart, parts);

		CollectGroups(parts[chosenIndex], animList, triangles, detailLevelIndex, texCtx);
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
	private static void CollectFirstFrame(TSCellAnimPart cellAnimPart, ANAnimList? animList, List<DtsTriangle> triangles, int? detailLevelIndex, TextureContext? texCtx) {
		if (cellAnimPart.Parts is { Length: > 0 } parts) {
			CollectGroups(parts[0], animList, triangles, detailLevelIndex, texCtx);
		}
	}

	private static void AppendGroupTriangles(TSGroup group, ANAnimList? animList, List<DtsTriangle> triangles, TextureContext? texCtx) {
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

			DtsTexture? texture = null;
			if (poly is TSTexture4Poly tex4Poly && texCtx != null && poly.VertexCount == 4) {
				int? frameIndex = ResolveTextureFrame(tex4Poly, group.Surfaces);
				if (frameIndex is int fi) {
					texture = texCtx.Resolve(fi);
				}
			}

			Color color = ResolveColor(poly, surfaceColors);

			for (int i = 0; i < poly.VertexCount - 2; i++) {
				int i1 = group.Indexes[listStart + 1 + i];
				int i2 = group.Indexes[listStart + 2 + i];
				if (i1 < 0 || i1 >= worldPoints.Length || i2 < 0 || i2 >= worldPoints.Length) {
					continue;
				}

				if (texture is { } tex) {
					triangles.Add(new DtsTriangle(v0, worldPoints[i1], worldPoints[i2], tex,
						QuadUvCorners[0], QuadUvCorners[i + 1], QuadUvCorners[i + 2]));
				} else {
					triangles.Add(new DtsTriangle(v0, worldPoints[i1], worldPoints[i2], color));
				}
			}
		}
	}

	/// <summary>
	/// Resolves a TSTexture4Poly's ColorIndexId to a DBA frame index. ColorIndexId is stored on
	/// disk as surfaceIndex*4, not a plain surface index — see class doc comment's front/back
	/// stride settlement. Always uses FrontColor (never BackColor); see class doc comment for why.
	/// </summary>
	private static int? ResolveTextureFrame(TSTexture4Poly poly, TSSurfaceEntry[]? surfaces) {
		if (surfaces == null) {
			return null;
		}

		int surfaceIndex = poly.ColorIndexId / 4;
		if (surfaceIndex < 0 || surfaceIndex >= surfaces.Length) {
			return null;
		}

		short frame = surfaces[surfaceIndex].FrontColor;
		return frame >= 0 ? frame : null;
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

		// ColorIndexId is surfaceIndex*4 for every TSSolidPoly subtype, not a plain surface index —
		// see class doc comment's front/back stride settlement (confirmed via TSTexture4Poly's own
		// render code, but ColorIndexId is one shared inherited field read the same way regardless
		// of poly subtype, so the same /4 applies here too).
		if (poly is TSSolidPoly solidPoly && surfaceColors != null) {
			int surfaceIndex = solidPoly.ColorIndexId / 4;
			if (surfaceIndex >= 0 && surfaceIndex < surfaceColors.Length) {
				return surfaceColors[surfaceIndex];
			}
		}

		return Color.Gainsboro;
	}

	private static Color ToColor(double[] rgb) {
		return Color.FromArgb(255, ClampByte(rgb[0]), ClampByte(rgb[1]), ClampByte(rgb[2]));
	}

	private static byte ClampByte(double v) => (byte)Math.Clamp(v * 255.0, 0, 255);
}
