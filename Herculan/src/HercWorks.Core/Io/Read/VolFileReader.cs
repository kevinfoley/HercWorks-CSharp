using HercWorks.Vol;

namespace HercWorks.Core.Io.Read;

/// <summary>
/// Ported from org.hercworks.core.io.read.VolFileReader — like io.write.VolFileWriter, this
/// duplicated the same parsing logic already present in org.hercworks.voln.io.VolFileReader
/// (likely a leftover from before ES2Vol was split into its own module). The Java original also
/// carried several debug-only/deprecated methods (debugSortPrefix, debugUnsortedPrefix,
/// debugFileByteJoins, scanVoidBytes) that were unused or fully commented out — not ported, since
/// they added no functional behavior.
/// Delegates to the already-ported, already-verified <see cref="HercWorks.Vol.Io.VolFileReader"/>.
/// </summary>
public static class VolFileReader {
	public static Voln ParseVolFile(string volPath) =>
		HercWorks.Vol.Io.VolFileReader.ParseVolFile(volPath);
}
