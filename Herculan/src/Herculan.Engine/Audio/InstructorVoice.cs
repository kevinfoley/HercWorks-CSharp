namespace Herculan.Engine.Audio;

/// <summary>
/// Where a training mission's instructor clips are — the one voice the original reads as loose files
/// rather than out of an archive. See docs/formats/audio.md, "File naming".
///
/// <para>The training port's paint (<c>PilotMessagePort_Paint</c>, <c>0043660c</c>) patches the
/// training number and <b>the posted id plus one</b> into <c>TMx_0000</c>, puts the voice folder
/// in front — <c>simvoice</c> with its last letter patched for the language, as
/// <c>Voice_ArchiveName</c> (<c>0045ef68</c>) does for the archive — and, because the digit is never
/// <c>0</c> on this port, prefixes the directory <c>data\drive.cfg</c> names (<c>FUN_0045ee44</c>,
/// the path <c>FUN_0045f144</c> reads at startup). One clip per instruction, not per sentence.</para>
/// </summary>
public static class InstructorVoice {
	/// <summary>The file <c>FUN_0045f144</c> reads the directory out of, in the install's data folder.</summary>
	public const string DriveConfigName = "drive.cfg";

	/// <summary>The clip for instruction <paramref name="messageId"/> of training mission <paramref name="trainingMission"/>.</summary>
	public static string ClipName(int trainingMission, int messageId) =>
		$"TM{trainingMission}_{messageId + 1:0000}.WAV";

	/// <summary>
	/// The folder the clips are in: <paramref name="voiceFolder"/> under the first token of
	/// <c>drive.cfg</c>, or under the install root (the data folder's parent) when that file is
	/// missing or empty.
	/// </summary>
	/// <param name="dataDirectory">The install's data folder — where <c>script.dat</c> came from.</param>
	/// <param name="voiceFolder">
	/// <c>SIMVOICE</c>, <c>SIMVOICF</c> or <c>SIMVOICG</c> — the mounted voice archive's own stem.
	/// </param>
	public static string? Directory(string? dataDirectory, string voiceFolder) {
		if (string.IsNullOrEmpty(dataDirectory)) {
			return null;
		}

		string? root = null;
		string config = Path.Combine(dataDirectory, DriveConfigName);
		if (File.Exists(config)) {
			root = File.ReadAllText(config)
				.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
				.FirstOrDefault();
		}

		root ??= Path.GetDirectoryName(Path.GetFullPath(dataDirectory));
		return root == null ? null : Path.Combine(root, voiceFolder);
	}
}
