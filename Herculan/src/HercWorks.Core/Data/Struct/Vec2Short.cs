using HercWorks.Core.Util;

namespace HercWorks.Core.Data.Struct;

/// <summary>A pair of signed 16-bit values, as the data files store them.</summary>
public class Vec2Short {
	public short X { get; set; }
	public short Y { get; set; }

	public Vec2Short() { }

	public Vec2Short(short x, short y) {
		X = x;
		Y = y;
	}

	public Vec2Short(byte[] values, ByteOrder order) {
		X = EndianOps.ToShort(values, 0, order);
		Y = EndianOps.ToShort(values, 2, order);
	}

	public double[] ToDouble() {
		return new double[] { X, Y };
	}

	public override string ToString() {
		return $"[{X},{Y}]";
	}
}
