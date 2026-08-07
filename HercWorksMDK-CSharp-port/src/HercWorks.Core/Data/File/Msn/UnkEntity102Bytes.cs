namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Observed following unit info segments; in TRAIN5.MSN there are only 2 of them.
/// Ported from org.hercworks.core.data.file.msn.UnkEntity102Bytes.
/// </summary>
public class UnkEntity102Bytes : MapObject {
	public short[] Flags { get; set; } = new short[49];
	public short UnkVal_100 { get; set; }

	public override string ToString() {
		var str = new System.Text.StringBuilder();

		str.Append("{\n");
		str.Append("\tguid = ").Append(GUID).Append('\n');
		str.Append("\tflags = \n");
		str.Append("\t\t[").Append(string.Join(", ", Flags)).Append("]\n");
		str.Append("\tunk 100 = ").Append(UnkVal_100).Append('\n');
		str.Append("}\n");

		return str.ToString();
	}
}
