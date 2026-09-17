namespace HercWorks.Video.Avi;

/// <summary>
/// A movie's whole audio track, decoded to signed 16-bit samples.
///
/// <para>Every AVI in the retail corpus carries uncompressed PCM — format tag 1, either 8-bit
/// stereo at 11025 Hz or 16-bit mono at 22050 Hz. There is no ADPCM and nothing else to handle, so
/// an unsupported format tag returns null rather than being guessed at, matching how
/// <c>Herculan.Engine.Audio.WaveSample</c> treats the sound banks.</para>
///
/// <para>Tracks are decoded whole rather than streamed. The longest one in the corpus is under a
/// megabyte once widened, which is small enough that streaming would be complexity with nothing to
/// show for it.</para>
/// </summary>
public sealed class AviAudioTrack {
	private AviAudioTrack(short[] samples, int channels, int sampleRate) {
		Samples = samples;
		Channels = channels;
		SampleRate = sampleRate;
	}

	/// <summary>Signed 16-bit samples, channels interleaved.</summary>
	public short[] Samples { get; }

	/// <summary>How many channels <see cref="Samples"/> interleaves.</summary>
	public int Channels { get; }

	/// <summary>Frames per second, as the file declared it.</summary>
	public int SampleRate { get; }

	/// <summary>How many sample frames the track holds, counting all channels as one frame.</summary>
	public int FrameCount => Channels > 0 ? Samples.Length / Channels : 0;

	/// <summary>How long the track runs.</summary>
	public TimeSpan Duration =>
		TimeSpan.FromSeconds(SampleRate > 0 ? (double)FrameCount / SampleRate : 0);

	private const ushort PcmFormatTag = 1;

	/// <summary>
	/// Concatenates and decodes every audio packet in <paramref name="file"/>, or returns null when
	/// there is no audio stream or it is not uncompressed PCM.
	/// </summary>
	public static AviAudioTrack? Decode(AviFile file) {
		ArgumentNullException.ThrowIfNull(file);

		AviAudioFormat? format = file.AudioFormat;
		if (format is null || format.FormatTag != PcmFormatTag) {
			return null;
		}

		if (format.BitsPerSample is not (8 or 16)) {
			return null;
		}

		int bytesPerSample = format.BitsPerSample / 8;

		// Size the output from the packets that are actually present rather than from any declared
		// length, so a truncated file yields a short track instead of a buffer full of silence.
		long totalBytes = 0;
		foreach (AviPacket packet in file.AudioPackets) {
			totalBytes += packet.Length;
		}

		long totalSamples = totalBytes / bytesPerSample;
		if (totalSamples <= 0 || totalSamples > int.MaxValue) {
			return null;
		}

		var samples = new short[totalSamples];
		int at = 0;

		foreach (AviPacket packet in file.AudioPackets) {
			ReadOnlySpan<byte> data = file.PacketData(packet);

			if (bytesPerSample == 1) {
				// 8-bit WAVE data is unsigned with 0x80 as silence; widen about that midpoint.
				for (int i = 0; i < data.Length && at < samples.Length; i++) {
					samples[at++] = (short)((data[i] - 0x80) << 8);
				}
			} else {
				for (int i = 0; i + 1 < data.Length && at < samples.Length; i += 2) {
					samples[at++] = (short)(data[i] | (data[i + 1] << 8));
				}
			}
		}

		// A packet whose length was not a whole number of samples leaves the tail unwritten; trim
		// rather than pad, so Duration stays honest.
		if (at != samples.Length) {
			Array.Resize(ref samples, at);
		}

		return new AviAudioTrack(samples, format.Channels, format.SampleRate);
	}

	/// <summary>
	/// Returns the track mixed down to one channel, or the samples unchanged when it already is.
	///
	/// <para>The engine's audio backend takes mono samples, so a stereo movie track has to fold
	/// before it can be played. Channels are averaged in <c>int</c> and not clamped, because the
	/// average of two values already inside the 16-bit range cannot leave it.</para>
	/// </summary>
	public short[] ToMono() {
		if (Channels <= 1) {
			return Samples;
		}

		int frames = FrameCount;
		var mono = new short[frames];

		for (int frame = 0; frame < frames; frame++) {
			int sum = 0;
			int baseAt = frame * Channels;
			for (int channel = 0; channel < Channels; channel++) {
				sum += Samples[baseAt + channel];
			}

			mono[frame] = (short)(sum / Channels);
		}

		return mono;
	}
}
