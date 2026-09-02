using HercWorks.Core.Data.File.Wav;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Reads a RIFF/WAVE file's header for display in the VOL browser's Content panel — format,
/// channel count, sample rate, bit depth and duration — without decoding the sample data itself.
/// Chunks are walked rather than assumed to sit at fixed offsets, matching
/// Herculan.Engine.Audio.WaveSample.Decode's approach for the same reason: a couple of retail voice
/// files carry trailing data past the end of their RIFF chunk.
///
/// Deliberately more permissive than WaveSample: that class only accepts mono uncompressed PCM,
/// because that is all the shipped sample banks ever contain and it exists to feed an audio
/// backend. This exists to describe whatever WAV is selected, so it reports the header as declared
/// regardless of channel count or format tag.
/// </summary>
public class WavInfoTransformer : ByteTransformer<WavInfo> {
	private const int PcmFormatTag = 1;

	public override WavInfo? Parse(byte[]? inputArray) {
		if (inputArray is not { Length: >= 12 } bytes
			|| bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F'
			|| bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E') {
			return null;
		}

		int formatTag = 0, channels = 0, sampleRate = 0, bitsPerSample = 0, dataLength = 0;
		bool sawFormat = false, sawData = false;

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
				formatTag = BitConverter.ToUInt16(bytes, body);
				channels = BitConverter.ToUInt16(bytes, body + 2);
				sampleRate = BitConverter.ToInt32(bytes, body + 4);
				bitsPerSample = BitConverter.ToUInt16(bytes, body + 14);
				sawFormat = true;
			} else if (id == FourCc('d', 'a', 't', 'a')) {
				dataLength = length;
				sawData = true;
			}

			// Chunk bodies are word-aligned; an odd length is followed by a pad byte.
			at = body + length + (length & 1);
		}

		if (!sawFormat || !sawData) {
			return null;
		}

		int bytesPerSecond = channels * (bitsPerSample / 8) * sampleRate;
		var duration = bytesPerSecond > 0
			? TimeSpan.FromSeconds((double)dataLength / bytesPerSecond)
			: TimeSpan.Zero;

		return new WavInfo {
			Format = formatTag == PcmFormatTag ? "PCM" : $"Unknown (tag {formatTag})",
			Channels = channels,
			SampleRate = sampleRate,
			BitsPerSample = bitsPerSample,
			Duration = duration,
		};
	}

	public override byte[]? Write(WavInfo source) =>
		throw new NotSupportedException("WavInfoTransformer is read-only -- it exists to show metadata in the VOL browser, never to round-trip a WAV file.");

	private static uint FourCc(char a, char b, char c, char d) =>
		(uint)a | ((uint)b << 8) | ((uint)c << 16) | ((uint)d << 24);
}
