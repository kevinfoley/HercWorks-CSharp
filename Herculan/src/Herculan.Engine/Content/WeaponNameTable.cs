using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Io.Transform.Shell;
using HercWorks.Vol;
using HercWorks.Vol.Io;

namespace Herculan.Engine.Content;

/// <summary>
/// Short weapon names by weapon id — what the cockpit's hardpoint rows print ("ATC50", "EMPC").
///
/// <para>The names live on the shell side, in <c>SHELL0.VOL</c>'s <c>gam\WEAPONS.DAT</c>: a 33-entry
/// catalog of <c>{id, name, salvage cost, start-unlocked, build priority}</c> keyed by the same
/// weapon ids <c>player.mec</c>'s hardpoint list uses. DBSIM itself does not carry them — its own
/// <c>dat\WEAPONS.DAT</c> is a mount-template table with no strings in it at all, and the name a
/// weapon gauge prints comes in as a pointer on the already-built weapon object
/// (<c>FUN_0040e18c</c>).</para>
///
/// <para><b>Read straight out of the archive rather than mounted.</b> <c>SHELL0.VOL</c> is 8.8 MB and
/// ships its own <c>DBA</c>, <c>DPL</c> and <c>DFN</c> folders, several of whose names collide with
/// <c>SIMVOL0</c>'s — mounting it into <see cref="GameContent"/> would let shell art shadow simulator
/// art depending on load order. One 717-byte catalog is not worth that risk, so this parses the
/// archive, takes the entry and lets the rest go.</para>
/// </summary>
public sealed class WeaponNameTable {
	/// <summary>The shell archive the catalog lives in, and its folder and name inside it.</summary>
	public const string ArchiveName = "SHELL0.VOL";
	public const string ResourceFolder = "gam";
	public const string ResourceName = "WEAPONS.DAT";

	private readonly Dictionary<int, string> _names;

	private WeaponNameTable(Dictionary<int, string> names) {
		_names = names;
	}

	/// <summary>The name for <paramref name="weaponId"/>, or null when the catalog has no such id.</summary>
	public string? Name(int weaponId) => _names.TryGetValue(weaponId, out var name) ? name : null;

	/// <summary>Names for a hardpoint list, in order; unknown ids become empty strings so slot indices stay aligned.</summary>
	public IReadOnlyList<string> NamesFor(IEnumerable<int> weaponIds) =>
		weaponIds.Select(id => Name(id) ?? string.Empty).ToList();

	/// <summary>
	/// Loads the catalog out of <c>SHELL0.VOL</c> beside the mounted simulator archives. Returns null
	/// when the archive or the entry is missing — the cockpit then draws hardpoint rows without names
	/// rather than inventing any.
	/// </summary>
	public static WeaponNameTable? Load(GameContent content) {
		string path = Path.Combine(content.ArchiveDirectory, ArchiveName);
		if (!File.Exists(path)) {
			return null;
		}

		byte[]? bytes = null;
		var vol = VolFileReader.ParseVolFile(path);
		foreach (var entry in vol.FilesSet) {
			if (entry.FileName != null
				&& entry.FileName.Equals(ResourceName, StringComparison.OrdinalIgnoreCase)
				&& vol.Folders.TryGetValue(entry.DirIdx, out var folder)
				&& folder.Label.Equals(ResourceFolder, StringComparison.OrdinalIgnoreCase)) {
				bytes = entry.RawBytes;
				break;
			}
		}

		if (bytes == null || new WeaponsDatTransformer().BytesToObject(bytes) is not WeaponsDat catalog
			|| catalog.Data is not { Length: > 0 } entries) {
			return null;
		}

		var names = new Dictionary<int, string>();
		foreach (var entry in entries) {
			if (entry?.Name is { Length: > 0 } raw) {
				// Names are stored with their terminator inside the declared length.
				names[entry.Id] = System.Text.Encoding.ASCII.GetString(raw).TrimEnd('\0');
			}
		}

		return names.Count > 0 ? new WeaponNameTable(names) : null;
	}
}
