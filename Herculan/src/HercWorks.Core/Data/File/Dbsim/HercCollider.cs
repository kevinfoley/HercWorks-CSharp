using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/COL/&lt;herc&gt;.COL — a unit's hit-sphere model, one file per HERC plus SKIMMER
/// (22 in a retail install). This is the geometry a shot is actually tested against: DBSIM never
/// tests a shot against a unit's polygons.
///
/// <para><b>Format</b> — every field is an <c>int16</c>, no header and no padding:</para>
/// <code>
/// nodeCount
///   per node: nodeIndex, clusterCount
///     per cluster: componentIndex, sphereCount, sphereCount * { x, y, z, radius }
/// </code>
///
/// <para>Structures use the identical record shape, 65 of them back to back in
/// <c>dat\BASECOL.DAT</c> — read through
/// <see cref="Io.Transform.Dbsim.HercColliderTransformer.ReadNodes"/>, which is the same walk with
/// an offset. Readers: <c>Collision_LoadRecordArray</c> (<c>0040ccf8</c>) →
/// <c>Collision_ReadNode</c> (<c>0040cc50</c>) → <c>Collision_ReadCluster</c> (<c>0040cc14</c>) →
/// <c>Collision_ReadSphereArray</c> (<c>0040c7c4</c>). Full RE in
/// <c>docs/simulation/hit-detection.md</c>.</para>
///
/// <para><b>They really are spheres.</b> <c>Collision_ReadSphereArray</c> allocates elements of 8
/// bytes, <c>Collision_ClusterSphereTest</c> (<c>0040c524</c>) strides four <c>int16</c> per element
/// and passes index 3 to <c>Collision_RaySphereTest</c> (<c>0040c428</c>) as a single scalar radius
/// applied radially about the ray, and <c>Collision_ComputeBoundingSphere</c> (<c>0040c5d0</c>)
/// inflates each child by that one radius on all three axes. Nothing anywhere compares per-axis
/// extents.</para>
///
/// <para><b>This class previously described a 10-byte header followed by undecoded data.</b> That
/// reading was wrong: those five shorts are the first five fields of the walk above, which is why
/// its own observations lined up the way they did — "always 6" is ACHILLES' <see cref="Nodes"/>
/// count (MONGOOSE has 8, PITBULL 10, SPIDER 13, RAZOR and SKIMMER 1), "always 3 for hercs, FFFF
/// for skimmer" is the first node's <see cref="ColliderNode.NodeIndex"/> (SPIDER's is 12, and
/// <c>FFFF</c> is the object frame), the "collider type" whose values above 1 crash is the first
/// node's cluster count reading past the end of the file, "hercs have 7" is the first cluster's
/// <see cref="ColliderCluster.ComponentIndex"/> (component 7 is <c>LEG/LEFT/UPPER</c>), and the
/// last is its sphere count. The walk lands exactly on the end of all 22 retail files.</para>
///
/// Ported from org.hercworks.core.data.file.dbsim.HercCollider, then corrected. The node/cluster/
/// sphere types are top-level (see <see cref="ColliderNode"/>) rather than nested, so that the
/// engine can consume the parsed model without taking a dependency on <see cref="DataFile"/>.
/// </summary>
public class HercCollider : DataFile {
	/// <summary>The model's nodes, in file order.</summary>
	public ColliderNode[]? Nodes { get; set; }
}
