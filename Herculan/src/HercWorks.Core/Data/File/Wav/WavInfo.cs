namespace HercWorks.Core.Data.File.Wav;

/// <summary>
/// Header-only metadata for a RIFF/WAVE file — channel count, sample rate, bit depth and duration —
/// for the VOL browser's Content panel. See <see cref="Io.Transform.Common.WavInfoTransformer"/> for
/// how it's read; nothing here decodes the sample data itself.
/// </summary>
public sealed class WavInfo {
	/// <summary>The fmt chunk's format tag, decoded to a name where recognized — "PCM" (tag 1) is
	/// the only one SIMSOUND.VOL's own samples ever use (see
	/// Herculan.Engine.Audio.WaveSample's doc comment); anything else shows as "Unknown (tag N)".</summary>
	public string Format { get; init; } = "";

	public int Channels { get; init; }

	public int SampleRate { get; init; }

	public int BitsPerSample { get; init; }

	public TimeSpan Duration { get; init; }
}
