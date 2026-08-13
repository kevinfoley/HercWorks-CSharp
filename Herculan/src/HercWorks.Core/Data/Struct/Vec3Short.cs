using HercWorks.Core.Util;
using System.Globalization;

namespace HercWorks.Core.Data.Struct;

/// <summary>
/// Some data structures reference a Vec3, but most common implementations assume double (float)
/// or int, whereas ES2 uses signed shorts. Mostly for data storage, less so actual vector operations.
/// Ported from org.hercworks.core.data.struct.Vec3Short.
/// </summary>
public class Vec3Short {
	public short X { get; set; }
	public short Y { get; set; }
	public short Z { get; set; }

	public Vec3Short() { }

	public Vec3Short(short x, short y, short z) {
		X = x;
		Y = y;
		Z = z;
	}

	public Vec3Short(byte[] values, ByteOrder order) {
		X = EndianOps.ToShort(values, 0, order);
		Y = EndianOps.ToShort(values, 2, order);
		Z = EndianOps.ToShort(values, 4, order);
	}

	public double[] ToDouble() {
		return new double[] { X, Y, Z };
	}

	private static string FormatFixedPoint(short p) {
		double d = p / 10.0;
		return d.ToString(CultureInfo.InvariantCulture);
	}

	public override string ToString() {
		return $"[{FormatFixedPoint(X)}, {FormatFixedPoint(Y)}, {FormatFixedPoint(Z)}]";
	}
}
