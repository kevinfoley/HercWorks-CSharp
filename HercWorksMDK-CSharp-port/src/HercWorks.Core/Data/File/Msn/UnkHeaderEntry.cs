using System.Text;

namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// 14 bytes per entry, observed at the top of the MSN file — there's a counter for how many,
/// and each has an iterator id.
/// Ported from org.hercworks.core.data.file.msn.UnkHeaderEntry.
/// </summary>
public class UnkHeaderEntry {
	public short IndexId { get; set; }
	public short StartFrameIndexId { get; set; }
	public short UnkValue1 { get; set; }
	public short FrameStartTime { get; set; }
	public short FrameEndTime { get; set; }
	public short TotalTime { get; set; }
	public short UnkValue2 { get; set; }

	public UnkHeaderEntry() { }

	public UnkHeaderEntry(short indexId, short startFrameIndexId, short v1, short frameStartTime,
		short frameEndTime, short totalTime, short unkValue2) {
		IndexId = indexId;
		StartFrameIndexId = startFrameIndexId;
		UnkValue1 = v1;
		FrameStartTime = frameStartTime;
		FrameEndTime = frameEndTime;
		TotalTime = totalTime;
		UnkValue2 = unkValue2;
	}

	public override string ToString() {
		var b = new StringBuilder();
		b.Append("[")
			.Append(IndexId).Append(", ")
			.Append(StartFrameIndexId).Append(", ")
			.Append(UnkValue1).Append(", ")
			.Append(FrameStartTime).Append(", ")
			.Append(FrameEndTime).Append(", ")
			.Append(TotalTime).Append(", ")
			.Append(UnkValue2)
			.Append("]");

		return b.ToString();
	}
}
