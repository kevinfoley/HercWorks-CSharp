using HercWorks.Vol;

namespace HercWorks.Core.Io.Write;

/// <summary>
/// Ported from org.hercworks.core.io.write.VolFileWriter — this class duplicated the exact same
/// "strict pack" / "unpack" logic already present in org.hercworks.voln.io.VolFileWriter (likely
/// a leftover from before ES2Vol was split into its own module; same situation found earlier
/// with org.hercworks.core.io.read.VolFileReader duplicating org.hercworks.voln.io.VolFileReader).
/// Rather than re-implement the same byte-for-byte logic a second time, this delegates to the
/// already-ported, already-verified <see cref="HercWorks.Vol.Io.VolFileWriter"/>.
/// </summary>
public static class VolFileWriter {
	/// <summary>
	/// 'Strict' here means DO NOT calculate new dynamic sizes — write the VOL directly to a file
	/// with all data already assembled and counted. Best case is modifying an existing VOL and
	/// doing simple byte-edit tasks.
	/// </summary>
	public static void PackVolToFileStrict(Voln vol, string destPath) =>
		HercWorks.Vol.Io.VolFileWriter.PackVolToFileStrict(vol, destPath);

	public static void UnpackVol(Voln vol, string destPath) =>
		HercWorks.Vol.Io.VolFileWriter.UnpackVol(vol, destPath);
}
