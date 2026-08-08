using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Vol;

namespace HercWorks.UI;

/// <summary>
/// Image export utility for Dynamix bitmap data — matches the image-conversion pieces of
/// ES2Excavator's original CLI (DBA -> separate DBM files, DBM(+DPL) -> colorized PNG, DPL -> a
/// grid PNG of its colors). Pure HercWorks.Core work; doesn't touch game data editing at all, so
/// it's grouped under a separate "Tools" menu in MainForm rather than "Edit". Follows the same
/// designer-split pattern as the editors, and the same VolEntryPrefixCodec-based retail-format
/// handling for whichever file (image or palette) is opened.
/// </summary>
public partial class ImageExportForm : Form {
	private readonly DynamixBitmapArrayTransformer _dbaTransformer = new();
	private readonly DynamixBitmapTransformer _dbmTransformer = new();
	private readonly DynamixPaletteTransformer _dplTransformer = new();

	// Exactly one of these is populated after a successful "Open Image".
	private DynamixBitmapArray? _loadedDba;
	private DynamixBitmap? _loadedDbm;

	private DynamixPalette? _loadedPalette;

	public ImageExportForm() {
		InitializeComponent();
		UpdateExportMenuState();
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

			var frames = Frames;
			if (frames == null || frames.Length == 0) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			_frameSelector.Minimum = 0;
			_frameSelector.Maximum = frames.Length - 1;
			_frameSelector.Value = 0;

			UpdateExportMenuState();
			RenderCurrentFrame();

			string kind = isArray ? "DBA" : "DBM";
			_statusLabel.Text = $"Loaded {Path.GetFileName(dialog.FileName)} ({kind}, {frames.Length} frame(s)).";
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

			_loadedPalette = palette;
			UpdateExportMenuState();
			RenderCurrentFrame();

			_statusLabel.Text = $"Loaded palette {Path.GetFileName(dialog.FileName)} ({palette.Colors.Count} colors).";
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load palette:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
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

	private void OnExportCurrentFrame(object? sender, EventArgs e) {
		var frames = Frames;
		if (frames == null || frames.Length == 0) {
			MessageBox.Show(this, "Open a DBA or DBM file first.", "Nothing to export",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var dialog = new SaveFileDialog {
			Filter = "PNG image (*.png)|*.png",
			Title = "Export current frame as PNG"
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			int index = Math.Clamp((int)_frameSelector.Value, 0, frames.Length - 1);
			using var bitmap = DynamixImageRenderer.RenderFrame(frames[index], _loadedPalette);
			DynamixImageRenderer.SaveAsPng(bitmap, dialog.FileName);

			MessageBox.Show(this, "Exported.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to export:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnExportAllFrames(object? sender, EventArgs e) {
		if (_loadedDba?.Images is not { Length: > 0 } frames) {
			MessageBox.Show(this, "Open a multi-frame DBA file first.", "Nothing to export",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var dialog = new FolderBrowserDialog { Description = "Choose a destination folder for the PNG frames" };
		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			for (int i = 0; i < frames.Length; i++) {
				using var bitmap = DynamixImageRenderer.RenderFrame(frames[i], _loadedPalette);
				string name = string.IsNullOrEmpty(frames[i].FileName) ? $"frame_{i}" : frames[i].FileName!;
				DynamixImageRenderer.SaveAsPng(bitmap, Path.Combine(dialog.SelectedPath, name + ".png"));
			}

			MessageBox.Show(this, $"Exported {frames.Length} frame(s).", "Done",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to export:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnUnpackToDbm(object? sender, EventArgs e) {
		if (_loadedDba?.Images is not { Length: > 0 } frames) {
			MessageBox.Show(this, "Open a multi-frame DBA file first.", "Nothing to unpack",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var dialog = new FolderBrowserDialog { Description = "Choose a destination folder for the .DBM files" };
		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			var unpacker = new DynamixBitmapTransformer();
			for (int i = 0; i < frames.Length; i++) {
				byte[] dbmBytes = unpacker.ObjectToBytes(frames[i])!;
				string name = string.IsNullOrEmpty(frames[i].FileName) ? $"frame_{i}" : frames[i].FileName!;
				File.WriteAllBytes(Path.Combine(dialog.SelectedPath, name + ".DBM"), dbmBytes);
			}

			MessageBox.Show(this, $"Unpacked {frames.Length} DBM file(s).", "Done",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to unpack:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnExportPaletteGrid(object? sender, EventArgs e) {
		if (_loadedPalette == null) {
			MessageBox.Show(this, "Open a DPL palette file first.", "Nothing to export",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var dialog = new SaveFileDialog {
			Filter = "PNG image (*.png)|*.png",
			Title = "Export palette grid as PNG"
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			using var bitmap = DynamixImageRenderer.RenderPaletteGrid(_loadedPalette);
			DynamixImageRenderer.SaveAsPng(bitmap, dialog.FileName);

			MessageBox.Show(this, "Exported.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to export:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void UpdateExportMenuState() {
		bool hasFrames = Frames is { Length: > 0 };
		bool hasMultiFrameDba = _loadedDba?.Images is { Length: > 0 };

		_exportCurrentFrameMenuItem.Enabled = hasFrames;
		_exportAllFramesMenuItem.Enabled = hasMultiFrameDba;
		_unpackToDbmMenuItem.Enabled = hasMultiFrameDba;
		_exportPaletteGridMenuItem.Enabled = _loadedPalette != null;
	}
}
