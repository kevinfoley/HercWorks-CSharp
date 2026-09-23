namespace Herculan.Engine.Audio;

/// <summary>
/// Which <see cref="ICdAudio"/> the machine this is running on gets. <b>The one place the engine
/// decides that</b>, so a second implementation is a change here and nowhere else — per
/// docs/engine/planning.md's "Target platform", which asks for OS-specific paths to sit behind a
/// seam from the start even while Windows is the only tested target.
///
/// <para>There is one real implementation so far, <see cref="MciCdAudio"/>, and it is Windows-only
/// because MCI is. Everything else gets <see cref="NullCdAudio"/> and runs the whole music layer
/// above it — the track choice, the enable flag, the saved position — silently.</para>
/// </summary>
public static class CdAudio {
	/// <summary>
	/// Opens the machine's CD player, or answers a <see cref="NullCdAudio"/> carrying the reason
	/// there is none. Never throws.
	/// </summary>
	/// <param name="drive">
	/// Which drive to play from, as a letter. Null takes the platform's own default, which on Windows
	/// is whichever CD drive MCI answers with — all retail ever asks for. See
	/// <see cref="MciCdAudio.TryCreate"/>.
	/// </param>
	public static ICdAudio Open(string? drive = null) =>
		OperatingSystem.IsWindows()
			? MciCdAudio.TryCreate(drive)
			: new NullCdAudio("CD music needs MCI, which is Windows-only");
}
