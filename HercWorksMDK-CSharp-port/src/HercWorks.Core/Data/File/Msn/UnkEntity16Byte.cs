namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Observed to follow on after the UnkEntity10Byte array, 2nd after MapCoords.
/// Ported from org.hercworks.core.data.file.msn.UnkEntity16Byte.
/// </summary>
public class UnkEntity16Byte : MapObject {
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
