using System.Text;
using HercWorks.Core.Data.File.Cfg;
using HercWorks.Core.Io.Read;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>Reads and writes <c>data\keyjoy.cfg</c> (<see cref="Keyjoy"/>).</summary>
public class KeyjoyTransformer : ByteTransformer<Keyjoy> {
	public override Keyjoy? Parse(byte[]? bytes) {
		if (bytes == null) {
			return null;
		}

		var values = IniSection.Read(IniSection.Lines(bytes), Keyjoy.Section);
		bool Reverse(string key) =>
			values.TryGetValue(key, out string? value)
			&& string.Equals(value, Keyjoy.ReverseWord, StringComparison.OrdinalIgnoreCase);

		return new Keyjoy {
			ReverseTilt = Reverse("Tilt"),
			ReverseBackturn = Reverse("Backturn"),
			ReverseMissile = Reverse("Missile"),
			ReverseRudder = Reverse("Rudder"),
		};
	}

	/// <summary>
	/// Writes the section with each switch as <c>Reverse</c> or <c>Default</c>. The comments a retail
	/// file carries are not kept.
	/// </summary>
	public override byte[]? Write(Keyjoy source) {
		static string Word(bool reverse) => reverse ? Keyjoy.ReverseWord : "Default";
		var text = new StringBuilder()
			.Append('[').Append(Keyjoy.Section).Append("]\r\n")
			.Append("Tilt=").Append(Word(source.ReverseTilt)).Append("\r\n")
			.Append("Backturn=").Append(Word(source.ReverseBackturn)).Append("\r\n")
			.Append("Missile=").Append(Word(source.ReverseMissile)).Append("\r\n")
			.Append("Rudder=").Append(Word(source.ReverseRudder)).Append("\r\n");
		return Encoding.ASCII.GetBytes(text.ToString());
	}
}
