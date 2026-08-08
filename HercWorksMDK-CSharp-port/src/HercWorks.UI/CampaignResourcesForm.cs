using HercWorks.Core.Data.File.Sav;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Vol;

namespace HercWorks.UI;

/// <summary>
/// Editor for a player's .sav file — scoped to just Salvage Total and the 5 Workshop Slots for
/// now. The rest of PlayerSave (weapon inventory, all 36 squadmates, the player pilot, herc bay
/// state, herc unlock flags) is far more deeply nested data that would need its own dedicated
/// editor(s) to do justice to; it's parsed and carried through unchanged on save rather than
/// exposed here. `PlayerSaveTransform.ObjectToBytes`'s herc-unlocks write bug (see
/// KNOWN_ISSUES.md) is now fixed, so herc-unlock editing could reasonably be added to this form
/// in the future — not done yet since it needs its own UI (a checklist of hercs), which is a
/// separate scope from this fix pass. Follows the same pattern as HercStatsForm/WeaponStatsForm:
/// works against a loose .sav file, uses the shared VolEntryPrefixCodec so exports stay
/// retail-compatible, and keeps layout in CampaignResourcesForm.Designer.cs for the WinForms
/// visual designer.
/// </summary>
public partial class CampaignResourcesForm : Form {
	private readonly PlayerSaveTransform _transformer = new();

	private PlayerSave? _loadedSave;
	private string? _loadedPath;

	/// <summary>Original VOL entry prefix, round-tripped on save — see VolEntryPrefixCodec.</summary>
	private byte? _originalCompressionType;
	private byte[]? _originalMagicPrefix;
	private bool _originalHadTrailingByte;

	public CampaignResourcesForm() {
		InitializeComponent();

		foreach (var combo in WorkshopCombos) {
			combo.Items.AddRange(WeaponLUT.Values().ToArray());
		}
	}

	private ComboBox[] WorkshopCombos => new[] {
		_workshopSlot1Combo, _workshopSlot2Combo, _workshopSlot3Combo, _workshopSlot4Combo, _workshopSlot5Combo
	};

	private void OnClose(object? sender, EventArgs e) => Close();

	private void OnOpen(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "Save files (*.sav)|*.sav|All files (*.*)|*.*",
			Title = "Open player save file"
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			byte[] rawBytes = File.ReadAllBytes(dialog.FileName);
			var prefix = VolEntryPrefixCodec.StripIfPresent(rawBytes);
			var save = (PlayerSave?)_transformer.BytesToObject(prefix.Content);

			if (save == null) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			_loadedSave = save;
			_salvageInput.Value = Math.Clamp(save.SalvageTotal, (int)_salvageInput.Minimum, (int)_salvageInput.Maximum);

			var combos = WorkshopCombos;
			for (int i = 0; i < combos.Length; i++) {
				combos[i].SelectedItem = save.WorkshopSlots[i];
			}

			_loadedPath = dialog.FileName;
			_originalCompressionType = prefix.HadPrefix ? prefix.CompressionType : null;
			_originalMagicPrefix = prefix.MagicPrefix;
			_originalHadTrailingByte = prefix.HadTrailingByte;

			string prefixNote = prefix.HadPrefix ? " (VOL entry prefix detected — will be preserved on save)" : "";
			_statusLabel.Text = $"Loaded {Path.GetFileName(dialog.FileName)}.{prefixNote}";
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnSaveAs(object? sender, EventArgs e) {
		if (_loadedSave == null) {
			MessageBox.Show(this, "Open a save file first.", "Nothing to save",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var dialog = new SaveFileDialog {
			Filter = "Save files (*.sav)|*.sav|All files (*.*)|*.*",
			Title = "Save player save file",
			FileName = _loadedPath == null ? "PLAYER.SAV" : Path.GetFileName(_loadedPath)
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			_loadedSave.SalvageTotal = (int)_salvageInput.Value;

			var combos = WorkshopCombos;
			for (int i = 0; i < combos.Length; i++) {
				if (combos[i].SelectedItem is WeaponLUT selected) {
					_loadedSave.WorkshopSlots[i] = selected;
				}
			}

			// Recalculate WorkshopSpace to match the edited slots (occupied slots are anything
			// other than WeaponLUT.None) — previously this was left at whatever value the file had
			// on load, so changing a slot's contents without this could leave WorkshopSpace
			// inconsistent with the slots actually written out.
			int occupiedSlots = _loadedSave.WorkshopSlots.Count(w => w.Id != WeaponLUT.None.Id);
			_loadedSave.WorkshopSpace = (short)(_loadedSave.WorkshopSlots.Length - occupiedSlots);

			byte[] content = _transformer.ObjectToBytes(_loadedSave)!;
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
				$"Saved in {formatNote}.",
				"Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to save file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}
}
