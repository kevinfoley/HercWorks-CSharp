namespace HercWorks.Core.Data.File.Msn;

/// <summary>Ported from org.hercworks.core.data.file.msn.UnkEntity22Byte.</summary>
public class UnkEntity22Byte : MapObject {
	public short[] Flags { get; set; } = new short[10];

	/// <summary>No getter/setter in the original (unused field); exposed as a property here.</summary>
	public UnkEntity164Bytes? UnkEntity164 { get; set; }

	public override string ToString() {
		var sb = new System.Text.StringBuilder();

		sb.Append("\n{\n");
		sb.Append("\tguid = ").Append(GUID).Append('\n');
		sb.Append("\tflags = [").Append(string.Join(", ", Flags)).Append("]\n");
		sb.Append("}\n");

		return sb.ToString();
	}
}
