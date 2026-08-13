namespace Herculan.Engine.Content;

/// <summary>
/// Finds an Earthsiege 2 installation to load data from. The engine never runs the original
/// executables (see docs/engine/planning.md) — it only reads their data files — so "an install"
/// here means a directory containing a <c>VOL</c> folder with the game's archives in it.
/// </summary>
public static class GameInstall {
	/// <summary>Environment variable checked first, so a developer can point at any install.</summary>
	public const string PathVariable = "ES2_GAME_PATH";

	/// <summary>The archive subfolder inside an install root.</summary>
	public const string ArchiveFolderName = "VOL";

	/// <summary>
	/// Resolves an install root, in order: an explicit <paramref name="explicitPath"/> (a command
	/// line argument), the <c>ES2_GAME_PATH</c> environment variable, then a short list of paths
	/// relative to the running binary that cover this repo's own layout
	/// (<c>E:\ES2Stuff\ES2</c> reached from <c>src/Herculan.Engine.Host/bin/...</c>).
	/// Returns null when nothing matched, so the caller can print something more useful than a
	/// stack trace.
	/// </summary>
	public static string? Locate(string? explicitPath = null) {
		if (!string.IsNullOrWhiteSpace(explicitPath)) {
			return IsInstallRoot(explicitPath) ? Path.GetFullPath(explicitPath) : null;
		}

		string? fromEnvironment = Environment.GetEnvironmentVariable(PathVariable);
		if (!string.IsNullOrWhiteSpace(fromEnvironment) && IsInstallRoot(fromEnvironment)) {
			return Path.GetFullPath(fromEnvironment);
		}

		// Walk up from the binary looking for a sibling ES2 folder — covers running straight out
		// of the repo without any configuration, which is the normal development case.
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null) {
			string candidate = Path.Combine(directory.FullName, "ES2");
			if (IsInstallRoot(candidate)) {
				return Path.GetFullPath(candidate);
			}
			directory = directory.Parent;
		}

		return null;
	}

	/// <summary>The <c>VOL</c> archive directory inside an install root.</summary>
	public static string ArchiveDirectory(string installRoot) =>
		Path.Combine(installRoot, ArchiveFolderName);

	private static bool IsInstallRoot(string path) =>
		Directory.Exists(path) && Directory.Exists(ArchiveDirectory(path));
}
