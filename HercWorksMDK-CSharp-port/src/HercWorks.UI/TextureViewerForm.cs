using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Vol;

namespace HercWorks.UI;

/// <summary>
/// View-only window for DBA/DBM textures — ImageExportForm already covers PNG export, so this is
/// just preview + palette selection. DBA/DBM never bind their own palette (see DynamixBitmap's doc
/// comment: "the game binaries seem to know which one has which"), and empirically there's no
/// reliable same-name .DPL for most textures (e.g. mech textures in simvol0/dba share a handful of
/// generic simvol0/dpl palettes like WORLD0-9, none named after the mech) — so "automatic"
/// detection is a best-effort same-basename match, falling back to a dropdown of every .DPL found
/// nearby (same loaded VOL, or a sibling "dpl" folder for loose files) plus a manual Open Palette
/// escape hatch.
/// </summary>
public partial class TextureViewerForm : Form {
	private readonly DynamixBitmapArrayTransformer _dbaTransformer = new();
	private readonly DynamixBitmapTransformer _dbmTransformer = new();
	private readonly DynamixPaletteTransformer _dplTransformer = new();

	// Exactly one of these is populated after a successful load.
	private DynamixBitmapArray? _loadedDba;
	private DynamixBitmap? _loadedDbm;

	private DynamixPalette? _loadedPalette;
	private readonly List<(string Label, byte[] RawBytes)> _paletteCandidates = new();

	public TextureViewerForm() {
		InitializeComponent();
	}

	private void OnClose(object? sender, EventArgs e) => Close();

	private void OnFrameChanged(object? sender, EventArgs e) => RenderCurrentFrame();

	private DynamixBitmap[]? Frames => _loadedDba?.Images ?? (_loadedDbm != null ? new[] { _loadedDbm } : null);

	private void OnOpenImage(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "Dynamix bitmap files (*.dba;*.dbm)|*.dba;*.dbm|All files (*.*)|*.*",
			Title = "Open DBA or DBM file"
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			byte[] rawBytes = File.ReadAllBytes(dialog.FileName);
			var prefix = VolEntryPrefixCodec.StripIfPresent(rawBytes);

			if (prefix.Content.Length < 4) {
				MessageBox.Show(this, "File is too short to be a valid DBA/DBM.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			byte[] fileHeader = prefix.Content[..4];
			bool isArray = fileHeader.SequenceEqual(DynamixBitmapArray.HeaderMagic);
			bool isSingle = fileHeader.SequenceEqual(DynamixBitmap.HeaderMagic);

			if (!isArray && !isSingle) {
				MessageBox.Show(this,
					"File header doesn't match a known DBA or DBM magic value — this may not be one of those formats.",
					"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (isArray) {
				_loadedDba = (DynamixBitmapArray?)_dbaTransformer.BytesToObject(prefix.Content);
				_loadedDbm = null;
			} else {
				_loadedDbm = (DynamixBitmap?)_dbmTransformer.BytesToObject(prefix.Content);
				_loadedDba = null;
			}

			if (Frames is not { Length: > 0 }) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			LoadPaletteCandidatesFromDisk(dialog.FileName);
			FinishLoad(Path.GetFileName(dialog.FileName), dialog.FileName);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	/// <summary>
	/// Loads a texture still packed inside a loaded VOL — e.g. from MainForm's "View Asset"
	/// button. VolEntry.RawBytes is already clean content (no per-entry prefix to strip), and
	/// entry.Ext reliably says which format it is (VOL folders are organized by extension), so
	/// this skips the magic-byte sniffing the loose-file path needs.
	/// </summary>
	public void LoadFromVolEntry(VolEntry entry, Voln sourceVol) {
		try {
			if (entry.RawBytes is not { Length: > 0 }) {
				MessageBox.Show(this, "File has no data to parse.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (entry.Ext == FileType.Dba) {
				_loadedDba = (DynamixBitmapArray?)_dbaTransformer.BytesToObject(entry.RawBytes);
				_loadedDbm = null;
			} else if (entry.Ext == FileType.Dbm) {
				_loadedDbm = (DynamixBitmap?)_dbmTransformer.BytesToObject(entry.RawBytes);
				_loadedDba = null;
			} else {
				MessageBox.Show(this, "Selected entry is not a DBA or DBM texture.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (Frames is not { Length: > 0 }) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			LoadPaletteCandidatesFromVol(sourceVol);
			FinishLoad(entry.FileName ?? "(texture asset)", entry.FileName ?? "");
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnOpenPalette(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "Dynamix palette files (*.dpl)|*.dpl|All files (*.*)|*.*",
			Title = "Open DPL palette file"
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			byte[] rawBytes = File.ReadAllBytes(dialog.FileName);
			var prefix = VolEntryPrefixCodec.StripIfPresent(rawBytes);
			var palette = (DynamixPalette?)_dplTransformer.BytesToObject(prefix.Content);

			if (palette == null) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			string label = Path.GetFileName(dialog.FileName);
			_paletteCandidates.Add((label, prefix.Content));
			_paletteSelector.Items.Add(label);
			// Triggers OnPaletteSelectionChanged, which re-parses and re-renders.
			_paletteSelector.SelectedIndex = _paletteSelector.Items.Count - 1;
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load palette:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnPaletteSelectionChanged(object? sender, EventArgs e) {
		int index = _paletteSelector.SelectedIndex;
		if (index <= 0) {
			_loadedPalette = null;
		} else {
			var candidate = _paletteCandidates[index - 1]; // -1 offsets the leading "(None)"
			_loadedPalette = (DynamixPalette?)_dplTransformer.BytesToObject(candidate.RawBytes);
		}

		RenderCurrentFrame();
	}

	private void LoadPaletteCandidatesFromDisk(string textureFilePath) {
		_paletteCandidates.Clear();

		string? dir = Path.GetDirectoryName(textureFilePath);
		if (dir == null) {
			return;
		}

		var searchDirs = new List<string> { dir };

		// Real retail layouts keep DBA and DPL in sibling folders (e.g. simvol0/dba, simvol0/dpl)
		// rather than the same one, so check for that shape too.
		string? parent = Path.GetDirectoryName(dir);
		if (parent != null) {
			string siblingDpl = Path.Combine(parent, "dpl");
			if (Directory.Exists(siblingDpl)) {
				searchDirs.Add(siblingDpl);
			}
		}

		foreach (var searchDir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase)) {
			foreach (var file in Directory.GetFiles(searchDir, "*.dpl")) {
				_paletteCandidates.Add((Path.GetFileName(file), File.ReadAllBytes(file)));
			}
		}
	}

	private void LoadPaletteCandidatesFromVol(Voln vol) {
		_paletteCandidates.Clear();

		foreach (var folder in vol.Folders.Values) {
			foreach (var candidate in folder.Files) {
				if (candidate.Ext == FileType.Dpl && candidate.RawBytes is { Length: > 0 }) {
					_paletteCandidates.Add((candidate.FileName ?? "(unnamed)", candidate.RawBytes));
				}
			}
		}
	}

	private void FinishLoad(string sourceLabel, string paletteMatchName) {
		var frames = Frames!;
		_frameSelector.Minimum = 0;
		_frameSelector.Maximum = frames.Length - 1;
		_frameSelector.Value = 0;

		bool autoMatched = PopulatePaletteSelector(paletteMatchName);

		string kind = _loadedDba != null ? "DBA" : "DBM";
		string paletteNote = autoMatched
			? " — auto-matched a same-name palette."
			: _paletteCandidates.Count > 0
				? " — no matching palette found automatically, pick one from the dropdown."
				: " — no palettes found nearby to try.";
		_statusLabel.Text = $"Loaded {sourceLabel} ({kind}, {frames.Length} frame(s)){paletteNote}";

		RenderCurrentFrame();
	}

	/// <summary>Returns true if a same-basename candidate was found and auto-selected.</summary>
	private bool PopulatePaletteSelector(string textureFileName) {
		_paletteSelector.Items.Clear();
		_paletteSelector.Items.Add("(None)");

		string baseName = Path.GetFileNameWithoutExtension(textureFileName);
		int matchIndex = 0;

		foreach (var candidate in _paletteCandidates) {
			_paletteSelector.Items.Add(candidate.Label);
			if (matchIndex == 0 &&
				string.Equals(Path.GetFileNameWithoutExtension(candidate.Label), baseName, StringComparison.OrdinalIgnoreCase)) {
				matchIndex = _paletteSelector.Items.Count - 1;
			}
		}

		_paletteSelector.SelectedIndex = matchIndex;
		return matchIndex != 0;
	}

	private void RenderCurrentFrame() {
		var frames = Frames;
		if (frames == null || frames.Length == 0) {
			return;
		}

		int index = Math.Clamp((int)_frameSelector.Value, 0, frames.Length - 1);
		_preview.Image?.Dispose();
		_preview.Image = DynamixImageRenderer.RenderFrame(frames[index], _loadedPalette);
	}
}
