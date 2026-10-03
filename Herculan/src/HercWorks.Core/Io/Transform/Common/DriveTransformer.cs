using System.Text;
using HercWorks.Core.Data.File.Cfg;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>
/// Reads and writes <c>data\drive.cfg</c> (<see cref="Drive"/>). Both executables read the disc's directory
/// with <c>fscanf("%s")</c>, so it ends at the first whitespace; the install's, the rest of the file, they read
/// the same way and discard, so it is kept whole here. <see cref="Write"/> puts each on a line of its own, as
/// the installer's <c>BATCH.EXE</c> does, and refuses a disc directory the readers would cut.
/// </summary>
public class DriveTransformer : ByteTransformer<Drive> {
	public override Drive? Parse(byte[]? bytes) {
		if (bytes == null) {
			return null;
		}

		string text = Encoding.Latin1.GetString(bytes).TrimStart();
		int end = text.IndexOfAny([' ', '\t', '\r', '\n', '\v', '\f']);
		string rest = end < 0 ? string.Empty : text[end..].Trim();
		return new Drive {
			Directory = text.Length == 0 ? null : end < 0 ? text : text[..end],
			InstallDirectory = rest.Length == 0 ? null : rest,
		};
	}

	/// <exception cref="ArgumentException">The disc's directory holds whitespace, which the readers would cut it at.</exception>
	public override byte[]? Write(Drive source) {
		if (HasWhitespace(source.Directory)) {
			throw new ArgumentException("drive.cfg cannot hold a disc directory with whitespace in it.", nameof(source));
		}

		string text = source.Directory ?? string.Empty;
		if (source.InstallDirectory != null) {
			text += "\r\n" + source.InstallDirectory;
		}

		return Encoding.Latin1.GetBytes(text);
	}

	/// <summary>Whether <paramref name="path"/> holds a character <c>fscanf("%s")</c> would stop at.</summary>
	public static bool HasWhitespace(string? path) => path != null && path.Any(char.IsWhiteSpace);
}
