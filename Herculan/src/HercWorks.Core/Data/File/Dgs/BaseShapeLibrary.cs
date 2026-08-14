using HercWorks.Core.Data.File.Dts;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dgs;

/// <summary>
/// One record of a <c>.DGS</c> shape library: <c>dat\BASES.DAT</c>'s <c>ShapeIndex</c> field
/// indexes into this list. <see cref="Geometry"/> is the embedded DTS subtree the record wraps —
/// see <see cref="Io.Transform.Dbsim.BasesDgsTransformer"/> for how the rest of the record's
/// bytes (a name/id triple, a per-vertex table, and a large opaque block believed to back
/// collision/BSP queries) are walked but not modelled, since the engine only draws.
/// </summary>
/// <param name="Id">
/// The record's own id field (third of three shorts at the head of every record) — not
/// cross-checked against anything else yet, kept for diagnostics.
/// </param>
/// <param name="Geometry">
/// The shape's drawable geometry, or null for the rare record with no child object.
/// </param>
public readonly record struct BaseShape(short Id, TSObject? Geometry);

/// <summary>
/// <c>dgs\BASES.DGS</c> / <c>dgs\BHULKS.DGS</c> — the static-structure shape library <c>dat\BASES.DAT</c>
/// selects into by index. See <see cref="Io.Transform.Dbsim.BasesDgsTransformer"/> for the format.
/// </summary>
public class BaseShapeLibrary : DataFile {
	public BaseShape[]? Shapes { get; set; }
}
