namespace HercWorks.Video.Codecs;

/// <summary>
/// PLACEHOLDER: stands in for a codec the corpus uses and this assembly does not decode yet —
/// Microsoft Video 1, Cinepak and Indeo Video 4.1 (docs/formats/avi-video.md#open). Every packet
/// paints the same magenta-and-black checkerboard, which no retail movie shows, so a movie still
/// runs its full length at its own rate, and its soundtrack still plays, without a frame of it being
/// guessed at.
///
/// <para><see cref="MoviePlayback.Open"/> hands this out only when its caller asks for it; the
/// <c>--movie</c> viewer does not, and reports the stream as undecodable instead.</para>
/// </summary>
public sealed class PlaceholderDecoder : IVideoCodec {
	/// <summary>The checkerboard's cell size in pixels.</summary>
	private const int Cell = 16;

	public bool DecodeFrame(ReadOnlySpan<byte> packet, VideoFrame frame) {
		ArgumentNullException.ThrowIfNull(frame);

		for (int y = 0; y < frame.Height; y++) {
			for (int x = 0; x < frame.Width; x++) {
				bool lit = ((x / Cell) + (y / Cell)) % 2 == 0;
				frame.SetPixel(x, y, lit ? (byte)0xFF : (byte)0, 0, lit ? (byte)0xFF : (byte)0);
			}
		}

		return true;
	}
}
