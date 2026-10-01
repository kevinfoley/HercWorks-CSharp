namespace HercWorks.Core.Data.File.Wav;

/// <summary>
/// Header-only metadata for a RIFF/WAVE file — channel count, sample rate, bit depth and duration —
/// for the VOL browser's Content panel. See <see cref="Io.Transform.Common.WavInfoTransformer"/> for
/// how it's read; nothing here decodes the sample data itself.
/// </summary>
public sealed class WavInfo {
	/// <summary>The fmt chunk's format tag as a name — "PCM" for tag 1, otherwise "Unknown (tag N)".</summary>
	public string Format { get; init; } = "";

	public int Channels { get; init; }

	public int SampleRate { get; init; }

	public int BitsPerSample { get; init; }

	public TimeSpan Duration { get; init; }
}
