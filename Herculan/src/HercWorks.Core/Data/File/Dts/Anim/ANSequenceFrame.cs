using System.Text;

namespace HercWorks.Core.Data.File.Dts.Anim;

/// <summary>One frame of an <see cref="ANSequence"/>.</summary>
public class ANSequenceFrame {
	/// <summary>Offset of the record in the buffer it was read from.</summary>
	public int Index { get; set; }

	/// <summary>The record's bytes as read.</summary>
	public byte[]? Data { get; set; }

	public int ByteLen { get; set; }

	/// <summary>The frame's length in animation ticks.</summary>
	public short Duration { get; set; }

	/// <summary>How many transitions the frame offers.</summary>
	public short NumTransitions { get; set; }

	/// <summary>Index of the frame's first transition in <see cref="ANAnimList.Transitions"/>.</summary>
	public short FirstTransition { get; set; }

	public override string ToString() {
		var str = new StringBuilder();

		str.Append("{ \"class\" : \"").Append(GetType().Name).Append("\",\n");
		str.Append("\"index\" : ").Append(Index).Append(",\n");
		str.Append("\"len\" : ").Append(ByteLen).Append(",\n");
		str.Append("\"data\" : ").Append(Data == null ? "null" : "[" + string.Join(", ", Data) + "]").Append(",\n");
		str.Append("\"duration\" :").Append(Duration).Append(",\n");
		str.Append("\"numTransitions\" :").Append(NumTransitions).Append(",\n");
		str.Append("\"firstTransition\" :").Append(FirstTransition).Append("\n");
		str.Append("}\n");

		return str.ToString();
	}
}
