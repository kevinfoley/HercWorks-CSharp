namespace HercWorks.Core.Data.Struct;

/// <summary>
/// Pure utility class to capture the underlying bytes of any 24-bit color value.
/// Ported from org.hercworks.core.data.struct.ColorBytes.
/// </summary>
public class ColorBytes {
	private RgbaColor _color;
	public byte[] Array { get; set; } = new byte[4];

	public ColorBytes(byte r, byte g, byte b, byte a) {
		Array[0] = r;
		Array[1] = g;
		Array[2] = b;
		Array[3] = a;
	}

	public ColorBytes(byte[] array) : this(array[0], array[1], array[2], array[3]) { }

	public ColorBytes() { }

	public void ReplaceVal(int index, byte b) {
		Array[index] = b;
	}

	public RgbaColor GetColor() => _color;

	public void SetColor(RgbaColor color) => _color = color;

	public int ColorIntRgb() {
		var tone = GetColor();
		return (tone.R << 16) | (tone.G << 8) | (tone.B << 0);
	}

	public int GetIntBgr() {
		return (Array[0] << 16) | (Array[1] << 8) | Array[2];
	}

	public int GetIntRgb() {
		return (Array[2] << 16) | (Array[1] << 8) | Array[0];
	}

	public override string ToString() {
		return "ColorBytes [hex=" + Convert.ToHexString(Array).ToLowerInvariant()
			 + ", rgb=(" + GetColor().R
			 + ", " + GetColor().G
			 + ", " + GetColor().B + ")"
			 + ", alpha=" + GetColor().A
			 + "]";
	}
}
