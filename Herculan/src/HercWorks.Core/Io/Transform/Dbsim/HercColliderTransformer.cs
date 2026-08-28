using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .COL hit-sphere models — see <see cref="HercCollider"/> for
/// the format and the RE behind it.
///
/// <para>Both directions are byte-exact on all 22 retail files. An earlier pass read only a
/// (misidentified) 10-byte header and kept the rest as raw shorts, and could not write at all;
/// the file has no header, and every short of it is now accounted for.</para>
///
/// <para>The engine has its own reader for the same format,
/// <c>Herculan.Engine.World.CollisionModelReader</c>, which additionally builds the per-cluster
/// bounding sphere DBSIM computes at load time. This one is the tool-side model and stays a plain
/// mirror of the bytes.</para>
/// </summary>
public class HercColliderTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length < 2) {
			return null;
		}

		SetBytes(inputArray);

		var col = new HercCollider {
			RawBytes = inputArray,
			Ext = FileType.Col,
			Dir = FileType.Col,
		};

		col.Nodes = new HercCollider.ColliderNode[NonNegative(IndexShortLE())];
		for (int n = 0; n < col.Nodes.Length; n++) {
			var node = new HercCollider.ColliderNode { NodeIndex = IndexShortLE() };

			node.Clusters = new HercCollider.ColliderCluster[NonNegative(IndexShortLE())];
			for (int c = 0; c < node.Clusters.Length; c++) {
				var cluster = new HercCollider.ColliderCluster { ComponentIndex = IndexShortLE() };

				// The original tests the count as `value & 0x1fff` but allocates and reads it
				// unmasked, so the mask is only a zero-test. Reproduced as written.
				short sphereCount = IndexShortLE();
				cluster.Spheres = new HercCollider.ColliderSphere[
					(sphereCount & SphereCountMask) != 0 ? NonNegative(sphereCount) : 0];

				for (int s = 0; s < cluster.Spheres.Length; s++) {
					cluster.Spheres[s] = new HercCollider.ColliderSphere {
						X = IndexShortLE(),
						Y = IndexShortLE(),
						Z = IndexShortLE(),
						Radius = IndexShortLE(),
					};
				}

				node.Clusters[c] = cluster;
			}

			col.Nodes[n] = node;
		}

		return col;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source is not HercCollider col) {
			return null;
		}

		using var outStream = new MemoryStream();
		var nodes = col.Nodes ?? Array.Empty<HercCollider.ColliderNode>();

		Write(outStream, WriteShortLE((short)nodes.Length));
		foreach (var node in nodes) {
			var clusters = node.Clusters ?? Array.Empty<HercCollider.ColliderCluster>();

			Write(outStream, WriteShortLE(node.NodeIndex));
			Write(outStream, WriteShortLE((short)clusters.Length));

			foreach (var cluster in clusters) {
				var spheres = cluster.Spheres ?? Array.Empty<HercCollider.ColliderSphere>();

				Write(outStream, WriteShortLE(cluster.ComponentIndex));
				Write(outStream, WriteShortLE((short)spheres.Length));

				foreach (var sphere in spheres) {
					Write(outStream, WriteShortLE(sphere.X));
					Write(outStream, WriteShortLE(sphere.Y));
					Write(outStream, WriteShortLE(sphere.Z));
					Write(outStream, WriteShortLE(sphere.Radius));
				}
			}
		}

		return outStream.ToArray();
	}

	/// <summary>See the read path — the mask is a zero-test, not a width.</summary>
	private const int SphereCountMask = 0x1fff;

	/// <summary>
	/// A negative count means a corrupt or hand-edited file. The original would allocate a negative
	/// size and fail; here it reads as empty so the rest of the walk still reports something.
	/// </summary>
	private static int NonNegative(short count) => count < 0 ? 0 : count;

	private static void Write(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
