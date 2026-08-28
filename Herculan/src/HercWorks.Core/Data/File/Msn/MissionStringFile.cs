using System.Text;

namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// FILE - (Mission).ENG, .GER, .FRE — a variant of the observed 'String' files, these provide
/// the string data for missions.
/// Ported from org.hercworks.core.data.file.msn.MissionStringFile.
/// </summary>
public class MissionStringFile {
	public int TotalSize { get; set; }
	public StringEntry[]? Strings { get; set; }

	public StringEntry CreateEntry(short guid, short rVal, short rFlag, short len, string val) {
		return new StringEntry {
			Guid = guid,
			ResultVal = rVal,
			ResultFlag = rFlag,
			Len = len,
			Val = val
		};
	}

	public class StringEntry {
		public short Guid { get; set; }
		public short ResultVal { get; set; }
		public short ResultFlag { get; set; }
		public short Len { get; set; }
		public string? Val { get; set; }

		public override string ToString() {
			var str = new StringBuilder();

			str.Append("{")
				.Append(" guid = ").Append(Guid)
				.Append(", r val = ").Append(ResultVal)
				.Append(", r flag = ").Append(ResultFlag)
				.Append(", len = ").Append(Len)
				.Append(", val = ").Append(Val)
				.Append("}");

			return str.ToString();
		}
	}
}
