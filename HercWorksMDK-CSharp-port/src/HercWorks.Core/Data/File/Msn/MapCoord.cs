using System.Text;

namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// As observed in 'script.var', there's a chunk of data for map coordinates, and the
/// pre-processed MSN file has option flags and IDs before each coordinate listed.
/// Size: 22 bytes. XXX (carried over from Java): map coords are INT32 but represent fixed-point
/// integers!
/// Ported from org.hercworks.core.data.file.msn.MapCoord.
/// </summary>
public class MapCoord {
	public short Id { get; set; }
	public short UnkFlag1 { get; set; }
	public short UnkFlag2 { get; set; }
	public short UnkFlag3 { get; set; }

	/// <summary>Or possible spacer.</summary>
	public short UnkFlag4 { get; set; }

	public int X { get; set; }
	public int Y { get; set; }
	public int Z { get; set; }

	public MapCoord() { }

	public MapCoord(short id, short f1, short f2, short f3, short f4, int vx, int vy, int vz) {
		Id = id;
		UnkFlag1 = f1;
		UnkFlag2 = f2;
		UnkFlag3 = f3;
		UnkFlag4 = f4;
		X = vx;
		Y = vy;
		Z = vz;
	}

	/// <summary>
	/// Original does integer division (f/1000) then widens to double, so the fractional part is
	/// truncated before conversion — likely meant f/1000.0 but ported literally (bug-compatible).
	/// </summary>
	public static double FixedInt(int f) => f / 1000;

	public override string ToString() {
		var b = new StringBuilder();
		b.Append(Id).Append(" = { ").Append(UnkFlag1).Append(", ")
			.Append(UnkFlag2).Append(", ").Append(UnkFlag3).Append(", ")
			.Append(UnkFlag4).Append(", (")
			.Append(FixedInt(X)).Append(", ").Append(FixedInt(Y)).Append(", ").Append(FixedInt(Z))
			.Append(")}");

		return b.ToString();
	}
}
