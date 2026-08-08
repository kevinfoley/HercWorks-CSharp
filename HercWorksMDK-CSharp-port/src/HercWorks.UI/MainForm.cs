using HercWorks.Core.Io.Transform;
using HercWorks.Vol;
using HercWorks.Vol.Io;
using HercWorks.Vol.Util;

namespace HercWorks.UI;

/// <summary>
/// Starting shell for the HercWorks MDK WinForms port. Currently supports opening
/// and browsing a .vol archive, unpacking it to a folder, editing HERC_INF.DAT via
/// the Herc Stats editor, editing WEAPONS.DAT via the Item Stats editor, editing a
/// player .sav file's salvage/workshop slots via the Campaign Resources editor, and
/// exporting DBA/DBM/DPL Dynamix bitmap data to PNG via the Tools menu's Image
/// Export dialog (all built directly on HercWorks.Core — no ES2TransferApi
/// dependency). The VOL browser's right-hand side is a fixed-height Metadata list
/// (the raw entry info — offset, size, compression, magic prefix) stacked above a
/// Content tree that fills the remaining space: when the selected file's type is
/// recognized by TransformerRegistry, Content shows its actual parsed,
/// human-readable data (fully expanded) via ContentTreeRenderer; unrecognized types
/// just show a "no parser available" note there. Mission file editing, the last
/// "ideal feature" from the original README, is still a stubbed disabled menu
/// entry. Control layout lives in MainForm.Designer.cs so the form can be opened in
/// the WinForms visual designer; this file holds only state and event-handler logic.
/// </summary>
public partial class MainForm : Form {
	private Voln? _currentVol;

	public MainForm() {
		InitializeComponent();
	}

	private void OnExit(object? sender, EventArgs e) => Close();

	private void OnOpenVol(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "Earthsiege 2 VOL files (*.vol)|*.vol|All files (*.*)|*.*",
			Title = "Open VOL file"
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			_currentVol = VolFileReader.ParseVolFile(dialog.FileName);
			PopulateTree(_currentVol);
			_statusLabel.Text = $"Loaded {_currentVol.FileName} — {_currentVol.FilesSet.Length} files, {_currentVol.Folders.Count} folders.";
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load VOL file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnOpenHercStats(object? sender, EventArgs e) {
		using var form = new HercStatsForm();
		form.ShowDialog(this);
	}

	private void OnOpenItemStats(object? sender, EventArgs e) {
		using var form = new WeaponStatsForm();
		form.ShowDialog(this);
	}

	private void OnOpenCampaignResources(object? sender, EventArgs e) {
		using var form = new CampaignResourcesForm();
		form.ShowDialog(this);
	}

	private void OnOpenImageExport(object? sender, EventArgs e) {
		using var form = new ImageExportForm();
		form.ShowDialog(this);
	}

	private void OnUnpackVol(object? sender, EventArgs e) {
		if (_currentVol == null) {
			MessageBox.Show(this, "Open a VOL file first.", "No VOL loaded",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var dialog = new FolderBrowserDialog { Description = "Choose a destination folder" };
		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			VolFileWriter.UnpackVol(_currentVol, dialog.SelectedPath);
			MessageBox.Show(this, "Unpack complete.", "Done",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to unpack VOL file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void PopulateTree(Voln vol) {
		_volTree.Nodes.Clear();
		var root = new TreeNode(vol.FileName);

		foreach (var kv in vol.Folders.OrderBy(f => f.Key)) {
			var dirNode = new TreeNode(kv.Value.Label) { Tag = kv.Value };
			foreach (var file in kv.Value.Files) {
				dirNode.Nodes.Add(new TreeNode(file.FileName) { Tag = file });
			}
			root.Nodes.Add(dirNode);
		}

		_volTree.Nodes.Add(root);
		root.Expand();
	}

	private void OnTreeSelect(object? sender, TreeViewEventArgs e) {
		_fileDetails.Items.Clear();
		_contentTree.Nodes.Clear();

		if (e.Node?.Tag is not VolEntry entry) {
			return;
		}

		AddDetail("File Name", entry.FileName ?? string.Empty);
		AddDetail("Directory", entry.Dir?.Val() ?? "(unknown)");
		AddDetail("Extension", entry.Ext?.Val() ?? "(unknown)");
		AddDetail("Offset In VOL", entry.VolOffsetValue.ToString());
		AddDetail("Size (bytes)", (entry.RawBytes?.Length ?? 0).ToString());
		AddDetail("Compression Type", entry.FileCompressionType.ToString());
		AddDetail("Magic Prefix", ByteOps.ToHex(entry.MagicPrefix));

		PopulateContent(entry);
	}

	private void PopulateContent(VolEntry entry) {
		var transformer = TransformerRegistry.FindTransformer(entry);
		if (transformer == null) {
			_contentTree.Nodes.Add(new TreeNode(
				"No parser available for this file type yet — showing metadata only."));
			return;
		}

		if (entry.RawBytes == null || entry.RawBytes.Length == 0) {
			_contentTree.Nodes.Add(new TreeNode("File has no data to parse."));
			return;
		}

		try {
			var parsed = transformer.BytesToObject(entry.RawBytes);
			if (parsed == null) {
				_contentTree.Nodes.Add(new TreeNode("Parser returned no data for this file."));
				return;
			}

			string label = TransformerRegistry.FindLabel(entry) ?? entry.FileName ?? "Content";
			ContentTreeRenderer.Populate(_contentTree, label, parsed);
		} catch (Exception ex) {
			_contentTree.Nodes.Add(new TreeNode($"Failed to parse this file:\n{ex.Message}"));
		}
	}

	private void AddDetail(string label, string value) {
		_fileDetails.Items.Add(new ListViewItem(new[] { label, value }));
	}
}
