using System.ComponentModel;
using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Core.Io.Transform.Shell;
using HercWorks.Vol;

namespace HercWorks.UI;

/// <summary>
/// Editor for SHELL/GAM/HERC_INF.DAT — one row per herc (weight, speed, hardpoint total, salvage
/// requirement, build-mission count, campaign-unlock flag). Works against a loose .DAT file
/// (either unpacked from a VOL via the main window, or a modded override file per the technique
/// documented in the original project's README) rather than a VOL archive directly, so this
/// doesn't require a VOL repacker to be useful yet. Control layout lives in
/// HercStatsForm.Designer.cs so the form can be opened in the WinForms visual designer; this file
/// holds only state and event-handler logic.
/// </summary>
public partial class HercStatsForm : Form {
	private readonly BindingList<HercStatRow> _rows = new();
	private readonly HercInfoTransformer _transformer = new();

	private string? _loadedPath;

	/// <summary>
	/// Original VOL entry prefix (compression type + magic, plus whether a trailing marker byte
	/// was present) captured from the file this editor last loaded — null/false if the loaded
	/// file didn't have one. Round-tripped on save so exports stay retail-compatible instead of
	/// silently dropping header bytes the game (or a byte-exact diff) might care about.
	/// </summary>
	private byte? _originalCompressionType;
	private byte[]? _originalMagicPrefix;
	private bool _originalHadTrailingByte;

	public HercStatsForm() {
		InitializeComponent();
	}

	private void OnClose(object? sender, EventArgs e) => Close();

	private void OnGridCellEndEdit(object? sender, DataGridViewCellEventArgs e) => _grid.InvalidateRow(e.RowIndex);

	private void OnOpen(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "HERC_INF.DAT|HERC_INF.DAT|DAT files (*.dat)|*.dat|All files (*.*)|*.*",
			Title = "Open HERC_INF.DAT"
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			byte[] rawBytes = File.ReadAllBytes(dialog.FileName);
			var prefix = VolEntryPrefixCodec.StripIfPresent(rawBytes);
			var hercInf = (HercInf?)_transformer.BytesToObject(prefix.Content);

			if (hercInf == null) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			_rows.Clear();
			foreach (var entry in hercInf.Data) {
				_rows.Add(new HercStatRow {
					HercId = entry.HercId,
					Weight = entry.Weight,
					Speed = entry.Speed,
					HardpointTotal = entry.HardpointTotal,
					SalvageReq = entry.SalvageReq,
					UnknownFlag = entry.UnknownFlag,
					BuildMissionCount = entry.BuildMissionCount,
					FlagCampaignStart = entry.FlagCampaignStart
				});
			}

			_loadedPath = dialog.FileName;
			_originalCompressionType = prefix.HadPrefix ? prefix.CompressionType : null;
			_originalMagicPrefix = prefix.MagicPrefix;
			_originalHadTrailingByte = prefix.HadTrailingByte;

			string prefixNote = prefix.HadPrefix ? " (VOL entry prefix detected — will be preserved on save)" : "";
			_statusLabel.Text = $"Loaded {Path.GetFileName(dialog.FileName)} — {_rows.Count} hercs.{prefixNote}";
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnSaveAs(object? sender, EventArgs e) {
		if (_rows.Count == 0) {
			MessageBox.Show(this, "Open a HERC_INF.DAT file first.", "Nothing to save",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var dialog = new SaveFileDialog {
			Filter = "HERC_INF.DAT|HERC_INF.DAT|DAT files (*.dat)|*.dat|All files (*.*)|*.*",
			Title = "Save HERC_INF.DAT",
			FileName = _loadedPath == null ? "HERC_INF.DAT" : Path.GetFileName(_loadedPath)
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			var hercInf = new HercInf(_rows.Count) {
				Ext = FileType.Dat,
				Dir = FileType.Gam
			};

			for (int i = 0; i < _rows.Count; i++) {
				var row = _rows[i];
				hercInf.Data[i] = new HercInfEntry {
					HercId = row.HercId,
					Weight = row.Weight,
					Speed = row.Speed,
					HardpointTotal = row.HardpointTotal,
					SalvageReq = row.SalvageReq,
					UnknownFlag = row.UnknownFlag,
					BuildMissionCount = row.BuildMissionCount,
					FlagCampaignStart = row.FlagCampaignStart
				};
			}

			byte[] content = _transformer.ObjectToBytes(hercInf)!;
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
}
