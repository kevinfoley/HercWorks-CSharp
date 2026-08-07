using System.ComponentModel;
using System.Text;
using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Core.Io.Transform.Shell;
using HercWorks.Vol;

namespace HercWorks.UI;

/// <summary>
/// Editor for SHELL/GAM/WEAPONS.DAT's weapon catalog (id, name, salvage cost, start-unlock flag,
/// armory autobuild priority) — one row per weapon. The file's separate campaign-start loadout
/// section (StartingWeapons) isn't shown here; that belongs more to a future Campaign Resources
/// editor than to per-weapon stats, but its bytes are carried through unchanged on save so
/// nothing is lost. Follows the same pattern as HercStatsForm: works against a loose .DAT file,
/// uses the shared VolEntryPrefixCodec so exports stay retail-compatible, and keeps layout in
/// WeaponStatsForm.Designer.cs for the WinForms visual designer.
/// </summary>
public partial class WeaponStatsForm : Form {
	private readonly BindingList<WeaponStatRow> _rows = new();
	private readonly WeaponsDatTransformer _transformer = new();

	private string? _loadedPath;

	// Campaign-start loadout section, carried through unchanged — not edited by this form.
	private short _loadedStartWeaponTotal;
	private UiWeaponEntry[]? _loadedStartingWeapons;

	/// <summary>Original VOL entry prefix, round-tripped on save — see VolEntryPrefixCodec.</summary>
	private byte? _originalCompressionType;
	private byte[]? _originalMagicPrefix;
	private bool _originalHadTrailingByte;

	public WeaponStatsForm() {
		InitializeComponent();
	}

	private void OnClose(object? sender, EventArgs e) => Close();

	private void OnGridCellEndEdit(object? sender, DataGridViewCellEventArgs e) => _grid.InvalidateRow(e.RowIndex);

	private void OnOpen(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "WEAPONS.DAT|WEAPONS.DAT|DAT files (*.dat)|*.dat|All files (*.*)|*.*",
			Title = "Open WEAPONS.DAT"
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			byte[] rawBytes = File.ReadAllBytes(dialog.FileName);
			var prefix = VolEntryPrefixCodec.StripIfPresent(rawBytes);
			var weaponsDat = (WeaponsDat?)_transformer.BytesToObject(prefix.Content);

			if (weaponsDat == null) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			_rows.Clear();
			foreach (var entry in weaponsDat.Data) {
				_rows.Add(new WeaponStatRow {
					Id = entry.Id,
					Name = DecodeName(entry.Name),
					SalvageCost = entry.SalvageCost,
					StartUnlock = entry.StartUnlock,
					AutobuildPriority = entry.AutobuildPriority
				});
			}

			_loadedStartWeaponTotal = weaponsDat.StartWeaponTotal;
			_loadedStartingWeapons = weaponsDat.StartingWeapons;

			_loadedPath = dialog.FileName;
			_originalCompressionType = prefix.HadPrefix ? prefix.CompressionType : null;
			_originalMagicPrefix = prefix.MagicPrefix;
			_originalHadTrailingByte = prefix.HadTrailingByte;

			string prefixNote = prefix.HadPrefix ? " (VOL entry prefix detected — will be preserved on save)" : "";
			_statusLabel.Text = $"Loaded {Path.GetFileName(dialog.FileName)} — {_rows.Count} weapons.{prefixNote}";
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnSaveAs(object? sender, EventArgs e) {
		if (_rows.Count == 0) {
			MessageBox.Show(this, "Open a WEAPONS.DAT file first.", "Nothing to save",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var dialog = new SaveFileDialog {
			Filter = "WEAPONS.DAT|WEAPONS.DAT|DAT files (*.dat)|*.dat|All files (*.*)|*.*",
			Title = "Save WEAPONS.DAT",
			FileName = _loadedPath == null ? "WEAPONS.DAT" : Path.GetFileName(_loadedPath)
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			var weaponsDat = new WeaponsDat(_rows.Count) {
				FileName = "WEAPONS",
				Ext = FileType.Dat,
				Dir = FileType.Gam,
				StartWeaponTotal = _loadedStartWeaponTotal,
				StartingWeapons = _loadedStartingWeapons
			};

			for (int i = 0; i < _rows.Count; i++) {
				var row = _rows[i];
				byte[] nameBytes = EncodeName(row.Name);

				var entry = weaponsDat.AddEntry(i);
				entry.Id = row.Id;
				entry.NameLen = (short)nameBytes.Length;
				entry.Name = nameBytes;
				entry.SalvageCost = row.SalvageCost;
				entry.StartUnlock = row.StartUnlock;
				entry.AutobuildPriority = row.AutobuildPriority;
			}

			byte[] content = _transformer.ObjectToBytes(weaponsDat)!;
			byte[] outBytes;
			string formatNote;

			if (_originalCompressionType.HasValue && _originalMagicPrefix != null) {
				outBytes = VolEntryPrefixCodec.Wrap(
					content, _originalCompressionType.Value, _originalMagicPrefix, _originalHadTrailingByte);
				formatNote = "retail-compatible format — the original VOL entry prefix (compression type, magic) was preserved, with the size field updated for the edited content";
			} else {
				outBytes = content;
				formatNote = "content-only format — this file wasn't loaded with a VOL entry prefix to preserve, so no prefix could be reconstructed for this export";
			}

			File.WriteAllBytes(dialog.FileName, outBytes);

			MessageBox.Show(this,
				$"Saved in {formatNote}.\n\nDrop this file into your ES2 install's GAM\\ folder to override the packed VOL copy.",
				"Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to save file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	// Weapon names are stored as raw length-prefixed bytes, one byte per char (matching how
	// ThreeSpaceByteTransformer.IndexString reads other strings elsewhere in Core) — not a
	// .NET string encoding. Latin1 gives an exact one-byte-per-char round trip for that shape.
	private static string DecodeName(byte[]? nameBytes) {
		if (nameBytes == null) {
			return string.Empty;
		}
		string raw = Encoding.Latin1.GetString(nameBytes);
		int nullIndex = raw.IndexOf('\0');
		return nullIndex >= 0 ? raw[..nullIndex] : raw;
	}

	private static byte[] EncodeName(string name) => Encoding.Latin1.GetBytes(name + "\0");
}
