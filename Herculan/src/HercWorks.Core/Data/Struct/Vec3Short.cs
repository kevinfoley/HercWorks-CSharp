using HercWorks.Core.Util;
using System.Globalization;

namespace HercWorks.Core.Data.Struct;

/// <summary>
/// A triple of signed 16-bit values, as the data files store them — for storage rather than vector
/// arithmetic.
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
