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
/// Which of the type's components this cluster belongs to, indexing
/// <see cref="BaseType.Components"/>. The hit test skips a cluster whose component is already
/// destroyed, so a building loses its hit volume piece by piece as it comes apart.
/// </param>
/// <param name="Spheres">The cluster's spheres.</param>
/// <param name="Bound">The sphere around all of them — the cheap first test.</param>
public readonly record struct CollisionCluster(
	short ComponentIndex, CollisionSphere[] Spheres, CollisionSphere Bound);

/// <summary>
/// One node's worth of collision clusters. <see cref="NodeIndex"/> is which of the shape's node
/// transforms the spheres are expressed in; <c>-1</c> means the object's own frame, which is what
/// every static structure uses and what the engine supports.
/// </summary>
/// <param name="NodeIndex">
/// The shape node the cluster's spheres are placed by, or <c>-1</c> for the object frame. The
/// original resolves a non-negative one through the shape instance's node-transform array
/// (<c>Mech_ComponentGeometryTest_Candidate</c>, <c>0040c8fc</c>) so that a moving part carries its
/// hit volume with it; the engine has no node transforms for structures yet, so those records are
/// carried and skipped rather than tested in the wrong place — see
/// <see cref="Herculan.Engine.Sim.CollisionModel.Test"/>.
/// </param>
/// <param name="Clusters">The clusters placed by that node.</param>
public readonly record struct CollisionNode(short NodeIndex, CollisionCluster[] Clusters);

/// <summary>
/// <c>dat\BASECOL.DAT</c> — one hit-sphere model per structure type, in the same order as
/// <c>dat\BASES.DAT</c>'s 65 types, read as one continuous stream at the tail of
/// <c>Bases_LoadTypeTable</c> (<c>0043a2e0</c>).
///
/// <para><b>Format</b>, from <c>Collision_LoadRecordArray</c> (<c>0040ccf8</c>) and the three
/// readers below it (<c>Collision_LoadSubSpheres</c> <c>0040cc50</c>,
/// <c>Collision_LoadSubSphereFlag</c> <c>0040cc14</c>, <c>Collision_LoadSubMeshIndices</c>
/// <c>0040c7c4</c>) — every field is an <c>int16</c>:</para>
/// <code>
/// per type:   nodeCount, then per node:
///               nodeIndex, clusterCount, then per cluster:
///                 componentIndex, sphereCount, then sphereCount * { x, y, z, radius }
/// </code>
///
/// <para>The field order inside a cluster is worth stating because it is not the struct order: the
/// original reads the component index into the record's <c>+6</c> first and the sphere count into
/// its <c>+0</c> second, because the two live in different functions.</para>
///
/// <para><b>Verified against the retail file</b> (4,938 content bytes): the walk lands exactly on
/// the end after 65 types, and the geometry reads as deliberate hand-authored hitboxes — a
/// three-component bunker with a sphere cluster per section, a gun tower with a separate cluster per
/// barrel. Types whose <see cref="BaseType.HasCollisionModel"/> is false state a count of zero, with
/// three exceptions that carry a full model the type flag leaves unused.</para>
///
/// <para><b>Not the whole system.</b> Mechs and flyers have collision models of their own, loaded
/// by name through the same readers (<c>FUN_0040cd88</c>) from per-type files rather than from this
/// one table. Porting those is what would let <c>Mech_SelectStruckComponent</c> pick a struck HERC
/// component; this table only covers structures.</para>
/// </summary>
public sealed class BaseCollisionTable {
	/// <summary>VOL folder and name of the table.</summary>
	public const string ResourceFolder = "dat";

	/// <summary>The table's resource name.</summary>
	public const string ResourceName = "BASECOL.DAT";

	/// <summary>The node index meaning "these spheres are in the object's own frame".</summary>
	public const short ObjectFrameNode = -1;

	/// <summary>
	/// Mask applied to a cluster's sphere count. The original reads the field and tests
	/// <c>value &amp; 0x1fff</c>, reserving the top three bits for flags that no retail record sets
	/// — but it then allocates and reads using the <i>unmasked</i> value, so the mask is only a
	/// zero-test. Reproduced as written.
	/// </summary>
	private const int SphereCountMask = 0x1fff;

	private readonly CollisionNode[][] _models;

	private BaseCollisionTable(CollisionNode[][] models) {
		_models = models;
	}

	/// <summary>How many types the table covers.</summary>
	public int Count => _models.Length;

	/// <summary>
	/// The model for a type, or an empty array when the file has none for it. Whether the model is
	/// actually used is <see cref="BaseType.HasCollisionModel"/>, not whether this is empty.
	/// </summary>
	public CollisionNode[] this[int typeIndex] =>
		typeIndex >= 0 && typeIndex < _models.Length ? _models[typeIndex] : Array.Empty<CollisionNode>();

	/// <param name="content">Mounted archives.</param>
	/// <param name="typeCount">
	/// How many types to read, which is <see cref="BaseTypeTable.Count"/> — the file carries no count
	/// of its own, exactly as the original reads it.
	/// </param>
	public static BaseCollisionTable Load(GameContent content, int typeCount) {
		byte[] bytes = content.ReadRequired(ResourceFolder, ResourceName);
		int offset = 0;

		short Next() {
			short value = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
			offset += 2;
			return value;
		}

		var models = new CollisionNode[typeCount][];

		for (int type = 0; type < typeCount; type++) {
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

			models[type] = nodes;
		}

		// The retail file is padded to an even length by the VOL prefix codec's own rule, so one
		// trailing byte is expected and anything more means the walk went wrong.
		if (offset < bytes.Length - 1) {
			throw new InvalidDataException(
				$"{ResourceFolder}\\{ResourceName}: walked {offset} of {bytes.Length} bytes across " +
				$"{typeCount} types — the record shape does not match this file.");
		}

		return new BaseCollisionTable(models);
	}

	/// <summary>
	/// <c>Collision_ComputeBoundingSphere</c> (<c>0040c5d0</c>) — the AABB of the children each
	/// inflated by its own radius, its midpoint as the centre, and the fast magnitude of the
	/// half-extents as the radius.
	///
	/// <para>The original seeds the box at ±10000 and does not special-case an empty cluster, which
	/// would leave it inverted; nothing in the retail file has one, and an empty bound here is
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
