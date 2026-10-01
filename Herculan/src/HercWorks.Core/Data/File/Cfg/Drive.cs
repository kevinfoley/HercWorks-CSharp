namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// <c>data\drive.cfg</c> — the directory both executables put in front of the movie and
/// voice-clip paths, as the file's first whitespace-separated token. Read by
/// <see cref="Io.Transform.Common.DriveTransformer"/>. See docs/shell/screen-layout.md and
/// docs/formats/cockpit-messages.md, "The training port".
/// </summary>
public class Drive {
	/// <summary>Where the game keeps the file, relative to its <c>data</c> folder.</summary>
	public const string FileName = "drive.cfg";

	/// <summary>The directory, or null when the file holds no token.</summary>
	public string? Directory { get; set; }
}
