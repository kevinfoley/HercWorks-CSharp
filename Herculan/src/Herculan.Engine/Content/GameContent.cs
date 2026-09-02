using HercWorks.Vol;
using HercWorks.Vol.Io;

namespace Herculan.Engine.Content;

/// <summary>
/// The engine's resource layer: mounts a set of the game's own <c>.VOL</c> archives and resolves
/// <c>folder\name</c> lookups against them, which is how DBSIM addresses everything it loads
/// (<c>dat\zone504</c>, <c>dba\zone504.dba</c>, <c>dts\samson.dts</c>, ...).
///
/// Mount order follows the VOL header's own load-precedence byte (<c>VolOrderNum</c>: 0x05 for
/// "first loaded", 0x0A for "load second", e.g. SIMPATCH.VOL) — archives are mounted in ascending
/// order and a later mount shadows an earlier one for the same <c>folder\name</c>, so the retail
/// patch VOL wins over the base VOL exactly as it does in the original.
///
/// Parsing is delegated wholesale to <see cref="VolFileReader"/> in HercWorks.Vol; this type adds
/// only the index and the load-order rule. Per docs/engine/planning.md's repo-structure decision
/// the engine talks to HercWorks.Core/HercWorks.Vol directly and never through
/// HercWorks.TransferApi, which is UI-only plumbing.
/// </summary>
public sealed class GameContent {
	/// <summary>
	/// The archives DBSIM needs for a terrain-and-one-mech scene: the main simulator archive, its
	/// retail patch, the zone heightmaps and the effect sample bank.
	///
	/// <para>The voice archives are still left out. They are seven megabytes each and
	/// <see cref="VolFileReader"/> holds both the whole file and a per-entry copy of every entry in
	/// memory, so nothing should mount one until the speech channel needs it.</para>
	/// </summary>
	public static readonly string[] SimulatorArchives =
		{ "SIMVOL0.VOL", "SIMPATCH.VOL", "ZONES.VOL", "SIMSOUND.VOL" };

	private readonly Dictionary<string, VolEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<Voln> _mounted = new();

	/// <summary>Directory the archives were mounted from (the game's <c>VOL</c> folder).</summary>
	public string ArchiveDirectory { get; }

	private GameContent(string archiveDirectory) {
		ArchiveDirectory = archiveDirectory;
	}

	/// <summary>Names of the archives actually mounted, in the order they were applied.</summary>
	public IReadOnlyList<string> MountedArchives =>
		_mounted.Select(v => v.FileName ?? "<unnamed>").ToList();

	/// <summary>
	/// Mounts the named archives out of <paramref name="archiveDirectory"/> (the game's <c>VOL</c>
	/// folder). Missing archives are skipped rather than fatal — SIMPATCH.VOL in particular is a
	/// patch, and an unpatched install legitimately won't have it.
	/// </summary>
	public static GameContent Mount(string archiveDirectory, IEnumerable<string>? archiveNames = null) {
		if (!Directory.Exists(archiveDirectory)) {
			throw new DirectoryNotFoundException($"Game archive directory not found: {archiveDirectory}");
		}

		var content = new GameContent(archiveDirectory);

		var found = (archiveNames ?? SimulatorArchives)
			.Select(name => Path.Combine(archiveDirectory, name))
			.Where(File.Exists)
			.Select(VolFileReader.ParseVolFile)
			.OrderBy(vol => vol.VolOrderNum)
			.ToList();

		foreach (var vol in found) {
			content.Apply(vol);
		}

		if (content._mounted.Count == 0) {
			throw new FileNotFoundException(
				$"No game archives could be mounted from {archiveDirectory}. Expected at least one of: " +
				string.Join(", ", archiveNames ?? SimulatorArchives));
		}

		return content;
	}

	private void Apply(Voln vol) {
		_mounted.Add(vol);

		foreach (var entry in vol.FilesSet) {
			if (entry.FileName == null || !vol.Folders.TryGetValue(entry.DirIdx, out var folder)) {
				continue;
			}

			// Folder labels come out of the VOL header already stripped of their '\' separator
			// (see VolFileReader.GenerateFolderList), so the key is just "dir\name".
			_entries[Key(folder.Label, entry.FileName)] = entry;
		}
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
