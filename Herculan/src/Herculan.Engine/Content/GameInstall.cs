using HercWorks.Core.Data.File.Cfg;
using HercWorks.Core.Io.Transform.Common;
using Herculan.Engine.World;

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
	/// Where the last install used is remembered: <c>%APPDATA%\Herculan\install-path.txt</c> on Windows,
	/// and the platform's equivalent user-config directory elsewhere. One line, the path.
	/// </summary>
	public static string RememberedPathFile => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
			Environment.SpecialFolderOption.DoNotVerify),
		"Herculan", "install-path.txt");

	/// <summary>
	/// Resolves an install root, in order: an explicit <paramref name="explicitPath"/> (a command
	/// line argument), the <c>ES2_GAME_PATH</c> environment variable, the install last used (see
	/// <see cref="Remember"/>), then an <c>ES2</c> folder beside the running binary or any folder above
	/// it, which covers a sibling install checked out beside this repo, reached from
	/// <c>src/Herculan.Engine.Host/bin/...</c>.
	/// Returns null when nothing matched, so the caller can ask the player or print something more
	/// useful than a stack trace. An explicit path that is not an install is null too, without trying
	/// the rest: a path the player typed is never silently swapped for another.
	/// </summary>
	public static string? Locate(string? explicitPath = null) {
		if (!string.IsNullOrWhiteSpace(explicitPath)) {
			return IsInstallRoot(explicitPath) ? Path.GetFullPath(explicitPath) : null;
		}

		string? fromEnvironment = Environment.GetEnvironmentVariable(PathVariable);
		if (!string.IsNullOrWhiteSpace(fromEnvironment) && IsInstallRoot(fromEnvironment)) {
			return Path.GetFullPath(fromEnvironment);
		}

		// A remembered install that has since moved or been deleted falls through to the search.
		if (LoadRemembered() is { } remembered && IsInstallRoot(remembered)) {
			return Path.GetFullPath(remembered);
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

	/// <summary>
	/// Records <paramref name="installRoot"/> as the install last used, which <see cref="Locate"/> tries
	/// before searching. A failed write is logged and otherwise ignored: the next launch searches again.
	/// </summary>
	public static void Remember(string installRoot) {
		string path = RememberedPathFile;
		try {
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, Path.GetFullPath(installRoot));
		} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
			Console.Error.WriteLine($"Could not write {path}: {ex.Message}");
		}
	}

	/// <summary>The <c>VOL</c> archive directory inside an install root.</summary>
	public static string ArchiveDirectory(string installRoot) =>
		Path.Combine(installRoot, ArchiveFolderName);

	/// <summary>Whether <paramref name="path"/> is an install root: a directory holding the archive directory.</summary>
	public static bool IsInstallRoot(string path) =>
		Directory.Exists(path) && Directory.Exists(ArchiveDirectory(path));

	/// <summary>
	/// The directory the first token of the install's <c>data\drive.cfg</c> names, which on a retail
	/// install is the disc (docs/formats/vol-archive.md, "Which archives are mounted"), resolved against
	/// the install root; null when the file is missing, unreadable or holds no token. Retail cannot start
	/// without the file (<c>Sim_Run</c>, <c>0045f144</c>; <c>DriveCfg_Read</c>, VSHELL <c>0040d327</c>);
	/// here an install without one simply has no disc.
	/// </summary>
	public static string? DiscDirectory(string installRoot) {
		string path = Path.Combine(installRoot, MissionLoader.DataFolderName, Drive.FileName);
		try {
			if (!File.Exists(path) || new DriveTransformer().Parse(File.ReadAllBytes(path))?.Directory is not { } directory) {
				return null;
			}

			return Path.GetFullPath(Path.Combine(installRoot, directory));
		} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
				or NotSupportedException) {
			Console.Error.WriteLine($"Could not read {path} ({ex.Message}); the install has no disc.");
			return null;
		}
	}

	/// <summary>
	/// A file both programs read from the disc — a movie, the on-line manual, a training instructor
	/// clip: <paramref name="relativePath"/> under <see cref="DiscDirectory"/> when it is there, and
	/// otherwise under the install root.
	///
	/// <para>Retail looks on the disc only (<c>DriveCfg_PrefixPath</c>, <c>0045ee44</c>;
	/// <c>Path_UnderDriveCfg</c>, VSHELL <c>0040d429</c>). Falling back to the install is this engine's,
	/// so an install copied whole, whose <c>drive.cfg</c> still names a CD drive that is gone, keeps its
	/// movies, manual and instructor; see KNOWN_ISSUES.md.</para>
	/// </summary>
	public static string DiscFile(string installRoot, string relativePath) {
		if (DiscDirectory(installRoot) is { } disc) {
			string onDisc = Path.Combine(disc, relativePath);
			if (File.Exists(onDisc)) {
				return onDisc;
			}
		}

		return Path.Combine(installRoot, relativePath);
	}

	private static string? LoadRemembered() {
		string path = RememberedPathFile;
		try {
			return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
		} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
			Console.Error.WriteLine($"Could not read {path} ({ex.Message}); searching for the install instead.");
			return null;
		}
	}
}
