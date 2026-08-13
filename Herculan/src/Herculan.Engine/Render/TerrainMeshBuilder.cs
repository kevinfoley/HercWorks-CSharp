using System.Numerics;
using Herculan.Engine.Gl;
using Herculan.Engine.Terrain;

namespace Herculan.Engine.Render;

/// <summary>
/// Turns a loaded <see cref="HeightGrid"/> into drawable triangles.
///
/// <para>The triangulation deliberately mirrors <see cref="HeightGrid.HeightAtWorld"/> exactly:
/// each quad is split along the diagonal that cell's own selector bits choose, so the surface being
/// drawn is the same surface the simulation queries. If the renderer used a fixed diagonal instead,
/// objects would visibly sit slightly above or below the ground on sloped cells — a whole class of
/// "why is the mech floating" bug avoided by construction rather than by tuning an offset later.</para>
///
/// <para>Flat-shaded lighting: one normal per triangle, no smoothing — which is what the original's
/// per-polygon terrain fill does, and keeps the mesh honest about the geometry actually being tested
/// against. Surface <i>colour</i> comes from the theater's texture bank when one is supplied (see
/// <see cref="TerrainTextureBank"/>) and from a height/slope ramp when it is not.</para>
/// </summary>
public static class TerrainMeshBuilder {
	/// <summary>
	/// Builds the whole zone as a single mesh. A retail 128x128 zone comes to about 32k triangles,
	/// which is small enough that chunking, culling and LOD are all premature — the original's own
	/// LOD parameter (<see cref="HeightGrid.DetailLod"/>) is carried on the grid for whenever they
	/// stop being premature.
	///
	/// <para>With a <paramref name="bank"/>, each cell gets the atlas rect its material selects; any
	/// cell whose material or frame does not resolve keeps the untextured ramp colour, which is why
	/// <see cref="MeshVertex.Textured"/> is per-vertex.</para>
	/// </summary>
	public static MeshVertex[] Build(HeightGrid grid, TerrainTextureBank? bank = null) {
		int quadsX = grid.Width - 1;
		int quadsY = grid.Height - 1;
		var vertices = new List<MeshVertex>(quadsX * quadsY * 6);

		// Normalise the colour gradient against this zone's own peak rather than a fixed height, so
		// a flat zone still reads as varied instead of uniformly low.
		float peak = MathF.Max(WorldScale.DistanceToRender(grid.MaxWorldHeight), 1f);

		for (int cellY = 0; cellY < quadsY; cellY++) {
			for (int cellX = 0; cellX < quadsX; cellX++) {
				Vector3 c00 = Corner(grid, cellX, cellY);
				Vector3 c10 = Corner(grid, cellX + 1, cellY);
				Vector3 c01 = Corner(grid, cellX, cellY + 1);
				Vector3 c11 = Corner(grid, cellX + 1, cellY + 1);

				// Corner UVs from the cell's own rect: u rises with cellX, v with cellY.
				var rect = bank?.CellRect(grid, cellX, cellY);
				bool textured = rect.HasValue;
				var r = rect ?? default;
				Vector2 t00 = new(r.U0, r.V0);
				Vector2 t10 = new(r.U1, r.V0);
				Vector2 t01 = new(r.U0, r.V1);
				Vector2 t11 = new(r.U1, r.V1);

				if (grid.DiagonalSelectorAt(cellX, cellY) == 2) {
					// Split along the c00-c11 diagonal, matching the height query's selector-2 case.
					AddTriangle(vertices, c00, c10, c11, t00, t10, t11, textured, peak);
					AddTriangle(vertices, c00, c11, c01, t00, t11, t01, textured, peak);
				} else {
					// Selector 0 splits along c01-c10; selectors 1 and 3 have no observed producer
					// and the height query treats them as a single plane through c00/c10/c01, which
					// this same split renders.
					AddTriangle(vertices, c00, c10, c01, t00, t10, t01, textured, peak);
					AddTriangle(vertices, c10, c11, c01, t10, t11, t01, textured, peak);
				}
			}
		}

		return vertices.ToArray();
	}

	private static Vector3 Corner(HeightGrid grid, int cellX, int cellY) =>
		WorldScale.ToRender(
			(float)cellX * grid.CellSize,
			(float)cellY * grid.CellSize,
			grid.WorldHeightAt(cellX, cellY));

	private static void AddTriangle(List<MeshVertex> vertices, Vector3 a, Vector3 b, Vector3 c,
			Vector2 uvA, Vector2 uvB, Vector2 uvC, bool textured, float peak) {
		Vector3 normal = Vector3.Cross(b - a, c - a);
		normal = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.UnitY;

		// The world-space mapping flips handedness relative to a naive reading of the grid, so
		// keep normals pointing up rather than trusting the cross product's sign.
		if (normal.Y < 0f) {
			normal = -normal;
		}

		Vector3 color = SurfaceColor((a.Y + b.Y + c.Y) / 3f, normal, peak);

		vertices.Add(new MeshVertex(a, normal, color, uvA, textured));
		vertices.Add(new MeshVertex(b, normal, color, uvB, textured));
		vertices.Add(new MeshVertex(c, normal, color, uvC, textured));
	}

	/// <summary>
	/// A stand-in surface colour: low ground reads sandy, high ground reads grey-rocky, and steep
	/// faces darken. This is presentation only — real terrain colour comes from the material index
	/// each cell already carries (<see cref="HeightGrid.MaterialIndexAt"/>) and the detail textures
	/// it selects, which is textured-rendering work the first milestone deliberately leaves out.
	/// </summary>
	private static Vector3 SurfaceColor(float renderHeight, Vector3 normal, float peak) {
		float elevation = System.Math.Clamp(renderHeight / peak, 0f, 1f);

		var low = new Vector3(0.44f, 0.40f, 0.28f);
		var high = new Vector3(0.58f, 0.58f, 0.56f);
		Vector3 color = Vector3.Lerp(low, high, elevation);

		float steepness = 1f - System.Math.Clamp(normal.Y, 0f, 1f);
		return Vector3.Lerp(color, color * 0.65f, steepness);
	}
}
