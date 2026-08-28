using System.Buffers.Binary;
using Herculan.Engine.Content;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.World;

/// <summary>
/// One sphere of a collision model, in shape space — four <c>int16</c>s on disk, and the smallest
/// unit of hit geometry the simulation has.
/// </summary>
/// <param name="X">Centre, shape X.</param>
/// <param name="Y">Centre, shape Y.</param>
/// <param name="Z">Centre, shape Z.</param>
/// <param name="Radius">Radius in world units.</param>
public readonly record struct CollisionSphere(short X, short Y, short Z, short Radius);

/// <summary>
/// One component's hit volume: a cluster of spheres plus the bound around them.
///
/// <para>The bound is not on disk — <c>Collision_ComputeBoundingSphere</c> (<c>0040c5d0</c>) builds
/// it at load time as the AABB of the children each inflated by its own radius, centred on that
/// box's midpoint, with the radius taken as <see cref="SimMath.FastMagnitude3D"/> of the
/// half-extents. That approximation is part of the behaviour, not an implementation detail: it runs
/// a few percent under a true Euclidean radius, so the bound is very slightly tight.</para>
/// </summary>
/// <param name="ComponentIndex">
/// Which of the owning type's components this cluster belongs to. For a structure that indexes
/// <see cref="BaseType.Components"/>; for a mech or a flyer it indexes the <c>.DMG</c> file's own
/// component array (see <see cref="Herculan.Engine.Sim.ComponentDamage"/>). The hit test skips a
/// cluster whose component is already destroyed, so an object loses its hit volume piece by piece
/// as it comes apart.
/// </param>
/// <param name="Spheres">The cluster's spheres.</param>
/// <param name="Bound">The sphere around all of them — the cheap first test.</param>
public readonly record struct CollisionCluster(
	short ComponentIndex, CollisionSphere[] Spheres, CollisionSphere Bound);

/// <summary>
/// One node's worth of collision clusters. <see cref="NodeIndex"/> is which of the shape's node
/// transforms the spheres are expressed in; <c>-1</c> means the object's own frame.
/// </summary>
/// <param name="NodeIndex">
/// The shape node the cluster's spheres are placed by, or <c>-1</c> for the object frame. The
/// original resolves a non-negative one through the shape instance's node-transform array
/// (<c>Mech_ComponentGeometryTest_Candidate</c>, <c>0040c8fc</c>) so that a moving part carries its
/// hit volume with it — see <see cref="Herculan.Engine.Sim.CollisionModel.Test"/> for what the
/// engine does with one.
/// </param>
/// <param name="Clusters">The clusters placed by that node.</param>
public readonly record struct CollisionNode(short NodeIndex, CollisionCluster[] Clusters);

/// <summary>
/// The reader behind every hit-sphere model in the simulation: <c>Collision_LoadRecordArray</c>
/// (<c>0040ccf8</c>) and the three functions under it (<c>Collision_ReadNode</c> <c>0040cc50</c>,
/// <c>Collision_ReadCluster</c> <c>0040cc14</c>, <c>Collision_ReadSphereArray</c> <c>0040c7c4</c>).
///
/// <para><b>One format, two sources.</b> Structures read 65 of these back to back out of
/// <c>dat\BASECOL.DAT</c> (see <see cref="BaseCollisionTable"/>); mechs and flyers each read one
/// whole file, <c>col\&lt;NAME&gt;.COL</c>, through <c>Collision_RegisterObject</c>
/// (<c>0040cd88</c>) — the mech from its constructor (<c>00415bb0</c>, into <c>mech+0x1f6</c>) and
/// the flyer from its type loader (<c>00422ed0</c>, into <c>flyerTypeRec+0x32</c>). Retail ships 22
/// <c>.COL</c> files, one per HERC plus <c>SKIMMER</c>.</para>
///
/// <para><b>Format</b> — every field is an <c>int16</c>:</para>
/// <code>
/// nodeCount, then per node:
///   nodeIndex, clusterCount, then per cluster:
///     componentIndex, sphereCount, then sphereCount * { x, y, z, radius }
/// </code>
///
/// <para>The field order inside a cluster is worth stating because it is not the struct order: the
/// original reads the component index into the record's <c>+6</c> first and the sphere count into
/// its <c>+0</c> second, because the two live in different functions.</para>
/// </summary>
public static class CollisionModelReader {
	/// <summary>VOL folder the per-type <c>.COL</c> files live in.</summary>
	public const string ResourceFolder = "col";

	/// <summary>The node index meaning "these spheres are in the object's own frame".</summary>
	public const short ObjectFrameNode = -1;

	/// <summary>
	/// Mask applied to a cluster's sphere count. The original reads the field and tests
	/// <c>value &amp; 0x1fff</c>, reserving the top three bits for flags that no retail record sets
	/// — but it then allocates and reads using the <i>unmasked</i> value, so the mask is only a
	/// zero-test. Reproduced as written.
	/// </summary>
	private const int SphereCountMask = 0x1fff;

	/// <summary>
	/// One object's whole model — <c>Collision_LoadRecordArray</c> itself. Advances
	/// <paramref name="offset"/> past everything it read, which is what lets
	/// <see cref="BaseCollisionTable"/> walk 65 of them out of one stream.
	/// </summary>
	public static CollisionNode[] Read(byte[] bytes, ref int offset) {
		int cursor = offset;

		short Next() {
			short value = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(cursor));
			cursor += 2;
			return value;
		}

		int nodeCount = Next();
		var nodes = new CollisionNode[nodeCount < 0 ? 0 : nodeCount];

		for (int n = 0; n < nodes.Length; n++) {
			short nodeIndex = Next();
			int clusterCount = Next();
			var clusters = new CollisionCluster[clusterCount < 0 ? 0 : clusterCount];

			for (int c = 0; c < clusters.Length; c++) {
				short componentIndex = Next();
				short sphereCount = Next();
				var spheres = new CollisionSphere[(sphereCount & SphereCountMask) != 0 ? sphereCount : 0];

				for (int s = 0; s < spheres.Length; s++) {
					spheres[s] = new CollisionSphere(Next(), Next(), Next(), Next());
				}

				clusters[c] = new CollisionCluster(componentIndex, spheres, BoundOf(spheres));
			}

			nodes[n] = new CollisionNode(nodeIndex, clusters);
		}

		offset = cursor;
		return nodes;
	}

	/// <summary>
	/// One type's <c>col\&lt;NAME&gt;.COL</c>, or an empty model when the install has no such file.
	/// Two of the three flyer names legitimately have none — retail ships a <c>.COL</c> for
	/// <c>SKIMMER</c> only — and an object with no model is simply not shootable, which is what the
	/// original ends up with too.
	/// </summary>
	public static CollisionNode[] Load(GameContent content, string typeName) {
		byte[]? bytes = content.Read(ResourceFolder, typeName + ".COL");
		if (bytes == null || bytes.Length < 2) {
			return Array.Empty<CollisionNode>();
		}

		int offset = 0;
		return Read(bytes, ref offset);
	}

	/// <summary>
	/// <c>Collision_ComputeBoundingSphere</c> (<c>0040c5d0</c>) — the AABB of the children each
	/// inflated by its own radius, its midpoint as the centre, and the fast magnitude of the
	/// half-extents as the radius.
	///
	/// <para>The original seeds the box at ±10000 and does not special-case an empty cluster, which
	/// would leave it inverted; nothing in the retail data has one, and an empty bound here is
	/// returned as a zero sphere so the test simply misses.</para>
	/// </summary>
	private static CollisionSphere BoundOf(CollisionSphere[] spheres) {
		if (spheres.Length == 0) {
			return default;
		}

		short minX = short.MaxValue, minY = short.MaxValue, minZ = short.MaxValue;
		short maxX = short.MinValue, maxY = short.MinValue, maxZ = short.MinValue;

		foreach (var s in spheres) {
			minX = Math.Min(minX, (short)(s.X - s.Radius));
			minY = Math.Min(minY, (short)(s.Y - s.Radius));
			minZ = Math.Min(minZ, (short)(s.Z - s.Radius));
			maxX = Math.Max(maxX, (short)(s.X + s.Radius));
			maxY = Math.Max(maxY, (short)(s.Y + s.Radius));
			maxZ = Math.Max(maxZ, (short)(s.Z + s.Radius));
		}

		short centerX = (short)((maxX + minX) >> 1);
		short centerY = (short)((maxY + minY) >> 1);
		short centerZ = (short)((maxZ + minZ) >> 1);

		int radius = SimMath.FastMagnitude3D(
			(short)(maxX - centerX), (short)(maxY - centerY), (short)(maxZ - centerZ));

		return new CollisionSphere(centerX, centerY, centerZ, (short)radius);
	}
}
