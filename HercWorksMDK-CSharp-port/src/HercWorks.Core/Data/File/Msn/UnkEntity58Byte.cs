namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Observed at end of TRAIN5.MSN, may or may not exist!
/// Ported from org.hercworks.core.data.file.msn.UnkEntity58Byte.
/// </summary>
public class UnkEntity58Byte : MapObject {
	public short[] Flags { get; set; } = new short[28];

	public override string ToString() {
		var sb = new System.Text.StringBuilder();

		sb.Append("\n{\n");
		sb.Append(" guid = ").Append(GUID).Append('\n');
		sb.Append(" flags = [").Append(string.Join(", ", Flags)).Append("]\n");
		sb.Append("}\n");

		return sb.ToString();
	}
}
