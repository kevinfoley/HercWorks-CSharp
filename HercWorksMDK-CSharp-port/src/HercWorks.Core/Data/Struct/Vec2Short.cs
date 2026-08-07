using HercWorks.Core.Util;

namespace HercWorks.Core.Data.Struct;

/// <summary>Ported from org.hercworks.core.data.struct.Vec2Short.</summary>
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
