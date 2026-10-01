using HercWorks.Core.Data.File.Cfg;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>Reads and writes <c>data\prefs.cfg</c> (<see cref="Prefs"/>).</summary>
public class PrefsTransformer : ByteTransformer<Prefs> {
	/// <summary>
	/// The file's bytes as the option array, or null when it is shorter than <see cref="Prefs.Length"/>.
	/// The simulator would take every byte it could not fill as option 0; a short file is rejected
	/// instead, so a caller with a sounder default of its own is told rather than handed every setting
	/// at its lowest value.
	/// </summary>
	public override Prefs? Parse(byte[]? bytes) =>
		bytes is { Length: >= Prefs.Length } ? new Prefs { Options = (byte[])bytes.Clone() } : null;

	public override byte[]? Write(Prefs source) => (byte[])source.Options.Clone();
}
