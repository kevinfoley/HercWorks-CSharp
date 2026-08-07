using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.Struct;
using HercWorks.Vol;
using System.Drawing;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Transforms byte[] data to and from DynamixPalette game files.
/// Ported from org.hercworks.core.io.transform.common.DynamixPaletteTransformer.
///
/// NOTE ON toByte()/byteOrder(): unlike .array() (see the ByteOps notes from earlier rounds),
/// .toByte() DOES respect the byteOrder tag — for LITTLE_ENDIAN it returns the value's
/// least-significant byte, i.e. equivalent to a plain narrowing (byte) cast here since every
/// value written is already guaranteed &lt; 256. Ported as a direct cast below.
/// </summary>
public class DynamixPaletteTransformer : ThreeSpaceByteTransformer {
	private readonly int _colorScalar = 4;

	public DynamixPaletteTransformer() { }

	public DynamixPaletteTransformer(int scalar) {
		_colorScalar = scalar;
	}

	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO (carried over from Java): log null
			return null;
		}
		SetBytes(inputArray);
		ResetIndex();

		var dpl = new DynamixPalette {
			RawBytes = (byte[])inputArray.Clone(),
			Ext = FileType.Dpl,
			Dir = FileType.Dpl
		};

		Index += 4; // skip magic header bytes

		dpl.PaletteSizeByte = IndexIntLE();
		dpl.ColorCount = IndexIntLE();

		int colorIdx = 0;

		dpl.Scalar = _colorScalar;

		// DO NOT CHANGE - original "mostly working" 4-byte RGBA value read
		for (int clr = 0; clr < dpl.ColorCount; clr++) {
			dpl.Colors[colorIdx] = ToColorBytes(IndexSegmentLE(4), dpl.Scalar);
			colorIdx++;
		}

		return dpl;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			return null;
		}

		var dpl = (DynamixPalette)source;
		using var objectBytes = new MemoryStream();

		objectBytes.Write(DynamixPalette.Header, 0, DynamixPalette.Header.Length);

		var sizeBytes = WriteInt(dpl.PaletteSizeByte);
		objectBytes.Write(sizeBytes, 0, sizeBytes.Length);

		var countBytes = WriteInt(dpl.ColorCount);
		objectBytes.Write(countBytes, 0, countBytes.Length);

		foreach (var color in dpl.Colors.Values) {
			var c = ToDynamixColor(color, dpl.Scalar);
			objectBytes.Write(c, 0, c.Length);
		}

		return objectBytes.ToArray();
	}

	/// <summary>Convert a 4-byte segment of a .DPL file into the ColorBytes wrapper.</summary>
	private static ColorBytes ToColorBytes(byte[] dynamixColor, int scalar) {
		var bytes = new byte[4];

		int ir = dynamixColor[0] & 0xFF;
		int ig = dynamixColor[1] & 0xFF;
		int ib = dynamixColor[2] & 0xFF;
		int ia = dynamixColor[3];

		ir = ir * scalar > 255 ? 255 : ir * scalar;
		ig = ig * scalar > 255 ? 255 : ig * scalar;
		ib = ib * scalar > 255 ? 255 : ib * scalar;
		ia = ia == 1 ? 255 : 0;

		bytes[0] = (byte)ir;
		bytes[1] = (byte)ig;
		bytes[2] = (byte)ib;
		bytes[3] = (byte)ia;

		var rawColor = new ColorBytes(bytes);
		rawColor.SetColor(Color.FromArgb(ia, ir, ig, ib));

		return rawColor;
	}

	/// <summary>
	/// Converts a ColorBytes object to a 4-byte array based on the parent DynamixPalette's
	/// scalar value.
	///
	/// NOTE: despite ToColorBytes() above reading byte0=R, byte1=G, byte2=B, this write path
	/// outputs byte0=R, byte1=B, byte2=G — the G/B channels are swapped versus the read path.
	/// That's how the Java original is written; looks like a genuine round-trip bug (write then
	/// read would swap green/blue), but ported literally rather than silently fixed.
	/// </summary>
	private static byte[] ToDynamixColor(ColorBytes color, int scalar) {
		var data = new byte[4];
		var c = color.GetColor();

		data[0] = (byte)(c.R / scalar);
		data[1] = (byte)(c.B / scalar);
		data[2] = (byte)(c.G / scalar);

		if (color.Array[3] != 0) {
			data[3] = 0x01;
		}

		return data;
	}
}
