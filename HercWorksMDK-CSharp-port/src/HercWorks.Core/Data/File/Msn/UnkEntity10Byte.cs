namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// In TRAIN5.MSN these seem to follow map coords.
/// Ported from org.hercworks.core.data.file.msn.UnkEntity10Byte.
/// </summary>
public class UnkEntity10Byte : MapObject {
	public short[]? Values { get; set; }

	public override string ToString() {
		var sb = new System.Text.StringBuilder();

		sb.Append("\n{\n");
		sb.Append("\tguid = ").Append(GUID).Append('\n');
		sb.Append("\tvals = ").Append(Values == null ? "null" : "[" + string.Join(", ", Values) + "]").Append('\n');
		sb.Append("}\n");

		return sb.ToString();
	}
}
