using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Io.Transform.Dbsim;
using HercWorks.Vol;

namespace HercWorks.UI;

/// <summary>
/// Viewer for .DTS 3D models (mechs, terrain props, etc.) — orbit camera over a flat/solid-shaded
/// software-rendered mesh (see Model3DViewerControl for the rasterizer and DtsGeometryBuilder for
/// how DTS's chunk tree becomes triangles). Textured polys render with a placeholder color since
/// DTS texture binding was never reverse-engineered (see DtsGeometryBuilder's doc comment).
/// Follows the same designer-split, Open-dialog-with-VolEntryPrefixCodec pattern as ImageExportForm.
/// </summary>
public partial class Model3DViewerForm : Form {
	private readonly DTSModelTransformer _dtsTransformer = new();

	// Kept around (rather than discarded after the initial Build()) so the Detail Level combo can
	// re-walk a single root's own tree on demand — see RefreshDetailLevelSelector/
	// OnDetailLevelSelectionChanged.
	private DynamixThreeSpaceModel? _currentModel;
	private string? _loadedDisplayName;

	public Model3DViewerForm() {
		InitializeComponent();
		SetRenderMode(Model3DRenderMode.ShadedWireframe);
	}

	private void OnClose(object? sender, EventArgs e) => Close();

	private void OnResetView(object? sender, EventArgs e) => _viewerControl.ResetView();

	private void OnShadedMode(object? sender, EventArgs e) => SetRenderMode(Model3DRenderMode.Shaded);

	private void OnWireframeMode(object? sender, EventArgs e) => SetRenderMode(Model3DRenderMode.Wireframe);

	private void OnShadedWireframeMode(object? sender, EventArgs e) => SetRenderMode(Model3DRenderMode.ShadedWireframe);

	private void SetRenderMode(Model3DRenderMode mode) {
		_viewerControl.RenderMode = mode;
		_shadedModeMenuItem.Checked = mode == Model3DRenderMode.Shaded;
		_wireframeModeMenuItem.Checked = mode == Model3DRenderMode.Wireframe;
		_shadedWireframeModeMenuItem.Checked = mode == Model3DRenderMode.ShadedWireframe;
		_viewerControl.Invalidate();
	}

	private void OnOpenDts(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "DTS 3D Model files (*.dts)|*.dts|All files (*.*)|*.*",
			Title = "Open DTS file"
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			byte[] rawBytes = File.ReadAllBytes(dialog.FileName);
			var prefix = VolEntryPrefixCodec.StripIfPresent(rawBytes);

			var model = (DynamixThreeSpaceModel?)_dtsTransformer.BytesToObject(prefix.Content);
			if (model == null) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			ApplyModel(model, Path.GetFileName(dialog.FileName));
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load DTS file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	/// <summary>
	/// Loads a DTS model still packed inside a loaded VOL — e.g. from MainForm's "View Asset"
	/// button. Unlike the loose-file OpenFileDialog path above, VolEntry.RawBytes is already clean
	/// content (VolFileReader strips the 9-byte per-entry prefix at parse time), so no
	/// VolEntryPrefixCodec step is needed here.
	/// </summary>
	public void LoadFromVolEntry(VolEntry entry) {
		try {
			var model = (DynamixThreeSpaceModel?)_dtsTransformer.BytesToObject(entry.RawBytes);
			if (model == null) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			ApplyModel(model, entry.FileName ?? "(DTS asset)");
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load DTS file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void ApplyModel(DynamixThreeSpaceModel model, string displayName) {
		_currentModel = model;
		_loadedDisplayName = displayName;

		var roots = DtsGeometryBuilder.Build(model);
		_viewerControl.LoadMeshes(roots);
		PopulatePartSelector(roots);
	}

	/// <summary>
	/// A DTS file's top-level entries are each independent objects with no in-file signal for how
	/// they relate to each other (see DtsGeometryBuilder's doc comment) — showing more than one at
	/// once produces garbled overlapping geometry regardless of whether they're really LOD levels
	/// of one thing or several unrelated objects sharing the file. So this always shows exactly
	/// one, defaulting to the first, and lets the user step through the rest one at a time.
	/// </summary>
	private void PopulatePartSelector(List<DtsRootMesh> roots) {
		_partSelector.SelectedIndexChanged -= OnPartSelectionChanged;

		_partSelector.Items.Clear();
		for (int i = 0; i < roots.Count; i++) {
			_partSelector.Items.Add($"Part {i} — {roots[i].Triangles.Count} triangles");
		}

		_partSelector.SelectedIndexChanged += OnPartSelectionChanged;

		bool canNavigate = _partSelector.Items.Count > 1;
		_prevPartButton.Enabled = canNavigate;
		_nextPartButton.Enabled = canNavigate;

		if (_partSelector.Items.Count > 0) {
			_partSelector.SelectedIndex = 0;
		} else {
			RefreshDetailLevelSelector();
		}
	}

	private void OnPreviousPart(object? sender, EventArgs e) => NavigatePart(-1);

	private void OnNextPart(object? sender, EventArgs e) => NavigatePart(1);

	/// <summary>Wraps at both ends: Next from the last part goes to the first, and vice versa.</summary>
	private void NavigatePart(int direction) {
		int count = _partSelector.Items.Count;
		if (count <= 1) {
			return;
		}

		int current = Math.Max(_partSelector.SelectedIndex, 0);
		_partSelector.SelectedIndex = ((current + direction) % count + count) % count;
	}

	/// <summary>
	/// Same idea as NavigatePart but for the Detail Level combo — no dedicated buttons for this
	/// one, keyboard-only. A no-op (not an error) when the current Part has no real detail-level
	/// choice, matching NavigatePart's own "nothing to navigate" guard.
	/// </summary>
	private void NavigateDetailLevel(int direction) {
		int count = _lodSelector.Items.Count;
		if (!_lodSelector.Visible || count <= 1) {
			return;
		}

		int current = Math.Max(_lodSelector.SelectedIndex, 0);
		_lodSelector.SelectedIndex = ((current + direction) % count + count) % count;
	}

	/// <summary>
	/// Left/Right navigate parts, Up/Down cycle the current part's detail levels — both regardless
	/// of which control has focus, including the combos themselves (DropDownList-style, so there's
	/// no text cursor for Left/Right to move, and Up/Down would otherwise just silently do nothing
	/// when the dropdown is closed). Up/Down deliberately fall through to normal handling while
	/// either combo's dropdown is actually open (DroppedDown), so the native "arrow keys browse the
	/// open list" behavior still works instead of being hijacked out from under it.
	/// </summary>
	protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
		if (keyData == Keys.Left) {
			NavigatePart(-1);
			return true;
		}
		if (keyData == Keys.Right) {
			NavigatePart(1);
			return true;
		}
		if ((keyData == Keys.Up || keyData == Keys.Down) && !_partSelector.DroppedDown && !_lodSelector.DroppedDown) {
			NavigateDetailLevel(keyData == Keys.Up ? -1 : 1);
			return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	private void OnPartSelectionChanged(object? sender, EventArgs e) {
		int selected = _partSelector.SelectedIndex;
		for (int i = 0; i < _viewerControl.Roots.Count; i++) {
			_viewerControl.SetRootVisible(i, i == selected);
		}

		RefreshDetailLevelSelector();
	}

	/// <summary>
	/// DTS does have a real in-file LOD mechanism (TSDetailPart — see DtsGeometryBuilder's doc
	/// comment), but it's per-part, not global: different Parts can have a different number of
	/// detail levels, or none at all. So this dropdown is rebuilt every time the selected Part
	/// changes, scoped to that Part's own TSObject tree, and hidden entirely when that Part has
	/// one level or fewer — showing an always-visible combo with nothing meaningful to pick would
	/// be misleading. Defaults to the highest level, matching DtsGeometryBuilder.Build()'s default.
	/// </summary>
	private void RefreshDetailLevelSelector() {
		_lodSelector.SelectedIndexChanged -= OnDetailLevelSelectionChanged;
		_lodSelector.Items.Clear();

		int partIndex = _partSelector.SelectedIndex;
		int levelCount = _currentModel?.Meshes is { } meshes && partIndex >= 0 && partIndex < meshes.Count
			? DtsGeometryBuilder.GetDetailLevelCount(meshes[partIndex])
			: 0;

		bool hasChoice = levelCount > 1;
		_lodLabel.Visible = hasChoice;
		_lodSelector.Visible = hasChoice;

		if (hasChoice) {
			for (int i = 0; i < levelCount; i++) {
				_lodSelector.Items.Add($"Detail {i}");
			}
		}

		_lodSelector.SelectedIndexChanged += OnDetailLevelSelectionChanged;

		if (hasChoice) {
			_lodSelector.SelectedIndex = levelCount - 1;
		} else {
			UpdateStatusForCurrentSelection();
		}
	}

	private void OnDetailLevelSelectionChanged(object? sender, EventArgs e) {
		int partIndex = _partSelector.SelectedIndex;
		int lodIndex = _lodSelector.SelectedIndex;
		if (_currentModel?.Meshes is not { } meshes || partIndex < 0 || partIndex >= meshes.Count || lodIndex < 0) {
			return;
		}

		var rebuilt = DtsGeometryBuilder.BuildRoot(meshes[partIndex], $"Part {partIndex}", lodIndex);
		_viewerControl.ReplaceRoot(partIndex, rebuilt);
		UpdateStatusForCurrentSelection();
	}

	private void UpdateStatusForCurrentSelection() {
		int partIndex = _partSelector.SelectedIndex;
		if (partIndex < 0 || partIndex >= _viewerControl.Roots.Count) {
			_statusLabel.Text = "No model loaded.";
			return;
		}

		int triangleCount = _viewerControl.Roots[partIndex].Mesh.Triangles.Count;
		string lodNote = _lodSelector.Visible && _lodSelector.SelectedIndex >= 0
			? $", detail level {_lodSelector.SelectedIndex} of {_lodSelector.Items.Count}"
			: "";
		_statusLabel.Text =
			$"Loaded {_loadedDisplayName} — part {partIndex} of {_viewerControl.Roots.Count}{lodNote}, {triangleCount} triangle(s).";
	}
}
