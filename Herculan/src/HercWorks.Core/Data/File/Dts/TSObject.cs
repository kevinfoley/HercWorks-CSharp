using System.Text;

namespace HercWorks.Core.Data.File.Dts;

/// <summary>
/// Base of every ThreeSpace chunk. A <c>.DTS</c> is a tree of chunks, each
/// <c>&lt;4-byte type marker&gt;&lt;int32 payload length&gt;&lt;payload&gt;</c>.
/// </summary>
public abstract class TSObject {
	/// <summary>The chunk's type marker.</summary>
	public TSObjectHeader? Header { get; }

	/// <summary>The chunk's declared payload length, excluding the 8-byte marker and length.</summary>
	public int ByteLen { get; set; }

	/// <summary>Offset of the chunk's marker in the buffer it was read from.</summary>
	public int Index { get; set; }

	/// <summary>The chunk this one was read inside, or null for a root.</summary>
	public TSObject? Parent { get; set; }

	/// <summary>The chunk's payload bytes as read.</summary>
	public byte[]? Data { get; set; }

	/// <summary>
	/// Not from the file: the reader numbers every <see cref="TSGroup"/> it reads, in file order, and
	/// stores the number here. 0 on every other chunk type.
	/// </summary>
	public int ListIndex { get; set; }

	protected TSObject() { }

	protected TSObject(TSObjectHeader hdr) {
		Header = hdr;
	}

	public int GetDataIndex() => Index + 8;

	public string MetaInfoString(string chunkName) {
		var str = new StringBuilder();

		str.Append("{ \n\"class\" : \"").Append(chunkName).Append("\",\n");
		str.Append("\"index\" : ").Append(Index).Append(",\n");
		str.Append("\"len\" : ").Append(ByteLen).Append(",\n");

		return str.ToString();
	}

	public abstract StringBuilder JsonString(StringBuilder str);
}
