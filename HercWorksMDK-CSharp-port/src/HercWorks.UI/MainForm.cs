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
/// Export dialog, and viewing .DTS 3D models with an orbit camera via the Tools
/// menu's 3D Model Viewer (all built directly on HercWorks.Core — no ES2TransferApi
/// dependency). The VOL browser's right-hand side is a fixed-height Metadata list
/// (the raw entry info — offset, size, compression, magic prefix — plus a "File Type"
/// row with a short human-readable description whenever TransformerRegistry recognizes
/// the file) stacked above a Content tree that fills the remaining space: when the
/// selected file's type is recognized by TransformerRegistry, Content shows its actual parsed,
/// human-readable data (fully expanded) via ContentTreeRenderer; unrecognized types
/// just show a "no parser available" note there. Below Content, a "View Asset" button
/// (enabled only for DTS/DBA/DBM entries) opens the selected file directly in the 3D
/// Model Viewer or the new Texture Viewer (HercWorks.UI.TextureViewerForm — preview
/// only, best-effort automatic palette matching with a manual dropdown fallback,
/// since DBA/DBM never embed which palette they use), reading straight from the
/// loaded VOL rather than requiring an already-extracted loose file. Mission file
/// editing, the last "ideal feature" from the original README, is still a stubbed
/// disabled menu entry. Control layout lives in MainForm.Designer.cs so the form can
/// be opened in the WinForms visual designer; this file holds only state and
/// event-handler logic.
/// </summary>
public partial class MainForm : Form {
	private Voln? _currentVol;
	private VolEntry? _selectedEntry;

	public MainForm() {
		InitializeComponent();
	}

	private void OnExit(object? sender, EventArgs e) => Close();

	/// <summary>
	/// Distinct per-dialog identity so Windows remembers this dialog's last-visited folder
	/// separately from other Open/Save dialogs in the app — see CampaignResourcesForm's
	/// DialogClientGuid for the full explanation.
	/// </summary>
	private static readonly Guid OpenVolClientGuid = new("af529f56-306a-4c02-9f85-feb11bdd75a1");

	private void OnOpenVol(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "Earthsiege 2 VOL files (*.vol)|*.vol|All files (*.*)|*.*",
			Title = "Open VOL file",
			ClientGuid = OpenVolClientGuid
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

	private void OnOpenModelViewer(object? sender, EventArgs e) {
		using var form = new Model3DViewerForm();
		form.ShowDialog(this);
	}

	/// <summary>
	/// Opens the currently selected VOL-tree entry in whichever viewer matches its type — the
	/// 3D model viewer for DTS, the texture viewer for DBA/DBM. _viewAssetButton.Enabled already
	/// guarantees _selectedEntry and _currentVol are usable here (see UpdateViewAssetButtonState).
	/// </summary>
	private void OnViewAsset(object? sender, EventArgs e) {
		if (_selectedEntry == null || _currentVol == null) {
			return;
		}

		if (_selectedEntry.Ext == FileType.Dts) {
			using var form = new Model3DViewerForm();
			form.LoadFromVolEntry(_selectedEntry);
			form.ShowDialog(this);
		} else if (_selectedEntry.Ext is FileType.Dba or FileType.Dbm or FileType.Hba or FileType.Hb0 or FileType.Hb1 or FileType.Hb2
			or FileType.Db0 or FileType.Db1 or FileType.Db2) {
			using var form = new TextureViewerForm();
			form.LoadFromVolEntry(_selectedEntry, _currentVol);
			form.ShowDialog(this);
		}
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

		_selectedEntry = e.Node?.Tag as VolEntry;
		UpdateViewAssetButtonState();

		if (_selectedEntry is not { } entry) {
			return;
		}

		AddDetail("File Name", entry.FileName ?? string.Empty);
		AddDetail("Directory", entry.Dir?.Val() ?? "(unknown)");
		AddDetail("Extension", entry.Ext?.Val() ?? "(unknown)");
		if (TransformerRegistry.FindLabel(entry) is { } typeLabel) {
			AddDetail("File Type", typeLabel);
		}
		AddDetail("Offset In VOL", entry.VolOffsetValue.ToString());
		AddDetail("Size (bytes)", (entry.RawBytes?.Length ?? 0).ToString());
		AddDetail("Compression Type", entry.FileCompressionType.ToString());
		AddDetail("Magic Prefix", ByteOps.ToHex(entry.MagicPrefix));

		PopulateContent(entry);
	}

	/// <summary>
	/// "View Asset" is enabled only for types that actually have a viewer: DTS (3D model) and
	/// DBA/DBM/HBA/HB0-2/DB0-2 (texture — the HBx/DBx types are byte-identical to the DBA container
	/// format, see TransformerRegistry's doc comment). A DPL alone isn't a texture, so it's
	/// intentionally excluded here.
	/// </summary>
	private void UpdateViewAssetButtonState() {
		_viewAssetButton.Enabled = _selectedEntry is { RawBytes.Length: > 0 } entry &&
			entry.Ext is FileType.Dts or FileType.Dba or FileType.Dbm or FileType.Hba or FileType.Hb0 or FileType.Hb1 or FileType.Hb2
				or FileType.Db0 or FileType.Db1 or FileType.Db2;
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
