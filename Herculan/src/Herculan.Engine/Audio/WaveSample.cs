namespace Herculan.Engine.Audio;

/// <summary>
/// One decoded RIFF/WAVE sample, normalised to signed 16-bit mono.
///
/// <para>The game's own sample banks need nothing more than this: every entry in
/// <c>SIMSOUND.VOL</c>, <c>SHLSOUND.VOL</c> and the three voice archives is uncompressed PCM
/// (format tag 1), single-channel, at 11025 or 22050 Hz, in 8-bit unsigned or 16-bit signed. There
/// is no ADPCM and no stereo anywhere in the shipped data, so nothing here tries to handle
/// either — an unsupported chunk layout returns null rather than being guessed at.</para>
///
/// <para>8-bit input is widened to 16-bit at load rather than at mix time: the backends want one
/// format, and the whole shipped bank is under two megabytes even doubled.</para>
/// </summary>
public sealed class WaveSample {
	private WaveSample(short[] samples, int sampleRate) {
		Samples = samples;
		SampleRate = sampleRate;
	}

	/// <summary>Signed 16-bit mono PCM.</summary>
	public short[] Samples { get; }

	/// <summary>Frames per second, as the file declared it.</summary>
	public int SampleRate { get; }

	/// <summary>How long the sample runs.</summary>
	public TimeSpan Duration =>
		TimeSpan.FromSeconds(SampleRate > 0 ? (double)Samples.Length / SampleRate : 0);

	/// <summary>
	/// Decodes a RIFF/WAVE file, or returns null when it is not one, is not uncompressed mono PCM,
	/// or is truncated. Chunks are walked rather than assumed to sit at fixed offsets, because a
	/// couple of retail voice files carry trailing data past the end of their RIFF chunk.
	/// </summary>
	public static WaveSample? Decode(byte[] bytes) {
		if (bytes.Length < 12
			|| bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F'
			|| bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E') {
			return null;
		}

		int channels = 0, sampleRate = 0, bitsPerSample = 0;
		int dataAt = -1, dataLength = 0;

		int at = 12;
		while (at + 8 <= bytes.Length) {
			uint id = BitConverter.ToUInt32(bytes, at);
			int length = BitConverter.ToInt32(bytes, at + 4);
			if (length < 0 || at + 8 + length > bytes.Length) {
				// A declared length running past the file is a truncated chunk; stop rather than
				// read whatever follows. What has been gathered so far may still be enough.
				break;
			}

			int body = at + 8;
			if (id == FourCc('f', 'm', 't', ' ') && length >= 16) {
				if (BitConverter.ToUInt16(bytes, body) != PcmFormatTag) {
					return null;
				}

				channels = BitConverter.ToUInt16(bytes, body + 2);
				sampleRate = BitConverter.ToInt32(bytes, body + 4);
				bitsPerSample = BitConverter.ToUInt16(bytes, body + 14);
			} else if (id == FourCc('d', 'a', 't', 'a')) {
				dataAt = body;
				dataLength = length;
			}

			// Chunk bodies are word-aligned; an odd length is followed by a pad byte.
			at = body + length + (length & 1);
		}

		if (channels != 1 || sampleRate <= 0 || dataAt < 0) {
			return null;
		}

		return bitsPerSample switch {
			8 => new WaveSample(FromUnsigned8(bytes, dataAt, dataLength), sampleRate),
			16 => new WaveSample(FromSigned16(bytes, dataAt, dataLength), sampleRate),
			_ => null,
		};
	}

	private const int PcmFormatTag = 1;

	private static uint FourCc(char a, char b, char c, char d) =>
		(uint)a | ((uint)b << 8) | ((uint)c << 16) | ((uint)d << 24);

	/// <summary>8-bit WAVE data is unsigned with 0x80 as silence; widen it about that midpoint.</summary>
	private static short[] FromUnsigned8(byte[] bytes, int at, int length) {
		var samples = new short[length];
		for (int i = 0; i < length; i++) {
			samples[i] = (short)((bytes[at + i] - 0x80) << 8);
		}

		return samples;
	}

	private static short[] FromSigned16(byte[] bytes, int at, int length) {
		var samples = new short[length / 2];
		for (int i = 0; i < samples.Length; i++) {
			samples[i] = BitConverter.ToInt16(bytes, at + i * 2);
		}

		return samples;
	}
}
