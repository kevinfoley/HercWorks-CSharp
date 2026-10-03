using HercWorks.Vol;
using HercWorks.Vol.Io;

namespace Herculan.Engine.Content;

/// <summary>
/// The engine's resource layer: mounts the game's own <c>.VOL</c> archives and resolves
/// <c>folder\name</c> lookups against them, which is how DBSIM addresses everything it loads
/// (<c>dat\zone504</c>, <c>dba\zone504.dba</c>, <c>dts\samson.dts</c>, ...). DBSIM builds each such
/// path with <c>ResourcePath_BuildFolderName</c> (<c>00492ae0</c>).
///
/// <para><see cref="MountInstall"/> mounts what the program would: every <c>vol\*.vol</c> in the
/// install and then in the <c>drive.cfg</c> directory whose program mask shares a bit with the
/// program's, in descending order of search precedence (<see cref="Voln.VolOrderNum"/>), the first
/// archive holding a <c>folder\name</c> answering for it (docs/formats/vol-archive.md, "Which
/// archives are mounted").</para>
///
/// <para>Every reader still asks for the English folders — the voice clips under <c>SIMVOICE</c>,
/// the simulator's string tables under <c>str</c> — so a v1.10 install's French and German, though
/// mounted, are not reached; docs/retail-builds.md, "How a language is chosen".</para>
///
/// Parsing is delegated wholesale to <see cref="VolFileReader"/> in HercWorks.Vol; this type adds
/// only the index and the load-order rule. Per docs/engine/planning.md's repo-structure decision
/// the engine talks to HercWorks.Core/HercWorks.Vol directly.
/// </summary>
public sealed class GameContent {
	/// <summary>The most archives a program's volume group holds (<c>VolumeGroup_SetGroup</c>, <c>00467bd4</c>).</summary>
	public const int MaxArchives = 0x1e;

	/// <summary>The archive file pattern both programs scan for.</summary>
	public const string ArchivePattern = "*.vol";

	private readonly Dictionary<string, VolEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<Voln> _mounted;

	private GameContent(List<Voln> searchOrder) {
		_mounted = searchOrder;
		foreach (var vol in searchOrder) {
			foreach (var entry in vol.FilesSet) {
				// Folder labels come out of the VOL header already stripped of their '\' separator
				// (see VolFileReader.GenerateFolderList), so the key is just "dir\name".
				if (entry.FileName != null && vol.Folders.TryGetValue(entry.DirIdx, out var folder)) {
					_entries.TryAdd(Key(folder.Label, entry.FileName), entry);
				}
			}
		}
	}

	/// <summary>Names of the archives mounted, in the order they are searched.</summary>
	public IReadOnlyList<string> MountedArchives =>
		_mounted.Select(v => v.FileName ?? "<unnamed>").ToList();

	/// <summary>What DBSIM mounts of the install; see <see cref="MountInstall"/>.</summary>
	public static GameContent MountSimulator(string installRoot) => MountInstall(installRoot, Voln.DbsimProgram);

	/// <summary>What VSHELL mounts of the install; see <see cref="MountInstall"/>.</summary>
	public static GameContent MountShell(string installRoot) => MountInstall(installRoot, Voln.VshellProgram);

	/// <summary>
	/// Mounts what <paramref name="program"/> — <see cref="Voln.DbsimProgram"/> or
	/// <see cref="Voln.VshellProgram"/> — would of the install at <paramref name="installRoot"/>.
	///
	/// <para><c>VolumeGroup_SetGroup</c> (<c>00467bd4</c>) scans <c>vol\*.vol</c> under the current
	/// directory, which is the install, and then under the <c>drive.cfg</c> directory
	/// (<see cref="GameInstall.DiscDirectory"/>): both programs pass 0 for its directory-first
	/// argument. It skips a file whose name an archive already loaded carries, and
	/// <c>VolumeGroup_AddVolume</c> (<c>00467e90</c>) inserts each loaded archive after every one of
	/// equal or higher precedence, so of two equal archives the first loaded is searched first.
	/// <c>findfirst</c> lists a directory in the file system's order; this lists it by name, which
	/// changes nothing for retail data: every <c>folder\name</c> two equal-precedence archives of one
	/// program share has the same content in both, in v1.0 and in v1.10. Names are compared ignoring
	/// case, where retail's comparison is exact.</para>
	///
	/// <para>Retail stops before scanning when the group already holds <see cref="MaxArchives"/>, but
	/// adding past that during a scan writes beyond its array; this stops adding at the limit and says
	/// so.</para>
	/// </summary>
	public static GameContent MountInstall(string installRoot, uint program) {
		string installArchives = GameInstall.ArchiveDirectory(installRoot);
		string?[] directories = {
			installArchives,
			GameInstall.DiscDirectory(installRoot) is { } disc
				? Path.Combine(disc, GameInstall.ArchiveFolderName)
				: null,
		};

		var loaded = new List<Voln>();
		var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
		foreach (string? directory in directories) {
			if (directory == null || !Directory.Exists(directory)) {
				continue;
			}

			foreach (string path in Directory.GetFiles(directory, ArchivePattern, options)
					.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)) {
				string name = Path.GetFileName(path);
				if (loaded.Any(vol => string.Equals(Path.GetFileName(vol.FilePath), name, StringComparison.OrdinalIgnoreCase))
						|| (VolFileReader.ReadProgramMask(path) & program) is null or 0) {
					continue;
				}

				if (loaded.Count == MaxArchives) {
					Console.Error.WriteLine($"Not mounting {path}: a program holds at most {MaxArchives} archives.");
					continue;
				}

				loaded.Add(VolFileReader.ParseVolFile(path));
			}
		}

		if (loaded.Count == 0) {
			throw new FileNotFoundException(
				$"No game archives for program mask 0x{program:x} in {string.Join(" or ", directories.OfType<string>())}.");
		}

		// OrderByDescending is stable, which keeps load order among equal precedences.
		return new GameContent(loaded.OrderByDescending(vol => vol.VolOrderNum).ToList());
	}

	/// <summary>
	/// Reads one resource's bytes, or null if no mounted archive has it. <paramref name="folder"/>
	/// and <paramref name="name"/> are matched case-insensitively, matching the original's own
	/// <c>_stricmp</c>-based resource resolution.
	/// </summary>
	public byte[]? Read(string folder, string name) =>
		_entries.TryGetValue(Key(folder, name), out var entry) ? entry.RawBytes : null;

	/// <summary>
	/// Same as <see cref="Read"/> but throws with the resolved resource path when the entry is
	/// missing — for callers where a missing file means the install is wrong, not a condition to
	/// handle.
	/// </summary>
	public byte[] ReadRequired(string folder, string name) =>
		Read(folder, name) ?? throw new FileNotFoundException(
			$"Resource '{Key(folder, name)}' not present in any mounted archive " +
			$"({string.Join(", ", MountedArchives)}).");

	/// <summary>Whether any mounted archive contains this resource.</summary>
	public bool Contains(string folder, string name) => _entries.ContainsKey(Key(folder, name));

	/// <summary>Every resource name in one folder, in mount order. Useful for tooling and probes.</summary>
	public IEnumerable<string> ListFolder(string folder) {
		string prefix = folder + "\\";
		return _entries.Keys
			.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			.Select(k => k.Substring(prefix.Length))
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
	}

	private static string Key(string folder, string name) => $"{folder}\\{name}";
}
